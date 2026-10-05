using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Events;

namespace Ustas.RimAI.Events
{
    public static class OngoingEventsUtil
    {
        // Adapter onto Core EventFilterPolicy (category + disabled def/instance + minor deny-list).
        private static bool IsEventFiltered(string defName, string instanceID, EventCategory? category, EventFilterSettings settings)
        {
            if (settings == null)
                return false;

            EventFilterCategory? coreCategory = category?.ToFilterCategory();

            string colonyId = null;
            if (!string.IsNullOrEmpty(instanceID))
            {
                var worldInfo = Find.World?.info;
                colonyId = worldInfo != null ? $"{worldInfo.seedString}_{worldInfo.persistentRandomValue}" : null;
            }

            return EventFilterPolicy.IsFiltered(
                defName,
                instanceID,
                coreCategory,
                settings.ShowQuestsEffective,
                settings.ShowMapConditionsEffective,
                settings.ShowThreatsEffective,
                settings.ShowSitePartsEffective,
                settings.IsTypeDisabled,
                colonyId,
                settings.IsEventInstanceDisabled);
        }

        // Get a small list of "ongoing" situations on this map right now.
        //
        // Priority order:
        // - Location: the site's visible parts, on a map that is not a player home.
        // - Threats: every hostile group ThreatTrackerComponent follows on this map,
        //   then the newest threat letter no tracked group owns, while it still
        //   points at a live threat.
        // - Game conditions: all active GameConditions on this map (solar flare, psychic drone, etc.).
        // - Quests: QuestManager-based, only quests that are ongoing and affect this map.
        public static List<OngoingEventSnapshot> GetOngoingEventsNow(
            Map map,
            int maxEvents = -1,
            int maxThreatScanBack = 30)
        {
            if (maxEvents < 0)
                maxEvents = EventsInteriorDefaults.DefaultMaxOngoingEvents;

            var result = new List<OngoingEventSnapshot>();
            if (map == null || Current.Game == null)
                return result;

            // 0) For non-home maps (quest sites, temporary maps), prepend current location info
            //    from SitePartDefs if available (e.g. "ancient mercenaries").
            if (!map.IsPlayerHome)
            {
                TryAddSitePartEvents(map, result, maxEvents);
            }

            // 1) Hostile groups on this map, then a threat letter none of them owns.
            if (result.Count < maxEvents)
            {
                TryAddActiveThreatsForMap(map, result, maxEvents);
            }
            if (result.Count < maxEvents)
            {
                TryAddMostRecentThreatLetter(map, result, maxEvents, maxThreatScanBack);
            }

            // 2) Active game conditions on this map (solar flare, psychic drone, heat wave, etc.)
            int remaining = maxEvents - result.Count;
            if (remaining > 0)
            {
                TryAddActiveGameConditionsForMap(map, result, remaining);
            }

            // 3) Ongoing quests that affect this map (refugees, guild members, etc.)
            if (result.Count < maxEvents)
            {
                TryAddOngoingQuestsForMap(map, result, maxEvents);
            }

            return result;
        }

        // For non-home maps attached to a Site, add a compact description of the
        // current location based on the SitePartDefs (e.g. bandit camp, ancient ruins).
        public static void TryAddSitePartEvents(Map map, List<OngoingEventSnapshot> result, int maxEvents)
        {
            if (map == null || result == null)
                return;

            if (result.Count >= maxEvents)
                return;

            MapParent parent = map.Parent;
            if (parent == null)
                return;

            // Only treat Site-based maps as special locations here.
            Site site = parent as Site;
            if (site == null)
                return;

            var parts = site.parts;
            if (parts == null || parts.Count == 0)
                return;

            foreach (var part in parts)
            {
                if (result.Count >= maxEvents)
                    break;
                // A hidden part is one the player has not been told about (the
                // ambush under a "peaceful" camp); naming it would spoil it.
                if (part == null || part.def == null || part.hidden)
                    continue;

                var def = part.def;

                // Check new filtering system using helper method
                if (IsEventFiltered(def.defName, null, EventCategory.SitePart, EventsMod.Settings))
                    continue;

                // The first part is the site itself, and the site's own label is
                // the name the player sees on the world map.
                string label = part == parts[0] ? (string)site.LabelCap : null;
                if (label.NullOrEmpty())
                    label = def.LabelCap;
                if (label.NullOrEmpty())
                {
                    label = def.label;
                }

                string desc = def.description ?? string.Empty;

                // Label: "[current location] ancient mercenaries"
                // Body:  "A hostile company of mercenaries hiding out in an ancient structure."
                result.Add(new OngoingEventSnapshot
                {
                    Kind = OngoingEventNormalizer.SitePartKind(def.defName),
                    SourceDefName = def.defName,
                    Label = OngoingEventNormalizer.FormatSitePartLabel(label),
                    Body = desc,
                    QuestDescription = string.Empty,
                    IsThreat = false,
                    Faction = site.Faction?.Name
                });
            }
        }


        // Quest side: use QuestManager, no letters.
        public static void TryAddOngoingQuestsForMap(Map map, List<OngoingEventSnapshot> result, int maxEvents)
        {
            if (Find.QuestManager == null)
                return;

            var quests = Find.QuestManager.ActiveQuestsListForReading;
            if (quests.NullOrEmpty())
                return;

            foreach (var quest in quests)
            {
                if (quest == null)
                    continue;

                // Skip endgame quests
                string rootDefName = quest.root?.defName;
                if (rootDefName != null && rootDefName.StartsWith("EndGame_"))
                    continue;

                // Check new filtering system using helper method
                string questDefName = quest.root?.defName;
                string questInstanceID = quest.id.ToString();
                if (IsEventFiltered(questDefName, questInstanceID, EventCategory.Quest, EventsMod.Settings))
                    continue;

                if (QuestLinkUtil.IsQuestHidden(quest))
                    continue;

                if (!QuestLinkUtil.IsQuestOngoing(quest))
                    continue;

                if (!QuestLinkUtil.QuestAffectsMap(quest, map))
                    continue;

                // Base label + age marker
                string label = QuestLinkUtil.TryGetQuestLabel(quest);
                string ageMarker = QuestLinkUtil.GetQuestAcceptedAgeMarker(quest);
                if (!ageMarker.NullOrEmpty())
                {
                    // e.g. "Pickles the Destitute [accepted ~1.3 days ago]"
                    label = label + " [" + ageMarker + "; quest is active and underway]";
                }

                // Add key pawn short names if available
                string pawnNames = QuestLinkUtil.GetQuestKeyPawnNames(quest);
                if (!pawnNames.NullOrEmpty())
                {
                    // e.g. "... | characters: Pickles" or multiple names
                    label = label + " | characters: " + pawnNames;
                }

                string desc = QuestLinkUtil.TryGetQuestDescription(quest);

                result.Add(new OngoingEventSnapshot
                {
                    Kind = EventsInteriorDefaults.QuestSnapshotKind,
                    SourceDefName = rootDefName,
                    QuestId = quest.id,
                    Label = label,
                    Body = desc,
                    QuestDescription = desc,
                    IsThreat = false,
                    Participants = pawnNames,
                    Deadline = ageMarker
                });

                if (result.Count >= maxEvents)
                    break;
            }
        }

        // Game conditions side: all active GameConditions on this map.
        // These are the same things shown in the top-right UI bar above the speed buttons.
        public static void TryAddActiveGameConditionsForMap(
            Map map,
            List<OngoingEventSnapshot> result,
            int maxToAdd)
        {
            if (maxToAdd <= 0 || map == null)
                return;

            var gcm = map.gameConditionManager;
            if (gcm == null)
                return;

            var conds = gcm.ActiveConditions;
            if (conds == null || conds.Count == 0)
                return;

            int added = 0;

            foreach (var cond in conds)
            {
                if (added >= maxToAdd)
                    break;
                if (cond == null || cond.def == null)
                    continue;

                // Skip game conditions that are explicitly hidden from the UI.
                // This corresponds to <displayOnUI>false</displayOnUI> in the GameConditionDef.
                if (!cond.def.displayOnUI)
                    continue;

                // Check new filtering system using helper method
                if (IsEventFiltered(cond.def.defName, null, EventCategory.MapCondition, EventsMod.Settings))
                    continue;

                // Use the instance's Label/Description
                string label = cond.Label;
                string body = cond.Description ?? string.Empty;

                result.Add(new OngoingEventSnapshot
                {
                    Kind = OngoingEventNormalizer.GameConditionKind(cond.def.defName),
                    SourceDefName = cond.def.defName,
                    Label = label,
                    Body = body,
                    QuestDescription = string.Empty,
                    IsThreat = false,
                    Deadline = FormatConditionDeadline(cond)
                });

                added++;
            }
        }

        static string FormatConditionDeadline(GameCondition cond)
        {
            if (cond == null || cond.Permanent || cond.TicksLeft <= 0)
                return string.Empty;
            int hours = cond.TicksLeft / 2500;
            if (hours <= 0)
                return "ending soon";
            return hours == 1 ? "~1 hour left" : "~" + hours + " hours left";
        }

        // Gets the current colony identifier for per-colony instance filtering. 
        // Shared between OngoingEventsUtil and EventFilterUI.
        public static string GetCurrentColonyId()
        {
            if (Current.Game == null)
                return null;

            var worldInfo = Find.World?.info;
            if (worldInfo == null)
                return null;

            return $"{worldInfo.seedString ?? ""}_{worldInfo.persistentRandomValue}";
        }

        // Every tracked hostile group on this map that the threat filter lets through.
        public static void TryAddActiveThreatsForMap(Map map, List<OngoingEventSnapshot> result, int maxEvents)
        {
            if (map == null || result == null || result.Count >= maxEvents)
                return;

            var tracker = ThreatTrackerComponent.Get();
            if (tracker == null)
                return;

            var snapshots = tracker.GetPromptSnapshotsForMap(map, int.MaxValue);
            for (int i = 0; i < snapshots.Count && result.Count < maxEvents; i++)
            {
                var snapshot = snapshots[i];
                if (IsEventFiltered(snapshot.SourceDefName, null, EventCategory.Threat, EventsMod.Settings))
                    continue;
                result.Add(snapshot);
            }
        }

        // The newest threat letter that no tracked group owns, while something it
        // points at is still a live threat on this map. Replaces the fixed
        // three-hour window: a letter is news for exactly as long as its threat is.
        public static void TryAddMostRecentThreatLetter(
            Map map,
            List<OngoingEventSnapshot> result,
            int maxEvents,
            int maxThreatScanBack)
        {
            if (map == null || result == null || result.Count >= maxEvents)
                return;

            var list = Find.Archive?.ArchivablesListForReading;
            if (list == null || list.Count == 0)
                return;

            var tracker = ThreatTrackerComponent.Get();
            if (tracker == null)
                return;

            int scanned = 0;
            for (int i = list.Count - 1; i >= 0 && scanned < maxThreatScanBack; i--, scanned++)
            {
                if (!(list[i] is Letter letter) || !ThreatLetterBinder.IsThreatLetter(letter))
                    continue;
                if (IsEventFiltered(letter.def.defName, null, EventCategory.Threat, EventsMod.Settings))
                    continue;
                if (tracker.TryGetUntrackedLetterSnapshot(letter, map, out var snapshot))
                {
                    result.Add(snapshot);
                    return;
                }
            }
        }
    }
}

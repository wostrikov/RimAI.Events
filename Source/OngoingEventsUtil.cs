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
        // Consumed from Core EventsInteriorDefaults (3 in-game hours * 2500 ticks).
        private static int ThreatLetterTimeoutTicks => EventsInteriorDefaults.ThreatLetterTimeoutTicks;

        // Adapter onto Core EventFilterPolicy — semantics unchanged.
        private static bool IsEventFiltered(string defName, string instanceID, EventCategory? category, EventFilterSettings settings)
        {
            if (settings == null)
                return false;

            EventFilterCategory? coreCategory = null;
            if (category.HasValue)
            {
                switch (category.Value)
                {
                    case EventCategory.Quest:
                        coreCategory = EventFilterCategory.Quest;
                        break;
                    case EventCategory.MapCondition:
                        coreCategory = EventFilterCategory.MapCondition;
                        break;
                    case EventCategory.Threat:
                        coreCategory = EventFilterCategory.Threat;
                        break;
                    case EventCategory.SitePart:
                        coreCategory = EventFilterCategory.SitePart;
                        break;
                }
            }

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
                settings.IsEventDefDisabled,
                colonyId,
                settings.IsEventInstanceDisabled);
        }

        // Get a small list of "ongoing" situations on this map right now.
        // Stateless: reads QuestManager + archive each time.
        //
        // Priority order:
        // - Threat letter: at most one most-recent red threat letter, only if isInDanger == true.
        // - Game conditions: all active GameConditions on this map (solar flare, psychic drone, etc.).
        // - Quests: QuestManager-based, only quests that are ongoing and affect this map.
        public static List<OngoingEventSnapshot> GetOngoingEventsNow(
            Map map,
            bool isInDanger,
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

            // 1) Single active threat letter (raid/siege), only if caller says we're in danger.
            if (isInDanger && result.Count < maxEvents)
            {
                TryAddMostRecentThreatLetter(result, maxEvents, maxThreatScanBack);
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
                if (part == null || part.def == null)
                    continue;

                var def = part.def;

                // Check new filtering system using helper method
                if (IsEventFiltered(def.defName, null, EventCategory.SitePart, EventsMod.Settings))
                    continue;

                string label = def.LabelCap;
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

        // Threat side: at most one most-recent red threat letter,
        // only if isInDanger == true, and only if it's not too old
        // (currently within 3 in-game hours).
        public static void TryAddMostRecentThreatLetter(
            List<OngoingEventSnapshot> result,
            int maxEvents,
            int maxThreatScanBack)
        {
            if (Find.Archive == null)
                return;

            var list = Find.Archive.ArchivablesListForReading;
            if (list == null || list.Count == 0)
                return;

            // Current in-game time for age computation
            int nowTicks = (Find.TickManager != null) ? Find.TickManager.TicksGame : -1;

            int count = list.Count;
            int scanned = 0;

            // Walk backwards: newest -> older, stop after the first suitable threat
            for (int i = count - 1; i >= 0 && scanned < maxThreatScanBack && result.Count < maxEvents; i--, scanned++)
            {
                IArchivable a = list[i];
                if (a == null)
                    continue;

                // Only Letters are interesting here
                if (!(a is Letter letter && letter.def != null))
                    continue;

                var def = letter.def;
                bool isThreatLetter = def == LetterDefOf.ThreatBig || def == LetterDefOf.ThreatSmall;
                if (!isThreatLetter)
                    continue;

                // Check new filtering system using helper method
                if (IsEventFiltered(def.defName, null, EventCategory.Threat, EventsMod.Settings))
                    continue;

                // Age filter: skip (and stop) if the newest threat is already too old.
                if (nowTicks >= 0 && ThreatLetterTimeoutTicks > 0)
                {
                    int createdTicks = 0;
                    try
                    {
                        createdTicks = a.CreatedTicksGame;
                    }
                    // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — archive adapters must not abort threat scan
                    catch (Exception ex)
                    {
                        RimAiLog.WarningOnce(RimAiLogCategory.Events, "[RimAI.Events] archive CreatedTicksGame failed: " + ex, a.GetHashCode());
                        createdTicks = 0;
                    }

                    if (createdTicks > 0)
                    {
                        int ageTicks = nowTicks - createdTicks;
                        if (ageTicks > ThreatLetterTimeoutTicks)
                        {
                            // This is already older than our timeout; since we're scanning from newest
                            // to oldest, all remaining threat letters will be even older.
                            break;
                        }
                    }
                }

                string label;
                string tooltip;
                try
                {
                    label = a.ArchivedLabel ?? string.Empty;
                    tooltip = a.ArchivedTooltip ?? string.Empty;
                }
                // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — archive label adapters must not abort threat scan
                catch (Exception ex)
                {
                    RimAiLog.WarningOnce(RimAiLogCategory.Events, "[RimAI.Events] archive label failed: " + ex, a.GetHashCode() ^ 7);
                    label = string.Empty;
                    tooltip = string.Empty;
                }

                var threat = EventsRaidMetadata.FromLetter(letter);
                result.Add(new OngoingEventSnapshot
                {
                    Kind = letter.GetType().Name,
                    SourceDefName = letter.def.defName,
                    Label = label,
                    Body = tooltip,
                    QuestDescription = string.Empty,
                    IsThreat = true,
                    Faction = threat.Faction,
                    ArrivalMethod = threat.ArrivalMethod,
                    Motive = threat.Motive,
                    Participants = threat.Participants,
                    Deadline = threat.Deadline
                });

                break; // only one threat event

            }

        }
    }
}

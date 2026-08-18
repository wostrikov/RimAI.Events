using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Ustas.RimAI.Events
{
    using static EventFilterUIState;
    using static EventFilterUIChrome;

    internal static class EventFilterUIEventQueries
    {
    internal static List<FilterableEvent> GetAvailableEventTypes(bool currentOnly, EventFilterSettings settings)
    {
        var events = new List<FilterableEvent>();
        _typeSubtitles.Clear();

        if (currentOnly)
        {
            // Derive types from actual appendable instances using OngoingEventsUtil
            var appendableEvents = GetCurrentAppendableEvents(settings);
            var addedDefs = new HashSet<string>();

            foreach (var inst in appendableEvents)
            {
                if (!addedDefs.Add(inst.rootID))
                    continue;

                // Try to get proper label from def database based on category
                string displayName = inst.rootID;
                switch (inst.category)
                {
                    case EventCategory.Quest:
                        var questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(inst.rootID);
                        if (questDef != null && !questDef.LabelCap.NullOrEmpty())
                            displayName = questDef.LabelCap;
                        break;
                    case EventCategory.MapCondition:
                        var condDef = DefDatabase<GameConditionDef>.GetNamedSilentFail(inst.rootID);
                        if (condDef != null && !condDef.LabelCap.NullOrEmpty())
                            displayName = condDef.LabelCap;
                        break;
                    case EventCategory.SitePart:
                        var siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail(inst.rootID);
                        if (siteDef != null && !siteDef.LabelCap.NullOrEmpty())
                            displayName = siteDef.LabelCap;
                        break;
                    case EventCategory.Threat:
                        var letterDef = DefDatabase<LetterDef>.GetNamedSilentFail(inst.rootID);
                        if (letterDef != null && !letterDef.LabelCap.NullOrEmpty())
                            displayName = letterDef.LabelCap;
                        break;
                }

                events.Add(new FilterableEvent(
                    inst.rootID,
                    displayName,
                    null,
                    inst.category,
                    inst.sourceDefName
                ));

                // Capture subtitle for quests and threats
                if ((inst.category == EventCategory.Quest || inst.category == EventCategory.Threat)
                    && !inst.instanceName.NullOrEmpty())
                {
                    _typeSubtitles[inst.rootID] = inst.instanceName;
                }
            }

            // Also include disabled types that are not currently active,
            // so they still appear in the disabled column
            foreach (var disabledDefName in settings.disabledEventDefNames)
            {
                if (addedDefs.Contains(disabledDefName))
                    continue;

                // Try to find the def and determine its category
                string displayName = disabledDefName;
                EventCategory category = EventCategory.Quest; // Default fallback

                var questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(disabledDefName);
                if (questDef != null)
                {
                    if (!questDef.LabelCap.NullOrEmpty())
                        displayName = questDef.LabelCap;
                    category = EventCategory.Quest;
                }
                else
                {
                    var condDef = DefDatabase<GameConditionDef>.GetNamedSilentFail(disabledDefName);
                    if (condDef != null)
                    {
                        if (!condDef.LabelCap.NullOrEmpty())
                            displayName = condDef.LabelCap;
                        category = EventCategory.MapCondition;
                    }
                    else
                    {
                        var siteDef = DefDatabase<SitePartDef>.GetNamedSilentFail(disabledDefName);
                        if (siteDef != null)
                        {
                            if (!siteDef.LabelCap.NullOrEmpty())
                                displayName = siteDef.LabelCap;
                            category = EventCategory.SitePart;
                        }
                        else
                        {
                            var letterDef = DefDatabase<LetterDef>.GetNamedSilentFail(disabledDefName);
                            if (letterDef != null)
                            {
                                if (!letterDef.LabelCap.NullOrEmpty())
                                    displayName = letterDef.LabelCap;
                                category = EventCategory.Threat;
                            }
                        }
                    }
                }

                events.Add(new FilterableEvent(
                    disabledDefName,
                    displayName,
                    null,
                    category,
                    disabledDefName
                ));

                addedDefs.Add(disabledDefName);
            }
        }
        else
        {
            // Show all event types from def databases
            var questDefs = DefDatabase<QuestScriptDef>.AllDefsListForReading;
            if (questDefs != null)
            {
                foreach (var def in questDefs)
                {
                    if (def?.defName != null)
                    {
                        events.Add(new FilterableEvent(
                            def.defName,
                            (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                            null,
                            EventCategory.Quest,
                            def.defName
                        ));
                    }
                }
            }

            var conditionDefs = DefDatabase<GameConditionDef>.AllDefsListForReading;
            if (conditionDefs != null)
            {
                foreach (var def in conditionDefs)
                {
                    if (def?.defName != null && def.displayOnUI)
                    {
                        events.Add(new FilterableEvent(
                            def.defName,
                            (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                            null,
                            EventCategory.MapCondition,
                            def.defName
                        ));
                    }
                }
            }

            var sitePartDefs = DefDatabase<SitePartDef>.AllDefsListForReading;
            if (sitePartDefs != null)
            {
                foreach (var def in sitePartDefs)
                {
                    if (def?.defName != null)
                    {
                        events.Add(new FilterableEvent(
                            def.defName,
                            (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                            null,
                            EventCategory.SitePart,
                            def.defName
                        ));
                    }
                }
            }

            // Threat types: letters only (ThreatBig / ThreatSmall)
            var threatLetters = new[] { LetterDefOf.ThreatBig, LetterDefOf.ThreatSmall };
            foreach (var def in threatLetters)
            {
                if (def?.defName != null)
                {
                    events.Add(new FilterableEvent(
                        def.defName,
                        (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                        null,
                        EventCategory.Threat,
                        def.defName
                    ));
                }
            }
        }

        return events;
    }

    // Gets all currently appendable events for type-based filtering. 
    // Uses OngoingEventsUtil to match actual runtime behavior. 
    internal static List<FilterableEvent> GetCurrentAppendableEvents(EventFilterSettings settings)
    {
        var events = new List<FilterableEvent>();

        if (Current.Game == null)
            return events;

        Map currentMap = Find.CurrentMap;
        if (currentMap == null)
            return events;

        // Use OngoingEventsUtil to get the same events that would be appended to RimTalk
        bool isInDanger = currentMap.dangerWatcher?.DangerRating != StoryDanger.None;
        var ongoingEvents = OngoingEventsUtil.GetOngoingEventsNow(
            currentMap,
            isInDanger,
            maxEvents: int.MaxValue,
            maxThreatScanBack: 50
        );

        foreach (var evt in ongoingEvents)
        {
            if (evt == null || string.IsNullOrEmpty(evt.SourceDefName))
                continue;

            // Determine category from Kind
            EventCategory category = DetermineEventCategory(evt);

            // Extract clean label for display
            string instanceName = evt.Label;
            if (category == EventCategory.Quest)
            {
                // Extract base label (before any metadata like "[accepted ~1.3 days ago]")
                int bracketIdx = instanceName.IndexOf(" [");
                if (bracketIdx > 0)
                    instanceName = instanceName.Substring(0, bracketIdx);

                // Also remove " | characters:" suffix if present
                int charIdx = instanceName.IndexOf(" | characters:");
                if (charIdx > 0)
                    instanceName = instanceName.Substring(0, charIdx);
            }

            events.Add(new FilterableEvent(
                evt.SourceDefName,
                evt.SourceDefName,
                instanceName,
                category,
                evt.SourceDefName,
                null // No instance ID needed for type-based filtering
            ));
        }

        return events;
    }

    // Gets all current quest instances for instance-based filtering.
    // Only quests support instance-based filtering; other event types use type-based filtering only.
    internal static List<FilterableEvent> GetCurrentEventInstances(EventFilterSettings settings)
    {
        var instances = new List<FilterableEvent>();

        if (Current.Game == null)
            return instances;

        Map currentMap = Find.CurrentMap;
        if (currentMap == null)
            return instances;

        // Only quests support instance-based filtering
        if (Find.QuestManager == null)
            return instances;

        var quests = Find.QuestManager.QuestsListForReading;
        if (quests == null)
            return instances;

        foreach (var quest in quests)
        {
            if (quest == null ||
                quest.State != QuestState.Ongoing ||
                QuestLinkUtil.IsQuestHidden(quest))
                continue;

            // Skip global, not map-specific quests.
            if (quest.root != null && quest.root.isRootSpecial)
                continue;

            // Apply map-affinity filter to match OngoingEventsUtil behavior
            if (!QuestLinkUtil.QuestAffectsMap(quest, currentMap))
                continue;

            string questDefName = quest.root?.defName ?? "Unknown";
            string questLabel = QuestLinkUtil.TryGetQuestLabel(quest);
            string instanceID = quest.id.ToString();

            instances.Add(new FilterableEvent(
                questDefName,
                questDefName,
                questLabel,
                EventCategory.Quest,
                questDefName,
                instanceID
            ));
        }

        return instances;
    }

    // Determines the EventCategory from an OngoingEventSnapshot's Kind field.
    internal static EventCategory DetermineEventCategory(OngoingEventSnapshot evt)
    {
        if (evt.Kind == "Quest")
            return EventCategory.Quest;
        if (evt.Kind.StartsWith("GameCondition_", StringComparison.Ordinal))
            return EventCategory.MapCondition;
        if (evt.Kind.StartsWith("SitePart_", StringComparison.Ordinal))
            return EventCategory.SitePart;
        if (evt.IsThreat)
            return EventCategory.Threat;

        return EventCategory.Quest;
    }

    // Removes instance filters for events that are no longer active.
    internal static void CleanupInactiveInstances(EventFilterSettings settings)
    {
        if (settings.disabledEventInstances == null || settings.disabledEventInstances.Count == 0)
            return;

        if (Current.Game == null || Find.QuestManager == null)
            return;

        string colonyId = OngoingEventsUtil.GetCurrentColonyId();
        if (string.IsNullOrEmpty(colonyId))
            return;

        var instanceSet = settings.GetInstanceSet(colonyId);
        if (instanceSet == null || instanceSet.Count == 0)
        {
            PruneEmptyInstanceSet(settings, colonyId);
            return;
        }

        var activeInstanceIDs = new HashSet<string>();

        var quests = Find.QuestManager.QuestsListForReading;
        if (quests != null)
        {
            foreach (var quest in quests)
            {
                if (quest != null && quest.State == QuestState.Ongoing)
                {
                    activeInstanceIDs.Add(quest.id.ToString());
                }
            }
        }

        var toRemove = instanceSet.ids
            .Where(id => !activeInstanceIDs.Contains(id))
            .ToList();

        foreach (var instanceID in toRemove)
        {
            instanceSet.Remove(instanceID);
        }

        PruneEmptyInstanceSet(settings, colonyId);
    }

    // Helper to remove empty per-colony instance sets
    internal static void PruneEmptyInstanceSet(EventFilterSettings settings, string colonyId)
    {
        if (settings?.disabledEventInstances == null || string.IsNullOrEmpty(colonyId))
            return;

        if (settings.disabledEventInstances.TryGetValue(colonyId, out var set))
        {
            if (set == null || set.Count == 0)
            {
                settings.disabledEventInstances.Remove(colonyId);
            }
        }
    }
    }
}

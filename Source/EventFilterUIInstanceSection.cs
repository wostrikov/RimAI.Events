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

    internal static class EventFilterUIInstanceSection
    {
    internal static void DoInstanceBasedFilteringSection(Rect rect, EventFilterSettings settings)
    {
        EventFilterUIChrome.DrawSectionHeader(rect, "EventsMod_InstanceBasedFiltering", "EventsMod_InstanceBasedFiltering_Desc");

        float yPos = HEADER_HEIGHT + 30f;

        // Auto-cleanup:  Remove instances that are no longer active
        EventFilterUIEventQueries.CleanupInactiveInstances(settings);

        // Two-column layout
        var layout = new EventFilterUIChrome.TwoColumnLayout(rect, yPos);

        string colonyId = OngoingEventsUtil.GetCurrentColonyId();
        var instanceSet = settings.GetInstanceSet(colonyId);

        // Get current and hidden instances (filtered by current map)
        var allInstances = EventFilterUIEventQueries.GetCurrentEventInstances(settings);

        // Helper to check if category is disabled
        Func<FilterableEvent, bool> isCategoryDisabled = e =>
            (e.category == EventCategory.Quest && !settings.ShowQuestsEffective) ||
            (e.category == EventCategory.MapCondition && !settings.ShowMapConditionsEffective) ||
            (e.category == EventCategory.Threat && !settings.ShowThreatsEffective) ||
            (e.category == EventCategory.SitePart && !settings.ShowSitePartsEffective);

        var currentInstances = allInstances.Where(e =>
            (instanceSet == null || !instanceSet.Contains(e.instanceID)) &&
            !settings.disabledEventDefNames.Contains(e.rootID) &&
            !isCategoryDisabled(e)
        ).ToList();

        // Hidden instances include manually hidden, globally disabled, AND category disabled
        var hiddenInstances = new List<FilterableEvent>();
        hiddenInstances.AddRange(allInstances.Where(e => instanceSet != null && instanceSet.Contains(e.instanceID)));
        hiddenInstances.AddRange(allInstances.Where(e =>
            settings.disabledEventDefNames.Contains(e.rootID) &&
            (instanceSet == null || !instanceSet.Contains(e.instanceID))
        ));
        hiddenInstances.AddRange(allInstances.Where(e =>
            isCategoryDisabled(e) &&
            !settings.disabledEventDefNames.Contains(e.rootID) &&
            (instanceSet == null || !instanceSet.Contains(e.instanceID))
        ));

        // Draw columns
        DoEventInstanceColumn(layout.LeftColumn, "EventsMod_CurrentInstances".Translate(), currentInstances, settings, ref _scrollPosCurrentInstances, false);
        DoEventInstanceColumn(layout.RightColumn, "EventsMod_HiddenInstances".Translate(), hiddenInstances, settings, ref _scrollPosHiddenInstances, true);

        // Draw arrow buttons
        DoInstanceFilterButtons(layout.ButtonsArea, currentInstances, hiddenInstances, settings);
    }
    internal static void DoEventInstanceColumn(Rect rect, string title, List<FilterableEvent> events, EventFilterSettings settings, ref Vector2 scrollPos, bool isHidden)
    {
        Widgets.DrawMenuSection(rect);

        Rect titleRect = new Rect(rect.x, rect.y, rect.width, 25f);
        using (new EventFilterUIChrome.TextBlock(GameFont.Small, TextAnchor.MiddleCenter))
        {
            Widgets.Label(titleRect, title);
        }

        var groupedEvents = events.GroupBy(e => e.category).OrderBy(g => g.Key).ToList();

        float contentHeight = 0f;
        foreach (var group in groupedEvents)
        {
            contentHeight += 25f;
            foreach (var evt in group)
            {
                contentHeight += 25f;
                if (settings.disabledEventDefNames.Contains(evt.rootID))
                    contentHeight += 15f;
            }
        }

        Rect scrollRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
        contentHeight = EventFilterUIChrome.EnsureMinimumScrollHeight(contentHeight, scrollRect.height);

        Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);
        Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect, true);

        float yOffset = 0f;
        foreach (var group in groupedEvents)
        {
            yOffset += EventFilterUIChrome.DrawCategoryHeader(viewRect.width, yOffset, group.Key.ToString());

            foreach (var evt in group)
            {
                bool isGloballyDisabled = settings.disabledEventDefNames.Contains(evt.rootID);
                bool isCategoryDisabled =
                    (evt.category == EventCategory.Quest && !settings.ShowQuestsEffective) ||
                    (evt.category == EventCategory.MapCondition && !settings.ShowMapConditionsEffective) ||
                    (evt.category == EventCategory.Threat && !settings.ShowThreatsEffective) ||
                    (evt.category == EventCategory.SitePart && !settings.ShowSitePartsEffective);
                bool isDisabled = isGloballyDisabled || isCategoryDisabled;
                bool isSelected = isHidden ? (_selectedHiddenInstance == evt.instanceID) : (_selectedCurrentInstance == evt.instanceID);

                string displayText = string.IsNullOrEmpty(evt.instanceName)
                    ? evt.rootID
                    : $"{evt.instanceName} ({evt.rootID})";

                yOffset += EventFilterUIChrome.DrawSelectableItem(
                    viewRect.width,
                    yOffset,
                    displayText,
                    null,
                    isSelected && !isDisabled,
                    () =>
                    {
                        if (isHidden)
                            _selectedHiddenInstance = evt.instanceID;
                        else
                            _selectedCurrentInstance = evt.instanceID;
                    },
                    isDisabled);

                if (isDisabled)
                {
                    yOffset += EventFilterUIChrome.DrawDisabledNote(viewRect.width, yOffset);
                }
            }
        }

        Widgets.EndScrollView();
    }
    internal static void DoInstanceFilterButtons(Rect rect, List<FilterableEvent> current, List<FilterableEvent> hidden, EventFilterSettings settings)
    {
        float centerY = rect.y + rect.height / 2f;
        float centerX = rect.x + (rect.width - BUTTON_WIDTH) / 2f;

        string colonyId = OngoingEventsUtil.GetCurrentColonyId();

        Rect rightArrowRect = new Rect(centerX, centerY - 40f, BUTTON_WIDTH, 30f);
        bool canHide = CanHideInstance(_selectedCurrentInstance, current, settings);

        if (EventFilterUIChrome.DrawArrowButton(rightArrowRect, "→", canHide))
        {
            if (!string.IsNullOrEmpty(colonyId))
            {
                var instanceSet = settings.GetOrCreateInstanceSet(colonyId);
                instanceSet.Add(_selectedCurrentInstance);
            }
            _selectedCurrentInstance = null;
        }

        Rect leftArrowRect = new Rect(centerX, centerY + 10f, BUTTON_WIDTH, 30f);
        bool canUnhide = CanUnhideInstance(_selectedHiddenInstance, hidden, settings);

        if (EventFilterUIChrome.DrawArrowButton(leftArrowRect, "←", canUnhide))
        {
            if (!string.IsNullOrEmpty(colonyId))
            {
                var instanceSet = settings.GetInstanceSet(colonyId);
                if (instanceSet != null)
                {
                    instanceSet.Remove(_selectedHiddenInstance);
                    EventFilterUIEventQueries.PruneEmptyInstanceSet(settings, colonyId);
                }
            }
            _selectedHiddenInstance = null;
        }
    }

    // Helper to check if instance can be hidden
    internal static bool CanHideInstance(string instanceID, List<FilterableEvent> events, EventFilterSettings settings)
    {
        if (string.IsNullOrEmpty(instanceID))
            return false;

        string colonyId = OngoingEventsUtil.GetCurrentColonyId();
        if (string.IsNullOrEmpty(colonyId))
            return false;

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            if (evt.instanceID == instanceID)
                return !settings.disabledEventDefNames.Contains(evt.rootID);
        }

        return false;
    }

    // Helper to check if instance can be unhidden
    internal static bool CanUnhideInstance(string instanceID, List<FilterableEvent> events, EventFilterSettings settings)
    {
        if (string.IsNullOrEmpty(instanceID))
            return false;

        string colonyId = OngoingEventsUtil.GetCurrentColonyId();
        if (string.IsNullOrEmpty(colonyId))
            return false;

        for (int i = 0; i < events.Count; i++)
        {
            var evt = events[i];
            if (evt.instanceID == instanceID)
                return !settings.disabledEventDefNames.Contains(evt.rootID);
        }

        return false;
    }
    }
}

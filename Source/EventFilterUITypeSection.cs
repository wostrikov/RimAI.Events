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

    internal static class EventFilterUITypeSection
    {
    internal static void DoTypeBasedFilteringSection(Rect rect, EventFilterSettings settings)
    {
        EventFilterUIChrome.DrawSectionHeader(rect, "EventsMod_TypeBasedFiltering", "EventsMod_TypeBasedFiltering_Desc");
        EventFilterUIChrome.DrawSectionSearch(rect, _typeSearch);

        float yPos = HEADER_HEIGHT + 25f;

        // Radio buttons for event source selection
        DrawRadioButtons(rect, yPos);
        yPos += 70f;

        // Two-column layout
        var layout = new EventFilterUIChrome.TwoColumnLayout(rect, yPos);

        // Threats are shown or hidden only as a category; they have no types to list here.
        var allEvents = EventFilterUIEventQueries.GetAvailableEventTypes(_showCurrentEventsOnly, settings)
            .Where(e => EventFilterSettings.SupportsTypeFiltering(e.category))
            .Where(e => EventFilterUIChrome.MatchesSearch(_typeSearch, e, Subtitle(e)))
            .ToList();
        var availableEvents = allEvents.Where(e => !settings.IsTypeDisabled(e.category, e.rootID)).ToList();
        var disabledEvents = allEvents.Where(e => settings.IsTypeDisabled(e.category, e.rootID)).ToList();

        // Draw columns
        DoEventTypeColumn(layout.LeftColumn, "EventsMod_AvailableTypes".Translate(), availableEvents, settings, ref _scrollPosAvailableTypes, false);
        DoEventTypeColumn(layout.RightColumn, "EventsMod_DisabledTypes".Translate(), disabledEvents, settings, ref _scrollPosDisabledTypes, true);

        // Draw arrow buttons
        DoTypeFilterButtons(layout.ButtonsArea, availableEvents, disabledEvents, settings);
    }

    private static string Subtitle(FilterableEvent evt) =>
        evt.category == EventCategory.Quest && _typeSubtitles.TryGetValue(evt.rootID, out var subtitle) ? subtitle : null;

    // Helper to draw radio buttons for event source selection
    internal static void DrawRadioButtons(Rect parentRect, float yOffset)
    {
        float yPos = parentRect.y + yOffset;
        float leftMargin = parentRect.x + 10f;
        float radioSize = 24f;
        float labelGap = 8f;
        float optionSpacing = 300f;

        bool wasShowCurrentOnly = _showCurrentEventsOnly;

        string label1 = "EventsMod_CurrentEventsOnly".Translate();
        Rect radio1Rect = new Rect(leftMargin, yPos, radioSize, radioSize);

        if (Widgets.RadioButton(radio1Rect.x, radio1Rect.y, _showCurrentEventsOnly))
        {
            _showCurrentEventsOnly = true;
        }

        Rect label1Rect = new Rect(leftMargin + radioSize + labelGap, yPos, 200f, 30f);
        Widgets.Label(label1Rect, label1);
        if (Widgets.ButtonInvisible(new Rect(radio1Rect.x, yPos, radioSize + labelGap + Text.CalcSize(label1).x, 30f)))
        {
            _showCurrentEventsOnly = true;
            if (!wasShowCurrentOnly)
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
        }

        string label2 = "EventsMod_AllEventTypes".Translate();
        float option2X = leftMargin + optionSpacing;
        Rect radio2Rect = new Rect(option2X, yPos, radioSize, radioSize);

        if (Widgets.RadioButton(radio2Rect.x, radio2Rect.y, !_showCurrentEventsOnly))
        {
            _showCurrentEventsOnly = false;
        }

        Rect label2Rect = new Rect(option2X + radioSize + labelGap, yPos, 200f, 30f);
        Widgets.Label(label2Rect, label2);
        if (Widgets.ButtonInvisible(new Rect(radio2Rect.x, yPos, radioSize + labelGap + Text.CalcSize(label2).x, 30f)))
        {
            _showCurrentEventsOnly = false;
            if (wasShowCurrentOnly)
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
        }

        // Explanatory note below radio buttons
        Rect noteRect = new Rect(parentRect.x + 10f, parentRect.y + yOffset + 32f, parentRect.width - 20f, 30f);
        using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
        using (new EventFilterUIChrome.ColorBlock(new Color(0.7f, 0.7f, 0.7f)))
        {
            string note = _showCurrentEventsOnly
                ? "EventsMod_CurrentEventsNote".Translate()
                : "EventsMod_AllEventTypesNote".Translate();

            Widgets.Label(noteRect, note);
        }
    }
    internal static void DoEventTypeColumn(Rect rect, string title, List<FilterableEvent> events, EventFilterSettings settings, ref Vector2 scrollPos, bool isDisabled)
    {
        Widgets.DrawMenuSection(rect);

        Rect titleRect = new Rect(rect.x, rect.y, rect.width, 25f);
        using (new EventFilterUIChrome.TextBlock(GameFont.Small, TextAnchor.MiddleCenter))
        {
            Widgets.Label(titleRect, title);
        }

        var hiddenCategories = new List<EventCategory>();
        if (!settings.ShowQuestsEffective) hiddenCategories.Add(EventCategory.Quest);
        if (!settings.ShowMapConditionsEffective) hiddenCategories.Add(EventCategory.MapCondition);
        if (!settings.ShowSitePartsEffective) hiddenCategories.Add(EventCategory.SitePart);

        float categoryIndicatorHeight = (isDisabled && hiddenCategories.Count > 0) ? hiddenCategories.Count * 30f : 0f;

        var filteredEvents = events.Where(e => settings.IsCategoryShown(e.category)).ToList();

        var groupedEvents = filteredEvents.GroupBy(e => e.category).OrderBy(g => g.Key).ToList();

        float contentHeight = groupedEvents.Sum(g =>
        {
            float perGroup = 25f;
            foreach (var evt in g)
            {
                bool hasSubtitle = evt.category == EventCategory.Quest;
                perGroup += hasSubtitle ? 41f : 25f; // 25 base + 16 subtitle
            }
            return perGroup;
        }) + categoryIndicatorHeight;

        Rect scrollRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
        contentHeight = EventFilterUIChrome.EnsureMinimumScrollHeight(contentHeight, scrollRect.height);

        Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);
        Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect, true);

        float yOffset = 0f;

        if (isDisabled && hiddenCategories.Count > 0)
        {
            foreach (var category in hiddenCategories.OrderBy(c => c))
            {
                yOffset += EventFilterUIChrome.DrawCategoryFilterIndicator(viewRect.width, yOffset, category);
            }
        }

        foreach (var group in groupedEvents)
        {
            yOffset += EventFilterUIChrome.DrawCategoryHeader(viewRect.width, yOffset, group.Key.Label());

            foreach (var evt in group)
            {
                bool isSelected = isDisabled
                    ? (_selectedDisabledType == evt.rootID && _selectedDisabledCategory == evt.category)
                    : (_selectedAvailableType == evt.rootID && _selectedAvailableCategory == evt.category);
                string subtitle = Subtitle(evt);

                yOffset += EventFilterUIChrome.DrawSelectableItem(
                    viewRect.width,
                    yOffset,
                    evt.displayName,
                    subtitle,
                    isSelected,
                    () =>
                    {
                        if (isDisabled)
                        {
                            _selectedDisabledType = evt.rootID;
                            _selectedDisabledCategory = evt.category;
                        }
                        else
                        {
                            _selectedAvailableType = evt.rootID;
                            _selectedAvailableCategory = evt.category;
                        }
                    });
            }
        }

        Widgets.EndScrollView();
    }
    internal static void DoTypeFilterButtons(Rect rect, List<FilterableEvent> available, List<FilterableEvent> disabled, EventFilterSettings settings)
    {
        float centerY = rect.y + rect.height / 2f;
        float centerX = rect.x + (rect.width - BUTTON_WIDTH) / 2f;

        Rect rightArrowRect = new Rect(centerX, centerY - 40f, BUTTON_WIDTH, 30f);
        bool canDisable = !string.IsNullOrEmpty(_selectedAvailableType);

        if (EventFilterUIChrome.DrawArrowButton(rightArrowRect, "→", canDisable))
        {
            settings.DisableType(_selectedAvailableCategory, _selectedAvailableType);
            _selectedAvailableType = null;
        }

        Rect leftArrowRect = new Rect(centerX, centerY + 10f, BUTTON_WIDTH, 30f);
        bool canEnable = !string.IsNullOrEmpty(_selectedDisabledType);

        if (EventFilterUIChrome.DrawArrowButton(leftArrowRect, "←", canEnable))
        {
            settings.EnableType(_selectedDisabledCategory, _selectedDisabledType);
            _selectedDisabledType = null;
        }
    }
    }
}

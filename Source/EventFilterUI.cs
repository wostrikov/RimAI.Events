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

    public static class EventFilterUI
    {
        public static void DoFilteringUI(Rect inRect, EventFilterSettings settings)
        {
            if (settings == null)
                return;

            float totalContentHeight = _lastMeasuredTopSectionHeight
                + MIN_TYPE_SECTION_HEIGHT
                + SECTION_SPACING
                + MIN_INSTANCE_SECTION_HEIGHT
                + 80f;

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, totalContentHeight);
            Widgets.BeginScrollView(inRect, ref _scrollPosOuter, viewRect, true);

            float currentY = 0f;

            var listing = new Listing_Standard();
            listing.Begin(new Rect(0f, currentY, viewRect.width, 9999f));
            EventFilterUIOptimizationPanel.RenderOptimizationSection(listing, settings);
            EventFilterUIOptimizationPanel.RenderCategoryFiltersSection(listing, settings);
            _lastMeasuredTopSectionHeight = listing.CurHeight;
            listing.End();

            currentY += _lastMeasuredTopSectionHeight;

            Rect typeBasedRect = new Rect(0f, currentY, viewRect.width, MIN_TYPE_SECTION_HEIGHT);
            EventFilterUITypeSection.DoTypeBasedFilteringSection(typeBasedRect, settings);

            currentY += MIN_TYPE_SECTION_HEIGHT + SECTION_SPACING;

            DrawHorizontalDivider(viewRect.width, currentY - SECTION_SPACING / 2f - 5f);

            Rect instanceBasedRect = new Rect(0f, currentY, viewRect.width, MIN_INSTANCE_SECTION_HEIGHT);
            EventFilterUIInstanceSection.DoInstanceBasedFilteringSection(instanceBasedRect, settings);

            currentY += MIN_INSTANCE_SECTION_HEIGHT + 30f;

            DrawResetButtons(viewRect.width, currentY, settings);

            Widgets.EndScrollView();
        }

    internal static void DrawResetButtons(float viewWidth, float yPos, EventFilterSettings settings)
    {
        float buttonSpacing = 20f;
        float leftMargin = 10f;
        float buttonHeight = 30f;
        float buttonPadding = 20f;

        string resetTypeText = "EventsMod_ResetTypeFilters".Translate();
        string resetInstanceText = "EventsMod_ResetInstanceFilters".Translate();

        float button1Width = Text.CalcSize(resetTypeText).x + buttonPadding;
        float button2Width = Text.CalcSize(resetInstanceText).x + buttonPadding;

        Rect resetTypeButtonRect = new Rect(leftMargin, yPos, button1Width, buttonHeight);
        Rect resetInstanceButtonRect = new Rect(leftMargin + button1Width + buttonSpacing, yPos, button2Width, buttonHeight);

        if (Widgets.ButtonText(resetTypeButtonRect, resetTypeText))
        {
            int count = settings.disabledEventDefNames.Count;
            settings.disabledEventDefNames.Clear();

            SoundDefOf.Click.PlayOneShotOnCamera(null);
            Messages.Message(
                "EventsMod_TypeFiltersCleared".Translate(count),
                MessageTypeDefOf.PositiveEvent
            );
        }

        if (Widgets.ButtonText(resetInstanceButtonRect, resetInstanceText))
        {
            int totalCount = 0;
            if (settings.disabledEventInstances != null)
            {
                foreach (var kvp in settings.disabledEventInstances)
                {
                    if (kvp.Value != null)
                    {
                        totalCount += kvp.Value.Count;
                    }
                }

                settings.disabledEventInstances.Clear();
            }

            SoundDefOf.Click.PlayOneShotOnCamera(null);
            Messages.Message(
                "EventsMod_InstanceFiltersCleared".Translate(totalCount),
                MessageTypeDefOf.PositiveEvent
            );
        }
    }
    }
}

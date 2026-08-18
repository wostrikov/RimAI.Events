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

    internal static class EventFilterUIOptimizationPanel
    {
    internal static void RenderOptimizationSection(Listing_Standard listing, EventFilterSettings settings)
    {
        listing.Label("EventsMod_OptimizationHeader".Translate());
        listing.GapLine();

        // Event text compression toggle
        listing.CheckboxLabeled(
            "EventsMod_EnableCompression_Label".Translate(),
            ref settings.enableEventTextCompression,
            "EventsMod_EnableCompression_Tooltip".Translate()
        );

        using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
        {
            listing.Label("EventsMod_Compression_Description".Translate());
        }

        listing.Gap(12f);

        // Context-aware filtering toggle
        listing.CheckboxLabeled(
            "EventsMod_EnableContextFiltering".Translate(),
            ref settings.EnableContextFiltering,
            "EventsMod_EnableContextFilteringTooltip".Translate()
        );

        using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
        {
            listing.Label("EventsMod_ContextFiltering_Description".Translate());
        }

        listing.Gap(SECTION_SPACING);

        RenderAdvancedModeSection(listing, settings);
    }

    internal static void RenderAdvancedModeSection(Listing_Standard listing, EventFilterSettings settings)
    {
        listing.Label("EventsMod_AdvancedModeHeader".Translate());
        listing.GapLine();

        bool isAdvancedMode = EventsCommunicationIntegration.IsAdvancedModeEnabled;

        if (isAdvancedMode)
        {
            listing.CheckboxLabeled(
                "EventsMod_AppendToContext".Translate(),
                ref settings.AppendToContext,
                "EventsMod_AppendToContext_Tooltip".Translate()
            );
        }
        else
        {
            settings.AppendToContext = true;
            bool locked = true;
            using (new EventFilterUIChrome.ColorBlock(new Color(0.5f, 0.5f, 0.5f)))
            {
                listing.CheckboxLabeled(
                    "EventsMod_AppendToContext".Translate(),
                    ref locked,
                    "EventsMod_AppendToContext_Tooltip".Translate()
                );
            }
        }

        if (isAdvancedMode)
        {
            using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
            {
                listing.Label("EventsMod_AppendToContext_Desc".Translate());
            }
        }
        else
        {
            using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
            using (new EventFilterUIChrome.ColorBlock(new Color(0.5f, 0.5f, 0.5f)))
            {
                listing.Label("EventsMod_AppendToContext_Desc".Translate());
            }
        }

        if (EnhancedPromptDetector.IsLoaded)
        {
            listing.Gap(8f);
            listing.CheckboxLabeled(
                "EventsMod_MandatoryLockToggle".Translate(),
                ref settings.allowEnhancedPromptOverlap,
                "EventsMod_MandatoryLockToggle_Tooltip".Translate()
            );

            using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
            {
                listing.Label("EventsMod_MandatoryLockToggle_Desc".Translate());
            }
        }

        listing.Gap(SECTION_SPACING);
    }

    // Helper to render category filters section
    internal static void RenderCategoryFiltersSection(Listing_Standard listing, EventFilterSettings settings)
    {
        listing.GapLine();
        listing.Gap(10f);

        bool enhancedPromptConflict = settings.IsEnhancedPromptLockActive;

        if (enhancedPromptConflict)
        {
            DrawEnhancedPromptBanner(listing);
        }

        using (new EventFilterUIChrome.TextBlock(GameFont.Small))
        {
            listing.Label("EventsMod_CategoryFilters".Translate());
        }

        using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
        {
            listing.Label("EventsMod_CategoryFilters_Desc".Translate());
        }

        listing.Gap(5f);
        Rect categoryRect = listing.GetRect(FILTER_CHECKBOX_HEIGHT * 2 + 4f);
        DoQuickCategoryFilters(categoryRect, settings, enhancedPromptConflict);
        listing.Gap(10f);
        listing.GapLine();
        listing.Gap(10f);
    }

    internal static void DrawEnhancedPromptBanner(Listing_Standard listing)
    {
        Rect bannerRect = listing.GetRect(BANNER_HEIGHT);

        // Background - warm orange/yellow tint
        Color bgColor = new Color(0.30f, 0.24f, 0.10f, 0.95f);
        Widgets.DrawBoxSolid(bannerRect, bgColor);

        // Border
        Color borderColor = new Color(0.7f, 0.55f, 0.2f, 1f);
        using (new EventFilterUIChrome.ColorBlock(borderColor))
        {
            Widgets.DrawBox(bannerRect, 2);
        }

        Rect contentRect = bannerRect.ContractedBy(8f);

        // Title line
        Rect titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, 20f);
        using (new EventFilterUIChrome.TextBlock(GameFont.Small))
        using (new EventFilterUIChrome.ColorBlock(new Color(1f, 0.9f, 0.5f)))
        {
            Widgets.Label(titleRect, "EventsMod_EnhancedPromptDetected_Title".Translate());
        }

        // Description
        Rect descRect = new Rect(contentRect.x, contentRect.y + 22f, contentRect.width, contentRect.height - 22f);
        using (new EventFilterUIChrome.TextBlock(GameFont.Tiny))
        using (new EventFilterUIChrome.ColorBlock(new Color(0.85f, 0.8f, 0.65f)))
        {
            Widgets.Label(descRect, "EventsMod_EnhancedPromptDetected_Desc".Translate());
        }

        listing.Gap(8f);
    }
    internal static void DoQuickCategoryFilters(Rect rect, EventFilterSettings settings, bool enhancedPromptConflict = false)
    {
        float cellPadding = 4f;
        float cellWidth = (rect.width - cellPadding) / 2f;
        float cellHeight = (rect.height - cellPadding) / 2f;
        float boxPadding = 2f;
        int borderThickness = 1;
        float checkboxSize = 24f;
        float checkboxLabelGap = 6f;
        Color boxColor = new Color(0.18f, 0.18f, 0.18f, 0.35f);
        Color borderColor = Color.gray;
        Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

        void DrawCheckboxBox(Rect outerRect, string label, ref bool value, bool disabled = false)
        {
            // Draw box background and border
            Widgets.DrawBoxSolid(outerRect, boxColor);
            using (new EventFilterUIChrome.ColorBlock(borderColor))
            {
                Widgets.DrawBox(outerRect, borderThickness);
            }

            Rect innerRect = new Rect(
                outerRect.x + boxPadding,
                outerRect.y + boxPadding,
                outerRect.width - 2 * boxPadding,
                outerRect.height - 2 * boxPadding
            );

            // Checkbox position (left side, vertically centered)
            float checkboxY = innerRect.y + (innerRect.height - checkboxSize) / 2f;

            // Label area (to the right of checkbox)
            Rect labelRect = new Rect(
                innerRect.x + checkboxSize + checkboxLabelGap,
                innerRect.y,
                innerRect.width - checkboxSize - checkboxLabelGap,
                innerRect.height
            );

            using (new EventFilterUIChrome.TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            using (new EventFilterUIChrome.ColorBlock(disabled ? disabledColor : Color.white))
            {
                Widgets.Label(labelRect, label);
            }

            // Handle click on entire box area
            if (!disabled && Widgets.ButtonInvisible(outerRect))
            {
                value = !value;
                if (value)
                    SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
                else
                    SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);
            }

            // Draw checkbox visual only (no click handling)
            bool displayValue = disabled ? false : value;
            using (new EventFilterUIChrome.ColorBlock(disabled ? disabledColor : Color.white))
            {
                Widgets.CheckboxDraw(innerRect.x, checkboxY, displayValue, disabled, checkboxSize);
            }
        }

        DrawCheckboxBox(
            new Rect(rect.x, rect.y, cellWidth, cellHeight),
            "EventsMod_QuickFilter_Quests".Translate(),
            ref settings.showQuests,
            disabled: enhancedPromptConflict
        );
        DrawCheckboxBox(
            new Rect(rect.x + cellWidth + cellPadding, rect.y, cellWidth, cellHeight),
            "EventsMod_QuickFilter_MapConditions".Translate(),
            ref settings.showMapConditions,
            disabled: enhancedPromptConflict
        );
        DrawCheckboxBox(
            new Rect(rect.x, rect.y + cellHeight + cellPadding, cellWidth, cellHeight),
            "EventsMod_QuickFilter_Threats".Translate(),
            ref settings.showThreats,
            disabled: enhancedPromptConflict
        );
        DrawCheckboxBox(
            new Rect(rect.x + cellWidth + cellPadding, rect.y + cellHeight + cellPadding, cellWidth, cellHeight),
            "EventsMod_QuickFilter_Sites".Translate(),
            ref settings.showSiteParts,
            disabled: false  // Site Parts is Event+ exclusive, never override by Enhanced Prompt's settings
        );
    }
    }
}

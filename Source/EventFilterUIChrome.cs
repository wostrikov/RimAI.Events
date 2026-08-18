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
    internal static class EventFilterUIChrome
    {
    internal const float SECTION_SPACING = 30f;
    internal const float COLUMN_SPACING = 10f;
    internal const float BUTTON_WIDTH = 30f;
    internal const float HEADER_HEIGHT = 30f;
    internal const float FILTER_CHECKBOX_HEIGHT = 24f;

    // Fixed section heights to ensure visibility
    internal const float MIN_TYPE_SECTION_HEIGHT = 400f;
    internal const float MIN_INSTANCE_SECTION_HEIGHT = 400f;
    internal const float BANNER_HEIGHT = 80f;

    // Helper method to ensure minimum content height for scrollbars to function
    internal static float EnsureMinimumScrollHeight(float contentHeight, float scrollRectHeight)
    {
        float minContentHeight = scrollRectHeight + 1f;
        return contentHeight < minContentHeight ? minContentHeight : contentHeight;
    }
    internal static void DrawSectionHeader(Rect rect, string titleKey, string descKey)
    {
        Rect headerRect = new Rect(rect.x, rect.y, rect.width, HEADER_HEIGHT);
        using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
        {
            Widgets.Label(headerRect, titleKey.Translate());
        }

        Rect descRect = new Rect(rect.x, rect.y + HEADER_HEIGHT, rect.width, 20f);
        using (new TextBlock(GameFont.Tiny))
        {
            Widgets.Label(descRect, descKey.Translate());
        }
    }

    // Helper to draw horizontal divider
    internal static void DrawHorizontalDivider(float width, float yPos)
    {
        Widgets.DrawLineHorizontal(0f, yPos, width);
    }
    internal struct TwoColumnLayout
    {
        public readonly Rect LeftColumn;
        public readonly Rect RightColumn;
        public readonly Rect ButtonsArea;

        public TwoColumnLayout(Rect parentRect, float yOffset)
        {
            float columnWidth = (parentRect.width - COLUMN_SPACING - BUTTON_WIDTH * 2) / 2f;
            float columnHeight = parentRect.height - yOffset;

            LeftColumn = new Rect(parentRect.x, parentRect.y + yOffset, columnWidth, columnHeight);
            RightColumn = new Rect(parentRect.x + columnWidth + COLUMN_SPACING + BUTTON_WIDTH * 2, parentRect.y + yOffset, columnWidth, columnHeight);
            ButtonsArea = new Rect(parentRect.x + columnWidth + COLUMN_SPACING, parentRect.y + yOffset, BUTTON_WIDTH * 2, columnHeight);
        }
    }
    internal static float DrawCategoryHeader(float width, float yPos, string category)
    {
        Rect categoryRect = new Rect(0f, yPos, width, 25f);
        using (new TextBlock(GameFont.Tiny))
        using (new ColorBlock(Color.gray))
        {
            Widgets.Label(categoryRect, $"— {category} —");
        }
        return 25f;
    }

    // Helper to draw category filter indicator
    internal static float DrawCategoryFilterIndicator(float width, float yPos, EventCategory category)
    {
        Rect indicatorRect = new Rect(5f, yPos, width - 10f, 28f);

        Color bgColor = new Color(0.4f, 0.3f, 0.3f, 0.3f);
        Widgets.DrawBoxSolid(indicatorRect, bgColor);

        Rect labelRect = new Rect(10f, yPos + 2f, width - 20f, 24f);
        using (new TextBlock(GameFont.Tiny))
        using (new ColorBlock(new Color(0.7f, 0.7f, 0.7f)))
        {
            string label = $"All {category}s (hidden by category filter)";
            Widgets.Label(labelRect, label);
        }

        return 30f;
    }

    // Helper to draw selectable item
    internal static float DrawSelectableItem(float width, float yPos, string label, string subtitle, bool isSelected, Action onSelect, bool isDisabled = false)
    {
        float subtitleHeight = subtitle.NullOrEmpty() ? 0f : 16f;
        float rowHeight = 25f + subtitleHeight;

        Rect itemRect = new Rect(10f, yPos, width - 10f, rowHeight);

        if (isSelected)
            Widgets.DrawHighlight(itemRect);

        if (Mouse.IsOver(itemRect) && !isDisabled)
            Widgets.DrawLightHighlight(itemRect);

        using (new ColorBlock(isDisabled ? Color.gray : Color.white))
        {
            if (!isDisabled && Widgets.ButtonInvisible(itemRect))
                onSelect();

            Rect labelRect = new Rect(itemRect.x, itemRect.y, itemRect.width, 25f);
            Widgets.Label(labelRect, label);

            if (!subtitle.NullOrEmpty())
            {
                using (new TextBlock(GameFont.Tiny))
                using (new ColorBlock(new Color(0.7f, 0.7f, 0.7f)))
                {
                    Rect subRect = new Rect(itemRect.x, itemRect.y + 14f, itemRect.width, 16f);
                    string subtitleText = $"(e.g. {subtitle})";
                    Widgets.Label(subRect, subtitleText);
                }
            }
        }

        return rowHeight;
    }

    // Helper to draw "globally disabled" note
    internal static float DrawDisabledNote(float width, float yPos, string noteKey = "EventsMod_GloballyDisabled")
    {
        Rect noteRect = new Rect(20f, yPos, width - 20f, 15f);
        using (new TextBlock(GameFont.Tiny))
        using (new ColorBlock(Color.gray))
        {
            Widgets.Label(noteRect, "(" + noteKey.Translate() + ")");
        }
        return 15f;
    }

    internal static bool DrawArrowButton(Rect rect, string label, bool enabled)
    {
        if (!enabled)
        {
            using (new ColorBlock(Color.gray))
            {
                Widgets.ButtonText(rect, label);
            }
            return false;
        }

        return Widgets.ButtonText(rect, label);
    }
    internal struct TextBlock : IDisposable
    {
        private readonly GameFont _previousFont;
        private readonly TextAnchor _previousAnchor;
        private readonly bool _restoreAnchor;

        public TextBlock(GameFont font, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            _previousFont = Text.Font;
            _previousAnchor = Text.Anchor;
            _restoreAnchor = anchor != TextAnchor.UpperLeft;

            Text.Font = font;
            if (_restoreAnchor)
                Text.Anchor = anchor;
        }

        public void Dispose()
        {
            Text.Font = _previousFont;
            if (_restoreAnchor)
                Text.Anchor = _previousAnchor;
        }
    }

    // Helper struct for managing GUI.color state
    internal struct ColorBlock : IDisposable
    {
        private readonly Color _previousColor;

        public ColorBlock(Color color)
        {
            _previousColor = GUI.color;
            GUI.color = color;
        }

        public void Dispose()
        {
            GUI.color = _previousColor;
        }
    }
    }
}

using Ustas.RimAI.Core.Events;
using Verse;

namespace Ustas.RimAI.Events
{
    // Categories for filtering different types of events.
    public enum EventCategory
    {
        Quest,
        MapCondition,
        Threat,
        SitePart
    }

    public static class EventCategoryExtensions
    {
        public static EventFilterCategory ToFilterCategory(this EventCategory category)
        {
            switch (category)
            {
                case EventCategory.MapCondition: return EventFilterCategory.MapCondition;
                case EventCategory.Threat: return EventFilterCategory.Threat;
                case EventCategory.SitePart: return EventFilterCategory.SitePart;
                default: return EventFilterCategory.Quest;
            }
        }

        /// <summary>The category's name as the category filter checkboxes show it.</summary>
        public static string Label(this EventCategory category)
        {
            switch (category)
            {
                case EventCategory.MapCondition: return "EventsMod_QuickFilter_MapConditions".Translate();
                case EventCategory.Threat: return "EventsMod_QuickFilter_Threats".Translate();
                case EventCategory.SitePart: return "EventsMod_Category_Sites".Translate();
                default: return "EventsMod_QuickFilter_Quests".Translate();
            }
        }
    }
}

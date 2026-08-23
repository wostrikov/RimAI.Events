using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Core.Events;
using Verse;

namespace Ustas.RimAI.Events
{
    public static class OngoingEventsFormatter
    {
        public static string FormatOngoingEventsBlock(
            List<OngoingEventSnapshot> events,
            int maxChars = OngoingEventsPromptFormatter.DefaultMaxChars,
            bool includeWrapper = true)
        {
            if (events == null || events.Count == 0)
                return string.Empty;

            var lines = events.Select(e =>
            {
                var record = e?.ToRegistryRecord() ?? new OngoingEventRegistryRecord();
                record.Label = StripSimpleTags(record.Label);
                record.Body = StripSimpleTags(record.Body);
                record.Faction = StripSimpleTags(record.Faction);
                record.ArrivalMethod = StripSimpleTags(record.ArrivalMethod);
                record.Motive = StripSimpleTags(record.Motive);
                record.Participants = StripSimpleTags(record.Participants);
                record.Deadline = StripSimpleTags(record.Deadline);
                return EventsActiveRegistryPolicy.ToPromptLine(record);
            }).ToList();

            return OngoingEventsPromptFormatter.FormatBlock(lines, maxChars, includeWrapper);
        }

        private static string StripSimpleTags(string input)
        {
            if (input.NullOrEmpty())
                return string.Empty;

            return input.StripTags();
        }
    }
}

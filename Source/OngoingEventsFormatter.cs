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
                string body = e == null
                    ? string.Empty
                    : (!e.QuestDescription.NullOrEmpty() ? e.QuestDescription : e.Body);
                return new OngoingEventPromptLine
                {
                    Label = StripSimpleTags(e?.Label),
                    Body = StripSimpleTags(body),
                };
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

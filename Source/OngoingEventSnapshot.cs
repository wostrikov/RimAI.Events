using Ustas.RimAI.Core.Events;
using Verse;

namespace Ustas.RimAI.Events
{
    /// Represents one "ongoing" situation that the LLM should know about right now.
    public class OngoingEventSnapshot
    {
        /// DefName of the underlying quest root / incident / condition, if known.
        public string SourceDefName;

        /// Stable ID of the source quest, or -1 for non-quest events.
        /// Used to match a snapshot back to its exact active quest.
        public int QuestId = -1;

        /// Underlying type name, e.g. "Quest", "ChoiceLetter_ThreatBig".
        public string Kind;

        /// Short title.
        public string Label;

        /// Main body text.
        public string Body;

        /// Optional quest description (often same as Body for quests).
        public string QuestDescription;

        /// True if this is a threat-type event (raid, big danger).
        public bool IsThreat;

        public string Faction;
        public string ArrivalMethod;
        public string Motive;
        public string Participants;
        public string Deadline;

        public OngoingEventRegistryRecord ToRegistryRecord() =>
            new()
            {
                Label = Label ?? string.Empty,
                Body = !string.IsNullOrEmpty(QuestDescription) ? QuestDescription : (Body ?? string.Empty),
                Faction = Faction ?? string.Empty,
                ArrivalMethod = ArrivalMethod ?? string.Empty,
                Motive = Motive ?? string.Empty,
                Participants = Participants ?? string.Empty,
                Deadline = Deadline ?? string.Empty
            };
    }
}

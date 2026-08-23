using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Core.Events;
using Verse;

namespace Ustas.RimAI.Events
{
    /// <summary>
    /// Extracts raid/threat registry fields from a live letter.
    /// Unknown fields stay empty — the policy must not invent them.
    /// </summary>
    public static class EventsRaidMetadata
    {
        public static OngoingEventRegistryRecord FromLetter(Letter letter)
        {
            var record = new OngoingEventRegistryRecord();
            if (letter == null)
                return record;

            if (letter is ChoiceLetter choice && choice.relatedFaction != null)
                record.Faction = choice.relatedFaction.Name;

            var names = new List<string>();
            var targets = letter.lookTargets;
            if (targets != null)
            {
                foreach (var target in targets.targets)
                {
                    if (target.Thing is Pawn pawn && pawn.Faction != null)
                    {
                        if (string.IsNullOrEmpty(record.Faction))
                            record.Faction = pawn.Faction.Name;
                        string name = pawn.LabelShortCap;
                        if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                            names.Add(name);
                    }
                }
            }

            if (names.Count > 0)
                record.Participants = string.Join(", ", names);

            if (letter.def == LetterDefOf.ThreatBig)
                record.Motive = "raid";
            else if (letter.def == LetterDefOf.ThreatSmall)
                record.Motive = "threat";

            return record;
        }
    }
}

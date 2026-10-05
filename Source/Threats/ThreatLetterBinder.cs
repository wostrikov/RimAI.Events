using System;
using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Core.Diagnostics;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Ustas.RimAI.Events
{
    /// <summary>
    /// Ties threat letters to tracked threats. A letter belongs to a threat when
    /// it points at one of its pawns; a letter nobody owns is still described on
    /// its own while something it points at is a live threat on the map.
    /// </summary>
    internal static class ThreatLetterBinder
    {
        public static bool IsThreatLetter(Letter letter) =>
            letter != null && (letter.def == LetterDefOf.ThreatBig || letter.def == LetterDefOf.ThreatSmall);

        public static bool TargetsCore(Letter letter, ThreatRecordCore core)
        {
            var targets = letter?.lookTargets?.targets;
            if (targets == null || core?.lords == null)
                return false;
            for (int i = 0; i < targets.Count; i++)
            {
                if (!(targets[i].Thing is Pawn pawn))
                    continue;
                if (pawn.lord != null && core.lords.Contains(pawn.lord))
                    return true;
                for (int j = 0; j < core.lords.Count; j++)
                {
                    var owned = core.lords[j]?.ownedPawns;
                    if (owned != null && owned.Contains(pawn))
                        return true;
                }
            }
            return false;
        }

        public static void AttachTargeted(ThreatRecordCore core, List<Letter> letters)
        {
            if (core == null || letters == null)
                return;
            for (int i = 0; i < letters.Count; i++)
            {
                if (IsThreatLetter(letters[i]) && TargetsCore(letters[i], core))
                    core.OwnLetter(letters[i].ID);
            }
        }

        public static Letter FindTargeted(List<Letter> letters, ThreatRecordCore core)
        {
            if (letters == null || core == null)
                return null;
            for (int i = 0; i < letters.Count; i++)
            {
                if (IsThreatLetter(letters[i]) && TargetsCore(letters[i], core))
                    return letters[i];
            }
            return null;
        }

        public static Letter FindArchived(int letterId)
        {
            if (letterId <= 0)
                return null;
            var archived = Find.Archive?.ArchivablesListForReading;
            if (archived == null)
                return null;
            for (int i = 0; i < archived.Count; i++)
            {
                if (archived[i] is Letter letter && letter.ID == letterId)
                    return letter;
            }
            return null;
        }

        public static Letter FindOwnedArchived(ThreatRecordCore core)
        {
            if (core?.ownedLetterIds == null)
                return null;
            for (int i = 0; i < core.ownedLetterIds.Count; i++)
            {
                Letter letter = FindArchived(core.ownedLetterIds[i]);
                if (letter != null)
                    return letter;
            }
            return null;
        }

        /// <summary>
        /// Something the letter points at is on this map, visible, not leaving,
        /// and still a threat. This replaces a fixed three-hour age limit: a siege
        /// stays news for days, and a raid that broke and fled is over at once.
        /// </summary>
        public static bool HasLiveThreatTarget(Letter letter, Map map)
        {
            var targets = letter?.lookTargets?.targets;
            if (targets == null || targets.Count == 0)
                return false;
            for (int i = 0; i < targets.Count; i++)
            {
                Thing thing = targets[i].Thing;
                if (!(thing is IAttackTarget target) || thing.Destroyed || !thing.Spawned || thing.Map != map)
                    continue;
                if (thing is Pawn pawn)
                {
                    if (pawn.IsPsychologicallyInvisible() || pawn.IsHiddenFromPlayer())
                        continue;
                    LordToil toil = pawn.lord?.CurLordToil;
                    if (toil is LordToil_PanicFlee || toil is LordToil_ExitMap
                        || (toil != null && toil.GetType().Name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0))
                        continue;
                }
                if (GenHostility.IsActiveThreatToPlayer(target, canBeFogged: true))
                    return true;
            }
            return false;
        }

        /// <summary>The letter as the player read it, with whatever raid metadata it carries.</summary>
        public static OngoingEventSnapshot CreateSnapshot(Letter letter)
        {
            if (letter == null)
                return null;

            IArchivable archivable = letter;
            string label;
            string tooltip;
            try
            {
                label = archivable.ArchivedLabel ?? string.Empty;
                tooltip = archivable.ArchivedTooltip ?? string.Empty;
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — archive label adapters must not abort threat collection
            catch (Exception ex)
            {
                RimAiLog.WarningOnce(RimAiLogCategory.Events, "[RimAI.Events] archive label failed: " + ex, letter.GetHashCode() ^ 7);
                label = string.Empty;
                tooltip = string.Empty;
            }

            var threat = EventsRaidMetadata.FromLetter(letter);
            return new OngoingEventSnapshot
            {
                Kind = letter.GetType().Name,
                SourceDefName = letter.def?.defName,
                Label = label,
                Body = tooltip,
                QuestDescription = string.Empty,
                IsThreat = true,
                Faction = threat.Faction,
                ArrivalMethod = threat.ArrivalMethod,
                Motive = threat.Motive,
                Participants = threat.Participants,
                Deadline = threat.Deadline
            };
        }
    }
}

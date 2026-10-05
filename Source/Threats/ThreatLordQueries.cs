using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Core.Events;
using Verse;
using Verse.AI.Group;

namespace Ustas.RimAI.Events
{
    /// <summary>Questions about live lords that the threat tracker asks; nothing here is stored.</summary>
    internal static class ThreatLordQueries
    {
        public static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Still registered on a map; a lord removed from its manager is finished.</summary>
        public static bool IsActive(Lord lord) =>
            lord?.lordManager?.map != null && lord.lordManager.lords != null && lord.lordManager.lords.Contains(lord);

        /// <summary>An active lord with someone in it who is hostile to the player.</summary>
        public static bool IsRecordableThreat(Lord lord)
        {
            if (!IsActive(lord) || !lord.AnyActivePawn)
                return false;
            // Lords made while a game is still being created come before the player faction.
            Faction player = Faction.OfPlayerSilentFail;
            if (player == null)
                return false;
            if (lord.faction != null && lord.faction.HostileTo(player))
                return true;
            return HasHostileMember(lord, player);
        }

        public static List<Lord> CollectRecordable(List<Lord> candidates, Faction requiredFaction = null)
        {
            var result = new List<Lord>();
            if (candidates == null)
                return result;
            for (int i = 0; i < candidates.Count; i++)
            {
                Lord lord = candidates[i];
                if (IsRecordableThreat(lord) && !result.Contains(lord)
                    && (requiredFaction == null || ThreatFaction(lord) == requiredFaction))
                    result.Add(lord);
            }
            return result;
        }

        /// <summary>
        /// Sightstealers exist only with Anomaly; the DefOf is null without it,
        /// and a null kind must not match every pawn whose kind is also unset.
        /// </summary>
        public static bool IsSightstealer(Pawn pawn)
        {
            PawnKindDef kind = PawnKindDefOf.Sightstealer;
            return kind != null && pawn != null && pawn.kindDef == kind;
        }

        public static bool IsSightstealerLord(Lord lord)
        {
            if (lord == null)
                return false;
            if (lord.LordJob is LordJob_SightstealerAssault)
                return true;
            var pawns = lord.ownedPawns;
            if (pawns == null)
                return false;
            bool any = false;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                    continue;
                if (!IsSightstealer(pawn))
                    return false;
                any = true;
            }
            return any;
        }

        public static List<Lord> CollectSightstealerLords(List<Lord> candidates)
        {
            var result = new List<Lord>();
            if (candidates == null)
                return result;
            for (int i = 0; i < candidates.Count; i++)
            {
                Lord lord = candidates[i];
                if (IsRecordableThreat(lord) && IsSightstealerLord(lord) && !result.Contains(lord))
                    result.Add(lord);
            }
            return result;
        }

        /// <summary>The lord's faction if it is hostile, otherwise the first hostile member's.</summary>
        public static Faction ThreatFaction(Lord lord)
        {
            Faction player = Faction.OfPlayerSilentFail;
            if (player == null)
                return lord?.faction;
            if (lord?.faction != null && lord.faction.HostileTo(player))
                return lord.faction;
            var pawns = lord?.ownedPawns;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn?.Faction != null && pawn.HostileTo(player))
                        return pawn.Faction;
                }
            }
            return lord?.faction;
        }

        public static string FactionName(Faction faction, List<Lord> lords)
        {
            if (!string.IsNullOrEmpty(faction?.Name))
                return faction.Name;
            if (lords == null)
                return null;
            for (int i = 0; i < lords.Count; i++)
            {
                string name = ThreatFaction(lords[i])?.Name;
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return null;
        }

        public static bool HasLordOnMap(ThreatRecordCore core, Map map)
        {
            if (core?.lords == null)
                return false;
            for (int i = 0; i < core.lords.Count; i++)
            {
                Lord lord = core.lords[i];
                if (IsActive(lord) && lord.Map == map)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Some member is on the map and none of them is hidden from the player.
        /// An invisible member means the player does not know its group is there,
        /// and the prompt must not know more than the player does.
        /// </summary>
        public static bool IsSafeToDescribe(ThreatRecordCore core, Map map)
        {
            bool any = false;
            foreach (Pawn pawn in LivePawnsOn(core, map))
            {
                any = true;
                if (pawn.IsPsychologicallyInvisible() || pawn.IsHiddenFromPlayer())
                    return false;
            }
            return any;
        }

        public static bool HasVisibleSightstealer(ThreatRecordCore core, Map map)
        {
            foreach (Pawn pawn in LivePawnsOn(core, map))
            {
                if (IsSightstealer(pawn) && !pawn.IsPsychologicallyInvisible() && !pawn.IsHiddenFromPlayer())
                    return true;
            }
            return false;
        }

        public static int CountOwnedPawns(List<Lord> lords)
        {
            int count = 0;
            if (lords == null)
                return count;
            for (int i = 0; i < lords.Count; i++)
                count += lords[i]?.ownedPawns?.Count ?? 0;
            return count;
        }

        public static int CountLivePawns(List<Lord> lords)
        {
            int count = 0;
            if (lords == null)
                return count;
            for (int i = 0; i < lords.Count; i++)
            {
                var pawns = lords[i]?.ownedPawns;
                if (pawns == null)
                    continue;
                for (int j = 0; j < pawns.Count; j++)
                {
                    if (pawns[j] != null && !pawns[j].Dead)
                        count++;
                }
            }
            return count;
        }

        public static int CountActiveLords(List<Lord> lords)
        {
            int count = 0;
            if (lords == null)
                return count;
            for (int i = 0; i < lords.Count; i++)
            {
                if (IsActive(lords[i]))
                    count++;
            }
            return count;
        }

        /// <summary>The phase of each active lord - leaving, or what it is doing - combined.</summary>
        public static string DescribePhase(ThreatRecordCore core, bool mechCluster)
        {
            var phases = new List<string>();
            if (core?.lords != null)
            {
                for (int i = 0; i < core.lords.Count; i++)
                {
                    Lord lord = core.lords[i];
                    if (!IsActive(lord))
                        continue;
                    string toil = lord.CurLordToil?.GetType().Name;
                    string job = lord.LordJob?.GetType().Name;
                    CountDeparting(lord, out int alive, out int panicking, out int exiting);
                    string phase = ThreatPhasePolicy.DescribeDeparture(
                        ThreatPhasePolicy.Departure(toil, alive, panicking, exiting));
                    phase ??= mechCluster
                        ? ThreatPhasePolicy.DescribeMechClusterPhase(toil, job)
                        : ThreatPhasePolicy.DescribeLordPhase(toil, job);
                    phases.Add(phase);
                }
            }
            return ThreatPhasePolicy.Combine(phases);
        }

        private static void CountDeparting(Lord lord, out int alive, out int panicking, out int exiting)
        {
            alive = panicking = exiting = 0;
            var pawns = lord.ownedPawns;
            if (pawns == null)
                return;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                    continue;
                alive++;
                if (pawn.MentalStateDef == MentalStateDefOf.PanicFlee)
                    panicking++;
                string duty = pawn.mindState?.duty?.def?.defName;
                if (duty != null && duty.IndexOf("ExitMap", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    exiting++;
            }
        }

        private static IEnumerable<Pawn> LivePawnsOn(ThreatRecordCore core, Map map)
        {
            if (core?.lords == null)
                yield break;
            for (int i = 0; i < core.lords.Count; i++)
            {
                var pawns = core.lords[i]?.ownedPawns;
                if (pawns == null)
                    continue;
                for (int j = 0; j < pawns.Count; j++)
                {
                    Pawn pawn = pawns[j];
                    if (pawn != null && !pawn.Dead && pawn.Spawned && pawn.Map == map)
                        yield return pawn;
                }
            }
        }

        private static bool HasHostileMember(Lord lord, Faction player)
        {
            var pawns = lord?.ownedPawns;
            if (pawns == null)
                return false;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && !pawn.Dead && pawn.HostileTo(player))
                    return true;
            }
            return false;
        }
    }
}

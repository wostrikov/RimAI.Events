using System.Collections.Generic;
using System.Text;
using RimWorld;
using Ustas.RimAI.Core.Events;
using Verse;
using Verse.AI.Group;

namespace Ustas.RimAI.Events
{
    /// <summary>
    /// Turns tracked threats into prompt snapshots. A threat that is not safe to
    /// describe - one of its members is still hidden from the player - is shown
    /// only as its letter, so the prompt never knows more than the player.
    /// </summary>
    internal static class ThreatSnapshotBuilder
    {
        private const string MechClusterIncident = "MechCluster";
        private const string SightstealerSwarmIncident = "SightstealerSwarm";

        public static bool IsSightstealerSwarm(ThreatRecordCore core) => core?.incidentDefName == SightstealerSwarmIncident;

        public static RaidThreatRecord CreateRaid(ThreatCaptureContext capture)
        {
            List<Lord> lords = ThreatLordQueries.CollectRecordable(capture.lords, capture.parms?.faction);
            if (lords.Count == 0)
                return null;

            Faction faction = capture.parms?.faction ?? ThreatLordQueries.ThreatFaction(lords[0]);
            var record = new RaidThreatRecord
            {
                core = CreateCore(capture, lords, "Raid", "Raid"),
                factionName = ThreatLordQueries.FactionName(faction, lords),
                strategyLabel = DefLabel(capture.parms?.raidStrategy),
                arrivalDescription = Arrival(capture.parms),
                questName = capture.parms?.quest?.name,
                initialForceSize = ThreatLordQueries.CountOwnedPawns(lords),
                initialGroupCount = lords.Count
            };
            ThreatLetterBinder.AttachTargeted(record.core, capture.letters);
            return record;
        }

        public static GenericLordThreatRecord CreateGeneric(ThreatCaptureContext capture, List<Lord> lords)
        {
            if (lords == null || lords.Count == 0)
                return null;
            Faction faction = ThreatLordQueries.ThreatFaction(lords[0]);
            var record = new GenericLordThreatRecord
            {
                core = CreateCore(capture, lords, "GenericLordThreat", "Hostile group"),
                factionName = ThreatLordQueries.FactionName(faction, lords),
                initialForceSize = ThreatLordQueries.CountOwnedPawns(lords),
                initialGroupCount = lords.Count
            };
            if (capture != null)
                ThreatLetterBinder.AttachTargeted(record.core, capture.letters);
            return record;
        }

        public static SightstealerThreatRecord CreateSightstealers(ThreatCaptureContext capture, List<Lord> lords)
        {
            if (lords == null || lords.Count == 0)
                return null;
            var record = new SightstealerThreatRecord
            {
                core = CreateCore(capture, lords, "SightstealerThreat", "Sightstealer threat")
            };
            if (capture != null)
            {
                Letter warning = ThreatLetterBinder.FindTargeted(capture.letters, record.core);
                if (warning != null)
                    record.warningLetterId = warning.ID;
                ThreatLetterBinder.AttachTargeted(record.core, capture.letters);
            }
            else
            {
                record.disclosureObserved = ThreatLordQueries.HasVisibleSightstealer(record.core, lords[0].Map);
            }
            return record;
        }

        public static OngoingEventSnapshot Build(RaidThreatRecord record, Map map)
        {
            if (!TryDescribe(record, map, out OngoingEventSnapshot fallback))
                return fallback;

            var body = new StringBuilder();
            AppendLine(body, ThreatPromptPolicy.RaidTypeLine(record.strategyLabel ?? record.core.incidentLabel));
            AppendLine(body, ThreatPromptPolicy.OriginLine(record.questName));
            return Snapshot(record.core, ThreatPromptPolicy.RaidLabel(record.factionName), body,
                record.factionName, record.arrivalDescription, ThreatPromptPolicy.RaidMotive,
                ThreatPromptPolicy.RaidUnit, record.initialForceSize, record.initialGroupCount, mechCluster: false);
        }

        public static OngoingEventSnapshot Build(GenericLordThreatRecord record, Map map)
        {
            if (!TryDescribe(record, map, out OngoingEventSnapshot fallback))
                return fallback;

            var body = new StringBuilder();
            AppendLine(body, ThreatPromptPolicy.ThreatTypeLine(record.core.incidentLabel));
            return Snapshot(record.core, ThreatPromptPolicy.ThreatLabel(record.factionName), body,
                record.factionName, null, ThreatPromptPolicy.ThreatMotive,
                ThreatPromptPolicy.HostileUnit, record.initialForceSize, 0,
                mechCluster: record.core.incidentDefName == MechClusterIncident);
        }

        /// <summary>Until a sightstealer has been seen, only its warning letter is known.</summary>
        public static OngoingEventSnapshot Build(SightstealerThreatRecord record, Map map)
        {
            if (record?.core == null || !ThreatLordQueries.HasLordOnMap(record.core, map))
                return null;
            if (!record.disclosureObserved)
            {
                Letter warning = ThreatLetterBinder.FindArchived(record.warningLetterId);
                return warning != null ? ThreatLetterBinder.CreateSnapshot(warning) : null;
            }
            return new OngoingEventSnapshot
            {
                Kind = "Threat_" + record.core.incidentDefName,
                SourceDefName = record.core.incidentDefName,
                Label = ThreatPromptPolicy.SightstealerLabel,
                Body = ThreatPromptPolicy.SightstealerBody,
                QuestDescription = string.Empty,
                IsThreat = true,
                Motive = ThreatPromptPolicy.ThreatMotive
            };
        }

        private static bool TryDescribe(TrackedThreatRecord record, Map map, out OngoingEventSnapshot fallback)
        {
            fallback = null;
            if (record?.core == null || !ThreatLordQueries.HasLordOnMap(record.core, map))
                return false;
            if (ThreatLordQueries.IsSafeToDescribe(record.core, map))
                return true;
            Letter letter = ThreatLetterBinder.FindOwnedArchived(record.core);
            fallback = letter != null ? ThreatLetterBinder.CreateSnapshot(letter) : null;
            return false;
        }

        private static OngoingEventSnapshot Snapshot(
            ThreatRecordCore core, string label, StringBuilder body, string faction, string arrival,
            string motive, string unit, int initialPawns, int initialGroups, bool mechCluster)
        {
            int alive = ThreatLordQueries.CountLivePawns(core.lords);
            int groups = ThreatLordQueries.CountActiveLords(core.lords);
            string initial = ThreatPromptPolicy.InitialForce(initialPawns, initialGroups, unit);
            return new OngoingEventSnapshot
            {
                Kind = "Threat_" + core.incidentDefName,
                SourceDefName = core.incidentDefName,
                Label = label,
                Body = body.ToString(),
                QuestDescription = string.Empty,
                IsThreat = true,
                Faction = faction,
                ArrivalMethod = arrival,
                Motive = motive,
                Force = ThreatPromptPolicy.Force(alive, groups, unit, initial),
                Phase = ThreatLordQueries.DescribePhase(core, mechCluster)
            };
        }

        private static ThreatRecordCore CreateCore(ThreatCaptureContext capture, List<Lord> lords, string fallbackDefName, string fallbackLabel) =>
            new ThreatRecordCore
            {
                lords = lords ?? new List<Lord>(),
                incidentDefName = capture?.IncidentDefName ?? fallbackDefName,
                incidentLabel = capture?.IncidentLabel ?? fallbackLabel,
                createdTick = ThreatLordQueries.CurrentTick
            };

        private static string DefLabel(Def def)
        {
            if (def == null)
                return null;
            return string.IsNullOrEmpty(def.label) ? def.defName : def.label.CapitalizeFirst();
        }

        private static string Arrival(IncidentParms parms)
        {
            string direction = null;
            if (parms?.target is Map map && parms.spawnCenter.IsValid)
            {
                IntVec3 center = map.Center;
                direction = ThreatPromptPolicy.Direction(
                    parms.spawnCenter.x - center.x, parms.spawnCenter.z - center.z, map.Size.x, map.Size.z);
            }
            return ThreatPromptPolicy.Arrival(DefLabel(parms?.raidArrivalMode), direction);
        }

        private static void AppendLine(StringBuilder body, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (body.Length > 0)
                body.AppendLine();
            body.Append(text);
        }
    }
}

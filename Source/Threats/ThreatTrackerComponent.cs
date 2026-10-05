using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Ustas.RimAI.Events
{
    /// <summary>
    /// Every hostile group on the player's maps, from the incident that brought it
    /// until its lord is gone. Replaces reading "the newest threat letter younger
    /// than three hours": a threat now lasts exactly as long as its lords do, and
    /// is described from what they are doing rather than what the letter said.
    /// </summary>
    public class ThreatTrackerComponent : GameComponent
    {
        /// <summary>How often letters that arrived after their threat are matched to it.</summary>
        private const int ArchiveBindingIntervalTicks = 2500;

        private List<RaidThreatRecord> raidThreats = new List<RaidThreatRecord>();
        private List<SightstealerThreatRecord> sightstealerThreats = new List<SightstealerThreatRecord>();
        private List<GenericLordThreatRecord> genericLordThreats = new List<GenericLordThreatRecord>();
        private int nextArchiveBindingTick;
        private int lastPruneTick = -1;

        public ThreatTrackerComponent(Game game)
        {
        }

        internal static ThreatTrackerComponent Get() => Current.Game?.GetComponent<ThreatTrackerComponent>();

        public override void ExposeData()
        {
            EnsureCollections();
            // A removed lord saved as a reference is a cross-reference the loader cannot resolve.
            if (Scribe.mode == LoadSaveMode.Saving)
                PruneInvalidRecords(force: true);
            Scribe_Collections.Look(ref raidThreats, "raidThreats", LookMode.Deep);
            Scribe_Collections.Look(ref sightstealerThreats, "sightstealerThreats", LookMode.Deep);
            Scribe_Collections.Look(ref genericLordThreats, "genericLordThreats", LookMode.Deep);
            EnsureCollections();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            EnsureCollections();
            raidThreats.Clear();
            sightstealerThreats.Clear();
            genericLordThreats.Clear();
            nextArchiveBindingTick = 0;
            lastPruneTick = -1;
        }

        /// <summary>
        /// A save from before this tracker, or one whose lords outlived a record,
        /// has hostile groups nobody tracks; adopt them as they are.
        /// </summary>
        public override void LoadedGame()
        {
            base.LoadedGame();
            EnsureCollections();
            PruneInvalidRecords(force: true);
            DiscoverUntrackedLords();
            BindArchiveLetters(force: true);
        }

        internal void RegisterCapturedThreat(ThreatCaptureContext capture)
        {
            if (capture == null)
                return;
            EnsureCollections();
            PruneInvalidRecords();

            var sightstealers = ThreatLordQueries.CollectSightstealerLords(capture.lords);
            if (sightstealers.Count > 0)
            {
                Add(sightstealerThreats, ThreatSnapshotBuilder.CreateSightstealers(capture, sightstealers));
                return;
            }
            if (capture.IsRaid)
            {
                Add(raidThreats, ThreatSnapshotBuilder.CreateRaid(capture));
                return;
            }
            Add(genericLordThreats, ThreatSnapshotBuilder.CreateGeneric(capture, ThreatLordQueries.CollectRecordable(capture.lords)));
        }

        /// <summary>A hostile lord made outside any incident - a quest's ambush, a mod's own spawner.</summary>
        internal void RegisterUnscopedLord(Lord lord)
        {
            if (!ThreatLordQueries.IsRecordableThreat(lord))
                return;
            EnsureCollections();
            PruneInvalidRecords();
            if (!IsTracked(lord))
                RouteUnscoped(lord);
        }

        internal void NotifySightstealerBecameVisible(Pawn pawn)
        {
            if (pawn?.lord == null)
                return;
            EnsureCollections();
            PruneInvalidRecords();
            SightstealerThreatRecord record = FindRecord(sightstealerThreats, pawn.lord);
            if (record == null)
            {
                RegisterUnscopedLord(pawn.lord);
                record = FindRecord(sightstealerThreats, pawn.lord);
            }
            if (record != null)
                record.disclosureObserved = true;
        }

        internal void NotifyLordRemoved(Lord lord)
        {
            if (lord == null)
                return;
            EnsureCollections();
            RemoveLord(raidThreats, lord);
            RemoveLord(sightstealerThreats, lord);
            RemoveLord(genericLordThreats, lord);
        }

        public List<OngoingEventSnapshot> GetPromptSnapshotsForMap(Map map, int maxToAdd)
        {
            var result = new List<OngoingEventSnapshot>();
            if (map == null || maxToAdd <= 0)
                return result;
            EnsureCollections();
            PruneInvalidRecords();
            BindArchiveLetters(force: false);

            for (int i = 0; i < sightstealerThreats.Count && result.Count < maxToAdd; i++)
                AddSnapshot(result, ThreatSnapshotBuilder.Build(sightstealerThreats[i], map));
            for (int i = 0; i < raidThreats.Count && result.Count < maxToAdd; i++)
                AddSnapshot(result, ThreatSnapshotBuilder.Build(raidThreats[i], map));
            for (int i = 0; i < genericLordThreats.Count && result.Count < maxToAdd; i++)
                AddSnapshot(result, ThreatSnapshotBuilder.Build(genericLordThreats[i], map));
            return result;
        }

        /// <summary>
        /// A threat letter no tracked threat owns, described on its own while it
        /// still points at a live threat on this map.
        /// </summary>
        public bool TryGetUntrackedLetterSnapshot(Letter letter, Map map, out OngoingEventSnapshot snapshot)
        {
            snapshot = null;
            if (!ThreatLetterBinder.IsThreatLetter(letter) || map == null)
                return false;
            EnsureCollections();
            PruneInvalidRecords();
            if (OwnsLetter(letter.ID) || IsTargetedByTracked(letter))
                return false;
            if (!ThreatLetterBinder.HasLiveThreatTarget(letter, map))
                return false;
            snapshot = ThreatLetterBinder.CreateSnapshot(letter);
            return snapshot != null;
        }

        private void RouteUnscoped(Lord lord)
        {
            var lords = new List<Lord> { lord };
            if (ThreatLordQueries.IsSightstealerLord(lord))
                Add(sightstealerThreats, ThreatSnapshotBuilder.CreateSightstealers(null, lords));
            else
                Add(genericLordThreats, ThreatSnapshotBuilder.CreateGeneric(null, lords));
        }

        private void DiscoverUntrackedLords()
        {
            var maps = Find.Maps;
            if (maps == null)
                return;
            for (int i = 0; i < maps.Count; i++)
            {
                var lords = maps[i]?.lordManager?.lords;
                if (lords == null)
                    continue;
                for (int j = 0; j < lords.Count; j++)
                {
                    if (ThreatLordQueries.IsRecordableThreat(lords[j]) && !IsTracked(lords[j]))
                        RouteUnscoped(lords[j]);
                }
            }
        }

        private void BindArchiveLetters(bool force)
        {
            int now = ThreatLordQueries.CurrentTick;
            if (!force && now < nextArchiveBindingTick)
                return;
            var archived = Find.Archive?.ArchivablesListForReading;
            if (archived == null)
                return;
            for (int i = 0; i < archived.Count; i++)
            {
                if (!(archived[i] is Letter letter) || !ThreatLetterBinder.IsThreatLetter(letter))
                    continue;
                if (!BindToSightstealers(letter) && !BindTo(raidThreats, letter))
                    BindTo(genericLordThreats, letter);
            }
            nextArchiveBindingTick = now + ArchiveBindingIntervalTicks;
        }

        private bool BindToSightstealers(Letter letter)
        {
            for (int i = 0; i < sightstealerThreats.Count; i++)
            {
                SightstealerThreatRecord record = sightstealerThreats[i];
                if (record == null || !ThreatLetterBinder.TargetsCore(letter, record.core))
                    continue;
                if (record.warningLetterId == 0 && ThreatSnapshotBuilder.IsSightstealerSwarm(record.core))
                    record.warningLetterId = letter.ID;
                record.core.OwnLetter(letter.ID);
                return true;
            }
            return false;
        }

        private static bool BindTo<T>(List<T> records, Letter letter) where T : TrackedThreatRecord
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null && ThreatLetterBinder.TargetsCore(letter, records[i].core))
                {
                    records[i].core.OwnLetter(letter.ID);
                    return true;
                }
            }
            return false;
        }

        private void PruneInvalidRecords(bool force = false)
        {
            int now = ThreatLordQueries.CurrentTick;
            if (!force && lastPruneTick == now)
                return;
            Prune(raidThreats);
            Prune(sightstealerThreats);
            Prune(genericLordThreats);
            lastPruneTick = now;
        }

        private static void Prune<T>(List<T> records) where T : TrackedThreatRecord
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                T record = records[i];
                if (record?.core == null)
                {
                    records.RemoveAt(i);
                    continue;
                }
                record.core.EnsureCollections();
                record.core.lords.RemoveAll(lord => !ThreatLordQueries.IsActive(lord));
                if (record.core.lords.Count == 0)
                    records.RemoveAt(i);
            }
        }

        private static void RemoveLord<T>(List<T> records, Lord lord) where T : TrackedThreatRecord
        {
            for (int i = records.Count - 1; i >= 0; i--)
            {
                var lords = records[i]?.core?.lords;
                if (lords == null)
                {
                    records.RemoveAt(i);
                    continue;
                }
                lords.Remove(lord);
                if (lords.Count == 0)
                    records.RemoveAt(i);
            }
        }

        /// <summary>A lord already tracked, or no longer active, does not join a new record.</summary>
        private void Add<T>(List<T> records, T record) where T : TrackedThreatRecord
        {
            if (record?.core == null)
                return;
            record.core.EnsureCollections();
            record.core.lords.RemoveAll(lord => !ThreatLordQueries.IsActive(lord) || IsTracked(lord));
            if (record.core.lords.Count > 0)
                records.Add(record);
        }

        private bool IsTracked(Lord lord) =>
            FindRecord(raidThreats, lord) != null
            || FindRecord(sightstealerThreats, lord) != null
            || FindRecord(genericLordThreats, lord) != null;

        private static T FindRecord<T>(List<T> records, Lord lord) where T : TrackedThreatRecord
        {
            if (lord == null)
                return null;
            for (int i = 0; i < records.Count; i++)
            {
                var lords = records[i]?.core?.lords;
                if (lords != null && lords.Contains(lord))
                    return records[i];
            }
            return null;
        }

        private bool OwnsLetter(int letterId) =>
            letterId > 0 && (Owns(sightstealerThreats, letterId) || Owns(raidThreats, letterId) || Owns(genericLordThreats, letterId));

        private static bool Owns<T>(List<T> records, int letterId) where T : TrackedThreatRecord
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null && records[i].OwnsLetter(letterId))
                    return true;
            }
            return false;
        }

        private bool IsTargetedByTracked(Letter letter) =>
            Targets(sightstealerThreats, letter) || Targets(raidThreats, letter) || Targets(genericLordThreats, letter);

        private static bool Targets<T>(List<T> records, Letter letter) where T : TrackedThreatRecord
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null && ThreatLetterBinder.TargetsCore(letter, records[i].core))
                    return true;
            }
            return false;
        }

        private static void AddSnapshot(List<OngoingEventSnapshot> destination, OngoingEventSnapshot snapshot)
        {
            if (snapshot != null)
                destination.Add(snapshot);
        }

        private void EnsureCollections()
        {
            raidThreats ??= new List<RaidThreatRecord>();
            sightstealerThreats ??= new List<SightstealerThreatRecord>();
            genericLordThreats ??= new List<GenericLordThreatRecord>();
        }
    }
}

using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace Ustas.RimAI.Events
{
    /// <summary>
    /// What every tracked threat keeps: the lords that make it up, the threat
    /// letters it owns (so the same raid is not described twice, once from the
    /// tracker and once from its letter), and what the incident was called.
    /// Saved with the game; lords are references, so ThreatTrackerComponent
    /// drops removed ones before every save.
    /// </summary>
    public class ThreatRecordCore : IExposable
    {
        public List<Lord> lords = new List<Lord>();
        public List<int> ownedLetterIds = new List<int>();
        public string incidentDefName;
        public string incidentLabel;
        public int createdTick;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref lords, "lords", LookMode.Reference);
            Scribe_Collections.Look(ref ownedLetterIds, "ownedLetterIds", LookMode.Value);
            Scribe_Values.Look(ref incidentDefName, "incidentDefName");
            Scribe_Values.Look(ref incidentLabel, "incidentLabel");
            Scribe_Values.Look(ref createdTick, "createdTick");
            EnsureCollections();
        }

        public void EnsureCollections()
        {
            lords ??= new List<Lord>();
            ownedLetterIds ??= new List<int>();
        }

        public void OwnLetter(int letterId)
        {
            if (letterId > 0 && !ownedLetterIds.Contains(letterId))
                ownedLetterIds.Add(letterId);
        }

        public bool OwnsLetter(int letterId) =>
            letterId > 0 && ownedLetterIds != null && ownedLetterIds.Contains(letterId);
    }

    public abstract class TrackedThreatRecord : IExposable
    {
        public ThreatRecordCore core = new ThreatRecordCore();

        public virtual void ExposeData()
        {
            Scribe_Deep.Look(ref core, "core");
            core ??= new ThreatRecordCore();
            core.EnsureCollections();
        }

        public virtual bool OwnsLetter(int letterId) => core != null && core.OwnsLetter(letterId);
    }

    /// <summary>An incident raid: faction, strategy and arrival are known when it lands.</summary>
    public class RaidThreatRecord : TrackedThreatRecord
    {
        public string factionName;
        public string strategyLabel;
        public string arrivalDescription;
        public string questName;
        public int initialForceSize;
        public int initialGroupCount;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref factionName, "factionName");
            Scribe_Values.Look(ref strategyLabel, "strategyLabel");
            Scribe_Values.Look(ref arrivalDescription, "arrivalDescription");
            Scribe_Values.Look(ref questName, "questName");
            Scribe_Values.Look(ref initialForceSize, "initialForceSize");
            Scribe_Values.Look(ref initialGroupCount, "initialGroupCount");
        }
    }

    /// <summary>Any other hostile lord: a mech cluster, a siege, a manhunter pack, a quest's ambush.</summary>
    public class GenericLordThreatRecord : TrackedThreatRecord
    {
        public string factionName;
        public int initialForceSize;
        public int initialGroupCount;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref factionName, "factionName");
            Scribe_Values.Look(ref initialForceSize, "initialForceSize");
            Scribe_Values.Look(ref initialGroupCount, "initialGroupCount");
        }
    }

    /// <summary>
    /// Sightstealers are invisible until they reveal themselves, so until one has
    /// been seen the prompt gets only what the warning letter told the player.
    /// </summary>
    public class SightstealerThreatRecord : TrackedThreatRecord
    {
        public int warningLetterId;
        public bool disclosureObserved;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref warningLetterId, "warningLetterId");
            Scribe_Values.Look(ref disclosureObserved, "disclosureObserved");
        }

        public override bool OwnsLetter(int letterId) =>
            letterId > 0 && (warningLetterId == letterId || base.OwnsLetter(letterId));
    }
}

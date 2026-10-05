using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Ustas.RimAI.Events
{
    /// <summary>
    /// The lords and threat letters one incident produced while it executed.
    /// An incident does not say which lords it made, so every lord created while
    /// its TryExecute is on the stack is attributed to it.
    /// </summary>
    internal sealed class ThreatCaptureContext
    {
        public readonly IncidentWorker worker;
        public readonly IncidentParms parms;
        public readonly List<Lord> lords = new List<Lord>();
        public readonly List<Letter> letters = new List<Letter>();

        public ThreatCaptureContext(IncidentWorker worker, IncidentParms parms)
        {
            this.worker = worker;
            this.parms = parms;
        }

        public string IncidentDefName => worker?.def?.defName;
        public string IncidentLabel => worker?.def?.label;
        public bool IsRaid => worker is IncidentWorker_Raid;

        public void CaptureLord(Lord lord)
        {
            if (lord == null || lords.Contains(lord))
                return;
            if (!(parms?.target is Map map) || lord.Map != map)
                return;
            // A raid may make a friendly lord too (an ally's escort); only its own faction counts.
            if (IsRaid && parms.faction != null && lord.faction != parms.faction)
                return;
            lords.Add(lord);
        }

        public void CaptureLetter(Letter letter)
        {
            if (letter != null && letter.ID > 0 && ThreatLetterBinder.IsThreatLetter(letter) && !letters.Contains(letter))
                letters.Add(letter);
        }
    }

    /// <summary>
    /// A per-thread stack of executing incidents; an incident can fire another
    /// (a raid that brings a second wave), and each keeps its own lords.
    /// </summary>
    internal static class ThreatCaptureScope
    {
        [ThreadStatic]
        private static Stack<ThreatCaptureContext> stack;

        private static ThreatCaptureContext Current => stack != null && stack.Count > 0 ? stack.Peek() : null;

        public static ThreatCaptureContext Push(IncidentWorker worker, IncidentParms parms)
        {
            stack ??= new Stack<ThreatCaptureContext>();
            var context = new ThreatCaptureContext(worker, parms);
            stack.Push(context);
            return context;
        }

        public static void Commit(ThreatCaptureContext context)
        {
            if (context != null && context.lords.Count > 0)
                ThreatTrackerComponent.Get()?.RegisterCapturedThreat(context);
        }

        public static void Pop(ThreatCaptureContext context)
        {
            if (context == null || stack == null || stack.Count == 0)
                return;
            if (stack.Peek() == context)
                stack.Pop();
            else
                stack.Clear(); // an inner incident threw past its own finalizer; start clean
        }

        /// <summary>False when no incident is executing, so the caller handles the lord itself.</summary>
        public static bool CaptureLord(Lord lord)
        {
            var current = Current;
            if (current == null)
                return false;
            current.CaptureLord(lord);
            return true;
        }

        /// <summary>
        /// A letter delivered now (not queued) while an incident executes belongs
        /// to it - once it reached the archive, which is where it is looked up later.
        /// </summary>
        public static void CaptureLetter(Letter letter, int delayTicks)
        {
            var current = Current;
            if (current != null && letter != null && delayTicks == 0
                && Find.Archive != null && Find.Archive.Contains(letter))
                current.CaptureLetter(letter);
        }
    }
}

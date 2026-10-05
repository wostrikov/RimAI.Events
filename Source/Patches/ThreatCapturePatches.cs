using System;
using HarmonyLib;
using RimWorld;
using Ustas.RimAI.Core.Diagnostics;
using Verse;
using Verse.AI.Group;

namespace Ustas.RimAI.Events
{
    // Feeds ThreatTrackerComponent. Each postfix runs inside a vanilla operation
    // that must complete - an incident, a lord being made, a letter arriving - so
    // a failure in the tracker is logged and swallowed rather than thrown into it.

    [HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
    public static class IncidentWorker_TryExecute_ThreatCapturePatch
    {
        static void Prefix(IncidentWorker __instance, IncidentParms parms, out object __state) =>
            __state = ThreatCaptureScope.Push(__instance, parms);

        static void Postfix(object __state, bool __result)
        {
            if (__result)
                ThreatCapturePatchGuard.Run("incident", () => ThreatCaptureScope.Commit((ThreatCaptureContext)__state));
        }

        static Exception Finalizer(Exception __exception, object __state)
        {
            ThreatCaptureScope.Pop(__state as ThreatCaptureContext);
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LordMaker), "MakeNewLord")]
    public static class LordMaker_MakeNewLord_ThreatCapturePatch
    {
        static void Postfix(Lord __result)
        {
            if (__result == null || ThreatCaptureScope.CaptureLord(__result))
                return;
            ThreatCapturePatchGuard.Run("lord", () => ThreatTrackerComponent.Get()?.RegisterUnscopedLord(__result));
        }
    }

    // Some incidents add a lord they built themselves rather than through LordMaker.
    [HarmonyPatch(typeof(LordManager), "AddLord")]
    public static class LordManager_AddLord_ThreatCapturePatch
    {
        static void Postfix(Lord newLord) => ThreatCaptureScope.CaptureLord(newLord);
    }

    [HarmonyPatch(typeof(LordManager), "RemoveLord")]
    public static class LordManager_RemoveLord_ThreatCapturePatch
    {
        static void Postfix(Lord oldLord)
        {
            if (oldLord != null)
                ThreatCapturePatchGuard.Run("lord removal", () => ThreatTrackerComponent.Get()?.NotifyLordRemoved(oldLord));
        }
    }

    // The other ReceiveLetter overloads build a Letter and call this one.
    [HarmonyPatch(typeof(LetterStack), "ReceiveLetter", new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class LetterStack_ReceiveLetter_ThreatCapturePatch
    {
        static void Postfix(Letter let, int delayTicks) => ThreatCaptureScope.CaptureLetter(let, delayTicks);
    }

    [HarmonyPatch(typeof(Pawn), "Notify_BecameVisible")]
    public static class Pawn_NotifyBecameVisible_ThreatCapturePatch
    {
        static void Postfix(Pawn __instance)
        {
            if (ThreatLordQueries.IsSightstealer(__instance))
                ThreatCapturePatchGuard.Run("sightstealer", () => ThreatTrackerComponent.Get()?.NotifySightstealerBecameVisible(__instance));
        }
    }

    internal static class ThreatCapturePatchGuard
    {
        internal static void Run(string what, Action action)
        {
            try
            {
                action();
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — Harmony postfix inside a vanilla incident/lord/letter operation must not abort it
            catch (Exception ex)
            {
                RimAiLog.WarningOnce(RimAiLogCategory.Events, "[RimAI.Events] threat tracking failed on " + what + ": " + ex, what.GetHashCode());
            }
        }
    }
}

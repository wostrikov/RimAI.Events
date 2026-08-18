using HarmonyLib;
using Ustas.RimAI.Core.Composition;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Events;

/// <summary>
/// Module composition root for RimAI.Events. Owns Harmony, prompt patches, and
/// Communication prompt-variable registration.
/// </summary>
public sealed class EventsComposition : IRimAiModuleComposition
{
    public static EventsComposition Current { get; } = new();

    public string ModuleId => RimAiModuleIds.Events;

    public bool IsStarted { get; private set; }

    public void Start()
    {
        if (IsStarted)
            return;

        var harmony = new Harmony("ustas.rimai.events");
        harmony.PatchAll();
        PromptService_OngoingEventsPatch.Register();
        EventsCommunicationIntegration.TryRegister();
        RimAIModuleRegistry.Current.Register(
            new RimAIModuleDescriptor(
                "events",
                "RimAI.Events",
                "RimAI.Events",
                "Events"));
        RimAiLog.Info(RimAiLogCategory.Events, "[RimAI.Events] Loaded.");
        IsStarted = true;
    }

    public void Stop()
    {
        IsStarted = false;
    }
}

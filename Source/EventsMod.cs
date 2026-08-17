using HarmonyLib;
using UnityEngine;
using Verse;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Events
{
    /// <summary>RimAI.Events mod entry. Registers the module and applies Harmony patches.</summary>
    public class EventsMod : Mod
    {
        public const string HandshakeModuleVersion = "1.0.0";
        public static EventsMod Instance;
        public static EventFilterSettings Settings;

        public EventsMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<EventFilterSettings>();
            RimAiHandshake.TryActivate(
                RimAiHandshakeDescriptor.Current(RimAiModuleIds.Events, HandshakeModuleVersion, isOptional: true),
                Activate);
        }

        static void Activate()
        {
            var harmony = new Harmony("ustas.rimai.events");
            harmony.PatchAll();
            PromptService_OngoingEventsPatch.Register();
            RimAIModuleRegistry.Current.Register(
                new RimAIModuleDescriptor(
                    "events",
                    "RimAI.Events",
                    "RimAI.Events",
                    "Events"));
            Log.Message("[RimAI.Events] Loaded.");
        }

        public override string SettingsCategory()
        {
            return Content?.Name ?? "RimAI.Events";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            RimAISettingsNavigation.Open("events");
            EventFilterUI.DoFilteringUI(inRect, Settings);
        }
    }
}

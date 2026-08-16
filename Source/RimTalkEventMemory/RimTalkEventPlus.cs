using HarmonyLib;
using System;
using UnityEngine;
using Verse;

namespace RimTalkEventPlus
{
    // Main mod entry point for RimTalk Event+.
    // Still responsible for applying Harmony patches,
    // now also exposes a Mod Settings UI.
    public class RimTalkEventPlus : Mod
    {
        public static RimTalkEventPlus Instance;
        public static EventFilterSettings Settings;

        public RimTalkEventPlus(ModContentPack content) : base(content)
        {
            Instance = this;

            // Load settings
            Settings = GetSettings<EventFilterSettings>();

            // Existing behavior: Harmony patches + log
            var harmony = new Harmony("saltgin.rimtalkeventmemory");
            harmony.PatchAll();
            Ustas.RimAI.Core.Modules.RimAIModuleRegistry.Current.Register(
                new Ustas.RimAI.Core.Modules.RimAIModuleDescriptor(
                    "events",
                    "RimAI.Events",
                    "RimAI.Events",
                    "Events"));
            Log.Message("[RimTalk Event+] Loaded.");
        }

        public override string SettingsCategory()
        {
            // Label in Options → Mod Settings
            return Content?.Name ?? "RimAI.Events";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            AccessTools.TypeByName("Ustas.RimAI.Core.Modules.RimAISettingsNavigation")
                ?.GetMethod("Open")
                ?.Invoke(null, new object[] { "events", null });
            EventFilterUI.DoFilteringUI(inRect, Settings);
        }
    }
}

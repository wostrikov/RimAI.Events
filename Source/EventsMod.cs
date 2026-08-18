using UnityEngine;
using Verse;
using Ustas.RimAI.Core.Handshake;
using Ustas.RimAI.Core.Modules;

namespace Ustas.RimAI.Events
{
    /// <summary>RimAI.Events mod entry. Service graph lives in <see cref="EventsComposition"/>.</summary>
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
                EventsComposition.Current.Start);
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

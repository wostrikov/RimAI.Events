using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication;
using Ustas.RimAI.Communication.API;
using Ustas.RimAI.Communication.Prompt;
using RimWorld;
using Ustas.RimAI.Core.Handshake;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Events
{
    [StaticConstructorOnStartup]
    public static class EventsCommunicationIntegration
    {
        private const string MOD_ID = "rimtalkeventplus";
        private static readonly bool _apiAvailable;

        static EventsCommunicationIntegration()
        {
            try
            {
                if (!RimAiHandshake.IsApproved(RimAiModuleIds.Events))
                {
                    return;
                }

                RegisterVariables();
                _apiAvailable = true;
                RimAiLog.Info(RimAiLogCategory.Events, "[RimAI.Events] Advanced Mode API integration successful.");
            }
            catch (Exception ex)
            {
                RimAiLog.Warning(RimAiLogCategory.Events, $"[RimAI.Events] Failed to integrate with Communication prompt API: {ex.Message}");
            }
        }

        public static bool IsAdvancedModeEnabled => Settings.Get()?.UseAdvancedPromptMode == true;

        private static void RegisterVariables()
        {
            Register("eventplus_all",
                "All enabled ongoing events combined",
                ctx =>
                {
                    var map = ctx?.Map;
                    if (map == null) return string.Empty;
                    bool isInDanger = map.IsPlayerHome && map.dangerWatcher?.DangerRating != StoryDanger.None;
                    return Format(OngoingEventsUtil.GetOngoingEventsNow(map, isInDanger));
                });

            Register("eventplus_quests",
                "Active quests on current map",
                ctx =>
                {
                    var map = ctx?.Map;
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    OngoingEventsUtil.TryAddOngoingQuestsForMap(map, result, 5);
                    return Format(result);
                });

            Register("eventplus_conditions",
                "Active game conditions on current map",
                ctx =>
                {
                    var map = ctx?.Map;
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    OngoingEventsUtil.TryAddActiveGameConditionsForMap(map, result, 5);
                    return Format(result);
                });

            Register("eventplus_threats",
                "Ongoing threats on current map",
                ctx =>
                {
                    var map = ctx?.Map;
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    bool isInDanger = map.IsPlayerHome && map.dangerWatcher?.DangerRating != StoryDanger.None;
                    if (isInDanger)
                        OngoingEventsUtil.TryAddMostRecentThreatLetter(result, 1, 30);
                    return Format(result);
                });

            Register("eventplus_location",
                "Current location description",
                ctx =>
                {
                    var map = ctx?.Map;
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    if (!map.IsPlayerHome)
                        OngoingEventsUtil.TryAddSitePartEvents(map, result, 3);
                    return Format(result);
                });
        }

        private static void Register(string name, string description, Func<PromptContext, string> provider)
        {
            RimTalkPromptAPI.RegisterContextVariable(MOD_ID, name, provider, description, 100);
        }

        private static string Format(List<OngoingEventSnapshot> events)
        {
            if (events == null || events.Count == 0)
                return string.Empty;
            return OngoingEventsFormatter.FormatOngoingEventsBlock(events, maxChars: 1500, includeWrapper: false);
        }

        public static bool IsApiAvailable => _apiAvailable;
    }
}

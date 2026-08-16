using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Communication;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Events
{
    /// <summary>Appends ongoing semantic events to the Communication talk context.</summary>
    public static class PromptService_OngoingEventsPatch
    {
        static bool _registered;

        public static void Register()
        {
            if (_registered)
                return;
            _registered = true;
            TalkLifecycle.PromptDecorated += OnPromptDecorated;
        }

        static void OnPromptDecorated(object talkRequestObj, object pawnsObj, string status)
        {
            try
            {
                if (talkRequestObj is not TalkRequest talkRequest)
                    return;
                if (EventsMod.Settings != null && !EventsMod.Settings.AppendToContext)
                    return;

                Pawn initiator = talkRequest.Initiator;
                if (initiator == null || initiator.Map == null)
                    return;

                Map map = initiator.Map;
                bool isInDanger = map.IsPlayerHome &&
                    map.dangerWatcher?.DangerRating != StoryDanger.None;

                var ongoingEvents = OngoingEventsUtil.GetOngoingEventsNow(
                    map,
                    isInDanger,
                    maxEvents: 5,
                    maxThreatScanBack: 30
                );

                if (ongoingEvents == null || ongoingEvents.Count == 0)
                    return;

                var settings = EventsMod.Settings;
                var pawns = pawnsObj as List<Pawn>;
                if (settings != null && settings.EnableContextFiltering)
                {
                    var contextPawnIds = ContextPawnMatcher.CollectContextPawnIds(
                        pawns,
                        initiator,
                        talkRequest.Recipient);

                    ongoingEvents = ContextPawnMatcher.FilterEventsByContext(
                        ongoingEvents,
                        contextPawnIds);

                    if (ongoingEvents == null || ongoingEvents.Count == 0)
                        return;
                }

                string block = OngoingEventsFormatter.FormatOngoingEventsBlock(
                    ongoingEvents,
                    maxChars: 1200
                );

                if (block.NullOrEmpty())
                    return;

                if (string.IsNullOrEmpty(talkRequest.Context))
                    talkRequest.Context = block;
                else
                    talkRequest.Context = talkRequest.Context + "\n\n" + block;
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Events] Error while appending ongoing events: {ex}");
            }
        }
    }
}

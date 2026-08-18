using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Data;
using Ustas.RimAI.Communication.Util;
using Ustas.RimAI.Core.Communication;
using Ustas.RimAI.Core.Events;
using RimWorld;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Events
{
    /// <summary>
    /// Talk-path contributor: appends ongoing semantic events to Communication talk context
    /// via <see cref="TalkLifecycle.PromptDecorated"/>. Not a Harmony patch.
    /// </summary>
    public static class OngoingEventsPromptContributor
    {
        static bool _registered;

        public static bool IsRegistered => _registered;

        public static void Register()
        {
            if (_registered)
                return;
            TalkLifecycle.PromptDecorated += OnPromptDecorated;
            _registered = true;
        }

        public static void Unregister()
        {
            if (!_registered)
                return;
            TalkLifecycle.PromptDecorated -= OnPromptDecorated;
            _registered = false;
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
                    maxEvents: EventsInteriorDefaults.DefaultMaxOngoingEvents,
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
                    maxChars: OngoingEventsPromptFormatter.TalkAppendMaxChars
                );

                if (block.NullOrEmpty())
                    return;

                if (string.IsNullOrEmpty(talkRequest.Context))
                    talkRequest.Context = block;
                else
                    talkRequest.Context = talkRequest.Context + "\n\n" + block;
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — Talk decorate contributor must not abort Communication prompt build
            catch (Exception ex)
            {
                RimAiLog.Warning(RimAiLogCategory.Events, "[RimAI.Events] Error while appending ongoing events: " + ex);
            }
        }
    }
}

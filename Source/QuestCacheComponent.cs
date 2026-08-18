using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Ustas.RimAI.Core.Diagnostics;
using Ustas.RimAI.Core.Events;

namespace Ustas.RimAI.Events
{
    // Per-game cache for quest-related lookups.
    // Map-affinity isolation + invalidate semantics live in QuestRuntimeCacheStore (Core).
    // Pawn object lists stay host-side (Verse Pawn references).
    public class QuestCacheComponent : GameComponent
    {
        private readonly Dictionary<(Type, string), FieldInfo> _fieldCache =
            new Dictionary<(Type, string), FieldInfo>();

        private readonly QuestRuntimeCacheStore _runtime = new QuestRuntimeCacheStore();

        private readonly Dictionary<int, List<Pawn>> _questPawnsCache =
            new Dictionary<int, List<Pawn>>();

        private const BindingFlags AllInstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public QuestCacheComponent(Game game) : base()
        {
        }

        /// <summary>Exposed for characterization / tests of the owned Core store.</summary>
        internal QuestRuntimeCacheStore RuntimeStore => _runtime;

        public FieldInfo GetField(Type type, string fieldName)
        {
            var key = (type, fieldName);
            if (_fieldCache.TryGetValue(key, out var cached))
                return cached;

            var field = type.GetField(fieldName, AllInstanceFlags);
            _fieldCache[key] = field;
            return field;
        }

        public bool TryGetQuestAffectsMap(int questId, int mapUniqueId, out bool affects) =>
            _runtime.TryGetQuestAffectsMap(questId, mapUniqueId, out affects);

        public void StoreQuestAffectsMap(int questId, int mapUniqueId, bool affects) =>
            _runtime.StoreQuestAffectsMap(questId, mapUniqueId, affects);

        public void InvalidateQuest(int questId)
        {
            _runtime.InvalidateQuest(questId);
            _questPawnsCache.Remove(questId);
        }

        public void PrewarmActiveQuestsForMap(Map map)
        {
            if (map == null)
                return;

            var quests = Find.QuestManager?.ActiveQuestsListForReading;
            if (quests == null)
                return;

            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (!QuestLinkUtil.IsQuestOngoing(quest))
                    continue;

                try
                {
                    QuestLinkUtil.QuestAffectsMap(quest, map);
                }
                // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — optional map prewarm must not abort FinalizeInit
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                        RimAiLog.Warning(RimAiLogCategory.Events, "[RimAI.Events] Failed to prewarm quest: " + ex);
                }
            }
        }

        public bool TryGetQuestPawns(int questId, out List<Pawn> pawns) =>
            _questPawnsCache.TryGetValue(questId, out pawns);

        public void StoreQuestPawns(int questId, List<Pawn> pawns) =>
            _questPawnsCache[questId] = pawns;

        public void InvalidateQuestPawns(int questId) =>
            _questPawnsCache.Remove(questId);
    }

    // DEPRECATED STUB: Preserves backward compatibility with saves that reference
    // the old QuestAffectsMapCacheComponent class.
    public class QuestAffectsMapCacheComponent : GameComponent
    {
        public QuestAffectsMapCacheComponent(Game game) : base() { }
        public override void ExposeData() { }
    }
}

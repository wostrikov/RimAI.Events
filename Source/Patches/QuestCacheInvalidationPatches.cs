using HarmonyLib;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Events
{
    // A quest's cached map affinity and pawn list are only true until the quest
    // changes. Quest.End already clears them (Quest_End_ClearCache_Patch); these
    // cover the changes an ongoing quest goes through.
    //
    // Pawn replacement needs no hook of its own: the one vanilla caller,
    // QuestPart_ReplaceLostLeaderReferences, runs inside a signal addressed to
    // its quest, which the signal patch below already answers.
    internal static class QuestCacheInvalidation
    {
        internal static void Invalidate(Quest quest)
        {
            if (quest == null || quest.id < 0)
                return;

            Current.Game?.GetComponent<QuestCacheComponent>()?.InvalidateQuest(quest.id);
        }

        /// <summary>
        /// True for a signal every quest hears, or one tagged "Quest{id}." for this
        /// quest. Called once per quest for every signal the game sends, so it
        /// reads the tag in place rather than formatting the prefix it compares.
        /// </summary>
        internal static bool IsAddressedTo(Quest quest, Signal signal)
        {
            if (signal.global)
                return true;

            string tag = signal.tag;
            const string prefix = "Quest";
            if (tag == null || !tag.StartsWith(prefix, System.StringComparison.Ordinal))
                return false;

            int id = quest.id;
            int value = 0;
            int i = prefix.Length;
            while (i < tag.Length && tag[i] >= '0' && tag[i] <= '9')
            {
                value = value * 10 + (tag[i] - '0');
                if (value > id)
                    return false;
                i++;
            }

            return i > prefix.Length && i < tag.Length && tag[i] == '.' && value == id;
        }
    }

    [HarmonyPatch(typeof(Quest), "Notify_SignalReceived")]
    public static class Quest_NotifySignalReceived_CachePatch
    {
        static void Postfix(Quest __instance, Signal signal)
        {
            if (__instance != null && QuestCacheInvalidation.IsAddressedTo(__instance, signal))
                QuestCacheInvalidation.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Quest), "AddPart")]
    public static class Quest_AddPart_CachePatch
    {
        static void Prefix(Quest __instance, out int __state) =>
            __state = __instance?.PartsListForReading.Count ?? 0;

        // Quest generation adds every part before the quest starts; only a part
        // added to a quest already running changes what was cached about it.
        static void Postfix(Quest __instance, int __state)
        {
            if (__instance != null && __instance.State == QuestState.Ongoing
                && __instance.PartsListForReading.Count != __state)
                QuestCacheInvalidation.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Quest), "RemovePart")]
    public static class Quest_RemovePart_CachePatch
    {
        static void Prefix(Quest __instance, out int __state) =>
            __state = __instance?.PartsListForReading.Count ?? 0;

        static void Postfix(Quest __instance, int __state)
        {
            if (__instance != null && __instance.State == QuestState.Ongoing
                && __instance.PartsListForReading.Count != __state)
                QuestCacheInvalidation.Invalidate(__instance);
        }
    }
}

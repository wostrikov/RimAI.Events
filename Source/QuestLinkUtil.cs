using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Ustas.RimAI.Events
{
    // Utility class for accessing Quest data and determining quest-map relationships.
    public static class QuestLinkUtil
    {
        private const BindingFlags FallbackBindingFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        #region Cache Access

        private static QuestCacheComponent GetCache()
        {
            return Current.Game?.GetComponent<QuestCacheComponent>();
        }

        #endregion

        #region Quest Field Access (Direct - Public Fields in RimWorld 1.6)

        public static string TryGetQuestDescription(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            string result = quest.description;
            return result ?? string.Empty;
        }

        public static string TryGetQuestLabel(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            if (!quest.name.NullOrEmpty())
                return quest.name;

            string desc = TryGetQuestDescription(quest);
            if (!desc.NullOrEmpty())
            {
                int nl = desc.IndexOf('\n');
                return nl > 0 ? desc.Substring(0, nl) : desc;
            }

            return "Quest";
        }

        public static bool IsQuestHidden(Quest quest)
        {
            if (quest == null)
                return false;

            return quest.hidden;
        }

        public static bool IsQuestOngoing(Quest quest)
        {
            return quest?.State == QuestState.Ongoing;
        }

        #endregion

        #region Quest Accepted Age

        // Returns a short marker like "accepted ~1.3 days ago" based on acceptanceTick.
        public static string GetQuestAcceptedAgeMarker(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            int acceptanceTick = quest.acceptanceTick;

            if (acceptanceTick <= 0)
                return string.Empty;

            int currentTicks = Find.TickManager.TicksGame;
            int diff = currentTicks - acceptanceTick;
            if (diff <= 0)
                return "accepted just now";

            // RimWorld uses 60,000 ticks per in-game day. 
            double days = diff / 60000.0;

            if (days >= 1.0)
            {
                double roundedDays = Math.Round(days, 1);
                string daysStr = roundedDays.ToString("0.0");
                return "accepted ~" + daysStr + " days ago";
            }
            else
            {
                double hours = days * 24.0;
                int roundedHours = (int)Math.Round(hours);
                if (roundedHours <= 0)
                    return "accepted just now";
                if (roundedHours == 1)
                    return "accepted ~1 hour ago";
                return "accepted ~" + roundedHours + " hours ago";
            }
        }

        #endregion

        #region Quest Key Pawns

        // Extract pawns directly referenced by this quest's parts (pawn/pawns fields).
        // Returns cached result when available.  Uses thingIDNumber for deduplication.
        public static List<Pawn> GetQuestKeyPawns(Quest quest)
        {
            if (quest == null)
                return new List<Pawn>();

            int questId = quest.id;
            var cache = GetCache();

            // Try cache first
            if (cache != null && questId >= 0)
            {
                if (cache.TryGetQuestPawns(questId, out var cachedPawns))
                    return cachedPawns;
            }

            // Extract pawns from quest parts
            var pawns = ExtractPawnsFromQuestParts(quest);

            // Store in cache
            if (cache != null && questId >= 0)
            {
                cache.StoreQuestPawns(questId, pawns);
            }

            return pawns;
        }

        // Internal method to extract pawns from quest parts via reflection.
        private static List<Pawn> ExtractPawnsFromQuestParts(Quest quest)
        {
            var parts = quest.PartsListForReading;
            if (parts == null || parts.Count == 0)
                return new List<Pawn>();

            var pawns = new List<Pawn>();
            var seenIds = new HashSet<int>();
            var cache = GetCache();

            foreach (var partObj in parts)
            {
                if (partObj == null)
                    continue;

                Type partType = partObj.GetType();

                var pawn = TryGetFieldValue(partObj, partType, "pawn", cache) as Pawn;
                if (pawn != null && seenIds.Add(pawn.thingIDNumber))
                    pawns.Add(pawn);

                if (TryGetFieldValue(partObj, partType, "pawns", cache) is System.Collections.IList pawnListObj)
                {
                    foreach (object o in pawnListObj)
                    {
                        if (o is Pawn p && seenIds.Add(p.thingIDNumber))
                            pawns.Add(p);
                    }
                }
            }

            return pawns;
        }

        // Get comma-separated short names of pawns involved in quest.
        // Uses cached pawn list from GetQuestKeyPawns().
        public static string GetQuestKeyPawnNames(Quest quest)
        {
            var pawns = GetQuestKeyPawns(quest);

            if (pawns.Count == 0)
                return string.Empty;

            var names = new List<string>();
            foreach (var p in pawns)
            {
                if (p == null)
                    continue;

                string name = TryReadPawnDisplayName(p);
                if (!name.NullOrEmpty())
                    names.Add(name);
            }

            if (names.Count == 0)
                return string.Empty;

            return string.Join(", ", names);
        }

        #endregion

        #region Quest Affects Map

        // True if this quest should be considered as affecting the given map.
        // Only positive results are cached: quest targets can be assigned later,
        // so false results are recalculated on the next prompt.
        public static bool QuestAffectsMap(Quest quest, Map map)
        {
            if (quest == null || map == null)
                return false;

            // Cache only works when a game is running
            var game = Current.Game;
            var tickManager = Find.TickManager;

            if (game != null && tickManager != null)
            {
                int questId = quest.id;
                if (questId >= 0)
                {
                    int mapUid = map.uniqueID;

                    var cache = GetCache();
                    if (cache != null)
                    {
                        if (cache.TryGetQuestAffectsMap(questId, mapUid, out bool cached))
                            return cached;

                        bool computed = QuestAffectsMap_Uncached(quest, map);
                        if (computed)
                            cache.StoreQuestAffectsMap(questId, mapUid, true);
                        return computed;
                    }
                }
            }

            // Fallback:  compute without caching
            return QuestAffectsMap_Uncached(quest, map);
        }

        private static bool QuestAffectsMap_Uncached(Quest quest, Map map)
        {
            if (quest == null || map == null)
                return false;

            MapParent mapParent = map.info?.parent;
            int mapTile = map.Tile;

            var parts = quest.PartsListForReading;
            var cache = GetCache();

            // 1. Check QuestLookTargets from filtered parts
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    if (part == null) continue;

                    // Skip auxiliary parts that don't indicate quest location
                    if (ShouldSkipQuestPart(part))
                        continue;

                    try
                    {
                        var partLookTargets = part.QuestLookTargets;
                        if (partLookTargets != null)
                        {
                            foreach (var target in partLookTargets)
                            {
                                // Check if target has a map and it matches our map
                                if (target.IsMapTarget && target.Map == map)
                                    return true;

                                // Check if target WorldObject is this map's parent
                                if (target.HasWorldObject && mapParent != null)
                                {
                                    var targetParent = target.WorldObject as MapParent;
                                    if (targetParent != null && targetParent == mapParent)
                                        return true;
                                }

                                // Check tile match
                                int targetTile = target.Tile;
                                if (targetTile >= 0 && targetTile == mapTile)
                                    return true;
                            }
                        }
                    }
                    // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — QuestLookTargets can throw during map generation
                    catch (Exception ex)
                    {
                        Log.WarningOnce("[RimAI.Events] QuestLookTargets enumeration failed: " + ex, part.GetHashCode());
                    }
                }
            }

            // 2. Check QuestParts for worldObject/site fields that may reference this map
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    if (part == null) continue;

                    // Skip auxiliary parts that don't indicate quest location
                    if (ShouldSkipQuestPart(part))
                        continue;

                    Type partType = part.GetType();

                    var wo = TryGetFieldValue(part, partType, "worldObject", cache) as WorldObject;
                    if (wo is MapParent mp && (mp == mapParent || TryMapParentOnMap(mp, map)))
                        return true;

                    var site = TryGetFieldValue(part, partType, "site", cache) as MapParent;
                    if (site != null && (site == mapParent || TryMapParentOnMap(site, map)))
                        return true;
                }
            }

            // 3. Check individual quest parts for mapParent field/property
            if (mapParent == null)
                return false;

            if (parts != null && parts.Count > 0)
            {
                foreach (var part in parts)
                {
                    if (part == null)
                        continue;

                    if (ShouldSkipQuestPart(part))
                        continue;

                    // Check part's QuestSelectTargets
                    try
                    {
                        var partSelectTargets = part.QuestSelectTargets;
                        if (partSelectTargets != null)
                        {
                            foreach (var target in partSelectTargets)
                            {
                                if (target.IsMapTarget && target.Map == map)
                                    return true;

                                if (target.HasWorldObject && mapParent != null)
                                {
                                    var targetParent = target.WorldObject as MapParent;
                                    if (targetParent != null && targetParent == mapParent)
                                        return true;
                                }
                            }
                        }
                    }
                    // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — QuestSelectTargets can throw during map generation
                    catch (Exception ex)
                    {
                        Log.WarningOnce("[RimAI.Events] QuestSelectTargets enumeration failed: " + ex, part.GetHashCode());
                    }

                    Type partType = part.GetType();
                    var partParent = TryGetFieldValue(part, partType, "mapParent", cache) as MapParent
                        ?? TryGetPropertyValue(part, partType, "MapParent") as MapParent;

                    if (partParent != null && (partParent == mapParent || TryMapParentOnMap(partParent, map)))
                        return true;
                }
            }

            return false;
        }

        static bool TryMapParentOnMap(MapParent parent, Map map)
        {
            if (parent == null || map == null)
                return false;
            try
            {
                return parent.HasMap && parent.Map == map;
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — MapParent can be uninitialized during generation
            catch (Exception ex)
            {
                Log.WarningOnce("[RimAI.Events] QuestLinkUtil map-parent check failed: " + ex, parent.GetHashCode());
                return false;
            }
        }

        static object TryGetFieldValue(object instance, Type type, string name, QuestCacheComponent cache)
        {
            if (instance == null || type == null || string.IsNullOrEmpty(name))
                return null;
            try
            {
                FieldInfo field = cache?.GetField(type, name) ?? type.GetField(name, FallbackBindingFlags);
                return field?.GetValue(instance);
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — optional quest-part fields must not abort prompt build
            catch (Exception ex)
            {
                Log.WarningOnce("[RimAI.Events] QuestLinkUtil field '" + name + "' failed: " + ex, type.GetHashCode() ^ name.GetHashCode());
                return null;
            }
        }

        static object TryGetPropertyValue(object instance, Type type, string name)
        {
            if (instance == null || type == null || string.IsNullOrEmpty(name))
                return null;
            try
            {
                var prop = type.GetProperty(name, FallbackBindingFlags);
                return prop?.GetValue(instance);
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — optional quest-part properties must not abort prompt build
            catch (Exception ex)
            {
                Log.WarningOnce("[RimAI.Events] QuestLinkUtil property '" + name + "' failed: " + ex, type.GetHashCode() ^ name.GetHashCode());
                return null;
            }
        }

        static string TryReadPawnDisplayName(Pawn pawn)
        {
            if (pawn == null)
                return null;
            try
            {
                var cap = pawn.LabelShortCap;
                if (!cap.NullOrEmpty())
                    return cap;
                return pawn.Name?.ToStringShort;
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — destroyed pawn labels must not abort quest prompt
            catch (Exception ex)
            {
                Log.WarningOnce("[RimAI.Events] QuestLinkUtil pawn name failed: " + ex, pawn.thingIDNumber);
                return null;
            }
        }

        private static bool ShouldSkipQuestPart(QuestPart part)
        {
            if (part == null) return true;

            string partTypeName = part.GetType().Name;

            // Skip parts that don't indicate actual quest location
            // These are auxiliary parts for rewards, notifications, or requirements
            return partTypeName == "QuestPart_DropPods" ||
                   partTypeName == "QuestPart_RequirementsToAcceptPlanetLayer" ||
                   partTypeName == "QuestPart_RequirementsToAccept" ||
                   partTypeName == "QuestPart_GiveRewards" ||
                   partTypeName == "QuestPart_Letter" ||
                   partTypeName == "QuestPart_Notify_PlayerRaidedSomeone" ||
                   partTypeName == "QuestPart_Choice";
        }

        #endregion
    }
}

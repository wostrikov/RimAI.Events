using System.Collections.Generic;
using RimWorld;
using Ustas.RimAI.Core.Events;
using Verse;

namespace Ustas.RimAI.Events
{
    // Wrapper class for per-colony disabled instance IDs.
    // Used as dictionary value for colony-specific instance filtering.
    public class DisabledInstanceSet : IExposable
    {
        public HashSet<string> ids = new HashSet<string>();

        public DisabledInstanceSet() { }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref ids, EventScribeLabels.DisabledInstanceSet.Ids, LookMode.Value);
            if (ids == null)
            {
                ids = new HashSet<string>();
            }
        }

        public bool Contains(string id) => ids != null && ids.Contains(id);
        public void Add(string id) => ids.Add(id);
        public bool Remove(string id) => ids.Remove(id);
        public void Clear() => ids.Clear();
        public int Count => ids?.Count ?? 0;
    }

    public class EventFilterSettings : ModSettings
    {
        // If true, Event+ will compress quest text using XML templates
        // instead of always sending the full original description.
        public bool enableEventTextCompression = true;

        // Schema 1 kept every disabled type in one set, so a quest and a site part
        // that happen to share a defName were one rule. Read once and migrated
        // into the per-category sets below by TryMigrateLegacyTypeFilters.
        public HashSet<string> disabledEventDefNames = new HashSet<string>();

        // Types hidden globally, across all saves, one set per category that
        // supports type filtering (threats are shown or hidden only as a category).
        public HashSet<string> disabledQuestDefNames = new HashSet<string>();
        public HashSet<string> disabledMapConditionDefNames = new HashSet<string>();
        public HashSet<string> disabledSitePartDefNames = new HashSet<string>();

        // Schema 2 and later keep the per-category sets; a settings file without
        // the field predates them.
        public int filterSchemaVersion = EventFilterPolicy.CurrentFilterSchemaVersion;

        // Stores specific event instance IDs that are filtered per-colony (instance-based filtering).
        // Key:  colony ID (permadeathModeUniqueName)
        // Value: set of local instance IDs disabled for that colony
        public Dictionary<string, DisabledInstanceSet> disabledEventInstances = new Dictionary<string, DisabledInstanceSet>();

        // Internal flag to track if XML blacklist migration has been completed.
        public bool questBlacklistMigrated = false;

        // Quick category filters for UI
        public bool showQuests = true;
        public bool showMapConditions = true;
        public bool showThreats = true;
        public bool showSiteParts = true;

        // Manual override for Enhanced Prompt conflict lock.
        // false = keep current mandatory lock behavior (default)
        // true  = allow Event+ category filters even when Enhanced Prompt auto-capture is enabled
        public bool allowEnhancedPromptOverlap = false;

        // Effective lock state — Core EventsEnhancedPromptDedupPolicy is authoritative.
        public bool IsEnhancedPromptLockActive =>
            EventsEnhancedPromptDedupPolicy.IsLockActive(
                EnhancedPromptDetector.IsAutoEventCaptureEnabled,
                allowEnhancedPromptOverlap);

        public bool ShowQuestsEffective =>
            EventsEnhancedPromptDedupPolicy.ShowQuestsEffective(showQuests, IsEnhancedPromptLockActive);

        public bool ShowMapConditionsEffective =>
            EventsEnhancedPromptDedupPolicy.ShowMapConditionsEffective(showMapConditions, IsEnhancedPromptLockActive);

        public bool ShowThreatsEffective =>
            EventsEnhancedPromptDedupPolicy.ShowThreatsEffective(showThreats, IsEnhancedPromptLockActive);

        public bool ShowSitePartsEffective =>
            EventsEnhancedPromptDedupPolicy.ShowSitePartsEffective(showSiteParts, IsEnhancedPromptLockActive);

        public bool IsCategoryShown(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.Quest: return ShowQuestsEffective;
                case EventCategory.MapCondition: return ShowMapConditionsEffective;
                case EventCategory.Threat: return ShowThreatsEffective;
                case EventCategory.SitePart: return ShowSitePartsEffective;
                default: return false;
            }
        }

        // When enabled, only append events involving pawns in the conversation context.
        // Threats, map conditions, and site parts are always included.
        public bool EnableContextFiltering = false;

        // Advanced Mode Settings
        public bool AppendToContext = true;

        public EventFilterSettings()
        {
            EnsureCollections();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(
                ref enableEventTextCompression,
                EventScribeLabels.Settings.EnableEventTextCompression,
                true
            );

            Scribe_Collections.Look(
                ref disabledEventDefNames,
                EventScribeLabels.Settings.DisabledEventDefNames,
                LookMode.Value
            );

            Scribe_Collections.Look(
                ref disabledQuestDefNames,
                EventScribeLabels.Settings.DisabledQuestDefNames,
                LookMode.Value
            );

            Scribe_Collections.Look(
                ref disabledMapConditionDefNames,
                EventScribeLabels.Settings.DisabledMapConditionDefNames,
                LookMode.Value
            );

            Scribe_Collections.Look(
                ref disabledSitePartDefNames,
                EventScribeLabels.Settings.DisabledSitePartDefNames,
                LookMode.Value
            );

            // Default 1: a file written before the field existed is schema 1.
            Scribe_Values.Look(
                ref filterSchemaVersion,
                EventScribeLabels.Settings.FilterSchemaVersion,
                1
            );

            Scribe_Collections.Look(
                ref disabledEventInstances,
                EventScribeLabels.Settings.DisabledEventInstances,
                LookMode.Value,
                LookMode.Deep
            );

            Scribe_Values.Look(
                ref questBlacklistMigrated,
                EventScribeLabels.Settings.QuestBlacklistMigrated,
                false
            );

            Scribe_Values.Look(
                ref showQuests,
                EventScribeLabels.Settings.ShowQuests,
                true
            );

            Scribe_Values.Look(
                ref showMapConditions,
                EventScribeLabels.Settings.ShowMapConditions,
                true
            );

            Scribe_Values.Look(
                ref showThreats,
                EventScribeLabels.Settings.ShowThreats,
                true
            );

            Scribe_Values.Look(
                ref showSiteParts,
                EventScribeLabels.Settings.ShowSiteParts,
                true
            );

            Scribe_Values.Look(
                ref EnableContextFiltering,
                EventScribeLabels.Settings.EnableContextFiltering,
                false
            );

            Scribe_Values.Look(
                ref AppendToContext,
                EventScribeLabels.Settings.AppendToContext,
                true
            );

            Scribe_Values.Look(
                ref allowEnhancedPromptOverlap,
                EventScribeLabels.Settings.AllowEnhancedPromptOverlap,
                false
            );

            EnsureCollections();
        }

        public static bool SupportsTypeFiltering(EventCategory category) =>
            EventFilterPolicy.SupportsTypeFiltering(category.ToFilterCategory());

        public static bool SupportsInstanceFiltering(EventCategory category) =>
            EventFilterPolicy.SupportsInstanceFiltering(category.ToFilterCategory());

        public bool IsTypeDisabled(EventCategory category, string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return false;
            return TypeRules(category)?.Contains(defName) ?? false;
        }

        public bool IsTypeDisabled(EventFilterCategory category, string defName)
        {
            switch (category)
            {
                case EventFilterCategory.Quest: return IsTypeDisabled(EventCategory.Quest, defName);
                case EventFilterCategory.MapCondition: return IsTypeDisabled(EventCategory.MapCondition, defName);
                case EventFilterCategory.SitePart: return IsTypeDisabled(EventCategory.SitePart, defName);
                default: return false;
            }
        }

        public bool DisableType(EventCategory category, string defName) =>
            SupportsTypeFiltering(category) && !string.IsNullOrEmpty(defName) && TypeRules(category).Add(defName);

        public bool EnableType(EventCategory category, string defName) =>
            SupportsTypeFiltering(category) && !string.IsNullOrEmpty(defName) && TypeRules(category).Remove(defName);

        public IEnumerable<string> DisabledTypes(EventCategory category) =>
            (IEnumerable<string>)TypeRules(category) ?? new string[0];

        public int ClearTypeFilters()
        {
            int count = disabledQuestDefNames.Count + disabledMapConditionDefNames.Count + disabledSitePartDefNames.Count;
            disabledQuestDefNames.Clear();
            disabledMapConditionDefNames.Clear();
            disabledSitePartDefNames.Clear();
            return count;
        }

        // Checks if a specific event instance is disabled for the given colony (instance-based filtering).
        public bool IsEventInstanceDisabled(string colonyId, string localInstanceId)
        {
            if (string.IsNullOrEmpty(colonyId) || string.IsNullOrEmpty(localInstanceId))
                return false;
            if (disabledEventInstances == null)
                return false;
            if (!disabledEventInstances.TryGetValue(colonyId, out var instanceSet))
                return false;
            return instanceSet.Contains(localInstanceId);
        }

        // Gets or creates the DisabledInstanceSet for a given colony.
        public DisabledInstanceSet GetOrCreateInstanceSet(string colonyId)
        {
            if (string.IsNullOrEmpty(colonyId))
                return null;
            if (disabledEventInstances == null)
                disabledEventInstances = new Dictionary<string, DisabledInstanceSet>();
            if (!disabledEventInstances.TryGetValue(colonyId, out var instanceSet))
            {
                instanceSet = new DisabledInstanceSet();
                disabledEventInstances[colonyId] = instanceSet;
            }
            return instanceSet;
        }

        // Gets the DisabledInstanceSet for a given colony, or null if none exists.
        public DisabledInstanceSet GetInstanceSet(string colonyId)
        {
            if (string.IsNullOrEmpty(colonyId) || disabledEventInstances == null)
                return null;
            disabledEventInstances.TryGetValue(colonyId, out var instanceSet);
            return instanceSet;
        }

        /// <summary>
        /// Moves each name of the schema-1 list into the set of the one def kind
        /// it names. A name that names no loaded def, or more than one kind, has no
        /// category to go to and is dropped - so is "ThreatBig", which threat
        /// tracking no longer filters by. Needs the def databases, so it runs from
        /// BlacklistMigrationStartup.
        /// </summary>
        public bool TryMigrateLegacyTypeFilters()
        {
            EnsureCollections();
            if (filterSchemaVersion >= EventFilterPolicy.CurrentFilterSchemaVersion)
                return false;

            foreach (string defName in new List<string>(disabledEventDefNames))
            {
                EventFilterCategory? category = EventFilterPolicy.ResolveLegacyCategory(
                    DefDatabase<QuestScriptDef>.GetNamedSilentFail(defName) != null,
                    DefDatabase<GameConditionDef>.GetNamedSilentFail(defName)?.displayOnUI ?? false,
                    DefDatabase<SitePartDef>.GetNamedSilentFail(defName) != null);
                if (category == EventFilterCategory.Quest)
                    DisableType(EventCategory.Quest, defName);
                else if (category == EventFilterCategory.MapCondition)
                    DisableType(EventCategory.MapCondition, defName);
                else if (category == EventFilterCategory.SitePart)
                    DisableType(EventCategory.SitePart, defName);
            }

            disabledEventDefNames.Clear();
            filterSchemaVersion = EventFilterPolicy.CurrentFilterSchemaVersion;
            return true;
        }

        private HashSet<string> TypeRules(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.Quest: return disabledQuestDefNames;
                case EventCategory.MapCondition: return disabledMapConditionDefNames;
                case EventCategory.SitePart: return disabledSitePartDefNames;
                default: return null;
            }
        }

        private void EnsureCollections()
        {
            disabledEventDefNames ??= new HashSet<string>();
            disabledQuestDefNames ??= new HashSet<string>();
            disabledMapConditionDefNames ??= new HashSet<string>();
            disabledSitePartDefNames ??= new HashSet<string>();
            disabledEventInstances ??= new Dictionary<string, DisabledInstanceSet>();
        }
    }
}

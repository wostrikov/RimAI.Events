using RimWorld;
using System.Collections.Generic;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Events
{
    // Helper class for migrating the old XML-based quest blacklist to the new EventFilterSettings system.
    // Migration happens during mod initialization after defs are loaded.
    public static class BlacklistMigrationHelper
    {
        // Migrates blacklisted quests from XML definitions to EventFilterSettings.
        // Only runs once per settings file based on the questBlacklistMigrated flag.
        // Safe to call during mod initialization as DefDatabase is already populated.
        // Returns true if migration was performed, false if already migrated or skipped.
        public static bool TryMigrateBlacklist(EventFilterSettings settings)
        {
            if (settings == null)
                return false;

            // Check if migration has already been completed
            if (settings.questBlacklistMigrated)
                return false;

            try
            {
                var blacklistedDefs = new List<string>();
                var validatedDefs = new List<string>();
                var skippedDefs = new List<string>();

                // Read all entries from DefDatabase<QuestBlacklistDef>
                // This is safe at mod initialization time as defs are already loaded
                var defs = DefDatabase<QuestBlacklistDef>.AllDefsListForReading;
                if (defs != null && defs.Count > 0)
                {
                    foreach (var def in defs)
                    {
                        if (def?.blacklistedQuestRoots == null)
                            continue;

                        foreach (var questDefName in def.blacklistedQuestRoots)
                        {
                            if (string.IsNullOrEmpty(questDefName))
                                continue;

                            blacklistedDefs.Add(questDefName);
                        }
                    }
                }

                // Validate each blacklisted quest def name
                foreach (var questDefName in blacklistedDefs)
                {
                    // Check if the corresponding QuestScriptDef exists
                    var questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(questDefName);
                    if (questDef != null)
                    {
                        // Quest def exists, migrate it
                        settings.disabledEventDefNames.Add(questDefName);
                        validatedDefs.Add(questDefName);
                    }
                    else
                    {
                        // Quest def doesn't exist in current mod loadout, skip it
                        skippedDefs.Add(questDefName);
                    }
                }

                // Mark migration as complete
                settings.questBlacklistMigrated = true;

                // Log migration results
                if (validatedDefs.Count > 0 || skippedDefs.Count > 0)
                {
                    RimAiLog.Info(RimAiLogCategory.Events, $"[RimAI.Events] Blacklist migration completed:");
                    if (validatedDefs.Count > 0)
                    {
                        RimAiLog.Info(RimAiLogCategory.Events, $"  - Migrated {validatedDefs.Count} quest type(s) to new filter system: {string.Join(", ", validatedDefs)}");
                    }
                    if (skippedDefs.Count > 0)
                    {
                        RimAiLog.Info(RimAiLogCategory.Events, $"  - Skipped {skippedDefs.Count} quest type(s) not found in current mod loadout: {string.Join(", ", skippedDefs)}");
                    }
                }
                else
                {
                    RimAiLog.Info(RimAiLogCategory.Events, "[RimAI.Events] Blacklist migration completed: No blacklist entries found.");
                }
                return true; // Migration was performed
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — one-shot blacklist migration must not abort settings load
            catch (System.Exception ex)
            {
                RimAiLog.Error(RimAiLogCategory.Events, "[RimAI.Events] Error during blacklist migration: " + ex);
                // Still mark as migrated to avoid repeated failures
                settings.questBlacklistMigrated = true;
                return true;
            }
        }
    }
}

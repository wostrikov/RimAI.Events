using Ustas.RimAI.Core.Handshake;
using Verse;

namespace Ustas.RimAI.Events
{
    [StaticConstructorOnStartup]
    public static class BlacklistMigrationStartup
    {
        static BlacklistMigrationStartup()
        {
            if (!RimAiHandshake.IsApproved(RimAiModuleIds.Events))
            {
                return;
            }

            // Now DefDatabase is fully populated. Type filters first: the
            // blacklist migration writes into the per-category sets.
            bool changed = EventsMod.Settings?.TryMigrateLegacyTypeFilters() == true;
            changed |= BlacklistMigrationHelper.TryMigrateBlacklist(EventsMod.Settings);
            if (changed)
            {
                EventsMod.Instance.WriteSettings();
            }
        }
    }
}
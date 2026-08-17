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

            // Now DefDatabase is fully populated
            if (BlacklistMigrationHelper.TryMigrateBlacklist(EventsMod.Settings))
            {
                EventsMod.Instance.WriteSettings();
            }
        }
    }
}
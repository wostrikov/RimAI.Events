using HarmonyLib;
using System;
using System.Reflection;
using Verse;
using Ustas.RimAI.Core.Diagnostics;

namespace Ustas.RimAI.Events
{
    [StaticConstructorOnStartup]
    public static class EnhancedPromptDetector
    {
        private const string PACKAGE_ID = "ruaji.rimtalkpromptenhance";

        public static readonly bool IsLoaded;

        // Cached reflection metadata only
        private static readonly FieldInfo _settingsField;
        private static readonly FieldInfo _enableAutoEventCaptureField;
        private static readonly PropertyInfo _enableAutoEventCaptureProperty;

        public static bool IsAutoEventCaptureEnabled
        {
            get
            {
                // Fast path: mod not loaded or reflection setup failed
                if (!IsLoaded || _settingsField == null
                    || (_enableAutoEventCaptureField == null && _enableAutoEventCaptureProperty == null))
                    return false;

                try
                {
                    // Fetch settings instance fresh
                    var settingsInstance = _settingsField.GetValue(null);
                    if (settingsInstance == null)
                        return false;

                    // Read current value fresh (prefer property over field)
                    if (_enableAutoEventCaptureProperty != null)
                        return (bool)_enableAutoEventCaptureProperty.GetValue(settingsInstance);

                    return (bool)_enableAutoEventCaptureField.GetValue(settingsInstance);
                }
                // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — optional third-party settings adapter must fail closed
                catch (Exception ex)
                {
                    RimAiLog.WarningOnce(RimAiLogCategory.Events, "[RimAI.Events] EnhancedPromptDetector settings read failed: " + ex, 0x51E11);
                    return false;
                }
            }
        }

        static EnhancedPromptDetector()
        {
            IsLoaded = ModLister.GetActiveModWithIdentifier(PACKAGE_ID, ignorePostfix: true) != null;

            if (!IsLoaded)
                return;

            RimAiLog.Info(RimAiLogCategory.Events, "[RimAI.Events] Detected RimTalk Enhanced Prompt mod.");

            // Cache reflection metadata at startup
            try
            {
                var modType = AccessTools.TypeByName("RimTalkHealthEnhance.RimTalkHealthEnhanceMod");
                if (modType == null)
                {
                    RimAiLog.Warning(RimAiLogCategory.Events, "[RimAI.Events] Could not find RimTalkHealthEnhanceMod type for caching.");
                    return;
                }

                _settingsField = AccessTools.Field(modType, "Settings");
                if (_settingsField == null)
                {
                    RimAiLog.Warning(RimAiLogCategory.Events, "[RimAI.Events] Could not find Settings field for caching.");
                    return;
                }

                var settingsInstance = _settingsField.GetValue(null);
                if (settingsInstance == null)
                {
                    RimAiLog.Warning(RimAiLogCategory.Events, "[RimAI.Events] Settings instance is null at startup; will retry on access.");
                    return;
                }

                _enableAutoEventCaptureProperty = AccessTools.Property(
                    settingsInstance.GetType(),
                    "EnableAutoEventCapture"
                );

                if (_enableAutoEventCaptureProperty == null)
                {
                    _enableAutoEventCaptureField = AccessTools.Field(
                        settingsInstance.GetType(),
                        "EnableAutoEventCapture"
                    );
                }

                if (_enableAutoEventCaptureProperty != null || _enableAutoEventCaptureField != null)
                {
                    RimAiLog.Info(RimAiLogCategory.Events, "[RimAI.Events] Successfully cached Enhanced Prompt settings accessor.");
                }
                else
                {
                    RimAiLog.Warning(RimAiLogCategory.Events, "[RimAI.Events] Could not find EnableAutoEventCapture field; feature detection disabled.");
                }
            }
            // RimAI.catch-boundary: ALLOWED_TOP_LEVEL_BOUNDARY — optional third-party Enhanced Prompt reflection must fail closed
            catch (System.Exception ex)
            {
                RimAiLog.Warning(RimAiLogCategory.Events, "[RimAI.Events] Failed to cache Enhanced Prompt settings: " + ex);
            }
        }
    }
}
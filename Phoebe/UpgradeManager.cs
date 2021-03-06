using System;
using Toggl.Phoebe.Data;
using XPlatUtils;

namespace Toggl.Phoebe
{
    public sealed class UpgradeManger
    {
        private const string Tag = "UpgradeManager";

        public void TryUpgrade ()
        {
            var settingsStore = ServiceContainer.Resolve<ISettingsStore> ();
            var platformInfo = ServiceContainer.Resolve<IPlatformInfo> ();
            var log = ServiceContainer.Resolve<Logger> ();

            var oldVersion = settingsStore.LastAppVersion;
            var newVersion = platformInfo.AppVersion;

            // User hasn't upgraded, do nothing.
            if (oldVersion == newVersion)
                return;

            log.Info (Tag, "App has been upgraded from '{0}' to '{1}'", oldVersion, newVersion);

            UpgradeAlaways ();

            settingsStore.LastAppVersion = newVersion;
        }

        private void UpgradeAlaways ()
        {
            var settingsStore = ServiceContainer.Resolve<ISettingsStore> ();
            var dataStore = ServiceContainer.Resolve<IDataStore> ();

            // Mark all non-dirty remote items as never having been modified:
            dataStore.ResetAllModificationTimes ().Wait ();

            // Reset sync last run
            settingsStore.SyncLastRun = null;
        }
    }
}

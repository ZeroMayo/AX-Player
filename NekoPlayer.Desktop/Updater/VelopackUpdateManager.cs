// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using NekoPlayer.App;
using NekoPlayer.App.Graphics.Containers;
using NekoPlayer.App.Localisation;
using NekoPlayer.App.Overlays;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osuTK.Graphics;
using Velopack;
using Velopack.Sources;
using YoutubeExplode.Videos.Streams;
using UpdateManager = NekoPlayer.App.Updater.UpdateManager;

namespace NekoPlayer.Desktop.Updater
{
    public partial class VelopackUpdateManager : UpdateManager
    {
        [Resolved]
        private NekoPlayerAppBase game { get; set; } = null!;

        [Resolved]
        private PushNotificationOverlay pushNotificationOverlay { get; set; } = null!;

#nullable enable
        private ScheduledDelegate? scheduledBackgroundCheck;
#nullable disable

        private void scheduleNextUpdateCheck()
        {
            scheduledBackgroundCheck?.Cancel();
            scheduledBackgroundCheck = Scheduler.AddDelayed(() =>
            {
                log("Running scheduled background update check...");
                CheckForUpdate();
            }, 60000 * 30);
        }

#nullable enable
        protected override async Task<bool> PerformUpdateCheck(CancellationToken cancellationToken)
        {
            scheduledBackgroundCheck?.Cancel();

            try
            {
                IUpdateSource updateSource = new GithubSource(@"https://github.com/ZeroMayo/NekoPlayer", null, ReleaseStream.Value == App.Config.ReleaseStream.Beta);
                Velopack.UpdateManager updateManager = new Velopack.UpdateManager(updateSource, new UpdateOptions
                {
                    AllowVersionDowngrade = true
                });

                UpdateInfo? update = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    log("Update check cancelled");
                    scheduleNextUpdateCheck();
                    return true;
                }

                if (update == null)
                {
                    // No update is available.
                    log("No update found");
                    scheduleNextUpdateCheck();
                    return false;
                }

                // Download update in the background while notifying awaiters of the update being available.
                log($"New update available: {update.TargetFullRelease.Version}");
                downloadUpdate(updateManager, update, cancellationToken);
                return true;
            }
            catch (Exception e)
            {
                log($"Update check failed with error ({e.Message})");

                // we shouldn't crash on a web failure. or any failure for the matter.
                scheduleNextUpdateCheck();
                return true;
            }
        }
#nullable disable

        private void downloadUpdate(Velopack.UpdateManager updateManager, UpdateInfo update, CancellationToken cancellationToken) => Task.Run(async () =>
        {
            log($"Beginning download of update {update.TargetFullRelease.Version}...");
            game.UpdateButtonEnabled.Value = false;

            try
            {
                await updateManager.DownloadUpdatesAsync(update, p =>
                {
                    game.UpdateManagerVersionText.Value = NekoPlayerStrings.DownloadingUpdate($"{p}");
                    game.UpdateButtonEnabled.Value = false;
                }, cancellationToken).ConfigureAwait(false);
                game.UpdateManagerVersionText.Value = NekoPlayerStrings.RestartRequired;
                Schedule(() =>
                {
                    pushNotificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Sync, Color4.Green, NekoPlayerStrings.RestartRequired, NekoPlayerStrings.Updates)
                    {
                        Action = () => restartToApplyUpdate(updateManager, update),
                    });
                });
                game.RestartRequired.Value = true;
                game.RestartAction = () => restartToApplyUpdate(updateManager, update);
                game.UpdateButtonEnabled.Value = true;
            }
            catch (Exception e)
            {
                // In the case of an error, a separate notification will be displayed.
                game.UpdateManagerVersionText.Value = NekoPlayerStrings.UpdateFailed;
                Schedule(() =>
                {
                    pushNotificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Sync, Color4.Red, NekoPlayerStrings.UpdateFailed, NekoPlayerStrings.Updates));
                });
                game.UpdateButtonEnabled.Value = true;
                Logger.Error(e, @"Update failed!");
            }

            return true;
        }, cancellationToken);

        private void restartToApplyUpdate(Velopack.UpdateManager updateManager, UpdateInfo update) => Task.Run(() =>
        {
            updateManager.WaitExitThenApplyUpdates(update.TargetFullRelease);
            Schedule(() => game.AttemptExit());
        });

        private static void log(string text) => Logger.Log($"VelopackUpdateManager: {text}");
    }
}

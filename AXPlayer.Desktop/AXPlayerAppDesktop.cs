// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AXPlayer.App;
using AXPlayer.App.Extensions;
using AXPlayer.App.Updater;
using AXPlayer.App.Utils;
using AXPlayer.Desktop.Linux;
using AXPlayer.Desktop.Updater;
using osu.Framework;
using Tmds.DBus;

namespace AXPlayer.Desktop
{
    internal partial class AXPlayerAppDesktop : AXPlayerApp
    {
        protected override UpdateManager CreateUpdateManager() => new VelopackUpdateManager();

        private TrayIconManager trayIconManager;

        public override bool RestartAppWhenExited()
        {
            Task.Run(() => Velopack.UpdateExe.Start(waitPid: (uint)Environment.ProcessId)).FireAndForget();
            return true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            LoadComponentAsync(trayIconManager = new TrayIconManager(), Add);

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                trayIconManager.OnHidedToTrayIcon = () =>
                {
                    Task.Run(async () =>
                    {
                        using (var client = Connection.Session)
                        {
                            await client.ConnectAsync();
                            Console.WriteLine("Client connected");

                            var proxy = client.CreateProxy<IFreeDesktopNotifications>(
                                "org.freedesktop.Notifications",
                                FreeDesktopNotificationsInfo.Path
                            );

                            var notificationId = await ShowNotification(proxy, "AX Player", AXPlayerTrayIconExtraStrings.HidedToTrayIcon);
                        }
                    });
                };
            }
        }

        public override void RequestHideToTrayIcon()
        {
            base.RequestHideToTrayIcon();
            trayIconManager.HideToTrayIcon();
        }

        private static async Task<uint> ShowNotification(IFreeDesktopNotifications proxy, string title, string desc, uint replaceId = 0)
        {
            var notificationId = await proxy.NotifyAsync(
                "AXPlayer"
                , replaceId
                , $"{Directory.GetCurrentDirectory()}/AXPlayer-Icon.png"
                , title
                , desc
                , new string[] { }
                , new Dictionary<string, object>{}
                , 10 * 1000
            );
            Console.WriteLine("Created notification {0}", notificationId);
            return notificationId;
        }

        public override MediaSession CreateMediaSession()
        {
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
                return new LinuxMediaSessionHandler();

            return new MediaSession();
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using AXPlayer.App;
using AXPlayer.App.Extensions;
using AXPlayer.App.Updater;
using AXPlayer.App.Utils;
using AXPlayer.Desktop.Updater;
using osu.Framework;

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
        }

        public override void RequestHideToTrayIcon()
        {
            base.RequestHideToTrayIcon();
            trayIconManager.HideToTrayIcon();
        }

        public override MediaSession CreateMediaSession()
        {
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
                return new LinuxMediaSessionHandler();

            return new MediaSession();
        }
    }
}

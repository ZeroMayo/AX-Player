// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.Desktop.LowLatency;
using osu.Framework;
using osu.Framework.Development;
using osu.Framework.Platform;
using SDL;
using Velopack;

namespace AXPlayer.Desktop
{
    public static class Program
    {
        public static void Main()
        {
            string gameName = @"NekoPlayer";

            VelopackApp.Build().Run();

            // NVIDIA profiles are based on the executable name of a process.
            // Stable sets this setting to "Off", which may not be what we want, so let's force it back to the default "Auto" on startup.
            if (OperatingSystem.IsWindows())
                NVAPI.ThreadedOptimisations = NvThreadControlSetting.OGL_THREAD_CONTROL_DEFAULT;

            // This is a safe default. Localised usages should specify lower values as required.
            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(1000));

            HostOptions hostOptions = new HostOptions
            {
                FriendlyGameName = "AX Player",
            };

            if (DebugUtils.IsDebugBuild)
                gameName = "NekoPlayer-development";


            using (GameHost host = Host.GetSuitableDesktopHost(gameName, hostOptions))
            {
                // Attempt to use the NVAPI Low Latency Provider. This should only succeed on systems with NVIDIA GPUs and the proper drivers installed.
                if (NVAPI.Available)
                    host.SetLowLatencyProvider(new NVAPIDirect3D11LowLatencyProvider());

                //host.AllowBenchmarkUnlimitedFrames = true;
                host.Run(new AXPlayerAppDesktop());
            }
        }
    }
}

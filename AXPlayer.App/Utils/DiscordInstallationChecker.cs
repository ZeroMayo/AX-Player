// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.IO;
using System.Runtime.Versioning;

namespace AXPlayer.App.Utils
{
    [SupportedOSPlatform("windows")]
    public class DiscordInstallationChecker
    {
        public static bool IsDiscordInstalled()
        {
            // Discord installs to %LocalAppData%/Discord
            string appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string discordPath = Path.Combine(appDataLocal, "Discord", "Update.exe"); // Update.exe is a key component

            if (File.Exists(discordPath))
            {
                return true;
            }

            // You can also look for the specific version folders within the Discord directory
            // e.g., %LocalAppData%/Discord/app-1.0.9001/Discord.exe

            return false;
        }
    }
}

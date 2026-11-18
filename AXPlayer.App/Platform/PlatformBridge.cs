// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Platform
{
    /// <summary>
    /// Platform-specific operations that cannot be expressed through osu!framework alone.
    /// Mobile hosts install their own <see cref="OpenUrlHandler"/> before the game is created.
    /// </summary>
    public static class PlatformBridge
    {
        /// <summary>
        /// When set by a mobile host, replaces the default shell-based URL launcher.
        /// </summary>
        public static Func<string, bool>? OpenUrlHandler { get; set; }

        /// <summary>
        /// Opens a URL with the platform's default browser.
        /// </summary>
        /// <returns><c>true</c> when a browser launch was requested.</returns>
        public static bool OpenUrl(string url)
        {
            if (OpenUrlHandler != null)
            {
                try
                {
                    return OpenUrlHandler(url);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $@"Failed to launch a browser for '{url}': {exception.GetDescription()}");
                    return false;
                }
            }

            return openUrlWithShell(url);
        }

        private static bool openUrlWithShell(string url)
        {
            try
            {
                // See https://github.com/dotnet/corefx/issues/10361
                // This is best-effort only, but should work most of the time.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // See https://stackoverflow.com/a/6040946/44360 for why this is required
                    url = System.Text.RegularExpressions.Regex.Replace(url, @"(\\*)" + "\"", @"$1$1\" + "\"");
                    url = System.Text.RegularExpressions.Regex.Replace(url, @"(\\+)$", @"$1$1");
                    Process.Start(new ProcessStartInfo(@"cmd", $@"/c start """" ""{url}""") { CreateNoWindow = true });
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start(@"xdg-open", url);
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(@"open", url);
                    return true;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to launch a browser for '{url}': {exception.GetDescription()}");
            }

            return false;
        }
    }
}

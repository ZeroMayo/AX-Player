// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Platform;

namespace AXPlayer.App.Tests
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost("NekoPlayer-VisualTests"))
            using (var game = new AXPlayerTestBrowser())
                host.Run(game);
        }
    }
}

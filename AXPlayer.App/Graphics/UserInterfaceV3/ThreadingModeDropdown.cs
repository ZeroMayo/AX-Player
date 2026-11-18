// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Platform;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class ThreadingModeDropdown : FormEnumDropdown<ExecutionMode>
    {
        protected override LocalisableString GenerateItemText(ExecutionMode item)
        {
            switch (item)
            {
                case ExecutionMode.SingleThread:
                    return AXPlayerStrings.SingleThread;

                case ExecutionMode.MultiThreaded:
                    return AXPlayerStrings.Multithreaded;
            }
            return base.GenerateItemText(item);
        }
    }
}

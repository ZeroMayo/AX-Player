// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class WindowModeDropdown : FormDropdown<WindowMode>
    {
        protected override LocalisableString GenerateItemText(WindowMode item)
        {
            switch (item)
            {
                case WindowMode.Windowed:
                    return AXPlayerStrings.Windowed;

                case WindowMode.Borderless:
                    return AXPlayerStrings.Borderless;

                case WindowMode.Fullscreen:
                    return AXPlayerStrings.Fullscreen;
            }
            return base.GenerateItemText(item);
        }
    }
}

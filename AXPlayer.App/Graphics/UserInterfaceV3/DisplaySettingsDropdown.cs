// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterfaceV2;
using osu.Framework.Localisation;
using osu.Framework.Platform;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class DisplaySettingsDropdown : FormDropdown<Display>
    {
        protected override LocalisableString GenerateItemText(Display item)
        {
            return $"{item.Index}: {item.Name} ({item.Bounds.Width}x{item.Bounds.Height})";
        }
    }
}

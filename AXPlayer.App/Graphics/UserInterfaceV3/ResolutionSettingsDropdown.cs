// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Drawing;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class ResolutionSettingsDropdown : FormDropdown<Size>
    {
        protected override LocalisableString GenerateItemText(Size item)
        {
            if (item == new Size(9999, 9999))
                return AXPlayerStrings.Default;

            return $"{item.Width}x{item.Height}";
        }
    }
}

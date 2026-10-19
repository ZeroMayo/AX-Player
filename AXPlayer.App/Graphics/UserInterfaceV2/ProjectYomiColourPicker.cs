// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public partial class ProjectYomiColourPicker : ColourPicker
    {
        public ProjectYomiColourPicker()
        {
            CornerRadius = 10;
            Masking = true;
        }

        protected override HSVColourPicker CreateHSVColourPicker() => new ProjectYomiHSVColourPicker();
        protected override HexColourPicker CreateHexColourPicker() => new ProjectYomiHexColourPicker();
    }
}

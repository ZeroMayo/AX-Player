// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using AXPlayer.App.Graphics.UserInterfaceV2;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Graphics
{
    public partial class SettingsIconButtonV2 : RoundedIconButton
    {
        public SettingsIconButtonV2(IconUsage icon)
            : base(icon)
        {
            RelativeSizeAxes = Axes.X;
        }
    }
}

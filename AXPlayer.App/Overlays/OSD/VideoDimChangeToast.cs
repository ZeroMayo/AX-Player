// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Extensions;
using AXPlayer.App.Localisation;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Overlays.OSD
{
    public partial class VideoDimChangeToast : ToastWithIcon
    {
        public VideoDimChangeToast(double newValue)
            : base(AXPlayerStrings.VideoDimLevel, newValue.ToStandardFormattedString(5, true), FontAwesome.Solid.Sun)
        {
        }
    }
}

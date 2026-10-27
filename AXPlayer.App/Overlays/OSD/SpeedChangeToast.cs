// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Localisation;

namespace AXPlayer.App.Overlays.OSD
{
    public partial class SpeedChangeToast : Toast
    {
        public SpeedChangeToast(double newSpeed)
            : base(AXPlayerStrings.PlaybackSpeedWithoutValue, $@"{newSpeed:0.##}x")
        {
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace NekoPlayer.App.Graphics.UserInterfaceV3
{
    public partial class PlaybackSpeedSliderBar : RoundedSliderBar<double>
    {
        public override LocalisableString TooltipText => NekoPlayerStrings.PlaybackSpeed(Current.Value);
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Config
{
    public enum AudioQuality
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.PreferHighQualityAudio))]
        PreferHighQuality,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.PreferOpus))]
        PreferOpus,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.PreferMp4a))]
        PreferMp4a,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.LowQuality))]
        LowQuality,
    }
}

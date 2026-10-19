// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Config
{
    public enum VideoQuality
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.PreferHighQuality))]
        PreferHighQuality,

        [Description("4320p (8K)")]
        Quality_8K,

        [Description("2160p (4K)")]
        Quality_4K,

        [Description("1440p (QHD)")]
        Quality_1440p,

        [Description("1080p (FHD)")]
        Quality_1080p,

        [Description("720p (HD)")]
        Quality_720p,

        [Description("480p")]
        Quality_480p,

        [Description("360p")]
        Quality_360p,

        [Description("240p")]
        Quality_240p,

        [Description("144p")]
        Quality_144p,
    }
}

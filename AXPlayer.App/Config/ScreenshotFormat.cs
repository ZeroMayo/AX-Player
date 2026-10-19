// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using AXPlayer.App.Localisation;
using System.ComponentModel;

namespace AXPlayer.App.Config
{
    public enum ScreenshotFormat
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Jpg))]
        Jpg = 1,

        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.Png))]
        Png = 2,

        [Description("WebP")]
        Webp = 3,
    }
}

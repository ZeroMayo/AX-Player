// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Config
{
    public enum ClosedCaptionLanguage
    {
        [LocalisableDescription(typeof(AXPlayerStrings), nameof(AXPlayerStrings.CaptionDisabled))]
        Disabled,

        [Description("English")]
        English,

        [Description("한국어")]
        Korean,

        [Description("日本語")]
        Japanese,
    }
}

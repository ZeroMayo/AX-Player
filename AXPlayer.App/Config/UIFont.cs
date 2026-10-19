// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Config
{
    public enum UIFont
    {
        [Description("Google Sans Flex")]
        GoogleSansFlex,
        [Description("Google Sans Flex Rounded")]
        GoogleSansFlexRounded,
        Rubik,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Pretendard))]
        Pretendard,
        Roboto,
    }
}

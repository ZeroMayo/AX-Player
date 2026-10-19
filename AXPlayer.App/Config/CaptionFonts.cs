// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Config
{
    public enum CaptionFonts
    {
        [Description("Google Sans Flex")]
        GoogleSansFlex,
        [Description("Google Sans Flex Rounded")]
        GoogleSansFlexRounded,
        Rubik,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Pretendard))]
        Pretendard,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Hungeul))]
        Hungeul,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Ownglyph_PDH))]
        Ownglyph_PDH,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Dovemayo_Gothic))]
        Dovemayo_Gothic,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Griun_Mongtori))]
        Griun_Mongtori,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.ONE_Mobile_POP))]
        ONE_Mobile_POP,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Cafe24Syongsyong))]
        Cafe24Syongsyong,
        Roboto,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.DreamHeumulKR))]
        DreamHeumulKR,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Hakgyoansim_ManitoR))]
        Hakgyoansim_ManitoR,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.OwnglyphDaisy))]
        OwnglyphDaisy,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.OwnglyphYuntaeng))]
        OwnglyphYuntaeng,
        [LocalisableDescription(typeof(CaptionFontStrings), nameof(CaptionFontStrings.Hippie))]
        Hippie,
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Humanizer;
using NekoPlayer.App.Extensions;
using osu.Framework.Localisation;

namespace NekoPlayer.App.Localisation
{
    public static class CaptionFontStrings
    {
        private const string prefix = @"NekoPlayer.App.Resources.Localisation.CaptionFonts";

        /// <summary>
        /// "Hungeul"
        /// </summary>
        public static LocalisableString Hungeul => new TranslatableString(getKey(@"hungeul"), "Hungeul");

        /// <summary>
        /// "Ownglyph PDH"
        /// </summary>
        public static LocalisableString Ownglyph_PDH => new TranslatableString(getKey(@"ownglyph_pdh"), "Ownglyph PDH");

        /// <summary>
        /// "Dovemayo Gothic"
        /// </summary>
        public static LocalisableString Dovemayo_Gothic => new TranslatableString(getKey(@"dovemayo_gothic"), "Dovemayo Gothic");

        /// <summary>
        /// "Pretendard"
        /// </summary>
        public static LocalisableString Pretendard => new TranslatableString(getKey(@"pretendard"), "Pretendard");

        /// <summary>
        /// "Griun Mongtori"
        /// </summary>
        public static LocalisableString Griun_Mongtori => new TranslatableString(getKey(@"griun_mongtori"), "Griun Mongtori");

        /// <summary>
        /// "ONE Mobile POP"
        /// </summary>
        public static LocalisableString ONE_Mobile_POP => new TranslatableString(getKey(@"one_mobile_pop"), "ONE Mobile POP");

        /// <summary>
        /// "Hayu Font"
        /// </summary>
        public static LocalisableString HayuFont => new TranslatableString(getKey(@"hayu_font"), "Hayu Font");

        /// <summary>
        /// "Puzzle Sans SUPER"
        /// </summary>
        public static LocalisableString PuzzleSansSuper => new TranslatableString(getKey(@"puzzle_sans_super"), "Puzzle Sans SUPER");

        /// <summary>
        /// "x12y12pxMaruMinyaHangul"
        /// </summary>
        public static LocalisableString x12y12pxMaruMinyaHangul => new TranslatableString(getKey(@"x12y12pxMaruMinyaHangul"), "x12y12pxMaruMinyaHangul");

        /// <summary>
        /// "Cafe24 Ssong Ssong"
        /// </summary>
        public static LocalisableString Cafe24Syongsyong => new TranslatableString(getKey(@"Cafe24Syongsyong"), "Cafe24 Ssong Ssong");

        /// <summary>
        /// "DreamHeumul KR"
        /// </summary>
        public static LocalisableString DreamHeumulKR => new TranslatableString(getKey(@"DreamHeumulKR"), "DreamHeumul KR");

        /// <summary>
        /// "Hakgyoansim Manito"
        /// </summary>
        public static LocalisableString Hakgyoansim_ManitoR => new TranslatableString(getKey(@"Hakgyoansim_ManitoR"), "Hakgyoansim Manito");

        /// <summary>
        /// "Ownglyph Daisy"
        /// </summary>
        public static LocalisableString OwnglyphDaisy => new TranslatableString(getKey(@"OwnglyphDaisy"), "Ownglyph Daisy");

        /// <summary>
        /// "Ownglyph Yuntaeng"
        /// </summary>
        public static LocalisableString OwnglyphYuntaeng => new TranslatableString(getKey(@"OwnglyphYuntaeng"), "Ownglyph Yuntaeng");

        /// <summary>
        /// "Hippie"
        /// </summary>
        public static LocalisableString Hippie => new TranslatableString(getKey(@"Hippie"), "Hippie");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

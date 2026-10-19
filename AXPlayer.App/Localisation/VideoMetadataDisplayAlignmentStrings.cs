// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Humanizer;
using AXPlayer.App.Extensions;
using osu.Framework.Localisation;

namespace AXPlayer.App.Localisation
{
    public static class VideoMetadataDisplayAlignmentStrings
    {
        private const string prefix = @"AXPlayer.App.Resources.Localisation.VideoMetadataDisplayAlignment";

        /// <summary>
        /// "Left"
        /// </summary>
        public static LocalisableString Left => new TranslatableString(getKey(@"left"), "Left");

        /// <summary>
        /// "Center"
        /// </summary>
        public static LocalisableString Center => new TranslatableString(getKey(@"center"), "Center");

        /// <summary>
        /// "Right"
        /// </summary>
        public static LocalisableString Right => new TranslatableString(getKey(@"right"), "Right");

        /// <summary>
        /// "Video Metadata Display Alignment"
        /// </summary>
        public static LocalisableString VideoMetadataDisplayAlignmentSetting => new TranslatableString(getKey(@"videoMetadataDisplayAlignmentSetting"), "Video Metadata Display Alignment");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

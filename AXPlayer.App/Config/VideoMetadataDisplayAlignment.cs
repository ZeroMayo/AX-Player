// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Localisation;
using osu.Framework.Localisation;

namespace AXPlayer.App.Config
{
    public enum VideoMetadataDisplayAlignment
    {
        [LocalisableDescription(typeof(VideoMetadataDisplayAlignmentStrings), nameof(VideoMetadataDisplayAlignmentStrings.Left))]
        Left,

        [LocalisableDescription(typeof(VideoMetadataDisplayAlignmentStrings), nameof(VideoMetadataDisplayAlignmentStrings.Center))]
        Center,

        [LocalisableDescription(typeof(VideoMetadataDisplayAlignmentStrings), nameof(VideoMetadataDisplayAlignmentStrings.Right))]
        Right,
    }
}

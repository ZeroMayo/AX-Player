// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using Humanizer;

namespace AXPlayer.App.Extensions
{
    /// <summary>
    /// Formats the parts of a video metadata line that a response is not required to carry.
    /// </summary>
    /// <remarks>
    /// The metadata lines are built on the update thread, where an exception ends the game instead of
    /// being caught, so the parts are formatted here and never unwrapped at the call site.
    /// </remarks>
    public static class VideoMetadataExtensions
    {
        /// <summary>
        /// The publish date of a video as a relative date, or an empty text when the response did not
        /// carry one.
        /// </summary>
        public static string toRelativeDate(this DateTimeOffset? published, DateTime? now = null)
            => published is { } value ? value.DateTime.Humanize(dateToCompareAgainst: now ?? DateTime.Now) : string.Empty;

        /// <summary>
        /// A counter of a video, such as its view count, as a formatted number.
        /// </summary>
        /// <remarks>
        /// The counters are 64 bit values. Narrowing them to an <see cref="int" /> overflows for the most
        /// watched videos, and narrowing a <see cref="ulong" /> to a <see cref="long" /> overflows for a
        /// counter that lost its sign, so the width is kept.
        /// </remarks>
        public static string toFormattedCount(this ulong? count)
            => (count ?? 0).ToStandardFormattedString(0);

        /// <summary>
        /// A counter of a video, such as its like count, as a formatted number.
        /// </summary>
        public static string toFormattedCount(this long? count)
            => (count ?? 0).ToStandardFormattedString(0);
    }
}

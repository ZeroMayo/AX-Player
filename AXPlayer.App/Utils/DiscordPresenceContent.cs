// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Xml;

namespace AXPlayer.App.Utils
{
    /// <summary>
    /// Helpers that build the values Discord accepts for a rich presence payload.
    /// </summary>
    /// <remarks>
    /// Discord rejects payloads with strings longer than <see cref="MaximumLength"/>, so values are
    /// built and clipped here instead of at every call site.
    /// </remarks>
    public static class DiscordPresenceContent
    {
        /// <summary>
        /// The maximum length Discord accepts for the <c>Details</c> and <c>State</c> fields.
        /// </summary>
        public const int MaximumLength = 128;

        private const string ellipsis = "...";

        /// <summary>
        /// Clips the given value to <paramref name="maxLength"/> characters, ending it with an ellipsis.
        /// </summary>
        public static string TruncateWithEllipsis(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length <= maxLength)
                return value;

            int truncateLength = Math.Max(0, maxLength - ellipsis.Length);

            // Building the result from a span keeps this at a single allocation instead of the two a
            // substring plus concatenation would need.
            return string.Concat(value.AsSpan(0, truncateLength), ellipsis);
        }

        /// <summary>
        /// Formats the video title shown as the presence details, appending the playback speed.
        /// </summary>
        /// <remarks>
        /// The title is clipped around the suffix so the speed stays visible while the combined
        /// string still fits into <see cref="MaximumLength"/>.
        /// </remarks>
        public static string FormatDetails(string? title, double playbackSpeed)
        {
            string suffix = IsUsableSpeed(playbackSpeed) && playbackSpeed != 1
                ? $" ({playbackSpeed:0.##}x speed)"
                : string.Empty;

            return TruncateWithEllipsis($"{TruncateWithEllipsis(title, MaximumLength - suffix.Length)}{suffix}", MaximumLength);
        }

        /// <summary>
        /// Resolves the timestamps that make Discord render the remaining playback time.
        /// </summary>
        /// <param name="progressSeconds">The position of the video in media time.</param>
        /// <param name="durationSeconds">The duration of the video in media time.</param>
        /// <param name="playbackSpeed">The speed the media is played at.</param>
        /// <param name="capturedAtUtc">The moment the position was captured.</param>
        /// <param name="start">The timestamp the presence should start its elapsed time from.</param>
        /// <param name="end">The timestamp the presence should count down to.</param>
        /// <returns>
        /// <c>false</c> when the duration is unknown (live streams, missing metadata), in which case no
        /// progress should be shown at all.
        /// </returns>
        public static bool TryResolveTimestamps(double progressSeconds, double durationSeconds, double playbackSpeed, DateTime capturedAtUtc, out DateTime start, out DateTime end)
        {
            start = capturedAtUtc;
            end = capturedAtUtc;

            if (!IsFinite(durationSeconds) || durationSeconds <= 0)
                return false;

            if (!IsFinite(progressSeconds) || progressSeconds < 0)
                progressSeconds = 0;

            if (!IsUsableSpeed(playbackSpeed))
                playbackSpeed = 1;

            // Remaining media time is worth more wall-clock time the slower the media plays.
            double elapsedSeconds = progressSeconds / playbackSpeed;
            double remainingSeconds = Math.Max(0, durationSeconds - progressSeconds) / playbackSpeed;

            start = capturedAtUtc - TimeSpan.FromSeconds(elapsedSeconds);
            end = capturedAtUtc + TimeSpan.FromSeconds(remainingSeconds);

            return true;
        }

        /// <summary>
        /// Parses an ISO 8601 duration (as returned by the YouTube API) into seconds.
        /// </summary>
        public static bool TryParseDurationSeconds(string? duration, out double seconds)
        {
            seconds = 0;

            if (string.IsNullOrEmpty(duration))
                return false;

            try
            {
                seconds = XmlConvert.ToTimeSpan(duration).TotalSeconds;
                return IsFinite(seconds) && seconds > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsUsableSpeed(double playbackSpeed) => IsFinite(playbackSpeed) && playbackSpeed > 0;

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

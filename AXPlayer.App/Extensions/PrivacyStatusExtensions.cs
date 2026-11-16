// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using osu.Framework.Localisation;

namespace AXPlayer.App.Extensions
{
    /// <summary>
    /// Maps the privacy status that the API reports for a playlist onto the values the player uses.
    /// </summary>
    /// <remarks>
    /// The Data API spells the privacy <c>public</c>, <c>unlisted</c> and <c>private</c>, while the
    /// library pages render the localised name of a badge. A row whose privacy is not known keeps an
    /// empty label instead of guessing one.
    /// </remarks>
    public static class PrivacyStatusExtensions
    {
        /// <summary>
        /// Reads the privacy of a playlist row.
        /// </summary>
        /// <returns>The status, or <see langword="null" /> when the row does not report one.</returns>
        public static PrivacyStatus? toPrivacyStatus(this string? privacyStatus)
        {
            switch (privacyStatus?.Trim().ToLowerInvariant())
            {
                case @"public":
                    return PrivacyStatus.Public;

                case @"unlisted":
                    return PrivacyStatus.Unlisted;

                case @"private":
                    return PrivacyStatus.Private;

                default:
                    return null;
            }
        }

        /// <summary>
        /// The label of the privacy of a playlist row.
        /// </summary>
        public static LocalisableString toPrivacyLabel(this string? privacyStatus)
        {
            switch (privacyStatus?.Trim().ToLowerInvariant())
            {
                case @"public":
                    return AXPlayerStrings.Public;

                case @"unlisted":
                    return AXPlayerStrings.Unlisted;

                case @"private":
                    return AXPlayerStrings.Private;

                default:
                    return string.Empty;
            }
        }
    }
}

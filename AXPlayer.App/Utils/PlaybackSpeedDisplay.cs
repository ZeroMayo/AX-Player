// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;

namespace AXPlayer.App.Utils
{
    /// <summary>
    /// Helpers for the playback speed text shown in the player controls.
    /// </summary>
    public static class PlaybackSpeedDisplay
    {
        /// <summary>
        /// Resolves the playback speed the controls should display.
        /// </summary>
        /// <param name="configuredSpeed">The speed configured on <c>MainAppView.playbackSpeed</c>.</param>
        /// <param name="activeSourceSpeed">
        /// The speed of the active video source, or <c>null</c> when there is no usable source
        /// (for example when it has been disposed while the next video is loading).
        /// </param>
        public static double ResolveSpeed(double configuredSpeed, double? activeSourceSpeed) =>
            activeSourceSpeed ?? configuredSpeed;

        /// <summary>
        /// Formats a playback speed as a percentage for the controls.
        /// </summary>
        public static string Format(double speed) => $"{(int)Math.Round(speed * 100)}%";
    }
}

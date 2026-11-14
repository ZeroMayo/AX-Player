// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AXPlayer.App.Utils
{
    /// <summary>
    /// Helpers for the custom playback speed presets stored in <c>AXPlayerSetting.CustomPlaybackSpeedPresets</c>.
    /// </summary>
    public static class PlaybackSpeedPresets
    {
        public static readonly double[] BUILT_IN = { 0.5, 0.75, 1, 1.25, 1.5, 2 };

        /// <summary>
        /// Parses the serialized custom preset list, ignoring invalid, duplicate and built-in speeds.
        /// </summary>
        public static List<double> Parse(string? value)
        {
            List<double> presets = new List<double>();

            if (string.IsNullOrWhiteSpace(value))
                return presets;

            foreach (string part in value.Split(','))
            {
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double speed)
                    && speed >= 0.1 && speed <= 8)
                {
                    speed = Math.Round(speed, 2);
                    if (!BUILT_IN.Contains(speed) && !presets.Contains(speed))
                        presets.Add(speed);
                }
            }

            return presets;
        }

        /// <summary>
        /// Serializes custom presets to the format stored in <c>AXPlayerSetting.CustomPlaybackSpeedPresets</c>.
        /// </summary>
        public static string Serialize(IEnumerable<double> presets) =>
            string.Join(",", presets.Select(speed => speed.ToString("0.##", CultureInfo.InvariantCulture)));
    }
}

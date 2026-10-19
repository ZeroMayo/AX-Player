// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;

namespace AXPlayer.App.Audio
{
    /// <summary>
    /// Audio Normalization Data
    /// </summary>
    public partial class AudioNormalization : CompositeDrawable, IEquatable<AudioNormalization>
    {
        /// <summary>
        /// The target level for audio normalization
        /// https://en.wikipedia.org/wiki/EBU_R_128
        /// </summary>
        public const int TARGET_LEVEL = -14;

        /// <summary>
        /// The integrated (average) loudness of the audio
        /// </summary>
        public float? IntegratedLoudness { get; init; }

        /// <summary>
        /// A volume offset that can be applied upon audio to reach <see cref="TARGET_LEVEL"/> (converted from integrated loudness).
        /// </summary>
        public double? IntegratedLoudnessInVolumeOffset => IntegratedLoudness != null ? TrackLoudness.ConvertToVolumeOffset(TARGET_LEVEL, (float)IntegratedLoudness) : null;

        public AudioNormalization()
        {
        }

        /// <summary>
        /// Get the audio normalization for a beatmap. The loudness normalization value is stored in the object under <see cref="IntegratedLoudness"/>
        /// </summary>
        /// <param name="audioFilePath">the audio file path</param>
        public AudioNormalization(string audioFilePath)
        {
            string audiofile = audioFilePath;

            if (string.IsNullOrEmpty(audiofile))
            {
                Logger.Log("Audio file not found!!!", LoggingTarget.Runtime, LogLevel.Error);
                return;
            }

            using (FileStream fs = File.OpenRead(audioFilePath))
            {
                using TrackLoudness loudness = new TrackLoudness(fs);

                float? integratedLoudness = loudness.GetIntegratedLoudness();

                if (integratedLoudness == null)
                {
                    Logger.Log("Failed to get loudness level for " + audiofile, LoggingTarget.Runtime, LogLevel.Error);
                    return;
                }

                IntegratedLoudness = integratedLoudness;
            }
        }

        /// <inheritdoc />
        public bool Equals(AudioNormalization? other) => other?.IntegratedLoudness != null && IntegratedLoudness != null && Math.Abs((float)(IntegratedLoudness - other.IntegratedLoudness)) < 0.0000001;
    }
}

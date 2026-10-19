// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osuTK;
using AXPlayer.App.Config;

namespace AXPlayer.App.Audio
{
    /// <summary>
    /// Manages audio normalization.
    /// </summary>
    public partial class AudioNormalizationManager
    {
        /// <summary>
        /// Fallback volume for tracks that <see cref="TrackLoudness"/> failed to measure loudness.
        /// </summary>
        public const double FALLBACK_VOLUME = 0.8;

        private readonly Bindable<bool> audioNormalizationSetting;

        /// <summary>
        /// Samples assigned to hitobjects needs to bind to this bindable to normalize their volume in line with track.
        /// </summary>
        public readonly BindableDouble SampleNormalizeVolume = new BindableDouble(1.0);
        private AXPlayerAppBase app;

        /// <summary>
        /// Creates a new <see cref="AudioNormalizationManager"/>.
        /// </summary>
        /// <param name="app">The app.</param>
        public AudioNormalizationManager(AXPlayerAppBase app, AXPlayerConfigManager config)
        {
            this.app = app;
            audioNormalizationSetting = config.GetBindable<bool>(AXPlayerSetting.AudioNormalization);

            updateNormalization(audioNormalizationSetting.Value);
            audioNormalizationSetting.BindValueChanged(change => updateNormalization(change.NewValue));
        }

        private void updateNormalization(bool value)
        {
            if (value)
            {
                app.EnableTrackNormlization();
                Logger.Log($"Normalization value: {(int)(app.CurrentTrackNormalizeVolume.Value * 100)}%");
            }
            else
            {
                app.DisableTrackNormalization();
            }
        }
    }
}

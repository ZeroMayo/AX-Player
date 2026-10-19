// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using AXPlayer.App.Config;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace AXPlayer.App.Graphics.UserInterface
{
    /// <summary>
    /// Adds hover sounds to a drawable.
    /// Does not draw anything.
    /// </summary>
    public partial class HoverSounds : HoverSampleDebounceComponent
    {
        public readonly Bindable<bool> Enabled = new Bindable<bool>(true);

        private Sample sampleHover;

        protected readonly HoverSampleSet SampleSet;

        public HoverSounds(HoverSampleSet sampleSet = HoverSampleSet.Default)
        {
            SampleSet = sampleSet;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleHover = audio.Samples.Get($@"UI/{SampleSet.GetDescription()}-hover")
                          ?? audio.Samples.Get($@"UI/{HoverSampleSet.Default.GetDescription()}-hover");
        }

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        public override void PlayHoverSample()
        {
            if (!Enabled.Value)
                return;

            sampleHover.Frequency.Value = 0.98 + RNG.NextDouble(0.04);

            if (config.Get<bool>(AXPlayerSetting.PlayOverlaySFX))
                sampleHover.Play();
        }
    }
}

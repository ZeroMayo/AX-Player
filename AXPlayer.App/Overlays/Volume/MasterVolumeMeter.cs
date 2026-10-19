// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osuTK.Graphics;

namespace AXPlayer.App.Overlays.Volume
{
    public partial class MasterVolumeMeter : VolumeMeter
    {
        private MuteButton muteButton = null!;

        public Bindable<bool> IsMuted { get; } = new Bindable<bool>();

        private readonly BindableDouble muteAdjustment = new BindableDouble();

        [Resolved]
        private VolumeOverlay volumeOverlay { get; set; } = null!;

        public MasterVolumeMeter(LocalisableString name, float circleSize, Color4 meterColour)
            : base(name, circleSize, meterColour)
        {
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            IsMuted.BindValueChanged(muted =>
            {
                if (muted.NewValue)
                    Bindable.Value = 0;
                else
                    Bindable.Value = 1;
            });

            Add(muteButton = new MuteButton
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Blending = BlendingParameters.Additive,
                X = CircleSize / 2,
                Y = CircleSize * 0.23f,
                Current = { BindTarget = IsMuted }
            });

            muteButton.Current.ValueChanged += _ => volumeOverlay.Show();
        }

        public void ToggleMute() => muteButton.Current.Value = !muteButton.Current.Value;
    }
}

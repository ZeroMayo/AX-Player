// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using AXPlayer.App.Audio.Effects;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A row of vertical sliders, one for each band of the 10-band equalizer.
    /// </summary>
    public partial class TenBandEqualizerDisplay : CompositeDrawable, IFormControl
    {
        public const float LABEL_HEIGHT = 18;
        public const float HEIGHT = LABEL_HEIGHT * 2 + EqualizerBandSlider.HEIGHT;

        private readonly Bindable<float>[] gains = new Bindable<float>[TenBandEqualizer.BAND_COUNT];

        [Resolved]
        private AudioEffectsConfigManager audioEffectsConfig { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public TenBandEqualizerDisplay()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            Drawable[] columns = new Drawable[TenBandEqualizer.BAND_COUNT];

            for (int band = 0; band < TenBandEqualizer.BAND_COUNT; band++)
            {
                gains[band] = audioEffectsConfig.GetEqualizerGain(band);

                var gainText = new ProjectYomiSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = AXPlayerApp.DefaultFont.With(size: 11),
                    Colour = colourProvider.Content1,
                };

                gains[band].BindValueChanged(gain => gainText.Text = formatGain(gain.NewValue), true);

                columns[band] = new FillFlowContainer
                {
                    Width = EqualizerBandSlider.WIDTH,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Width = EqualizerBandSlider.WIDTH,
                            Height = LABEL_HEIGHT,
                            Child = gainText,
                        },
                        new EqualizerBandSlider
                        {
                            Current = { BindTarget = gains[band] },
                        },
                        new Container
                        {
                            Width = EqualizerBandSlider.WIDTH,
                            Height = LABEL_HEIGHT,
                            Child = new ProjectYomiSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = formatFrequency(TenBandEqualizer.BAND_FREQUENCIES[band]),
                                Font = AXPlayerApp.DefaultFont.With(size: 11),
                                Colour = colourProvider.Content2,
                            },
                        },
                    },
                };
            }

            InternalChild = new Container
            {
                Masking = true,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                CornerRadius = new CornersInfo(8, AXPlayerApp.UI_CORNER_RADIUS, 8, AXPlayerApp.UI_CORNER_RADIUS),
                Children = new Drawable[]
                {
                    new Box
                    {
                        Colour = overlayColourProvider.Background4.Darken(0.1f),
                        RelativeSizeAxes = Axes.Both,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        // Leaves room for the revert-to-default button of the enclosing settings item.
                        Padding = new MarginPadding {  Left = 16, Right = 16 },
                        Children = columns,
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (Bindable<float> gain in gains)
                gain.BindValueChanged(_ => ValueChanged?.Invoke());
        }

        public event Action ValueChanged = null!;

        public bool IsDefault
        {
            get
            {
                foreach (Bindable<float> gain in gains)
                {
                    if (Math.Abs(gain.Value) > 0.001f)
                        return false;
                }

                return true;
            }
        }

        public void SetDefault()
        {
            foreach (Bindable<float> gain in gains)
                gain.Value = 0;
        }

        public bool IsDisabled => false;

        public IEnumerable<LocalisableString> FilterTerms => new[] { AXPlayerStrings.Equalizer };

        private static string formatGain(float gain) => gain.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);

        private static string formatFrequency(int frequency) => frequency >= 1000
            ? $"{frequency / 1000}K"
            : frequency.ToString(CultureInfo.InvariantCulture);
    }
}

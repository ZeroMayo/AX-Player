// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Utils;
using Google.Apis.YouTube.v3.Data;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK.Graphics;
using PaletteNet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Vector2 = osuTK.Vector2;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class AXPlayerSeekBar<T> : ProjectYomiSliderBar<T>
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        protected readonly AXPlayerSeekBar.SliderNub Nub;
        protected readonly Path LeftBox;
        protected readonly Box RightBox, RightBoxGradient;
        protected readonly Container LeftBoxContainer;
        protected readonly Container RightBoxContainer;
        private readonly Container nubContainer;
        private readonly Container mainContent;

        private const float track_height = AXPlayerSeekBar.SliderNub.HEIGHT / 3f;
        private const float nub_overlap = 0f;
        private const float wave_frequency = 0.1f;
        private const float wave_point_spacing = 1f;

        private static readonly HttpClient httpClient = new HttpClient();
        private readonly List<Vector2> waveVertices = new List<Vector2>();

        private bool isWavy;
        private bool isNubGlowing;
        private bool waveIsFlat = true;
        private float lastWaveWidth = float.NaN;
        private float lastRangePadding = float.NaN;
        private int paletteRequestVersion;

        private Color4 accentColour;

        public Color4 AccentColour
        {
            get => accentColour;
            set
            {
                accentColour = value;
                updateColours();
            }
        }

        private Colour4 backgroundColour;

        public Color4 BackgroundColour
        {
            get => backgroundColour;
            set
            {
                backgroundColour = value;
                updateColours();
            }
        }

        public Bindable<double> PlaybackSpeed { get; } = new Bindable<double>(1);

        public Bindable<bool> IsPlaying { get; } = new Bindable<bool>();

        /// <summary>
        /// The action to use to reset the value of <see cref="SliderBar{T}.Current"/> to the default.
        /// Triggered on double click.
        /// </summary>
        public Action ResetToDefault { get; internal set; }

        public AXPlayerSeekBar()
        {
            Height = AXPlayerSeekBar.SliderNub.HEIGHT;
            //RangePadding = AXPlayerSeekBar.SliderNub.DEFAULT_EXPANDED_SIZE / 2;
            ResetToDefault = () =>
            {
                if (!Current.Disabled)
                    Current.SetDefault();
            };
            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Horizontal = 2 },
                    Child = mainContent = new CircularContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Children = new Drawable[]
                        {
                            LeftBoxContainer = new Container
                            {
                                Height = track_height,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Masking = false,
                                Children = new Drawable[] {
                                    LeftBox = new SmoothPath
                                    {
                                        AlwaysPresent = true,
                                        PathRadius = 5f,
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                },
                            },
                            RightBoxContainer = new Container
                            {
                                Height = track_height,
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Masking = true,
                                CornerRadius = new CornersInfo(track_height / 3, track_height / 3, track_height / 2, track_height / 2),
                                Children = new Drawable[] {
                                    RightBox = new Box
                                    {
                                        Height = track_height,
                                        Colour = backgroundColour,
                                        RelativeSizeAxes = Axes.X,
                                    },
                                    RightBoxGradient = new Box
                                    {
                                        Width = 50,
                                        Height = track_height,
                                        Colour = ColourInfo.GradientHorizontal(accentColour, backgroundColour),
                                        Alpha = 0.5f,
                                    },
                                },
                             },
                        },
                    },
                },
                nubContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Nub = new SliderNub
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        Colour = AccentColour,
                        RelativePositionAxes = Axes.X,
                        //OnDoubleClicked = () => ResetToDefault.Invoke(),
                    },
                },
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            AccentColour = Nub.Colour = overlayColourProvider.Content2;
            BackgroundColour = overlayColourProvider.Content2.Darken(1);
            WavyToggle = configManager.GetBindable<bool>(AXPlayerSetting.WavySeekBar);

            PlaybackSpeed.BindValueChanged(speed =>
            {
                this.TransformBindableTo(speedRolling, speed.NewValue, 2400, Easing.OutQuint);
            }, true);

            IsPlaying.BindValueChanged(what =>
            {
                this.TransformBindableTo(amplitudeAnimated, what.NewValue && WavyToggle.Value ? 3.5f : 0f, 400, Easing.OutQuint);
            }, true);

            WavyToggle.BindValueChanged(what =>
            {
                this.TransformBindableTo(amplitudeAnimated, what.NewValue && IsPlaying.Value ? 3.5f : 0f, 400, Easing.OutQuint);
            }, true);
        }

        private void updateColours()
        {
            LeftBox.FadeColour(accentColour, 500, Easing.OutQuint);
            Nub.FadeColour(accentColour, 500, Easing.OutQuint);
            RightBox.FadeColour(backgroundColour, 500, Easing.OutQuint);

            RightBoxGradient.FadeColour(ColourInfo.GradientHorizontal(accentColour, backgroundColour), 500, Easing.OutQuint);
        }

        [Resolved]
        private AXPlayerConfigManager configManager { get; set; }

        private Bindable<bool> WavyToggle;

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; } = null!;

        public void GetPalette(Video video)
        {
            ArgumentNullException.ThrowIfNull(video);

            int requestVersion = Interlocked.Increment(ref paletteRequestVersion);
            _ = updatePaletteAsync(video, requestVersion);
        }

        private async Task updatePaletteAsync(Video video, int requestVersion)
        {
            try
            {
                string? thumbnailUrl = video.Snippet?.Thumbnails?.High?.Url;

                if (string.IsNullOrWhiteSpace(thumbnailUrl))
                    throw new InvalidOperationException("The video does not have a high-resolution thumbnail.");

                byte[] imageBytes = await httpClient.GetByteArrayAsync(thumbnailUrl).ConfigureAwait(false);
                using Image<Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<Rgba32>(imageBytes);

                Palette palette = new PaletteBuilder().Generate(new BitmapHelper(bitmap));
                int? accentRgb = palette.LightMutedSwatch?.Rgb;
                int? backgroundRgb = palette.DarkMutedSwatch?.Rgb;

                if (accentRgb is null || backgroundRgb is null)
                    throw new InvalidOperationException("The thumbnail palette does not contain suitable muted colours.");

                applyPalette(requestVersion, System.Drawing.Color.FromArgb(accentRgb.Value), System.Drawing.Color.FromArgb(backgroundRgb.Value));
            }
            catch (Exception)
            {
                applyPalette(requestVersion, overlayColourProvider.Content2, overlayColourProvider.Content2.Darken(1));
            }
        }

        private void applyPalette(int requestVersion, Color4 accent, Color4 background)
        {
            Schedule(() =>
            {
                if (requestVersion != Volatile.Read(ref paletteRequestVersion))
                    return;

                AccentColour = accent;
                BackgroundColour = background;
            });
        }

        protected override void Update()
        {
            base.Update();

            if (lastRangePadding != RangePadding)
            {
                nubContainer.Padding = new MarginPadding { Horizontal = RangePadding };
                lastRangePadding = RangePadding;
            }

            updateWaveState();

            updateWavePath();
        }

        private readonly Bindable<double> speedRolling = new Bindable<double>(1);
        private readonly Bindable<float> amplitudeAnimated = new Bindable<float>(0);
        private readonly Bindable<float> amplitudeAnimated2 = new Bindable<float>(0);

        private void updateWaveState()
        {
            double minValue = double.CreateTruncating(CurrentNumber.MinValue);
            double maxValue = double.CreateTruncating(CurrentNumber.MaxValue);
            double value = double.CreateTruncating(CurrentNumber.Value);
            double range = maxValue - minValue;
            //bool shouldBeWavy = range > 0 && value >= minValue + range * 0.04 && value <= maxValue - range * 0.04;
            bool shouldBeWavy = true; // always wavy seekbar

            if (shouldBeWavy == isWavy)
                return;

            isWavy = shouldBeWavy;
            this.TransformBindableTo(amplitudeAnimated2, shouldBeWavy ? 1f : 0f, shouldBeWavy ? 1250 : 750, Easing.OutQuint);
        }

        private void updateWavePath()
        {
            float amplitude = amplitudeAnimated.Value * amplitudeAnimated2.Value;
            float width = Math.Max(0, LeftBoxContainer.Width - nub_overlap);
            bool isFlat = amplitude <= 0.001f;

            if (width == lastWaveWidth)
                return;

            waveVertices.Clear();

            if (isFlat)
            {
                waveVertices.Add(Vector2.Zero);
                waveVertices.Add(new Vector2(width - wave_point_spacing, 0));
            }
            else
            {
                float phase = (float)(Time.Current * -0.05 * speedRolling.Value);

                for (float x = wave_point_spacing; x < width; x += wave_point_spacing)
                    waveVertices.Add(new Vector2(x, MathF.Sin((x - phase) * wave_frequency) * amplitude));

                waveVertices.Add(new Vector2(width, MathF.Sin((width - phase) * wave_frequency) * amplitude));
            }

            LeftBox.Vertices = waveVertices;
            waveIsFlat = isFlat;
            lastWaveWidth = width;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindDisabledChanged(disabled =>
            {
                Alpha = disabled ? 0.3f : 1;
            }, true);
        }

        protected override bool ShouldHandleAsRelativeDrag(MouseDownEvent e)
            => Nub.ReceivePositionalInputAt(e.ScreenSpaceMouseDownPosition);

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            float trackWidth = mainContent.DrawWidth;
            float nubCentre = Nub.ToSpaceOfOtherDrawable(Vector2.Zero, mainContent).X;

            LeftBoxContainer.Width = Math.Clamp(RangePadding + nubCentre - (nub_overlap), 0, trackWidth);
            RightBoxContainer.Width = Math.Clamp(trackWidth - nubCentre - (nub_overlap) - RangePadding, 0, trackWidth);
        }

        protected override void UpdateValue(float value)
        {
            Nub.MoveToX(value, 250, Easing.OutQuint);
        }

        public partial class SliderNub : AXPlayerSeekBar.SliderNub
        {
            public Action? OnDoubleClicked { get; init; }

            protected override bool OnClick(ClickEvent e) => true;

            protected override bool OnDoubleClick(DoubleClickEvent e)
            {
                OnDoubleClicked?.Invoke();
                return true;
            }
        }
    }
}

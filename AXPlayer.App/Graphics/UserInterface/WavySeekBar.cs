// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Utils;
using Google.Apis.YouTube.v3.Data;
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
    public partial class WavySeekBar<T> : ProjectYomiSliderBar<T>
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        protected readonly SliderNub Nub;
        protected readonly SmoothPath LeftBox;
        protected readonly SmoothPath LeftBoxGlow, LeftBoxHighlight;
        protected readonly Box RightBox, RightBoxGradient, RightBoxHighlight;
        protected readonly Container LeftBoxContainer;
        protected readonly Container RightBoxContainer;
        private readonly Container nubContainer;
        private readonly Container mainContent;

        /// <summary>
        /// Thickness of the unplayed portion of the track.
        /// </summary>
        private const float track_height = 6f;

        /// <summary>
        /// Thickness (radius) of the wave drawn over the played portion of the track.
        /// </summary>
        private const float wave_radius = 3f;

        /// <summary>
        /// Radius of the soft glow which is rendered underneath the wave.
        /// </summary>
        private const float wave_glow_radius = 7f;

        /// <summary>
        /// Radius of the bright core which runs along the centre of the wave.
        /// </summary>
        private const float wave_highlight_radius = 1f;

        private const float wave_glow_alpha = 0.16f;
        private const float nub_overlap = 0f;
        private const float wave_frequency = 0.1f;
        private const float wave_point_spacing = 2f;

        private static readonly HttpClient httpClient = new HttpClient();

        private bool isWavy;
        private bool isDragging;
        private bool isBarHovered;
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

        private Color4 backgroundColour;

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

        public WavySeekBar()
        {
            Height = SliderNub.HEIGHT;
            //RangePadding = SliderNub.DEFAULT_EXPANDED_SIZE / 2;
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
                    Child = mainContent = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Children = new Drawable[]
                        {
                            RightBoxContainer = new Container
                            {
                                Height = track_height,
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Masking = true,
                                CornerRadius = new CornersInfo(track_height / 2),
                                Children = new Drawable[] {
                                    RightBox = new Box
                                    {
                                        Height = track_height,
                                        Colour = backgroundColour,
                                        RelativeSizeAxes = Axes.X,
                                    },
                                    // A very soft sheen gives the remaining track a slight glass-like curvature.
                                    RightBoxHighlight = new Box
                                    {
                                        Height = track_height,
                                        RelativeSizeAxes = Axes.X,
                                        Colour = ColourInfo.GradientVertical(Color4.White.Opacity(0.07f), Color4.White.Opacity(0)),
                                    },
                                    // The accent bleeds out of the nub into the remaining track, like a light trail.
                                    RightBoxGradient = new Box
                                    {
                                        Width = 56,
                                        Height = track_height,
                                        Blending = BlendingParameters.Additive,
                                        Colour = ColourInfo.GradientHorizontal(accentColour.Opacity(0.55f), accentColour.Opacity(0)),
                                        Alpha = 0.3f,
                                    },
                                },
                            },
                            LeftBoxContainer = new Container
                            {
                                Height = track_height,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Masking = false,
                                Children = new Drawable[] {
                                    LeftBoxGlow = createWave(wave_glow_radius, accentColour.Opacity(wave_glow_alpha), additive: true),
                                    LeftBox = createWave(wave_radius, accentColour),
                                    LeftBoxHighlight = createWave(wave_highlight_radius, Color4.White.Opacity(0.22f), additive: true),
                                },
                            },
                        },
                    },
                },
                nubContainer = new NubHoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    HoverChanged = hovered =>
                    {
                        isBarHovered = hovered;
                        updateNubHighlight();
                    },
                    Child = Nub = new SliderNub
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        RelativePositionAxes = Axes.X,
                        //OnDoubleClicked = () => ResetToDefault.Invoke(),
                    },
                },
            };
        }

        private static SmoothPath createWave(float radius, ColourInfo colour, bool additive = false) => new SmoothPath
        {
            AlwaysPresent = true,
            PathRadius = radius,
            Colour = colour,
            Blending = additive ? BlendingParameters.Additive : BlendingParameters.Mixture,
            // Path sizes itself to the vertices padded by PathRadius on every side, so shift it back by that
            // amount to keep the start of the played portion flush with the left edge of the track.
            Position = new Vector2(-radius, 0),
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            AccentColour = overlayColourProvider.Content2;
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
            LeftBoxGlow.FadeColour(accentColour.Opacity(wave_glow_alpha), 500, Easing.OutQuint);
            Nub.AccentColour = accentColour;
            RightBox.FadeColour(backgroundColour, 500, Easing.OutQuint);

            RightBoxGradient.FadeColour(ColourInfo.GradientHorizontal(accentColour.Opacity(0.55f), accentColour.Opacity(0)), 500, Easing.OutQuint);
        }

        [Resolved]
        private AXPlayerConfigManager configManager { get; set; } = null!;

        private Bindable<bool> WavyToggle = new Bindable<bool>();

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
            if (isWavy)
                return;

            isWavy = true;
            this.TransformBindableTo(amplitudeAnimated2, 1f, 1250, Easing.OutQuint);
        }

        private void updateWavePath()
        {
            float amplitude = amplitudeAnimated.Value * amplitudeAnimated2.Value;
            float width = Math.Max(0, LeftBoxContainer.Width - nub_overlap);
            bool isFlat = amplitude <= 0.001f;

            // A flat wave only needs to be rebuilt when the track is resized, a wavy one scrolls every frame.
            if (isFlat && width == lastWaveWidth)
                return;

            lastWaveWidth = width;

            int pointCount = Math.Max(2, (int)MathF.Ceiling(width / wave_point_spacing) + 1);
            var vertices = new Vector2[pointCount];

            float phase = (float)(Time.Current * -0.05 * speedRolling.Value);

            for (int i = 0; i < pointCount; i++)
            {
                float x = Math.Min(i * wave_point_spacing, width);
                float y = isFlat ? 0 : MathF.Sin((x - phase) * wave_frequency) * amplitude;
                vertices[i] = new Vector2(x, y);
            }

            LeftBox.Vertices = vertices;
            LeftBoxGlow.Vertices = vertices;
            LeftBoxHighlight.Vertices = vertices;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindDisabledChanged(disabled =>
            {
                Alpha = disabled ? 0.3f : 1;

                if (disabled)
                    Nub.Highlighted = false;
            }, true);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            bool handled = base.OnDragStart(e);

            if (handled)
            {
                isDragging = true;
                updateNubHighlight();
            }

            return handled;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            isDragging = false;
            updateNubHighlight();
            base.OnDragEnd(e);
        }

        private void updateNubHighlight()
            => Nub.Highlighted = !Current.Disabled && (isBarHovered || isDragging);

        protected override bool ShouldHandleAsRelativeDrag(MouseDownEvent e)
            => Nub.ReceivePositionalInputAt(e.ScreenSpaceMouseDownPosition);

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            float trackWidth = mainContent.DrawWidth;
            float nubCentre = Nub.ToSpaceOfOtherDrawable(Vector2.Zero, mainContent).X;

            LeftBoxContainer.Width = Math.Clamp(RangePadding + nubCentre - nub_overlap, 0, trackWidth);
            RightBoxContainer.Width = Math.Clamp(trackWidth - nubCentre - nub_overlap - RangePadding, 0, trackWidth);
        }

        protected override void UpdateValue(float value)
        {
            Nub.MoveToX(value, 250, Easing.OutQuint);
        }

        /// <summary>
        /// A full-size input surface spanning the bar, so that hovering anywhere over the seek bar can highlight the nub.
        /// </summary>
        private partial class NubHoverContainer : Container
        {
            public Action<bool>? HoverChanged;

            protected override bool OnHover(HoverEvent e)
            {
                HoverChanged?.Invoke(true);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                HoverChanged?.Invoke(false);
                base.OnHoverLost(e);
            }
        }
    }
}

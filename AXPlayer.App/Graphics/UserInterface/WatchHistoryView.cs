// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    /// <summary>
    /// A single entry of the watch history list.
    /// </summary>
    public partial class WatchHistoryView : ProjectYomiClickableContainer
    {
        private Sprite thumbnail = null!;
        private TruncatingSpriteText titleText = null!;
        private TruncatingSpriteText channelNameText = null!;
        private TruncatingSpriteText viewsText = null!;
        private TruncatingSpriteText lengthText = null!;
        private Box resumeBar = null!;
        private Box resumeBarBackground = null!;
        private AXPlayerLoadingLayer loading = null!;

        public Action<WatchHistoryView>? ClickAction { get; set; }

        public WatchHistoryItem Data = null!;

        [Resolved]
        private TextureStore textureStore { get; set; } = null!;

        public WatchHistoryView()
            : base(HoverSampleSet.Default)
        {
            Height = 110;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            CornerRadius = AXPlayerApp.UI_CORNER_RADIUS;
            Masking = true;

            base.Content.AddRange(new Drawable[]
            {
                bgLayer = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background4,
                    Alpha = 0.7f,
                },
                Hover = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(7),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Width = 190,
                            Height = 96,
                            Masking = true,
                            CornerRadius = 12,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black,
                                },
                                thumbnail = new Sprite
                                {
                                    Size = new Vector2(1, 1.5f),
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both,
                                    FillMode = FillMode.Stretch,
                                },
                                loading = new AXPlayerLoadingLayer(true, false, false),
                                resumeBarBackground = new Box
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 4,
                                    Colour = Color4.Black.Opacity(0.5f),
                                    Alpha = 0,
                                },
                                resumeBar = new Box
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 4,
                                    Width = 0,
                                    Colour = overlayColourProvider.Highlight1,
                                    Alpha = 0,
                                },
                                new Container
                                {
                                    Anchor = Anchor.BottomRight,
                                    Origin = Anchor.BottomRight,
                                    AutoSizeAxes = Axes.Both,
                                    Margin = new MarginPadding(4),
                                    Masking = true,
                                    CornerRadius = 4,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = Color4.Black.Opacity(0.75f),
                                        },
                                        lengthText = new TruncatingSpriteText
                                        {
                                            Margin = new MarginPadding { Horizontal = 5, Vertical = 1 },
                                            Font = AXPlayerApp.DefaultFont.With(size: 11, weight: "Bold"),
                                            Colour = Color4.White,
                                        },
                                    },
                                },
                            },
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Top = 25,
                                Bottom = 5,
                                Left = 200,
                                Right = 5,
                            },
                            Children = new Drawable[]
                            {
                                titleText = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = AXPlayerApp.DefaultFont.With(size: 17, weight: "Bold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                channelNameText = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Position = new Vector2(0, 17),
                                    Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"),
                                    Colour = overlayColourProvider.Background1,
                                },
                                viewsText = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Position = new Vector2(0, 17 + 13),
                                    Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"),
                                    Colour = overlayColourProvider.Background1,
                                },
                            },
                        },
                    },
                },
            });
        }

        private Box bgLayer = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Schedule(() => loading.Show());
        }

        public void UpdateData()
        {
            if (Data == null)
                return;

            Schedule(() =>
            {
                titleText.Text = Data.Title;
                channelNameText.Text = Data.ChannelTitle;
                viewsText.Text = buildSubtitle(Data);
                lengthText.Text = Data.LengthText ?? string.Empty;
                lengthText.Parent!.Alpha = string.IsNullOrEmpty(Data.LengthText) ? 0 : 1;

                if (Data.ResumePercent > 0)
                {
                    resumeBarBackground.Alpha = 1;
                    resumeBar.Alpha = 1;
                    resumeBar.Width = (float)Math.Clamp(Data.ResumePercent / 100.0, 0, 1);
                }
                else
                {
                    resumeBarBackground.Alpha = 0;
                    resumeBar.Alpha = 0;
                }
            });

            if (!string.IsNullOrEmpty(Data.ThumbnailUrl))
                GetThumbnail(Data.ThumbnailUrl).FireAndForget();
            else
                Schedule(() => loading.Hide());
        }

        private static string buildSubtitle(WatchHistoryItem item)
        {
            if (!string.IsNullOrEmpty(item.ViewCountText) && !string.IsNullOrEmpty(item.PublishedTimeText))
                return $@"{item.ViewCountText} • {item.PublishedTimeText}";

            return item.ViewCountText ?? item.PublishedTimeText ?? string.Empty;
        }

        public async Task GetThumbnail(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                Texture texture = await textureStore.GetAsync(url, cancellationToken).ConfigureAwait(false);
                Schedule(() => thumbnail.Texture = texture);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A missing thumbnail should never break the list.
            }
            finally
            {
                Schedule(() => loading.Hide());
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            Hover.FadeTo(0.1f, 500, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            Content.FadeColour(Color4.Gray, 2000, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            Content.FadeColour(Color4.White, 1000, Easing.OutQuint);
            base.OnMouseUp(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            Hover.FadeOut(500, Easing.OutQuint);
        }

        private Box Hover = null!;

        public Color4 BackgroundColour { get; set; }

        private Color4? flashColour;

        /// <summary>
        /// The colour the background will flash with when this button is clicked.
        /// </summary>
        public Color4 FlashColour
        {
            get => flashColour ?? BackgroundColour;
            set => flashColour = value;
        }

        private void trigger()
        {
            if (Enabled.Value)
                ClickAction?.Invoke(this);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!IsHovered)
                Hover.FadeOutFromOne(1600);

            Hover.FlashColour(FlashColour, 800, Easing.OutQuint);
            trigger();

            return base.OnClick(e);
        }
    }
}

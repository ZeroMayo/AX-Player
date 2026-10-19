// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using Humanizer;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class YouTubeSearchResultView : ProjectYomiClickableContainer
    {
        private Sprite thumbnail = null!;
        private ProjectYomiTextFlowContainer videoNameText = null!;
        private TruncatingSpriteText channelNameText = null!;
        public Action<YouTubeSearchResultView> ClickEvent = null!;
        private ProjectYomiSpriteText viewsText = null!;

        private Box bgLayer = null!;

        [Resolved]
        private YouTubeAPI api { get; set; } = null!;

        [Resolved]
        private AXPlayerAppBase app { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private AXPlayerConfigManager appConfig { get; set; } = null!;

        private Bindable<Localisation.Language> uiLanguage;

        public YouTubeSearchResultView()
            : base(HoverSampleSet.Default)
        {
            Height = 110;
        }

        [Resolved]
        private TextureStore textureStore { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            uiLanguage = app.CurrentLanguage.GetBoundCopy();

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
                new Container {
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
                            Children = new Drawable[] {
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
                                videoNameText = new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 17, weight: "ExtraBold"))
                                {
                                    RelativeSizeAxes = Axes.X,
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
                                    Position = new Vector2(0, (17 + 13)),
                                    Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"),
                                    Colour = overlayColourProvider.Background1,
                                },
                            }
                        },
                    }
                },
                loading = new AXPlayerLoadingLayer(true, false, false)
            });
        }

        public SearchResult Data = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Schedule(() => loading.Show());
        }

        public async Task GetThumbnail(string url, CancellationToken cancellationToken = default)
        {
            Texture north = await textureStore.GetAsync(Data.Snippet.Thumbnails.High.Url, cancellationToken);
            Schedule(() => { thumbnail.Texture = north; });
            Schedule(() => loading.Hide());
        }

        private AXPlayerLoadingSpinner loading = null!;

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

        protected Box Hover = null!;

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

        public Action<YouTubeSearchResultView>? ClickAction { get; set; }

        protected override bool OnClick(ClickEvent e)
        {
            if (!IsHovered)
                Hover.FadeOutFromOne(1600);

            Hover.FlashColour(FlashColour, 800, Easing.OutQuint);
            trigger();

            return base.OnClick(e);
        }

        public void UpdateData()
        {
            uiLanguage.UnbindEvents();
            Task.Run(async () =>
            {
                try
                {
                    if (Data.Id.Kind == "youtube#video")
                    {
                        DateTimeOffset? dateTime = Data.Snippet.PublishedAtDateTimeOffset;
                        DateTime now = DateTime.Now;
                        Channel channelData = api.GetChannel(Data.Snippet.ChannelId);
                        Video videoData = api.GetVideo(Data.Id.VideoId);

                        Schedule(() =>
                        {
                            Schedule(() =>
                            {
                                channelNameText.Text = api.GetLocalizedChannelTitle(channelData, true);
                                videoNameText.Text = api.GetLocalizedVideoTitle(videoData);
#pragma warning disable CS8629 // Nullable 값 형식이 null일 수 있습니다.
                                viewsText.Text = AXPlayerStrings.VideoMetadataDescWithoutChannelName(Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), dateTime.Value.DateTime.Humanize(dateToCompareAgainst: now));
#pragma warning restore CS8629 // Nullable 값 형식이 null일 수 있습니다.
                            });

                            uiLanguage.BindValueChanged(locale =>
                            {
                                Schedule(() =>
                                {
                                    channelNameText.Text = api.GetLocalizedChannelTitle(channelData, true);
                                    videoNameText.Text = api.GetLocalizedVideoTitle(videoData);
                                    viewsText.Text = AXPlayerStrings.VideoMetadataDescWithoutChannelName(Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), dateTime.Value.DateTime.Humanize(dateToCompareAgainst: now));
                                });
                            });
                        });
                    }
                    else if (Data.Id.Kind == "youtube#playlist")
                    {
                        DateTimeOffset? dateTime = Data.Snippet.PublishedAtDateTimeOffset;
                        DateTime now = DateTime.Now;
                        Playlist playlistData = api.GetPlaylistInfo(Data.Id.PlaylistId);
                        Channel channelData = api.GetChannel(playlistData.Snippet.ChannelId);

                        Schedule(() =>
                        {
                            Schedule(() =>
                            {
                                channelNameText.Text = api.GetLocalizedChannelTitle(channelData, true);
                                videoNameText.Text = playlistData.Snippet.Title;
                                viewsText.Text = string.Empty;
                            });

                            uiLanguage.BindValueChanged(locale =>
                            {
                                Schedule(() =>
                                {
                                    channelNameText.Text = api.GetLocalizedChannelTitle(channelData, true);
                                    videoNameText.Text = playlistData.Snippet.Title;
                                    viewsText.Text = string.Empty;
                                });
                            });
                        });
                    }

                    await GetThumbnail(Data.Snippet.Thumbnails.High.Url);
                }
                catch
                {
                    Hide();
                }
            });
        }
    }
}

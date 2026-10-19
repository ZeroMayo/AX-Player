// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using Humanizer;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osuTK;
using osuTK.Graphics;
using PaletteNet;
using SixLabors.ImageSharp.PixelFormats;
using YoutubeExplode.Videos.ClosedCaptions;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class PlaylistItemView : ProjectYomiClickableContainer
    {
        private Sprite thumbnail = null!;
        private TruncatingSpriteText videoNameText = null!;
        private TruncatingSpriteText channelNameText = null!;
        public Action<PlaylistItemView> ClickEvent = null!;
        private ProjectYomiSpriteText viewsText = null!;
        private ProjectYomiSpriteText indexText = null!;

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

        public int Index;

        public PlaylistItemView(int index)
            : base(HoverSampleSet.Default)
        {
            this.Index = index;
            Height = 110;
        }

        [Resolved]
        private TextureStore textureStore { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider? overlayColourProvider, ProjectYomiColour colour)
        {
            uiLanguage = app.CurrentLanguage.GetBoundCopy();

            BorderColour = overlayColourProvider?.Highlight1 ?? colour.Yellow;
            CornerRadius = AXPlayerApp.UI_CORNER_RADIUS;
            Masking = true;

#pragma warning disable CS8602 // null 가능 참조에 대한 역참조입니다.
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
                                new Box
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.75f), Color4.Black.Opacity(0)),
                                    Height = 50,
                                },
                                indexText = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Padding = new MarginPadding(8),
                                    Text = $"#{Index + 1}",
                                    Font = AXPlayerApp.DefaultFont.With(size: 17, weight: "Regular"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                loading = new AXPlayerLoadingLayer(true, false, false)
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
                                videoNameText = new TruncatingSpriteText
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
                                    Position = new Vector2(0, (17 + 13)),
                                    Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"),
                                    Colour = overlayColourProvider.Background1,
                                },
                            }
                        },
                    }
                }
            });
#pragma warning restore CS8602 // null 가능 참조에 대한 역참조입니다.
        }

        public Video Data = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
        }

        public async Task GetThumbnail(string url, CancellationToken cancellationToken = default)
        {
            Schedule(() => loading.Show());
            Texture north = await textureStore.GetAsync(Data.Snippet.Thumbnails.High.Url, cancellationToken);
            Schedule(() => { thumbnail.Texture = north; });
            Schedule(() => loading.Hide());
        }

        private AXPlayerLoadingLayer loading = null!;

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

        public void UpdateState(bool isCurrentItem)
        {
            if (isCurrentItem)
            {
                Schedule(() => { BorderThickness = 3; });
            } else
            {
                Schedule(() => { BorderThickness = 0; });
            }
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

        public Action<PlaylistItemView>? ClickAction { get; set; }

        protected override bool OnClick(ClickEvent e)
        {
            if (!IsHovered)
                Hover.FadeOutFromOne(1600);

            Hover.FlashColour(FlashColour, 800, Easing.OutQuint);
            trigger();

            return base.OnClick(e);
        }

        public void GetPalette()
        {
            Task.Run(async () =>
            {
                try
                {
                    var cachePath = app.Host.CacheStorage.GetStorageForDirectory("videoThumbnailCache").GetFullPath($"{Data.Id}-PlaylistItemView.png");

                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        var imageBytes = await httpClient.GetByteArrayAsync(Data.Snippet.Thumbnails.High.Url);
                        await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes);
                    }

                    SixLabors.ImageSharp.Image<Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<Rgba32>(cachePath);

                    IBitmapHelper bitmapHelper = new BitmapHelper(bitmap);
                    PaletteBuilder paletteBuilder = new PaletteBuilder();
                    Palette palette = paletteBuilder.Generate(bitmapHelper);
#pragma warning disable CS8602 // null 가능 참조에 대한 역참조입니다.
                    int? rgbColor = palette.MutedSwatch.Rgb;
#pragma warning restore CS8602 // null 가능 참조에 대한 역참조입니다.
                    int? rgbTextColor = palette.MutedSwatch.TitleTextColor;

                    if (rgbColor != null && rgbTextColor != null)
                    {
                        Color4 bgColor = Color.FromArgb((int)rgbColor);
                        Color4 textColor = Color.FromArgb((int)rgbTextColor);

                        bgLayer.Alpha = 1;
                        bgLayer.Colour = ColourInfo.GradientHorizontal(bgColor, bgColor.Darken(1f));
                        videoNameText.Colour = (textColor);
                        channelNameText.Colour = (textColor);
                        viewsText.Colour = (textColor);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            });
        }

        public void UpdateData()
        {
            uiLanguage.UnbindEvents();
            Task.Run(async () =>
            {
                try
                {
                    DateTimeOffset? dateTime = Data.Snippet.PublishedAtDateTimeOffset;
                    DateTime now = DateTime.Now;
                    Channel channelData = api.GetChannel(Data.Snippet.ChannelId);
                    Video videoData = api.GetVideo(Data.Id);

                    Schedule(() =>
                    {
                        GetPalette();

                        Schedule(() =>
                        {
                            channelNameText.Text = api.GetLocalizedChannelTitle(channelData);
                            videoNameText.Text = api.GetLocalizedVideoTitle(videoData);
#pragma warning disable CS8629 // Nullable 값 형식이 null일 수 있습니다.
                            viewsText.Text = AXPlayerStrings.VideoMetadataDescWithoutChannelName(Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), dateTime.Value.DateTime.Humanize(dateToCompareAgainst: now));
#pragma warning restore CS8629 // Nullable 값 형식이 null일 수 있습니다.
                        });

                        uiLanguage.BindValueChanged(locale =>
                        {
                            Schedule(() =>
                            {
                                channelNameText.Text = api.GetLocalizedChannelTitle(channelData);
                                videoNameText.Text = api.GetLocalizedVideoTitle(videoData);
                                viewsText.Text = AXPlayerStrings.VideoMetadataDescWithoutChannelName(Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), dateTime.Value.DateTime.Humanize(dateToCompareAgainst: now));
                            });
                        });
                    });

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

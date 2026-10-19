// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
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
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using PaletteNet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YoutubeExplode.Videos.ClosedCaptions;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class VideoMetadataDisplay : CompositeDrawable
    {
        private ProjectYomiSpriteText videoName;
        private ProjectYomiSpriteText desc;
        public Action<VideoMetadataDisplay> ClickEvent;

        private Box bgLayer, hover;

        [Resolved]
        private YouTubeAPI api { get; set; }

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; }

        [Resolved]
        private AXPlayerConfigManager appConfig { get; set; }

        [Resolved]
        private AXPlayerAppBase app { get; set; }

        private Bindable<Localisation.Language> uiLanguage;
        private Bindable<UsernameDisplayMode> usernameDisplayMode;
        private Bindable<VideoMetadataTranslateSource> translationSource = new Bindable<VideoMetadataTranslateSource>();

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            uiLanguage = app.CurrentLanguage.GetBoundCopy();
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode);
            translationSource = appConfig.GetBindable<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource);
            Masking = false;

            InternalChildren = new Drawable[]
            {
                samples,
                bgLayer = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background4,
                    Alpha = 0,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 4, Vertical = 2 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Child = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    videoName = new ProjectYomiSpriteText
                                    {
                                        Font = AXPlayerApp.DefaultFont.With(size: 20, weight: "Black"),
                                        Text = AXPlayerStrings.VideoNotLoaded,
                                        Colour = overlayColourProvider.Content2,
                                    },
                                    desc = new ProjectYomiSpriteText
                                    {
                                        Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "SemiBold"),
                                        Colour = overlayColourProvider.Foreground2,
                                        Text = AXPlayerStrings.VideoNotLoadedDesc,
                                    },
                                }
                            },
                        },
                    }
                },
                hover = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0,
                },
            };
        }

        private Video videoData;

        protected override bool OnClick(ClickEvent e)
        {
            ClickEvent?.Invoke(this);

            return base.OnClick(e);
        }

        private HoverSounds samples = new HoverClickSounds(HoverSampleSet.Default);

        protected override bool OnHover(HoverEvent e)
        {
            if (ClickEvent != null)
                hover.FadeTo(0.1f, 500, Easing.OutQuint);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            if (ClickEvent != null)
                hover.FadeOut(500, Easing.OutQuint);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            (samples as HoverClickSounds).Enabled.Value = (ClickEvent != null);
        }

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; }

        public void GetPalette()
        {
            /*
            Task.Run(async () =>
            {
                var cachePath = app.Host.CacheStorage.GetStorageForDirectory("videoThumbnailCache").GetFullPath($"{videoData.Id}.png");

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(videoData.Snippet.Thumbnails.High.Url);
                    await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes);
                }

                using Image<Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<Rgba32>(app.Host.CacheStorage.GetStorageForDirectory("videoThumbnailCache").GetFullPath($"{videoData.Id}.png"));

                IBitmapHelper bitmapHelper = new BitmapHelper(bitmap);
                PaletteBuilder paletteBuilder = new PaletteBuilder();
                Palette palette = paletteBuilder.Generate(bitmapHelper);
                int? rgbColor = palette.MutedSwatch.Rgb;
                int? rgbTextColor = palette.MutedSwatch.TitleTextColor;

                if (rgbColor != null && rgbTextColor != null)
                {
                    Color4 bgColor = System.Drawing.Color.FromArgb((int)rgbColor);
                    Color4 textColor = System.Drawing.Color.FromArgb((int)rgbTextColor);
                    Schedule(() =>
                    {
                        //bgLayer.Alpha = 1;
                        //bgLayer.Colour = bgColor;
                        videoName.Colour = (bgColor);
                        desc.Colour = (bgColor);
                    });
                }
                else
                {
                    //bgLayer.Colour = overlayColourProvider.Background4;
                    videoName.Colour = overlayColourProvider.Content2;
                    desc.Colour = overlayColourProvider.Foreground2;
                }
            });
            */
        }

        private void updateDescText()
        {
            Schedule(() =>
            {
                DateTimeOffset? dateTime = videoData.Snippet.PublishedAtDateTimeOffset;
                DateTimeOffset now = DateTime.Now;
                Channel channelData = api.GetChannel(videoData.Snippet.ChannelId);
                desc.Text = AXPlayerStrings.VideoMetadataDescWithLikeCount(api.GetLocalizedChannelTitle(channelData), Convert.ToInt32(videoData.Statistics.LikeCount).ToStandardFormattedString(0), Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), dateTime.Value.Humanize(dateToCompareAgainst: now));
            });
        }

        public void UpdateVideo(string videoId)
        {
            uiLanguage.UnbindEvents();
            Task.Run(async () =>
            {
                videoData = api.GetVideo(videoId);
                //UpdateChannelSubscribeState(videoData.Snippet.ChannelId);
                DateTimeOffset? dateTime = videoData.Snippet.PublishedAtDateTimeOffset;
                DateTimeOffset now = DateTime.Now;
                Channel channelData = api.GetChannel(videoData.Snippet.ChannelId);
                Schedule(() =>
                {
                    videoName.Text = api.GetLocalizedVideoTitle(videoData);
                    updateDescText();
                });

                GetPalette();

                uiLanguage.BindValueChanged(locale =>
                {
                    Task.Run(async () =>
                    {
                        Schedule(() =>
                        {
                            videoName.Text = api.GetLocalizedVideoTitle(videoData);
                            updateDescText();
                        });
                    });
                });

                usernameDisplayMode.BindValueChanged(locale =>
                {
                    Task.Run(async () =>
                    {
                        Schedule(() =>
                        {
                            updateDescText();
                        });
                    });
                }, true);

                translationSource.BindValueChanged(locale =>
                {
                    Task.Run(async () =>
                    {
                        Schedule(() =>
                        {
                            videoName.Text = api.GetLocalizedVideoTitle(videoData);
                            updateDescText();
                        });
                    });
                }, true);
            });
        }
    }
}

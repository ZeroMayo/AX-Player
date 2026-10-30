// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Drawing;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Utils;
using Google.Apis.YouTube.v3.Data;
using Humanizer;
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
using SixLabors.ImageSharp.PixelFormats;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class VideoMetadataDisplayWithoutProfile : CircularContainer
    {
        private ProjectYomiTextFlowContainer videoName;
        private ProjectYomiTextFlowContainer desc;
        public Action<VideoMetadataDisplayWithoutProfile> ClickEvent;

        private Box bgLayer, hover, bgLayer2;

        [Resolved]
        private YouTubeAPI api { get; set; }

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; }

        [Resolved]
        private AXPlayerConfigManager appConfig { get; set; }

        private Container mainContent;

        private Bindable<Localisation.Language> uiLanguage;
        private Bindable<UsernameDisplayMode> usernameDisplayMode;
        private Bindable<VideoMetadataTranslateSource> translationSource = new Bindable<VideoMetadataTranslateSource>();

        public class VideoMetadataDisplayWithoutProfileColours
        {
            public ColourInfo BGColor;
            public ColourInfo FGColor;
            public ColourInfo FGColor2;
        }

        public void ApplyColours(VideoMetadataDisplayWithoutProfileColours colours)
        {
            bgLayer.FadeColour(colours.BGColor, 500, Easing.OutQuint);
            videoName.FadeColour(colours.FGColor, 500, Easing.OutQuint);
            desc.FadeColour(colours.FGColor2, 500, Easing.OutQuint);

            bgLayer2.FadeColour(ColourInfo.GradientHorizontal(colours.FGColor, colours.BGColor), 500, Easing.OutQuint);
            bgLayer2.FadeTo(0.2f, 500, Easing.OutQuint);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            mainContent.ScaleTo(0.9f, 2000, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            mainContent.ScaleTo(1, 1000, Easing.OutElastic);
            base.OnMouseUp(e);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            uiLanguage = app.CurrentLanguage.GetBoundCopy();
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode);
            translationSource = appConfig.GetBindable<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource);

            Masking = true;

            InternalChildren = new Drawable[]
            {
                samples,
                mainContent = new Container
                {
                    Origin = Anchor.Centre,
                    Anchor = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    /*
                    EdgeEffect = new osu.Framework.Graphics.Effects.EdgeEffectParameters
                    {
                        Type = osu.Framework.Graphics.Effects.EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(0.25f),
                        Offset = new Vector2(0, 2),
                        Radius = 16,
                    },
                    */
                    Children = new Drawable[]
                    {
                        bgLayer = new Box
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Colour = overlayColourProvider.Background5,
                            Alpha = 1f,
                        },
                        bgLayer2 = new Box
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.Both,
                            Size = new Vector2(0.5f, 1),
                            Colour = overlayColourProvider.Background5,
                            Alpha = 0f,
                        },
                        new Container {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding
                                    {
                                        Vertical = 7,
                                        Horizontal = 14,
                                    },
                                    Children = new Drawable[]
                                    {
                                        videoName = new ProjectYomiTextFlowContainer(f =>
                                        {
                                            f.Font = AXPlayerApp.DefaultFont.With(size: 20, weight: "Black");
                                        })
                                        {
                                            Colour = overlayColourProvider.Content2,
                                            AutoSizeAxes = Axes.Both,
                                            Text = AXPlayerStrings.VideoNotLoaded,
                                        },
                                        desc = new ProjectYomiTextFlowContainer(f =>
                                        {
                                            f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "SemiBold");
                                        })
                                        {
                                            Colour = overlayColourProvider.Content2,
                                            AutoSizeAxes = Axes.Both,
                                            Text = AXPlayerStrings.VideoNotLoadedDesc,
                                        }
                                    }
                                },
                                hover = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.White,
                                    Blending = BlendingParameters.Additive,
                                    Alpha = 0,
                                },
                            }
                        },
                    }
                },
            };
        }

        public string TruncateWithEllipsis(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // If the string is already short enough, return it as-is
            if (value.Length <= maxLength) return value;

            // Ensure we don't get a negative length if maxLength is smaller than the ellipsis
            int truncateLength = Math.Max(0, maxLength - 3);

            return value.Substring(0, truncateLength) + "...";
        }

        private Video videoData;

        protected override bool OnClick(ClickEvent e)
        {
            ClickEvent?.Invoke(this);

            return base.OnClick(e);
        }

        public void SetVideoMetadataDisplayAlignment(VideoMetadataDisplayAlignment alignment)
        {
            switch (alignment)
            {
                case VideoMetadataDisplayAlignment.Left:
                {
                    videoName.Anchor = Anchor.TopLeft;
                    videoName.Origin = Anchor.TopLeft;
                    videoName.TextAnchor = Anchor.TopLeft;

                    desc.Anchor = Anchor.TopLeft;
                    desc.Origin = Anchor.TopLeft;
                    desc.TextAnchor = Anchor.TopLeft;
                    break;
                }
                case VideoMetadataDisplayAlignment.Center:
                {
                    videoName.Anchor = Anchor.TopCentre;
                    videoName.Origin = Anchor.TopCentre;
                    videoName.TextAnchor = Anchor.TopCentre;

                    desc.Anchor = Anchor.TopCentre;
                    desc.Origin = Anchor.TopCentre;
                    desc.TextAnchor = Anchor.TopCentre;
                    break;
                }
                case VideoMetadataDisplayAlignment.Right:
                {
                    videoName.Anchor = Anchor.TopRight;
                    videoName.Origin = Anchor.TopRight;
                    videoName.TextAnchor = Anchor.TopRight;

                    desc.Anchor = Anchor.TopRight;
                    desc.Origin = Anchor.TopRight;
                    desc.TextAnchor = Anchor.TopRight;
                    break;
                }
            }
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
                hover.FadeTo(0f, 500, Easing.OutQuint);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            (samples as HoverClickSounds).Enabled.Value = (ClickEvent != null);
        }

        [Resolved]
        private AXPlayerAppBase app { get; set; }

        public void GetPalette()
        {
            Task.Run(async () =>
            {
                var cachePath = app.Host.CacheStorage.GetStorageForDirectory("videoThumbnailCache").GetFullPath($"{videoData.Id}-2.png");

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(videoData.Snippet.Thumbnails.High.Url);
                    await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes);
                }

                SixLabors.ImageSharp.Image<Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<Rgba32>(app.Host.CacheStorage.GetStorageForDirectory("videoThumbnailCache").GetFullPath($"{videoData.Id}-2.png"));

                IBitmapHelper bitmapHelper = new BitmapHelper(bitmap);
                PaletteBuilder paletteBuilder = new PaletteBuilder();
                Palette palette = paletteBuilder.Generate(bitmapHelper);
                int? rgbColor = palette.VibrantSwatch.Rgb;
                int? rgbTextColor = palette.VibrantSwatch.TitleTextColor;

                if (rgbColor != null)
                {
                    Color4 bgColor = Color.FromArgb((int)rgbColor);
                    Schedule(() =>
                    {
                        bgLayer.Colour = ColourInfo.GradientHorizontal(bgColor.Opacity(0.75f), bgColor.Opacity(0f));
                    });
                }

                if (rgbTextColor != null)
                {
                    Color4 textColor = Color.FromArgb((int)rgbTextColor);
                    Schedule(() =>
                    {
                        videoName.Colour = (textColor);
                        desc.Colour = (textColor);
                    });
                }
            });
        }

        private void updateDescText()
        {
            Schedule(() =>
            {
                DateTimeOffset? dateTime = videoData.Snippet.PublishedAtDateTimeOffset;
                DateTimeOffset now = DateTime.Now;
                Channel channelData = api.GetChannel(videoData.Snippet.ChannelId);
                desc.Text = AXPlayerStrings.VideoMetadataDescWithLikeCount(TruncateWithEllipsis(api.GetLocalizedChannelTitle(channelData), 50), videoData.Statistics.LikeCount != null ? Convert.ToDouble(videoData.Statistics.LikeCount).ToStandardFormattedString(0) : Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoData.Id).RawLikes).ToStandardFormattedString(0), Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), dateTime.Value.Humanize(dateToCompareAgainst: now));
            });
        }

        public void UpdateVideo(string videoId)
        {
            uiLanguage.UnbindEvents();
            Task.Run(async () =>
            {
                videoData = api.GetVideo(videoId);
                DateTimeOffset? dateTime = videoData.Snippet.PublishedAtDateTimeOffset;
                DateTimeOffset now = DateTimeOffset.Now;
                Channel channelData = api.GetChannel(videoData.Snippet.ChannelId);
                Schedule(() =>
                {
                    videoName.Text = TruncateWithEllipsis(api.GetLocalizedVideoTitle(videoData), 50);
                    updateDescText();
                });

                //GetPalette();

                uiLanguage.BindValueChanged(locale =>
                {
                    Task.Run(async () =>
                    {
                        Schedule(() =>
                        {
                            videoName.Text = TruncateWithEllipsis(api.GetLocalizedVideoTitle(videoData), 50);
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
                            videoName.Text = TruncateWithEllipsis(api.GetLocalizedVideoTitle(videoData), 50);
                            updateDescText();
                        });
                    });
                }, true);
            });
        }
    }
}

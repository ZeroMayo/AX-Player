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
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
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

        private VideoMetadataDisplayWithoutProfileColours videoMetadataDisplayWithoutProfileColours;

        public void ApplyColours(VideoMetadataDisplayWithoutProfileColours colours)
        {
            videoMetadataDisplayWithoutProfileColours = colours;
            bgLayer.FadeColour(colours.BGColor, 500, Easing.OutQuint);
            videoName.FadeColour(colours.FGColor, 500, Easing.OutQuint);
            desc.FadeColour(colours.FGColor2, 500, Easing.OutQuint);

            switch (videoMetadataDisplayAlignment)
            {
                case VideoMetadataDisplayAlignment.Left:
                    bgLayer2.FadeColour(ColourInfo.GradientHorizontal(colours.FGColor, colours.BGColor), 500, Easing.OutQuint);
                    break;

                case VideoMetadataDisplayAlignment.Center:
                    bgLayer2.FadeColour(ColourInfo.GradientVertical(colours.FGColor, colours.BGColor), 500, Easing.OutQuint);
                    break;

                case VideoMetadataDisplayAlignment.Right:
                    bgLayer2.FadeColour(ColourInfo.GradientHorizontal(colours.BGColor, colours.FGColor), 500, Easing.OutQuint);
                    break;
            }
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
                            Colour = ColourInfo.GradientHorizontal(overlayColourProvider.Content2, overlayColourProvider.Background5),
                            Alpha = 0.2f,
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

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; }

        private VideoMetadataDisplayAlignment videoMetadataDisplayAlignment;

        public void SetVideoMetadataDisplayAlignment(VideoMetadataDisplayAlignment alignment)
        {
            videoMetadataDisplayAlignment = alignment;

            if (videoMetadataDisplayWithoutProfileColours != null)
                ApplyColours(videoMetadataDisplayWithoutProfileColours);

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

                    bgLayer2.FadeColour(ColourInfo.GradientHorizontal(overlayColourProvider.Content2, overlayColourProvider.Background5), 500, Easing.OutQuint);
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

                    bgLayer2.FadeColour(ColourInfo.GradientVertical(overlayColourProvider.Content2, overlayColourProvider.Background5), 500, Easing.OutQuint);
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
                    bgLayer2.FadeColour(ColourInfo.GradientHorizontal(overlayColourProvider.Background5, overlayColourProvider.Content2), 500, Easing.OutQuint);
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
                try
                {
                    const string key_suffix = "-2";

                    string cachePath = app.Host.CacheStorage.GetStorageForDirectory(MediaCache.VIDEO_THUMBNAIL_DIRECTORY).GetFullPath($"{videoData.Id}{key_suffix}.png");

                    byte[] imageBytes = await MediaThumbnailCache.GetThumbnailAsync(MediaCacheKind.VideoThumbnail, $"{videoData.Id}{key_suffix}", cachePath, videoData.Snippet.Thumbnails.High.Url);

                    using SixLabors.ImageSharp.Image<Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<Rgba32>(imageBytes);

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
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            });
        }

        private void updateDescText(Channel channelData)
        {
            Schedule(() =>
            {
                DateTimeOffset? dateTime = videoData.Snippet.PublishedAtDateTimeOffset;
                DateTimeOffset now = DateTime.Now;
                desc.Text = AXPlayerStrings.VideoMetadataDescWithLikeCount(TruncateWithEllipsis(api.GetLocalizedChannelTitle(channelData), 50), videoData.Statistics.LikeCount != null ? Convert.ToDouble(videoData.Statistics.LikeCount).ToStandardFormattedString(0) : Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoData.Id).RawLikes).ToStandardFormattedString(0), videoData.Statistics.ViewCount.toFormattedCount(), dateTime.toRelativeDate(now.DateTime));
            });
        }

        public void UpdateVideo(Video video)
        {
            if (video == null)
                return;

            uiLanguage.UnbindEvents();
            Task.Run(async () =>
            {
                videoData = video;
                DateTimeOffset? dateTime = videoData.Snippet.PublishedAtDateTimeOffset;
                DateTimeOffset now = DateTimeOffset.Now;
                Channel channelData = api.GetChannel(videoData.Snippet.ChannelId);
                Schedule(() =>
                {
                    videoName.Text = TruncateWithEllipsis(api.GetLocalizedVideoTitle(videoData), 50);
                    updateDescText(channelData);
                });

                //GetPalette();

                uiLanguage.BindValueChanged(locale =>
                {
                    Task.Run(async () =>
                    {
                        Schedule(() =>
                        {
                            videoName.Text = TruncateWithEllipsis(api.GetLocalizedVideoTitle(videoData), 50);
                            updateDescText(channelData);
                        });
                    });
                });

                usernameDisplayMode.BindValueChanged(locale =>
                {
                    Task.Run(async () =>
                    {
                        Schedule(() =>
                        {
                            updateDescText(channelData);
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
                            updateDescText(channelData);
                        });
                    });
                }, true);
            });
        }
    }
}

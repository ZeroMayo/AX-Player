// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using Humanizer;
using NekoPlayer.App.Config;
using NekoPlayer.App.Graphics.Sprites;
using NekoPlayer.App.Localisation;
using NekoPlayer.App.Online;
using NekoPlayer.App.Utils;
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

namespace NekoPlayer.App.Graphics.UserInterface
{
    public partial class YouTubeChannelMetadataDisplayWithShadow : CompositeDrawable
    {
        private ProfileImage profileImage;
        private ProjectYomiTextFlowContainer videoName;
        private TruncatingSpriteText desc;
        public Action<YouTubeChannelMetadataDisplayWithShadow> ClickEvent;

        private Box bgLayer, hover;

        [Resolved]
        private YouTubeAPI api { get; set; }

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; }

        [Resolved]
        private NekoPlayerConfigManager appConfig { get; set; }

        [Resolved]
        private NekoPlayerAppBase app { get; set; }

        private Bindable<Localisation.Language> uiLanguage;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            uiLanguage = app.CurrentLanguage.GetBoundCopy();

            CornerRadius = NekoPlayerApp.UI_CORNER_RADIUS;
            Masking = true;

            EdgeEffect = new osu.Framework.Graphics.Effects.EdgeEffectParameters
            {
                Type = osu.Framework.Graphics.Effects.EdgeEffectType.Shadow,
                Colour = Color4.Black.Opacity(0.25f),
                Offset = new Vector2(0, 2),
                Radius = 16,
            };

            InternalChildren = new Drawable[]
            {
                samples,
                bgLayer = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background4,
                    Alpha = 1f,
                },
                new Container {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(7),
                    Children = new Drawable[]
                    {
                        profileImage = new ProfileImage(45),
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Vertical = 5,
                                Left = 50,
                                Right = 5,
                            },
                            Children = new Drawable[]
                            {
                                videoName = new ProjectYomiTextFlowContainer(f => f.Font = NekoPlayerApp.DefaultFont.With(size: 20, weight: "ExtraBold"))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = "",
                                    Colour = overlayColourProvider.Content2,
                                },
                                desc = new TruncatingSpriteText
                                {
                                    Font = NekoPlayerApp.DefaultFont.With(size: 13, weight: "Regular"),
                                    RelativeSizeAxes = Axes.X,
                                    Colour = overlayColourProvider.Foreground2,
                                    Text = "",
                                    Position = new osuTK.Vector2(0, 20),
                                }
                            }
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
            };
        }

        private Channel channelData;

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

        public string TruncateWithEllipsis(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // If the string is already short enough, return it as-is
            if (value.Length <= maxLength) return value;

            // Ensure we don't get a negative length if maxLength is smaller than the ellipsis
            int truncateLength = Math.Max(0, maxLength - 3);

            return value.Substring(0, truncateLength) + "...";
        }

        public void GetPalette()
        {
            Task.Run(async () =>
            {
                var cachePath = app.Host.CacheStorage.GetStorageForDirectory("profile_cache").GetFullPath($"{channelData.Id}.png");

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(channelData.Snippet.Thumbnails.High.Url);
                    await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes);
                }

                using Image<Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<Rgba32>(app.Host.CacheStorage.GetStorageForDirectory("profile_cache").GetFullPath($"{channelData.Id}.png"));

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
                        bgLayer.Colour = ColourInfo.GradientHorizontal(bgColor, overlayColourProvider.Background4);
                        videoName.Colour = (textColor);
                        desc.Colour = (textColor);
                    });
                }
            });
        }

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; }

        public void UpdateUser(Channel channel)
        {
            uiLanguage.UnbindEvents();
            channelData = channel;

            GetPalette();

            Task.Run(async () =>
            {
                Schedule(() =>
                {
                    videoName.Text = TruncateWithEllipsis(api.GetLocalizedChannelTitle(channel, forceUsernameDisplay: true), 20);
                    desc.Text = NekoPlayerStrings.ProfileImageTooltip(channel.Snippet.CustomUrl, Convert.ToInt32(channel.Statistics.SubscriberCount).ToMetric(decimals: 2));
                    profileImage.UpdateProfileImage(channel.Id);
                });
            });

            uiLanguage.BindValueChanged(locale =>
            {
                Task.Run(async () =>
                {
                    Schedule(() =>
                    {
                        videoName.Text = TruncateWithEllipsis(api.GetLocalizedChannelTitle(channel, forceUsernameDisplay: true), 20);
                    });
                });
            });
        }
    }
}

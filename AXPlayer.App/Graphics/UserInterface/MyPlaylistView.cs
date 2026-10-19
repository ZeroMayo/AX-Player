// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using AXPlayer.App.Config;
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
    public partial class MyPlaylistView : ProjectYomiClickableContainer
    {
        private Sprite thumbnail = null!;
        private TruncatingSpriteText playlistNameText = null!;
        private TruncatingSpriteText channelNameText = null!;
        public Action<MyPlaylistView> ClickEvent = null!;
        public Action<Playlist> OptionsClickEvent = null!;
        private ProjectYomiSpriteText privacyStatusText = null!;

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

        public MyPlaylistView()
            : base(HoverSampleSet.Default)
        {
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
                                playlistNameText = new TruncatingSpriteText
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
                                privacyStatusText = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Position = new Vector2(0, (17 + 13)),
                                    Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"),
                                    Colour = overlayColourProvider.Background1,
                                },
                            }
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Top = 5,
                                Bottom = 5,
                                Left = 200,
                                Right = 5,
                            },
                            Children = new Drawable[]
                            {
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(25, 25),
                                    Icon = FontAwesome.Regular.Edit,
                                    IconScale = new Vector2(0.7f),
                                    TooltipText = AXPlayerStrings.EditPlaylist,
                                    BackgroundColour = overlayColourProvider.Background5,
                                    Action = () =>
                                    {
                                        OptionsClickEvent.Invoke(playlistData);
                                    }
                                },
                            }
                        },
                    }
                }
            });
#pragma warning restore CS8602 // null 가능 참조에 대한 역참조입니다.
        }

        public Playlist Data = null!;

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

        public Action<MyPlaylistView>? ClickAction { get; set; }

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
                    var cachePath = app.Host.CacheStorage.GetStorageForDirectory("playlistThumbnailCache").GetFullPath($"{Data.Id}-MyPlaylistView.png");

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
                        playlistNameText.Colour = (textColor);
                        channelNameText.Colour = (textColor);
                        privacyStatusText.Colour = (textColor);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            });
        }

        private Playlist playlistData;

        public void UpdateData()
        {
            uiLanguage.UnbindEvents();
            Task.Run(async () =>
            {
                try
                {
                    Channel channelData = api.GetChannel(Data.Snippet.ChannelId);
                    playlistData = api.GetPlaylistInfo(Data.Id);

                    Schedule(() =>
                    {
                        GetPalette();

                        channelNameText.Text = api.GetLocalizedChannelTitle(channelData);
                        playlistNameText.Text = playlistData.Snippet.Title;
#pragma warning disable CS8629 // Nullable 값 형식이 null일 수 있습니다.
                        try
                        {
                            switch (playlistData.Status.PrivacyStatus)
                            {
                                case "public":
                                    privacyStatusText.Text = AXPlayerStrings.Public;
                                    break;
                                case "unlisted":
                                    privacyStatusText.Text = AXPlayerStrings.Unlisted;
                                    break;
                                case "private":
                                    privacyStatusText.Text = AXPlayerStrings.Private;
                                    break;
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, e.GetDescription());
                        }
#pragma warning restore CS8629 // Nullable 값 형식이 null일 수 있습니다.

                        uiLanguage.BindValueChanged(locale =>
                        {
                            Schedule(() =>
                            {
                                channelNameText.Text = api.GetLocalizedChannelTitle(channelData);
                                playlistNameText.Text = playlistData.Snippet.Title;
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

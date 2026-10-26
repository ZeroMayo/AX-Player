// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class UrlRedirectDisplay : ProjectYomiClickableContainer, IHasLineBaseHeight
    {
        private string url;

        private ProjectYomiTextFlowContainer textFlow;

        private string displayName;
        private IconUsage icon;

        public UrlRedirectDisplay(string url)
            : base(HoverSampleSet.Button)
        {
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            this.url = url;
            displayName = url;
            icon = FontAwesome.Solid.Globe;
            Enabled.Value = true;
            Masking = true;
            TooltipText = url;
        }

        private Bindable<Localisation.Language> uiLanguage = null!;
        private Bindable<UsernameDisplayMode> usernameDisplayMode = null!;

        [Resolved]
        private AXPlayerConfigManager appConfig { get; set; } = null!;
        protected Box Hover;

        [BackgroundDependencyLoader]
        private async Task load(OverlayColourProvider overlayColourProvider)
        {
            uiLanguage = app.CurrentLanguage.GetBoundCopy();
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode);
            AutoSizeAxes = Axes.Both;

            AddRangeInternal(new Drawable[]
            {
                new CircularContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = overlayColourProvider.Background2,
                        },
                        textFlow = new ProjectYomiTextFlowContainer(f =>
                        {
                            f.Font = AXPlayerApp.DefaultFont.With(size: 13.5f, weight: "Bold");
                        })
                        {
                            AutoSizeAxes = Axes.Both,
                            Margin = new MarginPadding(4),
                        },
                        Hover = new Box
                        {
                            Alpha = 0,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                            Blending = BlendingParameters.Additive,
                            Depth = float.MinValue
                        },
                    }
                }
            });

#pragma warning disable CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.
            Task.Run(async () =>
            {
                if (AXPlayerDescriptionParser.IsYouTubeVideo(url))
                {
                    icon = FontAwesome.Brands.Youtube;

                    string videoId = VideoId.Parse(url);
                    Google.Apis.YouTube.v3.Data.Video video = api.GetVideo(videoId);

                    displayName = api.GetLocalizedVideoTitle(video);
                    Schedule(() => RefreshTextFlow());

                    uiLanguage.BindValueChanged(locale =>
                    {
                        Schedule(() =>
                        {
                            displayName = api.GetLocalizedVideoTitle(video);
                            RefreshTextFlow();
                        });
                    });
                }
                else if (AXPlayerDescriptionParser.IsYouTubePlaylist(url))
                {
                    icon = FontAwesome.Brands.Youtube;

                    string playlistId = PlaylistId.Parse(url);
                    Google.Apis.YouTube.v3.Data.Playlist video = api.GetPlaylistInfo(playlistId);

                    displayName = video.Snippet.Title;
                    Schedule(() => RefreshTextFlow());
                }
                else if (AXPlayerDescriptionParser.IsYouTubeChannel(url))
                {
                    icon = FontAwesome.Brands.Youtube;

                    string channelId = url.Replace("https://www.youtube.com/channel/", string.Empty);
                    Google.Apis.YouTube.v3.Data.Channel channel = api.GetChannel(channelId);

                    displayName = api.GetLocalizedChannelTitle(channel);
                    Schedule(() => RefreshTextFlow());

                    uiLanguage.BindValueChanged(locale =>
                    {
                        Schedule(() =>
                        {
                            displayName = api.GetLocalizedChannelTitle(channel);
                            RefreshTextFlow();
                        });
                    });

                    usernameDisplayMode.BindValueChanged(locale =>
                    {
                        Schedule(() =>
                        {
                            displayName = api.GetLocalizedChannelTitle(channel);
                            RefreshTextFlow();
                        });
                    });
                }
                else if (AXPlayerDescriptionParser.IsDiscord(url))
                {
                    icon = FontAwesome.Brands.Discord;
                    Schedule(() => RefreshTextFlow());
                }
                else if (AXPlayerDescriptionParser.IsTwitch(url))
                {
                    icon = FontAwesome.Brands.Twitch;
                    Schedule(() => RefreshTextFlow());
                }
                else if (AXPlayerDescriptionParser.IsTwitter(url))
                {
                    icon = FontAwesome.Brands.Twitter;
                    Schedule(() => RefreshTextFlow());
                }
                else
                {
                    icon = FontAwesome.Solid.Globe;
                    Schedule(() => RefreshTextFlow());
                }
            });
#pragma warning restore CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.
        }

        [Resolved]
        private GameHost host { get; set; }

        [Resolved]
        private YouTubeAPI api { get; set; }

        [Resolved]
        private AXPlayerAppBase app { get; set; }

        protected virtual float HoverLayerFinalAlpha => 0.1f;

        protected override bool OnHover(HoverEvent e)
        {
            if (Enabled.Value)
            {
                Hover.FadeTo(0.2f, 40, Easing.OutQuint)
                     .Then()
                     .FadeTo(HoverLayerFinalAlpha, 800, Easing.OutQuint);
            }

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            Hover.FadeOut(800, Easing.OutQuint);
        }

        private void RefreshTextFlow()
        {
            base.LoadComplete();
            textFlow.AddIcon(icon, o => o.Margin = new MarginPadding() { Right = 4 });
            textFlow.AddText("/  " + displayName);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (AXPlayerDescriptionParser.IsYouTubeVideo(url))
                app.AppMessageHandler.SelectVideo(url);
            else if (AXPlayerDescriptionParser.IsYouTubePlaylist(url))
                app.AppMessageHandler.SelectPlaylist(url);
            else
                host.OpenUrlExternally(url);

            return base.OnClick(e);
        }

        public float LineBaseHeight => 15.5f;
    }
}

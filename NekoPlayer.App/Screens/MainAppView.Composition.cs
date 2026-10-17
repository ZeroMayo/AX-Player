// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using DiscordRPC;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Humanizer;
using NekoPlayer.App.Audio;
using NekoPlayer.App.Config;
using NekoPlayer.App.Extensions;
using NekoPlayer.App.Graphics;
using NekoPlayer.App.Graphics.Containers;
using NekoPlayer.App.Graphics.Shaders;
using NekoPlayer.App.Graphics.Sprites;
using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Graphics.UserInterfaceV2;
using NekoPlayer.App.Graphics.UserInterfaceV3;
using NekoPlayer.App.Graphics.Videos;
using NekoPlayer.App.Input;
using NekoPlayer.App.Input.Binding;
using NekoPlayer.App.Localisation;
using NekoPlayer.App.Online;
using NekoPlayer.App.Overlays;
using NekoPlayer.App.Overlays.Containers;
using NekoPlayer.App.Overlays.OSD;
using NekoPlayer.App.Overlays.Volume;
using NekoPlayer.App.Updater;
using NekoPlayer.App.Utils;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Graphics.Video;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using PaletteNet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.ClosedCaptions;
using YoutubeExplode.Videos.Streams;
using static Google.Apis.YouTube.v3.CommentThreadsResource.ListRequest;
using Container = osu.Framework.Graphics.Containers.Container;
using Language = NekoPlayer.App.Localisation.Language;
using OverlayContainer = NekoPlayer.App.Graphics.Containers.OverlayContainer;

namespace NekoPlayer.App.Screens
{
    public partial class MainAppView
    {
        [BackgroundDependencyLoader]
        private void load(ISampleStore sampleStore, FrameworkConfigManager config, NekoPlayerConfigManager appConfig, GameHost host, Storage storage, OverlayColourProvider overlayColourProvider, TextureStore textures, FrameworkDebugConfigManager debugConfig)
        {
            speedTextRolling = new Bindable<double>(1);
            volumeTextRolling = new Bindable<double>(1);
            appliedEffects.Value = new List<InternalShader>();
            window = host.Window;

            app.RegisterMessage(this);

            videoVolume = config.GetBindable<double>(FrameworkSetting.VolumeMusic);

            showVideoMetadataOnWindowTitle = appConfig.GetBindable<bool>(NekoPlayerSetting.ShowVideoMetadataOnWindowTitle);

            uiVisible = screenshotManager.CursorVisibility.GetBoundCopy();
            signedIn = googleOAuth2.SignedIn.GetBoundCopy();

            isAnyOverlayOpen = sessionStatics.GetBindable<bool>(Static.IsAnyOverlayOpen);
            videoPlaying = sessionStatics.GetBindable<bool>(Static.IsVideoPlaying);
            trayIconVisible = sessionStatics.GetBindable<bool>(Static.WindowIsTray);
            ReleaseStream = appConfig.GetBindable<ReleaseStream>(NekoPlayerSetting.ReleaseStream);

            playOverlaySFX = appConfig.GetBindable<bool>(NekoPlayerSetting.PlayOverlaySFX);
            overlaySFXType = appConfig.GetBindable<SFXType>(NekoPlayerSetting.OverlaySFXType);

            captionBGOpacity = appConfig.GetBindable<float>(NekoPlayerSetting.CaptionBGOpacity);

            uiLanguage = app.CurrentLanguage.GetBoundCopy();
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(NekoPlayerSetting.UsernameDisplayMode);
            commentsSort = appConfig.GetBindable<CommentsSortCriteria>(NekoPlayerSetting.CommentsSortCriteria);
            searchSort = appConfig.GetBindable<SearchSortCriteria>(NekoPlayerSetting.SearchSortCriteria);
            resetPlaybackSpeedWhenLoadingAVideo = appConfig.GetBindable<bool>(NekoPlayerSetting.ResetPlaybackSpeedWhenLoadingAVideo);

            reverbEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled);
            rotateEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.RotateEnabled);
            echoEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EchoEnabled);
            distortionEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.DistortionEnabled);
            karaokeEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.KaraokeEnabled);
            chorusEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ChorusEnabled);
            eightBitEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled);

            DistortionVolume = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.DistortionVolume);

            videoMetadataDisplayAlignment = appConfig.GetBindable<VideoMetadataDisplayAlignment>(NekoPlayerSetting.VideoMetadataDisplayAlignment);

            scalingMode = appConfig.GetBindable<ScalingMode>(NekoPlayerSetting.Scaling);
            scalingSizeX = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingSizeX);
            scalingSizeY = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingSizeY);
            scalingPositionX = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingPositionX);
            scalingPositionY = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingPositionY);
            scalingBackgroundDim = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingBackgroundDim);
            alwaysUseOriginalAudio = appConfig.GetBindable<bool>(NekoPlayerSetting.AlwaysUseOriginalAudio);
            discordRichPresence = appConfig.GetBindable<DiscordRichPresenceMode>(NekoPlayerSetting.DiscordRichPresence);
            closeButtonAction = appConfig.GetBindable<CloseButtonAction>(NekoPlayerSetting.CloseButtonAction);
            colourSchemeBindable = appConfig.GetBindable<OverlayColourScheme>(NekoPlayerSetting.ColourScheme);
            profileImageShape = appConfig.GetBindable<ProfileImageShape>(NekoPlayerSetting.ProfileImageShape);

            captionEnabled = appConfig.GetBindable<bool>(NekoPlayerSetting.CaptionEnabled);

            localeBindable = config.GetBindable<string>(FrameworkSetting.Locale);
            fpsDisplay = appConfig.GetBindable<bool>(NekoPlayerSetting.ShowFpsDisplay);
            use_sdl3 = config.GetBindable<bool>(FrameworkSetting.UseExperimentalSDL3);
            adjustPitch = appConfig.GetBindable<bool>(NekoPlayerSetting.AdjustPitchOnSpeedChange);
            audioQuality = appConfig.GetBindable<Config.AudioQuality>(NekoPlayerSetting.AudioQuality);
            videoQuality = appConfig.GetBindable<Config.VideoQuality>(NekoPlayerSetting.VideoQuality);
            audioLanguage = appConfig.GetBindable<Localisation.Language>(NekoPlayerSetting.AudioLanguage);
            hardwareVideoDecoder = config.GetBindable<HardwareVideoDecoder>(FrameworkSetting.HardwareVideoDecoder);
            cursorInWindow = host.Window?.CursorInWindow.GetBoundCopy();
            windowMode = config.GetBindable<WindowMode>(FrameworkSetting.WindowMode);
            captionLanguage = appConfig.GetBindable<ClosedCaptionLanguage>(NekoPlayerSetting.ClosedCaptionLanguage);
            windowedPositionX = config.GetBindable<double>(FrameworkSetting.WindowedPositionX);
            windowedPositionY = config.GetBindable<double>(FrameworkSetting.WindowedPositionY);
            updateInfomationText = game.UpdateManagerVersionText.GetBoundCopy();
            updateButtonEnabled = game.UpdateButtonEnabled.GetBoundCopy();

            ui_font = appConfig.GetBindable<UIFont>(NekoPlayerSetting.UIFont);
            caption_font = appConfig.GetBindable<CaptionFonts>(NekoPlayerSetting.CaptionFont);

            aspectRatioMethod = appConfig.GetBindable<AspectRatioMethod>(NekoPlayerSetting.AspectRatioMethod);

            advancedCaptions = appConfig.GetBindable<bool>(NekoPlayerSetting.UseNewSubtitlesFeature);

            accentColor = overlayColourProvider1.Content2;
            bgColor = overlayColourProvider1.Background3;

            overlaySFXType.BindValueChanged(sfx =>
            {
                refreshSFX();
            }, true);

            use_sdl3.BindValueChanged(_ =>
            {
                if (game?.RestartAppWhenExited() == true)
                {
                    game.AttemptExit();
                }
            });

            colourSchemeBindable.BindValueChanged(_ =>
            {
                if (game?.RestartAppWhenExited() == true)
                {
                    game.AttemptExit();
                }
            });

            ui_font.BindValueChanged(_ =>
            {
                if (game?.RestartAppWhenExited() == true)
                {
                    game.AttemptExit();
                }
            });

            #region The UI Components
            InternalChildren = new Drawable[]
            {
                idleTracker = new AppIdleTracker(3000),
                videoScalingContainer = new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ScalingContainerNew(ScalingMode.Video)
                    {
                        Children = new Drawable[] {
                            new ParallaxContainer
                            {
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.Black,
                                    },
                                    thumbnailContainer = new ThumbnailContainerBackground
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.Black,
                                        Alpha = .5f,
                                    },
                                },
                            },
                        },
                    },
                },
                videoContainer = new BufferedContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                new GlobalScrollAdjustsVolume(),
                userInterfaceContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        /*
                        spinner = new NekoPlayerLoadingSpinner(true, true)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Margin = new MarginPadding(40),
                        },
                        */
                        videoLoadingProgressBase = new CircularContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 4),
                                    Children = new Drawable[]
                                    {
                                        videoLoadingProgressSpinner = new NekoPlayerLoadingSpinner(true, true)
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                        },
                                        new CircularContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Masking = true,
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Children = new Drawable[]
                                            {
                                                videoLoadingProgressBaseBG = new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = overlayColourProvider.Background3,
                                                    Alpha = 0.7f,
                                                },
                                                new Container
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    Padding = new MarginPadding(8),
                                                    Child = videoLoadingProgress = new ProjectYomiSpriteText
                                                    {
                                                        Anchor = Anchor.CentreLeft,
                                                        Origin = Anchor.CentreLeft,
                                                    }
                                                }
                                            },
                                        }
                                    }
                                },
                            },
                        },
                        uiGradientContainer = new DrawSizePreservingFillContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.65f), Color4.Black.Opacity(0)),
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 300,
                                },
                                new Box
                                {
                                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0), Color4.Black.Opacity(0.65f)),
                                    Origin = Anchor.BottomLeft,
                                    Anchor = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 300,
                                },
                            }
                        },
                        uiContainer = new BufferedContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding(8),
                            Children = new Drawable[]
                            {
                                topUIContainer = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[] {
                                        videoMetadataDisplayBase = new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding
                                            {
                                                Right = 44,
                                            },
                                            Child = videoMetadataDisplay = new VideoMetadataDisplayWithoutProfile
                                            {
                                                AutoSizeAxes = Axes.Both,
                                                Origin = Anchor.TopLeft,
                                                Anchor = Anchor.TopLeft,
                                                ClickEvent = _ => showOverlayContainer(videoDescriptionContainer),
                                            },
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Children = new Drawable[]
                                            {
                                                menuOverlayShow = new IconButton
                                                {
                                                    Enabled = { Value = true },
                                                    Origin = Anchor.TopRight,
                                                    Anchor = Anchor.TopRight,
                                                    Size = new Vector2(40, 40),
                                                    Icon = FontAwesome.Solid.Bars,
                                                    IconScale = new Vector2(1.2f),
                                                    TooltipText = NekoPlayerStrings.Menu,
                                                    BackgroundColour = overlayColourProvider.Background5,
                                                },
                                            }
                                        },
                                    }
                                },
                                bottomUIContainer = new Container {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[] {
                                        new Container
                                        {
                                            Anchor = Anchor.BottomCentre,
                                            Origin = Anchor.BottomCentre,
                                            RelativeSizeAxes = Axes.X,
                                            Height = 84,
                                            Masking = false,
                                            //CornerRadius = NekoPlayerApp.UI_CORNER_RADIUS,
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
                                                new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = overlayColourProvider.Background5,
                                                    Alpha = 0f,
                                                },
                                                new FillFlowContainer {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Padding = new MarginPadding(16),
                                                    Spacing = new Vector2(0, 2),
                                                    Children = new Drawable[] {
                                                        seekbar = new RoundedSeekBar
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            PlaySamplesOnAdjust = false,
                                                            DisplayAsPercentage = true,
                                                            AlwaysPresent = true,
                                                            Current = { BindTarget = videoProgress },
                                                        },
                                                        new Container
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Children = new Drawable[] {
                                                                currentTime = new ProjectYomiSpriteText
                                                                {
                                                                    Anchor = Anchor.TopLeft,
                                                                    Origin = Anchor.TopLeft,
                                                                    Text = "0:00",
                                                                    Alpha = 0,
                                                                    Colour = overlayColourProvider.Content2,
                                                                },
                                                            },
                                                        },
                                                        new ProjectYomiRoundedScrollContainer(Direction.Horizontal)
                                                        {
                                                            ScrollbarVisible = false,
                                                            Masking = false,
                                                            RelativeSizeAxes = Axes.Both,
                                                            Children = new Drawable[]
                                                            {
                                                                new FillFlowContainer
                                                                {
                                                                    RelativeSizeAxes = Axes.Y,
                                                                    AutoSizeAxes = Axes.X,
                                                                    AlwaysPresent = true,
                                                                    Spacing = new Vector2(8, 0),
                                                                    Direction = FillDirection.Horizontal,
                                                                    Children = new Drawable[]
                                                                    {
                                                                        new Container
                                                                        {
                                                                            AutoSizeAxes = Axes.X,
                                                                            Height = 30,
                                                                            Children = new Drawable[]
                                                                            {
                                                                                new FillFlowContainer
                                                                                {
                                                                                    AutoSizeAxes = Axes.Both,
                                                                                    Spacing = new Vector2(3, 0),
                                                                                    Direction = FillDirection.Horizontal,
                                                                                    Children = new Drawable[]
                                                                                    {
                                                                                        prevVideoButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = false },
                                                                                            Icon = FontAwesome.Solid.FastBackward,
                                                                                            TooltipText = NekoPlayerStrings.PreviousVideo,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = async _ =>
                                                                                            {
                                                                                                if (playlists.Count > 0)
                                                                                                {
                                                                                                    if (playlistItemIndex != 0)
                                                                                                        playlistItemIndex--;

                                                                                                    Schedule(async () =>
                                                                                                    {
                                                                                                        SetVideoSource(playlists[playlistItemIndex].Snippet.ResourceId.VideoId);
                                                                                                    });
                                                                                                }
                                                                                            }
                                                                                        },
                                                                                        playPause = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.Play,
                                                                                            TooltipText = NekoPlayerStrings.Play,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                if (currentVideoSource != null)
                                                                                                {
                                                                                                    if (currentVideoSource.IsPlaying())
                                                                                                        currentVideoSource.Pause();
                                                                                                    else
                                                                                                        currentVideoSource.Play();
                                                                                                }
                                                                                            }
                                                                                        },
                                                                                        nextVideoButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = false },
                                                                                            Icon = FontAwesome.Solid.FastForward,
                                                                                            TooltipText = NekoPlayerStrings.NextVideo,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = async _ =>
                                                                                            {
                                                                                                if (playlists.Count > 0)
                                                                                                {
                                                                                                    if (playlistItemIndex != playlists.Count - 1)
                                                                                                        playlistItemIndex++;

                                                                                                    Schedule(async () =>
                                                                                                    {
                                                                                                        SetVideoSource(playlists[playlistItemIndex].Snippet.ResourceId.VideoId);
                                                                                                    });
                                                                                                }
                                                                                            }
                                                                                        },
                                                                                        repeatButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.Sync,
                                                                                            TooltipText = NekoPlayerStrings.Repeat,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                updateRepeatState();
                                                                                            }
                                                                                        },
                                                                                        captionButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.ClosedCaptioning,
                                                                                            TooltipText = NekoPlayerStrings.ClosedCaptions,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                CycleCaptionLanguage();
                                                                                            }
                                                                                        },
                                                                                        videoSettingsButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.Cog,
                                                                                            TooltipText = NekoPlayerStrings.VideoSettings,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                ShowSettingsOverlayAtName("Video Settings");
                                                                                            }
                                                                                        },
                                                                                        playlistButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.List,
                                                                                            TooltipText = NekoPlayerStrings.Playlists,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                showOverlayContainer(playlistOverlay);
                                                                                            }
                                                                                        },
                                                                                        quickCommentOpenButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = false },
                                                                                            Icon = FontAwesome.Regular.CommentAlt,
                                                                                            TooltipText = NekoPlayerStrings.Comments("0"),
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                showOverlayContainer(commentsContainer);
                                                                                            }
                                                                                        },
                                                                                        pinButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.MapPin,
                                                                                            TooltipText = NekoPlayerStrings.PlayerControlPin,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                updatePinState();
                                                                                            }
                                                                                        },
                                                                                        quickLikeButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = false },
                                                                                            Icon = FontAwesome.Solid.ThumbsUp,
                                                                                            TooltipText = NekoPlayerStrings.LikeButton,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                        },
                                                                                        quickDislikeButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = false },
                                                                                            Icon = FontAwesome.Solid.ThumbsDown,
                                                                                            TooltipText = NekoPlayerStrings.DislikeButton,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                        },
                                                                                    }
                                                                                }
                                                                            }
                                                                        },
                                                                        new Container
                                                                        {
                                                                            AutoSizeAxes = Axes.X,
                                                                            Height = 30,
                                                                            Masking = true,
                                                                            CornerRadius = 15,
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
                                                                                speedBarBG = new Box
                                                                                {
                                                                                    RelativeSizeAxes = Axes.Both,
                                                                                    Colour = overlayColourProvider.Background3,
                                                                                    Alpha = 1f,
                                                                                },
                                                                                new FillFlowContainer
                                                                                {
                                                                                    AutoSizeAxes = Axes.Both,
                                                                                    Spacing = new Vector2(8, 0),
                                                                                    Direction = FillDirection.Horizontal,
                                                                                    Padding = new MarginPadding
                                                                                    {
                                                                                        Horizontal = 8
                                                                                    },
                                                                                    Children = new Drawable[]
                                                                                    {
                                                                                        speedBarIcon = new SpriteIcon
                                                                                        {
                                                                                            Icon = FontAwesome.Solid.TachometerAlt,
                                                                                            Width = 16,
                                                                                            Height = 16,
                                                                                            Margin = new MarginPadding
                                                                                            {
                                                                                                Top = 8,
                                                                                            },
                                                                                            Colour = overlayColourProvider.Content2,
                                                                                        },
                                                                                        speedBarSlider = new PlaybackSpeedSliderBar
                                                                                        {
                                                                                            Width = 200,
                                                                                            Margin = new MarginPadding
                                                                                            {
                                                                                                Top = 6,
                                                                                            },
                                                                                            KeyboardStep = 0.05f,
                                                                                            PlaySamplesOnAdjust = true,
                                                                                            AlwaysPresent = true,
                                                                                            Current = { BindTarget = playbackSpeed },
                                                                                        },
                                                                                        speedText = new ProjectYomiSpriteText
                                                                                        {
                                                                                            Margin = new MarginPadding
                                                                                            {
                                                                                                Top = 7
                                                                                            },
                                                                                            AlwaysPresent = true,
                                                                                            Font = NekoPlayerApp.DefaultFont.With(weight: "Bold"),
                                                                                            Colour = overlayColourProvider.Content2,
                                                                                        },
                                                                                    }
                                                                                }
                                                                            }
                                                                        },
                                                                        new Container
                                                                        {
                                                                            AutoSizeAxes = Axes.X,
                                                                            Height = 30,
                                                                            Masking = true,
                                                                            CornerRadius = 15,
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
                                                                                volumeBarBG = new Box
                                                                                {
                                                                                    RelativeSizeAxes = Axes.Both,
                                                                                    Colour = overlayColourProvider.Background3,
                                                                                    Alpha = 1f,
                                                                                },
                                                                                new FillFlowContainer
                                                                                {
                                                                                    AutoSizeAxes = Axes.Both,
                                                                                    Spacing = new Vector2(8, 0),
                                                                                    Direction = FillDirection.Horizontal,
                                                                                    Padding = new MarginPadding
                                                                                    {
                                                                                        Horizontal = 8
                                                                                    },
                                                                                    Children = new Drawable[]
                                                                                    {
                                                                                        volumeIcon = new SpriteIcon
                                                                                        {
                                                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                                                            Width = 16,
                                                                                            Height = 16,
                                                                                            Margin = new MarginPadding
                                                                                            {
                                                                                                Top = 8,
                                                                                            },
                                                                                            Colour = overlayColourProvider.Content2,
                                                                                        },
                                                                                        volumeBarSlider = new RoundedSliderBar<double>
                                                                                        {
                                                                                            Width = 200,
                                                                                            Margin = new MarginPadding
                                                                                            {
                                                                                                Top = 6,
                                                                                            },
                                                                                            KeyboardStep = 0.05f,
                                                                                            PlaySamplesOnAdjust = false,
                                                                                            DisplayAsPercentage = true,
                                                                                            AlwaysPresent = true,
                                                                                            Current = videoVolume,
                                                                                        },
                                                                                        volumeText = new ProjectYomiSpriteText
                                                                                        {
                                                                                            Margin = new MarginPadding
                                                                                            {
                                                                                                Top = 7
                                                                                            },
                                                                                            AlwaysPresent = true,
                                                                                            Font = NekoPlayerApp.DefaultFont.With(weight: "Bold"),
                                                                                            Colour = overlayColourProvider.Content2,
                                                                                        },
                                                                                    }
                                                                                }
                                                                            }
                                                                        },
                                                                        new Container
                                                                        {
                                                                            AutoSizeAxes = Axes.X,
                                                                            Height = 30,
                                                                            Masking = true,
                                                                            CornerRadius = 15,
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
                                                                                timeBG = new Box
                                                                                {
                                                                                    RelativeSizeAxes = Axes.Both,
                                                                                    Colour = overlayColourProvider.Background3,
                                                                                    Alpha = 1f,
                                                                                },
                                                                                new FillFlowContainer
                                                                                {
                                                                                    AutoSizeAxes = Axes.Both,
                                                                                    Spacing = new Vector2(8, 0),
                                                                                    Direction = FillDirection.Horizontal,
                                                                                    Padding = new MarginPadding
                                                                                    {
                                                                                        Horizontal = 8
                                                                                    },
                                                                                    Children = new Drawable[]
                                                                                    {
                                                                                        timeText = new ProjectYomiSpriteText
                                                                                        {
                                                                                            Margin = new MarginPadding
                                                                                            {
                                                                                                Top = 7
                                                                                            },
                                                                                            AlwaysPresent = true,
                                                                                            Font = NekoPlayerApp.DefaultFont.With(weight: "Bold"),
                                                                                            Colour = overlayColourProvider.Content2,
                                                                                            Text = "0:00 / 0:00"
                                                                                        },
                                                                                    }
                                                                                }
                                                                            }
                                                                        },
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            },
                                        }
                                    }
                                }
                            }
                        },
                        overlayFadeContainer = new OverlayFadeContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ClickAction = _ => hideOverlays(),
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.Black,
                            }
                        },
                        loadVideoContainer = new BottomOverlayContainer
                        {
                            Size = new Vector2(0.7f, 1f),
                            Height = 200,
                            RelativeSizeAxes = Axes.X,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.LoadFromVideoId,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding(14),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(loadVideoContainer);
                                    }
                                },
                                loadBtn = new RoundedButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.BottomCentre,
                                    Anchor = Anchor.BottomCentre,
                                    Text = NekoPlayerStrings.LoadVideo,
                                    Size = new Vector2(450, 40),
                                    Margin = new MarginPadding(16),
                                },
                                videoIdBox = new EnhancedFocusedTextBox
                                {
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.Centre,
                                    Text = "",
                                    FontSize = 30,
                                    Margin = new MarginPadding(8),
                                    Size = new Vector2(.9f, 60),
                                    RelativeSizeAxes = Axes.X,
                                    OnEnterKeyPressed = () =>
                                    {
                                        if (string.IsNullOrEmpty(videoIdBox.Text))
                                            return;

                                        Task.Run(async () =>
                                        {
                                            try
                                            {
                                                Schedule(async () =>
                                                {
                                                    ClearPlaylistItems();
                                                    Schedule(async () =>
                                                    {
                                                        YoutubeExplode.Playlists.PlaylistId? playlistId = YoutubeExplode.Playlists.PlaylistId.TryParse(videoIdBox.Text);
                                                        YoutubeExplode.Videos.VideoId? videoId = YoutubeExplode.Videos.VideoId.TryParse(videoIdBox.Text);

                                                        if (videoId != null && !string.IsNullOrEmpty(videoId.Value))
                                                        {
                                                            SetVideoSource(videoIdBox.Text);
                                                        } else
                                                        {
                                                            SetPlaylist(videoIdBox.Text).FireAndForget();
                                                        }
                                                    });
                                                });
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Error(ex, ex.GetDescription());
                                            }
                                        });
                                    }
                                },
                            }
                        },
                        settingsContainer = new SettingsContainer
                        {
                           OAuthSignInAction = () =>
                           {
                                if (!googleOAuth2.SignedIn.Value)
                                {
                                    Task.Run(() => googleOAuth2.SignIn());
                                }
                                else
                                {
                                    hideOverlays();
                                    showOverlayContainer(myChannelDialog);
                                }
                           },
                           CheckUpdateAction = () =>
                           {
                                if (game.UpdateManager is NoActionUpdateManager)
                                {
                                    host.OpenUrlExternally(@"https://github.com/ZeroMayo/NekoPlayer/releases");
                                }
                                else
                                {
                                    if (game.RestartRequired.Value != true)
                                        checkForUpdates().FireAndForget();
                                    else
                                        game.RestartAction.Invoke();
                                }
                           },
                           CloseOverlayAction = () =>
                           {
                               hideOverlayContainer(settingsContainer);
                           }
                        },
                        videoDescriptionContainer = new BottomOverlayContainer
                        {
                            Size = new Vector2(0.7f),
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding()
                                    {
                                        Horizontal = 6,
                                    },
                                    Child = new ProjectYomiScrollContainer
                                    {
                                        Padding = new MarginPadding()
                                        {
                                            Top = 96,
                                            Bottom = 6,
                                        },
                                        RelativeSizeAxes = Axes.Both,
                                        CornerRadius = NekoPlayerApp.UI_CORNER_RADIUS,
                                        Masking = true,
                                        ScrollbarVisible = false,
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                CornerRadius = NekoPlayerApp.UI_CORNER_RADIUS,
                                                Masking = true,
                                                Child = new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = overlayColourProvider.Background4,
                                                    Alpha = 0.7f,
                                                },
                                            },
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                CornerRadius = NekoPlayerApp.UI_CORNER_RADIUS,
                                                Spacing = new Vector2(0, 8),
                                                Padding = new MarginPadding(12),
                                                Masking = true,
                                                Children = new Drawable[]
                                                {
                                                    videoInfoDetails = new ProjectYomiSpriteText
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        Font = NekoPlayerApp.DefaultFont.With(weight: "Bold"),
                                                        Colour = overlayColourProvider.Content2,
                                                        AlwaysPresent = true,
                                                    },
                                                    videoDescription = new LinkFlowContainer(f => f.Font = NekoPlayerApp.DefaultFont)
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        AlwaysPresent = true,
                                                        Colour = overlayColourProvider.Content2,
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = 96 + 24,
                                },
                                new Container
                                {
                                    Padding = new MarginPadding(6),
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Child = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Masking = true,
                                        CornerRadius = new CornersInfo(16),
                                        Children = new Drawable[]
                                        {
                                            new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Colour = overlayColourProvider.Background4,
                                            },
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Padding = new MarginPadding(6),
                                                Children = new Drawable[]
                                                {
                                                    new Container
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Children = new Drawable[]
                                                        {
                                                            new Container
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                AutoSizeAxes = Axes.Y,
                                                                Child = videoMetadataDisplayDetails = new VideoMetadataDisplay
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    Height = 40,
                                                                    Origin = Anchor.TopLeft,
                                                                    Anchor = Anchor.TopLeft,
                                                                    AlwaysPresent = true,
                                                                },
                                                            },
                                                            new Box
                                                            {
                                                                Name = "masking of overlay",
                                                                Colour = ColourInfo.GradientHorizontal(overlayColourProvider.Background4.Opacity(0), overlayColourProvider.Background4),
                                                                RelativeSizeAxes = Axes.Y,
                                                                Width = 100,
                                                                Anchor = Anchor.CentreRight,
                                                                Origin = Anchor.CentreRight,
                                                            },
                                                        },
                                                    },
                                                    new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(2, 0),
                                                        Children = new Drawable[]
                                                        {
                                                            profileImage = new ProfileImage(32),
                                                            subscribeButton = new RoundedAdaptiveButtonV2
                                                            {
                                                                Enabled = { Value = true },
                                                                Width = 90,
                                                                Height = 32,
                                                                Text = NekoPlayerStrings.Subscribe,
                                                            },
                                                            likeButton = new RoundedButtonContainer
                                                            {
                                                                AutoSizeAxes = Axes.X,
                                                                Height = 32,
                                                                CornerRadius = new CornersInfo(16, 16, NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f),
                                                                Masking = true,
                                                                AlwaysPresent = true,
                                                                Children = new Drawable[]
                                                                {
                                                                    new Container
                                                                    {
                                                                        RelativeSizeAxes = Axes.Both,
                                                                        Children = new Drawable[] {
                                                                            likeButtonBackground = new Box
                                                                            {
                                                                                RelativeSizeAxes = Axes.Both,
                                                                                Colour = overlayColourProvider.Background3,
                                                                                Alpha = 1f,
                                                                            },
                                                                            likeButtonBackgroundSelected = new Box
                                                                            {
                                                                                RelativeSizeAxes = Axes.Both,
                                                                                Colour = overlayColourProvider.Content2,
                                                                                Alpha = 0f,
                                                                            },
                                                                        },
                                                                    },
                                                                    likeButtonForeground = new FillFlowContainer
                                                                    {
                                                                        AutoSizeAxes = Axes.X,
                                                                        RelativeSizeAxes = Axes.Y,
                                                                        Direction = FillDirection.Horizontal,
                                                                        Spacing = new Vector2(4, 0),
                                                                        Padding = new MarginPadding(8),
                                                                        Colour = overlayColourProvider.Content2,
                                                                        Children = new Drawable[]
                                                                        {
                                                                            new SpriteIcon
                                                                            {
                                                                                Width = 15,
                                                                                Height = 15,
                                                                                Icon = FontAwesome.Solid.ThumbsUp,
                                                                            },
                                                                            likeCount = new ProjectYomiSpriteText
                                                                            {
                                                                                Text = "0",
                                                                            },
                                                                        }
                                                                    }
                                                                }
                                                            },
                                                            dislikeButton = new RoundedButtonContainer
                                                            {
                                                                AutoSizeAxes = Axes.X,
                                                                Height = 32,
                                                                CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f, 16, 16),
                                                                Masking = true,
                                                                AlwaysPresent = true,
                                                                Children = new Drawable[]
                                                                {
                                                                    new Container
                                                                    {
                                                                        RelativeSizeAxes = Axes.Both,
                                                                        Children = new Drawable[] {
                                                                            dislikeButtonBackground = new Box
                                                                            {
                                                                                RelativeSizeAxes = Axes.Both,
                                                                                Colour = overlayColourProvider.Background3,
                                                                                Alpha = 1f,
                                                                            },
                                                                            dislikeButtonBackgroundSelected = new Box
                                                                            {
                                                                                RelativeSizeAxes = Axes.Both,
                                                                                Colour = overlayColourProvider.Content2,
                                                                                Alpha = 0f,
                                                                            },
                                                                        },
                                                                    },
                                                                    dislikeButtonForeground = new FillFlowContainer
                                                                    {
                                                                        AutoSizeAxes = Axes.X,
                                                                        RelativeSizeAxes = Axes.Y,
                                                                        Direction = FillDirection.Horizontal,
                                                                        Spacing = new Vector2(4, 0),
                                                                        Padding = new MarginPadding(8),
                                                                        Colour = overlayColourProvider.Content2,
                                                                        Children = new Drawable[]
                                                                        {
                                                                            new SpriteIcon
                                                                            {
                                                                                Width = 15,
                                                                                Height = 15,
                                                                                Icon = FontAwesome.Solid.ThumbsDown,
                                                                            },
                                                                            dislikeCount = new ProjectYomiSpriteText
                                                                            {
                                                                                Text = "0",
                                                                            },
                                                                        }
                                                                    }
                                                                }
                                                            },
                                                            commentOpenButtonDetails = new RoundedButtonContainer
                                                            {
                                                                AutoSizeAxes = Axes.X,
                                                                Height = 32,
                                                                CornerRadius = 16,
                                                                Masking = true,
                                                                AlwaysPresent = true,
                                                                ClickAction = f =>
                                                                {
                                                                    if (commentsDisabled)
                                                                        return;

                                                                    hideOverlays();
                                                                    showOverlayContainer(commentsContainer);
                                                                },
                                                                Children = new Drawable[]
                                                                {
                                                                    new Container
                                                                    {
                                                                        RelativeSizeAxes = Axes.Both,
                                                                        CornerRadius = NekoPlayerApp.UI_CORNER_RADIUS / 1.5f,
                                                                        Child = new Box
                                                                        {
                                                                            RelativeSizeAxes = Axes.Both,
                                                                            Colour = overlayColourProvider.Background3,
                                                                            Alpha = 1f,
                                                                        },
                                                                    },
                                                                    new FillFlowContainer
                                                                    {
                                                                        AutoSizeAxes = Axes.X,
                                                                        RelativeSizeAxes = Axes.Y,
                                                                        Direction = FillDirection.Horizontal,
                                                                        Spacing = new Vector2(4, 0),
                                                                        Padding = new MarginPadding(8),
                                                                        Children = new Drawable[]
                                                                        {
                                                                            new SpriteIcon
                                                                            {
                                                                                Width = 15,
                                                                                Height = 15,
                                                                                Icon = FontAwesome.Regular.CommentAlt,
                                                                                Colour = overlayColourProvider.Content2,
                                                                            },
                                                                            commentCount = new ProjectYomiSpriteText
                                                                            {
                                                                                Text = "0",
                                                                                Colour = overlayColourProvider.Content2,
                                                                            },
                                                                        }
                                                                    }
                                                                }
                                                            },
                                                        }
                                                    },
                                                }
                                            }
                                        },
                                    },
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding(14),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background3,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(videoDescriptionContainer);
                                    }
                                },
                            }
                        },
                        commentsContainer = new SideOverlayContainer
                        {
                            Size = new Vector2(0.45f, 1f),
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, 0),
                            Masking = true,
                            Origin = Anchor.CentreRight,
                            Anchor = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Top = 56,
                                                Bottom = 56 + 8,
                                            },
                                            Children = new Drawable[]
                                            {
                                                commentContainer = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Spacing = new Vector2(0, 4),
                                                    AlwaysPresent = true,
                                                }
                                            }
                                        },
                                        commentsEmpty = new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    Direction = FillDirection.Vertical,
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Width = 300,
                                                    AutoSizeAxes = Axes.Y,
                                                    Children = new Drawable[]
                                                    {
                                                        new Container
                                                        {
                                                            Anchor = Anchor.TopCentre,
                                                            Origin = Anchor.TopCentre,
                                                            Margin = new MarginPadding(10),
                                                            Size = new Vector2(50),
                                                            Child = ghostIcon = new GhostIcon
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                            },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Anchor = Anchor.TopCentre,
                                                            Origin = Anchor.TopCentre,
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 32, weight: "Bold"),
                                                            Text = NekoPlayerStrings.NoComments,
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new LinkFlowContainer
                                                        {
                                                            Alpha = 1,
                                                            AlwaysPresent = true,
                                                            Anchor = Anchor.TopCentre,
                                                            Origin = Anchor.TopCentre,
                                                            Padding = new MarginPadding { Top = 8 },
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            TextAnchor = Anchor.Centre,
                                                            Text = NekoPlayerStrings.NoCommentsDesc,
                                                            Colour = overlayColourProvider.Foreground2,
                                                        }
                                                    }
                                                },
                                                new Sprite
                                                {
                                                    Size = new Vector2(120),
                                                    Texture = textures.Get(@"speaki"),
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Margin = new MarginPadding { Left = 35, Bottom = 56 },
                                                },
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = 56 + 20,
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5),
                                    Height = 56 + 20,
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding(14),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(commentsContainer);
                                    }
                                },
                                commentsContainerTitle = new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    Text = NekoPlayerStrings.Comments("0"),
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                new OverlaySortTabControl<CommentsSortCriteria>
                                {
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Current = commentsSort,
                                    Margin = new MarginPadding()
                                    {
                                        Top = 15,
                                        Right = 20 + 35,
                                    },
                                },
                                commentTextBoxContainer = new Container
                                {
                                    Margin = new MarginPadding
                                    {
                                        Bottom = 12,
                                    },
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 48,
                                    },
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    RelativeSizeAxes = Axes.X,
                                    Size = new Vector2(0.95f, 1f),
                                    Height = 45,
                                    Children = new Drawable[]
                                    {
                                        commentTextBox = new EnhancedFocusedTextBoxWithProfileImage
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = "",
                                            FontSize = 20,
                                            Height = 45,
                                            PlaceholderText = NekoPlayerStrings.LoginToComment,
                                            EdgeEffect = new osu.Framework.Graphics.Effects.EdgeEffectParameters
                                            {
                                                Type = osu.Framework.Graphics.Effects.EdgeEffectType.Shadow,
                                                Colour = Color4.Black.Opacity(0.25f),
                                                Offset = new Vector2(0, 8),
                                                Radius = 64,
                                            },
                                            OnEnterKeyPressed = () =>
                                            {
                                                if (videoData == null)
                                                    return;

                                                if (!googleOAuth2.SignedIn.Value)
                                                    return;

                                                if (string.IsNullOrEmpty(commentTextBox.Text))
                                                    return;

                                                //ToastBase toast = new ToastWithIcon(NekoPlayerStrings.General, NekoPlayerStrings.CommentAdded, FontAwesome.Regular.Comment);
                                                api.SendComment(videoId, commentTextBox.Text);

                                                Scheduler.AddDelayed(() => updateComments(videoId), 2000);

                                                //Schedule(() => onScreenDisplay.Display(toast));
                                                Schedule(() =>
                                                {
                                                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Regular.Comment, Color4.White, NekoPlayerStrings.CommentAdded, NekoPlayerStrings.General));
                                                });

                                                commentTextBox.Text = string.Empty;
                                            }
                                        },
                                        commentSendButton = new IconButton
                                        {
                                            Origin = Anchor.CentreRight,
                                            Anchor = Anchor.CentreRight,
                                            Margin = new MarginPadding
                                            {
                                                Right = 6,
                                            },
                                            Icon = FontAwesome.Solid.PaperPlane,
                                            Width = 35,
                                            Height = 35,
                                            AlwaysPresent = true,
                                            Enabled = { Value = true },
                                            BackgroundColour = overlayColourProvider.Background3,
                                        },
                                    },
                                },
                            }
                        },
                        searchContainer = new BottomOverlayContainer
                        {
                            Size = new Vector2(0.7f),
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Top = (56 * 2),
                                                Bottom = 6,
                                            },
                                            Children = new Drawable[]
                                            {
                                                searchResultContainer = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Spacing = new Vector2(0, 4),
                                                    AlwaysPresent = true,
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = 56 + 20,
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5),
                                    Height = 56 + 20,
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.Search,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding(14),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(searchContainer);
                                    }
                                },
                                new OverlaySortTabControl<SearchSortCriteria>
                                {
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Current = searchSort,
                                    Margin = new MarginPadding()
                                    {
                                        Top = 15,
                                        Right = 20 + 35,
                                    },
                                },
                                searchTextBoxContainer = new Container
                                {
                                    Margin = new MarginPadding
                                    {
                                        Bottom = 12,
                                    },
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 48,
                                    },
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    RelativeSizeAxes = Axes.X,
                                    Size = new Vector2(0.55f, 1f),
                                    Height = 45,
                                    Children = new Drawable[]
                                    {
                                        searchTextBox = new EnhancedFocusedTextBox
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Text = "",
                                            PlaceholderText = NekoPlayerStrings.SearchPlaceholder,
                                            FontSize = 20,
                                            Height = 45,
                                            EdgeEffect = new osu.Framework.Graphics.Effects.EdgeEffectParameters
                                            {
                                                Type = osu.Framework.Graphics.Effects.EdgeEffectType.Shadow,
                                                Colour = Color4.Black.Opacity(0.25f),
                                                Offset = new Vector2(0, 8),
                                                Radius = 64,
                                            },
                                            OnEnterKeyPressed = () =>
                                            {
                                                if (string.IsNullOrEmpty(searchTextBox.Text))
                                                    return;

                                                Schedule(() => Search());
                                            }
                                        },
                                        searchButton = new IconButton
                                        {
                                            Origin = Anchor.CentreRight,
                                            Anchor = Anchor.CentreRight,
                                            Margin = new MarginPadding
                                            {
                                                Right = 6,
                                            },
                                            Icon = FontAwesome.Solid.Search,
                                            Width = 35,
                                            Height = 35,
                                            AlwaysPresent = true,
                                            Enabled = { Value = true },
                                        },
                                    },
                                },
                            }
                        },
                        reportAbuseOverlay = new BottomOverlayContainer
                        {
                            Size = new Vector2(0.7f, 1f),
                            Height = 300,
                            RelativeSizeAxes = Axes.X,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[]
                                    {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Bottom = 72,
                                                Top = 56,
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        new TruncatingSpriteText
                                                        {
                                                            Text = NekoPlayerStrings.WhatsGoingOn,
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 27, weight: "Bold"),
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new ProjectYomiTextFlowContainer(f => f.Font = NekoPlayerApp.DefaultFont.With(size: 17, weight: "Regular"))
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Text = NekoPlayerStrings.ReportDesc,
                                                            Colour = overlayColourProvider.Background1,
                                                        },
                                                        reportReason = new ReportDropdown
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Caption = NekoPlayerStrings.ReportReason,
                                                        },
                                                        reportSubReason = new ReportDropdown
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Caption = NekoPlayerStrings.ReportSubReason,
                                                        },
                                                        reportComment = new FormTextBox
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = NekoPlayerStrings.Description,
                                                        },
                                                    }
                                                }
                                            }
                                        },
                                        reportButton = new SettingsButtonV2
                                        {
                                            Height = 40,
                                            Padding = new MarginPadding
                                            {
                                                Horizontal = 16,
                                            },
                                            Margin = new MarginPadding
                                            {
                                                Bottom = 16,
                                            },
                                            Text = NekoPlayerStrings.Submit,
                                            BackgroundColour = colours.Yellow,
                                            Origin = Anchor.BottomCentre,
                                            Anchor = Anchor.BottomCentre,
                                        },
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding(14),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(reportAbuseOverlay);
                                    }
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.Report,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        playlistOverlay = new SideOverlayContainer
                        {
                            Name = "Playlist Overlay",
                            Size = new Vector2(1f, 1f),
                            Width = 400,
                            RelativeSizeAxes = Axes.Y,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, 0),
                            Masking = true,
                            Origin = Anchor.CentreRight,
                            Anchor = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Bottom = 16,
                                                Top = 250 + 16
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        playlistItemsView = new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Spacing = new Vector2(4),
                                                            Padding = new MarginPadding
                                                            {
                                                                Horizontal = 16,
                                                            },
                                                            Children = Array.Empty<Drawable>()
                                                        },
                                                    }
                                                }
                                            }
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 250,
                                            Masking = true,
                                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS),
                                            Children = new Drawable[] {
                                                new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = Color4.Black,
                                                },
                                                new Container
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Margin = new MarginPadding(10),
                                                    Size = new Vector2(50),
                                                    Child = ghostIcon2 = new GhostIcon
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                    Colour = overlayColourProvider.Content2,
                                                },
                                                playlistThumbnail = new Sprite
                                                {
                                                    Scale = new Vector2(1.5f),
                                                    Origin = Anchor.Centre,
                                                    Anchor = Anchor.Centre,
                                                    RelativeSizeAxes = Axes.Both,
                                                    FillMode = FillMode.Fill,
                                                },
                                                new Box
                                                {
                                                    Name = "masking of overlay",
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.BottomCentre,
                                                    Origin = Anchor.BottomCentre,
                                                    Height = (56 + 56),
                                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5),
                                                },
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Anchor = Anchor.BottomCentre,
                                                    Origin = Anchor.BottomCentre,
                                                    Padding = new MarginPadding
                                                    {
                                                        Bottom = 16,
                                                    },
                                                    Spacing = new Vector2(4),
                                                    Height = 100,
                                                    Children = new Drawable[]
                                                    {
                                                        playlistAuthor = new LinkFlowContainer(f =>
                                                        {
                                                            f.Font = NekoPlayerApp.DefaultFont.With(size: 16, weight: "SemiBold");
                                                            f.Colour = overlayColourProvider.Background1;
                                                        })
                                                        {
                                                            TextAnchor = Anchor.Centre,
                                                            Origin = Anchor.BottomCentre,
                                                            Anchor = Anchor.BottomCentre,
                                                            Text = NekoPlayerStrings.PlaylistNotLoadedDesc,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                        },
                                                        playlistName = new ProjectYomiTextFlowContainer(f =>
                                                        {
                                                            f.Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "Bold");
                                                            f.Colour = overlayColourProvider.Content2;
                                                        })
                                                        {
                                                            TextAnchor = Anchor.Centre,
                                                            Origin = Anchor.BottomCentre,
                                                            Anchor = Anchor.BottomCentre,
                                                            Text = NekoPlayerStrings.PlaylistNotLoaded,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                        },
                                                    }
                                                }
                                            },
                                        },
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                    Margin = new MarginPadding { Top = 250 }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5),
                                    Height = (56 + 20),
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding(14),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(playlistOverlay);
                                    }
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    Text = NekoPlayerStrings.Playlists,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        myPlaylistsOverlay = new SideOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 400,
                            RelativeSizeAxes = Axes.Y,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, 0),
                            Masking = true,
                            Origin = Anchor.CentreRight,
                            Anchor = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Bottom = 16,
                                                Top = 56,
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        myPlaylistItemsView = new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Vertical,
                                                            Spacing = new Vector2(4),
                                                            Children = Array.Empty<Drawable>()
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Margin = new MarginPadding(14),
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(myPlaylistsOverlay);
                                    }
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    Text = NekoPlayerStrings.MyPlaylists,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        loadPlaylistContainer = new BottomOverlayContainer
                        {
                            Size = new Vector2(0.7f, 1f),
                            Height = 200,
                            RelativeSizeAxes = Axes.X,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.LoadFromPlaylistId,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                loadPlaylistBtn = new ProjectYomiButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.BottomRight,
                                    Anchor = Anchor.BottomRight,
                                    Text = NekoPlayerStrings.LoadPlaylist,
                                    Size = new Vector2(200, 60),
                                    Margin = new MarginPadding(8),
                                },
                                playlistIdBox = new EnhancedFocusedTextBox
                                {
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.Centre,
                                    Text = "",
                                    FontSize = 30,
                                    Size = new Vector2(0.9f, 60),
                                    RelativeSizeAxes = Axes.X,
                                    Margin = new MarginPadding(8),
                                    OnEnterKeyPressed = () =>
                                    {
                                        if (string.IsNullOrEmpty(playlistIdBox.Text))
                                            return;

                                        SetPlaylist(playlistIdBox.Text).FireAndForget();
                                    }
                                },
                            }
                        },
                        audioEffectsOverlay = new SideOverlayContainer
                        {
                            Name = "Audio Effects Overlay",
                            Size = new Vector2(1f, 1f),
                            Width = 400,
                            RelativeSizeAxes = Axes.Y,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, 0),
                            Masking = true,
                            Origin = Anchor.CentreRight,
                            Anchor = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Bottom = 16,
                                                Top = 56,
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.ReverbEffect,
                                                            Current = reverbEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleReverbEffect),
                                                        }),
                                                        reverbSettings = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.WetMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.StereoWidth,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbStereoWidth),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.HighFreqDamp,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbDamp),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.RoomSize,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbRoomSize),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.RotateParameters_Enabled,
                                                            Current = rotateEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleRotateEffect),
                                                        }),
                                                        rotateSettings = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.RotateParameters_fRate,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.RotateRate),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.EchoEffect,
                                                            Current = echoEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleEchoEffect),
                                                        }),
                                                        echoSettings = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.DryMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDryMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.EchoWetMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoWetMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.EchoFeedback,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoFeedback),
                                                                    LabelFormat = f => $"{f - 1}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.EchoDelay,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDelay),
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.DistortionEffect,
                                                            Current = distortionEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleDistortionEffect),
                                                        }),
                                                        distortionSettings = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(DistortionVolumeSlider = new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.DistortionVolume,
                                                                    Current = DistortionVolume,
                                                                    DisplayAsPercentage = true,
                                                                })
                                                                {
                                                                    Note = { BindTarget = DistortionVolumeNotice },
                                                                },
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.KaraokeMode,
                                                            Current = karaokeEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleKaraokeEffect),
                                                        }),
                                                        karaokeSettings = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.KaraokeVocalVolume,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.KaraokeVocalVolume),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.ChorusEffect,
                                                            Current = chorusEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleChorusEffect),
                                                        }),
                                                        chorusSettings = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.DryMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusDryMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.EchoWetMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusWetMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.EchoFeedback,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusFeedback),
                                                                    LabelFormat = f => $"{f - 1}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.ChorusMinSweep,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusMinSweep),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.ChorusMaxSweep,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusMaxSweep),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.ChorusRate,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusRate),
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.EightBitEffectEnabled,
                                                            Current = eightBitEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleEightBitEffect),
                                                        }),
                                                        eightBitSettings = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.BitDepth,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth),
                                                                    LabelFormat = f => $"{f} bits",
                                                                }),
                                                            }
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Margin = new MarginPadding(14),
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(audioEffectsOverlay);
                                    }
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    Text = NekoPlayerStrings.AudioEffects,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        unsubscribeDialog = new BottomOverlayContainer
                        {
                            Width = 450,
                            Height = 200,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                youtubeChannelMetadataDisplay = new YouTubeChannelMetadataDisplay
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Margin = new MarginPadding(8),
                                    Size = new Vector2(0.965f, 1f),
                                    Height = 60,
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    AlwaysPresent = true,
                                },
                                new ProjectYomiTextFlowContainer(f => f.Font = NekoPlayerApp.DefaultFont.With(size: 20))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.Centre,
                                    TextAnchor = Anchor.Centre,
                                    AlwaysPresent = true,
                                    Text = NekoPlayerStrings.UnsubscribeDesc,
                                    Colour = overlayColourProvider.Content2,
                                },
                                declineButton = new RoundedAltButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.BottomLeft,
                                    Anchor = Anchor.BottomLeft,
                                    Text = NekoPlayerStrings.Cancel,
                                    Size = new Vector2(200, 40),
                                    Margin = new MarginPadding(8),
                                },
                                acceptButton = new RoundedButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.BottomRight,
                                    Anchor = Anchor.BottomRight,
                                    Text = NekoPlayerStrings.Yes,
                                    Size = new Vector2(200, 40),
                                    BackgroundColour = colours.RedDark,
                                    Margin = new MarginPadding(8),
                                },
                            }
                        },
                        videoSaveLocationOverlay = new BottomOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 450,
                            Height = 210,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Top = 56,
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        myPlaylistsDropdown = new PlaylistDropdown
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = NekoPlayerStrings.Playlists,
                                                        },
                                                        new RoundedAltButton
                                                        {
                                                            Enabled = { Value = true },
                                                            Text = NekoPlayerStrings.AddNewPlaylist,
                                                            RelativeSizeAxes = Axes.X,
                                                            Size = new Vector2(1, 40),
                                                            Action = () =>
                                                            {
                                                                hideOverlays();
                                                                showOverlayContainer(addPlaylistOverlay);
                                                            }
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Horizontal,
                                                            Children = new Drawable[]
                                                            {
                                                                new RoundedAltButton
                                                                {
                                                                    Enabled = { Value = true },
                                                                    Text = NekoPlayerStrings.Cancel,
                                                                    Size = new Vector2(200, 40),
                                                                    Margin = new MarginPadding(4),
                                                                    Action = () =>
                                                                    {
                                                                        hideOverlayContainer(videoSaveLocationOverlay);
                                                                    }
                                                                },
                                                                new RoundedButton
                                                                {
                                                                    Enabled = { Value = true },
                                                                    Text = NekoPlayerStrings.SaveOrRemove,
                                                                    Size = new Vector2(200, 40),
                                                                    Margin = new MarginPadding(4),
                                                                    Action = async () =>
                                                                    {
                                                                        if (videoId != null)
                                                                        {
                                                                            bool trickcalChibiGo = await api.IsVideoExistsOnPlaylist(myPlaylistsDropdown.Current.Value.Id, videoData.Id);
                                                                            hideOverlays();

                                                                            if (!trickcalChibiGo)
                                                                                saveVideoToPlaylist(videoData.Id);
                                                                            else
                                                                                removeVideoFromPlaylist(videoData.Id);
                                                                        }
                                                                    }
                                                                },
                                                            },
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.SaveLocation,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        editPlaylistOverlay = new BottomOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 450,
                            Height = 220,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Bottom = 16,
                                                Top = 56,
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        editPlaylistTitleBox = new FormTextBox
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = NekoPlayerStrings.Title,
                                                            PlaceholderText = NekoPlayerStrings.TitlePlaceholder,
                                                        },
                                                        editPlaylistPrivacyStatusDropdown = new FormEnumDropdown<PrivacyStatus>
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = NekoPlayerStrings.PrivacyStatus,
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Horizontal,
                                                            Children = new Drawable[]
                                                            {
                                                                new RoundedAltButton
                                                                {
                                                                    Enabled = { Value = true },
                                                                    Text = NekoPlayerStrings.Cancel,
                                                                    Size = new Vector2(200, 40),
                                                                    Margin = new MarginPadding(4),
                                                                    Action = () =>
                                                                    {
                                                                        hideOverlayContainer(editPlaylistOverlay);
                                                                    }
                                                                },
                                                                updatePlaylistButton = new RoundedButton
                                                                {
                                                                    Enabled = { Value = true },
                                                                    Text = NekoPlayerStrings.Apply,
                                                                    Size = new Vector2(200, 40),
                                                                    Margin = new MarginPadding(4),
                                                                },
                                                            },
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.EditPlaylist,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        addPlaylistOverlay = new BottomOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 450,
                            Height = 220,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Top = 56,
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        playlistTitleBox = new FormTextBox
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = NekoPlayerStrings.Title,
                                                            PlaceholderText = NekoPlayerStrings.TitlePlaceholder,
                                                        },
                                                        playlistPrivacyStatusDropdown = new FormEnumDropdown<PrivacyStatus>
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = NekoPlayerStrings.PrivacyStatus,
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Horizontal,
                                                            Children = new Drawable[]
                                                            {
                                                                new RoundedAltButton
                                                                {
                                                                    Enabled = { Value = true },
                                                                    Text = NekoPlayerStrings.Cancel,
                                                                    Size = new Vector2(200, 40),
                                                                    Margin = new MarginPadding(4),
                                                                    Action = () =>
                                                                    {
                                                                        hideOverlayContainer(addPlaylistOverlay);
                                                                    }
                                                                },
                                                                new RoundedButton
                                                                {
                                                                    Enabled = { Value = true },
                                                                    Text = NekoPlayerStrings.Create,
                                                                    Size = new Vector2(200, 40),
                                                                    Margin = new MarginPadding(4),
                                                                    Action = async () =>
                                                                    {
                                                                        hideOverlays();
                                                                        await api.AddPlaylist(playlistTitleBox.Current.Value, playlistPrivacyStatusDropdown.Current.Value);
                                                                    }
                                                                },
                                                            },
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.AddNewPlaylist,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        menuOverlay = new SideOverlayContainer
                        {
                            Name = "Menu Overlay",
                            Size = new Vector2(1f, 1f),
                            Width = 500,
                            RelativeSizeAxes = Axes.Y,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, 0),
                            Masking = true,
                            Origin = Anchor.CentreRight,
                            Anchor = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Padding = new MarginPadding
                                            {
                                                Bottom = 16,
                                                Top = 56,
                                            },
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(2),
                                                    Children = new Drawable[]
                                                    {
                                                        loadBtnOverlayShow = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Regular.FolderOpen,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.LoadVideo,
                                                            Hotkey = new Hotkey(GlobalAction.OpenLoadVideo),
                                                            RoundCorner = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 8, NekoPlayerApp.UI_CORNER_RADIUS, 8),
                                                        },
                                                        settingsOverlayShowBtn = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.Cog,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Settings,
                                                            Hotkey = new Hotkey(GlobalAction.OpenSettings),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        commentOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = false },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Regular.CommentAlt,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.CommentsWithoutCount,
                                                            Hotkey = new Hotkey(GlobalAction.OpenComments),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        searchOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.Search,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Search,
                                                            Hotkey = new Hotkey(GlobalAction.OpenSearch),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        reportOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = false },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.Flag,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Report,
                                                            Hotkey = new Hotkey(GlobalAction.ReportAbuse),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        playlistOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.List,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Playlists,
                                                            Hotkey = new Hotkey(GlobalAction.OpenPlaylist),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        myPlaylistsOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = false },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.List,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.MyPlaylists,
                                                            Hotkey = new Hotkey(GlobalAction.OpenMyPlaylists),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                            Action = () =>
                                                            {
                                                                hideOverlays();
                                                                showOverlayContainer(myPlaylistsOverlay);
                                                            }
                                                        },
                                                        audioEffectsOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.AudioEffects,
                                                            Hotkey = new Hotkey(GlobalAction.OpenAudioEffects),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        saveVideoOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = false },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Regular.Bookmark,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Save,
                                                            Hotkey = new Hotkey(GlobalAction.SaveVideoToPlaylist),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        newPlaylistOpenButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = false },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.Bookmark,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.AddNewPlaylist,
                                                            Hotkey = new Hotkey(GlobalAction.AddPlaylistKey),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                            Action = () =>
                                                            {
                                                                hideOverlays();
                                                                showOverlayContainer(addPlaylistOverlay);
                                                            },
                                                        },
                                                        new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.Bell,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Notifications,
                                                            Hotkey = new Hotkey(GlobalAction.Notifications),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                            Action = () =>
                                                            {
                                                                hideOverlays();
                                                                notificationOverlay.OpenOverlay();
                                                            },
                                                        },
                                                        new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.SignOutAlt,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Exit,
                                                            Hotkey = new Hotkey(GlobalAction.QuitApp),
                                                            RoundCorner = new CornersInfo(8, NekoPlayerApp.UI_CORNER_RADIUS, 8, NekoPlayerApp.UI_CORNER_RADIUS),
                                                            Action = () =>
                                                            {
                                                                overlayHideSample.Volume.Value = 0;
                                                                hideOverlays();
                                                                game.AttemptExit();
                                                            },
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                                    Height = (56 + 20),
                                },
                                new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.TopRight,
                                    Anchor = Anchor.TopRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding(14),
                                    Icon = FontAwesome.Solid.Times,
                                    BackgroundColour = overlayColourProvider.Background4,
                                    Action = () =>
                                    {
                                        hideOverlayContainer(menuOverlay);
                                    }
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    Text = NekoPlayerStrings.Menu,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        exitOptions = new SideOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 400,
                            RelativeSizeAxes = Axes.Y,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, 0),
                            Masking = true,
                            Origin = Anchor.CentreRight,
                            Anchor = Anchor.CentreRight,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopLeft,
                                    Anchor = Anchor.TopLeft,
                                    Text = NekoPlayerStrings.ExitOptions,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 16,
                                        Bottom = 16,
                                        Top = 56,
                                    },
                                    Children = new Drawable[] {
                                        new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Vertical,
                                                    Spacing = new Vector2(4),
                                                    Children = new Drawable[]
                                                    {
                                                        new MenuButtonItem
                                                        {
                                                            Enabled = { Value = true },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Solid.SignOutAlt,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = NekoPlayerStrings.Exit,
                                                            Action = () =>
                                                            {
                                                                hideOverlays();
                                                                game.AttemptExit();
                                                            },
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        myChannelDialog = new BottomOverlayContainer
                        {
                            Width = 450,
                            Height = 185,
                            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, 0, NekoPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = NekoPlayerStrings.GoogleAccount,
                                    Margin = new MarginPadding(16),
                                    Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                youtubeChannelMetadataDisplay2 = new YouTubeChannelMetadataDisplay
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Margin = new MarginPadding(8),
                                    Size = new Vector2(0.95f, 1f),
                                    Height = 60,
                                    Origin = Anchor.Centre,
                                    Anchor = Anchor.Centre,
                                    AlwaysPresent = true,
                                },
                                editChannelButton = new IconButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.CentreRight,
                                    Anchor = Anchor.CentreRight,
                                    Size = new Vector2(35, 35),
                                    IconScale = new Vector2(0.8f),
                                    Margin = new MarginPadding() { Right = 24 },
                                    Icon = FontAwesome.Solid.Edit,
                                    BackgroundColour = overlayColourProvider.Background3,
                                },
                                logoutButton = new RoundedAltButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.BottomLeft,
                                    Anchor = Anchor.BottomLeft,
                                    Text = NekoPlayerStrings.Logout,
                                    Size = new Vector2(200, 40),
                                    Margin = new MarginPadding(16),
                                    Action = () =>
                                    {
                                        if (googleOAuth2.SignedIn.Value)
                                        {
                                            hideOverlays();
                                            Task.Run(() => googleOAuth2.SignOut());
                                        }
                                    },
                                },
                                viewChannelButton = new RoundedButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.BottomRight,
                                    Anchor = Anchor.BottomRight,
                                    Text = NekoPlayerStrings.ViewChannel,
                                    Size = new Vector2(200, 40),
                                    Margin = new MarginPadding(16),
                                },
                            }
                        },
                    }
                }
            };
            #endregion

            videoLoadingProgressBase.Hide();
            videoLoadingProgressSpinner.Show();

            prevVideoButton.SetCornerRadius(new CornersInfo(15, 15, NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f));
            nextVideoButton.SetCornerRadius(new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f, 15, 15));

            repeatButton.SetCornerRadius(new CornersInfo(15, 15, NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f));
            pinButton.SetCornerRadius(new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f, 15, 15));
            playPause.SetEnabledValue2(true);
            captionButton.SetEnabledValue2(true);
            videoSettingsButton.SetEnabledValue2(true);
            playlistButton.SetEnabledValue2(true);
            quickCommentOpenButton.SetEnabledValue2(true);

            quickLikeButton.SetCornerRadius(new CornersInfo(15, 15, NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f));
            quickDislikeButton.SetCornerRadius(new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS / 3f, NekoPlayerApp.UI_CORNER_RADIUS / 3f, 15, 15));

            thumbnailContainer.BlurTo(Vector2.Divide(new Vector2(10, 10), 1));

            RegisterOverlayContainer(loadVideoContainer);
            overlayFadeContainer.Hide();
            RegisterOverlayContainer(settingsContainer);
            RegisterOverlayContainer(videoDescriptionContainer);
            RegisterOverlayContainer(commentsContainer);
            RegisterOverlayContainer(searchContainer);
            RegisterOverlayContainer(reportAbuseOverlay);
            RegisterOverlayContainer(playlistOverlay);
            RegisterOverlayContainer(loadPlaylistContainer);
            RegisterOverlayContainer(audioEffectsOverlay);
            RegisterOverlayContainer(unsubscribeDialog);
            RegisterOverlayContainer(videoSaveLocationOverlay);
            RegisterOverlayContainer(addPlaylistOverlay);
            RegisterOverlayContainer(menuOverlay);
            RegisterOverlayContainer(myChannelDialog);
            RegisterOverlayContainer(myPlaylistsOverlay);
            RegisterOverlayContainer(exitOptions);
            RegisterOverlayContainer(editPlaylistOverlay);

            DistortionVolume.BindValueChanged(value =>
            {
                if (value.NewValue > 0.5f)
                {
                    DistortionVolumeSlider.ValueTextColour = Color4.Red;
                    DistortionVolumeNotice.Value = new SettingsNote.Data(NekoPlayerStrings.DistortionWarning, SettingsNote.Type.Warning);
                }
                else
                {
                    DistortionVolumeSlider.ValueTextColour = Color4.White;
                    DistortionVolumeNotice.Value = null;
                }
            }, true);

            ReleaseStream.BindValueChanged(async _ => await checkForUpdates());

            captionEnabled.Disabled = true;

            menuOverlayShow.ClickAction = _ =>
            {
                showOverlayContainer(menuOverlay);
            };

            videoMetadataDisplayAlignment.BindValueChanged(v =>
            {
                SetVideoMetadataDisplayAlignment(v.NewValue);
            }, true);

            signedIn.BindValueChanged(loginBool =>
            {
                if (loginBool.NewValue)
                {
                    GetReportReasons();

                    localeBindable.BindValueChanged(locale =>
                    {
                        Task.Run(async () =>
                        {
                            GetReportReasons();
                        });
                    });

                    if (videoId != null)
                        Task.Run(async () => updateVideoMetadata(videoId));

                    #region playlists

                    Schedule(() => myPlaylistsOpenButton.Enabled.Value = true);

                    fetchMyPlaylists();
                    #endregion

                    Schedule(() => commentSendButton.Enabled.Value = true);
                    Schedule(() => newPlaylistOpenButton.Enabled.Value = true);
                    Channel wth = api.GetMineChannel();

                    //login.Text = NekoPlayerStrings.SignedIn(api.GetLocalizedChannelTitle(wth, true));
                    settingsContainer.UpdateLoginStateText(NekoPlayerStrings.SignedIn(api.GetLocalizedChannelTitle(wth, true)));

                    youtubeChannelMetadataDisplay2.UpdateUser(wth);

                    viewChannelButton.Action = () =>
                    {
                        if (googleOAuth2.SignedIn.Value)
                        {
                            hideOverlays();

                            if (wth != null)
                                app.Host.OpenUrlExternally($"https://www.youtube.com/channel/{wth.Id}");
                        }
                    };

                    editChannelButton.Action = () =>
                    {
                        if (googleOAuth2.SignedIn.Value)
                        {
                            hideOverlays();

                            if (wth != null)
                                app.Host.OpenUrlExternally($"https://studio.youtube.com/channel/{wth.Id}/editing/profile");
                        }
                    };

                    if (api.TryToGetMineChannel() != null)
                    {
                        commentTextBox.PlaceholderText = NekoPlayerStrings.CommentWith;
                        commentTextBox.RefreshChannelProfile(api.GetMineChannel());
                    }

                    Schedule(() => settingsContainer.UpdateLoginState());

                    GetProfileImagePalette(api.GetMineChannel());
                }
                else
                {
                    if (videoId != null)
                        Task.Run(async () => updateVideoMetadata(videoId));

                    Schedule(() => commentSendButton.Enabled.Value = false);
                    settingsContainer.UpdateLoginStateText(NekoPlayerStrings.SignedOut);
                    Schedule(() => saveVideoOpenButton.Enabled.Value = false);
                    Schedule(() => reportOpenButton.Enabled.Value = false);
                    Schedule(() => newPlaylistOpenButton.Enabled.Value = false);
                    Schedule(() => myPlaylistsOpenButton.Enabled.Value = false);
                    Schedule(() => quickLikeButton.Enabled.Value = false);
                    Schedule(() => quickDislikeButton.Enabled.Value = false);

                    foreach (var item in myPlaylistItemsView.Children)
                    {
                        Schedule(() => item.Expire());
                    }

                    commentTextBox.PlaceholderText = NekoPlayerStrings.LoginToComment;
                    Schedule(() => settingsContainer.UpdateLoginState());
                }
            }, true);
            /*
            if (googleOAuth2.SignedIn.Value)
            {
                login.Text = "Signed in";
            }
            else
            {
                login.Text = "Not logged in";
            }
            */

            reportReason.Current.BindValueChanged(value =>
            {
                try
                {
                    if (value.NewValue.ContainsSecondaryReasons == true)
                    {
                        reportSubReason.Show();
                        reportSubReason.Items = value.NewValue.SecondaryReasons;
                        reportSubReason.Current.Value = value.NewValue.SecondaryReasons[0];
                    }
                    else
                    {
                        reportSubReason.Hide();
                    }
                }
                catch
                {
                    reportSubReason.Hide();
                }
            });

            commentsDisabled = true;

            searchButton.BackgroundColour = commentSendButton.BackgroundColour = overlayColourProvider.Background3;

            oauth_note.Value = new SettingsNote.Data(NekoPlayerStrings.OAuthNote, SettingsNote.Type.Informational);

            playlistName.Text = NekoPlayerStrings.PlaylistNotLoaded;
            playlistAuthor.Text = NekoPlayerStrings.PlaylistNotLoadedDesc;

            settingsContainer.VideoQualitySettings.Current.BindValueChanged(quality =>
            {
                if (currentVideoSource != null && isVideoLoading == false)
                {
                    Task.Run(async () =>
                    {
                        Schedule(async () =>
                        {
                            SetVideoSource(videoId, true, LoadType.VideoOnly);
                        });
                    });
                }
            });

            settingsContainer.AudioQualitySettings.Current.BindValueChanged(quality =>
            {
                if (currentVideoSource != null && isVideoLoading == false)
                {
                    Task.Run(async () =>
                    {
                        Schedule(async () =>
                        {
                            SetVideoSource(videoId, true, LoadType.AudioOnly);
                        });
                    });
                }
            });

            videoVolume.BindValueChanged(volume =>
            {
                this.TransformBindableTo(volumeTextRolling, volume.NewValue, 400, Easing.OutQuint);
                if (volume.NewValue > 0.5)
                {
                    volumeIcon.Icon = FontAwesome.Solid.VolumeUp;
                }
                else if (volume.NewValue >= 0.01)
                {
                    volumeIcon.Icon = FontAwesome.Solid.VolumeDown;
                }
                else
                {
                    volumeIcon.Icon = FontAwesome.Solid.VolumeMute;
                }
            }, true);

            captionEnabled.BindValueChanged(enabled =>
            {
                captionButton.SetEnabledValue2(!enabled.NewValue);
                captionButton.IconObject.FadeColour(enabled.NewValue ? bgColor : accentColor, 250, Easing.OutQuint);
                captionButton.Icon = enabled.NewValue ? FontAwesome.Solid.ClosedCaptioning : FontAwesome.Regular.ClosedCaptioning;
            }, true);

            alwaysUseOriginalAudio.BindValueChanged(enabled =>
            {
                if (currentVideoSource != null && isVideoLoading == false)
                {
                    Task.Run(async () =>
                    {
                        Schedule(async () =>
                        {
                            SetVideoSource(videoId, true, LoadType.AudioOnly);
                        });
                    });
                }
            }, true);

            adjustPitch.BindValueChanged(value =>
            {
                currentVideoSource?.UpdatePreservePitch(value.NewValue);
            });

            audioLanguage.BindValueChanged(_ =>
            {
                if (currentVideoSource != null && isVideoLoading == false)
                {
                    Task.Run(async () =>
                    {
                        Schedule(async () =>
                        {
                            SetVideoSource(videoId, true, LoadType.AudioOnly);
                        });
                    });
                }
            });

            settingsContainer.CaptionLangDropdown.Current.BindValueChanged(lang =>
            {
                if (currentVideoSource != null)
                {
                    if (captionEnabled.Value)
                    {
                        Task.Run(async () =>
                        {
                            srv3Contents = string.Empty;
                            var trackManifest = await game.YouTubeClient.Videos.ClosedCaptions.GetManifestAsync(videoUrl);

                            var trackLists = trackManifest.Tracks;

                            var trackInfo = trackLists.Where(track => track.Language.Name == lang.NewValue.Name).First();

                            ClosedCaptionTrack captionTrack = null;

                            if (trackInfo != null)
                            {
                                Schedule(() =>
                                {
                                    ToastBase toast = new ToastWithIcon(NekoPlayerStrings.CaptionLanguage, lang.NewValue.Name, FontAwesome.Solid.ClosedCaptioning);

                                    onScreenDisplay.Display(toast);
                                });

                                captionTrack = await game.YouTubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                                if (File.Exists(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{videoId}") + @$"/{videoId}.{trackInfo.Language.Code}.srv3"))
                                    srv3Contents = File.ReadAllText(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{videoId}") + @$"/{videoId}.{trackInfo.Language.Code}.srv3");
                            }

                            currentVideoSource.UpdateCaptionTrack(captionTrack, srv3Contents);
                        });
                    }
                    else
                    {
                        srv3Contents = string.Empty;
                        currentVideoSource.UpdateCaptionTrack(null, srv3Contents);
                    }
                }
            });

            advancedCaptions.BindValueChanged(enabled =>
            {
                if (currentVideoSource != null)
                {
                    if (captionEnabled.Value)
                    {
                        Task.Run(async () =>
                        {
                            srv3Contents = string.Empty;
                            var trackManifest = await game.YouTubeClient.Videos.ClosedCaptions.GetManifestAsync(videoUrl);

                            string preferedLang = string.Empty;

                            if (settingsContainer.CaptionLangDropdown.Current.Value != null)
                            {
                                preferedLang = settingsContainer.CaptionLangDropdown.Current.Value.Hl.ToString();
                            }
                            else
                            {
                                preferedLang = CultureInfo.CurrentCulture.Name;
                            }

                            settingsContainer.CaptionLangDropdown.Current.Value = settingsContainer.CaptionLangDropdown.Items.Where(lang => lang.Hl.Contains(preferedLang)).First();

                            var trackLists = trackManifest.Tracks;

                            var trackInfo = trackLists.Where(track => track.Language.Code.Contains(preferedLang)).First();

                            ClosedCaptionTrack captionTrack = null;

                            if (captionEnabled.Value)
                            {
                                Schedule(() =>
                                {
                                    try
                                    {
                                        ToastBase toast = new ToastWithIcon(NekoPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name, FontAwesome.Solid.ClosedCaptioning);
                                        onScreenDisplay.Display(toast);
                                    }
                                    catch (Exception e)
                                    {
                                        Logger.Error(e, e.GetDescription());
                                    }
                                });

                                captionTrack = await game.YouTubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                                if (File.Exists(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{videoId}") + @$"/{videoId}.{trackInfo.Language.Code}.srv3"))
                                    srv3Contents = File.ReadAllText(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{videoId}") + @$"/{videoId}.{trackInfo.Language.Code}.srv3");
                            }

                            currentVideoSource.UpdateCaptionTrack(captionTrack, srv3Contents);
                        });
                    }
                    else
                    {
                        srv3Contents = string.Empty;
                        currentVideoSource.UpdateCaptionTrack(null, srv3Contents);
                    }
                }
            });

            captionEnabled.BindValueChanged(enabled =>
            {
                if (currentVideoSource != null)
                {
                    if (captionEnabled.Value)
                    {
                        Task.Run(async () =>
                        {
                            srv3Contents = string.Empty;
                            var trackManifest = await game.YouTubeClient.Videos.ClosedCaptions.GetManifestAsync(videoUrl);

                            string preferedLang = string.Empty;

                            if (settingsContainer.CaptionLangDropdown.Current.Value != null)
                            {
                                preferedLang = settingsContainer.CaptionLangDropdown.Current.Value.Hl.ToString();
                            }
                            else
                            {
                                preferedLang = CultureInfo.CurrentCulture.Name;
                            }

                            settingsContainer.CaptionLangDropdown.Current.Value = settingsContainer.CaptionLangDropdown.Items.Where(lang => lang.Hl.Contains(preferedLang)).First();

                            var trackLists = trackManifest.Tracks;

                            var trackInfo = trackLists.Where(track => track.Language.Code.Contains(preferedLang)).First();

                            ClosedCaptionTrack captionTrack = null;

                            if (enabled.NewValue)
                            {
                                Schedule(() =>
                                {
                                    try
                                    {
                                        ToastBase toast = new ToastWithIcon(NekoPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name, FontAwesome.Solid.ClosedCaptioning);
                                        onScreenDisplay.Display(toast);
                                    }
                                    catch (Exception e)
                                    {
                                        Logger.Error(e, e.GetDescription());
                                    }
                                });

                                captionTrack = await game.YouTubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                                if (File.Exists(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{videoId}") + @$"/{videoId}.{trackInfo.Language.Code}.srv3"))
                                    srv3Contents = File.ReadAllText(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{videoId}") + @$"/{videoId}.{trackInfo.Language.Code}.srv3");
                            }

                            currentVideoSource.UpdateCaptionTrack(captionTrack, srv3Contents);
                        });
                    }
                    else
                    {
                        srv3Contents = string.Empty;
                        currentVideoSource.UpdateCaptionTrack(null, srv3Contents);
                    }
                }
            });

            idleTracker.IsIdle.BindValueChanged(idle =>
            {
                if (idle.NewValue == true)
                {
                    if (videoPlaying.Value)
                    {
                        hideControls();
                    }
                }
                else
                {
                    showControls();
                }
            }, true);

            playbackSpeed.BindValueChanged(speed =>
            {
                this.TransformBindableTo(speedTextRolling, speed.NewValue, 400, Easing.OutQuint);
            }, true);

            speedTextRolling.BindValueChanged(speed =>
            {
                double intValue = speed.NewValue;
                speedText.Text = $@"{intValue:0.##}x";
            }, true);

            volumeTextRolling.BindValueChanged(volume =>
            {
                int intValue = (int)Math.Round(volume.NewValue * 100);
                volumeText.Text = $"{intValue}%";
            }, true);

            reverbEnabled.BindValueChanged(_ =>
            {
                reverbSettings.ClearTransforms();
                reverbSettings.AutoSizeDuration = 400;
                reverbSettings.AutoSizeEasing = Easing.OutQuint;

                updateAudioEffectsVisibility();
            });

            rotateEnabled.BindValueChanged(_ =>
            {
                rotateSettings.ClearTransforms();
                rotateSettings.AutoSizeDuration = 400;
                rotateSettings.AutoSizeEasing = Easing.OutQuint;

                updateAudioEffectsVisibility();
            });

            echoEnabled.BindValueChanged(_ =>
            {
                echoSettings.ClearTransforms();
                echoSettings.AutoSizeDuration = 400;
                echoSettings.AutoSizeEasing = Easing.OutQuint;

                updateAudioEffectsVisibility();
            });

            distortionEnabled.BindValueChanged(_ =>
            {
                distortionSettings.ClearTransforms();
                distortionSettings.AutoSizeDuration = 400;
                distortionSettings.AutoSizeEasing = Easing.OutQuint;

                updateAudioEffectsVisibility();
            });

            karaokeEnabled.BindValueChanged(_ =>
            {
                karaokeSettings.ClearTransforms();
                karaokeSettings.AutoSizeDuration = 400;
                karaokeSettings.AutoSizeEasing = Easing.OutQuint;

                updateAudioEffectsVisibility();
            });

            chorusEnabled.BindValueChanged(_ =>
            {
                chorusSettings.ClearTransforms();
                chorusSettings.AutoSizeDuration = 400;
                chorusSettings.AutoSizeEasing = Easing.OutQuint;

                updateAudioEffectsVisibility();
            });

            eightBitEnabled.BindValueChanged(_ =>
            {
                eightBitSettings.ClearTransforms();
                eightBitSettings.AutoSizeDuration = 400;
                eightBitSettings.AutoSizeEasing = Easing.OutQuint;

                updateAudioEffectsVisibility();
            });
            updateAudioEffectsVisibility();

            videoProgress.BindValueChanged(seek =>
            {
                if (seekbar.IsDragged)
                {
                    currentVideoSource?.SeekTo(seek.NewValue * 1000);
                }
            });

            uiVisible.BindValueChanged(visible =>
            {
                Schedule(() =>
                {
                    if (visible.NewValue)
                    {
                        userInterfaceContainer.Show();
                    }
                    else
                    {
                        userInterfaceContainer.Hide();
                    }
                });
            }, true);

            void updateAudioEffectsVisibility()
            {
                try
                {
                    //reverb
                    if (reverbEnabled.Value == false)
                        reverbSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    reverbSettings.AutoSizeAxes = reverbEnabled.Value != false ? Axes.Y : Axes.None;

                    //rotate
                    if (rotateEnabled.Value == false)
                        rotateSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    rotateSettings.AutoSizeAxes = rotateEnabled.Value != false ? Axes.Y : Axes.None;

                    //echo
                    if (echoEnabled.Value == false)
                        echoSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    echoSettings.AutoSizeAxes = echoEnabled.Value != false ? Axes.Y : Axes.None;

                    //distortion
                    if (distortionEnabled.Value == false)
                        distortionSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    distortionSettings.AutoSizeAxes = distortionEnabled.Value != false ? Axes.Y : Axes.None;

                    //distortion
                    if (karaokeEnabled.Value == false)
                        karaokeSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    karaokeSettings.AutoSizeAxes = karaokeEnabled.Value != false ? Axes.Y : Axes.None;

                    //chorus
                    if (chorusEnabled.Value == false)
                        chorusSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    chorusSettings.AutoSizeAxes = chorusEnabled.Value != false ? Axes.Y : Axes.None;

                    //8-bit
                    if (eightBitEnabled.Value == false)
                        eightBitSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    eightBitSettings.AutoSizeAxes = eightBitEnabled.Value != false ? Axes.Y : Axes.None;
                }
                catch
                {
                }
            }
        }
    }
}

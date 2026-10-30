// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Shaders;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.Sprites.Odometer;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Graphics.UserInterfaceV3;
using AXPlayer.App.Input;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Overlays.Containers;
using AXPlayer.App.Overlays.OSD;
using AXPlayer.App.Overlays.Volume;
using AXPlayer.App.Updater;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osuTK;
using osuTK.Graphics;
using YoutubeExplode.Videos.ClosedCaptions;
using Container = osu.Framework.Graphics.Containers.Container;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        [BackgroundDependencyLoader]
        private void load(ISampleStore sampleStore, FrameworkConfigManager config, AXPlayerConfigManager appConfig, GameHost host, Storage storage, OverlayColourProvider overlayColourProvider, TextureStore textures, FrameworkDebugConfigManager debugConfig)
        {
            AlwaysPresent = true;
            speedTextRolling = new Bindable<double>(1);
            volumeTextRolling = new Bindable<double>(1);
            appliedEffects.Value = new List<InternalShader>();
            window = host.Window;

            app.RegisterMessage(this);

            videoDim = appConfig.GetBindable<double>(AXPlayerSetting.VideoDimLevel);

            videoVolume = config.GetBindable<double>(FrameworkSetting.VolumeMusic);

            showVideoMetadataOnWindowTitle = appConfig.GetBindable<bool>(AXPlayerSetting.ShowVideoMetadataOnWindowTitle);

            uiVisible = screenshotManager.CursorVisibility.GetBoundCopy();
            signedIn = googleOAuth2.SignedIn.GetBoundCopy();

            isAnyOverlayOpen = sessionStatics.GetBindable<bool>(Static.IsAnyOverlayOpen);
            videoPlaying = sessionStatics.GetBindable<bool>(Static.IsVideoPlaying);
            trayIconVisible = sessionStatics.GetBindable<bool>(Static.WindowIsTray);
            ReleaseStream = appConfig.GetBindable<ReleaseStream>(AXPlayerSetting.ReleaseStream);

            playOverlaySFX = appConfig.GetBindable<bool>(AXPlayerSetting.PlayOverlaySFX);
            overlaySFXType = appConfig.GetBindable<SFXType>(AXPlayerSetting.OverlaySFXType);

            captionBGOpacity = appConfig.GetBindable<float>(AXPlayerSetting.CaptionBGOpacity);

            uiLanguage = app.CurrentLanguage.GetBoundCopy();
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode);
            commentsSort = appConfig.GetBindable<CommentsSortCriteria>(AXPlayerSetting.CommentsSortCriteria);
            searchSort = appConfig.GetBindable<SearchSortCriteria>(AXPlayerSetting.SearchSortCriteria);
            resetPlaybackSpeedWhenLoadingAVideo = appConfig.GetBindable<bool>(AXPlayerSetting.ResetPlaybackSpeedWhenLoadingAVideo);

            reverbEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled);
            rotateEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.RotateEnabled);
            echoEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EchoEnabled);
            distortionEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.DistortionEnabled);
            karaokeEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.KaraokeEnabled);
            chorusEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ChorusEnabled);
            eightBitEnabled = audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled);

            DistortionVolume = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.DistortionVolume);

            videoMetadataDisplayAlignment = appConfig.GetBindable<VideoMetadataDisplayAlignment>(AXPlayerSetting.VideoMetadataDisplayAlignment);

            scalingMode = appConfig.GetBindable<ScalingMode>(AXPlayerSetting.Scaling);
            scalingSizeX = appConfig.GetBindable<float>(AXPlayerSetting.ScalingSizeX);
            scalingSizeY = appConfig.GetBindable<float>(AXPlayerSetting.ScalingSizeY);
            scalingPositionX = appConfig.GetBindable<float>(AXPlayerSetting.ScalingPositionX);
            scalingPositionY = appConfig.GetBindable<float>(AXPlayerSetting.ScalingPositionY);
            scalingBackgroundDim = appConfig.GetBindable<float>(AXPlayerSetting.ScalingBackgroundDim);
            alwaysUseOriginalAudio = appConfig.GetBindable<bool>(AXPlayerSetting.AlwaysUseOriginalAudio);
            discordRichPresence = appConfig.GetBindable<DiscordRichPresenceMode>(AXPlayerSetting.DiscordRichPresence);
            closeButtonAction = appConfig.GetBindable<CloseButtonAction>(AXPlayerSetting.CloseButtonAction);
            colourSchemeBindable = appConfig.GetBindable<OverlayColourScheme>(AXPlayerSetting.ColourScheme);
            profileImageShape = appConfig.GetBindable<ProfileImageShape>(AXPlayerSetting.ProfileImageShape);

            captionEnabled = appConfig.GetBindable<bool>(AXPlayerSetting.CaptionEnabled);

            localeBindable = config.GetBindable<string>(FrameworkSetting.Locale);
            fpsDisplay = appConfig.GetBindable<bool>(AXPlayerSetting.ShowFpsDisplay);
            use_sdl3 = config.GetBindable<bool>(FrameworkSetting.UseExperimentalSDL3);
            adjustPitch = appConfig.GetBindable<bool>(AXPlayerSetting.AdjustPitchOnSpeedChange);
            audioQuality = appConfig.GetBindable<Config.AudioQuality>(AXPlayerSetting.AudioQuality);
            videoQuality = appConfig.GetBindable<Config.VideoQuality>(AXPlayerSetting.VideoQuality);
            audioLanguage = appConfig.GetBindable<Localisation.Language>(AXPlayerSetting.AudioLanguage);
            hardwareVideoDecoder = config.GetBindable<HardwareVideoDecoder>(FrameworkSetting.HardwareVideoDecoder);
            cursorInWindow = host.Window?.CursorInWindow.GetBoundCopy();
            windowMode = config.GetBindable<WindowMode>(FrameworkSetting.WindowMode);
            captionLanguage = appConfig.GetBindable<ClosedCaptionLanguage>(AXPlayerSetting.ClosedCaptionLanguage);
            windowedPositionX = config.GetBindable<double>(FrameworkSetting.WindowedPositionX);
            windowedPositionY = config.GetBindable<double>(FrameworkSetting.WindowedPositionY);
            updateInfomationText = game.UpdateManagerVersionText.GetBoundCopy();
            updateButtonEnabled = game.UpdateButtonEnabled.GetBoundCopy();

            ui_font = appConfig.GetBindable<UIFont>(AXPlayerSetting.UIFont);
            caption_font = appConfig.GetBindable<CaptionFonts>(AXPlayerSetting.CaptionFont);

            aspectRatioMethod = appConfig.GetBindable<AspectRatioMethod>(AXPlayerSetting.AspectRatioMethod);

            advancedCaptions = appConfig.GetBindable<bool>(AXPlayerSetting.UseNewSubtitlesFeature);

            accentColor = overlayColourProvider1.Content2;
            bgColor = overlayColourProvider1.Background3;

            overlaySFXType.BindValueChanged(sfx =>
            {
                refreshSFX();
            }, true);

            use_sdl3.BindValueChanged(_ =>
            {
                restartDialogOverlay.OpenDialog();
            });

            colourSchemeBindable.BindValueChanged(_ =>
            {
                restartDialogOverlay.OpenDialog();
            });

            ui_font.BindValueChanged(_ =>
            {
                restartDialogOverlay.OpenDialog();
            });

            LoadComponentAsync(settingsContainer = new SettingsContainer
            {
                OAuthSignInAction = () =>
                {
                    if (!googleOAuth2.SignedIn.Value)
                    {
                        Schedule(() =>
                        {
                            popupDialogOverlay.ShowDialog(
                                    AXPlayerStrings.GoogleAccount,
                                    AXPlayerStrings.WaitingForAuthentication,
                                    AXPlayerStrings.Yes,
                                    AXPlayerStrings.Cancel,
                                    showActionButtons: false);

                        });
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
                        host.OpenUrlExternally(@"https://github.com/ZeroMayo/AX-Player/releases");
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
                userInterfaceContainer = new PopoverContainer
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
                        muteStateOverlay = new SmoothFadeInOutContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Masking = true,
                            Padding = new MarginPadding(8),
                            Children = new Drawable[]
                            {
                                new CircularContainer
                                {
                                    Width = 50,
                                    Height = 50,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Masking = true,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0.7f,
                                        Colour = Color4.Black,
                                    }
                                },
                                new SpriteIcon
                                {
                                    Width = 24,
                                    Height = 24,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Icon = FontAwesome.Solid.VolumeMute,
                                },
                            }
                        },
                        videoLoadingProgressBase = new SmoothFadeInOutContainer
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
                                        videoLoadingProgressSpinner = new AXPlayerLoadingSpinner(true, true)
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
                                                    TooltipText = AXPlayerStrings.Menu,
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
                                            //CornerRadius = AXPlayerApp.UI_CORNER_RADIUS,
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
                                                                    Spacing = new Vector2(3, 0),
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
                                                                                            TooltipText = AXPlayerStrings.PreviousVideo,
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
                                                                                            TooltipText = AXPlayerStrings.Play,
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
                                                                                            TooltipText = AXPlayerStrings.NextVideo,
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
                                                                                            TooltipText = AXPlayerStrings.Repeat,
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
                                                                                            TooltipText = AXPlayerStrings.ClosedCaptions,
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
                                                                                            TooltipText = AXPlayerStrings.VideoSettings,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                ShowSettingsOverlayAtName("Video Settings");
                                                                                            }
                                                                                        },
                                                                                        takeScreenshotButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.Camera,
                                                                                            TooltipText = AXPlayerStrings.TakeScreenshot,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                            ClickAction = _ =>
                                                                                            {
                                                                                                screenshotManager.TakeScreenshot();
                                                                                            }
                                                                                        },
                                                                                        playlistButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = true },
                                                                                            Icon = FontAwesome.Solid.List,
                                                                                            TooltipText = AXPlayerStrings.Playlists,
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
                                                                                            TooltipText = AXPlayerStrings.Comments("0"),
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
                                                                                            TooltipText = AXPlayerStrings.PlayerControlPin,
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
                                                                                            TooltipText = AXPlayerStrings.LikeButton,
                                                                                            IconColour = overlayColourProvider.Content2,
                                                                                            BackgroundColour = overlayColourProvider.Background3,
                                                                                            IconScale = new Vector2(0.85f),
                                                                                        },
                                                                                        quickDislikeButton = new ControlBarIconButton(false)
                                                                                        {
                                                                                            Width = 50,
                                                                                            Enabled = { Value = false },
                                                                                            Icon = FontAwesome.Solid.ThumbsDown,
                                                                                            TooltipText = AXPlayerStrings.DislikeButton,
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
                                                                            Children = new Drawable[]
                                                                            {
                                                                                new FillFlowContainer
                                                                                {
                                                                                    AutoSizeAxes = Axes.Both,
                                                                                    Spacing = new Vector2(3, 0),
                                                                                    Direction = FillDirection.Horizontal,
                                                                                    Children = new Drawable[]
                                                                                    {
                                                                                        playbackSpeedContainer = new PlaybackSpeedContainer
                                                                                        {
                                                                                            AutoSizeAxes = Axes.X,
                                                                                            Height = 30,
                                                                                            Masking = true,
                                                                                            CornerRadius = new CornersInfo(15, 15, AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f),
                                                                                            Current = playbackSpeed.GetBoundCopy(),
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
                                                                                                speedBarBGGradient = new Box
                                                                                                {
                                                                                                    Anchor = Anchor.BottomCentre,
                                                                                                    Origin = Anchor.BottomCentre,
                                                                                                    RelativeSizeAxes = Axes.Both,
                                                                                                    Size = new Vector2(1, 0.75f),
                                                                                                    Alpha = 0.3f,
                                                                                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background3.Darken(1f).Opacity(0f), overlayColourProvider.Background3.Darken(1f)),
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
                                                                                                        speedText = new OdometerTextTicker
                                                                                                        {
                                                                                                            AlwaysPresent = true,
                                                                                                            Colour = overlayColourProvider.Content2,
                                                                                                        },
                                                                                                        speedBarExpandIcon = new SpriteIcon
                                                                                                        {
                                                                                                            Icon = FontAwesome.Solid.ChevronUp,
                                                                                                            Width = 12,
                                                                                                            Height = 12,
                                                                                                            Margin = new MarginPadding
                                                                                                            {
                                                                                                                Top = 9,
                                                                                                            },
                                                                                                            Colour = overlayColourProvider.Content2,
                                                                                                        },
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                        },
                                                                                        videoVolumeControlContainer = new VideoVolumeControlContainer
                                                                                        {
                                                                                            AutoSizeAxes = Axes.X,
                                                                                            Height = 30,
                                                                                            Masking = true,
                                                                                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f, 15, 15),
                                                                                            Current = videoVolume.GetBoundCopy(),
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
                                                                                                volumeBarBGGradient = new Box
                                                                                                {
                                                                                                    Anchor = Anchor.BottomCentre,
                                                                                                    Origin = Anchor.BottomCentre,
                                                                                                    RelativeSizeAxes = Axes.Both,
                                                                                                    Size = new Vector2(1, 0.75f),
                                                                                                    Alpha = 0.3f,
                                                                                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background3.Darken(1f).Opacity(0f), overlayColourProvider.Background3.Darken(1f)),
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
                                                                                                        volumeText = new OdometerTextTicker
                                                                                                        {
                                                                                                            Colour = overlayColourProvider.Content2,
                                                                                                        },
                                                                                                        volumeBarExpandIcon = new SpriteIcon
                                                                                                        {
                                                                                                            Icon = FontAwesome.Solid.ChevronUp,
                                                                                                            Width = 12,
                                                                                                            Height = 12,
                                                                                                            Margin = new MarginPadding
                                                                                                            {
                                                                                                                Top = 9,
                                                                                                            },
                                                                                                            Colour = overlayColourProvider.Content2,
                                                                                                        },
                                                                                                    }
                                                                                                }
                                                                                            }
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
                                                                                timeBGGradient = new Box
                                                                                {
                                                                                    Anchor = Anchor.BottomCentre,
                                                                                    Origin = Anchor.BottomCentre,
                                                                                    RelativeSizeAxes = Axes.Both,
                                                                                    Size = new Vector2(1, 0.75f),
                                                                                    Alpha = 0.3f,
                                                                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background3.Darken(1f).Opacity(0f), overlayColourProvider.Background3.Darken(1f)),
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
                                                                                        timeText = new OdometerTimeDisplay
                                                                                        {
                                                                                            Colour = overlayColourProvider.Content2,
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                    Text = AXPlayerStrings.LoadFromVideoId,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
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
                                    Text = AXPlayerStrings.LoadVideo,
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
                        settingsContainer,
                        videoDescriptionContainer = new BottomOverlayContainer
                        {
                            Size = new Vector2(0.7f),
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                        CornerRadius = AXPlayerApp.UI_CORNER_RADIUS,
                                        Masking = true,
                                        ScrollbarVisible = false,
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                CornerRadius = AXPlayerApp.UI_CORNER_RADIUS,
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
                                                CornerRadius = AXPlayerApp.UI_CORNER_RADIUS,
                                                Spacing = new Vector2(0, 8),
                                                Padding = new MarginPadding(12),
                                                Masking = true,
                                                Children = new Drawable[]
                                                {
                                                    videoInfoDetails = new ProjectYomiSpriteText
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        Font = AXPlayerApp.DefaultFont.With(weight: "Bold"),
                                                        Colour = overlayColourProvider.Content2,
                                                        AlwaysPresent = true,
                                                    },
                                                    videoDescription = new LinkFlowContainer(f => f.Font = AXPlayerApp.DefaultFont)
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
                                                                Text = AXPlayerStrings.Subscribe,
                                                            },
                                                            likeButton = new RoundedButtonContainer
                                                            {
                                                                AutoSizeAxes = Axes.X,
                                                                Height = 32,
                                                                CornerRadius = new CornersInfo(16, 16, AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f),
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
                                                                CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f, 16, 16),
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
                                                                        CornerRadius = AXPlayerApp.UI_CORNER_RADIUS / 1.5f,
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0),
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
                                                            Font = AXPlayerApp.DefaultFont.With(size: 32, weight: "Bold"),
                                                            Text = AXPlayerStrings.NoComments,
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
                                                            Text = AXPlayerStrings.NoCommentsDesc,
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
                                    Text = AXPlayerStrings.Comments("0"),
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
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
                                            PlaceholderText = AXPlayerStrings.LoginToComment,
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

                                                api.SendComment(videoId, commentTextBox.Text);

                                                Scheduler.AddDelayed(() => updateComments(videoId), 2000);

                                                //Schedule(() => onScreenDisplay.Display(toast));
                                                Schedule(() =>
                                                {
                                                    //notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Regular.Comment, Color4.White, AXPlayerStrings.CommentAdded, AXPlayerStrings.General));
                                                    newToastOverlay.ShowToast(AXPlayerStrings.CommentAdded);
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                    Text = AXPlayerStrings.Search,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
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
                                            PlaceholderText = AXPlayerStrings.SearchPlaceholder,
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                                            Text = AXPlayerStrings.WhatsGoingOn,
                                                            Font = AXPlayerApp.DefaultFont.With(size: 27, weight: "Bold"),
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 17, weight: "Regular"))
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Text = AXPlayerStrings.ReportDesc,
                                                            Colour = overlayColourProvider.Background1,
                                                        },
                                                        reportReason = new ReportDropdown
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Caption = AXPlayerStrings.ReportReason,
                                                        },
                                                        reportSubReason = new ReportDropdown
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Caption = AXPlayerStrings.ReportSubReason,
                                                        },
                                                        reportComment = new FormTextBox
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = AXPlayerStrings.Description,
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
                                            Text = AXPlayerStrings.Submit,
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
                                    Text = AXPlayerStrings.Report,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0),
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
                                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS),
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
                                                            f.Font = AXPlayerApp.DefaultFont.With(size: 16, weight: "SemiBold");
                                                            f.Colour = overlayColourProvider.Background1;
                                                        })
                                                        {
                                                            TextAnchor = Anchor.Centre,
                                                            Origin = Anchor.BottomCentre,
                                                            Anchor = Anchor.BottomCentre,
                                                            Text = AXPlayerStrings.PlaylistNotLoadedDesc,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                        },
                                                        playlistName = new ProjectYomiTextFlowContainer(f =>
                                                        {
                                                            f.Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "Bold");
                                                            f.Colour = overlayColourProvider.Content2;
                                                        })
                                                        {
                                                            TextAnchor = Anchor.Centre,
                                                            Origin = Anchor.BottomCentre,
                                                            Anchor = Anchor.BottomCentre,
                                                            Text = AXPlayerStrings.PlaylistNotLoaded,
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
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = AXPlayerStrings.Playlists,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        myPlaylistsOverlay = new SideOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 400,
                            RelativeSizeAxes = Axes.Y,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0),
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
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = AXPlayerStrings.MyPlaylists,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        loadPlaylistContainer = new BottomOverlayContainer
                        {
                            Size = new Vector2(0.7f, 1f),
                            Height = 200,
                            RelativeSizeAxes = Axes.X,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                    Text = AXPlayerStrings.LoadFromPlaylistId,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                loadPlaylistBtn = new ProjectYomiButton
                                {
                                    Enabled = { Value = true },
                                    Origin = Anchor.BottomRight,
                                    Anchor = Anchor.BottomRight,
                                    Text = AXPlayerStrings.LoadPlaylist,
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0),
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
                                                            Caption = AXPlayerStrings.ReverbEffect,
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
                                                                    Caption = AXPlayerStrings.WetMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.StereoWidth,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbStereoWidth),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.HighFreqDamp,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbDamp),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.RoomSize,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbRoomSize),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.RotateParameters_Enabled,
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
                                                                    Caption = AXPlayerStrings.RotateParameters_fRate,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.RotateRate),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.EchoEffect,
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
                                                                    Caption = AXPlayerStrings.DryMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDryMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.EchoWetMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoWetMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.EchoFeedback,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoFeedback),
                                                                    LabelFormat = f => $"{f - 1}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.EchoDelay,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDelay),
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.DistortionEffect,
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
                                                                    Caption = AXPlayerStrings.DistortionVolume,
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
                                                            Caption = AXPlayerStrings.KaraokeMode,
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
                                                                    Caption = AXPlayerStrings.KaraokeVocalVolume,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.KaraokeVocalVolume),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ChorusEffect,
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
                                                                    Caption = AXPlayerStrings.DryMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusDryMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.EchoWetMix,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusWetMix),
                                                                    LabelFormat = f => $"{f - 2}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.EchoFeedback,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusFeedback),
                                                                    LabelFormat = f => $"{f - 1}",
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.ChorusMinSweep,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusMinSweep),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.ChorusMaxSweep,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusMaxSweep),
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.ChorusRate,
                                                                    Current = audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusRate),
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.EightBitEffectEnabled,
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
                                                                    Caption = AXPlayerStrings.BitDepth,
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
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = AXPlayerStrings.AudioEffects,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        unsubscribeDialog = new BottomOverlayContainer
                        {
                            Width = 450,
                            Height = 200,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        youtubeChannelMetadataDisplay = new YouTubeChannelMetadataDisplay
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Margin = new MarginPadding(8),
                                            Size = new Vector2(0.965f, 1f),
                                            Height = 60,
                                            Origin = Anchor.TopCentre,
                                            Anchor = Anchor.TopCentre,
                                            AlwaysPresent = true,
                                        },
                                        new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 20))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 70,
                                            Origin = Anchor.TopCentre,
                                            Anchor = Anchor.TopCentre,
                                            TextAnchor = Anchor.Centre,
                                            AlwaysPresent = true,
                                            Text = AXPlayerStrings.UnsubscribeDesc,
                                            Colour = overlayColourProvider.Content2,
                                        },
                                        new FillFlowContainer
                                        {
                                            Origin = Anchor.TopCentre,
                                            Anchor = Anchor.TopCentre,
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Horizontal,
                                            Children = new Drawable[]
                                            {
                                                declineButton = new RoundedAltButton
                                                {
                                                    Origin = Anchor.TopCentre,
                                                    Anchor = Anchor.TopCentre,
                                                    Enabled = { Value = true },
                                                    Text = AXPlayerStrings.Cancel,
                                                    Size = new Vector2(200, 40),
                                                    Margin = new MarginPadding(4),
                                                },
                                                acceptButton = new RoundedButton
                                                {
                                                    Origin = Anchor.TopCentre,
                                                    Anchor = Anchor.TopCentre,
                                                    Enabled = { Value = true },
                                                    Text = AXPlayerStrings.Yes,
                                                    Size = new Vector2(200, 40),
                                                    BackgroundColour = colours.RedDark,
                                                    Margin = new MarginPadding(4),
                                                },
                                            }
                                        }
                                    }
                                },
                            }
                        },
                        videoSaveLocationOverlay = new BottomOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 450,
                            Height = 210,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                                            Caption = AXPlayerStrings.Playlists,
                                                        },
                                                        new RoundedAltButton
                                                        {
                                                            Enabled = { Value = true },
                                                            Text = AXPlayerStrings.AddNewPlaylist,
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
                                                                    Text = AXPlayerStrings.Cancel,
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
                                                                    Text = AXPlayerStrings.SaveOrRemove,
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
                                    Text = AXPlayerStrings.SaveLocation,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        editPlaylistOverlay = new BottomOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 450,
                            Height = 220,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                                            Caption = AXPlayerStrings.Title,
                                                            PlaceholderText = AXPlayerStrings.TitlePlaceholder,
                                                        },
                                                        editPlaylistPrivacyStatusDropdown = new FormEnumDropdown<PrivacyStatus>
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = AXPlayerStrings.PrivacyStatus,
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
                                                                    Text = AXPlayerStrings.Cancel,
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
                                                                    Text = AXPlayerStrings.Apply,
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
                                    Text = AXPlayerStrings.EditPlaylist,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        addPlaylistOverlay = new BottomOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 450,
                            Height = 220,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
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
                                                            Caption = AXPlayerStrings.Title,
                                                            PlaceholderText = AXPlayerStrings.TitlePlaceholder,
                                                        },
                                                        playlistPrivacyStatusDropdown = new FormEnumDropdown<PrivacyStatus>
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 50,
                                                            Caption = AXPlayerStrings.PrivacyStatus,
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
                                                                    Text = AXPlayerStrings.Cancel,
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
                                                                    Text = AXPlayerStrings.Create,
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
                                    Text = AXPlayerStrings.AddNewPlaylist,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0),
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
                                                            Text = AXPlayerStrings.LoadVideo,
                                                            Hotkey = new Hotkey(GlobalAction.OpenLoadVideo),
                                                            RoundCorner = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 8, AXPlayerApp.UI_CORNER_RADIUS, 8),
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
                                                            Text = AXPlayerStrings.Settings,
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
                                                            Text = AXPlayerStrings.CommentsWithoutCount,
                                                            Hotkey = new Hotkey(GlobalAction.OpenComments),
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                        },
                                                        copyVideoUrlButton = new MenuButtonItem
                                                        {
                                                            Enabled = { Value = false },
                                                            Origin = Anchor.TopRight,
                                                            Anchor = Anchor.TopRight,
                                                            Size = new Vector2(1, 45),
                                                            RelativeSizeAxes = Axes.X,
                                                            Icon = FontAwesome.Regular.Clipboard,
                                                            IconScale = new Vector2(1.2f),
                                                            Text = AXPlayerStrings.CopyVideoURL,
                                                            RoundCorner = new CornersInfo(8, 8, 8, 8),
                                                            Action = () =>
                                                            {
                                                                if (videoUrl != null)
                                                                {
                                                                    clipboard.SetText(videoUrl);

                                                                    /*
                                                                    Toast toast = new Toast(AXPlayerStrings.General, AXPlayerStrings.CopiedToClipboard);

                                                                    onScreenDisplay.Display(toast);
                                                                    */

                                                                    newToastOverlay.ShowToast(AXPlayerStrings.CopiedToClipboard);
                                                                    hideOverlayContainer(menuOverlay);
                                                                }
                                                            },
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
                                                            Text = AXPlayerStrings.Search,
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
                                                            Text = AXPlayerStrings.Report,
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
                                                            Text = AXPlayerStrings.Playlists,
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
                                                            Text = AXPlayerStrings.MyPlaylists,
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
                                                            Text = AXPlayerStrings.AudioEffects,
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
                                                            Text = AXPlayerStrings.Save,
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
                                                            Text = AXPlayerStrings.AddNewPlaylist,
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
                                                            Text = AXPlayerStrings.Notifications,
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
                                                            Text = AXPlayerStrings.Exit,
                                                            Hotkey = new Hotkey(GlobalAction.QuitApp),
                                                            RoundCorner = new CornersInfo(8, AXPlayerApp.UI_CORNER_RADIUS, 8, AXPlayerApp.UI_CORNER_RADIUS),
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
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = AXPlayerStrings.Menu,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                    Colour = overlayColourProvider.Content2,
                                },
                            }
                        },
                        exitOptions = new SideOverlayContainer
                        {
                            Size = new Vector2(1f, 1f),
                            Width = 400,
                            RelativeSizeAxes = Axes.Y,
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0),
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
                                    Text = AXPlayerStrings.ExitOptions,
                                    Margin = new MarginPadding(16),
                                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
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
                                                            Text = AXPlayerStrings.Exit,
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
                            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, 0, AXPlayerApp.UI_CORNER_RADIUS, 0),
                            Masking = true,
                            Origin = Anchor.BottomCentre,
                            Anchor = Anchor.BottomCentre,
                            Children = new Drawable[]
                            {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new ProjectYomiSpriteText
                                        {
                                            Origin = Anchor.TopCentre,
                                            Anchor = Anchor.TopCentre,
                                            Text = AXPlayerStrings.GoogleAccount,
                                            Margin = new MarginPadding(16),
                                            Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                                            Colour = overlayColourProvider.Content2,
                                        },
                                        youtubeChannelMetadataDisplay2 = new YouTubeChannelMetadataDisplay
                                        {
                                            Origin = Anchor.TopCentre,
                                            Anchor = Anchor.TopCentre,
                                            RelativeSizeAxes = Axes.X,
                                            Margin = new MarginPadding(4),
                                            Size = new Vector2(0.95f, 1f),
                                            Height = 60,
                                            AlwaysPresent = true,
                                        },
                                        new FillFlowContainer
                                        {
                                            Origin = Anchor.TopCentre,
                                            Anchor = Anchor.TopCentre,
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Horizontal,
                                            Children = new Drawable[]
                                            {
                                                logoutButton = new RoundedAltButton
                                                {
                                                    Origin = Anchor.TopCentre,
                                                    Anchor = Anchor.TopCentre,
                                                    Enabled = { Value = true },
                                                    Text = AXPlayerStrings.Logout,
                                                    Size = new Vector2(200, 40),
                                                    Margin = new MarginPadding(4),
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
                                                    Origin = Anchor.TopCentre,
                                                    Anchor = Anchor.TopCentre,
                                                    Enabled = { Value = true },
                                                    Text = AXPlayerStrings.ViewChannel,
                                                    Size = new Vector2(200, 40),
                                                    Margin = new MarginPadding(4),
                                                },
                                            }
                                        },
                                    }
                                },
                            }
                        },
                    }
                }
            };
            #endregion

            videoLoadingProgressBase.Hide();
            videoLoadingProgressSpinner.Show();

            prevVideoButton.SetCornerRadius(new CornersInfo(15, 15, AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f));
            nextVideoButton.SetCornerRadius(new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f, 15, 15));

            repeatButton.SetCornerRadius(new CornersInfo(15, 15, AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f));
            pinButton.SetCornerRadius(new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f, 15, 15));
            playPause.SetEnabledValue2(true);
            captionButton.SetEnabledValue2(true);
            videoSettingsButton.SetEnabledValue2(true);
            takeScreenshotButton.SetEnabledValue2(true);
            playlistButton.SetEnabledValue2(true);
            quickCommentOpenButton.SetEnabledValue2(true);

            quickLikeButton.SetCornerRadius(new CornersInfo(15, 15, AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f));
            quickDislikeButton.SetCornerRadius(new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f, 15, 15));

            thumbnailContainer.BlurTo(Vector2.Divide(new Vector2(10, 10), 1));

            DistortionVolume.BindValueChanged(value =>
            {
                if (value.NewValue > 0.5f)
                {
                    DistortionVolumeSlider.ValueTextColour = Color4.Red;
                    DistortionVolumeNotice.Value = new SettingsNote.Data(AXPlayerStrings.DistortionWarning, SettingsNote.Type.Warning);
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
                    Schedule(() =>
                    {
                        if (popupDialogOverlay.IsOpened.Value)
                        {
                            popupDialogOverlay.HideDialog();
                            //host.Window.Flash();
                            host.Window.Raise();
                        }
                    });

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
                    settingsContainer.UpdateLoginStateText(AXPlayerStrings.SignedIn(api.GetLocalizedChannelTitle(wth, true)));

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

                    if (api.TryToGetMineChannel() != null)
                    {
                        commentTextBox.PlaceholderText = AXPlayerStrings.CommentWith;
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
                    settingsContainer.UpdateLoginStateText(AXPlayerStrings.SignedOut);
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

                    commentTextBox.PlaceholderText = AXPlayerStrings.LoginToComment;
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

            oauth_note.Value = new SettingsNote.Data(AXPlayerStrings.OAuthNote, SettingsNote.Type.Informational);

            playlistName.Text = AXPlayerStrings.PlaylistNotLoaded;
            playlistAuthor.Text = AXPlayerStrings.PlaylistNotLoadedDesc;

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
                volumeText.SetText($"{(int)Math.Round(volume.NewValue * 100)}%");
                if (volume.NewValue > 0.5)
                {
                    if (!sessionStatics.Get<bool>(Static.IsControlVisible))
                        muteStateOverlay.Hide();

                    volumeIcon.Icon = FontAwesome.Solid.VolumeUp;
                }
                else if (volume.NewValue >= 0.01)
                {
                    if (!sessionStatics.Get<bool>(Static.IsControlVisible))
                        muteStateOverlay.Hide();

                    volumeIcon.Icon = FontAwesome.Solid.VolumeDown;
                }
                else
                {
                    if (!sessionStatics.Get<bool>(Static.IsControlVisible))
                        muteStateOverlay.Show();

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
                                    ToastBase toast = new Toast(AXPlayerStrings.CaptionLanguage, lang.NewValue.Name);

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
                                        ToastBase toast = new Toast(AXPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name);
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
                                        ToastBase toast = new Toast(AXPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name);
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

            videoPlaying.BindValueChanged(value =>
            {
                updatePresence(discordRichPresence.Value);
            });

            playbackSpeed.BindValueChanged(speed =>
            {
                updatePresence(discordRichPresence.Value);
                this.TransformBindableTo(speedTextRolling, speed.NewValue, 400, Easing.OutQuint);
                speedText.SetText($"{(int)Math.Round(speed.NewValue * 100)}%");
            }, true);

            speedTextRolling.BindValueChanged(speed =>
            {
                double intValue = speed.NewValue;
                //speedText.Text = $@"{intValue:0.##}x";
            }, true);

            uiLanguage.BindValueChanged(_ =>
            {
                Schedule(() =>
                {
                    speedText.SetText($"{(int)Math.Round(playbackSpeed.Value * 100)}%");
                });
            }, true);

            volumeTextRolling.BindValueChanged(volume =>
            {
                int intValue = (int)Math.Round(volume.NewValue * 100);
                //volumeText.Text = $"{intValue}%";
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
                        if (isAnyOverlayOpen.Value)
                            videoContainer.BlurTo(new Vector2(6));

                        videoScalingContainer.Show();
                        userInterfaceContainer.Show();
                    }
                    else
                    {
                        if (isAnyOverlayOpen.Value)
                            videoContainer.BlurTo(new Vector2(0));

                        videoScalingContainer.Hide();
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

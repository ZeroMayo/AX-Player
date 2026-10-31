// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Caption;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Graphics.UserInterfaceV3;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using AXPlayer.App.Utils;
using FFmpeg.AutoGen;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Rendering.LowLatency;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Platform.Windows;
using osu.Framework.Statistics;
using osu.Framework.Testing;
using osuTK;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;

namespace AXPlayer.App.Overlays.Containers
{
    public partial class SettingsContainer : SideOverlayContainer
    {
        private ProjectYomiScrollContainer settingsSections;

        private Bindable<ReleaseStream> configReleaseStream;

        private Bindable<bool> darkModeBindable;

        public void ShowSettingsOverlayAtName(string name)
        {
            // wait for load of sections
            if (!settingsSections.Any())
            {
                Scheduler.Add(() => ShowSettingsOverlayAtName(name));
                return;
            }

            settingsSections.ScrollTo(settingsSections.ChildrenOfType<Drawable>().Where(child => child.Name == name).Single());
        }

        [Resolved]
        private OnScreenDisplay onScreenDisplay { get; set; }
        private AudioDeviceDropdown audioDeviceDropdown = null!;

        private Storage exportStorage = null!;

#nullable enable
        private void exportLogs()
        {
            Schedule(() =>
            {
                exportLogButton.Text = AXPlayerStrings.ExportingLogs;
                exportLogButton.Enabled.Value = false;
            });
            const string archive_filename = "compressed-logs.zip";

            try
            {
                GlobalStatistics.OutputToLog();
                Logger.Flush();

                var logStorage = Logger.Storage;

                using (var outStream = exportStorage.CreateFileSafely(archive_filename))
                using (var zip = ZipArchive.CreateArchive())
                {
                    foreach (string? f in logStorage.GetFiles(string.Empty, "*.log"))
                        FileUtils.AttemptOperation(z => z.AddEntry(f, logStorage.GetStream(f), closeStream: true), zip, throwOnFailure: false);

                    zip.SaveTo(outStream, new ZipWriterOptions(CompressionType.Deflate));
                }
            }
            catch
            {
                // cleanup if export is failed or canceled.
                exportStorage.Delete(archive_filename);
                throw;
            }

            Schedule(() =>
            {
                exportLogButton.Text = AXPlayerStrings.ExportLogs;
                exportLogButton.Enabled.Value = true;
                popupDialogOverlay.ShowDialog(
                        AXPlayerStrings.ExportLogs,
                        AXPlayerStrings.LogsExportFinished,
                        AXPlayerStrings.OpenInFileExplorer,
                        AXPlayerStrings.Close,
                        okPressedAction: () => exportStorage.PresentFileExternally(archive_filename));
                
            });
        }
#nullable disable

        private Bindable<OverlayColourScheme> colourSchemeBindable;
        private Bindable<ProfileImageShape> profileImageShape;
        private Bindable<CloseButtonAction> closeButtonAction;
        private Bindable<DiscordRichPresenceMode> discordRichPresence;
        private Bindable<bool> adjustPitch, advancedSubtitles;
        private Bindable<VideoMetadataDisplayAlignment> videoMetadataDisplayAlignment;
        private Bindable<AspectRatioMethod> aspectRatioMethod;
        private Bindable<bool> fpsDisplay;

        private FormEnumDropdown<ScalingMode> ScreenScaling;

        private FormGoogleOAuthButton login;

        public Action OAuthSignInAction;
        public Action CheckUpdateAction;
        private readonly BindableBool displayDropdownCanBeShown = new BindableBool(true);
        private FillFlowContainer<SettingsItemV2> scalingSettings = null!;

        public SettingsItemV2 VideoQualitySettingsCore, AudioQualitySettingsCore;
        private FormEnumDropdown<LatencyMode>? reflexSetting;

        [Resolved]
        private PopupDialogOverlay popupDialogOverlay { get; set; }

        private void releaseStreamChanged(ValueChangedEvent<ReleaseStream> stream)
        {
            switch (stream.NewValue)
            {
                case ReleaseStream.Stable:
                    configReleaseStream.Value = stream.NewValue;
                    break;

                default:
                    popupDialogOverlay.ShowDialog(
                        AXPlayerStrings.ChangeReleaseStreamConfirmation,
                        AXPlayerStrings.ChangeReleaseStreamConfirmationInfo,
                        AXPlayerStrings.Yes,
                        AXPlayerStrings.Cancel,
                        okPressedAction: () => configReleaseStream.Value = stream.NewValue,
                        cancelPressedAction: () => releaseStreamDropdown.Current.Value = stream.OldValue);
                    break;
            }
        }

        private void onAudioDeviceChanged(string _)
        {
            updateAudioDeviceItems();
        }

        [Resolved]
        private AudioManager audio { get; set; }

        [Resolved]
        private RestartDialogOverlay restartDialogOverlay { get; set; }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (audio.IsNotNull())
            {
                audio.OnNewDevice -= onAudioDeviceChanged;
                audio.OnLostDevice -= onAudioDeviceChanged;
            }
        }

        public void UpdateLoginState()
        {
            login.UpdateLoginState();
        }

        private void updateAudioDeviceItems()
        {
            var deviceItems = new List<string> { string.Empty };
            deviceItems.AddRange(audio.AudioDeviceNames.Select(d => d.Name));

            string preferredDeviceName = audio.AudioDevice.Value;
            if (deviceItems.All(kv => kv != preferredDeviceName))
                deviceItems.Add(preferredDeviceName);

            // The option dropdown for audio device selection lists all audio
            // device names. Dropdowns, however, may not have multiple identical
            // keys. Thus, we remove duplicate audio device names from
            // the dropdown. BASS does not give us a simple mechanism to select
            // specific audio devices in such a case anyways. Such
            // functionality would require involved OS-specific code.
            audioDeviceDropdown.Items = deviceItems
                             // Dropdown doesn't like null items. Somehow we are seeing some arrive here (see https://github.com/ppy/osu/issues/21271)
                             .Where(i => i.IsNotNull())
                             .Distinct()
                             .ToList();
        }

        [Resolved]
        private GameHost host { get; set; } = null!;

        private Bindable<double> videoVolume;

        public void UpdateLoginStateText(LocalisableString text)
        {
            login.Text = text;
        }

        public FormButton CheckForUpdatesButton;

        private SettingsItemV2 checkForUpdatesButtonCore, reflexSettingBase, captionLangOptions, wasapiExperimentalItem, releaseStreamSelectorButtonCore;

        private FormCheckBox wasapiExperimental;
        private Bindable<UsernameDisplayMode> usernameDisplayMode;

        private FormEnumDropdown<ReleaseStream> releaseStreamDropdown;

        private Bindable<UIFont> ui_font;
        private Bindable<CaptionFonts> caption_font;

        private Bindable<SFXType> overlaySFXType;
        private Bindable<bool> playOverlaySFX, updateButtonEnabled;
        private Bindable<HardwareVideoDecoder> hardwareVideoDecoder;
        private Bindable<Config.AudioQuality> audioQuality;
        private Bindable<bool> alwaysUseOriginalAudio, captionEnabled;
        private Bindable<float> captionBGOpacity;

        private PopoverContainer popoverContainer;

        public Action CloseOverlayAction;

        public YouTubeI18nLangDropdown CaptionLangDropdown;

        private IWindow? window;

        private readonly Bindable<Display> currentDisplay = new Bindable<Display>();

        // An example driver name of an ALSA device will look like this: "hw:4,0".
        // For contrast, Pipewire Server and Default devices will have driver names called respectively "pipewire" and "default".
        public const string LINUX_ALSA_DEVICE_DRIVER_PREFIX = "hw:";

        private readonly Bindable<SettingsNote.Data?> alsaExclusiveDeviceNote = new Bindable<SettingsNote.Data?>();
        private readonly Bindable<SettingsNote.Data?> srv3Notice = new Bindable<SettingsNote.Data?>();

        private readonly Bindable<SettingsNote.Data?> wasapiExperimentalNote = new Bindable<SettingsNote.Data?>();

        private void onDeviceSelected(string selectedDevice)
        {
            string? currentDriver = audio.AudioDeviceNames.Where(d => d.Name == selectedDevice).Select(d => d.Driver).FirstOrDefault();
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux && currentDriver.IsNotNull() && currentDriver.StartsWith(LINUX_ALSA_DEVICE_DRIVER_PREFIX, System.StringComparison.Ordinal))
            {
                alsaExclusiveDeviceNote.Value = new SettingsNote.Data(AXPlayerStrings.AlsaExclusiveNotice, SettingsNote.Type.Warning);
            }
            else
            {
                alsaExclusiveDeviceNote.Value = null;
            }
        }

        private void onDisplaysChanged(IEnumerable<Display> displays)
        {
            Scheduler.AddOnce(d =>
            {
                if (!displayDropdown.Items.SequenceEqual(d, DisplayListComparer.DEFAULT))
                    displayDropdown.Items = d;
                updateDisplaySettingsVisibility();
            }, displays);
        }

        private Bindable<WindowMode> windowMode = null!;
        private Bindable<LocalisableString> updateInfomationText;

        private SettingsButtonV2 exportLogButton;

        private Bindable<float> scalingPositionX = null!;
        private Bindable<float> scalingPositionY = null!;
        private Bindable<float> scalingSizeX = null!;
        private Bindable<float> scalingSizeY = null!;
        private FormSliderBar<float> dimSlider = null!;

        private Bindable<float> scalingBackgroundDim = null!;

        public YouTubeQualityDropdown VideoQualitySettings;
        public YouTubeAudioStreamDropdown AudioQualitySettings;
        private Bindable<bool> showVideoMetadataOnWindowTitle;

        private Bindable<ScreenshotFormat> screenshotFormat;

        private void prepareSettingsTabs()
        {
            Drawable[] drawables = new Drawable[]
            {
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Star,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Quick Actions")),
                    TooltipText = AXPlayerStrings.QuickAction,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Cog,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("General Settings")),
                    TooltipText = AXPlayerStrings.General,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.WindowMaximize,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("UI Settings")),
                    TooltipText = AXPlayerStrings.UserInterface,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Bolt,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Graphics Settings")),
                    TooltipText = AXPlayerStrings.Graphics,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Camera,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Screenshot Settings")),
                    TooltipText = AXPlayerStrings.Screenshot,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Video,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Video Settings")),
                    TooltipText = AXPlayerStrings.Video,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Sun,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("VFX Settings")),
                    TooltipText = AXPlayerStrings.VisualEffects,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.ClosedCaptioning,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("CC Settings")),
                    TooltipText = AXPlayerStrings.ClosedCaptions,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.VolumeUp,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Audio Settings")),
                    TooltipText = AXPlayerStrings.Audio,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Bug,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Debug Settings")),
                    TooltipText = AXPlayerStrings.Debug,
                },
                new AXPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.InfoCircle,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("App Info")),
                    TooltipText = AXPlayerStrings.AppInfo,
                },
            };

            settingsTabBar.SetItems(drawables);
        }

        private SettingsItemV2 advancedSubtitlesBase;

        private BindableBool wasapi;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider, TextureStore textures, ProjectYomiColour colours, FrameworkDebugConfigManager debugConfig, FrameworkConfigManager config, Storage storage, AXPlayerConfigManager appConfig)
        {
            wasapi = audio.UseExperimentalWasapi;
            videoVolume = config.GetBindable<double>(FrameworkSetting.VolumeMusic);
            fpsDisplay = appConfig.GetBindable<bool>(AXPlayerSetting.ShowFpsDisplay);
            closeButtonAction = appConfig.GetBindable<CloseButtonAction>(AXPlayerSetting.CloseButtonAction);
            colourSchemeBindable = appConfig.GetBindable<OverlayColourScheme>(AXPlayerSetting.ColourScheme);
            darkModeBindable = appConfig.GetBindable<bool>(AXPlayerSetting.DarkMode);
            profileImageShape = appConfig.GetBindable<ProfileImageShape>(AXPlayerSetting.ProfileImageShape);
            adjustPitch = appConfig.GetBindable<bool>(AXPlayerSetting.AdjustPitchOnSpeedChange);
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode);
            windowMode = config.GetBindable<WindowMode>(FrameworkSetting.WindowMode);
            videoMetadataDisplayAlignment = appConfig.GetBindable<VideoMetadataDisplayAlignment>(AXPlayerSetting.VideoMetadataDisplayAlignment);
            hardwareVideoDecoder = config.GetBindable<HardwareVideoDecoder>(FrameworkSetting.HardwareVideoDecoder);
            sizeFullscreen = config.GetBindable<Size>(FrameworkSetting.SizeFullscreen);
            scalingMode = appConfig.GetBindable<ScalingMode>(AXPlayerSetting.Scaling);
            captionBGOpacity = appConfig.GetBindable<float>(AXPlayerSetting.CaptionBGOpacity);
            scalingSizeX = appConfig.GetBindable<float>(AXPlayerSetting.ScalingSizeX);
            audioQuality = appConfig.GetBindable<Config.AudioQuality>(AXPlayerSetting.AudioQuality);
            scalingSizeY = appConfig.GetBindable<float>(AXPlayerSetting.ScalingSizeY);
            scalingPositionX = appConfig.GetBindable<float>(AXPlayerSetting.ScalingPositionX);
            scalingPositionY = appConfig.GetBindable<float>(AXPlayerSetting.ScalingPositionY);
            scalingBackgroundDim = appConfig.GetBindable<float>(AXPlayerSetting.ScalingBackgroundDim);
            alwaysUseOriginalAudio = appConfig.GetBindable<bool>(AXPlayerSetting.AlwaysUseOriginalAudio);
            sizeWindowed = config.GetBindable<Size>(FrameworkSetting.WindowedSize);

            configReleaseStream = appConfig.GetBindable<ReleaseStream>(AXPlayerSetting.ReleaseStream);

            screenshotFormat = appConfig.GetBindable<ScreenshotFormat>(AXPlayerSetting.ScreenshotFormat);
            window = host.Window;

            var reflexMode = config.GetBindable<LatencyMode>(FrameworkSetting.LatencyMode);
            var frameSyncMode = config.GetBindable<FrameSync>(FrameworkSetting.FrameSync);

            captionEnabled = appConfig.GetBindable<bool>(AXPlayerSetting.CaptionEnabled);
            showVideoMetadataOnWindowTitle = appConfig.GetBindable<bool>(AXPlayerSetting.ShowVideoMetadataOnWindowTitle);

            windowedPositionX = config.GetBindable<double>(FrameworkSetting.WindowedPositionX);
            windowedPositionY = config.GetBindable<double>(FrameworkSetting.WindowedPositionY);

            aspectRatioMethod = appConfig.GetBindable<AspectRatioMethod>(AXPlayerSetting.AspectRatioMethod);

            advancedSubtitles = appConfig.GetBindable<bool>(AXPlayerSetting.UseNewSubtitlesFeature);

            advancedSubtitles.BindValueChanged(value =>
            {
                if (value.NewValue)
                    srv3Notice.Value = new SettingsNote.Data(AXPlayerStrings.SRV3Notice, SettingsNote.Type.Warning);
                else
                    srv3Notice.Value = null;
            }, true);

            var renderer = config.GetBindable<RendererType>(FrameworkSetting.Renderer);
            automaticRendererInUse = renderer.Value == RendererType.Automatic;

            windowedResolution.Value = sizeWindowed.Value;

            if (window != null)
            {
                currentDisplay.BindTo(window.CurrentDisplayBindable);
                window.DisplaysChanged += onDisplaysChanged;
            }

            if (host.Renderer is IWindowsRenderer windowsRenderer)
                fullscreenCapability.BindTo(windowsRenderer.FullscreenCapability);

            playOverlaySFX = appConfig.GetBindable<bool>(AXPlayerSetting.PlayOverlaySFX);
            overlaySFXType = appConfig.GetBindable<SFXType>(AXPlayerSetting.OverlaySFXType);

            ui_font = appConfig.GetBindable<UIFont>(AXPlayerSetting.UIFont);
            caption_font = appConfig.GetBindable<CaptionFonts>(AXPlayerSetting.CaptionFont);

            exportStorage = storage.GetStorageForDirectory(@"exports");

            updateInfomationText = game.UpdateManagerVersionText.GetBoundCopy();
            updateButtonEnabled = game.UpdateButtonEnabled.GetBoundCopy();

            discordRichPresence = appConfig.GetBindable<DiscordRichPresenceMode>(AXPlayerSetting.DiscordRichPresence);

            Size = new Vector2(0.34f, 1f);
            RelativeSizeAxes = Axes.Both;
            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0);
            Masking = true;
            Origin = Anchor.CentreRight;
            Anchor = Anchor.CentreRight;
            Children = new Drawable[]
            {
                popoverContainer = new PopoverContainer {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[] {
                                new OverlayBackground
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding
                                    {
                                        Horizontal = 4,
                                    },
                                    Children = new Drawable[] {
                                        settingsSections = new ProjectYomiScrollContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            ScrollbarVisible = false,
                                            Children = new Drawable[]
                                            {
                                                new FillFlowContainer {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Spacing = new Vector2(0, 2),
                                                    Direction = FillDirection.Vertical,
                                                    Padding = new MarginPadding
                                                    {
                                                        Top = 56,
                                                        Bottom = 48 + 24,
                                                    },
                                                    Children = new Drawable[] {
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Quick Actions",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.QuickAction,
                                                            Padding = new MarginPadding { Horizontal = 30, Bottom = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        exportLogButton = new SettingsButtonV2
                                                        {
                                                            Padding = new MarginPadding { Horizontal = 30 },
                                                            Text = AXPlayerStrings.ExportLogs,
                                                            BackgroundColour = colours.YellowDarker.Darken(0.5f),
                                                            Action = () => Task.Run(exportLogs),
                                                        },
                                                        new SettingsButtonV2
                                                        {
                                                            Padding = new MarginPadding { Horizontal = 30 },
                                                            Text = AXPlayerStrings.ReportBugs,
                                                            TooltipText = AXPlayerStrings.ReportBugsDesc,
                                                            Action = () => host.OpenUrlExternally("https://zeromayo.featurebase.app/"),
                                                        },
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "General Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.General,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<CloseButtonAction>
                                                        {
                                                            Caption = AXPlayerStrings.CloseButtonAction,
                                                            Current = closeButtonAction,
                                                            Icon = FontAwesome.Regular.WindowClose,
                                                            CornerType = FormControlCornerType.Top,
                                                        }),
                                                        new SettingsItemV2(discordRichPresenceDropdown = new FormEnumDropdownWithDiscordProfileImage<DiscordRichPresenceMode>
                                                        {
                                                            Caption = AXPlayerStrings.DiscordRichPresence,
                                                            Current = discordRichPresence,
                                                            Icon = FontAwesome.Brands.Discord,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            Note = { BindTarget = discordNotInstalledNote },
                                                        },
                                                        new SettingsItemV2(login = new FormGoogleOAuthButton
                                                        {
                                                            Caption = AXPlayerStrings.GoogleAccount,
                                                            Text = AXPlayerStrings.SignedOut,
                                                            Icon = FontAwesome.Brands.Google,
                                                            CornerType = game.IsDeployedBuild ? FormControlCornerType.Center : FormControlCornerType.Bottom,
                                                            Action = () => {
                                                                OAuthSignInAction.Invoke();
                                                            },
                                                        }),
                                                        releaseStreamSelectorButtonCore = new SettingsItemV2(releaseStreamDropdown = new FormEnumDropdown<ReleaseStream>
                                                        {
                                                            Caption = AXPlayerStrings.ReleaseStream,
                                                            Current = { Value = configReleaseStream.Value },
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        checkForUpdatesButtonCore = new SettingsItemV2(CheckForUpdatesButton = new FormButton
                                                        {
                                                            Caption = AXPlayerStrings.CheckUpdate,
                                                            Text = game.Version,
                                                            ButtonIcon = FontAwesome.Solid.Sync,
                                                            Icon = FontAwesome.Solid.Sync,
                                                            Action = () => {
                                                                CheckUpdateAction.Invoke();
                                                            },
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "UI Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.UserInterface,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<Language>
                                                        {
                                                            Caption = AXPlayerStrings.Language,
                                                            Current = game.CurrentLanguage,
                                                            Icon = FontAwesome.Solid.Language,
                                                            AlwaysShowSearchBar = true,
                                                            CornerType = FormControlCornerType.Top,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<UsernameDisplayMode>
                                                        {
                                                            Caption = AXPlayerStrings.UsernameDisplayMode,
                                                            Current = usernameDisplayMode,
                                                            Icon = FontAwesome.Solid.User,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<VideoMetadataDisplayAlignment>
                                                        {
                                                            Caption = VideoMetadataDisplayAlignmentStrings.VideoMetadataDisplayAlignmentSetting,
                                                            Current = videoMetadataDisplayAlignment,
                                                            Icon = FontAwesome.Solid.List,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<OverlayColourScheme>
                                                        {
                                                            Caption = AXPlayerStrings.ColourScheme,
                                                            Current = colourSchemeBindable,
                                                            Icon = FontAwesome.Solid.Palette,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.DarkMode,
                                                            Current = darkModeBindable,
                                                            Icon = FontAwesome.Solid.Moon,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<UIFont>
                                                        {
                                                            Caption = AXPlayerStrings.UIFont,
                                                            Current = ui_font,
                                                            Icon = FontAwesome.Solid.Font,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<ProfileImageShape>
                                                        {
                                                            Caption = AXPlayerStrings.ProfileImageShape,
                                                            Current = profileImageShape,
                                                            Icon = FontAwesome.Solid.Shapes,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = AXPlayerStrings.UIScaling,
                                                            Icon = FontAwesome.Solid.SlidersH,
                                                            TransferValueOnCommit = true,
                                                            Current = appConfig.GetBindable<float>(AXPlayerSetting.UIScale),
                                                            KeyboardStep = 0.01f,
                                                            LabelFormat = v => $@"{v:0.##}x",
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.PlayOverlaySFX,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = playOverlaySFX,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<SFXType>
                                                        {
                                                            Caption = AXPlayerStrings.SFXType,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = overlaySFXType,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<PlayerControllerFadeStyle>
                                                        {
                                                            Caption = AXPlayerStrings.PlayerControllerFadeStyle,
                                                            Icon = FontAwesome.Solid.Eye,
                                                            Current = appConfig.GetBindable<PlayerControllerFadeStyle>(AXPlayerSetting.PlayerControllerFadeStyle),
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.WavySeekbar,
                                                            Icon = FontAwesome.Solid.SlidersH,
                                                            Current = appConfig.GetBindable<bool>(AXPlayerSetting.WavySeekBar),
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Graphics Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.Graphics,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FrameSyncDropdown
                                                        {
                                                            Caption = AXPlayerStrings.FrameLimiter,
                                                            Icon = FontAwesome.Solid.SlidersH,
                                                            Current = frameSyncMode,
                                                            Hotkey = new Hotkey(new KeyCombination(new [] { InputKey.Control, InputKey.F7 })),
                                                            CornerType = FormControlCornerType.Top,
                                                        }),
                                                        windowModeDropdownSettings = new SettingsItemV2(windowModeDropdown = new WindowModeDropdown
                                                        {
                                                            Caption = AXPlayerStrings.ScreenMode,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            Items = window?.SupportedWindowModes,
                                                            Current = windowMode,
                                                            Hotkey = new Hotkey(new KeyCombination(new [] { InputKey.F11 })),
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            CanBeShown = { Value = window?.SupportedWindowModes.Count() > 1 },
                                                        },
                                                        displayDropdownCore = new SettingsItemV2(displayDropdown = new DisplaySettingsDropdown
                                                        {
                                                            Caption = AXPlayerStrings.Display,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            Items = window?.Displays,
                                                            Current = currentDisplay,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            CanBeShown = { BindTarget = displayDropdownCanBeShown }
                                                        },
                                                        resolutionFullscreenDropdownCore = new SettingsItemV2(resolutionFullscreenDropdown = new ResolutionSettingsDropdown
                                                        {
                                                            Caption = AXPlayerStrings.ScreenResolution,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            ItemSource = resolutionsFullscreen,
                                                            Current = sizeFullscreen,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                            CanBeShown = { BindTarget = resolutionFullscreenCanBeShown }
                                                        },
                                                        resolutionWindowedDropdownCore = new SettingsItemV2(resolutionWindowedDropdown = new ResolutionSettingsDropdown
                                                        {
                                                            Caption = AXPlayerStrings.ScreenResolution,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            ItemSource = resolutionsWindowed,
                                                            Current = windowedResolution,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                            CanBeShown = { BindTarget = resolutionWindowedCanBeShown }
                                                        },
                                                        minimiseOnFocusLossCheckboxCore = new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.MinimiseOnFocusLoss,
                                                            Icon = FontAwesome.Regular.WindowMinimize,
                                                            Current = config.GetBindable<bool>(FrameworkSetting.MinimiseOnFocusLossInFullscreen),
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new RendererSettingsDropdown
                                                        {
                                                            Caption = AXPlayerStrings.Renderer,
                                                            Icon = FontAwesome.Solid.Bolt,
                                                            Current = renderer,
                                                            Items = host.GetPreferredRenderersForCurrentPlatform().Order()
                                                            #pragma warning disable CS0612 // Type or member is obsolete
                                                            .Where(t => t != RendererType.Vulkan && t != RendererType.OpenGLLegacy),
                                                            #pragma warning restore CS0612 // Type or member is obsolete
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        reflexSettingBase = new SettingsItemV2(reflexSetting = new ReflexDropdown
                                                        {
                                                            Caption = "NVIDIA Reflex",
                                                            Icon = FontAwesome.Solid.Bolt,
                                                            Current = reflexMode,
                                                            HintText = AXPlayerStrings.ReflexHint,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            Note = { BindTarget = reflexNotice }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ShowFPS,
                                                            Icon = FontAwesome.Solid.Bolt,
                                                            Current = fpsDisplay,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleFPSDisplay),
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(ScreenScaling = new FormEnumDropdown<ScalingMode>
                                                        {
                                                            Caption = AXPlayerStrings.ScreenScaling,
                                                            Icon = FontAwesome.Solid.WindowMaximize,
                                                            Current = appConfig.GetBindable<ScalingMode>(AXPlayerSetting.Scaling),
                                                            Hotkey = new Hotkey(GlobalAction.CycleScalingMode),
                                                            CornerType = FormControlCornerType.Bottom,
                                                            DynamicCornerType = true,
                                                        })
                                                        {
                                                            Keywords = new[] { "scale", "letterbox" },
                                                        },
                                                        scalingSettings = new FillFlowContainer<SettingsItemV2>
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 2),
                                                            Children = new[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.HorizontalPosition,
                                                                    Current = scalingPositionX,
                                                                    Icon = FontAwesome.Solid.RulerHorizontal,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                    CornerType = FormControlCornerType.Center,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.VerticalPosition,
                                                                    Current = scalingPositionY,
                                                                    Icon = FontAwesome.Solid.RulerVertical,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                    CornerType = FormControlCornerType.Center,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.HorizontalScale,
                                                                    Icon = FontAwesome.Solid.RulerHorizontal,
                                                                    Current = scalingSizeX,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                    CornerType = FormControlCornerType.Center,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.VerticalScale,
                                                                    Icon = FontAwesome.Solid.RulerVertical,
                                                                    Current = scalingSizeY,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                    CornerType = FormControlCornerType.Center,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(dimSlider = new FormSliderBar<float>
                                                                {
                                                                    Caption = AXPlayerStrings.ThumbnailDim,
                                                                    Icon = FontAwesome.Regular.Sun,
                                                                    Current = scalingBackgroundDim,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                    CornerType = FormControlCornerType.Bottom,
                                                                })
                                                            }
                                                        },
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Screenshot Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.Screenshot,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<Config.ScreenshotFormat>
                                                        {
                                                            Caption = AXPlayerStrings.ScreenshotFormat,
                                                            Icon = FontAwesome.Solid.WindowMaximize,
                                                            Current = screenshotFormat,
                                                            CornerType = FormControlCornerType.Top,
                                                        }),
                                                        screenshotOptions = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 4),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<int>
                                                                {
                                                                    Caption = AXPlayerStrings.ScreenshotQuality,
                                                                    Icon = FontAwesome.Solid.SlidersH,
                                                                    Current = appConfig.GetBindable<int>(AXPlayerSetting.ScreenshotQuality),
                                                                    LabelFormat = value => $"{value}%",
                                                                    CornerType = FormControlCornerType.Center,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ShowCursorInScreenshots,
                                                            Icon = FontAwesome.Solid.MousePointer,
                                                            Current = appConfig.GetBindable<bool>(AXPlayerSetting.ScreenshotCaptureMenuCursor),
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Video Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.Video,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<AspectRatioMethod>
                                                        {
                                                            Caption = AXPlayerStrings.AspectRatioMethod,
                                                            Icon = FontAwesome.Solid.WindowMaximize,
                                                            Current = aspectRatioMethod,
                                                            Hotkey = new Hotkey(GlobalAction.CycleAspectRatio),
                                                            CornerType = FormControlCornerType.Top,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<double>
                                                        {
                                                            Caption = AXPlayerStrings.VideoDimLevel,
                                                            Icon = FontAwesome.Regular.Sun,
                                                            Current = appConfig.GetBindable<double>(AXPlayerSetting.VideoDimLevel),
                                                            DisplayAsPercentage = true,
                                                            KeyboardStep = 0.05f,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(hwAccelCheckbox = new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.UseHardwareAcceleration,
                                                            Icon = FontAwesome.Solid.Bolt,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            Note = { BindTarget = hwAccelNote },
                                                        },
                                                        VideoQualitySettingsCore = new SettingsItemV2(VideoQualitySettings = new YouTubeQualityDropdown
                                                        {
                                                            Caption = AXPlayerStrings.VideoQuality,
                                                            Icon = FontAwesome.Solid.Video,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        AudioQualitySettingsCore = new SettingsItemV2(AudioQualitySettings = new YouTubeAudioStreamDropdown
                                                        {
                                                            Caption = AXPlayerStrings.AudioTracks,
                                                            Icon = FontAwesome.Solid.FileAudio,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ShowVideoMetadataOnWindowTitle,
                                                            Icon = FontAwesome.Solid.Font,
                                                            Current = showVideoMetadataOnWindowTitle,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ResetPlaybackSpeedWhenLoadingAVideo,
                                                            Icon = FontAwesome.Solid.TachometerAlt,
                                                            Current = appConfig.GetBindable<bool>(AXPlayerSetting.ResetPlaybackSpeedWhenLoadingAVideo),
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "VFX Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.VisualEffects,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = AXPlayerStrings.VideoBloomLevel,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(AXPlayerSetting.VideoBloomLevel),
                                                            DisplayAsPercentage = true,
                                                            CornerType = FormControlCornerType.Top,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = AXPlayerStrings.ChromaticAberration,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(AXPlayerSetting.ChromaticAberrationStrength),
                                                            DisplayAsPercentage = true,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = AXPlayerStrings.VideoGrayscaleLevel,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(AXPlayerSetting.VideoGrayscaleLevel),
                                                            DisplayAsPercentage = true,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = AXPlayerStrings.VideoHueShift,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(AXPlayerSetting.VideoHueShift),
                                                            KeyboardStep = 1,
                                                            LabelFormat = value => $"{value:N0}°",
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "CC Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.ClosedCaptions,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new ClosedCaptionPreview
                                                        {
                                                            Padding = new MarginPadding { Horizontal = 30 },
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 150,
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ClosedCaptions,
                                                            Icon = FontAwesome.Solid.ClosedCaptioning,
                                                            Current = captionEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.CycleCaptionLanguage),
                                                            CornerType = FormControlCornerType.Top,
                                                        }),
                                                        advancedSubtitlesBase = new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.AdvancedSubtitleStyle,
                                                            Icon = FontAwesome.Solid.ClosedCaptioning,
                                                            Current = advancedSubtitles,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            Note = { BindTarget = srv3Notice },
                                                        },
                                                        captionLangOptions = new SettingsItemV2(CaptionLangDropdown = new YouTubeI18nLangDropdown
                                                        {
                                                            Caption = AXPlayerStrings.CaptionLanguage,
                                                            Icon = FontAwesome.Solid.Language,
                                                            CornerType = FormControlCornerType.Center,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        new SettingsItemV2(new FormEnumFontDropdown<CaptionFonts>
                                                        {
                                                            Caption = AXPlayerStrings.CaptionFont,
                                                            Current = caption_font,
                                                            Icon = FontAwesome.Solid.Font,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = AXPlayerStrings.CaptionBGOpacity,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = captionBGOpacity,
                                                            DisplayAsPercentage = true,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormColourPicker
                                                        {
                                                            Caption = AXPlayerStrings.CaptionBGColour,
                                                            Current = appConfig.GetBindable<Colour4>(AXPlayerSetting.CaptionBGColor),
                                                            Icon = FontAwesome.Solid.Palette,
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<int>
                                                        {
                                                            Caption = AXPlayerStrings.CaptionCornerRadius,
                                                            Icon = FontAwesome.Solid.SlidersH,
                                                            Current = appConfig.GetBindable<int>(AXPlayerSetting.CaptionCornerRadius),
                                                            KeyboardStep = 1,
                                                            LabelFormat = value => $"{value}px",
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ClosedCaptionsSmoothAnimation,
                                                            Icon = FontAwesome.Solid.ClosedCaptioning,
                                                            Current = appConfig.GetBindable<bool>(AXPlayerSetting.SmoothCaptionAnimation),
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Audio Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.Audio,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(audioDeviceDropdown = new AudioDeviceDropdown
                                                        {
                                                            Caption = AXPlayerStrings.OutputDevice,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            CornerType = FormControlCornerType.Top,
                                                        })
                                                        {
                                                            Note = { BindTarget = alsaExclusiveDeviceNote },
                                                        },
                                                        wasapiExperimentalItem = new SettingsItemV2(wasapiExperimental = new LegacyAudioCheckbox())
                                                        {
                                                            Keywords = new[] { "wasapi", "latency", "exclusive", "legacy", "experimental" },
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.AdjustPitchOnSpeedChange,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = adjustPitch,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleAdjustPitchOnSpeedChange),
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.Volume,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        volumeOptions = new FillFlowContainer
                                                        {
                                                            Direction = FillDirection.Vertical,
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Masking = true,
                                                            Spacing = new Vector2(0, 2),
                                                            Children = new Drawable[]
                                                            {
                                                                new SettingsItemV2(new FormSliderBar<double>
                                                                {
                                                                    Caption = AXPlayerStrings.VideoVolume,
                                                                    Icon = FontAwesome.Solid.VolumeUp,
                                                                    Current = videoVolume,
                                                                    DisplayAsPercentage = true,
                                                                    CornerType = FormControlCornerType.Top,
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<double>
                                                                {
                                                                    Caption = AXPlayerStrings.SFXVolume,
                                                                    Icon = FontAwesome.Solid.VolumeUp,
                                                                    Current = config.GetBindable<double>(FrameworkSetting.VolumeEffect),
                                                                    DisplayAsPercentage = true,
                                                                    CornerType = FormControlCornerType.Center,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.AudioNormalization,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = appConfig.GetBindable<bool>(AXPlayerSetting.AudioNormalization),
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Debug Settings",
                                                            Font = AXPlayerApp.DefaultFont.With(size: 30),
                                                            Text = AXPlayerStrings.Debug,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.ShowLogOverlay,
                                                            Icon = FontAwesome.Solid.Bug,
                                                            Current = config.GetBindable<bool>(FrameworkSetting.ShowLogOverlay),
                                                            Hotkey = new Hotkey(new KeyCombination(new [] { InputKey.Control, InputKey.F10 })),
                                                            CornerType = FormControlCornerType.Top,
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = AXPlayerStrings.BypassFTBRenderPass,
                                                            Icon = FontAwesome.Solid.Bug,
                                                            Current = debugConfig.GetBindable<bool>(DebugSetting.BypassFrontToBackPass),
                                                            CornerType = FormControlCornerType.Center,
                                                        }),
                                                        new SettingsItemV2(latencyModeDropdown = new FormEnumDropdown<GCLatencyMode>
                                                        {
                                                            Caption = AXPlayerStrings.GC_Mode,
                                                            Icon = FontAwesome.Solid.Bug,
                                                            CornerType = FormControlCornerType.Bottom,
                                                        }),
                                                        new SettingsButtonV2
                                                        {
                                                            Text = AXPlayerStrings.ClearAllCaches,
                                                            Padding = new MarginPadding { Horizontal = 30 },
                                                            Action = () =>
                                                            {
                                                                host.Collect();

                                                                // host.Collect() uses GCCollectionMode.Optimized, but we should be as aggressive as possible here.
                                                                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                                                            }
                                                        },
                                                        new Container
                                                        {
                                                            Name = "App Info",
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Margin = new MarginPadding { Top = 12 },
                                                            Child = new Container
                                                            {
                                                                AutoSizeAxes = Axes.Both,
                                                                Anchor = Anchor.Centre,
                                                                Origin = Anchor.Centre,
                                                                Child = new Sprite
                                                                {
                                                                    Width = 100,
                                                                    Height = 100,
                                                                    Texture = textures.Get(@"AXPlayer-Icon"),
                                                                    FillMode = FillMode.Fit,
                                                                }
                                                            },
                                                        },
                                                        new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"))
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Text = "AX Player",
                                                            TextAnchor = Anchor.Centre,
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        gameVersion = new LinkFlowContainer(f =>
                                                        {
                                                            f.Font = AXPlayerApp.DefaultFont.With(size: 15);
                                                            f.Colour = overlayColourProvider.Content2;
                                                        })
                                                        {
                                                            Margin = new MarginPadding { Top = 4 },
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            TextAnchor = Anchor.Centre,
                                                        },
                                                        ffmpegLibVersion = new ProjectYomiTextFlowContainer(f =>
                                                        {
                                                            f.Font = AXPlayerApp.DefaultFont.With(size: 15);
                                                            f.Colour = overlayColourProvider.Content2;
                                                        })
                                                        {
                                                            Margin = new MarginPadding { Top = 4 },
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            TextAnchor = Anchor.Centre,
                                                        },
                                                        madeByText = new LinkFlowContainer(f =>
                                                        {
                                                            f.Font = AXPlayerApp.DefaultFont.With(size: 15);
                                                            f.Colour = overlayColourProvider.Content2;
                                                        })
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            TextAnchor = Anchor.Centre,
                                                        },
                                                        dislikeCounterCredits = new LinkFlowContainer(f =>
                                                        {
                                                            f.Font = AXPlayerApp.DefaultFont.With(size: 15);
                                                            f.Colour = overlayColourProvider.Content2;
                                                        })
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            TextAnchor = Anchor.Centre,
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
                                    Height = 76,
                                },
                                new Box
                                {
                                    Name = "masking of overlay",
                                    RelativeSizeAxes = Axes.X,
                                    Anchor = Anchor.BottomCentre,
                                    Origin = Anchor.BottomCentre,
                                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5),
                                    Height = 76,
                                },
                                new ProjectYomiSpriteText
                                {
                                    Origin = Anchor.TopCentre,
                                    Anchor = Anchor.TopCentre,
                                    Text = AXPlayerStrings.Settings,
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
                                        CloseOverlayAction.Invoke();
                                    }
                                },
                                settingsTabBar = new AXPlayerSettingsTabBar
                                {
                                    Origin = Anchor.BottomCentre,
                                    Anchor = Anchor.BottomCentre,
                                    Margin = new MarginPadding(16),
                                }
                    }
                }
            };

            screenshotFormat.BindValueChanged(_ =>
            {
                screenshotOptions.ClearTransforms();
                screenshotOptions.AutoSizeDuration = 400;
                screenshotOptions.AutoSizeEasing = Easing.OutQuint;

                updateScreenshotOptionsVisibility();
            }, true);

            if (RuntimeInfo.OS != RuntimeInfo.Platform.Windows)
            {
                wasapiExperimentalItem.Hide();
            }

            releaseStreamDropdown.Current.BindValueChanged(releaseStreamChanged);

            wasapiExperimental.Current.ValueChanged += _ => onAudioDeviceChanged(string.Empty);

            VideoQualitySettingsCore.Hide();
            AudioQualitySettingsCore.Hide();

            dislikeCounterCredits.AddText(AXPlayerStrings.DislikeCounterCredits_1);
            dislikeCounterCredits.AddLink("Return YouTube Dislike API", "https://returnyoutubedislike.com/");
            dislikeCounterCredits.AddText(AXPlayerStrings.DislikeCounterCredits_2);

            ffmpegLibVersion.AddText($"libavcodec {ffmpeg.LIBAVCODEC_VERSION}\n");
            ffmpegLibVersion.AddText($"libavformat {ffmpeg.LIBAVFORMAT_VERSION}\n");
            ffmpegLibVersion.AddText($"libavutil {ffmpeg.LIBAVUTIL_VERSION}\n");
            ffmpegLibVersion.AddText($"libswscale {ffmpeg.LIBSWSCALE_VERSION}");

            if (game.IsDeployedBuild)
            {
                gameVersion.AddText(AXPlayerStrings.VersionText(string.Empty));
                gameVersion.AddLink(game.Version, $"https://github.com/ZeroMayo/AX-Player/releases/{game.Version}", tooltipText: AXPlayerStrings.ViewChangelog(game.Version));
            }
            else
            {
                gameVersion.AddText(AXPlayerStrings.VersionText(game.Version));
            }

            madeByText.AddText("made by ");
            madeByText.AddLink("Mayo_0x0 (ZeroMayo)", "https://github.com/ZeroMayo/", AXPlayerStrings.ViewGitHubProfile);

            if (discordRPC != null)
            {
                try
                {
                    discordRichPresenceDropdown.HintText = $"{discordRPC.GetCurrentUser().DisplayName} ({discordRPC.GetCurrentUser().Username})";
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            }

            screenshotFormat.BindValueChanged(_ =>
            {
                screenshotOptions.ClearTransforms();
                screenshotOptions.AutoSizeDuration = 400;
                screenshotOptions.AutoSizeEasing = Easing.OutQuint;

                updateScreenshotOptionsVisibility();
            }, true);

            updateInfomationText.BindValueChanged(text =>
            {
                Schedule(() => CheckForUpdatesButton.Text = text.NewValue);
            });

            updateButtonEnabled.BindValueChanged(enabled =>
            {
                Schedule(() => CheckForUpdatesButton.Enabled.Value = enabled.NewValue);
            });

            hwAccelCheckbox.Current.Default = hardwareVideoDecoder.Default != HardwareVideoDecoder.None;
            hwAccelCheckbox.Current.Value = hardwareVideoDecoder.Value != HardwareVideoDecoder.None;

            hwAccelCheckbox.Current.BindValueChanged(val =>
            {
                hwAccelNote.Value = val.NewValue ? new SettingsNote.Data(AXPlayerStrings.HardwareAccelerationEnabledNote, SettingsNote.Type.Informational) : null;
                hardwareVideoDecoder.Value = val.NewValue ? HardwareVideoDecoder.Any : HardwareVideoDecoder.None;
            }, true);

            scalingMode.BindValueChanged(_ =>
            {
                scalingSettings.ClearTransforms();
                scalingSettings.AutoSizeDuration = 400;
                scalingSettings.AutoSizeEasing = Easing.OutQuint;

                updateScalingModeVisibility();
            });
            updateScalingModeVisibility();

            // Ensure NVIDIA reflex is turned off and hidden if the resolved renderer isn't Direct3D 11
            if (host.ResolvedRenderer is not (RendererType.Deferred_Direct3D11 or RendererType.Direct3D11))
            {
                reflexMode.Value = LatencyMode.Off;
                reflexSettingBase.Hide();
            }

            /*
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
                advancedSubtitlesBase.Hide();
            */

            // Disable frame limiter if reflex is enabled and add notice when reflex boost is enabled
            reflexMode.BindValueChanged(r =>
            {
                frameSyncMode.Disabled = r.NewValue != LatencyMode.Off;

                //reflexSetting.ClearNoticeText();
                reflexNotice.Value = null;

                if (r.NewValue == LatencyMode.Boost)
                    setReflexBoostNotice();
            }, true);

            captionEnabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    captionLangOptions.Show();
                else
                    captionLangOptions.Hide();
            }, true);

            renderer.BindValueChanged(r =>
            {
                if (r.NewValue == host.ResolvedRenderer)
                    return;

                // Need to check startup renderer for the "automatic" case, as ResolvedRenderer above will track the final resolved renderer instead.
                if (r.NewValue == RendererType.Automatic && automaticRendererInUse)
                    return;

                restartDialogOverlay.OpenDialog();
            });

            latencyModeDropdown.Current.BindValueChanged(mode =>
            {
                Logger.Log($"Changing latency mode: {mode.NewValue}");

                switch (mode.NewValue)
                {
                    case GCLatencyMode.Default:
                        // https://github.com/ppy/osu-framework/blob/1d5301018dfed1a28702be56e1d53c4835b199f2/osu.Framework/Platform/GameHost.cs#L703
                        GCSettings.LatencyMode = System.Runtime.GCLatencyMode.SustainedLowLatency;
                        break;

                    case GCLatencyMode.Interactive:
                        GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Interactive;
                        break;
                }
            });

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
#pragma warning disable CA1416 // 플랫폼 호환성 유효성 검사
                discordRichPresence.Disabled = !DiscordInstallationChecker.IsDiscordInstalled();

                if (!DiscordInstallationChecker.IsDiscordInstalled())
                {
                    discordNotInstalledNote.Value = new SettingsNote.Data(AXPlayerStrings.DiscordNotInstalled, SettingsNote.Type.Informational);
                }
#pragma warning restore CA1416 // 플랫폼 호환성 유효성 검사
            }

            audio.OnNewDevice += onAudioDeviceChanged;
            audio.OnLostDevice += onAudioDeviceChanged;
            audioDeviceDropdown.Current = audio.AudioDevice;

            audioDeviceDropdown.Current.ValueChanged += d => onDeviceSelected(d.NewValue);

            onDeviceSelected(audioDeviceDropdown.Current.Value);

            onAudioDeviceChanged(string.Empty);

            prepareSettingsTabs();

            if (!game.IsDeployedBuild)
            {
                releaseStreamSelectorButtonCore.Hide();
                checkForUpdatesButtonCore.Hide();
            }

            if (window?.SupportedWindowModes.Count() > 1)
            {
                windowModeDropdownSettings.Show();
            }
            else
            {
                windowModeDropdownSettings.Hide();
            }

            windowModeDropdown.Current.BindValueChanged(_ =>
            {
                updateDisplaySettingsVisibility();
            }, true);

            /*
            alwaysUseOriginalAudio.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                {
                    audioLanguageItem.Hide();
                }
                else
                {
                    audioLanguageItem.Show();
                }
            }, true);
            */

            currentDisplay.BindValueChanged(display => Schedule(() =>
            {
                if (display.NewValue == null)
                {
                    resolutionsFullscreen.Clear();
                    resolutionsWindowed.Clear();
                    return;
                }

                var buffer = new Bindable<Size>(windowedResolution.Value);
                resolutionWindowedDropdown.Current = buffer;

                var fullscreenResolutions = display.NewValue.DisplayModes
                                                   .Where(m => m.Size.Width >= 800 && m.Size.Height >= 600)
                                                   .OrderByDescending(m => Math.Max(m.Size.Height, m.Size.Width))
                                                   .Select(m => m.Size)
                                                   .Distinct()
                                                   .ToList();
                var windowedResolutions = fullscreenResolutions
                                          .Where(res => res.Width <= display.NewValue.UsableBounds.Width && res.Height <= display.NewValue.UsableBounds.Height)
                                          .ToList();

                resolutionsFullscreen.ReplaceRange(1, resolutionsFullscreen.Count - 1, fullscreenResolutions);
                resolutionsWindowed.ReplaceRange(0, resolutionsWindowed.Count, windowedResolutions);

                resolutionWindowedDropdown.Current = windowedResolution;

                updateDisplaySettingsVisibility();
            }), true);

            windowedResolution.BindValueChanged(size =>
            {
                if (size.NewValue == sizeWindowed.Value || windowModeDropdown.Current.Value != WindowMode.Windowed)
                    return;

                if (window?.WindowState == osu.Framework.Platform.WindowState.Maximised)
                {
                    window.WindowState = osu.Framework.Platform.WindowState.Normal;
                }

                // Adjust only for top decorations (assuming system titlebar).
                // Bottom/left/right borders are ignored as invisible padding, which don't align with the screen.
                var dBounds = currentDisplay.Value.Bounds;
                var dUsable = currentDisplay.Value.UsableBounds;
                float topBar = host.Window?.BorderSize.Value.Top ?? 0;

                int w = Math.Min(size.NewValue.Width, dUsable.Width);
                int h = (int)Math.Min(size.NewValue.Height, dUsable.Height - topBar);

                windowedResolution.Value = new Size(w, h);
                sizeWindowed.Value = windowedResolution.Value;

                float adjustedY = Math.Max(
                    dUsable.Y + ((dUsable.Height - h) / 2f),
                    dUsable.Y + topBar // titlebar adjustment
                );
                windowedPositionY.Value = dBounds.Height - h != 0 ? (adjustedY - dBounds.Y) / (dBounds.Height - h) : 0;
                windowedPositionX.Value = dBounds.Width - w != 0 ? (dUsable.X - dBounds.X + ((dUsable.Width - w) / 2f)) / (dBounds.Width - w) : 0;
            });

            sizeWindowed.BindValueChanged(size =>
            {
                if (size.NewValue != windowedResolution.Value)
                    windowedResolution.Value = size.NewValue;
            });

            void updateScreenshotOptionsVisibility()
            {
                try
                {
                    if (screenshotFormat.Value != ScreenshotFormat.Jpg)
                        screenshotOptions.ResizeHeightTo(0, 400, Easing.OutQuint);

                    screenshotOptions.AutoSizeAxes = screenshotFormat.Value == ScreenshotFormat.Jpg ? Axes.Y : Axes.None;
                }
                catch
                {
                }
            }

            void updateScalingModeVisibility()
            {
                try
                {
                    if (scalingMode.Value == ScalingMode.Off)
                        scalingSettings.ResizeHeightTo(0, 400, Easing.OutQuint);

                    scalingSettings.AutoSizeAxes = scalingMode.Value != ScalingMode.Off ? Axes.Y : Axes.None;
                    ScreenScaling.CornerType = scalingMode.Value != ScalingMode.Off ? FormControlCornerType.Center : FormControlCornerType.Bottom;

                    foreach (SettingsItemV2 item in scalingSettings)
                    {
                        FormSliderBar<float> slider = (FormSliderBar<float>)item.Control;

                        if (slider == dimSlider)
                            item.CanBeShown.Value = scalingMode.Value == ScalingMode.Everything || scalingMode.Value == ScalingMode.Video;
                        else
                        {
                            slider.TransferValueOnCommit = scalingMode.Value == ScalingMode.Everything;
                            item.CanBeShown.Value = scalingMode.Value != ScalingMode.Off;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void setReflexBoostNotice() => reflexNotice.Value = new SettingsNote.Data(AXPlayerStrings.ReflexNotice, SettingsNote.Type.Informational);

        private Bindable<double> windowedPositionX = null!;
        private Bindable<double> windowedPositionY = null!;
        private Bindable<ScalingMode> scalingMode = null!;

        private bool automaticRendererInUse;
        private FormCheckBox hwAccelCheckbox;
        private Bindable<SettingsNote.Data> hwAccelNote = new Bindable<SettingsNote.Data>();
        private Bindable<SettingsNote.Data> reflexNotice = new Bindable<SettingsNote.Data>();

        private void updateDisplaySettingsVisibility()
        {
            if (windowModeDropdown.Current.Value == WindowMode.Fullscreen && resolutionsFullscreen.Count > 1)
            {
                resolutionFullscreenDropdownCore.Show();
            }
            else
            {
                resolutionFullscreenDropdownCore.Hide();
            }

            if (windowModeDropdown.Current.Value == WindowMode.Windowed && resolutionsFullscreen.Count > 1)
            {
                resolutionWindowedDropdownCore.Show();
            }
            else
            {
                resolutionWindowedDropdownCore.Hide();
            }

            if (displayDropdown.Items.Count() > 1)
            {
                displayDropdownCore.Show();
            }
            else
            {
                displayDropdownCore.Hide();
            }

            if (RuntimeInfo.IsDesktop && windowModeDropdown.Current.Value == WindowMode.Fullscreen)
            {
                minimiseOnFocusLossCheckboxCore.Show();
            }
            else
            {
                minimiseOnFocusLossCheckboxCore.Hide();
            }

            /*
        resolutionFullscreenCanBeShown.Value = windowModeDropdown.Current.Value == WindowMode.Fullscreen && resolutionsFullscreen.Count > 1;
        displayDropdownCanBeShown.Value = windowModeDropdown.Current.Value == WindowMode.Windowed && resolutionsWindowed.Count > 1;
        minimiseOnFocusLossCanBeShown.Value = RuntimeInfo.IsDesktop && windowModeDropdown.Current.Value == WindowMode.Fullscreen;
            */
        }

        private readonly BindableList<Size> resolutionsFullscreen = new BindableList<Size>(new[] { new Size(9999, 9999) });
        private readonly BindableList<Size> resolutionsWindowed = new BindableList<Size>();
        private readonly Bindable<Size> windowedResolution = new Bindable<Size>();
        private readonly IBindable<FullscreenCapability> fullscreenCapability = new Bindable<FullscreenCapability>(FullscreenCapability.Capable);

        private Bindable<Size> sizeFullscreen = null!;
        private Bindable<Size> sizeWindowed = null!;

        private readonly BindableBool resolutionFullscreenCanBeShown = new BindableBool(true);
        private readonly BindableBool resolutionWindowedCanBeShown = new BindableBool(true);
        private readonly BindableBool minimiseOnFocusLossCanBeShown = new BindableBool(true);
        private SettingsItemV2 safeAreaConsiderationsCanBeShown;

        private SettingsItemV2 resolutionFullscreenDropdownCore, resolutionWindowedDropdownCore, displayDropdownCore, minimiseOnFocusLossCheckboxCore;

        private FormDropdown<Size> resolutionFullscreenDropdown = null!;
        private FormDropdown<Size> resolutionWindowedDropdown = null!;
        private FormDropdown<osu.Framework.Platform.Display> displayDropdown = null!;
        private FormDropdown<WindowMode> windowModeDropdown = null!;

        private SettingsItemV2 windowModeDropdownSettings;

        private enum GCLatencyMode
        {
            Default,
            Interactive,
        }

        [Resolved]
        private AXPlayerApp game { get; set; }
        private AXPlayerSettingsTabBar settingsTabBar;
        private Bindable<SettingsNote.Data> discordNotInstalledNote = new Bindable<SettingsNote.Data>();
        private LinkFlowContainer dislikeCounterCredits, madeByText, gameVersion;

        private ProjectYomiTextFlowContainer ffmpegLibVersion;
        private FormEnumDropdownWithDiscordProfileImage<DiscordRichPresenceMode> discordRichPresenceDropdown;
        private FillFlowContainer volumeOptions, screenshotOptions;

        private FormEnumDropdown<GCLatencyMode> latencyModeDropdown;

        private SettingsItemV2 systemMuteSwitchBase;

        [Resolved(canBeNull: true)]
        private Online.DiscordRPCClient? discordRPC { get; set; }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using NekoPlayer.App.Config;
using NekoPlayer.App.Graphics;
using NekoPlayer.App.Graphics.Caption;
using NekoPlayer.App.Graphics.Containers;
using NekoPlayer.App.Graphics.Sprites;
using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Graphics.UserInterfaceV2;
using NekoPlayer.App.Graphics.UserInterfaceV3;
using NekoPlayer.App.Input.Binding;
using NekoPlayer.App.Localisation;
using NekoPlayer.App.Overlays.OSD;
using NekoPlayer.App.Utils;
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

namespace NekoPlayer.App.Overlays.Containers
{
    public partial class SettingsContainer : SideOverlayContainer
    {
        private ProjectYomiScrollContainer settingsSections;

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
                ToastBase toast = new ToastWithIcon(NekoPlayerStrings.General, NekoPlayerStrings.LogsExportFinished, FontAwesome.Regular.ListAlt);

                onScreenDisplay.Display(toast);
                exportStorage.PresentFileExternally(archive_filename);
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

        private FormGoogleOAuthButton login;

        public Action OAuthSignInAction;
        public Action CheckUpdateAction;
        private readonly BindableBool displayDropdownCanBeShown = new BindableBool(true);
        private FillFlowContainer<SettingsItemV2> scalingSettings = null!;

        public SettingsItemV2 VideoQualitySettingsCore, AudioQualitySettingsCore;
        private FormEnumDropdown<LatencyMode>? reflexSetting;

        private void onAudioDeviceChanged(string _)
        {
            updateAudioDeviceItems();
        }

        [Resolved]
        private AudioManager audio { get; set; }

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

        private SettingsItemV2 checkForUpdatesButtonCore, reflexSettingBase, captionLangOptions;
        private Bindable<UsernameDisplayMode> usernameDisplayMode;

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

        private void onDeviceSelected(string selectedDevice)
        {
            string? currentDriver = audio.AudioDeviceNames.Where(d => d.Name == selectedDevice).Select(d => d.Driver).FirstOrDefault();
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux && currentDriver.IsNotNull() && currentDriver.StartsWith(LINUX_ALSA_DEVICE_DRIVER_PREFIX, System.StringComparison.Ordinal))
            {
                alsaExclusiveDeviceNote.Value = new SettingsNote.Data(NekoPlayerStrings.AlsaExclusiveNotice, SettingsNote.Type.Warning);
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
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Star,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Quick Actions")),
                    TooltipText = NekoPlayerStrings.QuickAction,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Cog,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("General Settings")),
                    TooltipText = NekoPlayerStrings.General,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.WindowMaximize,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("UI Settings")),
                    TooltipText = NekoPlayerStrings.UserInterface,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Bolt,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Graphics Settings")),
                    TooltipText = NekoPlayerStrings.Graphics,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Camera,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Screenshot Settings")),
                    TooltipText = NekoPlayerStrings.Screenshot,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Video,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Video Settings")),
                    TooltipText = NekoPlayerStrings.Video,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Sun,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("VFX Settings")),
                    TooltipText = NekoPlayerStrings.VisualEffects,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.ClosedCaptioning,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("CC Settings")),
                    TooltipText = NekoPlayerStrings.ClosedCaptions,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.VolumeUp,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Audio Settings")),
                    TooltipText = NekoPlayerStrings.Audio,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.Bug,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("Debug Settings")),
                    TooltipText = NekoPlayerStrings.Debug,
                },
                new NekoPlayerSettingsTabBar.Button
                {
                    Icon = FontAwesome.Solid.InfoCircle,
                    ClickAction = _ => Schedule(() => ShowSettingsOverlayAtName("App Info")),
                    TooltipText = NekoPlayerStrings.AppInfo,
                },
            };

            settingsTabBar.SetItems(drawables);
        }

        private SettingsItemV2 advancedSubtitlesBase;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider, TextureStore textures, ProjectYomiColour colours, FrameworkDebugConfigManager debugConfig, FrameworkConfigManager config, Storage storage, NekoPlayerConfigManager appConfig)
        {
            videoVolume = config.GetBindable<double>(FrameworkSetting.VolumeMusic);
            fpsDisplay = appConfig.GetBindable<bool>(NekoPlayerSetting.ShowFpsDisplay);
            closeButtonAction = appConfig.GetBindable<CloseButtonAction>(NekoPlayerSetting.CloseButtonAction);
            colourSchemeBindable = appConfig.GetBindable<OverlayColourScheme>(NekoPlayerSetting.ColourScheme);
            profileImageShape = appConfig.GetBindable<ProfileImageShape>(NekoPlayerSetting.ProfileImageShape);
            adjustPitch = appConfig.GetBindable<bool>(NekoPlayerSetting.AdjustPitchOnSpeedChange);
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(NekoPlayerSetting.UsernameDisplayMode);
            windowMode = config.GetBindable<WindowMode>(FrameworkSetting.WindowMode);
            videoMetadataDisplayAlignment = appConfig.GetBindable<VideoMetadataDisplayAlignment>(NekoPlayerSetting.VideoMetadataDisplayAlignment);
            hardwareVideoDecoder = config.GetBindable<HardwareVideoDecoder>(FrameworkSetting.HardwareVideoDecoder);
            sizeFullscreen = config.GetBindable<Size>(FrameworkSetting.SizeFullscreen);
            scalingMode = appConfig.GetBindable<ScalingMode>(NekoPlayerSetting.Scaling);
            captionBGOpacity = appConfig.GetBindable<float>(NekoPlayerSetting.CaptionBGOpacity);
            scalingSizeX = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingSizeX);
            audioQuality = appConfig.GetBindable<Config.AudioQuality>(NekoPlayerSetting.AudioQuality);
            scalingSizeY = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingSizeY);
            scalingPositionX = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingPositionX);
            scalingPositionY = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingPositionY);
            scalingBackgroundDim = appConfig.GetBindable<float>(NekoPlayerSetting.ScalingBackgroundDim);
            alwaysUseOriginalAudio = appConfig.GetBindable<bool>(NekoPlayerSetting.AlwaysUseOriginalAudio);
            sizeWindowed = config.GetBindable<Size>(FrameworkSetting.WindowedSize);

            screenshotFormat = appConfig.GetBindable<ScreenshotFormat>(NekoPlayerSetting.ScreenshotFormat);
            window = host.Window;

            var reflexMode = config.GetBindable<LatencyMode>(FrameworkSetting.LatencyMode);
            var frameSyncMode = config.GetBindable<FrameSync>(FrameworkSetting.FrameSync);

            captionEnabled = appConfig.GetBindable<bool>(NekoPlayerSetting.CaptionEnabled);
            showVideoMetadataOnWindowTitle = appConfig.GetBindable<bool>(NekoPlayerSetting.ShowVideoMetadataOnWindowTitle);

            windowedPositionX = config.GetBindable<double>(FrameworkSetting.WindowedPositionX);
            windowedPositionY = config.GetBindable<double>(FrameworkSetting.WindowedPositionY);

            aspectRatioMethod = appConfig.GetBindable<AspectRatioMethod>(NekoPlayerSetting.AspectRatioMethod);

            advancedSubtitles = appConfig.GetBindable<bool>(NekoPlayerSetting.UseNewSubtitlesFeature);

            advancedSubtitles.BindValueChanged(value =>
            {
                if (value.NewValue)
                    srv3Notice.Value = new SettingsNote.Data(NekoPlayerStrings.SRV3Notice, SettingsNote.Type.Warning);
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

            playOverlaySFX = appConfig.GetBindable<bool>(NekoPlayerSetting.PlayOverlaySFX);
            overlaySFXType = appConfig.GetBindable<SFXType>(NekoPlayerSetting.OverlaySFXType);

            ui_font = appConfig.GetBindable<UIFont>(NekoPlayerSetting.UIFont);
            caption_font = appConfig.GetBindable<CaptionFonts>(NekoPlayerSetting.CaptionFont);

            exportStorage = storage.GetStorageForDirectory(@"exports");

            updateInfomationText = game.UpdateManagerVersionText.GetBoundCopy();
            updateButtonEnabled = game.UpdateButtonEnabled.GetBoundCopy();

            discordRichPresence = appConfig.GetBindable<DiscordRichPresenceMode>(NekoPlayerSetting.DiscordRichPresence);

            Size = new Vector2(0.34f, 1f);
            RelativeSizeAxes = Axes.Both;
            CornerRadius = new CornersInfo(NekoPlayerApp.UI_CORNER_RADIUS, NekoPlayerApp.UI_CORNER_RADIUS, 0, 0);
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
                                        Horizontal = 16,
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
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.QuickAction,
                                                            Padding = new MarginPadding { Horizontal = 30, Bottom = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsButtonV2
                                                        {
                                                            Padding = new MarginPadding { Horizontal = 30 },
                                                            Text = NekoPlayerStrings.ExportLogs,
                                                            BackgroundColour = colours.YellowDarker.Darken(0.5f),
                                                            Action = () => Task.Run(exportLogs),
                                                        },
                                                        new SettingsButtonV2
                                                        {
                                                            Padding = new MarginPadding { Horizontal = 30 },
                                                            Text = NekoPlayerStrings.ReportBugs,
                                                            TooltipText = NekoPlayerStrings.ReportBugsDesc,
                                                            Action = () => host.OpenUrlExternally("https://boomboxrapsody.featurebase.app/en"),
                                                        },
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "General Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.General,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<CloseButtonAction>
                                                        {
                                                            Caption = NekoPlayerStrings.CloseButtonAction,
                                                            Current = closeButtonAction,
                                                            Icon = FontAwesome.Regular.WindowClose,
                                                        }),
                                                        new SettingsItemV2(discordRichPresenceDropdown = new FormEnumDropdownWithDiscordProfileImage<DiscordRichPresenceMode>
                                                        {
                                                            Caption = NekoPlayerStrings.DiscordRichPresence,
                                                            Current = discordRichPresence,
                                                            Icon = FontAwesome.Brands.Discord,
                                                        })
                                                        {
                                                            Note = { BindTarget = discordNotInstalledNote },
                                                        },
                                                        new SettingsItemV2(login = new FormGoogleOAuthButton
                                                        {
                                                            Caption = NekoPlayerStrings.GoogleAccount,
                                                            Text = NekoPlayerStrings.SignedOut,
                                                            Icon = FontAwesome.Brands.Google,
                                                            Action = () => {
                                                                OAuthSignInAction.Invoke();
                                                            },
                                                        }),
                                                        checkForUpdatesButtonCore = new SettingsItemV2(CheckForUpdatesButton = new FormButton
                                                        {
                                                            Caption = NekoPlayerStrings.CheckUpdate,
                                                            Text = game.Version,
                                                            ButtonIcon = FontAwesome.Solid.Sync,
                                                            Icon = FontAwesome.Solid.Sync,
                                                            Action = () => {
                                                                CheckUpdateAction.Invoke();
                                                            },
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "UI Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.UserInterface,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<Language>
                                                        {
                                                            Caption = NekoPlayerStrings.Language,
                                                            Current = game.CurrentLanguage,
                                                            Icon = FontAwesome.Solid.Language,
                                                            AlwaysShowSearchBar = true,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<UsernameDisplayMode>
                                                        {
                                                            Caption = NekoPlayerStrings.UsernameDisplayMode,
                                                            Current = usernameDisplayMode,
                                                            Icon = FontAwesome.Solid.User,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<VideoMetadataDisplayAlignment>
                                                        {
                                                            Caption = VideoMetadataDisplayAlignmentStrings.VideoMetadataDisplayAlignmentSetting,
                                                            Current = videoMetadataDisplayAlignment,
                                                            Icon = FontAwesome.Solid.List,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<OverlayColourScheme>
                                                        {
                                                            Caption = NekoPlayerStrings.ColourScheme,
                                                            Current = colourSchemeBindable,
                                                            Icon = FontAwesome.Solid.Palette,
                                                            HintText = NekoPlayerStrings.SettingsItem_RestartRequired,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<UIFont>
                                                        {
                                                            Caption = NekoPlayerStrings.UIFont,
                                                            Current = ui_font,
                                                            Icon = FontAwesome.Solid.Font,
                                                            HintText = NekoPlayerStrings.SettingsItem_RestartRequired,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<ProfileImageShape>
                                                        {
                                                            Caption = NekoPlayerStrings.ProfileImageShape,
                                                            Current = profileImageShape,
                                                            Icon = FontAwesome.Solid.Shapes,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = NekoPlayerStrings.UIScaling,
                                                            Icon = FontAwesome.Solid.SlidersH,
                                                            TransferValueOnCommit = true,
                                                            Current = appConfig.GetBindable<float>(NekoPlayerSetting.UIScale),
                                                            KeyboardStep = 0.01f,
                                                            LabelFormat = v => $@"{v:0.##}x",
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.UseSystemCursor,
                                                            Icon = FontAwesome.Solid.MousePointer,
                                                            Current = appConfig.GetBindable<bool>(NekoPlayerSetting.UseSystemCursor),
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.PlayOverlaySFX,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = playOverlaySFX,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<SFXType>
                                                        {
                                                            Caption = NekoPlayerStrings.SFXType,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = overlaySFXType,
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<PlayerControllerFadeStyle>
                                                        {
                                                            Caption = NekoPlayerStrings.PlayerControllerFadeStyle,
                                                            Icon = FontAwesome.Solid.Eye,
                                                            Current = appConfig.GetBindable<PlayerControllerFadeStyle>(NekoPlayerSetting.PlayerControllerFadeStyle),
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Graphics Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.Graphics,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FrameSyncDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.FrameLimiter,
                                                            Icon = FontAwesome.Solid.SlidersH,
                                                            Current = frameSyncMode,
                                                            Hotkey = new Hotkey(new KeyCombination(new [] { InputKey.Control, InputKey.F7 }))
                                                        }),
                                                        windowModeDropdownSettings = new SettingsItemV2(windowModeDropdown = new WindowModeDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.ScreenMode,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            Items = window?.SupportedWindowModes,
                                                            Current = windowMode,
                                                            Hotkey = new Hotkey(new KeyCombination(new [] { InputKey.F11 }))
                                                        })
                                                        {
                                                            CanBeShown = { Value = window?.SupportedWindowModes.Count() > 1 },
                                                        },
                                                        displayDropdownCore = new SettingsItemV2(displayDropdown = new DisplaySettingsDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.Display,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            Items = window?.Displays,
                                                            Current = currentDisplay,
                                                        })
                                                        {
                                                            CanBeShown = { BindTarget = displayDropdownCanBeShown }
                                                        },
                                                        resolutionFullscreenDropdownCore = new SettingsItemV2(resolutionFullscreenDropdown = new ResolutionSettingsDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.ScreenResolution,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            ItemSource = resolutionsFullscreen,
                                                            Current = sizeFullscreen
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                            CanBeShown = { BindTarget = resolutionFullscreenCanBeShown }
                                                        },
                                                        resolutionWindowedDropdownCore = new SettingsItemV2(resolutionWindowedDropdown = new ResolutionSettingsDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.ScreenResolution,
                                                            Icon = FontAwesome.Regular.WindowMaximize,
                                                            ItemSource = resolutionsWindowed,
                                                            Current = windowedResolution
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                            CanBeShown = { BindTarget = resolutionWindowedCanBeShown }
                                                        },
                                                        minimiseOnFocusLossCheckboxCore = new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.MinimiseOnFocusLoss,
                                                            Icon = FontAwesome.Regular.WindowMinimize,
                                                            Current = config.GetBindable<bool>(FrameworkSetting.MinimiseOnFocusLossInFullscreen),
                                                        }),
                                                        new SettingsItemV2(new RendererSettingsDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.Renderer,
                                                            Icon = FontAwesome.Solid.Bolt,
                                                            Current = renderer,
                                                            Items = host.GetPreferredRenderersForCurrentPlatform().Order()
                                                            #pragma warning disable CS0612 // Type or member is obsolete
                                                            .Where(t => t != RendererType.Vulkan && t != RendererType.OpenGLLegacy),
                                                            #pragma warning restore CS0612 // Type or member is obsolete
                                                            HintText = NekoPlayerStrings.SettingsItem_RestartRequired,
                                                        }),
                                                        reflexSettingBase = new SettingsItemV2(reflexSetting = new ReflexDropdown
                                                        {
                                                            Caption = "NVIDIA Reflex",
                                                            Icon = FontAwesome.Solid.Bolt,
                                                            Current = reflexMode,
                                                            HintText = NekoPlayerStrings.ReflexHint,
                                                        })
                                                        {
                                                            Note = { BindTarget = reflexNotice }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.ShowFPS,
                                                            Icon = FontAwesome.Solid.Bolt,
                                                            Current = fpsDisplay,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleFPSDisplay),
                                                        }),
                                                        new SettingsItemV2(new FormEnumDropdown<ScalingMode>
                                                        {
                                                            Caption = NekoPlayerStrings.ScreenScaling,
                                                            Icon = FontAwesome.Solid.WindowMaximize,
                                                            Current = appConfig.GetBindable<ScalingMode>(NekoPlayerSetting.Scaling),
                                                            Hotkey = new Hotkey(GlobalAction.CycleScalingMode),
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
                                                                    Caption = NekoPlayerStrings.HorizontalPosition,
                                                                    Current = scalingPositionX,
                                                                    Icon = FontAwesome.Solid.RulerHorizontal,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.VerticalPosition,
                                                                    Current = scalingPositionY,
                                                                    Icon = FontAwesome.Solid.RulerVertical,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.HorizontalScale,
                                                                    Icon = FontAwesome.Solid.RulerHorizontal,
                                                                    Current = scalingSizeX,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.VerticalScale,
                                                                    Icon = FontAwesome.Solid.RulerVertical,
                                                                    Current = scalingSizeY,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                })
                                                                {
                                                                    Keywords = new[] { "screen", "scaling" },
                                                                },
                                                                new SettingsItemV2(dimSlider = new FormSliderBar<float>
                                                                {
                                                                    Caption = NekoPlayerStrings.ThumbnailDim,
                                                                    Icon = FontAwesome.Regular.Sun,
                                                                    Current = scalingBackgroundDim,
                                                                    KeyboardStep = 0.01f,
                                                                    DisplayAsPercentage = true,
                                                                })
                                                            }
                                                        },
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Screenshot Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.Screenshot,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<Config.ScreenshotFormat>
                                                        {
                                                            Caption = NekoPlayerStrings.ScreenshotFormat,
                                                            Icon = FontAwesome.Solid.WindowMaximize,
                                                            Current = screenshotFormat,
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
                                                                    Caption = NekoPlayerStrings.ScreenshotQuality,
                                                                    Icon = FontAwesome.Solid.SlidersH,
                                                                    Current = appConfig.GetBindable<int>(NekoPlayerSetting.ScreenshotQuality),
                                                                    LabelFormat = value => $"{value}%",
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.ShowCursorInScreenshots,
                                                            Icon = FontAwesome.Solid.MousePointer,
                                                            Current = appConfig.GetBindable<bool>(NekoPlayerSetting.ScreenshotCaptureMenuCursor)
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Video Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.Video,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormEnumDropdown<AspectRatioMethod>
                                                        {
                                                            Caption = NekoPlayerStrings.AspectRatioMethod,
                                                            Icon = FontAwesome.Solid.WindowMaximize,
                                                            Current = aspectRatioMethod,
                                                            Hotkey = new Hotkey(GlobalAction.CycleAspectRatio),
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<double>
                                                        {
                                                            Caption = NekoPlayerStrings.VideoDimLevel,
                                                            Icon = FontAwesome.Regular.Sun,
                                                            Current = appConfig.GetBindable<double>(NekoPlayerSetting.VideoDimLevel),
                                                            DisplayAsPercentage = true,
                                                        }),
                                                        new SettingsItemV2(hwAccelCheckbox = new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.UseHardwareAcceleration,
                                                            Icon = FontAwesome.Solid.Bolt,
                                                        })
                                                        {
                                                            Note = { BindTarget = hwAccelNote },
                                                        },
                                                        VideoQualitySettingsCore = new SettingsItemV2(VideoQualitySettings = new YouTubeQualityDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.VideoQuality,
                                                            Icon = FontAwesome.Solid.Video,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        AudioQualitySettingsCore = new SettingsItemV2(AudioQualitySettings = new YouTubeAudioStreamDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.AudioTracks,
                                                            Icon = FontAwesome.Solid.FileAudio,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.ShowVideoMetadataOnWindowTitle,
                                                            Icon = FontAwesome.Solid.Font,
                                                            Current = showVideoMetadataOnWindowTitle,
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.ResetPlaybackSpeedWhenLoadingAVideo,
                                                            Icon = FontAwesome.Solid.TachometerAlt,
                                                            Current = appConfig.GetBindable<bool>(NekoPlayerSetting.ResetPlaybackSpeedWhenLoadingAVideo)
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "VFX Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.VisualEffects,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = NekoPlayerStrings.VideoBloomLevel,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(NekoPlayerSetting.VideoBloomLevel),
                                                            DisplayAsPercentage = true,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = NekoPlayerStrings.ChromaticAberration,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(NekoPlayerSetting.ChromaticAberrationStrength),
                                                            DisplayAsPercentage = true,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = NekoPlayerStrings.VideoGrayscaleLevel,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(NekoPlayerSetting.VideoGrayscaleLevel),
                                                            DisplayAsPercentage = true,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = NekoPlayerStrings.VideoHueShift,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = appConfig.GetBindable<float>(NekoPlayerSetting.VideoHueShift),
                                                            KeyboardStep = 1,
                                                            LabelFormat = value => $"{value:N0}°"
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "CC Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.ClosedCaptions,
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
                                                            Caption = NekoPlayerStrings.ClosedCaptions,
                                                            Icon = FontAwesome.Solid.ClosedCaptioning,
                                                            Current = captionEnabled,
                                                            Hotkey = new Hotkey(GlobalAction.CycleCaptionLanguage),
                                                        }),
                                                        advancedSubtitlesBase = new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.AdvancedSubtitleStyle,
                                                            Icon = FontAwesome.Solid.ClosedCaptioning,
                                                            Current = advancedSubtitles,
                                                        })
                                                        {
                                                            Note = { BindTarget = srv3Notice },
                                                        },
                                                        captionLangOptions = new SettingsItemV2(CaptionLangDropdown = new YouTubeI18nLangDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.CaptionLanguage,
                                                            Icon = FontAwesome.Solid.Language,
                                                        })
                                                        {
                                                            ShowRevertToDefaultButton = false,
                                                        },
                                                        new SettingsItemV2(new FormEnumFontDropdown<CaptionFonts>
                                                        {
                                                            Caption = NekoPlayerStrings.CaptionFont,
                                                            Current = caption_font,
                                                            Icon = FontAwesome.Solid.Font,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<float>
                                                        {
                                                            Caption = NekoPlayerStrings.CaptionBGOpacity,
                                                            Icon = FontAwesome.Solid.Sun,
                                                            Current = captionBGOpacity,
                                                            DisplayAsPercentage = true,
                                                        }),
                                                        new SettingsItemV2(new FormColourPicker
                                                        {
                                                            Caption = NekoPlayerStrings.CaptionBGColour,
                                                            Current = appConfig.GetBindable<Colour4>(NekoPlayerSetting.CaptionBGColor),
                                                            Icon = FontAwesome.Solid.Palette,
                                                        }),
                                                        new SettingsItemV2(new FormSliderBar<int>
                                                        {
                                                            Caption = NekoPlayerStrings.CaptionCornerRadius,
                                                            Icon = FontAwesome.Solid.SlidersH,
                                                            Current = appConfig.GetBindable<int>(NekoPlayerSetting.CaptionCornerRadius),
                                                            KeyboardStep = 1,
                                                            LabelFormat = value => $"{value}px"
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Audio Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.Audio,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(audioDeviceDropdown = new AudioDeviceDropdown
                                                        {
                                                            Caption = NekoPlayerStrings.OutputDevice,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                        })
                                                        {
                                                            Note = { BindTarget = alsaExclusiveDeviceNote },
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.AdjustPitchOnSpeedChange,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = adjustPitch,
                                                            Hotkey = new Hotkey(GlobalAction.ToggleAdjustPitchOnSpeedChange),
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.Volume,
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
                                                                /*
                                                                new SettingsItemV2(new FormVolumeSliderBar<double>
                                                                {
                                                                    Caption = NekoPlayerStrings.MasterVolume,
                                                                    Icon = FontAwesome.Solid.VolumeUp,
                                                                    Current = config.GetBindable<double>(FrameworkSetting.VolumeUniversal),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                                */
                                                                new SettingsItemV2(new FormSliderBar<double>
                                                                {
                                                                    Caption = NekoPlayerStrings.VideoVolume,
                                                                    Icon = FontAwesome.Solid.VolumeUp,
                                                                    Current = videoVolume,
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                                new SettingsItemV2(new FormSliderBar<double>
                                                                {
                                                                    Caption = NekoPlayerStrings.SFXVolume,
                                                                    Icon = FontAwesome.Solid.VolumeUp,
                                                                    Current = config.GetBindable<double>(FrameworkSetting.VolumeEffect),
                                                                    DisplayAsPercentage = true,
                                                                }),
                                                            }
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.AudioNormalization,
                                                            Icon = FontAwesome.Solid.VolumeUp,
                                                            Current = appConfig.GetBindable<bool>(NekoPlayerSetting.AudioNormalization)
                                                        }),
                                                        new ProjectYomiSpriteText
                                                        {
                                                            Name = "Debug Settings",
                                                            Font = NekoPlayerApp.DefaultFont.With(size: 30),
                                                            Text = NekoPlayerStrings.Debug,
                                                            Padding = new MarginPadding { Horizontal = 30, Vertical = 12 },
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.ShowLogOverlay,
                                                            Icon = FontAwesome.Solid.Bug,
                                                            Current = config.GetBindable<bool>(FrameworkSetting.ShowLogOverlay),
                                                            Hotkey = new Hotkey(new KeyCombination(new [] { InputKey.Control, InputKey.F10 }))
                                                        }),
                                                        new SettingsItemV2(new FormCheckBox
                                                        {
                                                            Caption = NekoPlayerStrings.BypassFTBRenderPass,
                                                            Icon = FontAwesome.Solid.Bug,
                                                            Current = debugConfig.GetBindable<bool>(DebugSetting.BypassFrontToBackPass)
                                                        }),
                                                        new SettingsItemV2(latencyModeDropdown = new FormEnumDropdown<GCLatencyMode>
                                                        {
                                                            Caption = NekoPlayerStrings.GC_Mode,
                                                            Icon = FontAwesome.Solid.Bug,
                                                        }),
                                                        new SettingsButtonV2
                                                        {
                                                            Text = NekoPlayerStrings.ClearAllCaches,
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
                                                                    Texture = textures.Get(@"NekoPlayer_Logo_20261014"),
                                                                    FillMode = FillMode.Fit,
                                                                }
                                                            },
                                                        },
                                                        new ProjectYomiTextFlowContainer(f => f.Font = NekoPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"))
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Text = "NekoPlayer",
                                                            TextAnchor = Anchor.Centre,
                                                            Colour = overlayColourProvider.Content2,
                                                        },
                                                        gameVersion = new LinkFlowContainer(f =>
                                                        {
                                                            f.Font = NekoPlayerApp.DefaultFont.With(size: 15);
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
                                                            f.Font = NekoPlayerApp.DefaultFont.With(size: 15);
                                                            f.Colour = overlayColourProvider.Content2;
                                                        })
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            TextAnchor = Anchor.Centre,
                                                        },
                                                        dislikeCounterCredits = new LinkFlowContainer(f =>
                                                        {
                                                            f.Font = NekoPlayerApp.DefaultFont.With(size: 15);
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
                                    Text = NekoPlayerStrings.Settings,
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
                                        CloseOverlayAction.Invoke();
                                    }
                                },
                                settingsTabBar = new NekoPlayerSettingsTabBar
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

            VideoQualitySettingsCore.Hide();
            AudioQualitySettingsCore.Hide();

            dislikeCounterCredits.AddText(NekoPlayerStrings.DislikeCounterCredits_1);
            dislikeCounterCredits.AddLink("Return YouTube Dislike API", "https://returnyoutubedislike.com/");
            dislikeCounterCredits.AddText(NekoPlayerStrings.DislikeCounterCredits_2);

            if (game.IsDeployedBuild)
            {
                gameVersion.AddLink(game.Version, $"https://github.com/ZeroMayo/NekoPlayer/releases/{game.Version}", tooltipText: NekoPlayerStrings.ViewChangelog(game.Version));
            }
            else
            {
                gameVersion.AddText(game.Version);
            }

            madeByText.AddText("made by ");
            madeByText.AddLink("Mayo_0x0 (ZeroMayo)", "https://github.com/ZeroMayo/", NekoPlayerStrings.ViewGitHubProfile);

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
                hwAccelNote.Value = val.NewValue ? new SettingsNote.Data(NekoPlayerStrings.HardwareAccelerationEnabledNote, SettingsNote.Type.Informational) : null;
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

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
                advancedSubtitlesBase.Hide();

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

                if (game?.RestartAppWhenExited() == true)
                {
                    game.AttemptExit();
                }
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
                    discordNotInstalledNote.Value = new SettingsNote.Data(NekoPlayerStrings.DiscordNotInstalled, SettingsNote.Type.Informational);
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
                //releaseStreamSelectorButtonCore.Hide();
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

        private void setReflexBoostNotice() => reflexNotice.Value = new SettingsNote.Data(NekoPlayerStrings.ReflexNotice, SettingsNote.Type.Informational);

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

        private SettingsItemV2 resolutionFullscreenDropdownCore, resolutionWindowedDropdownCore, displayDropdownCore, minimiseOnFocusLossCheckboxCore, releaseStreamSelectorButtonCore;

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
        private NekoPlayerApp game { get; set; }
        private NekoPlayerSettingsTabBar settingsTabBar;
        private Bindable<SettingsNote.Data> discordNotInstalledNote = new Bindable<SettingsNote.Data>();
        private LinkFlowContainer dislikeCounterCredits, madeByText, gameVersion;
        private FormEnumDropdownWithDiscordProfileImage<DiscordRichPresenceMode> discordRichPresenceDropdown;
        private FillFlowContainer volumeOptions, screenshotOptions;

        private FormEnumDropdown<GCLatencyMode> latencyModeDropdown;

        private SettingsItemV2 systemMuteSwitchBase;

        [Resolved(canBeNull: true)]
        private Online.DiscordRPC? discordRPC { get; set; }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using AXPlayer.App.Config;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Overlays;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using SDL;

namespace AXPlayer.App.Utils
{
    public unsafe partial class TrayIconManager : Component
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private VolumeOverlay volumeOverlay { get; set; } = null!;

        private IDisposable? activeTrayIcon = null!;

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        [Resolved]
        private SessionStatics statics { get; set; }

        [Resolved]
        private AudioManager audioManager { get; set; }

        private Bindable<double> muteBindable = new Bindable<double>(0);

        private Bindable<bool> trayIconVisible;

        private SDL_Surface* appIcon;

        [Resolved]
        private YouTubeAPI api { get; set; }

        [Resolved]
        private GoogleOAuth2 googleOAuth2 { get; set; }

        [Resolved]
        private AXPlayerApp app { get; set; }

        public Action OnHidedToTrayIcon;

        [BackgroundDependencyLoader]
        private void load()
        {
            trayIconVisible = statics.GetBindable<bool>(Static.WindowIsTray);
            appIcon = SDL3.SDL_LoadBMP(Directory.GetCurrentDirectory() + @"/nekoplayer_app.bmp");
        }

        public void HideToTrayIcon()
        {
            bool previousState = volumeOverlay.IsMuted.Value;

            if (host.Window is ISDLWindow window)
            {
                host.Window.Hide();
                Schedule(() => audioManager.Samples.AddAdjustment(AdjustableProperty.Volume, muteBindable));
                trayIconVisible.Value = true;

                var icon = new TrayIcon
                {
                    Label = "NekoPlayer",
                    Icon = appIcon,
                    Menu = new TrayMenuEntry[]
                    {
                        new TrayButton
                        {
                            Label = AXPlayerTrayIconStrings.OpenApp,
                            Action = () => onShow(previousState),
                        },
                        new TraySeparator(),
                        new TrayButton
                        {
                            Label = AXPlayerTrayIconStrings.OpenSettings,
                            Action = () =>
                            {
                                onShow(previousState);
                                app.AppMessageHandler.OpenSettings();
                            },
                        },
                        new TraySeparator(),
                        new TrayButton
                        {
                            Label = AXPlayerTrayIconStrings.MyPlaylists,
                            Enabled = googleOAuth2.SignedIn.Value,
                            Action = () =>
                            {
                                onShow(previousState);
                                app.AppMessageHandler.OpenMyPlaylists();
                            },
                        },
                        new TrayButton
                        {
                            Label = AXPlayerTrayIconStrings.AudioEffects,
                            Action = () =>
                            {
                                onShow(previousState);
                                app.AppMessageHandler.OpenAudioEffects();
                            },
                        },
                        new TraySeparator(),
                        new TrayButton
                        {
                            Label = AXPlayerTrayIconStrings.Quit,
                            Action = () =>
                            {
                                Schedule(() => app.AttemptExit(true));
                            },
                        },
                    }
                };

                try
                {
                    activeTrayIcon = window.CreateTrayIcon(icon);
                    OnHidedToTrayIcon?.Invoke();
                }
                catch (PlatformNotSupportedException ex)
                {
                    Logger.Log($"aaaa");
                    return;
                }

                Logger.Log($"Created notification tray icon");
            }
        }

        private void onShow(bool previousState)
        {
            Logger.Log($"Notification tray icon clicked");
            host.Window.Show();
            //host.Window.Raise();

            trayIconVisible.Value = false;
            activeTrayIcon.Dispose();
            Logger.Log($"Notification tray icon removed");
            Schedule(() => audioManager.Samples.RemoveAdjustment(AdjustableProperty.Volume, muteBindable));
        }
    }
}

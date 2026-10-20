// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using OverlayContainer = AXPlayer.App.Graphics.Containers.OverlayContainer;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        [Resolved]
        private ScreenshotManager screenshotManager { get; set; }

        private ProjectYomiTextFlowContainer infoForNerds, playlistName;

        private Bindable<float> scalingPositionX = null!;
        private Bindable<float> scalingPositionY = null!;
        private Bindable<float> scalingSizeX = null!;
        private Bindable<float> scalingSizeY = null!;

        private FormSliderBar<float> dimSlider = null!;
        private FillFlowContainer<SettingsItemV2> scalingSettings = null!;
        private Bindable<ScalingMode> scalingMode = null!;

        private bool automaticRendererInUse;

        private IBindable<bool> uiVisible;

        [Resolved]
        private SessionStatics sessionStatics { get; set; }

        private async Task checkForUpdates()
        {
            if (updateManager == null || game == null)
                return;

            // ------------------------------
            //                              |
            //                              v      bro what
            CancellationTokenSource nanahiraSingsHope = new CancellationTokenSource();

            game.UpdateManagerVersionText.Value = AXPlayerStrings.CheckingUpdate;

            settingsContainer.CheckForUpdatesButton.Enabled.Value = false;

            try
            {
                bool foundUpdate = await updateManager.CheckForUpdateAsync(nanahiraSingsHope.Token).ConfigureAwait(true);

                if (!foundUpdate)
                {
                    /*
                    alert.Text = NekoPlayerStrings.RunningLatestRelease(game.Version);
                    alert.Show();
                    */
                    if (settingsContainer.IsVisible)
                    {
                        Schedule(() =>
                        {
                            notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.CheckCircle, Color4.Green, AXPlayerStrings.RunningLatestRelease(game.Version), AXPlayerStrings.Updates));
                        });
                        /*
                        ToastBase toast = new ToastWithIcon(NekoPlayerStrings.Updates, NekoPlayerStrings.RunningLatestRelease(game.Version), FontAwesome.Solid.CheckCircle);

                        onScreenDisplay.Display(toast);
                        */
                    }

                    game.UpdateManagerVersionText.Value = game.Version;
                    settingsContainer.CheckForUpdatesButton.Enabled.Value = true;
                }
            }
            catch
            {
                game.UpdateManagerVersionText.Value = game.Version;
                settingsContainer.CheckForUpdatesButton.Enabled.Value = true;
            }
            finally
            {
            }
        }

        public override bool CursorVisible => (isControlVisible || isAnyOverlayOpen.Value || notificationOverlay.IsOpened.Value);

        private void showControls()
        {
            if (isControlVisible == false)
            {
                muteStateOverlay.Hide();
                if (appGlobalConfig.Get<PlayerControllerFadeStyle>(AXPlayerSetting.PlayerControllerFadeStyle) == PlayerControllerFadeStyle.Modern)
                {
                    isControlVisible = true;
                    uiContainer.FadeInFromZero(500, Easing.OutQuint);
                    uiGradientContainer.FadeInFromZero(500, Easing.OutQuint);
                    uiContainer.BlurTo(new Vector2(0), 500, Easing.OutQuint);
                    sessionStatics.GetBindable<bool>(Static.IsControlVisible).Value = true;
                    bottomUIContainer.MoveToY(0, 500, Easing.OutQuint);
                    topUIContainer.MoveToY(0, 500, Easing.OutQuint);
                }
                else
                {
                    isControlVisible = true;
                    uiContainer.FadeInFromZero(125);
                    uiGradientContainer.FadeInFromZero(125);
                    sessionStatics.GetBindable<bool>(Static.IsControlVisible).Value = true;
                }
            }
        }

        private void hideControls()
        {
            if (!alwaysShowControl.Value)
            {
                if (isControlVisible == true)
                {
                    playbackSpeedContainer.HidePopover();
                    videoVolumeControlContainer.HidePopover();
                    if (appGlobalConfig.Get<PlayerControllerFadeStyle>(AXPlayerSetting.PlayerControllerFadeStyle) == PlayerControllerFadeStyle.Modern)
                    {
                        isControlVisible = false;
                        uiContainer.FadeOutFromOne(250, Easing.InCubic);
                        uiGradientContainer.FadeOutFromOne(250, Easing.InCubic);
                        uiContainer.BlurTo(new Vector2(8), 250, Easing.InCubic);
                        sessionStatics.GetBindable<bool>(Static.IsControlVisible).Value = false;
                        bottomUIContainer.MoveToY(30, 250, Easing.InCubic);
                        topUIContainer.MoveToY(-30, 250, Easing.InCubic);
                    }
                    else
                    {
                        isControlVisible = false;
                        uiContainer.FadeOutFromOne(250);
                        uiGradientContainer.FadeOutFromOne(250);
                        sessionStatics.GetBindable<bool>(Static.IsControlVisible).Value = false;
                    }

                    if (videoVolume.Value == 0)
                        Scheduler.AddDelayed(() => muteStateOverlay.Show(), 250);
                }
            }
        }

        private IBindable<bool> cursorInWindow;
#nullable enable
        private IWindow? window;
#nullable disable

        private partial class RoundedSeekBar : AXPlayerSeekBar<double>
        {
            public override LocalisableString TooltipText => "";
        }

        private void updateRepeatState()
        {
            repeat.Value = !repeat.Value;
            repeatButton.SetEnabledValueLeftSide(repeat.Value);
            repeatButton.IconObject.FadeColour(repeat.Value ? bgColor : accentColor, 250, Easing.OutQuint);
            //repeatButton.TransformTo(nameof(Width), repeat.Value ? 50f : 40f, 1000, Easing.OutElastic);
        }

        private void updatePinState()
        {
            alwaysShowControl.Value = !alwaysShowControl.Value;
            pinButton.SetEnabledValueRightSide(alwaysShowControl.Value);
            pinButton.IconObject.FadeColour(alwaysShowControl.Value ? bgColor : accentColor, 250, Easing.OutQuint);
            //pinButton.TransformTo(nameof(Width), alwaysShowControl.Value ? 50f : 40f, 1000, Easing.OutElastic);
        }

#nullable enable
        private readonly Bindable<SettingsNote.Data?> windowModeDropdownNote = new Bindable<SettingsNote.Data?>();
#nullable disable

        private BindableNumber<double> playbackSpeed = new BindableNumber<double>(1)
        {
            MinValue = 0.1,
            MaxValue = 8,
            Precision = 0.01,
        };

#nullable enable
        private IDisposable? duckOperation;
#nullable disable

        private void showOverlayContainer(OverlayContainer overlayContent)
        {
            if (notificationOverlay.IsOpened.Value)
                notificationOverlay.HideOverlay();

            /*
            duckOperation = game.Duck(new DuckParameters
            {
                DuckVolumeTo = 1,
                DuckDuration = 100,
                RestoreDuration = 100,
            });
            */

            if ((overlayContent == commentsContainer) && (commentContainer.Children.Count == 0))
            {
                Sample sample = audio.Samples.Get(empty_comments_samples[Random.Shared.Next(0, empty_comments_samples.Length)]);
                sample.Play();
            }

            if (playOverlaySFX.Value)
                overlayShowSample.Play();

            videoContainer.BlurTo(new Vector2(6), 250, Easing.OutQuart);

            if (overlayContent is BottomOverlayContainer)
            {
                isAnyOverlayOpen.Value = true;
                overlayContent.IsVisible = true;
                //videoContainer.ScaleTo(1.03f, 250, Easing.OutQuart);
                overlayFadeContainer.FadeTo(0.5f, 250, Easing.OutQuart);
                overlayContent.BlurTo(new Vector2(15));
                overlayContent.BlurTo(Vector2.Zero, 500, Easing.OutExpo);
                overlayContent.Show();
                overlayContent.MoveToY(200);
                overlayContent.MoveToY(0, 500, Easing.OutExpo);
                overlayContent.FadeInFromZero(250, Easing.OutQuart);
                //overlayShowSample.Play();
            }
            else if (overlayContent is SideOverlayContainer)
            {
                isAnyOverlayOpen.Value = true;
                overlayContent.IsVisible = true;
                //videoContainer.ScaleTo(1.03f, 250, Easing.OutQuart);
                overlayFadeContainer.FadeTo(0.5f, 250, Easing.OutQuart);
                overlayContent.BlurTo(new Vector2(15));
                overlayContent.BlurTo(Vector2.Zero, 500, Easing.OutExpo);
                overlayContent.Show();
                overlayContent.MoveToX(200);
                overlayContent.MoveToX(0, 500, Easing.OutExpo);
                overlayContent.FadeInFromZero(250, Easing.OutQuart);
                //overlayShowSample.Play();
            }
            else
            {
                isAnyOverlayOpen.Value = true;
                overlayContent.IsVisible = true;
                videoScalingContainer?.BlurTo(new Vector2(4), 250, Easing.OutQuart);
                videoContainer?.BlurTo(new Vector2(4), 250, Easing.OutQuart);
                //videoContainer.ScaleTo(1.03f, 250, Easing.OutQuart);
                overlayFadeContainer.FadeTo(0.5f, 250, Easing.OutQuart);
                overlayContent.Show();
                overlayContent.ScaleTo(0.8f);
                overlayContent.ScaleTo(1f, 750, Easing.OutExpo);
                overlayContent.FadeInFromZero(250, Easing.OutQuart);
                //overlayShowSample.Play();
            }
        }

        private void refreshSFX()
        {
            if (appGlobalConfig.Get<SFXType>(AXPlayerSetting.OverlaySFXType) == SFXType.Legacy)
            {
                overlayShowSample = sampleStoreGlobal.Get(@"overlay-pop-in");
                overlayHideSample = sampleStoreGlobal.Get(@"overlay-pop-out");
            }
            else
            {
                overlayShowSample = sampleStoreGlobal.Get(@"New_Fix/overlay-pop-in");
                overlayHideSample = sampleStoreGlobal.Get(@"New_Fix/overlay-pop-out");
            }
        }

        [Resolved]
        private ISampleStore sampleStoreGlobal { get; set; }

        private void hideOverlayContainer(OverlayContainer overlayContent)
        {
            //duckOperation?.Dispose();

            if (playOverlaySFX.Value)
                overlayHideSample.Play();

            videoContainer.BlurTo(new Vector2(0), 250, Easing.OutQuart);

            if (overlayContent is BottomOverlayContainer)
            {
                overlayContent.IsVisible = false;
                isAnyOverlayOpen.Value = false;
                //overlayHideSample.Play();
                //videoContainer.ScaleTo(1f, 250, Easing.OutQuart);
                overlayFadeContainer.FadeTo(0f, 250, Easing.OutQuart);
                overlayContent.BlurTo(new Vector2(15), 500, Easing.OutExpo);
                overlayContent.MoveToY(200, 500, Easing.OutQuart);
                overlayContent.FadeOutFromOne(250, Easing.OutQuart);
            }
            else if (overlayContent is SideOverlayContainer)
            {
                overlayContent.IsVisible = false;
                isAnyOverlayOpen.Value = false;
                //overlayHideSample.Play();
                //videoContainer.ScaleTo(1f, 250, Easing.OutQuart);
                overlayFadeContainer.FadeTo(0f, 250, Easing.OutQuart);
                overlayContent.BlurTo(new Vector2(15), 500, Easing.OutExpo);
                overlayContent.MoveToX(200, 500, Easing.OutQuart);
                overlayContent.FadeOutFromOne(250, Easing.OutQuart);
            }
            else
            {
                overlayContent.IsVisible = false;
                isAnyOverlayOpen.Value = false;
                //overlayHideSample.Play();
                //videoContainer.ScaleTo(1f, 250, Easing.OutQuart);
                overlayFadeContainer.FadeTo(0f, 250, Easing.OutQuart);
                overlayContent.BlurTo(new Vector2(15), 500, Easing.OutExpo);
                overlayContent.ScaleTo(0.8f, 250, Easing.OutQuart);
                overlayContent.FadeOutFromOne(250, Easing.OutQuart);
            }
        }

        private Bindable<bool> isAnyOverlayOpen;
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using AXPlayer.App.Config;
using AXPlayer.App.Overlays.OSD;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Transforms;
using osu.Framework.Threading;
using osuTK;

namespace AXPlayer.App.Overlays
{
    /// <summary>
    /// An on-screen display which automatically tracks and displays toast notifications for <seealso cref="TrackedSettings"/>.
    /// Can also display custom content via <see cref="Display(Toast)"/>
    /// </summary>
    public partial class OnScreenDisplay : Container
    {
        private readonly BufferedContainer box;

        private Bindable<bool> videoPlaying = null!;

        public OnScreenDisplay()
        {
            AlwaysPresent = true;
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            Children = new Drawable[]
            {
                box = new BufferedContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Origin = Anchor.BottomCentre,
                    Anchor = Anchor.BottomCentre,
                    Margin = new MarginPadding(32),
                    Masking = true,
                    CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS),
                    BlurSigma = new Vector2(15),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(SessionStatics sessionStatics)
        {
            box.ScaleTo(0.8f);
            box.FadeOut();
            isAnyOverlayOpen = sessionStatics.GetBindable<bool>(Static.IsAnyOverlayOpen);
            videoPlaying = sessionStatics.GetBindable<bool>(Static.IsVideoPlaying);

            /*
            controlsVisibleState.BindValueChanged(v =>
            {
                box.MoveTo(new Vector2(0.5f, v.NewValue ? (isAnyOverlayOpen.Value ? 0.935f : 0.8f) : 0.935f), 500, Easing.OutQuint);
            }, true);

            isAnyOverlayOpen.BindValueChanged(v =>
            {
                box.MoveTo(new Vector2(0.5f, controlsVisibleState.Value ? (isAnyOverlayOpen.Value ? 0.935f : 0.8f) : 0.935f), 500, Easing.OutQuint);
            }, true);
            */
        }

        private Bindable<bool> isAnyOverlayOpen;

        private readonly Dictionary<(object, IConfigManager), TrackedSettings> trackedConfigManagers = new Dictionary<(object, IConfigManager), TrackedSettings>();

        /// <summary>
        /// Registers a <see cref="ConfigManager{T}"/> to have its settings tracked by this <see cref="OnScreenDisplay"/>.
        /// </summary>
        /// <param name="source">The object that is registering the <see cref="ConfigManager{T}"/> to be tracked.</param>
        /// <param name="configManager">The <see cref="ConfigManager{T}"/> to be tracked.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="configManager"/> is null.</exception>
        /// <exception cref="InvalidOperationException">If <paramref name="configManager"/> is already being tracked from the same <paramref name="source"/>.</exception>
        /// <returns>An object representing the registration, that may be disposed to stop tracking the <see cref="ConfigManager{T}"/>.</returns>
        public IDisposable BeginTracking(object source, ITrackableConfigManager configManager)
        {
            //Debug.Assert(ThreadSafety.IsUpdateThread);

            //ArgumentNullException.ThrowIfNull(configManager);

            if (trackedConfigManagers.ContainsKey((source, configManager)))
                throw new InvalidOperationException($"{nameof(configManager)} is already registered.");

            var trackedSettings = configManager.CreateTrackedSettings();
            if (trackedSettings == null)
                return new InvokeOnDisposal(() => { });

            configManager.LoadInto(trackedSettings);
            trackedSettings.SettingChanged += displayTrackedSettingChange;
            trackedConfigManagers.Add((source, configManager), trackedSettings);

            return new InvokeOnDisposal(() =>
            {
                trackedSettings.Unload();
                trackedSettings.SettingChanged -= displayTrackedSettingChange;
                trackedConfigManagers.Remove((source, configManager));
            });
        }

        /// <summary>
        /// Displays the provided <see cref="Toast"/> temporarily.
        /// </summary>
        /// <param name="toast"></param>
        public void Display(ToastBase toast) => Schedule(() =>
        {
            box.Child = toast;
            DisplayTemporarily(box);
        });

        private void displayTrackedSettingChange(SettingDescription description) => Scheduler.AddOnce(Display, new TrackedSettingToast(description));

        private TransformSequence<BufferedContainer> fadeIn;
        private ScheduledDelegate fadeOut;

        protected virtual void DisplayTemporarily(BufferedContainer toDisplay)
        {
            // avoid starting a new fade-in if one is already active.
            if (fadeIn == null)
            {
                fadeIn = toDisplay.Animate(
                    b => b.BlurTo(new Vector2(0), 250, Easing.OutExpo),
                    b => b.FadeIn(500, Easing.OutQuint),
                    b => b.ScaleTo(1f, 500, Easing.OutQuint)
                );

                fadeIn.Finally(_ => fadeIn = null);
            }

            fadeOut?.Cancel();
            fadeOut = Scheduler.AddDelayed(() =>
            {
                toDisplay.Animate(
                    b => b.BlurTo(new Vector2(15), 250, Easing.OutQuint),
                    b => b.FadeOutFromOne(250, Easing.OutQuint),
                    b => b.ScaleTo(0.8f, 250, Easing.OutQuint)
                );
            }, videoPlaying.Value ? 1250 : 1500);
        }
    }
}

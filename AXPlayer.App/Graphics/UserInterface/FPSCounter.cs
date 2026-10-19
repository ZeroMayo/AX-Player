// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osuTK;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Sprites;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class FPSCounter : VisibilityContainer, IHasCustomTooltip
    {
        private ProjectYomiSpriteText counterUpdateFrameTime = null!;
        private ProjectYomiSpriteText counterDrawFPS = null!;

        private Container mainContent = null!;

        private Container background = null!;

        private Container counters = null!;

        private const double min_time_between_updates = 10;

        private const double spike_time_ms = 20;

        private const float idle_background_alpha = 0.4f;

        private readonly BindableBool showFpsDisplay = new BindableBool(true);

        private double displayedFpsCount;
        private double displayedFrameTime;

        private bool isDisplayed;

        private double aimDrawFPS;
        private double aimUpdateFPS;

        private double lastUpdate;
        private ThrottledFrameClock drawClock = null!;
        private ThrottledFrameClock updateClock = null!;
        private ThrottledFrameClock inputClock = null!;

        private Box focusLayer = null!;

        /// <summary>
        /// The last time value where the display was required (due to a significant change or hovering).
        /// </summary>
        private double lastDisplayRequiredTime;

        private IBindable<bool> uiVisible;

        public FPSCounter()
        {
            AlwaysPresent = true;
            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(AXPlayerConfigManager config, GameHost gameHost, ScreenshotManager screenshotManager, OverlayColourProvider overlayColourProvider)
        {
            uiVisible = screenshotManager.CursorVisibility;

            InternalChildren = new Drawable[]
            {
                mainContent = new Container
                {
                    Alpha = 0,
                    Height = 26,
                    Children = new Drawable[]
                    {
                        background = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            CornerRadius = 5,
                            CornerExponent = 5f,
                            Masking = true,
                            Alpha = idle_background_alpha,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    Colour = overlayColourProvider.Background5,
                                    RelativeSizeAxes = Axes.Both,
                                },
                                focusLayer = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                }
                            }
                        },
                        counters = new Container
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                counterUpdateFrameTime = new ProjectYomiSpriteText
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Margin = new MarginPadding(1),
                                    Colour = overlayColourProvider.Content2,
                                    Font = AXPlayerApp.DefaultFont.With(fixedWidth: true, size: 16, weight: "SemiBold"),
                                    Spacing = new Vector2(-1),
                                    Y = -2,
                                },
                                counterDrawFPS = new ProjectYomiSpriteText
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Margin = new MarginPadding(2),
                                    Colour = overlayColourProvider.Content2,
                                    Font = AXPlayerApp.DefaultFont.With(fixedWidth: true, size: 13, weight: "SemiBold"),
                                    Spacing = new Vector2(-2),
                                    Y = 10,
                                }
                            }
                        },
                    }
                },
            };

            config.BindWith(AXPlayerSetting.ShowFpsDisplay, showFpsDisplay);

            uiVisible.BindValueChanged(visible =>
            {
                Schedule(() =>
                {
                    if (visible.NewValue)
                    {
                        if (showFpsDisplay.Value)
                            Show();
                    }
                    else
                    {
                        Hide();
                    }
                });
            });

            drawClock = gameHost.DrawThread.Clock;
            updateClock = gameHost.UpdateThread.Clock;
            inputClock = gameHost.InputThread.Clock;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            requestDisplay();

            showFpsDisplay.BindValueChanged(showFps =>
            {
                State.Value = showFps.NewValue ? Visibility.Visible : Visibility.Hidden;
                if (showFps.NewValue)
                    requestDisplay();
            }, true);

            //State.BindValueChanged(state => showFpsDisplay.Value = state.NewValue == Visibility.Visible);
        }

        protected override void PopIn() => this.FadeIn(uiVisible.Value ? 100 : 0);

        protected override void PopOut() => this.FadeOut(uiVisible.Value ? 100 : 0);

        protected override bool OnHover(HoverEvent e)
        {
            focusLayer.FadeTo(.15f, 200);
            requestDisplay();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            focusLayer.FadeTo(0, 200);
            requestDisplay();
            base.OnHoverLost(e);
        }

        protected override void Update()
        {
            base.Update();

            double elapsedDrawFrameTime = drawClock.ElapsedFrameTime;
            double elapsedUpdateFrameTime = updateClock.ElapsedFrameTime;

            // If the game goes into a suspended state (ie. debugger attached or backgrounded on a mobile device)
            // we want to ignore really long periods of no processing.
            if (elapsedUpdateFrameTime > 10000)
                return;

            mainContent.Width = Math.Max(mainContent.Width, counters.DrawWidth);

            // Handle the case where the window has become inactive or the user changed the
            // frame limiter (we want to show the FPS as it's changing, even if it isn't an outlier).
            bool aimRatesChanged = updateAimFPS();

            bool hasUpdateSpike = displayedFrameTime < spike_time_ms && elapsedUpdateFrameTime > spike_time_ms;
            // use elapsed frame time rather then FramesPerSecond to better catch stutter frames.
            bool hasDrawSpike = displayedFpsCount > (1000 / spike_time_ms) && elapsedDrawFrameTime > spike_time_ms;

            const float damp_time = 100;

            displayedFrameTime = Interpolation.DampContinuously(displayedFrameTime, elapsedUpdateFrameTime, hasUpdateSpike ? 0 : damp_time, elapsedUpdateFrameTime);

            if (hasDrawSpike)
                // show spike time using raw elapsed value, to account for `FramesPerSecond` being so averaged spike frames don't show.
                displayedFpsCount = 1000 / elapsedDrawFrameTime;
            else
                displayedFpsCount = Interpolation.DampContinuously(displayedFpsCount, drawClock.FramesPerSecond, damp_time, Time.Elapsed);

            if (Time.Current - lastUpdate > min_time_between_updates)
            {
                updateFpsDisplay();
                updateFrameTimeDisplay();

                lastUpdate = Time.Current;
            }

            bool hasSignificantChanges = aimRatesChanged
                                         || hasDrawSpike
                                         || hasUpdateSpike
                                         || displayedFpsCount < aimDrawFPS * 0.8
                                         || 1000 / displayedFrameTime < aimUpdateFPS * 0.8;

            if (hasSignificantChanges)
                requestDisplay();
            else if (isDisplayed && Time.Current - lastDisplayRequiredTime > 2000 && !IsHovered)
            {
                mainContent.FadeTo(0.7f, 300, Easing.OutQuint);
                isDisplayed = false;
            }
        }

        private void requestDisplay()
        {
            lastDisplayRequiredTime = Time.Current;

            if (!isDisplayed)
            {
                mainContent.FadeTo(1, 300, Easing.OutQuint);
                isDisplayed = true;
            }
        }

        private void updateFpsDisplay()
        {
            counterDrawFPS.Colour = getColour(displayedFpsCount / aimDrawFPS);
            counterDrawFPS.Text = $"{displayedFpsCount:#,0} fps";
        }

        private void updateFrameTimeDisplay()
        {
            counterUpdateFrameTime.Text = displayedFrameTime < 5
                ? $"{displayedFrameTime:N1} ms"
                : $"{displayedFrameTime:N0} ms";

            counterUpdateFrameTime.Colour = getColour((1000 / displayedFrameTime) / aimUpdateFPS);
        }

        private bool updateAimFPS()
        {
            if (updateClock.Throttling)
            {
                double newAimDrawFPS = drawClock.MaximumUpdateHz;
                double newAimUpdateFPS = updateClock.MaximumUpdateHz;

                if (aimDrawFPS != newAimDrawFPS || aimUpdateFPS != newAimUpdateFPS)
                {
                    aimDrawFPS = newAimDrawFPS;
                    aimUpdateFPS = newAimUpdateFPS;
                    return true;
                }
            }
            else
            {
                double newAimFPS = inputClock.MaximumUpdateHz;

                if (aimDrawFPS != newAimFPS || aimUpdateFPS != newAimFPS)
                {
                    aimUpdateFPS = aimDrawFPS = newAimFPS;
                    return true;
                }
            }

            return false;
        }

        private ColourInfo getColour(double performanceRatio)
        {
            if (performanceRatio < 0.5f)
                return Interpolation.ValueAt(performanceRatio, Color4Extensions.FromHex(@"ed1121"), Color4Extensions.FromHex(@"ebc247"), 0, 0.5);

            return Interpolation.ValueAt(performanceRatio, Color4Extensions.FromHex(@"ebc247"), Color4Extensions.FromHex(@"ccff99"), 0.5, 0.9);
        }

        public ITooltip GetCustomTooltip() => new FPSCounterTooltip();

        public object TooltipContent => this;
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Transforms;
using osu.Framework.Threading;
using osuTK;
using osuTK.Graphics;
using OverlayContainer = AXPlayer.App.Graphics.Containers.OverlayContainer;

namespace AXPlayer.App.Overlays
{
    public partial class PushNotificationOverlay : Container
    {
        private Box bg, bg2;
        private FillFlowContainer notifications, notifications2;
        private BufferedContainer content, overlayContainer;
        private ProjectYomiSpriteText titleText;
        private IconButton closeBtn;

        [Resolved]
        private AXPlayerConfigManager appGlobalConfig { get; set; }

        [Resolved]
        private ISampleStore sampleStoreGlobal { get; set; }

        private Sample overlayShowSample, overlayHideSample;

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

        private Container overlayFadeContainer;

        public PushNotificationOverlay()
        {
            AlwaysPresent = true;
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            Add(overlayFadeContainer = new OverlayFadeContainer
            {
                RelativeSizeAxes = Axes.Both,
                ClickAction = _ => HideOverlay(),
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                }
            });

            Add(overlayContainer = new OverlayContainer
            {
                Width = 396 + 32,
                RelativeSizeAxes = Axes.Y,
                Origin = Anchor.TopRight,
                Anchor = Anchor.TopRight,
                AutoSizeDuration = 400,
                AutoSizeEasing = Easing.OutQuint,
                Masking = true,
                CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0),
                Children = new Drawable[]
                {
                    new OverlayBackground
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding(16),
                        Children = new Drawable[]
                        {
                            new OverlayContainer {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Top = 48 },
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Masking = true,
                                        CornerRadius = AXPlayerApp.UI_CORNER_RADIUS + 24,
                                        Children = new Drawable[]
                                        {
                                            bg2 = new Box
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                            },
                                            new ProjectYomiScrollContainer {
                                                RelativeSizeAxes = Axes.Both,
                                                Masking = true,
                                                Children = new Drawable[]
                                                {
                                                    notifications2 = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Vertical,
                                                        Padding = new MarginPadding(8),
                                                        Spacing = new Vector2(8),
                                                        Children = Array.Empty<Drawable>(),
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                            },
                            titleText = new ProjectYomiSpriteText
                            {
                                Origin = Anchor.TopLeft,
                                Anchor = Anchor.TopLeft,
                                Text = AXPlayerStrings.Notifications,
                                Margin = new MarginPadding(2),
                                Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                            },
                            closeBtn = new IconButton
                            {
                                Enabled = { Value = true },
                                Origin = Anchor.TopRight,
                                Anchor = Anchor.TopRight,
                                Size = new Vector2(35, 35),
                                IconScale = new Vector2(0.8f),
                                Icon = FontAwesome.Solid.Times,
                                Action = () =>
                                {
                                   HideOverlay();
                                }
                            },
                        },
                    },
                }
            });

            Add(content = new BufferedContainer
            {
                Width = 396,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = AXPlayerApp.UI_CORNER_RADIUS + 24,
                Margin = new MarginPadding(16),
                Origin = Anchor.TopRight,
                Anchor = Anchor.TopRight,
                AutoSizeDuration = 400,
                AutoSizeEasing = Easing.OutQuint,
                Children = new Drawable[]
                {
                    bg = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    notifications = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(8),
                        Spacing = new Vector2(8),
                        Children = Array.Empty<Drawable>(),
                    }
                }
            });
        }

        private Bindable<SFXType> overlaySFXType;
        private Bindable<bool> playOverlaySFX;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider, AXPlayerConfigManager appConfig)
        {
            playOverlaySFX = appConfig.GetBindable<bool>(AXPlayerSetting.PlayOverlaySFX);
            overlaySFXType = appConfig.GetBindable<SFXType>(AXPlayerSetting.OverlaySFXType);
            overlayFadeContainer.Hide();

            overlaySFXType.BindValueChanged(sfx =>
            {
                refreshSFX();
            }, true);

            bg.Colour = overlayColourProvider.Background5;
            bg2.Colour = overlayColourProvider.Background4;
            titleText.Colour = overlayColourProvider.Content2;
            closeBtn.BackgroundColour = overlayColourProvider.Background4;
            content.MoveToX(100);
            content.FadeOut();
            overlayContainer.MoveToX(100);
            overlayContainer.FadeOut();
        }

        [Resolved]
        private OverlayColourProvider overlayColourProvider1 { get; set; }

        [Resolved]
        private AudioManager audio { get; set; }

        private TransformSequence<BufferedContainer> fadeIn, fadeIn2, fadeOut2;
        private ScheduledDelegate fadeOut;

        public Bindable<bool> IsOpened = new Bindable<bool>(false);

        public void OpenOverlay()
        {
            if (IsOpened.Value == false)
            {
                IsOpened.Value = true;

                if (playOverlaySFX.Value)
                    overlayShowSample.Play();

                overlayFadeContainer.FadeTo(0.5f, 250, Easing.OutQuart);

                fadeIn2 = overlayContainer.Animate(
                    b => b.BlurTo(new Vector2(0), 250, Easing.OutExpo),
                    b => b.FadeIn(250, Easing.OutQuart),
                    b => b.MoveToX(0f, 500, Easing.OutExpo)
                );

                fadeIn2.Finally(_ => fadeIn2 = null);
            }
        }

        public void HideOverlay()
        {
            if (IsOpened.Value == true)
            {
                IsOpened.Value = false;

                if (playOverlaySFX.Value)
                    overlayHideSample.Play();

                overlayFadeContainer.FadeTo(0f, 250, Easing.OutQuart);

                fadeOut2 = overlayContainer.Animate(
                    b => b.BlurTo(new Vector2(15), 250, Easing.OutExpo),
                    b => b.FadeOutFromOne(250, Easing.OutQuart),
                    b => b.MoveToX(100, 250, Easing.OutQuart)
                );

                fadeOut2.Finally(_ => fadeOut2 = null);
            }
        }

        public void Push(PushNotificationContainer container)
        {
            Schedule(() =>
            {
                Sample sample = audio.Samples.Get("NotificationPush");

                if (playOverlaySFX.Value)
                    sample.Play();

                notifications.Add(container);
                container.HideCloseButton();

                // avoid starting a new fade-in if one is already active.
                if (fadeIn == null)
                {
                    fadeIn = content.Animate(
                        b => b.BlurTo(new Vector2(0), 250, Easing.OutExpo),
                        b => b.FadeIn(500, Easing.OutQuint),
                        b => b.MoveToX(0f, 500, Easing.OutQuint)
                    );

                    fadeIn.Finally(_ => fadeIn = null);
                }

                fadeOut?.Cancel();
                fadeOut = Scheduler.AddDelayed(() =>
                {
                    content.Animate(
                        b => b.BlurTo(new Vector2(15), 250, Easing.OutQuint),
                        b => b.FadeOutFromOne(250, Easing.OutQuint),
                        b => b.MoveToX(100, 250, Easing.OutQuint)
                    );

                    foreach (PushNotificationContainer item in notifications.AliveChildren)
                    {
                        if (item != null)
                        {
                            Scheduler.AddDelayed(() =>
                            {
                                if (item.DoNotMoveToNotificationCenter)
                                {
                                    notifications.Expire();
                                }
                                else
                                {
                                    notifications.Remove(item, false);
                                    notifications2.Add(item);
                                    item.SetBackgroundColour(overlayColourProvider1.Background3);
                                    item.SetCloseBtnBGColour(overlayColourProvider1.Background2);
                                    item.ShowCloseButton();
                                }
                            }, 250);
                        }
                    }
                }, 3500);
            });
        }
    }
}

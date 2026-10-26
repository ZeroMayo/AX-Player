// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Transforms;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osuTK;
using osuTK.Graphics;
using static AXPlayer.App.AXPlayerApp;
using OverlayContainer = AXPlayer.App.Graphics.Containers.OverlayContainer;

namespace AXPlayer.App.Overlays
{
    public partial class PopupDialogOverlay : Container
    {
        private BufferedContainer overlayContainer;

        private TruncatingSpriteText titleText;
        private ProjectYomiTextFlowContainer bodyText;

        private RoundedButton acceptButton;
        private RoundedAltButton cancelButton;

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

        private OverlayFadeContainer overlayFadeContainer;

        private IDisposable? duckOperation;

        public PopupDialogOverlay()
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
                AutoSizeAxes = Axes.Y,
                Origin = Anchor.Centre,
                Anchor = Anchor.Centre,
                Masking = true,
                CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS),
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
                        Origin = Anchor.TopCentre,
                        Anchor = Anchor.TopCentre,
                        Padding = new MarginPadding(16),
                        Spacing = new Vector2(0, 4),
                        Children = new Drawable[]
                        {
                            titleText = new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Origin = Anchor.TopCentre,
                                Anchor = Anchor.TopCentre,
                                Font = AXPlayerApp.DefaultFont.With(size: 24, weight: "Bold"),
                            },
                            bodyText = new ProjectYomiTextFlowContainer(text => text.Font = AXPlayerApp.DefaultFont.With(size: 20))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Origin = Anchor.TopCentre,
                                Anchor = Anchor.TopCentre,
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
                                    cancelButton = new RoundedAltButton
                                    {
                                        Origin = Anchor.TopCentre,
                                        Anchor = Anchor.TopCentre,
                                        Enabled = { Value = true },
                                        Text = AXPlayerStrings.Cancel,
                                        Size = new Vector2(200, 40),
                                        Margin = new MarginPadding() { Top = 4, Horizontal = 4 },
                                        Action = () =>
                                        {
                                            HideOverlay();
                                        }
                                    },
                                    acceptButton = new RoundedButton
                                    {
                                        Origin = Anchor.TopCentre,
                                        Anchor = Anchor.TopCentre,
                                        Enabled = { Value = true },
                                        Text = AXPlayerStrings.Yes,
                                        Size = new Vector2(200, 40),
                                        Margin = new MarginPadding() { Top = 4, Horizontal = 4 },
                                        Action = () =>
                                        {
                                            HideOverlay();
                                        }
                                    },
                                }
                            }
                        },
                    },
                }
            });
        }

        private Bindable<SFXType> overlaySFXType;
        private Bindable<bool> playOverlaySFX;

        [Resolved]
        private AXPlayerApp game { get; set; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider, AXPlayerConfigManager appConfig, ProjectYomiColour colours)
        {
            playOverlaySFX = appConfig.GetBindable<bool>(AXPlayerSetting.PlayOverlaySFX);
            overlaySFXType = appConfig.GetBindable<SFXType>(AXPlayerSetting.OverlaySFXType);
            overlayFadeContainer.Hide();

            overlaySFXType.BindValueChanged(sfx =>
            {
                refreshSFX();
            }, true);
            overlayContainer.ScaleTo(0.9f);
            overlayContainer.FadeOut();

            titleText.Colour = overlayColourProvider.Content2;

            acceptButton.BackgroundColour = overlayColourProvider.Background3;
        }

        [Resolved]
        private OverlayColourProvider overlayColourProvider1 { get; set; }

        [Resolved]
        private AudioManager audio { get; set; }

        private TransformSequence<BufferedContainer> fadeIn, fadeIn2, fadeOut2;
        private ScheduledDelegate fadeOut;

        public Bindable<bool> IsOpened = new Bindable<bool>(false);

        public void ShowDialog(LocalisableString title, LocalisableString bodyText, LocalisableString okButtonText, LocalisableString cancelButtonText, Action? okPressedAction = null, Action? cancelPressedAction = null)
        {
            if (IsOpened.Value == false)
            {
                acceptButton.Text = okButtonText;
                cancelButton.Text = cancelButtonText;
                titleText.Text = title;
                this.bodyText.Text = bodyText;
                IsOpened.Value = true;

                if (playOverlaySFX.Value)
                    overlayShowSample.Play();

                overlayFadeContainer.FadeTo(0.5f, 250, Easing.OutQuart);

                fadeIn2 = overlayContainer.Animate(
                    b => b.BlurTo(new Vector2(0), 250, Easing.OutExpo),
                    b => b.FadeIn(250, Easing.OutQuart),
                    b => b.ScaleTo(1f, 500, Easing.OutExpo)
                );

                fadeIn2.Finally(_ => fadeIn2 = null);

                acceptButton.Action = () =>
                {
                    if (IsOpened.Value)
                    {
                        HideOverlay();
                        okPressedAction?.Invoke();
                    }
                };

                overlayFadeContainer.ClickAction = _ =>
                {
                    if (IsOpened.Value)
                    {
                        HideOverlay();
                        cancelPressedAction?.Invoke();
                    }
                };

                cancelButton.Action = () =>
                {
                    if (IsOpened.Value)
                    {
                        HideOverlay();
                        cancelPressedAction?.Invoke();
                    }
                };
            }
        }

        private void HideOverlay()
        {
            if (IsOpened.Value == true)
            {
                IsOpened.Value = false;

                if (playOverlaySFX.Value)
                    overlayHideSample.Play();

                overlayFadeContainer.FadeTo(0f, 250, Easing.OutQuart);

                fadeOut2 = overlayContainer.Animate(
                    b => b.BlurTo(new Vector2(15), 250, Easing.InExpo),
                    b => b.FadeOutFromOne(250, Easing.InQuart),
                    b => b.ScaleTo(0.9f, 250, Easing.InQuart)
                );

                fadeOut2.Finally(_ => fadeOut2 = null);
            }
        }
    }
}

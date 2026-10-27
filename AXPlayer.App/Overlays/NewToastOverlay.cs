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
    public partial class NewToastOverlay : Container
    {
        private BufferedContainer overlayContainer;
        private ProjectYomiTextFlowContainer bodyText;

        public NewToastOverlay()
        {
            AlwaysPresent = true;
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            Add(overlayContainer = new OverlayContainer
            {
                Width = 250,
                AutoSizeAxes = Axes.Y,
                Origin = Anchor.BottomCentre,
                Anchor = Anchor.BottomCentre,
                Margin = new MarginPadding(32),
                Masking = true,
                CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS),
                BlurSigma = new Vector2(15),
                Children = new Drawable[]
                {
                    new OverlayBackground
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.8f,
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
                            bodyText = new ProjectYomiTextFlowContainer(text => text.Font = AXPlayerApp.DefaultFont.With(size: 18))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Origin = Anchor.TopCentre,
                                Anchor = Anchor.TopCentre,
                                TextAnchor = Anchor.Centre,
                            },
                        },
                    },
                }
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            overlayContainer.ScaleTo(0.8f);
            overlayContainer.FadeOut();
            bodyText.Colour = overlayColourProvider.Content2;
        }
        private TransformSequence<BufferedContainer> fadeIn;
        private ScheduledDelegate fadeOut;

        public Bindable<bool> IsOpened = new Bindable<bool>(false);

        public void ShowToast(LocalisableString text)
        {
            Schedule(() =>
            {
                bodyText.Text = text;
                //notifications.Add(container);
                //container.HideCloseButton();

                // avoid starting a new fade-in if one is already active.
                if (fadeIn == null)
                {
                    fadeIn = overlayContainer.Animate(
                        b => b.BlurTo(new Vector2(0), 250, Easing.OutExpo),
                        b => b.FadeIn(500, Easing.OutQuint),
                        b => b.ScaleTo(1f, 500, Easing.OutQuint)
                    );

                    fadeIn.Finally(_ => fadeIn = null);
                }

                fadeOut?.Cancel();
                fadeOut = Scheduler.AddDelayed(() =>
                {
                    overlayContainer.Animate(
                        b => b.BlurTo(new Vector2(15), 250, Easing.OutQuint),
                        b => b.FadeOutFromOne(250, Easing.OutQuint),
                        b => b.ScaleTo(0.8f, 250, Easing.OutQuint)
                    );
                }, 2000);
            });
        }
    }
}

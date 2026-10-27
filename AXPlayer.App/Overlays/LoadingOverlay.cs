// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.UserInterface;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;

namespace AXPlayer.App.Overlays
{
    public partial class LoadingOverlay : VisibilityContainer
    {
        private OverlayFadeContainer overlayFadeContainer;

        public LoadingOverlay()
        {
            AlwaysPresent = true;
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            Add(overlayFadeContainer = new OverlayFadeContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                }
            });

            Add(spinner = new AXPlayerLoadingSpinner(true, true)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
        }

        private AXPlayerLoadingSpinner spinner;

        public Bindable<bool> IsOpened = new Bindable<bool>(false);

        protected override void PopIn()
        {
            if (IsOpened.Value == false)
            {
                IsOpened.Value = true;
                overlayFadeContainer.FadeTo(0.5f, 250, Easing.OutQuart);
                spinner.Show();
            }
        }

        protected override void PopOut()
        {
            if (IsOpened.Value == true)
            {
                IsOpened.Value = false;
                overlayFadeContainer.FadeTo(0f, 250, Easing.OutQuart);
                spinner.Hide();
            }
        }
    }
}

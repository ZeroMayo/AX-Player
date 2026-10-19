// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class SortButton : CircularContainer
    {
        private const int transition_duration = 200;

        protected override Container<Drawable> Content => content;

        private readonly Box background;
        private readonly Container content;

        public SortButton()
        {
            AutoSizeAxes = Axes.X;
            Height = 25;
            Masking = true;
            AddRangeInternal(new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
                content = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Margin = new MarginPadding { Horizontal = 10 }
                },
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background3;
        }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; }

        protected override bool OnHover(HoverEvent e)
        {
            ShowBackground();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            HideBackground();
        }

        protected void ItemFocused(bool focused) => background.FadeColour(focused ? colourProvider.Background2 : colourProvider.Background3, transition_duration, Easing.OutQuint);

        protected void ShowBackground() => background.FadeIn(transition_duration, Easing.OutQuint);

        protected void HideBackground() => background.FadeOut(transition_duration, Easing.OutQuint);
    }
}

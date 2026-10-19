// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics;
using osu.Framework.Allocation;

namespace AXPlayer.App.Overlays.OSD
{
    public partial class ToastBase : Container
    {
        private const int toast_minimum_width = 600;

        private readonly Container content;
        private readonly Box background;

        protected override Container<Drawable> Content => content;

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; } = null!;

        public ToastBase()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            // A toast's height is decided (and transformed) by the containing OnScreenDisplay.
            RelativeSizeAxes = Axes.Y;
            AutoSizeAxes = Axes.X;

            InternalChildren = new Drawable[]
            {
                new Container // this container exists just to set a minimum width for the toast
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = toast_minimum_width
                },
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 1f
                },
                content = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            background.Colour = overlayColourProvider.Background4;
        }
    }
}

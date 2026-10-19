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
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Overlays.OSD
{
    public partial class ToastWithIcon : ToastBase
    {
        /// <summary>
        /// Extra text to be shown at the bottom of the toast. Usually a key binding if available.
        /// </summary>
        public LocalisableString ExtraText
        {
            get => extraText.Text;
            set => extraText.Text = value.ToUpper();
        }

        private const int toast_minimum_width = 600;

        private readonly Container content;
        private readonly Box background;

        protected override Container<Drawable> Content => content;

        protected readonly ProjectYomiSpriteText ValueSpriteText;
        private readonly ProjectYomiSpriteText extraText, descriptionText;
        private readonly SpriteIcon spriteIcon;

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; } = null!;

        public ToastWithIcon(LocalisableString description, LocalisableString value, IconUsage icon)
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
                spriteIcon = new SpriteIcon
                {
                    Margin = new MarginPadding { Horizontal = 22, Vertical = 15 },
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Icon = icon,
                    Width = 16,
                    Height = 16,
                },
                descriptionText = new ProjectYomiSpriteText
                {
                    Padding = new MarginPadding { Horizontal = 46, Vertical = 15 },
                    Name = "Description",
                    Font = AXPlayerApp.DefaultFont.With(size: 20, weight: "ExtraBold"),
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Text = description
                },
                ValueSpriteText = new ProjectYomiSpriteText
                {
                    Font = AXPlayerApp.DefaultFont.With(size: 24, weight: "Light"),
                    Padding = new MarginPadding { Horizontal = 22, Vertical = 15 },
                    Name = "Value",
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Text = value
                },
                extraText = new ProjectYomiSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Name = "Extra Text",
                    Margin = new MarginPadding { Bottom = 15, Horizontal = 46 },
                    Font = AXPlayerApp.DefaultFont.With(size: 12, weight: "Bold"),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            descriptionText.Origin = descriptionText.Anchor = (string.IsNullOrEmpty(extraText.Text.ToString())) ? Anchor.CentreLeft : Anchor.TopLeft;
            spriteIcon.Origin = spriteIcon.Anchor = (string.IsNullOrEmpty(extraText.Text.ToString())) ? Anchor.CentreLeft : Anchor.TopLeft;
            descriptionText.Colour = ValueSpriteText.Colour = spriteIcon.Colour = overlayColourProvider.Content2;
            background.Colour = overlayColourProvider.Background4;
            extraText.Colour = overlayColourProvider.Background1;
        }
    }
}

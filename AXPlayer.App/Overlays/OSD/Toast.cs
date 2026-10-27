// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using static System.Net.Mime.MediaTypeNames;

namespace AXPlayer.App.Overlays.OSD
{
    public partial class Toast : ToastBase
    {
        /// <summary>
        /// Extra text to be shown at the bottom of the toast. Usually a key binding if available.
        /// </summary>
        public LocalisableString ExtraText
        {
            set => ExtraSpriteText.Text = value.ToUpper();
        }

        private const int toast_minimum_height = 100;

        private readonly Container content;
        private readonly Box background;

        protected override Container<Drawable> Content => content;

        protected readonly ProjectYomiTextFlowContainer ExtraSpriteText;
        private readonly ProjectYomiTextFlowContainer valueSpriteText, descriptionText;

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; } = null!;

        public Toast(LocalisableString description, LocalisableString value)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            // A toast's height is decided (and transformed) by the containing OnScreenDisplay.
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Container // this container exists just to set a minimum width for the toast
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Height = toast_minimum_height,
                    Width = 250,
                },
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.8f
                },
                content = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Direction = FillDirection.Vertical,
                    Spacing = new osuTK.Vector2(0, 8),
                    Padding = new MarginPadding(16),
                    Children = new Drawable[]
                    {
                        descriptionText = new ProjectYomiTextFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: 20, weight: "ExtraBold"))
                        {
                            AutoSizeAxes = Axes.Both,
                            Name = "Description",
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = description
                        },
                        valueSpriteText = new ProjectYomiTextFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: 24, weight: "Light"))
                        {
                            AutoSizeAxes = Axes.Both,
                            Name = "Value",
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = value
                        },
                        ExtraSpriteText = new ProjectYomiTextFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: 12, weight: "Bold"))
                        {
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Name = "Extra Text",
                        },
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            //descriptionText.Origin = descriptionText.Anchor = (string.IsNullOrEmpty(extraText.Text.ToString())) ? Anchor.CentreLeft : Anchor.TopLeft;
            descriptionText.Colour = valueSpriteText.Colour = overlayColourProvider.Content2;
            background.Colour = overlayColourProvider.Background5;
            ExtraSpriteText.Colour = overlayColourProvider.Background1;
        }
    }
}

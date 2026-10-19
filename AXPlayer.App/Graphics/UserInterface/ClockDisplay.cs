// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Graphics.Sprites;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ClockDisplay : Container
    {
        private ProjectYomiSpriteText timeText, dateText;

        public ClockDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void Update()
        {
            timeText.Text = DateTime.Now.ToString("hh:mm");
            dateText.Text = DateTime.Now.ToString("MMMM dd, dddd");
            base.Update();
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5.Opacity(0.5f))
                    }
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Padding = new MarginPadding
                            {
                                Horizontal = 32,
                                Vertical = 16
                            },
                            Direction = FillDirection.Vertical,
                            Children = new Drawable[]
                            {
                                timeText = new ProjectYomiSpriteText
                                {
                                    Font = FontUsage.Default.With("InflateVF", 100, "ClockFont"),
                                    Colour = overlayColourProvider.Content2,
                                },
                                dateText = new ProjectYomiSpriteText
                                {
                                    Font = AXPlayerApp.DefaultFont.With(size: 20),
                                },
                            }
                        }
                    }
                }
            };
        }
    }
}

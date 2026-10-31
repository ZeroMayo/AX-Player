// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class AXPlayerSettingsTabBar : CircularContainer
    {
        private Box bg;
        private FillFlowContainer items;

        public AXPlayerSettingsTabBar()
        {
            Masking = true;
            AutoSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                bg = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                items = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Margin = new MarginPadding(4),
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        new Button
                        {
                            Icon = FontAwesome.Solid.Video,
                        },
                        new Button
                        {
                            Icon = FontAwesome.Solid.VolumeUp,
                        },
                        new Button
                        {
                            Icon = FontAwesome.Solid.PaintBrush,
                        },
                        new Button
                        {
                            Icon = FontAwesome.Solid.Cog,
                        }
                    }
                }
            };
        }

        public void SetItems(Drawable[] drawables)
        {
            items.Children = drawables;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            bg.Colour = overlayColourProvider.Background4;
        }

        public partial class Button : IconButton
        {
            public Button()
            {
                Width = 40;
                Height = 40;
                Enabled.Value = true;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider overlayColourProvider)
            {
                BackgroundColour = overlayColourProvider.Background4.Opacity(0);
            }
        }
    }
}

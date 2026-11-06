// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class AXPlayerSettingsTabBar : CircularContainer
    {
        private Box bg;
        private FillFlowContainer items;

        /// <summary>
        /// Whether we should block any positional input from interacting with things behind us.
        /// </summary>
        protected virtual bool BlockPositionalInput => true;

        /// <summary>
        /// Scroll events are sometimes required to be handled differently to general positional input.
        /// This covers whether scroll events that occur within this overlay's bounds are blocked or not.
        /// Defaults to the same value as <see cref="BlockPositionalInput"/>
        /// </summary>
        protected virtual bool BlockScrollInput => BlockPositionalInput;

        /// <summary>
        /// Whether we should block any non-positional input from interacting with things behind us.
        /// </summary>
        protected virtual bool BlockNonPositionalInput => false;

        public override bool BuildNonPositionalInputQueue(List<Drawable> queue, bool allowBlocking = true)
        {
            if (PropagateNonPositionalInputSubTree && HandleNonPositionalInput && BlockNonPositionalInput)
            {
                // when blocking non-positional input behind us, we still want to make sure the global handlers receive events
                // but we don't want other drawables behind us handling them.
                queue.RemoveAll(d => !(d is IHandleGlobalKeyboardInput));
            }

            return base.BuildNonPositionalInputQueue(queue, allowBlocking);
        }

        public override bool DragBlocksClick => true;
        protected override bool OnHover(HoverEvent e) => BlockPositionalInput;
        protected override bool OnMouseDown(MouseDownEvent e) => BlockPositionalInput;
        protected override bool OnMouseMove(MouseMoveEvent e) => BlockPositionalInput;
        protected override bool OnScroll(ScrollEvent e) => BlockScrollInput && base.ReceivePositionalInputAt(e.ScreenSpaceMousePosition);

        public AXPlayerSettingsTabBar()
        {
            Masking = true;
            AutoSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                bg = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.4f,
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
                : base(HoverSampleSet.ButtonSidebar)
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

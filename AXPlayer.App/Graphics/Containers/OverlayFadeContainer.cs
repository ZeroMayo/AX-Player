// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;

namespace AXPlayer.App.Graphics.Containers
{
    public partial class OverlayFadeContainer : Container
    {
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

        public Action<OverlayFadeContainer>? ClickAction { get; set; }

        private void trigger()
        {
            ClickAction?.Invoke(this);
        }

        protected override bool OnClick(ClickEvent e)
        {
            trigger();

            return base.OnClick(e);
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Graphics.UserInterfaceV2;
using NekoPlayer.App.Localisation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;

namespace NekoPlayer.App.Graphics.UserInterfaceV3
{
    public partial class PlaybackSpeedContainer : Container, IHasPopover
    {
        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            this.ShowPopover();
            return base.OnDoubleClick(e);
        }

        public Popover GetPopover() => new ProjectYomiPopover(false)
        {
            // Ensure the popover doesn't cover up the text input.
            AllowableAnchors = [Anchor.TopCentre, Anchor.BottomCentre],
            Child = new ProjectYomiTextBox
            {

            }
        };
    }
}

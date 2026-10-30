// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class VideoVolumeControlContainer : ProjectYomiClickableContainer, IHasPopover
    {
        public Bindable<double> Current;

        protected override bool OnClick(ClickEvent e)
        {
            this.ShowPopover();
            return base.OnClick(e);
        }

        public Popover GetPopover() => new ProjectYomiPopover(false)
        {
            // Ensure the popover doesn't cover up the text input.
            AllowableAnchors = [Anchor.TopCentre, Anchor.BottomCentre],
            Child = new Container
            {
                Width = 500,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Vertical = 8 },
                Child = new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AXPlayerStrings.VideoVolume,
                    Icon = FontAwesome.Solid.VolumeUp,
                    Current = Current,
                    DisplayAsPercentage = true,
                    KeyboardStep = 0.05f,
                })
            }
        };
    }
}

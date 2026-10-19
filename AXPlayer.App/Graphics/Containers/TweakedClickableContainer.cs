// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;

namespace AXPlayer.App.Graphics.Containers
{
    public partial class TweakedClickableContainer : Container
    {
        public Action Action { get; set; }

        public readonly BindableBool Enabled = new BindableBool();

        protected override bool OnClick(ClickEvent e)
        {
            if (Enabled.Value)
                Action?.Invoke();
            return true;
        }
    }
}

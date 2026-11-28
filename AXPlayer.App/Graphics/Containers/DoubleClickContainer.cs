// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace AXPlayer.App.Graphics.Containers
{
    /// <summary>
    /// A container which invokes an action when its content is double tapped.
    /// The gesture is only active on mobile platforms by default, leaving desktop input untouched.
    /// </summary>
    public partial class DoubleClickContainer : Container
    {
        /// <summary>
        /// Invoked when the content is double clicked.
        /// </summary>
        public Action? DoubleClickAction { get; set; }

        /// <summary>
        /// Whether the double click gesture should be handled.
        /// Defaults to mobile platforms only.
        /// </summary>
        public readonly BindableBool Enabled = new BindableBool(!RuntimeInfo.IsDesktop);

        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            // The framework only raises DoubleClickEvent for a drawable which handled the preceding
            // ClickEvent, so taps have to be consumed while the gesture is enabled.
            return Enabled.Value;
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            if (!Enabled.Value || e.Button != MouseButton.Left)
                return false;

            DoubleClickAction?.Invoke();
            return true;
        }
    }
}

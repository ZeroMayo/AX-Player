// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterface
{
    public class ProjectYomiMenuItem : MenuItem
    {
        public IconUsage Icon { get; init; }

        public ProjectYomiMenuItem(LocalisableString text)
            : this(text, null)
        {
        }

        public ProjectYomiMenuItem(LocalisableString text, Action? action)
            : base(text, action)
        {
        }
    }
}

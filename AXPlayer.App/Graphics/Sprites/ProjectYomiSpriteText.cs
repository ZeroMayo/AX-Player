// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Graphics.Sprites
{
    public partial class ProjectYomiSpriteText : SpriteText
    {
        [Obsolete("Use TruncatingSpriteText instead.")]
        public new bool Truncate
        {
            set => throw new InvalidOperationException($"Use {nameof(TruncatingSpriteText)} instead.");
        }

        public ProjectYomiSpriteText(bool enableShadow = true)
        {
            Font = AXPlayerApp.DefaultFont;
            Shadow = enableShadow;
        }
    }
}

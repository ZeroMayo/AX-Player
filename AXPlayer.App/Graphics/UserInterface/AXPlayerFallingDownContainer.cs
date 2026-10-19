// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Utils;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class AXPlayerFallingDownContainer : Container
    {
        public override void Show()
        {
            var col = (Color4)Colour;
            this.FadeColour(col.Opacity(0)).FadeColour(col, 120, Easing.Out);
        }

        public override void Hide()
        {
            this.FadeOut(200);
            this.RotateTo(RNG.Next(-90, 90), 200, Easing.InQuad);
            this.MoveToX(this.X + 10, 200, Easing.InQuad);
            this.MoveToY(DrawSize.Y, 200, Easing.InQuad);
        }
    }
}

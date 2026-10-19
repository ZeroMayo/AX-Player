// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osuTK;

namespace AXPlayer.App.Graphics.Extensions
{
    public static class RectangleFExtensions
    {
        public static RectangleF Scale(this RectangleF source, float scale, Anchor origin = Anchor.Centre)
            => source.Scale(new Vector2(scale), origin);

        public static RectangleF Scale(this RectangleF source, Vector2 scale, Anchor origin = Anchor.Centre)
        {
            var newWidth = source.Width * scale.X;
            var newHeight = source.Height * scale.Y;

            var x = source.X - getXScale(origin) * (newWidth - source.Width);
            var y = source.Y - getYScale(origin) * (newHeight - source.Height);

            return new RectangleF(x, y, newWidth, newHeight);

            static float getXScale(Anchor origin)
            {
                if (origin.HasFlag(Anchor.x0))
                    return 0;

                if (origin.HasFlag(Anchor.x1))
                    return 0.5f;

                if (origin.HasFlag(Anchor.x2))
                    return 1f;

                return 100;
            }

            static float getYScale(Anchor origin)
            {
                if (origin.HasFlag(Anchor.y0))
                    return 0;

                if (origin.HasFlag(Anchor.y1))
                    return 0.5f;

                if (origin.HasFlag(Anchor.y2))
                    return 1f;

                return 100;
            }
        }
    }
}

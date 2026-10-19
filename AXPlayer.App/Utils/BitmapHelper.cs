// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using PaletteNet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AXPlayer.App.Utils
{
    // ported to SixLabors.ImageSharp
    public class BitmapHelper : IBitmapHelper
    {
        private int mResizeArea = 112 * 112;
        private int mResizeMaxDimension = -1;

        private Image<Rgba32> bitmap;

        public BitmapHelper(Image<Rgba32> bitmap)
        {
            this.bitmap = bitmap;
        }

        public int[] ScaleDownAndGetPixels()
        {
            scaleBitmapDown();

            int width = bitmap.Width;
            int height = bitmap.Height;

            int[] subsetPixels = new int[width * height];

            bitmap.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        Rgba32 p = row[x];
                        subsetPixels[y * width + x] =
                            (p.A << 24) | (p.R << 16) | (p.G << 8) | p.B; // ARGB int
                    }
                }
            });
            return subsetPixels;
        }

        private void scaleBitmapDown()
        {
            double scaleRatio = -1;

            if (mResizeArea > 0)
            {
                int bitmapArea = bitmap.Width * bitmap.Height;
                if (bitmapArea > mResizeArea)
                {
                    scaleRatio = Math.Sqrt(mResizeArea / (double)bitmapArea);
                }
            }
            else if (mResizeMaxDimension > 0)
            {
                int maxDimension = Math.Max(bitmap.Width, bitmap.Height);
                if (maxDimension > mResizeMaxDimension)
                {
                    scaleRatio = mResizeMaxDimension / (double)maxDimension;
                }
            }

            if (scaleRatio <= 0)
            {
                // Scaling has been disabled or not needed so just return the WriteableBitmap
                return;
            }

            int newWidth = (int)Math.Ceiling(bitmap.Width * scaleRatio);
            int newHeight = (int)Math.Ceiling(bitmap.Height * scaleRatio);

            bitmap.Mutate(x => x.Resize(newWidth, newHeight));
        }
    }
}

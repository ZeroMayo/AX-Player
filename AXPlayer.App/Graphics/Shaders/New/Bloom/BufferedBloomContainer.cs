// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace AXPlayer.App.Graphics.Shaders.New.Bloom
{
    public partial class BufferedBloomContainer : BufferedContainer
    {
        public BufferedBloomContainer()
        {
            DrawOriginal = true;
            EffectBlending = BlendingParameters.Additive;
            EffectPlacement = EffectPlacement.InFront;
            BlurSigma = new Vector2(40);
        }
    }
}

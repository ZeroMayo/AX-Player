// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using osu.Framework.Graphics;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Graphics
{
    public interface ISingleShaderBufferedDrawable : IBufferedDrawable
    {
        ICustomizedShader? Shader { get; }
    }
}

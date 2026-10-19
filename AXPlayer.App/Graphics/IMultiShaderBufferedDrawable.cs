// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Graphics
{
    public interface IMultiShaderBufferedDrawable : IBufferedDrawable
    {
        IReadOnlyList<ICustomizedShader> Shaders { get; }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;

namespace AXPlayer.App.Graphics.Extensions
{
    public static class RendererExtension
    {
        /// <summary>
        /// Convert the vector2 to shader vector2.
        /// Because might have different origin of uv if using different renderer.
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="vector2"></param>
        /// <returns></returns>
        public static UniformVector2 ToShaderVector2(this IRenderer renderer, Vector2 vector2)
        {
            return vector2 * new Vector2(-1, renderer.IsUvOriginTopLeft ? -1 : 1);
        }
    }
}

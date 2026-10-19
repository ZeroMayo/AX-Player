// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Shaders;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Extensions
{
    /// <summary>
    /// Holds extension methods for <see cref="IShader"/>.
    /// </summary>
    public static class ShaderExtension
    {
        /// <summary>
        /// Adjusts specified properties of a <see cref="IShader"/>.
        /// </summary>
        /// <param name="drawable">The <see cref="IShader"/> whose properties should be adjusted.</param>
        /// <param name="adjustment">The adjustment function.</param>
        /// <returns>The given <see cref="IShader"/>.</returns>
        public static T With<T>(this T drawable, Action<T> adjustment)
            where T : ICustomizedShader
        {
            adjustment(drawable);
            return drawable;
        }
    }
}

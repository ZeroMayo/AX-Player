// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Shaders;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Extensions
{
    public static class ShaderManagerExtensions
    {
        public static T LocalInternalShader<T>(this ShaderManager shaderManager) where T : InternalShader, new()
        {
            var internalShader = new T();
            shaderManager.AttachShader(internalShader);
            return internalShader;
        }

        public static void AttachShader<T>(this ShaderManager shaderManager, T internalShader) where T : InternalShader
        {
            var shader = shaderManager.Load("Customized", internalShader.ShaderName);
            internalShader.AttachOriginShader(shader);
        }
    }
}

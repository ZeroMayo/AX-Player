// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;

namespace AXPlayer.App.Graphics.Shaders
{
    /// <summary>
    /// Shader with customized property
    /// </summary>
    public abstract class CustomizedShader : ICustomizedShader
    {
        private IShader shader = null!;

        public void AttachOriginShader(IShader originShader)
        {
            shader = originShader ?? throw new ArgumentNullException(nameof(originShader));
        }

        public void Bind() => shader.Bind();

        public void Unbind() => shader.Unbind();

        public void BindUniformBlock(string blockName, IUniformBuffer buffer)
            => shader.BindUniformBlock(blockName, buffer);

        public bool IsLoaded => shader.IsLoaded;

        public bool IsBound { get; private set; }

        public abstract void ApplyValue(IRenderer renderer);

        public void Dispose()
        {
        }
    }
}

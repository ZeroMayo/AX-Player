// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.InteropServices;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;

namespace AXPlayer.App.Graphics.Shaders
{
    public class PixelShader : InternalShader, IHasTextureSize, IHasInflationPercentage
    {
        public override string ShaderName => "Pixel";

        public Vector2 Size { get; set; } = new(5);

        private IUniformBuffer<PixelParameters>? pixelParametersBuffer;

        public override void ApplyValue(IRenderer renderer)
        {
            pixelParametersBuffer ??= renderer.CreateUniformBuffer<PixelParameters>();

            pixelParametersBuffer.Data = new PixelParameters { Size = Size };

            BindUniformBlock("m_PixelParameters", pixelParametersBuffer);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct PixelParameters
        {
            public UniformVector2 Size;
            private readonly UniformPadding8 pad1;
        }
    }
}

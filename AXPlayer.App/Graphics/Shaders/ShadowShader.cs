// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;
using osuTK.Graphics;
using AXPlayer.App.Graphics.Extensions;

namespace AXPlayer.App.Graphics.Shaders
{
    public class ShadowShader : InternalShader, IApplicableToDrawRectangle, IHasTextureSize, IHasInflationPercentage
    {
        public override string ShaderName => "Shadow";

        public Color4 ShadowColour { get; set; }

        public Vector2 ShadowOffset { get; set; }

        private IUniformBuffer<ShadowParameters>? shadowParametersBuffer;

        public override void ApplyValue(IRenderer renderer)
        {
            shadowParametersBuffer ??= renderer.CreateUniformBuffer<ShadowParameters>();

            shadowParametersBuffer.Data = new ShadowParameters
            {
                Colour = new Vector4(ShadowColour.ToSRGB().R, ShadowColour.ToSRGB().G, ShadowColour.ToSRGB().B, ShadowColour.ToSRGB().A),
                Offset = renderer.ToShaderVector2(ShadowOffset),
            };

            BindUniformBlock("m_ShadowParameters", shadowParametersBuffer);
        }

        public RectangleF ComputeDrawRectangle(RectangleF originDrawRectangle)
            => originDrawRectangle.Inflate(new MarginPadding
            {
                Left = Math.Max(-ShadowOffset.X, 0),
                Right = Math.Max(ShadowOffset.X, 0),
                Top = Math.Max(-ShadowOffset.Y, 0),
                Bottom = Math.Max(ShadowOffset.Y, 0),
            });

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct ShadowParameters
        {
            public UniformVector4 Colour;
            public UniformVector2 Offset;
            private readonly UniformPadding8 pad1;
        }
    }
}

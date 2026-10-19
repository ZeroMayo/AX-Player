// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Runtime.InteropServices;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.Shaders
{
    public class OutlineShader : InternalShader, IApplicableToCharacterSize, IApplicableToDrawRectangle, IHasTextureSize, IHasInflationPercentage
    {
        public override string ShaderName => "Outline";

        public Color4 Colour { get; set; }

        public Color4 OutlineColour { get; set; }

        public float Radius { get; set; }

        private IUniformBuffer<OutlineParameters>? outlineParametersBuffer;

        public override void ApplyValue(IRenderer renderer)
        {
            outlineParametersBuffer ??= renderer.CreateUniformBuffer<OutlineParameters>();

            outlineParametersBuffer.Data = new OutlineParameters
            {
                Colour = new Vector4(Colour.ToSRGB().R, Colour.ToSRGB().G, Colour.ToSRGB().B, Colour.ToSRGB().A),
                OutlineColour = new Vector4(OutlineColour.ToSRGB().R, OutlineColour.ToSRGB().G, OutlineColour.ToSRGB().B, OutlineColour.ToSRGB().A),
                Radius = Radius,
            };

            BindUniformBlock("m_OutlineParameters", outlineParametersBuffer);
        }

        public RectangleF ComputeCharacterDrawRectangle(RectangleF originalCharacterDrawRectangle)
            => originalCharacterDrawRectangle.Inflate(Math.Max(Radius, 0));

        public RectangleF ComputeDrawRectangle(RectangleF originDrawRectangle)
            => ComputeCharacterDrawRectangle(originDrawRectangle);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct OutlineParameters
        {
            public UniformVector4 Colour;
            public UniformVector4 OutlineColour;
            public UniformFloat Radius;
            private readonly UniformPadding12 pad1;
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Runtime.InteropServices;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.Shaders
{
    public class CrtShader : InternalShader
    {
        public override string ShaderName => "CRT";

        public Color4 BackgroundColour { get; set; } = Color4.Black;

        private IUniformBuffer<CrtParameters>? crtParametersBuffer;

        public override void ApplyValue(IRenderer renderer)
        {
            crtParametersBuffer ??= renderer.CreateUniformBuffer<CrtParameters>();

            crtParametersBuffer.Data = new CrtParameters
            {
                BackgroundColour = new Vector4(BackgroundColour.ToSRGB().R, BackgroundColour.ToSRGB().G, BackgroundColour.ToSRGB().B, BackgroundColour.ToSRGB().A),
            };

            BindUniformBlock("m_CrtParameters", crtParametersBuffer);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct CrtParameters
        {
            public UniformVector4 BackgroundColour;
            private readonly UniformPadding12 pad1;
            private readonly UniformPadding4 pad2;
        }
    }
}

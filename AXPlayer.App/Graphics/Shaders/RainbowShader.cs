// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.InteropServices;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;

namespace AXPlayer.App.Graphics.Shaders
{
    public class RainbowShader : InternalShader, IHasCurrentTime
    {
        public override string ShaderName => "Rainbow";

        public Vector2 Uv { get; set; } = new(0, 1);

        public float Speed { get; set; } = 1;

        public float Saturation { get; set; } = 0.5f;

        public float Brightness { get; set; } = 1f;

        // todo: this property might able to be removed because it can be replaced by other property.
        public float Section { get; set; } = 1f;

        public float Mix { get; set; } = 0.5f;

        private IUniformBuffer<RainbowParameters>? rainbowParametersBuffer;

        public override void ApplyValue(IRenderer renderer)
        {
            rainbowParametersBuffer ??= renderer.CreateUniformBuffer<RainbowParameters>();

            rainbowParametersBuffer.Data = new RainbowParameters
            {
                Uv = Uv,
                Speed = Speed,
                Saturation = Saturation,
                Brightness = Brightness,
                Section = Section,
                Mix = Mix,
            };

            BindUniformBlock("m_RainbowParameters", rainbowParametersBuffer);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct RainbowParameters
        {
            public UniformVector2 Uv;
            public UniformFloat Speed;
            public UniformFloat Saturation;
            public UniformFloat Brightness;
            public UniformFloat Section;
            public UniformFloat Mix;
            private readonly UniformPadding4 pad1;
        }
    }
}

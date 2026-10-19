// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics.ES30;

namespace AXPlayer.App.Graphics.Shaders
{
    public class RepeatMovingBackgroundShader : InternalShader, IHasCurrentTime, IHasTextureSize
    {
        public override string ShaderName => "RepeatMovingBackground";

        public Texture? Texture { get; set; }

        public Vector2 TextureDisplaySize { get; set; } = new(10);

        public Vector2 TextureDisplayBorder { get; set; }

        public Vector2 Speed { get; set; }

        public float Mix { get; set; } = 1f;

        private IUniformBuffer<RepeatParameters>? repeatParametersBuffer;

        public override void ApplyValue(IRenderer renderer)
        {
            if (Texture == null)
                return;

            const int unit_id = TextureUnit.Texture1 - TextureUnit.Texture0;
            Texture.Bind(unit_id);

            repeatParametersBuffer ??= renderer.CreateUniformBuffer<RepeatParameters>();

            repeatParametersBuffer.Data = new RepeatParameters
            {
                RepeatSampleCoord = Texture.GetTextureRect().TopLeft,
                RepeatSampleSize = Texture.GetTextureRect().Size,
                DisplaySize = TextureDisplaySize,
                DisplayBorder = TextureDisplayBorder,
                Speed = Speed,
                Mix = Math.Clamp(Mix, 0, 1),
            };

            BindUniformBlock("m_RepeatMovingBackgroundParameters", repeatParametersBuffer);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct RepeatParameters
        {
            public UniformVector2 RepeatSampleCoord;
            public UniformVector2 RepeatSampleSize;
            public UniformVector2 DisplaySize;
            public UniformVector2 DisplayBorder;
            public UniformVector2 Speed;
            public UniformFloat Mix;
            private readonly UniformPadding4 pad1;
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.RetroTV
{
    public partial class RetroTvContainer
    {
        private class RetroTvDrawNode : ShaderDrawNode<RetroTvContainer>
        {
            private float strength;
            private float curvature;
            private float noise;
            private float time;
            private IUniformBuffer<RetroTvParameters>? parametersBuffer;

            public RetroTvDrawNode(RetroTvContainer source, BufferedDrawNodeSharedData sharedData)
                : base(source, sharedData)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                strength = Math.Clamp(Source.Strength, 0f, 1f);
                curvature = Math.Clamp(Source.Curvature, 0f, 1f);
                noise = Math.Clamp(Source.Noise, 0f, 1f);
                time = Source.ElapsedTime;
            }

            protected override void PopulateContents(IRenderer renderer)
            {
                base.PopulateContents(renderer);

                if (strength > 0f)
                    drawFrameBuffer(renderer);
            }

            private void drawFrameBuffer(IRenderer renderer)
            {
                parametersBuffer ??= renderer.CreateUniformBuffer<RetroTvParameters>();

                IFrameBuffer current = SharedData.CurrentEffectBuffer;
                IFrameBuffer target = SharedData.GetNextEffectBuffer();

                renderer.SetBlend(BlendingParameters.None);

                using (BindFrameBuffer(target))
                {
                    parametersBuffer.Data = new RetroTvParameters
                    {
                        TexSize = current.Size,
                        Strength = strength,
                        Curvature = curvature,
                        Noise = noise,
                        Time = time,
                    };

                    Shader.BindUniformBlock("m_RetroTvParameters", parametersBuffer);
                    Shader.Bind();
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    Shader.Unbind();
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                parametersBuffer?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct RetroTvParameters
            {
                public UniformVector2 TexSize;
                public UniformFloat Strength;
                public UniformFloat Curvature;
                public UniformFloat Noise;
                public UniformFloat Time;
                private readonly UniformPadding4 pad1;
                private readonly UniformPadding4 pad2;
            }
        }
    }
}
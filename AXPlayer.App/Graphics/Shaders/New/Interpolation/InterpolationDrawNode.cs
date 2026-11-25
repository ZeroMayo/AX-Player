// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;

namespace AXPlayer.App.Graphics.Shaders.New.Interpolation
{
    public partial class InterpolationContainer
    {
        private class InterpolationDrawNode : ShaderDrawNode<InterpolationContainer>
        {
            private const int history_texture_unit = TextureUnit.Texture1 - TextureUnit.Texture0;

            private float strength;
            private Vector2 historySize;
            private bool historyValid;
            private IFrameBuffer? history;
            private IUniformBuffer<InterpolationParameters>? parametersBuffer;

            public InterpolationDrawNode(InterpolationContainer source, BufferedDrawNodeSharedData sharedData)
                : base(source, sharedData)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                strength = Math.Clamp(Source.Strength, 0f, 1f);
            }

            protected override void PopulateContents(IRenderer renderer)
            {
                base.PopulateContents(renderer);

                if (strength <= 0f)
                {
                    // Drop the history so re-enabling the effect starts from a clean frame.
                    historyValid = false;
                    return;
                }

                IFrameBuffer current = SharedData.CurrentEffectBuffer;

                if (current.Size.X <= 0 || current.Size.Y <= 0)
                    return;

                if (current.Size != historySize)
                {
                    historySize = current.Size;
                    historyValid = false;
                }

                IFrameBuffer target = SharedData.GetNextEffectBuffer();

                parametersBuffer ??= renderer.CreateUniformBuffer<InterpolationParameters>();
                history ??= renderer.CreateFrameBuffer();

                IUniformBuffer<InterpolationParameters> parameters = parametersBuffer;
                IFrameBuffer historyBuffer = history;

                renderer.SetBlend(BlendingParameters.None);

                using (BindFrameBuffer(target))
                {
                    parameters.Data = new InterpolationParameters
                    {
                        TexSize = current.Size,
                        Strength = strength,
                        HistoryValid = historyValid ? 1f : 0f,
                        Radius = Math.Clamp(MathF.Min(current.Size.X, current.Size.Y) * 0.003f, 1.5f, 6f),
                    };

                    // Always leave a valid texture on the second unit: some drivers warn about an
                    // incomplete sampler even when the shader does not reach the history sample.
                    (historyValid ? historyBuffer.Texture : current.Texture).Bind(history_texture_unit);

                    Shader.BindUniformBlock("m_InterpolationParameters", parameters);
                    Shader.Bind();
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    Shader.Unbind();
                }

                // Store the source frame rather than the interpolated result. On the next display
                // update the shader therefore always compares two actual video frames, and a held
                // frame matches itself perfectly and passes through without any softening.
                // The texture shader is used instead of the effect shader to avoid a read/write
                // hazard on the history texture.
                using (BindFrameBuffer(historyBuffer))
                {
                    BindTextureShader(renderer);
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    UnbindTextureShader(renderer);
                }

                historyValid = true;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                parametersBuffer?.Dispose();
                history?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct InterpolationParameters
            {
                public UniformVector2 TexSize;
                public UniformFloat Strength;
                public UniformFloat HistoryValid;
                public UniformFloat Radius;
                private readonly UniformPadding4 pad1;
                private readonly UniformPadding4 pad2;
                private readonly UniformPadding4 pad3;
            }
        }
    }
}

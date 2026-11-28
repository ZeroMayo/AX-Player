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

namespace AXPlayer.App.Graphics.Shaders.New.MotionBlur
{
    public partial class MotionBlurContainer
    {
        private class MotionBlurDrawNode : ShaderDrawNode<MotionBlurContainer>
        {
            private const int history_texture_unit = TextureUnit.Texture1 - TextureUnit.Texture0;

            private float strength;
            private Vector2 historySize;
            private bool historyValid;
            private IFrameBuffer? history;
            private IUniformBuffer<MotionBlurParameters>? parametersBuffer;

            public MotionBlurDrawNode(MotionBlurContainer source, BufferedDrawNodeSharedData sharedData)
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

                parametersBuffer ??= renderer.CreateUniformBuffer<MotionBlurParameters>();
                history ??= renderer.CreateFrameBuffer();

                IUniformBuffer<MotionBlurParameters> parameters = parametersBuffer;
                IFrameBuffer historyBuffer = history;

                renderer.SetBlend(BlendingParameters.None);

                using (BindFrameBuffer(target))
                {
                    parameters.Data = new MotionBlurParameters
                    {
                        TexSize = current.Size,
                        Strength = strength,
                        HistoryValid = historyValid ? 1f : 0f,
                        // Search radius scales with the frame so the captured motion is the same
                        // fraction of the screen at every video resolution. The searched range is
                        // twice this, which comfortably covers a fast pan without a full optical
                        // flow pass.
                        Radius = Math.Clamp(MathF.Min(current.Size.X, current.Size.Y) * 0.005f, 2f, 8f),
                    };

                    // Always leave a valid texture on the second unit: some drivers warn about an
                    // incomplete sampler even when the shader does not reach the history sample.
                    (historyValid ? historyBuffer.Texture : current.Texture).Bind(history_texture_unit);

                    Shader.BindUniformBlock("m_MotionBlurParameters", parameters);
                    Shader.Bind();
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    Shader.Unbind();
                }

                // Store the source frame rather than the blurred result. The next display update
                // therefore always matches a sharp frame against a sharp frame, which keeps the
                // motion estimate stable instead of progressively smearing it into itself.
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
            private record struct MotionBlurParameters
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

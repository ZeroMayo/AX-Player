// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Graphics
{
    public abstract class CustomizedShaderBufferedDrawNode : BufferedDrawNode
    {
        private readonly double loadTime;

        private readonly Action<TexturedVertex2D> addVertexAction;

        protected CustomizedShaderBufferedDrawNode(IBufferedDrawable source, DrawNode child, BufferedDrawNodeSharedData sharedData)
            : base(source, child, sharedData)
        {
            loadTime = Source.Clock.CurrentTime;

            addVertexAction = v =>
            {
                sharedQuadBatch!.Add(new SharedVertex
                {
                    Position = v.Position,
                    TexturePosition = v.TexturePosition,
                });
            };
        }

        protected static bool ContainTimePropertyShader(ICustomizedShader shader)
        {
            switch (shader)
            {
                case IHasCurrentTime:
                case IStepShader stepShader when stepShader.StepShaders.Any(s => s is IHasCurrentTime):
                    return true;

                default:
                    return false;
            }
        }

        protected void ResetDrawVersion()
        {
            // todo : use better way to reset draw version.
            var prop = typeof(BufferedDrawNodeSharedData).GetField("DrawVersion", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new NotSupportedException("Underlying data structure has changed, KaraokeFont needs an update");
            prop.SetValue(SharedData, -1);
        }

        private IUniformBuffer<SharedParameters>? sharedParametersBuffer;
        private IVertexBatch<SharedVertex>? sharedQuadBatch;

        protected void RenderShader(IRenderer renderer, ICustomizedShader shader, IFrameBuffer current, IFrameBuffer target)
        {
            sharedParametersBuffer ??= renderer.CreateUniformBuffer<SharedParameters>();
            sharedQuadBatch ??= renderer.CreateQuadBatch<SharedVertex>(1, 1);

            renderer.SetBlend(BlendingParameters.None);

            using (BindFrameBuffer(target))
            {
                if (shader is IHasTextureSize)
                {
                    var size = current.Size;
                    sharedParametersBuffer.Data = sharedParametersBuffer.Data with
                    {
                        TexSize = size,
                    };
                }

                if (shader is IHasInflationPercentage)
                {
                    var localInflationAmount = DrawInfo.Matrix.ExtractScale().X;
                    sharedParametersBuffer.Data = sharedParametersBuffer.Data with
                    {
                        InflationPercentage = localInflationAmount,
                    };
                }

                if (shader is IHasCurrentTime)
                {
                    var currentTime = (float)(Source.Clock.CurrentTime - loadTime) / 1000;
                    sharedParametersBuffer.Data = sharedParametersBuffer.Data with
                    {
                        Time = currentTime,
                    };
                }

                if (shader is ICustomizedShader customizedShader)
                    customizedShader.ApplyValue(renderer);

                shader.BindUniformBlock("m_SharedParameters", sharedParametersBuffer);
                shader.Bind();

                renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White), addVertexAction);

                shader.Unbind();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            sharedParametersBuffer?.Dispose();
            sharedQuadBatch?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private record struct SharedParameters
        {
            public UniformVector2 TexSize;
            public UniformFloat InflationPercentage;
            public UniformFloat Time;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SharedVertex : IEquatable<SharedVertex>, IVertex
        {
            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 Position;

            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 TexturePosition;

            public readonly bool Equals(SharedVertex other) =>
                Position.Equals(other.Position)
                && TexturePosition.Equals(other.TexturePosition);
        }
    }
}

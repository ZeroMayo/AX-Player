// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.Videos
{
    public partial class VideoFrameInterpolationContainer : Container, IBufferedDrawable
    {
        private readonly FrameInterpolationSharedData sharedData = new();
        private IShader textureShader = null!;
        private IShader interpolationShader = null!;
        private bool enabled;

        public bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value)
                    return;

                enabled = value;
                sharedData.ResetHistory();
                Invalidate(Invalidation.DrawNode);
            }
        }

        public Color4 BackgroundColour => new(0, 0, 0, 0);
        public DrawColourInfo? FrameBufferDrawColour => base.DrawColourInfo;
        public Vector2 FrameBufferScale => Vector2.One;
        public IShader TextureShader => textureShader;

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            textureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            interpolationShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "VideoFrameInterpolation");
        }

        protected override DrawNode CreateDrawNode()
            => new VideoFrameInterpolationDrawNode(this, sharedData);

        public void ResetHistory() => sharedData.ResetHistory();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            sharedData.Dispose();
        }

        private sealed class FrameInterpolationSharedData : BufferedDrawNodeSharedData
        {
            private readonly BufferedDrawNodeSharedData historyData = new(
                TexturePixelFormat.R8G8B8A8Float,
                null,
                false,
                true);

            private bool historyInitialised;

            public bool HasHistory { get; set; }
            public IFrameBuffer HistoryBuffer => historyData.MainBuffer;

            public FrameInterpolationSharedData()
                : base(
                    1,
                    TexturePixelFormat.R8G8B8A8Float,
                    null,
                    false,
                    true)
            {
            }

            public void InitialiseHistory(IRenderer renderer)
            {
                if (historyInitialised)
                    return;

                historyData.Initialise(renderer);
                historyInitialised = true;
            }

            public void ResetHistory() => HasHistory = false;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                historyData.Dispose();
            }
        }

        private sealed class VideoFrameInterpolationDrawNode : BufferedDrawNode, ICompositeDrawNode
        {
            private new VideoFrameInterpolationContainer Source => (VideoFrameInterpolationContainer)base.Source;
            private new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            private readonly FrameInterpolationSharedData interpolationData;
            private bool isEnabled;
            private IShader shader = null!;
            private IUniformBuffer<InterpolationParameters>? parametersBuffer;

            public List<DrawNode>? Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;

            public VideoFrameInterpolationDrawNode(VideoFrameInterpolationContainer source, FrameInterpolationSharedData sharedData)
                : base(source, new CompositeDrawableDrawNode(source), sharedData)
            {
                interpolationData = sharedData;
            }

            public override void ApplyState()
            {
                base.ApplyState();
                isEnabled = Source.Enabled;
                shader = Source.interpolationShader;
            }

            protected override void PopulateContents(IRenderer renderer)
            {
                base.PopulateContents(renderer);

                if (!isEnabled)
                    return;

                interpolationData.InitialiseHistory(renderer);

                IFrameBuffer current = SharedData.MainBuffer;
                IFrameBuffer target = SharedData.GetNextEffectBuffer();
                IFrameBuffer history = interpolationData.HistoryBuffer;

                renderer.SetBlend(BlendingParameters.None);

                using (BindFrameBuffer(target))
                {
                    if (interpolationData.HasHistory && history.Size == current.Size)
                    {
                        parametersBuffer ??= renderer.CreateUniformBuffer<InterpolationParameters>();
                        parametersBuffer.Data = parametersBuffer.Data with { TexSize = current.Size };

                        renderer.BindTexture(history.Texture, 1);
                        shader.BindUniformBlock("m_InterpolationParameters", parametersBuffer);
                        shader.Bind();
                        renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                        shader.Unbind();
                    }
                    else
                    {
                        renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    }
                }

                using (BindFrameBuffer(history))
                {
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                }

                interpolationData.HasHistory = true;
            }

            protected override void DrawContents(IRenderer renderer)
                => renderer.DrawFrameBuffer(SharedData.CurrentEffectBuffer, DrawRectangle, DrawColourInfo.Colour);

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                parametersBuffer?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct InterpolationParameters
            {
                public UniformVector2 TexSize;
                private readonly UniformPadding4 pad1;
            }
        }
    }
}

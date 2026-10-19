// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shaders;
using osuTK;
using osuTK.Graphics;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Graphics.Videos
{
    public partial class VideoShaderContainer : Container, IMultiShaderBufferedDrawable
    {
        public IShader TextureShader { get; private set; } = null!;

        private readonly List<ICustomizedShader> shaders = new();

        // todo: should have a better way to let user able to customize formats?
        private readonly MultiShaderBufferedDrawNodeSharedData sharedData = new();

        public IReadOnlyList<ICustomizedShader> Shaders
        {
            get => shaders;
            set
            {
                shaders.Clear();
                shaders.AddRange(value);
                Invalidate(Invalidation.DrawNode);
            }
        }

        public Color4 BackgroundColour => new(0, 0, 0, 0);
        public DrawColourInfo? FrameBufferDrawColour => base.DrawColourInfo;
        public Vector2 FrameBufferScale => Vector2.One;

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaderManager)
        {
            TextureShader = shaderManager.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        protected override DrawNode CreateDrawNode()
           => new VideoShaderContainerShaderEffectDrawNode(this, sharedData);

        /// <summary>
        /// <see cref="BufferedDrawNode"/> to apply <see cref="IShader"/>.
        /// </summary>
        protected class VideoShaderContainerShaderEffectDrawNode : MultiShaderBufferedDrawNode, ICompositeDrawNode
        {
            protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            public VideoShaderContainerShaderEffectDrawNode(VideoShaderContainer source, MultiShaderBufferedDrawNodeSharedData sharedData)
                : base(source, new CompositeDrawableDrawNode(source), sharedData)
            {
            }

            public List<DrawNode>? Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;
        }
    }
}

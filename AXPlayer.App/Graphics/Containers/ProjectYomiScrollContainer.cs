// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace AXPlayer.App.Graphics.Containers
{
    public partial class ProjectYomiScrollContainer : AdaptiveScrollContainer<Drawable>
    {
        public ProjectYomiScrollContainer()
        {
        }

        public ProjectYomiScrollContainer(Direction direction)
            : base(direction)
        {
        }
    }

    public partial class AdaptiveScrollContainer<T> : ScrollContainer<T>, IBufferedDrawable
        where T : Drawable
    {
        public const float SCROLL_BAR_WIDTH = 10;
        public const float SCROLL_BAR_PADDING = 3;
        private const float EDGE_BLUR_SIGMA = 10;
        private const int EDGE_BLUR_RADIUS = 30;

        private IShader edgeBlurShader = null!;
        private readonly BufferedDrawNodeSharedData sharedData = new(effectBufferCount: 2, textureFormat: TexturePixelFormat.R8G8B8A8Float, null, false, true);

        private bool progressiveBlurEnabled = true;
        private float progressiveBlurTopHeight = 76;
        private float progressiveBlurBottomHeight = 76;

        public bool ProgressiveBlurEnabled
        {
            get => progressiveBlurEnabled;
            set
            {
                if (progressiveBlurEnabled == value)
                    return;

                progressiveBlurEnabled = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        public float ProgressiveBlurTopHeight
        {
            get => progressiveBlurTopHeight;
            set
            {
                value = Math.Max(0, value);

                if (progressiveBlurTopHeight == value)
                    return;

                progressiveBlurTopHeight = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        public float ProgressiveBlurBottomHeight
        {
            get => progressiveBlurBottomHeight;
            set
            {
                value = Math.Max(0, value);

                if (progressiveBlurBottomHeight == value)
                    return;

                progressiveBlurBottomHeight = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        public IShader TextureShader { get; private set; } = null!;
        public Color4 BackgroundColour => new(0, 0, 0, 0);
        public DrawColourInfo? FrameBufferDrawColour => base.DrawColourInfo;
        public Vector2 FrameBufferScale => Vector2.One;

        public AdaptiveScrollContainer(Direction scrollDirection = Direction.Vertical)
            : base(scrollDirection)
        {
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            edgeBlurShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, "ScrollEdgeBlur");
        }

        protected override DrawNode CreateDrawNode()
            => Masking ? new EdgeBlurDrawNode(this, sharedData) : base.CreateDrawNode();

        protected override void Update()
        {
            base.Update();
            Invalidate(Invalidation.DrawNode);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            sharedData.Dispose();
        }

        private class EdgeBlurDrawNode : BufferedDrawNode, ICompositeDrawNode
        {
            private readonly AdaptiveScrollContainer<T> source;
            private readonly BufferedDrawNodeSharedData sharedData;
            private IUniformBuffer<EdgeBlurParameters>? parametersBuffer;
            private IShader shader = null!;

            private CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            public List<DrawNode> Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;

            private bool progressiveBlurEnabled;
            private float progressiveBlurTopHeight;
            private float progressiveBlurBottomHeight;

            public EdgeBlurDrawNode(AdaptiveScrollContainer<T> source, BufferedDrawNodeSharedData sharedData)
                : base(source, new CompositeDrawableDrawNode(source), sharedData)
            {
                this.source = source;
                this.sharedData = sharedData;
            }

            public override void ApplyState()
            {
                base.ApplyState();
                shader = source.edgeBlurShader;
                progressiveBlurEnabled = source.ProgressiveBlurEnabled;
                progressiveBlurTopHeight = source.ProgressiveBlurTopHeight;
                progressiveBlurBottomHeight = source.ProgressiveBlurBottomHeight;
            }

            protected override void PopulateContents(IRenderer renderer)
            {
                base.PopulateContents(renderer);

                if (!progressiveBlurEnabled)
                    return;

                drawFrameBuffer(renderer, new Vector2(1, 0));
                drawFrameBuffer(renderer, new Vector2(0, 1));
            }

            private void drawFrameBuffer(IRenderer renderer, Vector2 blurDirection)
            {
                IFrameBuffer current = sharedData.CurrentEffectBuffer;
                IFrameBuffer target = sharedData.GetNextEffectBuffer();
                parametersBuffer ??= renderer.CreateUniformBuffer<EdgeBlurParameters>();
                parametersBuffer.Data = parametersBuffer.Data with
                {
                    TexSize = current.Size,
                    Radius = EDGE_BLUR_RADIUS,
                    Sigma = EDGE_BLUR_SIGMA,
                    BlurDirection = blurDirection,
                    StartEdgeSize = progressiveBlurTopHeight,
                    EndEdgeSize = progressiveBlurBottomHeight,
                    EdgeAxis = source.ScrollDim,
                };

                renderer.SetBlend(BlendingParameters.None);

                using (BindFrameBuffer(target))
                {
                    shader.BindUniformBlock("m_ScrollEdgeBlurParameters", parametersBuffer);
                    shader.Bind();
                    renderer.DrawFrameBuffer(current, new RectangleF(0, 0, current.Texture.Width, current.Texture.Height), ColourInfo.SingleColour(Color4.White));
                    shader.Unbind();
                }
            }

            protected override void DrawContents(IRenderer renderer)
            {
                renderer.DrawFrameBuffer(sharedData.CurrentEffectBuffer, DrawRectangle, DrawColourInfo.Colour);
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                parametersBuffer?.Dispose();
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct EdgeBlurParameters
            {
                public UniformVector2 TexSize;
                public UniformInt Radius;
                public UniformFloat Sigma;
                public UniformVector2 BlurDirection;
                public UniformFloat StartEdgeSize;
                public UniformFloat EndEdgeSize;
                public UniformInt EdgeAxis;
                private readonly UniformPadding12 padding;
            }
        }

        /// <summary>
        /// Scrolls a <see cref="Drawable"/> into view.
        /// </summary>
        /// <param name="d">The <see cref="Drawable"/> to scroll into view.</param>
        /// <param name="animated">Whether to animate the movement.</param>
        /// <param name="extraScroll">An added amount to scroll beyond the requirement to bring the target into view.</param>
        public void ScrollIntoView(Drawable d, bool animated = true, float extraScroll = 0)
        {
            double childPos0 = GetChildPosInContent(d);
            double childPos1 = GetChildPosInContent(d, d.DrawSize);

            double minPos = Math.Min(childPos0, childPos1);
            double maxPos = Math.Max(childPos0, childPos1);

            if (minPos < Current || (minPos > Current && d.DrawSize[ScrollDim] > DisplayableContent))
                ScrollTo(minPos - extraScroll, animated);
            else if (maxPos > Current + DisplayableContent)
                ScrollTo(maxPos - DisplayableContent + extraScroll, animated);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // allow for controlling volume when alt is held.
            // mostly for compatibility with osu-stable.
            if (e.AltPressed) return false;

            return base.OnScroll(e);
        }

        #region Absolute scrolling

        /// <summary>
        /// Controls the rate with which the target position is approached when performing a relative drag. Default is 0.02.
        /// </summary>
        public double DistanceDecayOnAbsoluteScroll = 0.02;

        protected virtual void ScrollToAbsolutePosition(Vector2 screenSpacePosition)
        {
            float fromScrollbarPosition = FromScrollbarPosition(ToLocalSpace(screenSpacePosition)[ScrollDim]);
            float scrollbarCentreOffset = FromScrollbarPosition(Scrollbar.DrawHeight) * 0.5f;

            ScrollTo(Clamp(fromScrollbarPosition - scrollbarCentreOffset), true, DistanceDecayOnAbsoluteScroll);
        }

        #endregion

        protected override ScrollbarContainer CreateScrollbar(Direction direction) => new AdaptiveScrollbar(direction);

        protected partial class AdaptiveScrollbar : ScrollbarContainer
        {
            private Color4 hoverColour;
            private Color4 defaultColour;
            private Color4 highlightColour;

            private readonly Box box;

            protected override float MinimumDimSize => SCROLL_BAR_WIDTH * 3;

            public AdaptiveScrollbar(Direction scrollDir)
                : base(scrollDir)
            {
                Blending = BlendingParameters.Additive;

                CornerRadius = 5;

                // needs to be set initially for the ResizeTo to respect minimum size
                Size = new Vector2(SCROLL_BAR_WIDTH);

                const float margin = 3;

                Margin = new MarginPadding
                {
                    Left = scrollDir == Direction.Vertical ? margin : 0,
                    Right = scrollDir == Direction.Vertical ? margin : 0,
                    Top = scrollDir == Direction.Horizontal ? margin : 0,
                    Bottom = scrollDir == Direction.Horizontal ? margin : 0,
                };

                Masking = true;
                Child = box = new Box { RelativeSizeAxes = Axes.Both };
            }

            [BackgroundDependencyLoader(true)]
            private void load()
            {
                Colour = defaultColour = Color4Extensions.FromHex(@"888");
                hoverColour = Color4Extensions.FromHex(@"fff");
                highlightColour = Color4Extensions.FromHex(@"88b300");
            }

            public override void ResizeTo(float val, int duration = 0, Easing easing = Easing.None)
            {
                this.ResizeTo(new Vector2(SCROLL_BAR_WIDTH)
                {
                    [(int)ScrollDirection] = val
                }, duration, easing);
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.FadeColour(hoverColour, 100);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.FadeColour(defaultColour, 100);
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (!base.OnMouseDown(e)) return false;

                // note that we are changing the colour of the box here as to not interfere with the hover effect.
                box.FadeColour(highlightColour, 100);
                return true;
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                if (e.Button != MouseButton.Left) return;

                box.FadeColour(Color4.White, 100);

                base.OnMouseUp(e);
            }
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;

namespace AXPlayer.App.Graphics.Containers
{
    /// <summary>
    /// It's a container that only masking target direction.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public partial class MaskingContainer<T> : Container<T> where T : Drawable
    {
        /// <summary>
        /// If enabled, only the portion of children that falls within this <see cref="Container"/>'s
        /// shape is drawn to the screen.
        /// </summary>
        public new bool Masking
        {
            get => throw new InvalidOperationException("Should not get masking in here.");
            set => throw new InvalidOperationException("Should not assign masking in here.");
        }

        private Edges maskingEdges = Edges.None;

        public Edges MaskingEdges
        {
            get => maskingEdges;
            set
            {
                if (maskingEdges == value)
                    return;

                maskingEdges = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        protected override DrawNode CreateDrawNode()
            => new MaskingCompositeDrawableDrawNode(this);

        protected class MaskingCompositeDrawableDrawNode : CompositeDrawableDrawNode
        {
            protected new MaskingContainer<T> Source => (MaskingContainer<T>)base.Source;

            /// <summary>
            /// Information about how masking of children should be carried out.
            /// </summary>
            private MaskingInfo? maskingInfo;

            public MaskingCompositeDrawableDrawNode(CompositeDrawable source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                maskingInfo = Source.MaskingEdges == Edges.None
                    ? null
                    : new MaskingInfo
                    {
                        ScreenSpaceAABB = generateMasking(Source.ScreenSpaceDrawQuad.AABB, Source.MaskingEdges),
                    };
            }

            private static RectangleI generateMasking(RectangleI source, Edges edges)
            {
                return source.Inflate(getExtendSize(Edges.Left), getExtendSize(Edges.Right), getExtendSize(Edges.Top), getExtendSize(Edges.Bottom));

                int getExtendSize(Edges flag)
                {
                    const int extend_size = 1000;
                    return edges.HasFlag(flag) ? 0 : extend_size;
                }
            }

            protected override void Draw(IRenderer renderer)
            {
                // will not working if not adding masking info into here.
                if (maskingInfo != null)
                    renderer.PushMaskingInfo(maskingInfo.Value);

                base.Draw(renderer);

                if (maskingInfo != null)
                    renderer.PopMaskingInfo();
            }

            protected override void DrawOpaqueInterior(IRenderer renderer)
            {
                // will have black border if not adding masking info into here.
                if (maskingInfo != null)
                    renderer.PushMaskingInfo(maskingInfo.Value);

                base.DrawOpaqueInterior(renderer);

                if (maskingInfo != null)
                    renderer.PopMaskingInfo();
            }
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace AXPlayer.App.Graphics.Shaders.New
{
    public partial class ShaderTransformHandler : CompositeComponent, IHasStrength
    {
        public override bool RemoveCompletedTransforms => false;

        public ShaderType Type => shader.Type;

        private VideoNewShaderContainer shader { get; }

        public float Strength
        {
            get => shader.Strength;
            set => shader.Strength = value;
        }

        public float Strength2
        {
            get => shader.Strength2;
            set => shader.Strength2 = value;
        }

        public float Strength3
        {
            get => shader.Strength3;
            set => shader.Strength3 = value;
        }

        public ShaderTransformHandler(VideoNewShaderContainer shader)
        {
            this.shader = shader;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
        }
    }
}

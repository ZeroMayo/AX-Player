// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.Interpolation
{
    public partial class InterpolationContainer : VideoNewShaderContainer
    {
        protected override string FragmentShader => "Interpolation";
        public override ShaderType Type => ShaderType.Interpolation;
        protected override DrawNode CreateShaderDrawNode() => new InterpolationDrawNode(this, SharedData);
    }
}

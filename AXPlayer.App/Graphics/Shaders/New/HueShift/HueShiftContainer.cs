// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.HueShift
{
    public partial class HueShiftContainer : VideoNewShaderContainer
    {
        protected override string FragmentShader => "HueShift";
        public override ShaderType Type => ShaderType.HueShift;
        protected override DrawNode CreateShaderDrawNode() => new HueShiftContainerDrawNode(this, SharedData);
    }
}

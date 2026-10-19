// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.Grayscale
{
    public partial class GrayscaleContainer : VideoNewShaderContainer
    {
        protected override string FragmentShader => "Greyscale";
        public override ShaderType Type => ShaderType.Greyscale;
        protected override DrawNode CreateShaderDrawNode() => new GrayscaleDrawNode(this, SharedData);
    }
}

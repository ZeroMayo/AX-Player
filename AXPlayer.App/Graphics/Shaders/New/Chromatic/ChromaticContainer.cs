// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.Chromatic
{
    public partial class ChromaticContainer : VideoNewShaderContainer
    {
        protected override string FragmentShader => "ChromaticAberration";
        public override ShaderType Type => ShaderType.Chromatic;
        protected override DrawNode CreateShaderDrawNode() => new ChromaticContainerDrawNode(this, SharedData);
    }
}

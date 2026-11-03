// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.CRT
{
    public partial class CrtContainer : VideoNewShaderContainer
    {
        protected override string FragmentShader => "CRTVideo";
        public override ShaderType Type => ShaderType.CRT;
        protected override DrawNode CreateShaderDrawNode() => new CrtContainerDrawNode(this, SharedData);
    }
}

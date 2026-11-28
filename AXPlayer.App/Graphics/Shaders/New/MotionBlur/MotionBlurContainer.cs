// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.MotionBlur
{
    /// <summary>
    /// Smears each video frame along the motion estimated from the previous frame, giving moving
    /// content a camera-shutter style blur while leaving static content untouched.
    /// </summary>
    public partial class MotionBlurContainer : VideoNewShaderContainer
    {
        protected override string FragmentShader => "MotionBlur";
        public override ShaderType Type => ShaderType.MotionBlur;
        protected override DrawNode CreateShaderDrawNode() => new MotionBlurDrawNode(this, SharedData);
    }
}

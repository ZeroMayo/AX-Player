// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace AXPlayer.App.Graphics.Shaders.New
{
    public enum ShaderType
    {
        Bloom,
        Greyscale,
        Invert,

        [ShaderStrength(1, Max = 20f, Step = 1f)]
        Chromatic,
        Mosaic,
        Noise,
        Vignette,
        Retro,
        HueShift,

        [ShaderStrength(1, ParamName = "X Strength", Tooltip = "The strength of the glitch effect on the x-axis.")]
        [ShaderStrength(2, ParamName = "Y Strength", Tooltip = "The strength of the glitch effect on the y-axis.")]
        [ShaderStrength(3, ParamName = "Block Size", Tooltip = "The size of the glitch blocks.")]
        Glitch,

        [ShaderStrength(1)]
        [ShaderStrength(2, Max = 16f, Step = 1f, ParamName = "Splits X", Tooltip = "Amount of splits on X axis.", Single = true)]
        [ShaderStrength(3, Max = 16f, Step = 1f, ParamName = "Splits Y", Tooltip = "Amount of splits on Y axis.", Single = true)]
        SplitScreen,

        [ShaderStrength(1, Min = -1f)]
        FishEye,

        [ShaderStrength(1)]
        [ShaderStrength(2, ParamName = "Scale", Tooltip = "Scale factor of each consecutive reflection.")]
        Reflections
    }
}

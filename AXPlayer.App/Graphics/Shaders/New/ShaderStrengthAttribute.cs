// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace AXPlayer.App.Graphics.Shaders.New
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class ShaderStrengthAttribute : Attribute
    {
        public int Index { get; }
        public float Min { get; set; }
        public float Max { get; set; } = 1f;
        public float Step { get; set; } = 0.01f;

        public string? ParamName { get; set; }
        public string? Tooltip { get; set; }

        /// <summary>
        /// Makes values apply to both start and end at the same time.
        /// </summary>
        public bool Single { get; set; } = false;

        public ShaderStrengthAttribute(int index = 1)
        {
            Index = index;
        }
    }
}

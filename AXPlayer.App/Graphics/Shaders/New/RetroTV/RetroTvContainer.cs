// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace AXPlayer.App.Graphics.Shaders.New.RetroTV
{
    /// <summary>
    /// Recreates the look of a worn 1980s television: curved glass, VHS colour bleed,
    /// convergence error, scanlines, tape grain and a drifting tracking band.
    /// </summary>
    public partial class RetroTvContainer : VideoNewShaderContainer
    {
        protected override string FragmentShader => "RetroTV";
        public override ShaderType Type => ShaderType.RetroTv;
        protected override DrawNode CreateShaderDrawNode() => new RetroTvDrawNode(this, SharedData);

        private float curvature = 0.65f;
        private float noise = 0.35f;
        private float elapsedTime;

        /// <summary>
        /// How strongly the picture is bent towards the edges of the tube.
        /// </summary>
        public float Curvature
        {
            get => curvature;
            set
            {
                if (value == curvature)
                    return;

                curvature = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The amount of tape grain and tracking noise.
        /// </summary>
        public float Noise
        {
            get => noise;
            set
            {
                if (value == noise)
                    return;

                noise = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        /// <summary>
        /// The time, in seconds, that drives the tape wobble, tracking band and grain.
        /// </summary>
        public float ElapsedTime => elapsedTime;

        protected override void Update()
        {
            // Wrapped so the float keeps enough precision for the high-frequency noise
            // after the player has been open for a long time.
            elapsedTime = (float)(Time.Current / 1000.0 % 3600.0);
            base.Update();
        }
    }
}
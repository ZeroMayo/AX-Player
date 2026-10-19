// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osuTK.Graphics;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Graphics
{
    public class MultiShaderBufferedDrawNode : CustomizedShaderBufferedDrawNode
    {
        protected new IMultiShaderBufferedDrawable Source => (IMultiShaderBufferedDrawable)base.Source;

        protected new MultiShaderBufferedDrawNodeSharedData SharedData => (MultiShaderBufferedDrawNodeSharedData)base.SharedData;

        public MultiShaderBufferedDrawNode(IMultiShaderBufferedDrawable source, DrawNode child, MultiShaderBufferedDrawNodeSharedData sharedData)
            : base(source, child, sharedData)
        {
        }

        public override void ApplyState()
        {
            base.ApplyState();

            // re-initialize the shader buffer size because the shader size might be changed.
            SharedData.IsLatestFrameBuffer = false;
        }

        protected override long GetDrawVersion()
        {
            // if contains shader that need to apply time, then need to force run populate contents in each frame.
            if (Source.Shaders.Any(ContainTimePropertyShader))
            {
                ResetDrawVersion();
            }

            return base.GetDrawVersion();
        }

        protected override void PopulateContents(IRenderer renderer)
        {
            base.PopulateContents(renderer);

            if (!SharedData.IsLatestFrameBuffer)
                SharedData.UpdateFrameBuffers(renderer, Source.Shaders.ToArray());

            drawFrameBuffer(renderer);
        }

        protected override void DrawContents(IRenderer renderer)
        {
            var sharedDatas = SharedData.GetDrawSharedDatas().Reverse().ToArray();

            if (sharedDatas.Length != 0)
            {
                foreach (var sharedData in sharedDatas)
                {
                    renderer.DrawFrameBuffer(sharedData.MainBuffer, DrawRectangle, Color4.White);
                }
            }
            else
            {
                // should draw origin content if no shader effects.
                base.DrawContents(renderer);
            }
        }

        private void drawFrameBuffer(IRenderer renderer)
        {
            var shaders = Source.Shaders;
            var mainFrameBuffer = SharedData.MainBuffer;

            foreach (var shader in shaders)
            {
                var sharedData = SharedData.GetBufferedDrawNodeSharedData(shader);

                switch (shader)
                {
                    case IStepShader stepShader:
                    {
                        var stepShaders = stepShader.StepShaders.ToArray();

                        foreach (ICustomizedShader childShader in stepShaders)
                        {
                            var fromFrameBuffer = stepShader.FromShader != null ? SharedData.GetBufferedDrawNodeSharedData(stepShader.FromShader) : null;

                            var isFirst = childShader == stepShaders.First();
                            var isLast = childShader == stepShaders.Last();

                            var source = isFirst ? fromFrameBuffer?.MainBuffer ?? mainFrameBuffer : sharedData.CurrentEffectBuffer;
                            var target = isLast ? sharedData.MainBuffer : sharedData.GetNextEffectBuffer();
                            RenderShader(renderer, childShader, source, target);
                        }

                        break;
                    }

                    default:
                    {
                        var target = sharedData.MainBuffer;
                        RenderShader(renderer, shader, mainFrameBuffer, target);
                        break;
                    }
                }
            }
        }
    }
}

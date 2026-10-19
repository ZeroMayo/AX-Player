// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using AXPlayer.App.Graphics.Shaders;

namespace AXPlayer.App.Graphics
{
    public class MultiShaderBufferedDrawNodeSharedData : BufferedDrawNodeSharedData
    {
        private readonly Dictionary<ICustomizedShader, BufferedDrawNodeSharedData> sharedDatas = new();

        public bool IsLatestFrameBuffer { get; set; }

        private readonly RenderBufferFormat[]? formats;

        public MultiShaderBufferedDrawNodeSharedData(RenderBufferFormat[]? formats = null, bool pixelSnapping = false)
            : base(0, formats, pixelSnapping)
        {
            this.formats = formats;
        }

        public void UpdateFrameBuffers(IRenderer renderer, ICustomizedShader[] shaders)
        {
            if (IsLatestFrameBuffer)
                return;

            IsLatestFrameBuffer = true;

            // will not re-initialize if shader is not changed.
            if (sharedDatas.Keys.SequenceEqual(shaders))
                return;

            // collect all frame buffer that needs to be disposed.
            var disposedFrameBuffer = sharedDatas.Values.ToArray();
            sharedDatas.Clear();

            foreach (var shader in shaders)
            {
                var sharedData = createBufferedDrawNodeSharedDataByShader(shader);
                sharedData.Initialise(renderer);
                sharedDatas.Add(shader, sharedData);
            }

            renderer.ScheduleDisposal(_ =>
            {
                clearSharedDatas(disposedFrameBuffer);
            }, this);
        }

        private BufferedDrawNodeSharedData createBufferedDrawNodeSharedDataByShader(ICustomizedShader customizedShader)
        {
            switch (customizedShader)
            {
                case IStepShader stepShader:
                {
                    var stepShaderAmount = Math.Min(stepShader.StepShaders.Count, 2);
                    return new BufferedDrawNodeSharedData(effectBufferCount: stepShaderAmount, textureFormat: TexturePixelFormat.R8G8B8A8Float, formats, PixelSnapping, ClipToRootNode);
                }

                default:
                    return new BufferedDrawNodeSharedData(textureFormat: TexturePixelFormat.R8G8B8A8Float, formats: formats, PixelSnapping, ClipToRootNode);
            }
        }

        public BufferedDrawNodeSharedData GetBufferedDrawNodeSharedData(ICustomizedShader shader)
        {
            if (!sharedDatas.ContainsKey(shader))
                throw new KeyNotFoundException();

            return sharedDatas[shader];
        }

        public BufferedDrawNodeSharedData[] GetDrawSharedDatas()
            => sharedDatas.Where(x =>
            {
                // should not draw the step shader if there's no content.
                if (x.Key is IStepShader stepShader)
                    return stepShader.StepShaders.Any() && stepShader.Draw;

                return true;
            }).Select(x => x.Value).ToArray();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            clearSharedDatas(sharedDatas.Values.ToArray());
        }

        private static void clearSharedDatas(IEnumerable<BufferedDrawNodeSharedData> effectBuffers)
        {
            // dispose all frame buffer in array.
            foreach (var shaderBuffer in effectBuffers)
            {
                shaderBuffer.Dispose();
            }
        }
    }
}

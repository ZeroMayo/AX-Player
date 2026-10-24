// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osuTK;
using SixLabors.ImageSharp.PixelFormats;
using SkiaSharp;
using SkiaSharp.Skottie;

namespace AXPlayer.App.Graphics
{
    public enum RepeatMode
    {
        Restart,
        Reverse
    }

    public sealed record AnimationInfo(string Version, TimeSpan Duration, double FramesPerSecond, double InPoint, double OutPoint);

    /// <summary>
    /// A drawable for displaying and controlling Lottie animations using osu!framework.
    /// </summary>
    public partial class LottieAnimationView : Sprite
    {
        private IRenderer renderer = null!;
        private Storage storage = null!;
        private Animation? animation;
        private SKBitmap? frameBitmap;
        private byte[]? framePixels;
        private Texture? animationTexture;
        private string? fileName;
        private double elapsed;
        private int repeatsRemaining;
        private bool isReversed;
        private bool dependenciesLoaded;
        private Vector2 animationScale = Vector2.One;

        /// <summary>
        /// Gets or sets the file path to the Lottie JSON animation. Relative paths are resolved by osu!framework storage.
        /// </summary>
        public string? FileName
        {
            get => fileName;
            set
            {
                if (fileName == value)
                    return;

                if (!dependenciesLoaded)
                {
                    fileName = value;
                    return;
                }

                if (value == null)
                {
                    fileName = null;
                    disposeAnimation();
                    return;
                }

                loadAnimation(value);
                fileName = value;
            }
        }

        /// <summary>
        /// Gets or sets whether the animation starts automatically after it is loaded.
        /// </summary>
        public bool AutoPlay { get; set; }

        /// <summary>
        /// Gets or sets the number of additional plays. Use -1 for infinite repeats.
        /// </summary>
        public int RepeatCount
        {
            get;
            set
            {
                if (value < -1)
                    throw new ArgumentOutOfRangeException(nameof(value), "Repeat count must be -1 or greater.");

                field = value;
                repeatsRemaining = value;
            }
        }

        /// <summary>
        /// Gets or sets the repeat mode.
        /// </summary>
        public RepeatMode Repeat { get; set; } = RepeatMode.Restart;

        /// <summary>
        /// Gets or sets an additional scale applied to the animation around its centre.
        /// </summary>
        public Vector2 AnimationScale
        {
            get => animationScale;
            set
            {
                if (animationScale == value)
                    return;

                animationScale = value;
                renderFrame();
            }
        }

        /// <summary>
        /// Gets information about the loaded animation.
        /// </summary>
        public AnimationInfo? Info { get; private set; }

        /// <summary>
        /// Gets whether the animation is currently playing.
        /// </summary>
        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Occurs when playback stops.
        /// </summary>
        public event EventHandler? OnStop;

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer, Storage storage)
        {
            this.renderer = renderer;
            this.storage = storage;
            dependenciesLoaded = true;

            if (fileName != null)
                loadAnimation(fileName);
        }

        /// <summary>
        /// Starts or resumes playback.
        /// </summary>
        public void PlayAnimation()
        {
            if (animation == null)
                return;

            IsPlaying = true;
        }

        /// <summary>
        /// Stops playback and resets the animation to its first frame.
        /// </summary>
        public void StopAnimation()
        {
            bool wasPlaying = IsPlaying;
            IsPlaying = false;
            elapsed = 0;
            isReversed = false;
            repeatsRemaining = RepeatCount;

            if (animation != null)
            {
                animation.SeekFrameTime(0);
                renderFrame();
            }

            if (wasPlaying)
                OnStop?.Invoke(this, EventArgs.Empty);
        }

        protected override void Update()
        {
            base.Update();

            if (!IsPlaying || animation == null)
                return;

            var duration = animation.Duration.TotalSeconds;
            if (duration <= 0)
            {
                finishPlayback();
                return;
            }

            elapsed += Time.Elapsed / 1000;
            long completedLoops = (long)(elapsed / duration);

            if (completedLoops > 0)
            {
                if (RepeatCount == -1)
                {
                    if (Repeat == RepeatMode.Reverse && completedLoops % 2 != 0)
                        isReversed = !isReversed;

                    elapsed %= duration;
                }
                else if (completedLoops > repeatsRemaining)
                {
                    if (Repeat == RepeatMode.Reverse && repeatsRemaining % 2 != 0)
                        isReversed = !isReversed;

                    finishPlayback();
                    return;
                }
                else
                {
                    repeatsRemaining -= (int)completedLoops;

                    if (Repeat == RepeatMode.Reverse && completedLoops % 2 != 0)
                        isReversed = !isReversed;

                    elapsed %= duration;
                }
            }

            animation.SeekFrameTime((float)(isReversed ? duration - elapsed : elapsed));
            renderFrame();
        }

        [Resolved]
        private Game Game { get; set; }

        private void loadAnimation(string assetName)
        {
            using StreamReader stream = new(Game.Resources.GetStream(assetName));
            using var skiaStream = new SKManagedStream(stream.BaseStream);

            if (!Animation.TryCreate(skiaStream, out var loadedAnimation) || loadedAnimation == null)
            {
                loadedAnimation?.Dispose();
                throw new InvalidOperationException($"Failed to load Lottie animation '{assetName}'.");
            }

            SKBitmap? loadedBitmap = null;
            Texture? loadedTexture = null;

            try
            {
                var size = loadedAnimation.Size;
                var maxDimension = Math.Max(size.Width, size.Height);

                if (!float.IsFinite(size.Width) || !float.IsFinite(size.Height)
                    || size.Width <= 0 || size.Height <= 0
                    || renderer.MaxTextureSize <= 0)
                    throw new InvalidOperationException($"Lottie animation '{assetName}' has an invalid size.");

                var scale = Math.Min(1, renderer.MaxTextureSize / maxDimension);
                int width = Math.Min(renderer.MaxTextureSize, Math.Max(1, (int)Math.Ceiling(size.Width * scale)));
                int height = Math.Min(renderer.MaxTextureSize, Math.Max(1, (int)Math.Ceiling(size.Height * scale)));

                loadedBitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
                var loadedPixels = new byte[checked(width * height * 4)];
                loadedTexture = new DisposableTexture(renderer.CreateTexture(width, height));
                loadedTexture.BypassTextureUploadQueueing = true;

                loadedAnimation.SeekFrameTime(0);
                renderFrame(loadedAnimation, loadedBitmap, loadedPixels, loadedTexture);

                var previousAnimation = animation;
                var previousBitmap = frameBitmap;
                var previousTexture = animationTexture;

                animation = loadedAnimation;
                frameBitmap = loadedBitmap;
                framePixels = loadedPixels;
                animationTexture = loadedTexture;
                Texture = loadedTexture;
                Info = new AnimationInfo(loadedAnimation.Version, loadedAnimation.Duration, loadedAnimation.Fps, loadedAnimation.InPoint, loadedAnimation.OutPoint);
                elapsed = 0;
                isReversed = false;
                repeatsRemaining = RepeatCount;

                loadedAnimation = null;
                loadedBitmap = null;
                loadedTexture = null;

                previousAnimation?.Dispose();
                previousBitmap?.Dispose();
                previousTexture?.Dispose();

                if (AutoPlay)
                    PlayAnimation();
            }
            finally
            {
                loadedAnimation?.Dispose();
                loadedBitmap?.Dispose();
                loadedTexture?.Dispose();
            }
        }

        private void renderFrame()
        {
            if (animation == null || frameBitmap == null || framePixels == null || animationTexture == null)
                return;

            renderFrame(animation, frameBitmap, framePixels, animationTexture);
        }

        private void renderFrame(Animation animation, SKBitmap frameBitmap, byte[] framePixels, Texture animationTexture)
        {
            using var canvas = new SKCanvas(frameBitmap);
            canvas.Clear(SKColors.Transparent);
            canvas.Save();
            canvas.Scale(AnimationScale.X, AnimationScale.Y, frameBitmap.Width / 2f, frameBitmap.Height / 2f);
            animation.Render(canvas, new SKRect(0, 0, frameBitmap.Width, frameBitmap.Height));
            canvas.Restore();

            Marshal.Copy(frameBitmap.GetPixels(), framePixels, 0, framePixels.Length);

            var pixels = MemoryMarshal.Cast<byte, Rgba32>(framePixels.AsSpan());
            var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(pixels, frameBitmap.Width, frameBitmap.Height);
            animationTexture.SetData(new TextureUpload(PremultipliedImage.FromPremultiplied(image)));
        }

        private void finishPlayback()
        {
            if (!IsPlaying)
                return;

            IsPlaying = false;
            elapsed = 0;
            isReversed = false;
            repeatsRemaining = RepeatCount;
            animation?.SeekFrameTime(0);
            renderFrame();
            OnStop?.Invoke(this, EventArgs.Empty);
        }

        private void disposeAnimation()
        {
            IsPlaying = false;
            Info = null;
            Texture = null;

            animation?.Dispose();
            animation = null;

            frameBitmap?.Dispose();
            frameBitmap = null;

            framePixels = null;

            animationTexture?.Dispose();
            animationTexture = null;

            elapsed = 0;
            isReversed = false;
            repeatsRemaining = RepeatCount;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
                disposeAnimation();
        }
    }
}

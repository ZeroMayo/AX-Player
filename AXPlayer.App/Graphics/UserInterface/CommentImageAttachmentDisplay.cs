// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace AXPlayer.App.Graphics.UserInterface
{
    /// <summary>
    /// Renders the image or GIF that a comment attached to it. Animated image formats are played
    /// frame by frame, while a still image is loaded through the texture store.
    /// </summary>
    public partial class CommentImageAttachmentDisplay : CompositeDrawable
    {
        private const float maximum_width = 360;
        private const float maximum_height = 270;

        /// <summary>
        /// The images of a comment are rendered at half the size of the attachment's natural size.
        /// </summary>
        private const float display_scale = 0.5f;

        private const double fallback_frame_duration = 100;

        private static readonly HttpClient http_client = new HttpClient();

        private static readonly DecoderOptions decoder_options = new DecoderOptions
        {
            MaxFrames = int.MaxValue,
        };

        private readonly CommentImageAttachment attachment;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private Sprite sprite = null!;
        private CancellationTokenSource? loadCancellation;

        private Image<Rgba32>? animation;
        private double[]? frameDurations;
        private byte[]? framePixels;
        private Texture? animationTexture;
        private double elapsed;
        private int frameIndex;

        internal CommentImageAttachmentDisplay(CommentImageAttachment attachment)
        {
            this.attachment = attachment;

            float aspectRatio = attachment.AspectRatio > 0
                ? attachment.AspectRatio
                : attachment.Width > 0 && attachment.Height > 0
                    ? attachment.Width / attachment.Height
                    : 1;

            float width = attachment.Width > 0 ? Math.Min(attachment.Width, maximum_width) : maximum_width;
            float height = width / aspectRatio;

            if (height > maximum_height)
            {
                height = maximum_height;
                width = height * aspectRatio;
            }

            Size = new Vector2(width * display_scale, height * display_scale);
            Masking = true;
            CornerRadius = AXPlayerApp.UI_CORNER_RADIUS;

            InternalChild = sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            loadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = loadCancellation.Token;

            Task.Run(async () => await loadImageAsync(cancellationToken).ConfigureAwait(false), cancellationToken);
        }

        private async Task loadImageAsync(CancellationToken cancellationToken)
        {
            if (shouldLoadAnimation())
            {
                Image<Rgba32>? image = null;

                try
                {
                    byte[] bytes = await http_client.GetByteArrayAsync(attachment.Url, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    using var stream = new System.IO.MemoryStream(bytes, writable: false);
                    image = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(decoder_options, stream, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (image.Frames.Count > 0)
                    {
                        Image<Rgba32> decoded = image;
                        image = null;

                        Schedule(() => applyAnimation(decoded, cancellationToken));
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $@"Failed to decode a comment animation from '{attachment.Url}'.");
                }
                finally
                {
                    image?.Dispose();
                }
            }

            try
            {
                Texture texture = await textures.GetAsync(attachment.Url, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                Schedule(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    sprite.Texture = texture;
                    sprite.FadeIn(120, Easing.OutQuint);
                });
            }
            catch (OperationCanceledException)
            {
                // The comment row was disposed before the image finished loading.
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to load a comment image from '{attachment.Url}'.");
            }
        }

        private bool shouldLoadAnimation()
            => attachment.IsAnimated
               || attachment.Url.Contains(@".gif", StringComparison.OrdinalIgnoreCase)
               || attachment.Url.Contains(@".webp", StringComparison.OrdinalIgnoreCase);

        private void applyAnimation(Image<Rgba32> image, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                image.Dispose();
                return;
            }

            animation = image;
            frameDurations = new double[image.Frames.Count];

            for (int i = 0; i < frameDurations.Length; i++)
                frameDurations[i] = getFrameDuration(image.Frames[i]);

            framePixels = new byte[checked(image.Width * image.Height * 4)];
            animationTexture = new DisposableTexture(renderer.CreateTexture(image.Width, image.Height))
            {
                BypassTextureUploadQueueing = true,
            };

            sprite.Texture = animationTexture;
            showFrame(0);
            sprite.FadeIn(120, Easing.OutQuint);
        }

        private static double getFrameDuration(ImageFrame<Rgba32> frame)
        {
            // ImageSharp exposes a TimeSpan duration on current metadata, while older formats keep
            // their delay as a number on the format-specific metadata.
            object? duration = frame.GetType().GetProperty(@"Duration")?.GetValue(frame)
                               ?? frame.Metadata.GetType().GetProperty(@"Duration")?.GetValue(frame.Metadata);

            if (duration is TimeSpan timeSpan && timeSpan > TimeSpan.Zero)
                return timeSpan.TotalMilliseconds;

            object? frameDelay = frame.Metadata.GetType().GetProperty(@"FrameDelay")?.GetValue(frame.Metadata);

            if (frameDelay != null)
            {
                double value = Convert.ToDouble(frameDelay, CultureInfo.InvariantCulture);

                if (value > 0)
                {
                    // GIF delays are stored in hundredths of a second, WebP delays in milliseconds.
                    return frame.Metadata.GetType().Name.Contains(@"Webp", StringComparison.OrdinalIgnoreCase)
                        ? value
                        : value * 10;
                }
            }

            return fallback_frame_duration;
        }

        private void showFrame(int index)
        {
            if (animation == null || framePixels == null || animationTexture == null)
                return;

            ImageFrame<Rgba32> frame = animation.Frames[index];
            Span<Rgba32> pixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Rgba32>(framePixels.AsSpan());
            frame.CopyPixelDataTo(pixels);

            var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(pixels, frame.Width, frame.Height);
            animationTexture.SetData(new TextureUpload(PremultipliedImage.FromStraight(image)));
        }

        protected override void Update()
        {
            base.Update();

            if (animation == null || frameDurations == null || frameDurations.Length <= 1)
                return;

            elapsed += Time.Elapsed;

            bool changed = false;

            while (elapsed >= frameDurations[frameIndex])
            {
                elapsed -= frameDurations[frameIndex];
                frameIndex = (frameIndex + 1) % frameDurations.Length;
                changed = true;
            }

            if (changed)
                showFrame(frameIndex);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
                loadCancellation = null;

                animation?.Dispose();
                animation = null;
                animationTexture?.Dispose();
                animationTexture = null;
                framePixels = null;
                frameDurations = null;
            }

            base.Dispose(isDisposing);
        }
    }
}

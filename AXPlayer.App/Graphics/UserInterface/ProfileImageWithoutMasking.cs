// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Extensions;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProfileImageWithoutMasking : CompositeDrawable
    {
        private Sprite profileImage;
        private BufferedContainer blur;

        private CancellationTokenSource imageLoadCancellation;
        private int imageLoadVersion;

        [Resolved]
        private TextureStore textureStore { get; set; }

        [Resolved]
        private YouTubeAPI api { get; set; }

        public ProfileImageWithoutMasking(float size = 30)
        {
            Width = Height = size;
            //CornerRadius = size / 2;
            InternalChildren = new Drawable[]
            {
                blur = new BufferedContainer
                {
                    BlurSigma = new osuTK.Vector2(2),
                    RelativeSizeAxes = Axes.Both,
                    Child = profileImage = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            blur.Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0.5f), Color4.White.Opacity(0));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Interlocked.Increment(ref imageLoadVersion);
                imageLoadCancellation?.Cancel();
                imageLoadCancellation?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Shows the avatar of the given channel, replacing whatever is being loaded for the previous one.
        /// </summary>
        /// <remarks>
        /// A channel without an id is ignored: comments and playlist items can reference an author that
        /// has none, and that must not take the display (or the whole player) down.
        /// </remarks>
        public void UpdateProfileImage(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return;

            var cancellation = new CancellationTokenSource();
            var previousCancellation = Interlocked.Exchange(ref imageLoadCancellation, cancellation);
            previousCancellation?.Cancel();
            previousCancellation?.Dispose();

            int version = Interlocked.Increment(ref imageLoadVersion);
            loadChannelAndProfileImageAsync(channelId, version, cancellation.Token).FireAndForget();
        }

        private async Task loadChannelAndProfileImageAsync(string channelId, int version, CancellationToken cancellationToken)
        {
            try
            {
                Google.Apis.YouTube.v3.Data.Channel loadedChannel = await Task.Run(() => api.GetChannel(channelId), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                string imageUrl = loadedChannel?.Snippet?.Thumbnails?.High?.Url;

                if (string.IsNullOrEmpty(imageUrl))
                    return;

                await loadImageAsync(imageUrl, version, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer channel was requested or this drawable was disposed.
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to load the profile image for channel '{channelId}'.");
            }
        }

        /// <summary>
        /// Loads and shows the image at the given url.
        /// </summary>
        public async Task GetProfileImage(string url, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            try
            {
                await loadImageAsync(url, Volatile.Read(ref imageLoadVersion), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to load the profile image '{url}'.");
            }
        }

        private async Task loadImageAsync(string url, int version, CancellationToken cancellationToken)
        {
            Texture texture = await textureStore.GetAsync(url, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (IsDisposed)
                return;

            //GetPalette();
            Schedule(() =>
            {
                if (version == Volatile.Read(ref imageLoadVersion))
                    profileImage.Texture = texture;
            });
        }
    }
}

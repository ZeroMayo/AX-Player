// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Extensions;
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
    public partial class DiscordProfileImageWithoutMasking : CompositeDrawable
    {
        private Sprite profileImage;
        private BufferedContainer blur;

        private CancellationTokenSource imageLoadCancellation;
        private int imageLoadVersion;

        [Resolved]
        private TextureStore textureStore { get; set; }

        [Resolved(canBeNull: true)]
        private Online.DiscordRPCClient api { get; set; }

        public DiscordProfileImageWithoutMasking(float size = 30)
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
                }
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

        protected override void LoadComplete()
        {
            base.LoadComplete();
            GetProfileImage().FireAndForget();
        }

        /// <summary>
        /// Shows the avatar of the signed in Discord user.
        /// </summary>
        /// <remarks>
        /// Discord does not have to be running: without a connection there is simply no avatar to show,
        /// and a failed download must not take the player down.
        /// </remarks>
        public async Task GetProfileImage(CancellationToken cancellationToken = default)
        {
            if (api == null)
                return;

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken linkedToken = cancellation.Token;

            var previousCancellation = Interlocked.Exchange(ref imageLoadCancellation, cancellation);
            previousCancellation?.Cancel();
            previousCancellation?.Dispose();

            int version = Interlocked.Increment(ref imageLoadVersion);

            try
            {
                var user = api.GetCurrentUser();

                if (user == null)
                    return;

                Texture texture = await textureStore.GetAsync(user.GetAvatarURL(DiscordRPC.User.AvatarFormat.PNG, DiscordRPC.User.AvatarSize.x2048), linkedToken).ConfigureAwait(false);
                linkedToken.ThrowIfCancellationRequested();

                if (IsDisposed)
                    return;

                Schedule(() =>
                {
                    if (version == Volatile.Read(ref imageLoadVersion))
                        profileImage.Texture = texture;
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A newer request replaced this one, or the drawable was disposed.
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "[Discord] Failed to load the profile image.");
            }
        }
    }
}

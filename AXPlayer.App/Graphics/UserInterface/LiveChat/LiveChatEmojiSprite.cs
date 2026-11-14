// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterface.LiveChat
{
    /// <summary>
    /// A picture emoji of a live chat message (for example ":clapping_hands:"), rendered as an image.
    /// </summary>
    public partial class LiveChatEmojiSprite : CompositeDrawable
    {
        private readonly LiveChatReplayTextSegment segment;
        private readonly float emojiSize;

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        private Sprite? sprite;
        private CancellationTokenSource? loadCancellation;

        public LiveChatEmojiSprite(LiveChatReplayTextSegment segment, bool compact = false)
        {
            this.segment = segment;
            emojiSize = compact ? 15 : 18;

            Size = new Vector2(emojiSize);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0,
            };

            AddInternal(sprite);

            if (!string.IsNullOrEmpty(segment.ImageUrl))
                loadEmoji(segment.ImageUrl);
        }

        private void loadEmoji(string url)
        {
            loadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = loadCancellation.Token;

            Task.Run(async () =>
            {
                try
                {
                    Texture texture = await textures.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    Schedule(() =>
                    {
                        if (sprite == null || cancellationToken.IsCancellationRequested)
                            return;

                        sprite.Texture = texture;
                        sprite.FadeIn(120, Easing.OutQuint);
                    });
                }
                catch (OperationCanceledException)
                {
                    // The drawable was disposed before the emoji finished loading.
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $@"Failed to load a live chat emoji from '{url}'.");
                }
            }, cancellationToken);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                loadCancellation?.Cancel();
                loadCancellation?.Dispose();
                loadCancellation = null;
            }

            base.Dispose(isDisposing);
        }
    }
}

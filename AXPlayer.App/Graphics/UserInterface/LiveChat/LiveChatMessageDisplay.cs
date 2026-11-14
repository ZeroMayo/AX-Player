// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface.LiveChat
{
    /// <summary>
    /// A single entry of a live chat replay, styled after YouTube's own chat panel.
    /// </summary>
    public partial class LiveChatMessageDisplay : CompositeDrawable
    {
        private const float avatar_size = 24;
        private const float avatar_size_compact = 18;
        private const float horizontal_padding = 8;
        private const float vertical_padding = 4;

        private readonly bool compact;

        private float avatarSize => compact ? avatar_size_compact : avatar_size;
        private float horizontalPadding => compact ? 6 : horizontal_padding;
        private float verticalPadding => compact ? 2 : vertical_padding;

        public LiveChatReplayMessage Message { get; }

        [Resolved]
        private TextureStore textures { get; set; } = null!;

        private Sprite? avatarSprite;
        private CancellationTokenSource? avatarLoadCancellation;

        public LiveChatMessageDisplay(LiveChatReplayMessage message, bool compact = false)
        {
            this.compact = compact;
            Message = message;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            if (Message.Kind == LiveChatReplayMessageKind.ViewerEngagement)
            {
                InternalChild = createSystemMessage(overlayColourProvider);
                return;
            }

            bool highlighted = Message.Kind is LiveChatReplayMessageKind.Paid or LiveChatReplayMessageKind.Membership or LiveChatReplayMessageKind.Sticker;

            var children = new List<Drawable>();

            if (highlighted)
            {
                children.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = overlayColourProvider.Background4,
                    Alpha = 0.7f,
                });
            }

            children.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding
                {
                    Vertical = verticalPadding,
                    Horizontal = horizontalPadding,
                },
                Children = new Drawable[]
                {
                    createAvatar(overlayColourProvider),
                    createContents(overlayColourProvider),
                },
            });

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                CornerRadius = compact ? 6 : 8,
                Masking = highlighted,
                Children = children,
            };
        }

        private Drawable createAvatar(OverlayColourProvider overlayColourProvider)
        {
            var avatar = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                Size = new Vector2(avatarSize),
                CornerRadius = avatarSize / 2,
                Masking = true,
            };

            avatar.Add(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = getFallbackColour(overlayColourProvider),
            });

            if (!string.IsNullOrEmpty(Message.AuthorName))
            {
                avatar.Add(new ProjectYomiSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = StringInfo.GetNextTextElement(Message.AuthorName).ToUpperInvariant(),
                    Font = AXPlayerApp.DefaultFont.With(size: compact ? 9 : 12, weight: "Bold"),
                    Colour = Color4.White,
                });
            }

            avatarSprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fill,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Alpha = 0,
            };

            avatar.Add(avatarSprite);

            if (!string.IsNullOrEmpty(Message.AuthorPhotoUrl))
                loadAvatar(Message.AuthorPhotoUrl);

            return avatar;
        }

        private Drawable createContents(OverlayColourProvider overlayColourProvider)
        {
            var authorRow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(compact ? 3 : 4, compact ? 1 : 2),
            };

            if (!string.IsNullOrEmpty(Message.AuthorName))
            {
                authorRow.Add(new ProjectYomiTextFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: compact ? 12 : 14, weight: "Bold"))
                {
                    AutoSizeAxes = Axes.Both,
                    Text = Message.AuthorName,
                    Colour = getAuthorColour(overlayColourProvider),
                });
            }

            foreach (LiveChatReplayBadge badge in Message.Badges)
                authorRow.Add(createBadge(badge, overlayColourProvider));

            if (!string.IsNullOrEmpty(Message.AmountText))
            {
                authorRow.Add(new ProjectYomiSpriteText(false)
                {
                    Text = Message.AmountText,
                    Font = AXPlayerApp.DefaultFont.With(size: compact ? 11 : 13, weight: "Bold"),
                    Colour = overlayColourProvider.Highlight1,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                });
            }

            var messageText = new ProjectYomiTextFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: compact ? 12 : 14, weight: "Regular"))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Colour = overlayColourProvider.Content2,
            };

            populateMessageText(messageText);

            var contents = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, compact ? 0 : 1),
                Padding = new MarginPadding { Left = avatarSize + (compact ? 6 : 8) },
                Children = new Drawable[]
                {
                    authorRow,
                    messageText,
                },
            };

            return contents;
        }

        private void populateMessageText(ProjectYomiTextFlowContainer container)
        {
            if (Message.Segments.Count == 0)
            {
                container.Text = Message.Text;
                return;
            }

            foreach (LiveChatReplayTextSegment segment in Message.Segments)
            {
                if (segment.HasImage)
                    container.AddArbitraryDrawable(new LiveChatEmojiSprite(segment, compact));
                else if (!string.IsNullOrEmpty(segment.Text))
                    container.AddText(segment.Text);
            }
        }

        private Drawable createSystemMessage(OverlayColourProvider overlayColourProvider)
        {
            var contents = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding
                {
                    Vertical = verticalPadding + (compact ? 0 : 1),
                    Left = horizontalPadding + (compact ? 14 : 18),
                    Right = horizontalPadding,
                },
                Child = new ProjectYomiTextFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: compact ? 11.5f : 13, weight: "SemiBold"))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = Message.Text,
                    Colour = overlayColourProvider.Foreground2,
                },
            };

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    contents,
                    new SpriteIcon
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        Position = new Vector2(horizontalPadding, verticalPadding + (compact ? 2 : 3)),
                        Size = new Vector2(compact ? 10 : 12),
                        Icon = FontAwesome.Solid.InfoCircle,
                        Colour = overlayColourProvider.Foreground2,
                    },
                },
            };
        }

        private Drawable createBadge(LiveChatReplayBadge badge, OverlayColourProvider overlayColourProvider)
        {
            return new Container
            {
                AutoSizeAxes = Axes.X,
                Height = compact ? 13 : 16,
                CornerRadius = compact ? 3 : 4,
                Masking = true,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = overlayColourProvider.Background3,
                    },
                    new ProjectYomiSpriteText(false)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Padding = new MarginPadding { Horizontal = compact ? 3 : 4 },
                        Text = badge.Label,
                        Font = AXPlayerApp.DefaultFont.With(size: compact ? 8.5f : 10, weight: "Bold"),
                        Colour = overlayColourProvider.Light2,
                    },
                },
            };
        }

        private Color4 getAuthorColour(OverlayColourProvider overlayColourProvider)
        {
            foreach (LiveChatReplayBadge badge in Message.Badges)
            {
                if (badge.Label.Contains(@"Owner", StringComparison.OrdinalIgnoreCase))
                    return overlayColourProvider.Colour1;

                if (badge.Label.Contains(@"Moderator", StringComparison.OrdinalIgnoreCase))
                    return overlayColourProvider.Colour2;

                if (badge.Label.Contains(@"Verified", StringComparison.OrdinalIgnoreCase))
                    return overlayColourProvider.Colour3;
            }

            return Message.Kind is LiveChatReplayMessageKind.Paid or LiveChatReplayMessageKind.Membership or LiveChatReplayMessageKind.Sticker
                ? overlayColourProvider.Highlight1
                : overlayColourProvider.Content1;
        }

        private Color4 getFallbackColour(OverlayColourProvider overlayColourProvider)
        {
            if (string.IsNullOrEmpty(Message.AuthorName))
                return overlayColourProvider.Background3;

            int hash = 0;

            foreach (char character in Message.AuthorName)
                hash = unchecked(hash * 31 + character);

            Color4[] palette =
            {
                overlayColourProvider.Colour2,
                overlayColourProvider.Colour3,
                overlayColourProvider.Dark1,
                overlayColourProvider.Dark2,
            };

            return palette[Math.Abs(hash) % palette.Length].Opacity(0.9f);
        }

        private void loadAvatar(string url)
        {
            avatarLoadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = avatarLoadCancellation.Token;

            Task.Run(async () =>
            {
                try
                {
                    Texture texture = await textures.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();

                    Schedule(() =>
                    {
                        if (avatarSprite == null || cancellationToken.IsCancellationRequested)
                            return;

                        avatarSprite.Texture = texture;
                        avatarSprite.FadeIn(250, Easing.OutQuint);
                    });
                }
                catch (OperationCanceledException)
                {
                    // The drawable was disposed before the image finished loading.
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $@"Failed to load a live chat avatar from '{url}'.");
                }
            }, cancellationToken);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                avatarLoadCancellation?.Cancel();
                avatarLoadCancellation?.Dispose();
                avatarLoadCancellation = null;
            }

            base.Dispose(isDisposing);
        }
    }
}

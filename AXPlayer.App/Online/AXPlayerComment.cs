// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using Google.Apis.YouTube.v3.Data;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// An image or GIF that was attached to a comment.
    /// </summary>
    internal sealed class CommentImageAttachment
    {
        public string Url { get; init; } = string.Empty;

        public float Width { get; init; }

        public float Height { get; init; }

        public float AspectRatio { get; init; }

        public bool IsAnimated { get; init; }
    }

    /// <summary>
    /// A comment that keeps the data which the Google DTO does not model, such as its image attachment.
    /// </summary>
    internal sealed class AXPlayerComment : Comment
    {
        public CommentImageAttachment? ImageAttachment { get; init; }
    }
}

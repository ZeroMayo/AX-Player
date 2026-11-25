// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

namespace AXPlayer.App.Online
{
    /// <summary>
    /// The kinds of files that can be indexed by <see cref="MediaCache"/>.
    /// </summary>
    public enum MediaCacheKind
    {
        /// <summary>A cached audio stream file.</summary>
        AudioStream,

        /// <summary>A cached video stream file.</summary>
        VideoStream,

        /// <summary>A cached subtitle file.</summary>
        Subtitle,

        /// <summary>A cached video thumbnail.</summary>
        VideoThumbnail,

        /// <summary>A cached channel profile thumbnail.</summary>
        ChannelThumbnail,

        /// <summary>A cached playlist thumbnail.</summary>
        PlaylistThumbnail,
    }
}

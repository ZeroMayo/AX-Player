// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using Google.Apis.YouTube.v3.Data;

namespace AXPlayer.App.Extensions
{
    /// <summary>
    /// Reads the parts of a playlist that a response is not required to carry.
    /// </summary>
    public static class PlaylistExtensions
    {
        /// <summary>
        /// The widest thumbnail that the response of a playlist carried, or <see langword="null" /> when
        /// it carried none. A playlist of the library can arrive without any thumbnail, and the Data API
        /// returns the sizes the channel uploaded.
        /// </summary>
        public static string? bestThumbnailUrl(this Playlist? playlist)
        {
            ThumbnailDetails? thumbnails = playlist?.Snippet?.Thumbnails;

            return thumbnails?.Maxres?.Url
                   ?? thumbnails?.Standard?.Url
                   ?? thumbnails?.High?.Url
                   ?? thumbnails?.Medium?.Url
                   ?? thumbnails?.Default__?.Url;
        }
    }
}

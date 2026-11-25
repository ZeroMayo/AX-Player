// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Platform;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Loads channel profile thumbnails from the cache and downloads them when they are missing.
    /// </summary>
    /// <remarks>
    /// The shared <see cref="MediaThumbnailCache"/> handles de-duplication and the atomic cache write; this class
    /// only knows where channel thumbnails live inside the cache storage.
    /// </remarks>
    public static class ChannelThumbnailCache
    {
        public const string CACHE_DIRECTORY = MediaCache.CHANNEL_THUMBNAIL_DIRECTORY;

        /// <summary>
        /// Retrieves the thumbnail of the supplied channel as an image encoded byte array.
        /// </summary>
        /// <param name="cacheStorage">The cache storage to store the downloaded thumbnail in.</param>
        /// <param name="channelId">The id of the channel the thumbnail belongs to.</param>
        /// <param name="thumbnailUrl">The url to download the thumbnail from.</param>
        /// <returns>The bytes of the thumbnail.</returns>
        public static Task<byte[]> GetThumbnailAsync(Storage cacheStorage, string channelId, string thumbnailUrl)
        {
            ArgumentNullException.ThrowIfNull(cacheStorage);
            ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
            ArgumentException.ThrowIfNullOrWhiteSpace(thumbnailUrl);

            string cachePath = cacheStorage.GetStorageForDirectory(CACHE_DIRECTORY).GetFullPath($"{channelId}.png");

            return MediaThumbnailCache.GetThumbnailAsync(MediaCacheKind.ChannelThumbnail, channelId, cachePath, thumbnailUrl);
        }
    }
}

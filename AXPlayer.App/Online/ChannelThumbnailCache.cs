// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Downloads channel profile thumbnails and keeps a copy of them in the cache storage.
    /// </summary>
    /// <remarks>
    /// The same channel can be displayed by multiple drawables at once (for example a metadata display and a tooltip),
    /// so downloads for a given channel are shared instead of running in parallel. The cache file is written to a
    /// temporary file first and then moved into place so that a half-written file is never observable.
    /// </remarks>
    public static class ChannelThumbnailCache
    {
        public const string CACHE_DIRECTORY = "profile_cache";

        private const int cache_write_attempts = 3;

        private static readonly HttpClient http_client = new HttpClient();

        private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> pending_downloads =
            new ConcurrentDictionary<string, Lazy<Task<byte[]>>>(StringComparer.Ordinal);

        /// <summary>
        /// Retrieves the thumbnail of the supplied channel as an image encoded byte array.
        /// </summary>
        /// <param name="cacheStorage">The cache storage to store the downloaded thumbnail in.</param>
        /// <param name="channelId">The id of the channel the thumbnail belongs to.</param>
        /// <param name="thumbnailUrl">The url to download the thumbnail from.</param>
        /// <returns>The bytes of the downloaded image.</returns>
        public static async Task<byte[]> GetThumbnailAsync(Storage cacheStorage, string channelId, string thumbnailUrl)
        {
            ArgumentNullException.ThrowIfNull(cacheStorage);
            ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
            ArgumentException.ThrowIfNullOrWhiteSpace(thumbnailUrl);

            string cachePath = cacheStorage.GetStorageForDirectory(CACHE_DIRECTORY).GetFullPath($"{channelId}.png");

            Lazy<Task<byte[]>> download = pending_downloads.GetOrAdd(cachePath, path => new Lazy<Task<byte[]>>(() => downloadAndCacheAsync(path, thumbnailUrl)));
            Task<byte[]> downloadTask = download.Value;

            try
            {
                return await downloadTask.ConfigureAwait(false);
            }
            finally
            {
                // Failed downloads must not be kept around, otherwise every later request would reuse the failure.
                if (downloadTask.IsCompleted)
                    pending_downloads.TryRemove(new KeyValuePair<string, Lazy<Task<byte[]>>>(cachePath, download));
            }
        }

        private static async Task<byte[]> downloadAndCacheAsync(string cachePath, string thumbnailUrl)
        {
            byte[] imageBytes = await http_client.GetByteArrayAsync(thumbnailUrl).ConfigureAwait(false);
            await writeCacheFileAsync(cachePath, imageBytes).ConfigureAwait(false);

            return imageBytes;
        }

        private static async Task writeCacheFileAsync(string cachePath, byte[] imageBytes)
        {
            string temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.tmp";

            try
            {
                string? directory = Path.GetDirectoryName(cachePath);

                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var options = new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.Asynchronous,
                };

                await using (var stream = new FileStream(temporaryPath, options))
                    await stream.WriteAsync(imageBytes).ConfigureAwait(false);

                for (int attempt = 1;; attempt++)
                {
                    try
                    {
                        File.Move(temporaryPath, cachePath, true);
                        break;
                    }
                    catch (IOException) when (attempt < cache_write_attempts)
                    {
                        // The previous cache file may still be held open by a reader or a scanner for a short while.
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception exception)
            {
                // Caching is best-effort: the caller already has the image bytes and must not fail because of the disk.
                Logger.Error(exception, $"Failed to write the channel thumbnail cache file '{cachePath}'.");
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $"Failed to clean up the temporary channel thumbnail cache file '{temporaryPath}'.");
                }
            }
        }
    }
}

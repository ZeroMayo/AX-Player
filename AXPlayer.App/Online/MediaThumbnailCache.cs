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

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Loads thumbnails from the cache when possible and downloads them otherwise.
    /// </summary>
    /// <remarks>
    /// The same thumbnail can be requested by multiple drawables at once (for example a metadata display and a
    /// tooltip), so downloads for a given cache path are shared instead of running in parallel. The cache file is
    /// written to a temporary file first and then moved into place so that a half-written file is never observable.
    /// </remarks>
    public static class MediaThumbnailCache
    {
        private const int cache_write_attempts = 3;

        private static readonly HttpClient http_client = new HttpClient();

        private static readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> pending_downloads =
            new ConcurrentDictionary<string, Lazy<Task<byte[]>>>(StringComparer.Ordinal);

        /// <summary>
        /// Retrieves a thumbnail as an image encoded byte array.
        /// </summary>
        /// <param name="kind">The cache kind used to index the thumbnail.</param>
        /// <param name="key">The key used to index the thumbnail.</param>
        /// <param name="cachePath">The conventional path of the cached thumbnail.</param>
        /// <param name="thumbnailUrl">The url to download the thumbnail from when it is not cached.</param>
        /// <returns>The bytes of the thumbnail.</returns>
        public static async Task<byte[]> GetThumbnailAsync(MediaCacheKind kind, string key, string cachePath, string? thumbnailUrl)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(thumbnailUrl);

            Lazy<Task<byte[]>> download = pending_downloads.GetOrAdd(cachePath, _ => new Lazy<Task<byte[]>>(() => loadAndCacheAsync(kind, key, cachePath, thumbnailUrl)));
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

        private static async Task<byte[]> loadAndCacheAsync(MediaCacheKind kind, string key, string cachePath, string thumbnailUrl)
        {
            if (MediaCache.TryGetFile(kind, key, cachePath, out string cachedPath))
            {
                try
                {
                    return await File.ReadAllBytesAsync(cachedPath).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $"Failed to read the cached thumbnail [{cachedPath}]; downloading it again.");
                    MediaCache.Remove(kind, key);
                }
            }

            byte[] imageBytes = await http_client.GetByteArrayAsync(thumbnailUrl).ConfigureAwait(false);

            if (await writeCacheFileAsync(cachePath, imageBytes).ConfigureAwait(false))
                MediaCache.Record(kind, key, cachePath);

            return imageBytes;
        }

        private static async Task<bool> writeCacheFileAsync(string cachePath, byte[] imageBytes)
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
                        return true;
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
                Logger.Error(exception, $"Failed to write the thumbnail cache file [{cachePath}].");
                return false;
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
                    Logger.Error(exception, $"Failed to clean up the temporary thumbnail cache file [{temporaryPath}].");
                }
            }
        }
    }
}

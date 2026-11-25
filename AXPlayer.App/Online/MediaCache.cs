// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AXPlayer.App.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Keeps track of the files stored in the cache storage using a Realm database.
    /// </summary>
    /// <remarks>
    /// The index complements the existing cache directory layout instead of replacing it. Files stay at the exact
    /// paths the rest of the application already uses, so a cache written by an older version is still picked up
    /// by <see cref="importExistingFiles"/> and cache files can be inspected with ordinary file tools.
    /// </remarks>
    public static class MediaCache
    {
        public const string DATABASE_DIRECTORY = "cache_index";
        public const string DATABASE_FILE = "media_cache.realm";

        public const string VIDEO_THUMBNAIL_DIRECTORY = "videoThumbnailCache";
        public const string SOCIAL_VIDEO_THUMBNAIL_DIRECTORY = "videoThumbnailCache_";
        public const string PLAYLIST_THUMBNAIL_DIRECTORY = "playlistThumbnailCache";
        public const string CHANNEL_THUMBNAIL_DIRECTORY = "profile_cache";
        public const string SUBTITLE_DIRECTORY = "subtitleCache";
        public const string VIDEO_DIRECTORY = "videos";

        private static readonly object initialise_lock = new object();

        private static MediaCacheDatabase? database;
        private static string? cacheRoot;
        private static bool initialise_attempted;

        /// <summary>
        /// Whether the Realm index has been opened successfully.
        /// </summary>
        public static bool IsAvailable => database != null;

        /// <summary>
        /// Opens the cache index, if it has not been opened yet.
        /// </summary>
        /// <param name="cacheStorage">The storage that contains the existing cache directories.</param>
        public static void Initialise(Storage cacheStorage)
        {
            ArgumentNullException.ThrowIfNull(cacheStorage);

            lock (initialise_lock)
            {
                if (initialise_attempted)
                    return;

                initialise_attempted = true;

                try
                {
                    string root = cacheStorage.GetFullPath(string.Empty);
                    cacheRoot = root;

                    string directory = Path.Combine(root, DATABASE_DIRECTORY);
                    Directory.CreateDirectory(directory);

                    database = new MediaCacheDatabase(Path.Combine(directory, DATABASE_FILE));
                }
                catch (Exception exception)
                {
                    // The Realm native library may not be available on every platform. The plain cache files keep
                    // working in that case, so a missing index must never prevent the application from starting.
                    Logger.Error(exception, "The Realm media cache index is unavailable; using the cache files without an index.");
                }
            }

            if (database != null)
                Task.Run(importExistingFiles).FireAndForget();
        }

        /// <summary>
        /// Finds a cached file, verifying that it still exists with the recorded size.
        /// </summary>
        /// <param name="kind">The kind of file to look for.</param>
        /// <param name="key">The key the file was recorded under.</param>
        /// <param name="fallbackPath">The conventional path of the file, used when the index has no entry yet.</param>
        /// <param name="path">The path of the cached file when this method returns <c>true</c>.</param>
        public static bool TryGetFile(MediaCacheKind kind, string key, string? fallbackPath, out string path)
        {
            if (string.IsNullOrEmpty(key))
            {
                path = string.Empty;
                return false;
            }

            MediaCacheDatabase? db = database;

            if (db != null)
            {
                try
                {
                    MediaCacheEntrySnapshot? entry = db.Find(kind, key);

                    if (entry is { } cached)
                    {
                        // When both containers exist (for example a legacy webm next to a new mp4), prefer the
                        // path the caller expects so the platform appropriate file is played.
                        if (!string.IsNullOrEmpty(fallbackPath) && cached.Path != fallbackPath && IsFileCurrent(fallbackPath, -1))
                        {
                            Record(kind, key, fallbackPath);
                            path = fallbackPath;
                            return true;
                        }

                        if (IsFileCurrent(cached.Path, cached.Length))
                        {
                            db.Touch(kind, key, DateTimeOffset.UtcNow);
                            path = cached.Path;
                            return true;
                        }

                        db.Remove(kind, key);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $"Failed to query the media cache for {kind} [{key}].");
                }
            }

            if (!string.IsNullOrEmpty(fallbackPath) && IsFileCurrent(fallbackPath, -1))
            {
                Record(kind, key, fallbackPath);
                path = fallbackPath;
                return true;
            }

            path = string.Empty;
            return false;
        }

        /// <summary>
        /// Records that the supplied file is a complete cache file.
        /// </summary>
        public static void Record(MediaCacheKind kind, string key, string path)
        {
            MediaCacheDatabase? db = database;

            if (db == null || string.IsNullOrEmpty(key) || string.IsNullOrEmpty(path))
                return;

            try
            {
                var info = new FileInfo(path);

                if (!info.Exists || info.Length <= 0)
                {
                    db.Remove(kind, key);
                    return;
                }

                db.Record(kind, key, path, info.Length, info.LastWriteTimeUtc, DateTimeOffset.UtcNow);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to record {kind} [{key}] in the media cache.");
            }
        }

        /// <summary>
        /// Removes the index entry for the supplied key.
        /// </summary>
        public static void Remove(MediaCacheKind kind, string key)
        {
            try
            {
                database?.Remove(kind, key);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to remove {kind} [{key}] from the media cache.");
            }
        }

        /// <summary>
        /// Removes every index entry that points at the supplied path.
        /// </summary>
        public static void RemoveByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                database?.RemoveByPath(path);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to remove [{path}] from the media cache.");
            }
        }

        /// <summary>
        /// Whether at least one subtitle file has already been cached for the supplied video.
        /// </summary>
        public static bool HasCachedSubtitles(string videoId)
        {
            string? root = cacheRoot;

            if (string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(root))
                return false;

            try
            {
                string directory = Path.Combine(root, SUBTITLE_DIRECTORY, videoId);
                return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.srv3").Any();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to inspect the subtitle cache for [{videoId}].");
                return false;
            }
        }

        /// <summary>
        /// Records every subtitle file that was written for the supplied video.
        /// </summary>
        public static void RecordSubtitles(string videoId, string directory)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return;

                foreach (string file in Directory.EnumerateFiles(directory, "*.srv3"))
                    Record(MediaCacheKind.Subtitle, Path.GetFileName(file), file);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to record the subtitle cache for [{videoId}].");
            }
        }

        /// <summary>
        /// Returns aggregate information about the cache index.
        /// </summary>
        internal static MediaCacheStatistics GetStatistics()
        {
            try
            {
                return database?.GetStatistics() ?? MediaCacheStatistics.Empty;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to read the media cache statistics.");
                return MediaCacheStatistics.Empty;
            }
        }

        /// <summary>
        /// Returns a snapshot of every index entry, most recently used first.
        /// </summary>
        internal static IReadOnlyList<MediaCacheEntrySnapshot> GetEntries()
        {
            try
            {
                return database?.GetEntries() ?? Array.Empty<MediaCacheEntrySnapshot>();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to read the media cache entries.");
                return Array.Empty<MediaCacheEntrySnapshot>();
            }
        }

        /// <summary>
        /// Verifies that a file still matches the metadata recorded for it.
        /// </summary>
        internal static bool IsFileCurrent(string path, long expectedLength)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var info = new FileInfo(path);

                if (!info.Exists || info.Length <= 0)
                    return false;

                return expectedLength < 0 || info.Length == expectedLength;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Closes the index and forgets the initialisation state. Intended for tests.
        /// </summary>
        internal static void Reset()
        {
            lock (initialise_lock)
            {
                database?.Dispose();
                database = null;
                cacheRoot = null;
                initialise_attempted = false;
            }
        }

        private static void importExistingFiles()
        {
            MediaCacheDatabase? db;
            string? root;

            lock (initialise_lock)
            {
                db = database;
                root = cacheRoot;
            }

            if (db == null || root == null)
                return;

            try
            {
                db.Prune();

                importMediaFiles(db, root);
                importThumbnailDirectory(db, root, VIDEO_THUMBNAIL_DIRECTORY, MediaCacheKind.VideoThumbnail, Path.GetFileNameWithoutExtension);
                importThumbnailDirectory(db, root, SOCIAL_VIDEO_THUMBNAIL_DIRECTORY, MediaCacheKind.VideoThumbnail, file => $"{Path.GetFileNameWithoutExtension(file)}-social");
                importThumbnailDirectory(db, root, PLAYLIST_THUMBNAIL_DIRECTORY, MediaCacheKind.PlaylistThumbnail, Path.GetFileNameWithoutExtension);
                importThumbnailDirectory(db, root, CHANNEL_THUMBNAIL_DIRECTORY, MediaCacheKind.ChannelThumbnail, Path.GetFileNameWithoutExtension);
                importSubtitleFiles(db, root);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to import the existing cache files into the media cache index.");
            }
        }

        private static void importMediaFiles(MediaCacheDatabase db, string root)
        {
            string videos = Path.Combine(root, VIDEO_DIRECTORY);

            if (!Directory.Exists(videos))
                return;

            foreach (string directory in Directory.EnumerateDirectories(videos))
            {
                string id = Path.GetFileName(directory);

                importFile(db, MediaCacheKind.AudioStream, id, Path.Combine(directory, "audio.ogg"));
                importFile(db, MediaCacheKind.AudioStream, id, Path.Combine(directory, "audio.m4a"));
                importFile(db, MediaCacheKind.VideoStream, id, Path.Combine(directory, "video.mp4"));
                importFile(db, MediaCacheKind.VideoStream, id, Path.Combine(directory, "video.webm"));
            }
        }

        private static void importThumbnailDirectory(MediaCacheDatabase db, string root, string directoryName, MediaCacheKind kind, Func<string, string> keySelector)
        {
            string directory = Path.Combine(root, directoryName);

            if (!Directory.Exists(directory))
                return;

            foreach (string file in Directory.EnumerateFiles(directory, "*.png"))
                importFile(db, kind, keySelector(file), file);
        }

        private static void importSubtitleFiles(MediaCacheDatabase db, string root)
        {
            string directory = Path.Combine(root, SUBTITLE_DIRECTORY);

            if (!Directory.Exists(directory))
                return;

            foreach (string file in Directory.EnumerateFiles(directory, "*.srv3", SearchOption.AllDirectories))
                importFile(db, MediaCacheKind.Subtitle, Path.GetFileName(file), file);
        }

        private static void importFile(MediaCacheDatabase db, MediaCacheKind kind, string key, string path)
        {
            if (!IsFileCurrent(path, -1))
                return;

            try
            {
                var info = new FileInfo(path);
                db.Record(kind, key, path, info.Length, info.LastWriteTimeUtc, DateTimeOffset.UtcNow);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"Failed to import the cache file [{path}].");
            }
        }
    }
}

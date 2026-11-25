// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Realms;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// The Realm backed index of cached files.
    /// </summary>
    /// <remarks>
    /// Realm instances are confined to the thread that created them, so every operation opens a short-lived
    /// instance while holding a lock. Realm keeps the underlying file open and caches schema information, which
    /// makes this considerably cheaper than it looks. All failures are propagated to <see cref="MediaCache"/>,
    /// which treats the index as an optional accelerator and never lets a cache problem break playback.
    /// </remarks>
    internal sealed class MediaCacheDatabase : IDisposable
    {
        private static readonly TimeSpan touch_interval = TimeSpan.FromMinutes(1);

        private readonly object gate = new object();
        private readonly RealmConfiguration configuration;

        private bool disposed;

        public MediaCacheDatabase(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

            string? directory = Path.GetDirectoryName(databasePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            configuration = new RealmConfiguration(databasePath)
            {
                SchemaVersion = 1,
                Schema = new[] { typeof(MediaCacheEntry) },
                ShouldDeleteIfMigrationNeeded = true,
            };

            // Opening once here surfaces missing native binaries and invalid paths during initialisation instead of
            // on the first playback request.
            using Realm realm = Realm.GetInstance(configuration);
        }

        /// <summary>
        /// Looks up an entry without changing it.
        /// </summary>
        public MediaCacheEntrySnapshot? Find(MediaCacheKind kind, string key)
        {
            string cacheKey = getCacheKey(kind, key);

            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);
                MediaCacheEntry? entry = realm.Find<MediaCacheEntry>(cacheKey);

                return entry == null ? null : snapshot(entry);
            }
        }

        /// <summary>
        /// Adds or updates the entry for the supplied file.
        /// </summary>
        public void Record(MediaCacheKind kind, string key, string path, long length, DateTimeOffset lastWriteTimeUtc, DateTimeOffset now)
        {
            string cacheKey = getCacheKey(kind, key);

            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);

                realm.Write(() =>
                {
                    MediaCacheEntry? entry = realm.Find<MediaCacheEntry>(cacheKey);

                    if (entry == null)
                    {
                        entry = new MediaCacheEntry
                        {
                            CacheKey = cacheKey,
                            Kind = kind.ToString(),
                            Key = key,
                            CreatedAt = now,
                        };

                        realm.Add(entry);
                    }

                    entry.Path = path;
                    entry.Length = length;
                    entry.LastWriteTimeUtc = lastWriteTimeUtc;
                    entry.LastAccessedAt = now;
                });
            }
        }

        /// <summary>
        /// Marks an entry as used, at most once per <see cref="touch_interval"/> to keep writes cheap.
        /// </summary>
        public void Touch(MediaCacheKind kind, string key, DateTimeOffset now)
        {
            string cacheKey = getCacheKey(kind, key);

            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);
                MediaCacheEntry? entry = realm.Find<MediaCacheEntry>(cacheKey);

                if (entry == null || now - entry.LastAccessedAt < touch_interval)
                    return;

                realm.Write(() =>
                {
                    entry.LastAccessedAt = now;
                    entry.AccessCount++;
                });
            }
        }

        /// <summary>
        /// Removes the entry for the supplied key, if it exists.
        /// </summary>
        public bool Remove(MediaCacheKind kind, string key)
        {
            string cacheKey = getCacheKey(kind, key);

            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);
                MediaCacheEntry? entry = realm.Find<MediaCacheEntry>(cacheKey);

                if (entry == null)
                    return false;

                realm.Write(() => realm.Remove(entry));
                return true;
            }
        }

        /// <summary>
        /// Removes every entry pointing at the supplied path.
        /// </summary>
        public int RemoveByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);
                List<MediaCacheEntry> matches = realm.All<MediaCacheEntry>().Where(entry => entry.Path == path).ToList();

                if (matches.Count == 0)
                    return 0;

                realm.Write(() =>
                {
                    foreach (MediaCacheEntry entry in matches)
                        realm.Remove(entry);
                });

                return matches.Count;
            }
        }

        /// <summary>
        /// Drops entries whose file has disappeared or no longer has the recorded size.
        /// </summary>
        public int Prune()
        {
            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);
                List<MediaCacheEntry> stale = realm.All<MediaCacheEntry>()
                                                 .ToList()
                                                 .Where(entry => !MediaCache.IsFileCurrent(entry.Path, entry.Length))
                                                 .ToList();

                if (stale.Count == 0)
                    return 0;

                realm.Write(() =>
                {
                    foreach (MediaCacheEntry entry in stale)
                        realm.Remove(entry);
                });

                return stale.Count;
            }
        }

        /// <summary>
        /// Returns a snapshot of every entry, most recently used first.
        /// </summary>
        public IReadOnlyList<MediaCacheEntrySnapshot> GetEntries()
        {
            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);

                return realm.All<MediaCacheEntry>()
                            .ToList()
                            .OrderByDescending(entry => entry.LastAccessedAt)
                            .Select(snapshot)
                            .ToList();
            }
        }

        /// <summary>
        /// Returns aggregate information about the index.
        /// </summary>
        public MediaCacheStatistics GetStatistics()
        {
            lock (gate)
            {
                throwIfDisposed();

                using Realm realm = Realm.GetInstance(configuration);
                var lengthByKind = new Dictionary<MediaCacheKind, long>();
                long totalLength = 0;
                int count = 0;

                foreach (MediaCacheEntry entry in realm.All<MediaCacheEntry>())
                {
                    count++;
                    totalLength += entry.Length;

                    if (Enum.TryParse(entry.Kind, out MediaCacheKind kind))
                    {
                        lengthByKind.TryGetValue(kind, out long current);
                        lengthByKind[kind] = current + entry.Length;
                    }
                }

                return new MediaCacheStatistics(count, totalLength, lengthByKind);
            }
        }

        public void Dispose()
        {
            lock (gate)
                disposed = true;
        }

        private static MediaCacheEntrySnapshot snapshot(MediaCacheEntry entry)
        {
            MediaCacheKind kind = Enum.TryParse(entry.Kind, out MediaCacheKind parsed) ? parsed : MediaCacheKind.VideoThumbnail;

            return new MediaCacheEntrySnapshot(kind, entry.Key, entry.Path, entry.Length, entry.CreatedAt, entry.LastAccessedAt, entry.AccessCount);
        }

        private static string getCacheKey(MediaCacheKind kind, string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return $"{(int)kind}|{key}";
        }

        private void throwIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(MediaCacheDatabase));
        }
    }

    /// <summary>
    /// An immutable copy of a <see cref="MediaCacheEntry"/> that can safely cross threads.
    /// </summary>
    internal readonly record struct MediaCacheEntrySnapshot(MediaCacheKind Kind, string Key, string Path, long Length, DateTimeOffset CreatedAt, DateTimeOffset LastAccessedAt, int AccessCount);

    /// <summary>
    /// Aggregate information about the media cache index.
    /// </summary>
    internal readonly record struct MediaCacheStatistics(int EntryCount, long TotalLength, IReadOnlyDictionary<MediaCacheKind, long> LengthByKind)
    {
        public static MediaCacheStatistics Empty => new MediaCacheStatistics(0, 0, new Dictionary<MediaCacheKind, long>());
    }
}

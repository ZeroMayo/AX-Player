// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using Realms;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// A single file tracked by the Realm backed media cache index.
    /// </summary>
    /// <remarks>
    /// The index only stores metadata; the file itself stays on disk exactly where the rest of the application
    /// expects it so that existing cache directories keep working without a migration step.
    /// </remarks>
    public partial class MediaCacheEntry : RealmObject
    {
        /// <summary>
        /// The composite primary key of this entry, made up of the cache kind and the caller supplied key.
        /// </summary>
        [PrimaryKey]
        public string CacheKey { get; set; } = string.Empty;

        /// <summary>
        /// The string representation of the <see cref="MediaCacheKind"/> this entry belongs to.
        /// </summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// The caller supplied key within the cache kind, for example a video id or a channel id.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The absolute path of the cached file at the time it was recorded.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// The size of the cached file in bytes, used to detect truncated or replaced files.
        /// </summary>
        public long Length { get; set; }

        /// <summary>
        /// The last write time of the cached file in UTC.
        /// </summary>
        public DateTimeOffset LastWriteTimeUtc { get; set; }

        /// <summary>
        /// When the entry was first added to the index.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// When the cached file was last used.
        /// </summary>
        public DateTimeOffset LastAccessedAt { get; set; }

        /// <summary>
        /// How often the cached file has been used.
        /// </summary>
        public int AccessCount { get; set; }
    }
}

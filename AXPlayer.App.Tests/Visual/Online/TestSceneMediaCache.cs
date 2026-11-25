// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using AXPlayer.App.Online;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.Online
{
    /// <summary>
    /// Covers the Realm backed cache index and the import of cache files written by older versions.
    /// </summary>
    [TestFixture]
    public partial class TestSceneMediaCache : AXPlayerTestScene
    {
        private TemporaryNativeStorage storage = null!;
        private MediaCacheDatabase database = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create isolated cache storage", () =>
            {
                MediaCache.Reset();

                storage = new TemporaryNativeStorage(@"media-cache-test");
                database = new MediaCacheDatabase(Path.Combine(storage.GetFullPath("cache_index"), MediaCache.DATABASE_FILE));
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("dispose the cache index", () =>
            {
                MediaCache.Reset();
                database?.Dispose();
                storage?.Dispose();
            });
        }

        [Test]
        public void TestRecordFindAndRemove()
        {
            string path = null!;
            long length = 0;
            DateTimeOffset createdAt = default;

            AddStep("write a cached audio file", () =>
            {
                path = createFile(Path.Combine("videos", "video-id", "audio.ogg"), "audio");
                length = new FileInfo(path).Length;
                createdAt = DateTimeOffset.UtcNow;

                database.Record(MediaCacheKind.AudioStream, "video-id", path, length, File.GetLastWriteTimeUtc(path), createdAt);
            });

            AddAssert("entry is found", () =>
            {
                MediaCacheEntrySnapshot? found = database.Find(MediaCacheKind.AudioStream, "video-id");
                return found.HasValue && found.Value.Path == path && found.Value.Length == length;
            });

            AddAssert("statistics include the entry", () =>
            {
                MediaCacheStatistics statistics = database.GetStatistics();
                return statistics.EntryCount == 1
                       && statistics.TotalLength == length
                       && statistics.LengthByKind.TryGetValue(MediaCacheKind.AudioStream, out long kindLength)
                       && kindLength == length;
            });

            AddStep("touch the entry", () => database.Touch(MediaCacheKind.AudioStream, "video-id", createdAt.AddMinutes(2)));

            AddAssert("touch is recorded", () =>
            {
                MediaCacheEntrySnapshot? found = database.Find(MediaCacheKind.AudioStream, "video-id");
                return found.HasValue && found.Value.AccessCount == 1 && found.Value.LastAccessedAt >= createdAt.AddMinutes(2);
            });

            AddAssert("remove reports the entry", () => database.Remove(MediaCacheKind.AudioStream, "video-id"));
            AddAssert("entry is gone", () => !database.Find(MediaCacheKind.AudioStream, "video-id").HasValue);
        }

        [Test]
        public void TestStaleEntriesArePruned()
        {
            string path = null!;

            AddStep("record a file that is removed afterwards", () =>
            {
                path = createFile(Path.Combine("videos", "stale-video", "video.webm"), "video");
                database.Record(MediaCacheKind.VideoStream, "stale-video", path, new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), DateTimeOffset.UtcNow);
                File.Delete(path);
            });

            AddAssert("the stale entry is pruned", () => database.Prune() == 1);
            AddAssert("the stale entry is gone", () => !database.Find(MediaCacheKind.VideoStream, "stale-video").HasValue);
        }

        [Test]
        public void TestRemoveByPathRemovesEveryKind()
        {
            string path = null!;

            AddStep("record the same file under two kinds", () =>
            {
                path = createFile(Path.Combine("cache", "shared.bin"), "shared");
                long length = new FileInfo(path).Length;
                DateTimeOffset now = DateTimeOffset.UtcNow;

                database.Record(MediaCacheKind.AudioStream, "shared", path, length, File.GetLastWriteTimeUtc(path), now);
                database.Record(MediaCacheKind.VideoStream, "shared", path, length, File.GetLastWriteTimeUtc(path), now);
            });

            AddAssert("both entries are removed", () => database.RemoveByPath(path) == 2);
            AddAssert("audio entry is gone", () => !database.Find(MediaCacheKind.AudioStream, "shared").HasValue);
            AddAssert("video entry is gone", () => !database.Find(MediaCacheKind.VideoStream, "shared").HasValue);
        }

        [Test]
        public void TestExistingCacheFilesAreImported()
        {
            string audioPath = null!;

            AddStep("write cache files in the layout used by older versions", () =>
            {
                audioPath = createFile(Path.Combine("videos", "video-id", "audio.ogg"), "audio");
                createFile(Path.Combine("videos", "video-id", "video.webm"), "video");
                createFile(Path.Combine("subtitleCache", "video-id", "video-id.en.srv3"), "subtitle");
                createFile(Path.Combine(MediaCache.VIDEO_THUMBNAIL_DIRECTORY, "video-id-2.png"), "thumbnail");

                MediaCache.Initialise(storage);
            });

            AddUntilStep("every cache file is imported", () => MediaCache.GetEntries().Count == 4);

            AddAssert("the cached audio is reused", () =>
            {
                bool found = MediaCache.TryGetFile(MediaCacheKind.AudioStream, "video-id", null, out string cachedPath);
                return found && cachedPath == audioPath;
            });

            AddAssert("the cached subtitle is indexed", () => MediaCache.GetEntries().Any(entry => entry.Kind == MediaCacheKind.Subtitle));
        }

        [Test]
        public void TestThumbnailIsReadFromCache()
        {
            byte[] expected = { 1, 2, 3, 4 };
            string path = null!;

            AddStep("write a cached thumbnail in the legacy layout", () =>
            {
                path = createFile(Path.Combine(MediaCache.VIDEO_THUMBNAIL_DIRECTORY, "cached-thumb.png"), expected);
                MediaCache.Initialise(storage);
            });

            AddUntilStep("thumbnail is imported", () => MediaCache.GetEntries().Any(entry => entry.Kind == MediaCacheKind.VideoThumbnail && entry.Key == "cached-thumb"));

            AddAssert("thumbnail is served from the cache", () =>
            {
                byte[] result = MediaThumbnailCache.GetThumbnailAsync(MediaCacheKind.VideoThumbnail, "cached-thumb", path, "http://127.0.0.1:1/not-used").GetAwaiter().GetResult();
                return result.SequenceEqual(expected);
            });
        }

        private string createFile(string relativePath, string contents)
        {
            string path = storage.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, contents);
            return path;
        }

        private string createFile(string relativePath, byte[] contents)
        {
            string path = storage.GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, contents);
            return path;
        }
    }
}

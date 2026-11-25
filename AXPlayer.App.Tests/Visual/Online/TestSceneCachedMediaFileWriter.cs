// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Online;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.Online
{
    /// <summary>
    /// Covers <see cref="CachedMediaFileWriter"/> guarding the cached media files of a video against
    /// concurrent downloads and against the temporary files an interrupted download leaves behind.
    /// </summary>
    [TestFixture]
    public partial class TestSceneCachedMediaFileWriter : AXPlayerTestScene
    {
        private TemporaryNativeStorage storage = null!;
        private string target = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create isolated cache storage", () =>
            {
                storage = new TemporaryNativeStorage(@"cached-media-file-writer-test");
                target = storage.GetFullPath(Path.Combine("videos", "M4z5DbtNnWc", "audio.ogg"));
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("dispose isolated cache storage", () => storage?.Dispose());
        }

        [Test]
        public void TestTemporaryFilesOfAnInterruptedDownloadAreRemoved()
        {
            string rawStreamTemporary = null!;
            string convertedTemporary = null!;
            Task write = null!;

            AddStep("leave the temporary files of an interrupted download behind", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                rawStreamTemporary = target + ".stream-0.tmp";
                convertedTemporary = target + ".tmp";

                File.WriteAllText(rawStreamTemporary, "leftover");
                File.WriteAllText(convertedTemporary, "leftover");

                // A read-only leftover cannot simply be overwritten, which is one of the ways a stale
                // download surfaces as an UnauthorizedAccessException.
                File.SetAttributes(rawStreamTemporary, FileAttributes.ReadOnly);
            });

            AddStep("write the audio file", () => write = CachedMediaFileWriter.WriteAsync(target, path => File.WriteAllTextAsync(path, "audio")));

            AddUntilStep("write completed", () => write.IsCompleted);

            AddAssert("write succeeded", () => write.IsCompletedSuccessfully);
            AddAssert("raw stream temporary was removed", () => !File.Exists(rawStreamTemporary));
            AddAssert("converted temporary was removed", () => !File.Exists(convertedTemporary));
            AddAssert("audio file was written", () => File.ReadAllText(target) == "audio");
        }

        [Test]
        public void TestTransientAccessFailureIsRetried()
        {
            int attempts = 0;
            Task write = null!;

            AddStep("write while the file stays locked for the first two attempts", () =>
            {
                attempts = 0;

                write = CachedMediaFileWriter.WriteAsync(target, _ =>
                {
                    if (Interlocked.Increment(ref attempts) < 3)
                        throw new UnauthorizedAccessException(@"simulated lock");

                    return Task.CompletedTask;
                });
            });

            AddUntilStep("write completed", () => write.IsCompleted);

            AddAssert("write eventually succeeded", () => write.IsCompletedSuccessfully);
            AddAssert("writer retried", () => attempts == 3);
        }

        [Test]
        public void TestPermanentAccessFailureIsReported()
        {
            Task write = null!;

            AddStep("write while the file stays locked", () => write = CachedMediaFileWriter.WriteAsync(target, _ => throw new UnauthorizedAccessException(@"simulated lock")));

            AddUntilStep("write completed", () => write.IsCompleted);

            AddAssert("failure is reported to the caller", () => write.IsFaulted && write.Exception!.InnerException is UnauthorizedAccessException);
        }

        [Test]
        public void TestConcurrentWritesAreSerialised()
        {
            int active = 0;
            int maximumActive = 0;
            Task first = null!;
            Task second = null!;

            AddStep("start two writes for the same file", () =>
            {
                active = 0;
                maximumActive = 0;

                async Task writeAsync(string _)
                {
                    int current = Interlocked.Increment(ref active);

                    if (current > maximumActive)
                        maximumActive = current;

                    await Task.Delay(100).ConfigureAwait(false);

                    Interlocked.Decrement(ref active);
                }

                first = CachedMediaFileWriter.WriteAsync(target, writeAsync);
                second = CachedMediaFileWriter.WriteAsync(target, writeAsync);
            });

            AddUntilStep("both writes completed", () => first.IsCompleted && second.IsCompleted);

            AddAssert("writes succeeded", () => first.IsCompletedSuccessfully && second.IsCompletedSuccessfully);
            AddAssert("writes did not overlap", () => maximumActive == 1);
        }
    }
}

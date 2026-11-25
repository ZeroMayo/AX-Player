// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Writes the cached media files (the audio and video streams) of a video.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Writes for the same cached file are serialised. Both the stream client and the converter create
    /// deterministic temporary files next to the target (for example "audio.ogg.stream-0.tmp"), so two
    /// downloads of the same video would otherwise open the same file at once and fail with an
    /// <see cref="UnauthorizedAccessException"/> or a sharing violation.
    /// </para>
    /// <para>
    /// Every attempt first removes the temporary files of an interrupted download. Windows keeps reporting
    /// access denied for such a leftover while it is still being deleted, so a failed attempt is retried
    /// after a short delay instead of taking the whole video down.
    /// </para>
    /// </remarks>
    public static class CachedMediaFileWriter
    {
        private const int write_attempts = 3;

        private static readonly TimeSpan retry_delay = TimeSpan.FromMilliseconds(250);

        private static readonly object gate_lock = new object();
        private static readonly Dictionary<string, WriteGate> gates = new Dictionary<string, WriteGate>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Writes the cached media file at <paramref name="targetPath"/>.
        /// </summary>
        /// <param name="targetPath">The path of the cached media file.</param>
        /// <param name="writeAsync">
        /// Writes the file to the path it is given. The path is always <paramref name="targetPath"/>; it is
        /// passed along so a caller cannot accidentally write to a stale path.
        /// </param>
        public static async Task WriteAsync(string targetPath, Func<string, Task> writeAsync)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
            ArgumentNullException.ThrowIfNull(writeAsync);

            string? directory = Path.GetDirectoryName(targetPath);

            using (await acquireGateAsync(targetPath).ConfigureAwait(false))
            {
                for (int attempt = 1;; attempt++)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);

                        if (File.Exists(targetPath))
                            clearReadOnlyAttribute(targetPath);

                        deleteTemporaryFiles(targetPath);
                        await writeAsync(targetPath).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception e) when (attempt < write_attempts && isFileSystemError(e))
                    {
                        Logger.Error(e, $"Failed to write the cached media file '{targetPath}' (attempt {attempt}/{write_attempts}), retrying.");
                        await Task.Delay(retry_delay * attempt).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Deletes a cached media file or one of its temporary files, ignoring the failure when another
        /// process or a scanner still has it open.
        /// </summary>
        /// <remarks>
        /// A failed delete is not fatal: the next write replaces the file anyway, and the write itself
        /// reports the error if the file stays locked.
        /// </remarks>
        public static void TryDelete(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                clearReadOnlyAttribute(path);
                File.Delete(path);
            }
            catch (Exception e)
            {
                // The file may also have been removed by another process in the meantime.
                if (File.Exists(path))
                    Logger.Error(e, $"Failed to delete the cached media file '{path}'.");
            }
        }

        private static void deleteTemporaryFiles(string targetPath)
        {
            string? directory = Path.GetDirectoryName(targetPath);

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            // YoutubeExplode keeps the raw stream in "{target}.stream-{index}.tmp" and the converter
            // output in "{target}.tmp". Both are leftovers once the previous attempt ended.
            foreach (string temporaryPath in Directory.EnumerateFiles(directory, Path.GetFileName(targetPath) + "*.tmp"))
            {
                clearReadOnlyAttribute(temporaryPath);
                File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Removes the read-only attribute, which would otherwise make an existing cache file
        /// impossible to replace.
        /// </summary>
        private static void clearReadOnlyAttribute(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);

            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        private static bool isFileSystemError(Exception exception) => exception is IOException || exception is UnauthorizedAccessException;

        private static async Task<IDisposable> acquireGateAsync(string path)
        {
            WriteGate gate;

            lock (gate_lock)
            {
                if (!gates.TryGetValue(path, out gate))
                {
                    gate = new WriteGate();
                    gates.Add(path, gate);
                }

                gate.Users++;
            }

            await gate.Semaphore.WaitAsync().ConfigureAwait(false);

            return new GateLease(path, gate);
        }

        private static void releaseGate(string path, WriteGate gate)
        {
            gate.Semaphore.Release();

            lock (gate_lock)
            {
                if (--gate.Users == 0)
                    gates.Remove(path);
            }
        }

        private sealed class WriteGate
        {
            public readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);

            public int Users;
        }

        private sealed class GateLease : IDisposable
        {
            private readonly string path;
            private WriteGate? gate;

            public GateLease(string path, WriteGate gate)
            {
                this.path = path;
                this.gate = gate;
            }

            public void Dispose()
            {
                WriteGate? current = Interlocked.Exchange(ref gate, null);

                if (current != null)
                    releaseGate(path, current);
            }
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.HighPerformance;
using osu.Framework.Extensions;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SixLabors.ImageSharp.Memory;

namespace AXPlayer.App.IO.Archives
{
    public sealed class ZipArchiveReader : ArchiveReader
    {
        /// <summary>
        /// Archives created by osu!stable still write out as Shift-JIS.
        /// We want to force this fallback rather than leave it up to the library/system.
        /// In the future we may want to change exports to set the zip UTF-8 flag and use that instead.
        /// </summary>
        public static readonly ArchiveEncoding DEFAULT_ENCODING;

        /// <summary>
        /// Guards all access to <see cref="archive"/> and the stream it reads from.
        /// SharpCompress keeps a single shared stream position for the whole archive and rewinds it to the local header of
        /// every entry it opens, so concurrent <see cref="GetStream"/> calls corrupt each other's reads
        /// (leading to garbage header offsets such as "Cannot seek to position 1637468204. End of stream reached at position 9698624").
        /// </summary>
        private readonly object archiveLock = new object();

        private readonly Stream archiveStream;
        private readonly IWritableArchive archive;

        /// <summary>
        /// Lazily built lookup of entry name to entry, so reading an entry does not have to scan every entry in the archive.
        /// Only accessed while <see cref="archiveLock"/> is held.
        /// </summary>
        private Dictionary<string, IArchiveEntry> entries;

        /// <summary>
        /// Lazily built, cached list of readable filenames. Only accessed while <see cref="archiveLock"/> is held.
        /// </summary>
        private string[] filenames;

        private bool isDisposed;

        static ZipArchiveReader()
        {
            // Required to support rare code pages.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            DEFAULT_ENCODING = new ArchiveEncoding
            {
                Default = Encoding.GetEncoding(932),
                Password = Encoding.GetEncoding(932),
            };
        }

        public ZipArchiveReader(Stream archiveStream, string name = null)
            : base(name)
        {
            if (archiveStream == null)
                throw new ArgumentNullException(nameof(archiveStream));

            // SharpCompress needs to be able to seek around the archive for the lifetime of the reader.
            // Buffer non-seekable sources up front so entries can always be (re-)read.
            this.archiveStream = archiveStream.CanSeek ? archiveStream : copyToMemory(archiveStream);

            archive = ZipArchive.OpenArchive(this.archiveStream, new ReaderOptions
            {
                ArchiveEncoding = DEFAULT_ENCODING
            });
        }

        public override Stream GetStream(string name)
        {
            if (name == null)
                return null;

            lock (archiveLock)
            {
                if (isDisposed)
                    return null;

                if (!getEntryMap().TryGetValue(name, out IArchiveEntry entry))
                    return null;

                try
                {
                    using (Stream s = entry.OpenEntryStream())
                    {
                        if (entry.Size > 0 && entry.Size <= int.MaxValue)
                        {
                            var owner = MemoryAllocator.Default.Allocate<byte>((int)entry.Size);

                            // Read as much as the entry actually contains rather than throwing if the archive under-reports
                            // (see the `Size == 0` workaround below for the opposite case).
                            int bytesRead = s.ReadAtLeast(owner.Memory.Span, owner.Memory.Length, false);

                            if (bytesRead > 0)
                                return new MemoryOwnerMemoryStream(owner, bytesRead);

                            owner.Dispose();
                            return Stream.Null;
                        }

                        // due to a sharpcompress bug (https://github.com/adamhathcock/sharpcompress/issues/88),
                        // in rare instances the `ZipArchiveEntry` will not contain a correct `Size` but instead report 0.
                        // this would lead to the block above reading nothing, and the game basically seeing an archive full of empty files.
                        // since the bug is years old now, and this is a rather rare situation anyways (reported once in years),
                        // work around this locally by falling back to reading as many bytes as possible and using a standard non-pooled memory stream.
                        return new MemoryStream(s.ReadAllRemainingBytesToArray());
                    }
                }
                catch (Exception e)
                {
                    // The archive is read-only content (skins, emoji, ...), so a single unreadable entry should not take the game down.
                    Logger.Log($"Failed to read entry \"{name}\" from archive \"{Name}\": {e.Message}", LoggingTarget.Runtime, LogLevel.Important);
                    return null;
                }
            }
        }

        public override void Dispose()
        {
            lock (archiveLock)
            {
                if (isDisposed)
                    return;

                isDisposed = true;
                entries = null;
                filenames = null;

                archive.Dispose();
                archiveStream.Dispose();
            }
        }

        public override IEnumerable<string> Filenames
        {
            get
            {
                lock (archiveLock)
                {
                    if (isDisposed)
                        return Array.Empty<string>();

                    getEntryMap();

                    return filenames;
                }
            }
        }

        /// <summary>
        /// Copies a non-seekable source into memory, taking ownership of (and disposing) the source stream.
        /// </summary>
        private static MemoryStream copyToMemory(Stream source)
        {
            using (source)
            {
                var buffer = new MemoryStream();
                source.CopyTo(buffer);
                buffer.Position = 0;
                return buffer;
            }
        }

        /// <summary>
        /// Builds (once) and returns the entry lookup of this archive. Must be called while <see cref="archiveLock"/> is held.
        /// </summary>
        private Dictionary<string, IArchiveEntry> getEntryMap()
        {
            if (entries != null)
                return entries;

            entries = new Dictionary<string, IArchiveEntry>();

            foreach (IArchiveEntry entry in archive.Entries)
            {
                if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key))
                    continue;

                // Duplicate keys exist in the wild; the first one wins (this used to throw via SingleOrDefault()).
                entries.TryAdd(entry.Key, entry);
            }

            filenames = entries.Keys.ExcludeSystemFileNames().ToArray();

            return entries;
        }

        private class MemoryOwnerMemoryStream : Stream
        {
            private readonly IMemoryOwner<byte> owner;
            private readonly Stream stream;

            public MemoryOwnerMemoryStream(IMemoryOwner<byte> owner, int length)
            {
                this.owner = owner;

                stream = owner.Memory.Slice(0, length).AsStream();
            }

            protected override void Dispose(bool disposing)
            {
                owner?.Dispose();
                base.Dispose(disposing);
            }

            public override void Flush() => stream.Flush();

            public override int Read(byte[] buffer, int offset, int count) => stream.Read(buffer, offset, count);

            public override int Read(Span<byte> buffer) => stream.Read(buffer);

            public override long Seek(long offset, SeekOrigin origin) => stream.Seek(offset, origin);

            public override void SetLength(long value) => stream.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) => stream.Write(buffer, offset, count);

            public override bool CanRead => stream.CanRead;

            public override bool CanSeek => stream.CanSeek;

            public override bool CanWrite => stream.CanWrite;

            public override long Length => stream.Length;

            public override long Position
            {
                get => stream.Position;
                set => stream.Position = value;
            }
        }
    }
}

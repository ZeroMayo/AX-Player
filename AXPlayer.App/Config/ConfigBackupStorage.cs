// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Platform;

namespace AXPlayer.App.Config
{
    /// <summary>
    /// Stores timestamped copies of an INI configuration file inside a <c>backups</c> directory.
    /// </summary>
    internal static class ConfigBackupStorage
    {
        /// <summary>
        /// Directory (relative to the configuration storage) that holds every backup file.
        /// </summary>
        internal const string BACKUP_DIRECTORY = @"backups";

        /// <summary>
        /// Maximum number of backups kept per configuration file. Older backups are removed first.
        /// </summary>
        internal const int MAX_BACKUPS_PER_CONFIG = 10;

        private const string BACKUP_EXTENSION = @".bak";

        private static readonly object sync_root = new object();

        /// <summary>
        /// Copies the current contents of <paramref name="sourceFilename"/> into a new timestamped backup.
        /// </summary>
        /// <returns>The name of the created backup, usable with <see cref="Read"/>, <see cref="Delete"/> and the restore methods.</returns>
        public static string Create(Storage storage, string sourceFilename)
        {
            lock (sync_root)
            {
                Storage backupStorage = getBackupStorage(storage);
                string backupName = getUniqueBackupName(backupStorage, sourceFilename);

                using (Stream source = storage.GetStream(sourceFilename))
                using (Stream target = backupStorage.GetStream(backupName, FileAccess.Write, FileMode.Create))
                    source.CopyTo(target);

                prune(backupStorage, sourceFilename);
                return backupName;
            }
        }

        /// <summary>
        /// Returns every backup of <paramref name="sourceFilename"/>, newest first.
        /// </summary>
        public static IReadOnlyList<string> GetAll(Storage storage, string sourceFilename)
        {
            lock (sync_root)
                return enumerate(getBackupStorage(storage), sourceFilename)
                       .OrderByDescending(name => name, StringComparer.Ordinal)
                       .ToArray();
        }

        /// <summary>
        /// Reads a backup into memory without modifying the live configuration file.
        /// </summary>
        /// <returns>The backup contents, or <c>null</c> if the name does not refer to an existing backup of <paramref name="sourceFilename"/>.</returns>
        public static byte[] Read(Storage storage, string sourceFilename, string backupFilename)
        {
            if (!tryNormaliseName(sourceFilename, backupFilename, out string backupName))
                return null;

            lock (sync_root)
            {
                Storage backupStorage = getBackupStorage(storage);

                if (!backupStorage.Exists(backupName))
                    return null;

                using (Stream source = backupStorage.GetStream(backupName))
                using (var buffer = new MemoryStream())
                {
                    source.CopyTo(buffer);
                    return buffer.ToArray();
                }
            }
        }

        /// <summary>
        /// Overwrites <paramref name="sourceFilename"/> with the given contents.
        /// </summary>
        public static bool Write(Storage storage, string sourceFilename, byte[] contents)
        {
            if (contents == null)
                return false;

            lock (sync_root)
            {
                using (Stream target = storage.GetStream(sourceFilename, FileAccess.Write, FileMode.Create))
                    target.Write(contents, 0, contents.Length);

                return true;
            }
        }

        /// <summary>
        /// Deletes a backup of <paramref name="sourceFilename"/>.
        /// </summary>
        /// <returns><c>true</c> if a matching backup was deleted.</returns>
        public static bool Delete(Storage storage, string sourceFilename, string backupFilename)
        {
            if (!tryNormaliseName(sourceFilename, backupFilename, out string backupName))
                return false;

            lock (sync_root)
            {
                Storage backupStorage = getBackupStorage(storage);

                if (!backupStorage.Exists(backupName))
                    return false;

                backupStorage.Delete(backupName);
                return true;
            }
        }

        private static Storage getBackupStorage(Storage storage) => storage.GetStorageForDirectory(BACKUP_DIRECTORY);

        private static string getUniqueBackupName(Storage backupStorage, string sourceFilename)
        {
            string timestamp = DateTime.Now.ToString(@"yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string prefix = $@"{sourceFilename}.{timestamp}.";

            // Continue after the highest existing sequence for this timestamp instead of filling gaps.
            // Older backups may have just been pruned; reusing their sequence would overwrite a newer backup.
            int nextSequence = 0;

            foreach (string existing in enumerate(backupStorage, sourceFilename))
            {
                if (!existing.StartsWith(prefix, StringComparison.Ordinal) || !existing.EndsWith(BACKUP_EXTENSION, StringComparison.Ordinal))
                    continue;

                string sequenceText = existing.Substring(prefix.Length, existing.Length - prefix.Length - BACKUP_EXTENSION.Length);

                if (int.TryParse(sequenceText, NumberStyles.None, CultureInfo.InvariantCulture, out int sequence))
                    nextSequence = Math.Max(nextSequence, sequence + 1);
            }

            return $@"{sourceFilename}.{timestamp}.{nextSequence:D2}{BACKUP_EXTENSION}";
        }

        private static IEnumerable<string> enumerate(Storage backupStorage, string sourceFilename) =>
            backupStorage.GetFiles(string.Empty, $@"{sourceFilename}.*{BACKUP_EXTENSION}")
                          .Select(Path.GetFileName)
                          .Where(name => tryNormaliseName(sourceFilename, name, out _));

        private static void prune(Storage backupStorage, string sourceFilename)
        {
            string[] names = enumerate(backupStorage, sourceFilename).ToArray();
            string[] stale = names.OrderBy(name => name, StringComparer.Ordinal)
                                  .Take(Math.Max(0, names.Length - MAX_BACKUPS_PER_CONFIG))
                                  .ToArray();

            foreach (string name in stale)
                backupStorage.Delete(name);
        }

        private static bool tryNormaliseName(string sourceFilename, string backupFilename, out string backupName)
        {
            backupName = string.Empty;

            if (string.IsNullOrWhiteSpace(backupFilename))
                return false;

            // Only accept plain file names that belong to this configuration file. This keeps restore/delete
            // operations confined to the backup directory and prevents reading arbitrary storage files.
            if (backupFilename.IndexOf('/') >= 0 || backupFilename.IndexOf('\\') >= 0)
                return false;

            if (!backupFilename.StartsWith($@"{sourceFilename}.", StringComparison.Ordinal)
                || !backupFilename.EndsWith(BACKUP_EXTENSION, StringComparison.Ordinal))
                return false;

            backupName = backupFilename;
            return true;
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace AXPlayer.App.Overlays.Containers
{
    public partial class SettingsContainer
    {
        [Resolved]
        private AXPlayerConfigManager appConfig { get; set; } = null!;

        [Resolved]
        private AudioEffectsConfigManager audioEffectsConfig { get; set; } = null!;

        private SettingsBackupDropdown settingsBackupDropdown = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            refreshSettingsBackups();
        }

        private void refreshSettingsBackups(SettingsBackupEntry? preferred = null)
        {
            if (settingsBackupDropdown == null)
                return;

            SettingsBackupEntry? previouslySelected = preferred ?? settingsBackupDropdown.Current.Value;

            List<SettingsBackupEntry> entries = appConfig.GetBackups()
                                                       .Select(name => new SettingsBackupEntry(SettingsBackupTarget.AppSettings, name))
                                                       .Concat(audioEffectsConfig.GetBackups()
                                                                                  .Select(name => new SettingsBackupEntry(SettingsBackupTarget.AudioEffects, name)))
                                                       .OrderByDescending(entry => entry.CreatedAt)
                                                       .ThenBy(entry => entry.FileName, StringComparer.Ordinal)
                                                       .ToList();

            settingsBackupDropdown.Items = entries;
            settingsBackupDropdown.Current.Value = entries.FirstOrDefault(entry => entry.Equals(previouslySelected)) ?? entries.FirstOrDefault();
        }

        private void backUpSettings()
        {
            string? appBackup = null;

            try
            {
                appBackup = appConfig.CreateBackup();
                audioEffectsConfig.CreateBackup();
            }
            catch
            {
                popupDialogOverlay.ShowDialog(
                    AXPlayerStrings.SettingsBackup,
                    AXPlayerStrings.SettingsBackupFailed,
                    AXPlayerStrings.Close,
                    AXPlayerStrings.Close,
                    showActionButtons: false);

                return;
            }

            refreshSettingsBackups(appBackup == null ? null : new SettingsBackupEntry(SettingsBackupTarget.AppSettings, appBackup));

            popupDialogOverlay.ShowDialog(
                AXPlayerStrings.SettingsBackup,
                AXPlayerStrings.SettingsBackupCreated,
                AXPlayerStrings.Close,
                AXPlayerStrings.Close,
                showActionButtons: false);
        }

        private void confirmRestoreSettingsBackup()
        {
            SettingsBackupEntry? entry = settingsBackupDropdown.Current.Value;

            if (entry == null)
            {
                popupDialogOverlay.ShowDialog(
                    AXPlayerStrings.SettingsBackup,
                    AXPlayerStrings.SettingsBackupEmpty,
                    AXPlayerStrings.Close,
                    AXPlayerStrings.Close,
                    showActionButtons: false);

                return;
            }

            popupDialogOverlay.ShowDialog(
                AXPlayerStrings.SettingsBackupRestoreConfirmation,
                AXPlayerStrings.SettingsBackupRestoreConfirmationInfo(entry.FileName),
                AXPlayerStrings.RestoreSettings,
                AXPlayerStrings.Cancel,
                okPressedAction: () => restoreSettingsBackup(entry));
        }

        private void restoreSettingsBackup(SettingsBackupEntry entry)
        {
            bool restored;

            try
            {
                restored = entry.Target == SettingsBackupTarget.AppSettings
                    ? appConfig.RestoreBackup(entry.FileName)
                    : audioEffectsConfig.RestoreBackup(entry.FileName);
            }
            catch
            {
                restored = false;
            }

            refreshSettingsBackups(entry);

            popupDialogOverlay.ShowDialog(
                AXPlayerStrings.SettingsBackup,
                restored ? AXPlayerStrings.SettingsBackupRestored : AXPlayerStrings.SettingsBackupRestoreFailed,
                AXPlayerStrings.Close,
                AXPlayerStrings.Close,
                showActionButtons: false);
        }
    }

    internal enum SettingsBackupTarget
    {
        AppSettings,
        AudioEffects,
    }

    /// <summary>
    /// A single entry in the settings backup dropdown, combining the configuration it belongs to with its timestamp.
    /// </summary>
    internal sealed class SettingsBackupEntry : IEquatable<SettingsBackupEntry>
    {
        public SettingsBackupTarget Target { get; }

        public string FileName { get; }

        public DateTime CreatedAt { get; }

        public LocalisableString DisplayText { get; }

        public SettingsBackupEntry(SettingsBackupTarget target, string fileName)
        {
            Target = target;
            FileName = fileName;
            CreatedAt = parseTimestamp(target, fileName);

            string timestamp = CreatedAt == DateTime.MinValue
                ? fileName
                : CreatedAt.ToString(@"g", CultureInfo.CurrentCulture);

            DisplayText = target == SettingsBackupTarget.AppSettings
                ? AXPlayerStrings.SettingsBackupAppItem(timestamp)
                : AXPlayerStrings.SettingsBackupAudioEffectsItem(timestamp);
        }

        private static DateTime parseTimestamp(SettingsBackupTarget target, string fileName)
        {
            string sourceFilename = target == SettingsBackupTarget.AppSettings
                ? AXPlayerConfigManager.FILENAME
                : AudioEffectsConfigManager.FILENAME;

            string prefix = $@"{sourceFilename}.";

            if (!fileName.StartsWith(prefix, StringComparison.Ordinal) || fileName.Length < prefix.Length + 15)
                return DateTime.MinValue;

            return DateTime.TryParseExact(
                fileName.Substring(prefix.Length, 15),
                @"yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime timestamp) ? timestamp : DateTime.MinValue;
        }

        public bool Equals(SettingsBackupEntry? other) =>
            other != null && Target == other.Target && string.Equals(FileName, other.FileName, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is SettingsBackupEntry other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Target, FileName);
    }

    internal partial class SettingsBackupDropdown : FormDropdown<SettingsBackupEntry>
    {
        protected override LocalisableString GenerateItemText(SettingsBackupEntry? item)
            => item?.DisplayText ?? AXPlayerStrings.SettingsBackupEmpty;
    }
}

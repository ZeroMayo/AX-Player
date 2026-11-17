// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using AXPlayer.App.Config;
using AXPlayer.App.Overlays.Containers;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osuTK;

namespace AXPlayer.App.Tests.Visual.Config
{
    [TestFixture]
    public partial class TestSceneConfigBackups : AXPlayerTestScene
    {
        private TemporaryNativeStorage storage = null!;
        private AXPlayerConfigManager appConfig = null!;
        private AudioEffectsConfigManager audioEffectsConfig = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create isolated config storage", () =>
            {
                storage = new TemporaryNativeStorage(@"config-backup-test");
                appConfig = new AXPlayerConfigManager(storage);
                audioEffectsConfig = new AudioEffectsConfigManager(storage);
            });
        }

        [TearDownSteps]
        public void TearDownSteps()
        {
            AddStep("dispose isolated config storage", () => storage?.Dispose());
        }

        [Test]
        public void TestAppConfigBackupAndRestore()
        {
            string backup = null!;

            AddStep("disable system cursor and create backup", () =>
            {
                appConfig.GetBindable<bool>(AXPlayerSetting.UseSystemCursor).Value = false;
                backup = appConfig.CreateBackup();
            });

            AddAssert("backup is listed", () => appConfig.GetBackups().Contains(backup));

            AddStep("change setting", () => appConfig.GetBindable<bool>(AXPlayerSetting.UseSystemCursor).Value = true);

            AddAssert("setting was changed", () => appConfig.GetBindable<bool>(AXPlayerSetting.UseSystemCursor).Value);

            AddAssert("backup is restored", () => appConfig.RestoreBackup(backup));

            AddAssert("old value is restored", () => !appConfig.GetBindable<bool>(AXPlayerSetting.UseSystemCursor).Value);

            AddAssert("current config was backed up before restoring", () => appConfig.GetBackups().Count == 2);
        }

        [Test]
        public void TestAudioEffectsBackupAndRestore()
        {
            const string preset_name = "backup-restore-preset";

            string backup = null!;

            AddStep("configure effects and save preset", () =>
            {
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled).Value = true;
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value = 2.5f;
                audioEffectsConfig.SavePreset(preset_name);
            });

            AddStep("create backup", () => backup = audioEffectsConfig.CreateBackup());

            AddStep("change effects and delete preset", () =>
            {
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value = 0.5f;
                audioEffectsConfig.DeletePreset(preset_name);
            });

            AddAssert("backup is restored", () => audioEffectsConfig.RestoreBackup(backup));

            AddAssert("effect values are restored", () =>
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value == 2.5f
                && audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled).Value);

            AddAssert("presets are restored", () => audioEffectsConfig.HasPreset(preset_name));
        }

        [Test]
        public void TestDeleteAndInvalidBackupNames()
        {
            string backup = null!;

            AddStep("create backup", () => backup = appConfig.CreateBackup());

            AddAssert("backup is deleted", () => appConfig.DeleteBackup(backup));
            AddAssert("deleting a missing backup returns false", () => !appConfig.DeleteBackup(backup));
            AddAssert("no backups remain", () => appConfig.GetBackups().Count == 0);

            AddAssert("config file itself is rejected", () => !appConfig.RestoreBackup(AXPlayerConfigManager.FILENAME));
            AddAssert("foreign backups are rejected", () => !appConfig.RestoreBackup(AudioEffectsConfigManager.FILENAME));
            AddAssert("path traversal is rejected", () => !appConfig.RestoreBackup(@"../app.ini.bak"));
            AddAssert("empty names are rejected", () => !appConfig.RestoreBackup(string.Empty));
        }

        [Test]
        public void TestBackupEntriesAreDisplayed()
        {
            SettingsBackupEntry appEntry = null!;

            AddStep("show backup dropdown", () =>
            {
                appEntry = new SettingsBackupEntry(SettingsBackupTarget.AppSettings, @"app.ini.20260712-143005.00.bak");
                var audioEntry = new SettingsBackupEntry(SettingsBackupTarget.AudioEffects, @"audio_effects.ini.20260712-143005.00.bak");

                Child = new SettingsBackupDropdown
                {
                    Width = 400,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Items = new[] { appEntry, audioEntry },
                    Current = { Value = appEntry },
                };
            });

            AddAssert("timestamp is parsed", () => appEntry.CreatedAt == new DateTime(2026, 7, 12, 14, 30, 5));

            AddAssert("entries for different configs are distinct", () => !appEntry.Equals(new SettingsBackupEntry(SettingsBackupTarget.AudioEffects, @"audio_effects.ini.20260712-143005.00.bak")));
        }

        [Test]
        public void TestOldBackupsArePruned()
        {
            AddStep("create more backups than the limit", () =>
            {
                for (int i = 0; i < ConfigBackupStorage.MAX_BACKUPS_PER_CONFIG + 3; i++)
                {
                    appConfig.GetBindable<int>(AXPlayerSetting.CaptionCornerRadius).Value = i;
                    appConfig.CreateBackup();
                }
            });

            AddAssert("only the newest backups are kept", () =>
                appConfig.GetBackups().Count == ConfigBackupStorage.MAX_BACKUPS_PER_CONFIG);

            AddAssert("newest backup wins", () =>
            {
                string newest = appConfig.GetBackups().First();
                return appConfig.RestoreBackup(newest)
                       && appConfig.GetBindable<int>(AXPlayerSetting.CaptionCornerRadius).Value
                       == ConfigBackupStorage.MAX_BACKUPS_PER_CONFIG + 2;
            });
        }
    }
}

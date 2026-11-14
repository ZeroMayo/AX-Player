// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using AXPlayer.App.Config;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.Config
{
    [TestFixture]
    public partial class TestSceneAudioEffectsPresets : AXPlayerTestScene
    {
        [Resolved]
        private AudioEffectsConfigManager audioEffectsConfig { get; set; } = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("clear presets", () =>
            {
                foreach (AudioEffectsPreset preset in audioEffectsConfig.GetPresets().ToArray())
                    audioEffectsConfig.DeletePreset(preset.Name);
            });
        }

        [Test]
        public void TestSaveAndApplyPreset()
        {
            const string name = "test-save-apply";

            AddStep("configure effects", () =>
            {
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled).Value = true;
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value = 2.5f;
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EchoEnabled).Value = true;
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDelay).Value = 3.5f;
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled).Value = true;
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth).Value = 5;
            });

            AddAssert("preset is saved", () => audioEffectsConfig.SavePreset(name));

            AddStep("change effects", () =>
            {
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled).Value = false;
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value = 0;
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EchoEnabled).Value = false;
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDelay).Value = 0;
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled).Value = false;
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth).Value = 8;
            });

            AddAssert("preset is applied", () => audioEffectsConfig.ApplyPreset(name));

            AddAssert("enabled states are restored", () =>
                audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled).Value
                && audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EchoEnabled).Value
                && audioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled).Value);

            AddAssert("parameter values are restored", () =>
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value == 2.5f
                && audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDelay).Value == 3.5f
                && audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth).Value == 5);
        }

        [Test]
        public void TestReplacingPreset()
        {
            const string name = "test-replace";

            AddStep("configure and save preset", () =>
            {
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbRoomSize).Value = 0.25f;
                audioEffectsConfig.SavePreset(name);
            });

            AddStep("save preset with different casing and values", () =>
            {
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbRoomSize).Value = 0.75f;
                audioEffectsConfig.SavePreset(name.ToUpperInvariant());
            });

            AddAssert("preset is replaced instead of duplicated", () =>
                audioEffectsConfig.GetPresets().Count(preset => preset.ReverbRoomSize is 0.25f or 0.75f) == 1);

            AddAssert("latest values are stored", () =>
                audioEffectsConfig.ApplyPreset(name)
                && audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbRoomSize).Value == 0.75f);
        }

        [Test]
        public void TestPresetNameValidation()
        {
            AddAssert("blank names are rejected", () => !audioEffectsConfig.SavePreset("   "));

            AddAssert("names are trimmed", () =>
                audioEffectsConfig.SavePreset("  trimmed-preset  ")
                && audioEffectsConfig.GetPresets().Any(preset => preset.Name == "trimmed-preset"));
        }

        [Test]
        public void TestDeletePreset()
        {
            const string name = "test-delete";

            AddStep("save preset", () => Assert.That(audioEffectsConfig.SavePreset(name), Is.True));

            AddAssert("preset exists", () => audioEffectsConfig.HasPreset(name));
            AddAssert("preset is deleted", () => audioEffectsConfig.DeletePreset(name));
            AddAssert("preset no longer exists", () => !audioEffectsConfig.HasPreset(name) && !audioEffectsConfig.ApplyPreset(name));
            AddAssert("deleting a missing preset returns false", () => !audioEffectsConfig.DeletePreset(name));
        }

        [Test]
        public void TestAppliedValuesAreClamped()
        {
            AddStep("apply out-of-range preset", () => audioEffectsConfig.ApplyPreset(new AudioEffectsPreset
            {
                Name = "out-of-range",
                ReverbWetMix = 100,
                EchoDelay = -50,
                ChorusRate = 99999,
                EightBitEffectBitDepth = 100,
            }));

            AddAssert("values are clamped to their valid range", () =>
                audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value == 3
                && audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDelay).Value == 0
                && audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusRate).Value == 1000
                && audioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth).Value == 8);
        }
    }
}

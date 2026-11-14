// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using osu.Framework;
using osu.Framework.Configuration;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Platform;
using AXPlayer.App.Localisation;
using AXPlayer.App.Input.Binding;

namespace AXPlayer.App.Config
{
    public class AudioEffectsConfigManager : IniConfigManager<AudioEffectsSetting>
    {
        internal const string FILENAME = @"audio_effects.ini";

        protected override string Filename => FILENAME;

        public GlobalShortcutManager? ShortcutManager { get; set; }

        protected override void InitialiseDefaults()
        {
            SetDefault(AudioEffectsSetting.ReverbEnabled, false);
            SetDefault(AudioEffectsSetting.ReverbWetMix, 1f, 0f, 3f, 0.01f);
            SetDefault(AudioEffectsSetting.ReverbRoomSize, 0.5f, 0f, 1f, 0.01f);
            SetDefault(AudioEffectsSetting.ReverbDamp, 0.5f, 0f, 1f, 0.01f);
            SetDefault(AudioEffectsSetting.ReverbStereoWidth, 1f, 0f, 1f, 0.01f);

            SetDefault(AudioEffectsSetting.RotateEnabled, false);
            SetDefault(AudioEffectsSetting.RotateRate, 0.1f, 0f, 1f, 0.01f);

            SetDefault(AudioEffectsSetting.EchoEnabled, false);
            SetDefault(AudioEffectsSetting.EchoDryMix, 2f, 0f, 4f, 0.01f);
            SetDefault(AudioEffectsSetting.EchoWetMix, 3f, 0f, 4f, 0.01f);
            SetDefault(AudioEffectsSetting.EchoFeedback, 1f, 0f, 2f, 0.01f);
            SetDefault(AudioEffectsSetting.EchoDelay, 0f, 0f, 6f, 0.01f);

            SetDefault(AudioEffectsSetting.DistortionEnabled, false);
            SetDefault(AudioEffectsSetting.DistortionVolume, 0.3f, 0f, 2f, 0.01f);
            SetDefault(AudioEffectsSetting.DistortionDrive, 0f, 0f, 5f, 0.1f);

            SetDefault(AudioEffectsSetting.KaraokeEnabled, false);
            SetDefault(AudioEffectsSetting.KaraokeVocalVolume, 0f, 0f, 1f, 0.1f);

            SetDefault(AudioEffectsSetting.ChorusEnabled, false);
            SetDefault(AudioEffectsSetting.ChorusDryMix, 2f, 0f, 4f, 0.01f);
            SetDefault(AudioEffectsSetting.ChorusWetMix, 3f, 0f, 4f, 0.01f);
            SetDefault(AudioEffectsSetting.ChorusFeedback, 1f, 0f, 2f, 0.01f);
            SetDefault(AudioEffectsSetting.ChorusMinSweep, 1f, 0f, 6000f, 1f);
            SetDefault(AudioEffectsSetting.ChorusMaxSweep, 400f, 0f, 6000f, 1f);
            SetDefault(AudioEffectsSetting.ChorusRate, 200f, 0f, 1000f, 1f);

            SetDefault(AudioEffectsSetting.EightBitEffectEnabled, false);
            SetDefault(AudioEffectsSetting.EightBitEffectBitDepth, 8, 4, 8, 1);
            SetDefault(AudioEffectsSetting.EightBitEffectDownsampleRatio, 0.25f, 0.1f, 1f, 0.01f);

            SetDefault(AudioEffectsSetting.Presets, string.Empty);
        }

        public AudioEffectsConfigManager(Storage storage, IDictionary<AudioEffectsSetting, object> defaultOverrides = null) : base(storage, defaultOverrides)
        {
        }

        /// <summary>
        /// Maximum length of a stored preset name.
        /// </summary>
        public const int MAX_PRESET_NAME_LENGTH = 64;

        /// <summary>
        /// Returns all saved presets in their stored order.
        /// </summary>
        public IReadOnlyList<AudioEffectsPreset> GetPresets() => loadPresets();

        /// <summary>
        /// Returns whether a preset with the given name exists.
        /// </summary>
        public bool HasPreset(string name)
        {
            string sanitized = sanitizePresetName(name);
            return !string.IsNullOrEmpty(sanitized)
                   && loadPresets().Any(preset => string.Equals(preset.Name, sanitized, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Captures the current audio effect configuration as a preset without storing it.
        /// </summary>
        public AudioEffectsPreset CaptureCurrentEffects(string name) => capturePreset(sanitizePresetName(name));

        /// <summary>
        /// Captures the current audio effect configuration and stores it under the given name.
        /// An existing preset with the same name is replaced.
        /// </summary>
        /// <returns><c>true</c> if the preset was saved.</returns>
        public bool SavePreset(string name)
        {
            string sanitized = sanitizePresetName(name);

            if (string.IsNullOrEmpty(sanitized))
                return false;

            List<AudioEffectsPreset> presets = loadPresets();
            AudioEffectsPreset preset = capturePreset(sanitized);

            int existingIndex = presets.FindIndex(existing => string.Equals(existing.Name, sanitized, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
                presets[existingIndex] = preset;
            else
                presets.Add(preset);

            storePresets(presets);
            return true;
        }

        /// <summary>
        /// Applies the preset with the given name to the current audio effect configuration.
        /// </summary>
        /// <returns><c>true</c> if a matching preset was found.</returns>
        public bool ApplyPreset(string name)
        {
            string sanitized = sanitizePresetName(name);

            AudioEffectsPreset preset = loadPresets()
                .FirstOrDefault(existing => string.Equals(existing.Name, sanitized, StringComparison.OrdinalIgnoreCase));

            if (preset == null)
                return false;

            ApplyPreset(preset);
            return true;
        }

        /// <summary>
        /// Applies the given preset to the current audio effect configuration.
        /// </summary>
        public void ApplyPreset(AudioEffectsPreset preset)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));

            GetBindable<bool>(AudioEffectsSetting.ReverbEnabled).Value = preset.ReverbEnabled;
            GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value = preset.ReverbWetMix;
            GetBindable<float>(AudioEffectsSetting.ReverbRoomSize).Value = preset.ReverbRoomSize;
            GetBindable<float>(AudioEffectsSetting.ReverbDamp).Value = preset.ReverbDamp;
            GetBindable<float>(AudioEffectsSetting.ReverbStereoWidth).Value = preset.ReverbStereoWidth;

            GetBindable<bool>(AudioEffectsSetting.RotateEnabled).Value = preset.RotateEnabled;
            GetBindable<float>(AudioEffectsSetting.RotateRate).Value = preset.RotateRate;

            GetBindable<bool>(AudioEffectsSetting.EchoEnabled).Value = preset.EchoEnabled;
            GetBindable<float>(AudioEffectsSetting.EchoDryMix).Value = preset.EchoDryMix;
            GetBindable<float>(AudioEffectsSetting.EchoWetMix).Value = preset.EchoWetMix;
            GetBindable<float>(AudioEffectsSetting.EchoFeedback).Value = preset.EchoFeedback;
            GetBindable<float>(AudioEffectsSetting.EchoDelay).Value = preset.EchoDelay;

            GetBindable<bool>(AudioEffectsSetting.DistortionEnabled).Value = preset.DistortionEnabled;
            GetBindable<float>(AudioEffectsSetting.DistortionVolume).Value = preset.DistortionVolume;
            GetBindable<float>(AudioEffectsSetting.DistortionDrive).Value = preset.DistortionDrive;

            GetBindable<bool>(AudioEffectsSetting.KaraokeEnabled).Value = preset.KaraokeEnabled;
            GetBindable<float>(AudioEffectsSetting.KaraokeVocalVolume).Value = preset.KaraokeVocalVolume;

            GetBindable<bool>(AudioEffectsSetting.ChorusEnabled).Value = preset.ChorusEnabled;
            GetBindable<float>(AudioEffectsSetting.ChorusDryMix).Value = preset.ChorusDryMix;
            GetBindable<float>(AudioEffectsSetting.ChorusWetMix).Value = preset.ChorusWetMix;
            GetBindable<float>(AudioEffectsSetting.ChorusFeedback).Value = preset.ChorusFeedback;
            GetBindable<float>(AudioEffectsSetting.ChorusMinSweep).Value = preset.ChorusMinSweep;
            GetBindable<float>(AudioEffectsSetting.ChorusMaxSweep).Value = preset.ChorusMaxSweep;
            GetBindable<float>(AudioEffectsSetting.ChorusRate).Value = preset.ChorusRate;

            GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled).Value = preset.EightBitEffectEnabled;
            GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth).Value = preset.EightBitEffectBitDepth;
            GetBindable<float>(AudioEffectsSetting.EightBitEffectDownsampleRatio).Value = preset.EightBitEffectDownsampleRatio;
        }

        /// <summary>
        /// Deletes the preset with the given name.
        /// </summary>
        /// <returns><c>true</c> if a matching preset was found.</returns>
        public bool DeletePreset(string name)
        {
            string sanitized = sanitizePresetName(name);
            List<AudioEffectsPreset> presets = loadPresets();

            int removed = presets.RemoveAll(existing => string.Equals(existing.Name, sanitized, StringComparison.OrdinalIgnoreCase));

            if (removed == 0)
                return false;

            storePresets(presets);
            return true;
        }

        private AudioEffectsPreset capturePreset(string name) => new AudioEffectsPreset
        {
            Name = name,

            ReverbEnabled = GetBindable<bool>(AudioEffectsSetting.ReverbEnabled).Value,
            ReverbWetMix = GetBindable<float>(AudioEffectsSetting.ReverbWetMix).Value,
            ReverbRoomSize = GetBindable<float>(AudioEffectsSetting.ReverbRoomSize).Value,
            ReverbDamp = GetBindable<float>(AudioEffectsSetting.ReverbDamp).Value,
            ReverbStereoWidth = GetBindable<float>(AudioEffectsSetting.ReverbStereoWidth).Value,

            RotateEnabled = GetBindable<bool>(AudioEffectsSetting.RotateEnabled).Value,
            RotateRate = GetBindable<float>(AudioEffectsSetting.RotateRate).Value,

            EchoEnabled = GetBindable<bool>(AudioEffectsSetting.EchoEnabled).Value,
            EchoDryMix = GetBindable<float>(AudioEffectsSetting.EchoDryMix).Value,
            EchoWetMix = GetBindable<float>(AudioEffectsSetting.EchoWetMix).Value,
            EchoFeedback = GetBindable<float>(AudioEffectsSetting.EchoFeedback).Value,
            EchoDelay = GetBindable<float>(AudioEffectsSetting.EchoDelay).Value,

            DistortionEnabled = GetBindable<bool>(AudioEffectsSetting.DistortionEnabled).Value,
            DistortionVolume = GetBindable<float>(AudioEffectsSetting.DistortionVolume).Value,
            DistortionDrive = GetBindable<float>(AudioEffectsSetting.DistortionDrive).Value,

            KaraokeEnabled = GetBindable<bool>(AudioEffectsSetting.KaraokeEnabled).Value,
            KaraokeVocalVolume = GetBindable<float>(AudioEffectsSetting.KaraokeVocalVolume).Value,

            ChorusEnabled = GetBindable<bool>(AudioEffectsSetting.ChorusEnabled).Value,
            ChorusDryMix = GetBindable<float>(AudioEffectsSetting.ChorusDryMix).Value,
            ChorusWetMix = GetBindable<float>(AudioEffectsSetting.ChorusWetMix).Value,
            ChorusFeedback = GetBindable<float>(AudioEffectsSetting.ChorusFeedback).Value,
            ChorusMinSweep = GetBindable<float>(AudioEffectsSetting.ChorusMinSweep).Value,
            ChorusMaxSweep = GetBindable<float>(AudioEffectsSetting.ChorusMaxSweep).Value,
            ChorusRate = GetBindable<float>(AudioEffectsSetting.ChorusRate).Value,

            EightBitEffectEnabled = GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled).Value,
            EightBitEffectBitDepth = GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth).Value,
            EightBitEffectDownsampleRatio = GetBindable<float>(AudioEffectsSetting.EightBitEffectDownsampleRatio).Value,
        };

        private List<AudioEffectsPreset> loadPresets()
        {
            string encoded = GetBindable<string>(AudioEffectsSetting.Presets).Value;

            if (string.IsNullOrEmpty(encoded))
                return new List<AudioEffectsPreset>();

            List<AudioEffectsPreset> deserialized;

            try
            {
                deserialized = JsonSerializer.Deserialize<List<AudioEffectsPreset>>(decode(encoded));
            }
            catch (Exception exception) when (exception is JsonException or FormatException or NotSupportedException)
            {
                return new List<AudioEffectsPreset>();
            }

            if (deserialized == null)
                return new List<AudioEffectsPreset>();

            List<AudioEffectsPreset> presets = new List<AudioEffectsPreset>();

            foreach (AudioEffectsPreset preset in deserialized)
            {
                if (preset == null)
                    continue;

                preset.Name = sanitizePresetName(preset.Name);

                if (string.IsNullOrEmpty(preset.Name))
                    continue;

                if (presets.Any(existing => string.Equals(existing.Name, preset.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                presets.Add(preset);
            }

            return presets;
        }

        private void storePresets(IEnumerable<AudioEffectsPreset> presets)
        {
            GetBindable<string>(AudioEffectsSetting.Presets).Value = encode(JsonSerializer.Serialize(presets));
        }

        private static string sanitizePresetName(string name)
        {
            name = name?.Trim() ?? string.Empty;

            if (name.Length > MAX_PRESET_NAME_LENGTH)
                name = name.Substring(0, MAX_PRESET_NAME_LENGTH).TrimEnd();

            return name;
        }

        private static string encode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string decode(string value)
        {
            string base64 = value.Replace('-', '+').Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;

                case 3:
                    base64 += "=";
                    break;
            }

            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings
        {
            new TrackedSetting<bool>(AudioEffectsSetting.ReverbEnabled, v => new SettingDescription(v, AXPlayerStrings.ReverbEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), getShortcut(GlobalAction.ToggleReverbEffect, "Shift+F1"))),
            new TrackedSetting<bool>(AudioEffectsSetting.RotateEnabled, v => new SettingDescription(v, AXPlayerStrings.RotateParameters_Enabled, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), getShortcut(GlobalAction.ToggleRotateEffect, "Shift+F2"))),
            new TrackedSetting<bool>(AudioEffectsSetting.EchoEnabled, v => new SettingDescription(v, AXPlayerStrings.EchoEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), getShortcut(GlobalAction.ToggleEchoEffect, "Shift+F3"))),
            new TrackedSetting<bool>(AudioEffectsSetting.DistortionEnabled, v => new SettingDescription(v, AXPlayerStrings.DistortionEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), getShortcut(GlobalAction.ToggleDistortionEffect, "Shift+F4"))),
            new TrackedSetting<bool>(AudioEffectsSetting.KaraokeEnabled, v => new SettingDescription(v, AXPlayerStrings.KaraokeMode, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), getShortcut(GlobalAction.ToggleKaraokeEffect, "Shift+F5"))),
            new TrackedSetting<bool>(AudioEffectsSetting.ChorusEnabled, v => new SettingDescription(v, AXPlayerStrings.ChorusEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), getShortcut(GlobalAction.ToggleChorusEffect, "Shift+F6"))),
            new TrackedSetting<bool>(AudioEffectsSetting.EightBitEffectEnabled, v => new SettingDescription(v, AXPlayerStrings.EightBitEffectEnabled, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), getShortcut(GlobalAction.ToggleEightBitEffect, "Shift+F7"))),
        };

        private string getShortcut(GlobalAction action, string fallback) =>
            ShortcutManager?.GetShortcutLabel(action, fallback) ?? fallback;
    }

    public enum AudioEffectsSetting
    {
        //reverb
        ReverbEnabled,
        ReverbWetMix,
        ReverbRoomSize,
        ReverbDamp,
        ReverbStereoWidth,

        //rotate
        RotateEnabled,
        RotateRate,

        //echo
        EchoEnabled,
        EchoDryMix,
        EchoWetMix,
        EchoFeedback,
        EchoDelay,

        //distortion
        DistortionEnabled,
        DistortionVolume,
        DistortionDrive,

        //karaoke (what else)
        KaraokeEnabled,
        KaraokeVocalVolume,

        //Chorus
        ChorusEnabled,
        ChorusDryMix,
        ChorusWetMix,
        ChorusFeedback,
        ChorusMinSweep,
        ChorusMaxSweep,
        ChorusRate,

        //8-bit
        EightBitEffectEnabled,
        EightBitEffectBitDepth,
        EightBitEffectDownsampleRatio,

        //saved presets
        Presets,
    }

    /// <summary>
    /// A named snapshot of every audio effect and its parameters.
    /// </summary>
    public class AudioEffectsPreset
    {
        public string Name { get; set; } = string.Empty;

        //reverb
        public bool ReverbEnabled { get; set; }
        public float ReverbWetMix { get; set; }
        public float ReverbRoomSize { get; set; }
        public float ReverbDamp { get; set; }
        public float ReverbStereoWidth { get; set; }

        //rotate
        public bool RotateEnabled { get; set; }
        public float RotateRate { get; set; }

        //echo
        public bool EchoEnabled { get; set; }
        public float EchoDryMix { get; set; }
        public float EchoWetMix { get; set; }
        public float EchoFeedback { get; set; }
        public float EchoDelay { get; set; }

        //distortion
        public bool DistortionEnabled { get; set; }
        public float DistortionVolume { get; set; }
        public float DistortionDrive { get; set; }

        //karaoke
        public bool KaraokeEnabled { get; set; }
        public float KaraokeVocalVolume { get; set; }

        //chorus
        public bool ChorusEnabled { get; set; }
        public float ChorusDryMix { get; set; }
        public float ChorusWetMix { get; set; }
        public float ChorusFeedback { get; set; }
        public float ChorusMinSweep { get; set; }
        public float ChorusMaxSweep { get; set; }
        public float ChorusRate { get; set; }

        //8-bit
        public bool EightBitEffectEnabled { get; set; }
        public float EightBitEffectBitDepth { get; set; }
        public float EightBitEffectDownsampleRatio { get; set; }
    }
}

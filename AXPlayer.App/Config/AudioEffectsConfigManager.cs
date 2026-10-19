// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework;
using osu.Framework.Configuration;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Platform;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Config
{
    public class AudioEffectsConfigManager : IniConfigManager<AudioEffectsSetting>
    {
        internal const string FILENAME = @"audio_effects.ini";

        protected override string Filename => FILENAME;

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
        }

        public AudioEffectsConfigManager(Storage storage, IDictionary<AudioEffectsSetting, object> defaultOverrides = null) : base(storage, defaultOverrides)
        {
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings
        {
            new TrackedSetting<bool>(AudioEffectsSetting.ReverbEnabled, v => new SettingDescription(v, AXPlayerStrings.ReverbEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+F1")),
            new TrackedSetting<bool>(AudioEffectsSetting.RotateEnabled, v => new SettingDescription(v, AXPlayerStrings.RotateParameters_Enabled, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+F2")),
            new TrackedSetting<bool>(AudioEffectsSetting.EchoEnabled, v => new SettingDescription(v, AXPlayerStrings.EchoEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+F3")),
            new TrackedSetting<bool>(AudioEffectsSetting.DistortionEnabled, v => new SettingDescription(v, AXPlayerStrings.DistortionEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+F4")),
            new TrackedSetting<bool>(AudioEffectsSetting.KaraokeEnabled, v => new SettingDescription(v, AXPlayerStrings.KaraokeMode, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+F5")),
            new TrackedSetting<bool>(AudioEffectsSetting.ChorusEnabled, v => new SettingDescription(v, AXPlayerStrings.ChorusEffect, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+F6")),
            new TrackedSetting<bool>(AudioEffectsSetting.EightBitEffectEnabled, v => new SettingDescription(v, AXPlayerStrings.EightBitEffectEnabled, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+F7")),
        };
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
    }
}

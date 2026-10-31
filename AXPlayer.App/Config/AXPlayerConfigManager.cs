// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using AXPlayer.App.Graphics;
using AXPlayer.App.Localisation;
using osu.Framework.Configuration;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Platform;

namespace AXPlayer.App.Config
{
    public class AXPlayerConfigManager : IniConfigManager<AXPlayerSetting>
    {
        internal const string FILENAME = @"app.ini";

        protected override string Filename => FILENAME;

        protected override void InitialiseDefaults()
        {
            SetDefault(AXPlayerSetting.UsernameDisplayMode, UsernameDisplayMode.DisplayName);
            SetDefault(AXPlayerSetting.ClosedCaptionLanguage, ClosedCaptionLanguage.Disabled);
            SetDefault(AXPlayerSetting.ClosedCaptionLanguageNew, 0);
            SetDefault(AXPlayerSetting.CaptionEnabled, false);
            SetDefault(AXPlayerSetting.AspectRatioMethod, AspectRatioMethod.Letterbox);
            SetDefault(AXPlayerSetting.VideoMetadataTranslateSource, VideoMetadataTranslateSource.YouTube);
            SetDefault(AXPlayerSetting.VideoQuality, VideoQuality.PreferHighQuality);
            SetDefault(AXPlayerSetting.AudioLanguage, Language.en);
            SetDefault(AXPlayerSetting.AdjustPitchOnSpeedChange, false);
            SetDefault(AXPlayerSetting.VideoDimLevel, 0, 0, .3, 0.01);
            SetDefault(AXPlayerSetting.ShowFpsDisplay, false);
            SetDefault(AXPlayerSetting.UIFont, UIFont.GoogleSansFlexRounded);

            SetDefault(AXPlayerSetting.Scaling, ScalingMode.Off);
            SetDefault(AXPlayerSetting.SafeAreaConsiderations, true);
            SetDefault(AXPlayerSetting.ScalingBackgroundDim, 0.9f, 0.5f, 1f, 0.01f);

            SetDefault(AXPlayerSetting.ScalingSizeX, 0.8f, 0.2f, 1f, 0.01f);
            SetDefault(AXPlayerSetting.ScalingSizeY, 0.8f, 0.2f, 1f, 0.01f);

            SetDefault(AXPlayerSetting.ScalingPositionX, 0.5f, 0f, 1f, 0.01f);
            SetDefault(AXPlayerSetting.ScalingPositionY, 0.5f, 0f, 1f, 0.01f);

            SetDefault(AXPlayerSetting.UIScale, 1f, 0.8f, 1.4f, 0.01f);

            SetDefault(AXPlayerSetting.ScreenshotFormat, ScreenshotFormat.Jpg);
            SetDefault(AXPlayerSetting.ScreenshotCaptureMenuCursor, true);

            SetDefault(AXPlayerSetting.CursorRotation, true);

            SetDefault(AXPlayerSetting.FinalLoginState, false);

            SetDefault(AXPlayerSetting.AlwaysUseOriginalAudio, true);

            SetDefault(AXPlayerSetting.UseSystemCursor, true);

            SetDefault(AXPlayerSetting.VideoBloomLevel, 0f, 0f, 1f, 0.01f);
            SetDefault(AXPlayerSetting.ChromaticAberrationStrength, 0f, 0f, 1f, 0.01f);
            SetDefault(AXPlayerSetting.VideoGrayscaleLevel, 0f, 0f, 1f, 0.01f);
            SetDefault(AXPlayerSetting.VideoHueShift, 0, 0, 360, 1);

            SetDefault(AXPlayerSetting.DiscordRichPresence, DiscordRichPresenceMode.Full);
            SetDefault(AXPlayerSetting.AudioNormalization, true);

            SetDefault(AXPlayerSetting.AccessToken, string.Empty);
            SetDefault(AXPlayerSetting.AudioQuality, AudioQuality.PreferHighQuality);
            SetDefault(AXPlayerSetting.CloseButtonAction, CloseButtonAction.HideToTrayIcon);
            SetDefault(AXPlayerSetting.ColourScheme, OverlayColourScheme.Lime);
            SetDefault(AXPlayerSetting.DarkMode, true);
            SetDefault(AXPlayerSetting.ProfileImageShape, ProfileImageShape.Circle);
            SetDefault(AXPlayerSetting.CommentsSortCriteria, CommentsSortCriteria.Top);

            SetDefault(AXPlayerSetting.ReleaseStream, ReleaseStream.Stable);

            SetDefault(AXPlayerSetting.ShowVideoMetadataOnWindowTitle, true);
            SetDefault(AXPlayerSetting.CaptionFont, CaptionFonts.GoogleSansFlex);

            SetDefault(AXPlayerSetting.VideoMetadataDisplayAlignment, VideoMetadataDisplayAlignment.Left);

            SetDefault(AXPlayerSetting.SearchSortCriteria, SearchSortCriteria.Relevance);

            SetDefault(AXPlayerSetting.PlayOverlaySFX, false);
            SetDefault(AXPlayerSetting.OverlaySFXType, SFXType.Normal);

            SetDefault(AXPlayerSetting.CaptionBGOpacity, 0.5f, 0.25f, 1f, 0.01f);
            SetDefault(AXPlayerSetting.ResetPlaybackSpeedWhenLoadingAVideo, false);
            SetDefault(AXPlayerSetting.CaptionBGColor, Colour4.Black);
            SetDefault(AXPlayerSetting.CaptionCornerRadius, 0, 0, 16);
            SetDefault(AXPlayerSetting.UseNewSubtitlesFeature, false);
            SetDefault(AXPlayerSetting.ScreenshotQuality, 92, 1, 100);
            SetDefault(AXPlayerSetting.PlayerControllerFadeStyle, PlayerControllerFadeStyle.Modern);
            SetDefault(AXPlayerSetting.WavySeekBar, true);

            SetDefault(AXPlayerSetting.Version, string.Empty);
            SetDefault(AXPlayerSetting.SmoothCaptionAnimation, true);
        }

        public AXPlayerConfigManager(Storage storage, IDictionary<AXPlayerSetting, object> defaultOverrides = null) : base(storage, defaultOverrides)
        {
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings
        {
            new TrackedSetting<ClosedCaptionLanguage>(AXPlayerSetting.ClosedCaptionLanguage, v => new SettingDescription(v, AXPlayerStrings.CaptionLanguage, v.GetLocalisableDescription(), "Shift+C")),
            new TrackedSetting<AspectRatioMethod>(AXPlayerSetting.AspectRatioMethod, v => new SettingDescription(v, AXPlayerStrings.AspectRatioMethod, v.GetLocalisableDescription(), "Ctrl+F6")),
            new TrackedSetting<bool>(AXPlayerSetting.AdjustPitchOnSpeedChange, v => new SettingDescription(v, AXPlayerStrings.AdjustPitchOnSpeedChange, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Alt+P")),
            new TrackedSetting<bool>(AXPlayerSetting.CaptionEnabled, v => new SettingDescription(v, AXPlayerStrings.ClosedCaptions, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Shift+C")),
            new TrackedSetting<float>(AXPlayerSetting.UIScale, v => new SettingDescription(v, AXPlayerStrings.UIScaling, $@"{v:0.##}x")),
            new TrackedSetting<bool>(AXPlayerSetting.ShowFpsDisplay, v => new SettingDescription(v, AXPlayerStrings.ShowFPS, v == true ? AXPlayerStrings.Enabled.ToLower() : AXPlayerStrings.Disabled.ToLower(), "Ctrl+P")),
            new TrackedSetting<ScalingMode>(AXPlayerSetting.Scaling, scalingMode => new SettingDescription(
                        rawValue: scalingMode,
                        name: AXPlayerStrings.ScreenScaling,
                        value: scalingMode.GetLocalisableDescription(),
                        shortcut: "Ctrl+Shift+F5"
                    )
                ),
            new TrackedSetting<ProfileImageShape>(AXPlayerSetting.ProfileImageShape, v => new SettingDescription(v, AXPlayerStrings.ProfileImageShape, v.GetLocalisableDescription(), "Ctrl+F8")),
        };
    }

    public enum AXPlayerSetting
    {
        UsernameDisplayMode,
        ClosedCaptionLanguage,
        CaptionEnabled,
        AspectRatioMethod,
        VideoMetadataTranslateSource,
        VideoQuality,
        UIScale,
        AudioLanguage,
        AdjustPitchOnSpeedChange,
        VideoDimLevel,
        ShowFpsDisplay,
        UIFont,
        Scaling,
        ScalingPositionX,
        ScalingPositionY,
        ScalingSizeX,
        ScalingSizeY,
        ScalingBackgroundDim,
        SafeAreaConsiderations,

        ScreenshotFormat,
        ScreenshotCaptureMenuCursor,

        CursorRotation,

        FinalLoginState,

        AlwaysUseOriginalAudio,
        UseSystemCursor,

        VideoBloomLevel,
        ChromaticAberrationStrength,
        VideoGrayscaleLevel,
        VideoHueShift,

        DiscordRichPresence,
        ClosedCaptionLanguageNew,
        AudioNormalization,

        AccessToken,
        AudioQuality,
        CloseButtonAction,
        ColourScheme,
        ProfileImageShape,
        CommentsSortCriteria,
        ReleaseStream,

        ShowVideoMetadataOnWindowTitle,
        CaptionFont,
        VideoMetadataDisplayAlignment,
        SearchSortCriteria,

        PlayOverlaySFX,
        OverlaySFXType,

        CaptionBGOpacity,
        ResetPlaybackSpeedWhenLoadingAVideo,
        CaptionBGColor,
        CaptionCornerRadius,
        UseNewSubtitlesFeature,
        ScreenshotQuality,
        PlayerControllerFadeStyle,
        WavySeekBar,
        Version,
        SmoothCaptionAnimation,
        DarkMode,
    }
}

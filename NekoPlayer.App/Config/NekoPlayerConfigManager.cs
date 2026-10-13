// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using NekoPlayer.App.Graphics;
using NekoPlayer.App.Localisation;
using osu.Framework.Configuration;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Platform;

namespace NekoPlayer.App.Config
{
    public class NekoPlayerConfigManager : IniConfigManager<NekoPlayerSetting>
    {
        internal const string FILENAME = @"app.ini";

        protected override string Filename => FILENAME;

        protected override void InitialiseDefaults()
        {
            SetDefault(NekoPlayerSetting.UsernameDisplayMode, UsernameDisplayMode.DisplayName);
            SetDefault(NekoPlayerSetting.ClosedCaptionLanguage, ClosedCaptionLanguage.Disabled);
            SetDefault(NekoPlayerSetting.ClosedCaptionLanguageNew, 0);
            SetDefault(NekoPlayerSetting.CaptionEnabled, false);
            SetDefault(NekoPlayerSetting.AspectRatioMethod, AspectRatioMethod.Letterbox);
            SetDefault(NekoPlayerSetting.VideoMetadataTranslateSource, VideoMetadataTranslateSource.YouTube);
            SetDefault(NekoPlayerSetting.VideoQuality, VideoQuality.PreferHighQuality);
            SetDefault(NekoPlayerSetting.AudioLanguage, Language.en);
            SetDefault(NekoPlayerSetting.AdjustPitchOnSpeedChange, false);
            SetDefault(NekoPlayerSetting.VideoDimLevel, 0, 0, .3, 0.01);
            SetDefault(NekoPlayerSetting.ShowFpsDisplay, false);
            SetDefault(NekoPlayerSetting.UIFont, UIFont.GoogleSansFlexRounded);

            SetDefault(NekoPlayerSetting.Scaling, ScalingMode.Off);
            SetDefault(NekoPlayerSetting.SafeAreaConsiderations, true);
            SetDefault(NekoPlayerSetting.ScalingBackgroundDim, 0.9f, 0.5f, 1f, 0.01f);

            SetDefault(NekoPlayerSetting.ScalingSizeX, 0.8f, 0.2f, 1f, 0.01f);
            SetDefault(NekoPlayerSetting.ScalingSizeY, 0.8f, 0.2f, 1f, 0.01f);

            SetDefault(NekoPlayerSetting.ScalingPositionX, 0.5f, 0f, 1f, 0.01f);
            SetDefault(NekoPlayerSetting.ScalingPositionY, 0.5f, 0f, 1f, 0.01f);

            SetDefault(NekoPlayerSetting.UIScale, 1f, 0.8f, 1.4f, 0.01f);

            SetDefault(NekoPlayerSetting.ScreenshotFormat, ScreenshotFormat.Jpg);
            SetDefault(NekoPlayerSetting.ScreenshotCaptureMenuCursor, true);

            SetDefault(NekoPlayerSetting.CursorRotation, true);

            SetDefault(NekoPlayerSetting.FinalLoginState, false);

            SetDefault(NekoPlayerSetting.AlwaysUseOriginalAudio, true);

            SetDefault(NekoPlayerSetting.UseSystemCursor, true);

            SetDefault(NekoPlayerSetting.VideoBloomLevel, 0f, 0f, 1f, 0.01f);
            SetDefault(NekoPlayerSetting.ChromaticAberrationStrength, 0f, 0f, 1f, 0.01f);
            SetDefault(NekoPlayerSetting.VideoGrayscaleLevel, 0f, 0f, 1f, 0.01f);
            SetDefault(NekoPlayerSetting.VideoHueShift, 0, 0, 360, 1);

            SetDefault(NekoPlayerSetting.DiscordRichPresence, DiscordRichPresenceMode.Full);
            SetDefault(NekoPlayerSetting.AudioNormalization, true);

            SetDefault(NekoPlayerSetting.AccessToken, string.Empty);
            SetDefault(NekoPlayerSetting.AudioQuality, AudioQuality.PreferHighQuality);
            SetDefault(NekoPlayerSetting.CloseButtonAction, CloseButtonAction.HideToTrayIcon);
            SetDefault(NekoPlayerSetting.ColourScheme, OverlayColourScheme.Lime);
            SetDefault(NekoPlayerSetting.ProfileImageShape, ProfileImageShape.Circle);
            SetDefault(NekoPlayerSetting.CommentsSortCriteria, CommentsSortCriteria.Top);

            SetDefault(NekoPlayerSetting.ReleaseStream, ReleaseStream.Stable);

            SetDefault(NekoPlayerSetting.ShowVideoMetadataOnWindowTitle, true);
            SetDefault(NekoPlayerSetting.CaptionFont, CaptionFonts.GoogleSansFlex);

            SetDefault(NekoPlayerSetting.VideoMetadataDisplayAlignment, VideoMetadataDisplayAlignment.Left);

            SetDefault(NekoPlayerSetting.SearchSortCriteria, SearchSortCriteria.Relevance);

            SetDefault(NekoPlayerSetting.PlayOverlaySFX, false);
            SetDefault(NekoPlayerSetting.OverlaySFXType, SFXType.Normal);

            SetDefault(NekoPlayerSetting.CaptionBGOpacity, 0.5f, 0.25f, 1f, 0.01f);
            SetDefault(NekoPlayerSetting.ResetPlaybackSpeedWhenLoadingAVideo, false);
            SetDefault(NekoPlayerSetting.CaptionBGColor, Colour4.Black);
            SetDefault(NekoPlayerSetting.CaptionCornerRadius, 0, 0, 16);
            SetDefault(NekoPlayerSetting.UseNewSubtitlesFeature, false);
            SetDefault(NekoPlayerSetting.ScreenshotQuality, 92, 1, 100);
        }

        public NekoPlayerConfigManager(Storage storage, IDictionary<NekoPlayerSetting, object> defaultOverrides = null) : base(storage, defaultOverrides)
        {
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings
        {
            new TrackedSetting<ClosedCaptionLanguage>(NekoPlayerSetting.ClosedCaptionLanguage, v => new SettingDescription(v, NekoPlayerStrings.CaptionLanguage, v.GetLocalisableDescription(), "Shift+C")),
            new TrackedSetting<AspectRatioMethod>(NekoPlayerSetting.AspectRatioMethod, v => new SettingDescription(v, NekoPlayerStrings.AspectRatioMethod, v.GetLocalisableDescription(), "Ctrl+F6")),
            new TrackedSetting<bool>(NekoPlayerSetting.AdjustPitchOnSpeedChange, v => new SettingDescription(v, NekoPlayerStrings.AdjustPitchOnSpeedChange, v == true ? NekoPlayerStrings.Enabled.ToLower() : NekoPlayerStrings.Disabled.ToLower(), "Alt+P")),
            new TrackedSetting<bool>(NekoPlayerSetting.CaptionEnabled, v => new SettingDescription(v, NekoPlayerStrings.ClosedCaptions, v == true ? NekoPlayerStrings.Enabled.ToLower() : NekoPlayerStrings.Disabled.ToLower(), "Shift+C")),
            new TrackedSetting<float>(NekoPlayerSetting.UIScale, v => new SettingDescription(v, NekoPlayerStrings.UIScaling, $@"{v:0.##}x")),
            new TrackedSetting<bool>(NekoPlayerSetting.ShowFpsDisplay, v => new SettingDescription(v, NekoPlayerStrings.ShowFPS, v == true ? NekoPlayerStrings.Enabled.ToLower() : NekoPlayerStrings.Disabled.ToLower(), "Ctrl+P")),
            new TrackedSetting<ScalingMode>(NekoPlayerSetting.Scaling, scalingMode => new SettingDescription(
                        rawValue: scalingMode,
                        name: NekoPlayerStrings.ScreenScaling,
                        value: scalingMode.GetLocalisableDescription(),
                        shortcut: "Ctrl+Shift+F5"
                    )
                ),
            new TrackedSetting<ProfileImageShape>(NekoPlayerSetting.ProfileImageShape, v => new SettingDescription(v, NekoPlayerStrings.ProfileImageShape, v.GetLocalisableDescription(), "Ctrl+F8")),
        };
    }

    public enum NekoPlayerSetting
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
    }
}

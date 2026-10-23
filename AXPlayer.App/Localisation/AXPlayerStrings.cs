// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace AXPlayer.App.Localisation
{
    public static class AXPlayerStrings
    {
        private const string prefix = @"AXPlayer.App.Resources.Localisation.AXPlayer";

        /// <summary>
        /// "{0} • {1} views • {2}"
        /// </summary>
        public static LocalisableString VideoMetadataDesc(string username, string views, string daysAgo) => new TranslatableString(getKey(@"video_metadata_desc"), "{0} • {1} views • {2}", username, views, daysAgo);

        /// <summary>
        /// "Playback speed"
        /// </summary>
        public static LocalisableString PlaybackSpeedWithoutValue => new TranslatableString(getKey(@"playback_speed_without_value"), "Playback speed");

        /// <summary>
        /// "Quick Action"
        /// </summary>
        public static LocalisableString QuickAction => new TranslatableString(getKey(@"quick_action"), "Quick Action");

        /// <summary>
        /// "Export logs"
        /// </summary>
        public static LocalisableString ExportLogs => new TranslatableString(getKey(@"export_logs"), "Export logs");

        /// <summary>
        /// "Load from video or playlist's ID or URL"
        /// </summary>
        public static LocalisableString LoadFromVideoId => new TranslatableString(getKey(@"load_from_video_id"), "Load from video or playlist's ID or URL");

        /// <summary>
        /// "Load Video or Playlist"
        /// </summary>
        public static LocalisableString LoadVideo => new TranslatableString(getKey(@"load_video"), "Load Video or Playlist");

        /// <summary>
        /// "Video ID must not be empty!"
        /// </summary>
        public static LocalisableString NoVideoIdError => new TranslatableString(getKey(@"error_noVideoId"), "Video ID must not be empty!");

        /// <summary>
        /// "{0} • {1} subscribers"
        /// </summary>
        public static LocalisableString ProfileImageTooltip(string username, string subs) => new TranslatableString(getKey(@"profile_image_tooltip"), "{0} • {1} subscribers", username, subs);

        /// <summary>
        /// "Settings"
        /// </summary>
        public static LocalisableString Settings => new TranslatableString(getKey(@"settings"), "Settings");

        /// <summary>
        /// "Screen resolution"
        /// </summary>
        public static LocalisableString ScreenResolution => new TranslatableString(getKey(@"screen_resolution"), "Screen resolution");

        /// <summary>
        /// "Disabled"
        /// </summary>
        public static LocalisableString CaptionDisabled => new TranslatableString(getKey(@"caption_disabled"), "Disabled");

        /// <summary>
        /// "General"
        /// </summary>
        public static LocalisableString General => new TranslatableString(getKey(@"general"), "General");

        /// <summary>
        /// "Graphics"
        /// </summary>
        public static LocalisableString Graphics => new TranslatableString(getKey(@"graphics"), "Graphics");

        /// <summary>
        /// "Language"
        /// </summary>
        public static LocalisableString Language => new TranslatableString(getKey(@"language"), "Language");

        /// <summary>
        /// "Display"
        /// </summary>
        public static LocalisableString Display => new TranslatableString(getKey(@"display"), "Display");

        /// <summary>
        /// "Screen mode"
        /// </summary>
        public static LocalisableString ScreenMode => new TranslatableString(getKey(@"screen_mode"), "Screen mode");

        /// <summary>
        /// "Closed caption language"
        /// </summary>
        public static LocalisableString CaptionLanguage => new TranslatableString(getKey(@"caption_language"), "Closed caption language");

        /// <summary>
        /// "Closed caption font"
        /// </summary>
        public static LocalisableString CaptionFont => new TranslatableString(getKey(@"caption_font"), "Closed caption font");

        /// <summary>
        /// "Press F11 to exit the full screen."
        /// </summary>
        public static LocalisableString FullscreenEntered => new TranslatableString(getKey(@"fullscreen_entered"), "Press F11 to exit the full screen.");

        /// <summary>
        /// "Audio"
        /// </summary>
        public static LocalisableString Audio => new TranslatableString(getKey(@"audio"), "Audio");

        /// <summary>
        /// "Video volume"
        /// </summary>
        public static LocalisableString VideoVolume => new TranslatableString(getKey(@"video_volume"), "Video volume");

        /// <summary>
        /// "SFX volume"
        /// </summary>
        public static LocalisableString SFXVolume => new TranslatableString(getKey(@"sfx_volume"), "SFX volume");

        /// <summary>
        /// "Selected caption: {0}"
        /// </summary>
        public static LocalisableString SelectedCaption(LocalisableString language) => new TranslatableString(getKey(@"selected_caption"), "Selected caption: {0}", language);

        /// <summary>
        /// "Selected caption: {0} (auto-generated)"
        /// </summary>
        public static LocalisableString SelectedCaptionAutoGen(LocalisableString language) => new TranslatableString(getKey(@"selected_caption_auto_gen"), "Selected caption: {0} (auto-generated)", language);

        /// <summary>
        /// "{0} (auto-generated)"
        /// </summary>
        public static LocalisableString CaptionAutoGen(LocalisableString language) => new TranslatableString(getKey(@"caption_auto_gen"), "{0} (auto-generated)", language);

        /// <summary>
        /// "Fill"
        /// </summary>
        public static LocalisableString Fill => new TranslatableString(getKey(@"fill"), "Fill");

        /// <summary>
        /// "Letterbox"
        /// </summary>
        public static LocalisableString Letterbox => new TranslatableString(getKey(@"letterbox"), "Letterbox");

        /// <summary>
        /// "Aspect ratio method"
        /// </summary>
        public static LocalisableString AspectRatioMethod => new TranslatableString(getKey(@"aspect_ratio_method"), "Aspect ratio method");

        /// <summary>
        /// "Estimated: {0} | Actual: {1}"
        /// </summary>
        public static LocalisableString DislikeCountTooltip(string estimated, string actual) => new TranslatableString(getKey(@"dislike_count_tooltip"), "Estimated: {0} | Actual: {1}", estimated, actual);

        /// <summary>
        /// "Video metadata translate source"
        /// </summary>
        public static LocalisableString VideoMetadataTranslateSource => new TranslatableString(getKey(@"video_metadata_translate_source"), "Video metadata translate source");

        /// <summary>
        /// "Google Translate"
        /// </summary>
        public static LocalisableString GoogleTranslate => new TranslatableString(getKey(@"google_translate"), "Google Translate");

        /// <summary>
        /// "Auto"
        /// </summary>
        public static LocalisableString Auto => new TranslatableString(getKey(@"auto"), "Auto");

        /// <summary>
        /// "Video"
        /// </summary>
        public static LocalisableString Video => new TranslatableString(getKey(@"video"), "Video");

        /// <summary>
        /// "Use hardware acceleration"
        /// </summary>
        public static LocalisableString UseHardwareAcceleration => new TranslatableString(getKey(@"use_hardware_acceleration"), @"Use hardware acceleration");

        /// <summary>
        /// "Minimise video player when switching to another app"
        /// </summary>
        public static LocalisableString MinimiseOnFocusLoss => new TranslatableString(getKey(@"minimise_on_focus_loss"), @"Minimise video player when switching to another app");

        /// <summary>
        /// "Prefer high quality"
        /// </summary>
        public static LocalisableString PreferHighQuality => new TranslatableString(getKey(@"prefer_high_quality"), "Prefer high quality");

        /// <summary>
        /// "Video quality"
        /// </summary>
        public static LocalisableString VideoQuality => new TranslatableString(getKey(@"video_quality"), "Video quality");

        /// <summary>
        /// "Master volume"
        /// </summary>
        public static LocalisableString MasterVolume => new TranslatableString(getKey(@"master_volume"), "Master volume");

        /// <summary>
        /// "Enabled"
        /// </summary>
        public static LocalisableString Enabled => new TranslatableString(getKey(@"enabled"), "Enabled");

        /// <summary>
        /// "Disabled"
        /// </summary>
        public static LocalisableString Disabled => new TranslatableString(getKey(@"disabled"), "Disabled");

        /// <summary>
        /// "UI scaling"
        /// </summary>
        public static LocalisableString UIScaling => new TranslatableString(getKey(@"ui_scaling"), @"UI scaling");

        /// <summary>
        /// "Audio tracks"
        /// </summary>
        public static LocalisableString AudioLanguage => new TranslatableString(getKey(@"audio_language"), @"Audio tracks");

        /// <summary>
        /// "Adjust pitch on speed change"
        /// </summary>
        public static LocalisableString AdjustPitchOnSpeedChange => new TranslatableString(getKey(@"adjust_pitch_on_speed_change"), @"Adjust pitch on speed change");

        /// <summary>
        /// "{0} views  {1}"
        /// </summary>
        public static LocalisableString VideoMetadataDescWithoutChannelName(string views, string daysAgo) => new TranslatableString(getKey(@"video_metadata_desc_without_channel_name"), "{0} views  {1}", views, daysAgo);

        /// <summary>
        /// "Comments ({0})"
        /// </summary>
        public static LocalisableString Comments(LocalisableString count) => new TranslatableString(getKey(@"comments"), "Comments ({0})", count);

        /// <summary>
        /// "Video dim level"
        /// </summary>
        public static LocalisableString VideoDimLevel => new TranslatableString(getKey(@"dim"), "Video dim level");

        /// <summary>
        /// "Translate to {0}"
        /// </summary>
        public static LocalisableString TranslateTo(LocalisableString targetLang) => new TranslatableString(getKey(@"translate_to"), "Translate to {0}", targetLang);

        /// <summary>
        /// "See original (Translated by Google)"
        /// </summary>
        public static LocalisableString TranslateViewOriginal => new TranslatableString(getKey(@"translate_view_original"), "See original (Translated by Google)");

        /// <summary>
        /// "{0} (Reply to {1})"
        /// </summary>
        public static LocalisableString CommentReply(string from, string to) => new TranslatableString(getKey(@"comment_reply"), "{0} (Reply to {1})", from, to);

        /// <summary>
        /// "You are running the latest release ({0})"
        /// </summary>
        public static LocalisableString RunningLatestRelease(string version) => new TranslatableString(getKey(@"running_latest_release"), @"You are running the latest release ({0})", version);

        /// <summary>
        /// "Updating AX Player ({0}%)"
        /// </summary>
        public static LocalisableString DownloadingUpdate(string percentage) => new TranslatableString(getKey(@"updating"), @"Updating AX Player ({0}%)", percentage);

        /// <summary>
        /// "The update is almost complete. Re-run AX Player to finish the update."
        /// </summary>
        public static LocalisableString RestartRequired => new TranslatableString(getKey(@"restart_required"), "The update is almost complete. Re-run AX Player to finish the update.");

        /// <summary>
        /// "Update failed!"
        /// </summary>
        public static LocalisableString UpdateFailed => new TranslatableString(getKey(@"update_failed"), "Update failed!");

        /// <summary>
        /// "Checking for update..."
        /// </summary>
        public static LocalisableString CheckingUpdate => new TranslatableString(getKey(@"checking_update"), "Checking for update...");

        /// <summary>
        /// "Check for updates"
        /// </summary>
        public static LocalisableString CheckUpdate => new TranslatableString(getKey(@"check_update"), "Check for updates");

        /// <summary>
        /// "Frame limiter"
        /// </summary>
        public static LocalisableString FrameLimiter => new TranslatableString(getKey(@"frame_limiter"), "Frame limiter");

        /// <summary>
        /// "Like count hidden by uploader"
        /// </summary>
        public static LocalisableString LikeCountHidden => new TranslatableString(getKey(@"like_count_hidden"), "Like count hidden by uploader");

        /// <summary>
        /// "Disabled by uploader"
        /// </summary>
        public static LocalisableString DisabledByUploader => new TranslatableString(getKey(@"disabled_by_uploader"), "Disabled by uploader");

        /// <summary>
        /// "Dislike count data is provided by the "
        /// </summary>
        public static LocalisableString DislikeCounterCredits_1 => new TranslatableString(getKey(@"dislike_counter_credits_1"), @"Dislike count data is provided by the ");

        /// <summary>
        /// "."
        /// </summary>
        public static LocalisableString DislikeCounterCredits_2 => new TranslatableString(getKey(@"dislike_counter_credits_2"), @".");

        /// <summary>
        /// "Default"
        /// </summary>
        public static LocalisableString Default => new TranslatableString(getKey(@"common_default"), "Default");

        /// <summary>
        /// "Show FPS"
        /// </summary>
        public static LocalisableString ShowFPS => new TranslatableString(getKey(@"show_fps"), "Show FPS");

        /// <summary>
        /// "Cannot play private videos."
        /// </summary>
        public static LocalisableString CannotPlayPrivateVideos => new TranslatableString(getKey(@"cannot_play_private_videos"), "Cannot play private videos.");

        /// <summary>
        /// "Play"
        /// </summary>
        public static LocalisableString Play => new TranslatableString(getKey(@"play"), "Play");

        /// <summary>
        /// "Pause"
        /// </summary>
        public static LocalisableString Pause => new TranslatableString(getKey(@"pause"), "Pause");

        /// <summary>
        /// "Playback speed: {0}"
        /// </summary>
        public static LocalisableString PlaybackSpeed(double value) => new TranslatableString(getKey(@"playback_speed"), "Playback speed: {0}", $@"{value:0.##}x");

        /// <summary>
        /// "Comments"
        /// </summary>
        public static LocalisableString CommentsWithoutCount => new TranslatableString(getKey(@"comments_without_count"), "Comments");

        /// <summary>
        /// "Setting the video quality to 8K may cause performance degradation, GPU overload, and driver crashes on some devices."
        /// </summary>
        public static LocalisableString VideoQuality8KWarning => new TranslatableString(getKey(@"video_quality_8k_warning"), "Setting the video quality to 8K may cause performance degradation, GPU overload, and driver crashes on some devices.");

        /// <summary>
        /// "Renderer"
        /// </summary>
        public static LocalisableString Renderer => new TranslatableString(getKey(@"renderer"), @"Renderer");

        /// <summary>
        /// "Exported logs!"
        /// </summary>
        public static LocalisableString LogsExportFinished => new TranslatableString(getKey(@"logs_export_finished"), @"Exported logs!");

        /// <summary>
        /// "Revert to default"
        /// </summary>
        public static LocalisableString RevertToDefault => new TranslatableString(getKey(@"revert_to_default"), @"Revert to default");

        /// <summary>
        /// "Everything"
        /// </summary>
        public static LocalisableString ScaleEverything => new TranslatableString(getKey(@"scale_everything"), @"Everything");

        /// <summary>
        /// "Video"
        /// </summary>
        public static LocalisableString ScaleVideo => new TranslatableString(getKey(@"scale_video"), @"Video");

        /// <summary>
        /// "Off"
        /// </summary>
        public static LocalisableString ScalingOff => new TranslatableString(getKey(@"scaling_off"), @"Off");

        /// <summary>
        /// "Screen scaling"
        /// </summary>
        public static LocalisableString ScreenScaling => new TranslatableString(getKey(@"screen_scaling"), @"Screen scaling");

        /// <summary>
        /// "Horizontal position"
        /// </summary>
        public static LocalisableString HorizontalPosition => new TranslatableString(getKey(@"horizontal_position"), @"Horizontal position");

        /// <summary>
        /// "Vertical position"
        /// </summary>
        public static LocalisableString VerticalPosition => new TranslatableString(getKey(@"vertical_position"), @"Vertical position");

        /// <summary>
        /// "Horizontal scale"
        /// </summary>
        public static LocalisableString HorizontalScale => new TranslatableString(getKey(@"horizontal_scale"), @"Horizontal scale");

        /// <summary>
        /// "Vertical scale"
        /// </summary>
        public static LocalisableString VerticalScale => new TranslatableString(getKey(@"vertical_scale"), @"Vertical scale");

        /// <summary>
        /// "Thumbnail dim"
        /// </summary>
        public static LocalisableString ThumbnailDim => new TranslatableString(getKey(@"thumbnail_dim"), @"Thumbnail dim");

        /// <summary>
        /// "Username display mode"
        /// </summary>
        public static LocalisableString UsernameDisplayMode => new TranslatableString(getKey(@"username_display_mode"), @"Username display mode");

        /// <summary>
        /// "Shrink app to avoid cameras and notches"
        /// </summary>
        public static LocalisableString ShrinkGameToSafeArea => new TranslatableString(getKey(@"shrink_game_to_safe_area"), @"Shrink app to avoid cameras and notches");

        /// <summary>
        /// "JPG (web-friendly)"
        /// </summary>
        public static LocalisableString Jpg => new TranslatableString(getKey(@"jpg_web_friendly"), @"JPG (web-friendly)");

        /// <summary>
        /// "PNG (lossless)"
        /// </summary>
        public static LocalisableString Png => new TranslatableString(getKey(@"png_lossless"), @"PNG (lossless)");

        /// <summary>
        /// "Screenshot saved!"
        /// </summary>
        public static LocalisableString ScreenshotSaved => new TranslatableString(getKey(@"screenshot_saved"), @"Screenshot saved!");

        /// <summary>
        /// "Screenshot"
        /// </summary>
        public static LocalisableString Screenshot => new TranslatableString(getKey(@"screenshot"), @"Screenshot");

        /// <summary>
        /// "Screenshot format"
        /// </summary>
        public static LocalisableString ScreenshotFormat => new TranslatableString(getKey(@"screenshot_format"), @"Screenshot format");

        /// <summary>
        /// "Show user interface in screenshots"
        /// </summary>
        public static LocalisableString ShowCursorInScreenshots => new TranslatableString(getKey(@"show_cursor_in_screenshots"), @"Show user interface in screenshots");

        /// <summary>
        /// "Prefer display username (2005~2022)"
        /// </summary>
        public static LocalisableString UsernameDisplayMode_DisplayName => new TranslatableString(getKey(@"username_display_mode_nickname"), @"Prefer display username (2005~2022)");

        /// <summary>
        /// "Prefer display handle (2022~)"
        /// </summary>
        public static LocalisableString UsernameDisplayMode_Handle => new TranslatableString(getKey(@"username_display_mode_handle"), @"Prefer display handle (2022~)");

        /// <summary>
        /// "Comment added."
        /// </summary>
        public static LocalisableString CommentAdded => new TranslatableString(getKey(@"comment_added"), "Comment added.");

        /// <summary>
        /// "Write comment..."
        /// </summary>
        public static LocalisableString CommentWith => new TranslatableString(getKey(@"comment_with"), "Write comment...");

        /// <summary>
        /// "Signed in to {0}"
        /// </summary>
        public static LocalisableString SignedIn(string username) => new TranslatableString(getKey(@"signed_in"), "Signed in to {0}", username);

        /// <summary>
        /// "Not logged in"
        /// </summary>
        public static LocalisableString SignedOut => new TranslatableString(getKey(@"signed_out"), "Not logged in");

        /// <summary>
        /// "Google account"
        /// </summary>
        public static LocalisableString GoogleAccount => new TranslatableString(getKey(@"google_account"), "Google account");

        /// <summary>
        /// "Search"
        /// </summary>
        public static LocalisableString Search => new TranslatableString(getKey(@"search"), "Search");

        /// <summary>
        /// "search..."
        /// </summary>
        public static LocalisableString SearchPlaceholder => new TranslatableString(getKey(@"search_placeholder"), "search...");

        /// <summary>
        /// "View latest versions"
        /// </summary>
        public static LocalisableString ViewLatestVersions => new TranslatableString(getKey(@"view_latest_versions"), "View latest versions");

        /// <summary>
        /// "Report video"
        /// </summary>
        public static LocalisableString Report => new TranslatableString(getKey(@"report"), "Report video");

        /// <summary>
        /// "What's going on?"
        /// </summary>
        public static LocalisableString WhatsGoingOn => new TranslatableString(getKey(@"whats_going_on"), "What's going on?");

        /// <summary>
        /// "We'll check for all Community Guidelines, so don't worry about making the perfect choice."
        /// </summary>
        public static LocalisableString ReportDesc => new TranslatableString(getKey(@"report_desc"), "We'll check for all Community Guidelines, so don't worry about making the perfect choice.");

        /// <summary>
        /// "Reason"
        /// </summary>
        public static LocalisableString ReportReason => new TranslatableString(getKey(@"report_reason"), "Reason");

        /// <summary>
        /// "Detailed reason"
        /// </summary>
        public static LocalisableString ReportSubReason => new TranslatableString(getKey(@"report_sub_reason"), "Detailed reason");

        /// <summary>
        /// "Submit"
        /// </summary>
        public static LocalisableString Submit => new TranslatableString(getKey(@"submit"), "Submit");

        /// <summary>
        /// "Thank you for your submission."
        /// </summary>
        public static LocalisableString ReportSuccess => new TranslatableString(getKey(@"report_success"), "Thank you for your submission.");

        /// <summary>
        /// "Description"
        /// </summary>
        public static LocalisableString Description => new TranslatableString(getKey(@"description"), "Description");

        /// <summary>
        /// "Windowed"
        /// </summary>
        public static LocalisableString Windowed => new TranslatableString(getKey(@"windowed"), "Windowed");

        /// <summary>
        /// "Borderless"
        /// </summary>
        public static LocalisableString Borderless => new TranslatableString(getKey(@"borderless"), "Borderless");

        /// <summary>
        /// "Fullscreen"
        /// </summary>
        public static LocalisableString Fullscreen => new TranslatableString(getKey(@"fullscreen"), "Fullscreen");

        /// <summary>
        /// "VSync"
        /// </summary>
        public static LocalisableString VSync => new TranslatableString(getKey(@"vertical_sync"), "VSync");

        /// <summary>
        /// "2x refresh rate"
        /// </summary>
        public static LocalisableString RefreshRate2X => new TranslatableString(getKey(@"refresh_rate_2x"), "2x refresh rate");

        /// <summary>
        /// "4x refresh rate"
        /// </summary>
        public static LocalisableString RefreshRate4X => new TranslatableString(getKey(@"refresh_rate_4x"), "4x refresh rate");

        /// <summary>
        /// "8x refresh rate"
        /// </summary>
        public static LocalisableString RefreshRate8X => new TranslatableString(getKey(@"refresh_rate_8x"), "8x refresh rate");

        /// <summary>
        /// "Basically unlimited"
        /// </summary>
        public static LocalisableString Unlimited => new TranslatableString(getKey(@"unlimited"), "Basically unlimited");

        /// <summary>
        /// "Always use original audio"
        /// </summary>
        public static LocalisableString AlwaysUseOriginalAudio => new TranslatableString(getKey(@"always_use_original_audio"), "Always use original audio");

        /// <summary>
        /// "No description has been added to this video."
        /// </summary>
        public static LocalisableString NoDescription => new TranslatableString(getKey(@"no_description"), "No description has been added to this video.");

        /// <summary>
        /// "View changelog for {0}"
        /// </summary>
        public static LocalisableString ViewChangelog(string version) => new TranslatableString(getKey(@"view_version_desc"), "View changelog for {0}", version);

        /// <summary>
        /// "Automatic"
        /// </summary>
        public static LocalisableString RenderTypeAutomatic => new TranslatableString(getKey(@"render_type_automatic"), "Automatic");

        /// <summary>
        /// "Automatic ({0})"
        /// </summary>
        public static LocalisableString RenderTypeAutomaticIsUse(string rendererName) => new TranslatableString(getKey(@"render_type_automatic_is_use"), "Automatic ({0})", rendererName);

        /// <summary>
        /// "Always use system cursor"
        /// </summary>
        public static LocalisableString UseSystemCursor => new TranslatableString(getKey(@"use_system_cursor"), "Always use system cursor");

        /// <summary>
        /// "Use experimental audio mode"
        /// </summary>
        public static LocalisableString WasapiLabel => new TranslatableString(getKey(@"wasapi_label"), @"Use experimental audio mode");

        /// <summary>
        /// "This will attempt to initialise the audio engine in a lower latency mode."
        /// </summary>
        public static LocalisableString WasapiTooltip => new TranslatableString(getKey(@"wasapi_tooltip"), @"This will attempt to initialise the audio engine in a lower latency mode.");

        /// <summary>
        /// "Output device"
        /// </summary>
        public static LocalisableString OutputDevice => new TranslatableString(getKey(@"output_device"), @"Output device");

        /// <summary>
        /// "Volume"
        /// </summary>
        public static LocalisableString Volume => new TranslatableString(getKey(@"volume"), @"Volume");

        /// <summary>
        /// "Debug"
        /// </summary>
        public static LocalisableString Debug => new TranslatableString(getKey(@"debug"), @"Debug");

        /// <summary>
        /// "Show log overlay"
        /// </summary>
        public static LocalisableString ShowLogOverlay => new TranslatableString(getKey(@"show_log_overlay"), @"Show log overlay");

        /// <summary>
        /// "Bypass front-to-back render pass"
        /// </summary>
        public static LocalisableString BypassFTBRenderPass => new TranslatableString(getKey(@"bypass_ftb_render_pass"), @"Bypass front-to-back render pass");

        /// <summary>
        /// "GC mode"
        /// </summary>
        public static LocalisableString GC_Mode => new TranslatableString(getKey(@"gc_mode"), @"GC mode");

        /// <summary>
        /// "Clear all caches"
        /// </summary>
        public static LocalisableString ClearAllCaches => new TranslatableString(getKey(@"clear_all_caches"), @"Clear all caches");

        /// <summary>
        /// "Load from playlist ID or URL"
        /// </summary>
        public static LocalisableString LoadFromPlaylistId => new TranslatableString(getKey(@"load_from_playlist_id"), "Load from playlist ID or URL");

        /// <summary>
        /// "Load Playlist"
        /// </summary>
        public static LocalisableString LoadPlaylist => new TranslatableString(getKey(@"load_playlist"), "Load Playlist");

        /// <summary>
        /// "Playlists"
        /// </summary>
        public static LocalisableString Playlists => new TranslatableString(getKey(@"playlists"), "Playlists");

        /// <summary>
        /// "Previous video"
        /// </summary>
        public static LocalisableString PreviousVideo => new TranslatableString(getKey(@"previous_video"), "Previous video");

        /// <summary>
        /// "Next video"
        /// </summary>
        public static LocalisableString NextVideo => new TranslatableString(getKey(@"next_video"), "Next video");

        /// <summary>
        /// "Hardware acceleration via GPU is enabled. If you experience any issues, please keep hardware acceleration disabled for now."
        /// </summary>
        public static LocalisableString HardwareAccelerationEnabledNote => new TranslatableString(getKey(@"hw_acceleration_enabled_note"), "Hardware acceleration via GPU is enabled. If you experience any issues, please keep hardware acceleration disabled for now.");

        /// <summary>
        /// "Audio effects"
        /// </summary>
        public static LocalisableString AudioEffects => new TranslatableString(getKey(@"audio_effects"), "Audio effects");

        /// <summary>
        /// "Reverb"
        /// </summary>
        public static LocalisableString ReverbEffect => new TranslatableString(getKey(@"reverb"), "Reverb");

        /// <summary>
        /// "Wet mix"
        /// </summary>
        public static LocalisableString WetMix => new TranslatableString(getKey(@"reverb_wet_mix"), "Wet mix");

        /// <summary>
        /// "Stereo width"
        /// </summary>
        public static LocalisableString StereoWidth => new TranslatableString(getKey(@"reverb_stereo_width"), "Stereo width");

        /// <summary>
        /// "High frequency damping"
        /// </summary>
        public static LocalisableString HighFreqDamp => new TranslatableString(getKey(@"reverb_high_frequency_damping"), "High frequency damping");

        /// <summary>
        /// "Room size"
        /// </summary>
        public static LocalisableString RoomSize => new TranslatableString(getKey(@"reverb_room_size"), "Room size");

        /// <summary>
        /// "Rotate rate"
        /// </summary>
        public static LocalisableString RotateParameters_fRate => new TranslatableString(getKey(@"audio_effect_rotate_rate"), "Rotate rate");

        /// <summary>
        /// "3D rotate effect"
        /// </summary>
        public static LocalisableString RotateParameters_Enabled => new TranslatableString(getKey(@"audio_effect_rotate_enabled"), "3D rotate effect");

        /// <summary>
        /// "Dry mix"
        /// </summary>
        public static LocalisableString DryMix => new TranslatableString(getKey(@"echo_dry_mix"), "Dry mix");

        /// <summary>
        /// "Wet mix"
        /// </summary>
        public static LocalisableString EchoWetMix => new TranslatableString(getKey(@"echo_wet_mix"), "Wet mix"); //separated from reverb_wet_mix

        /// <summary>
        /// "Feedback"
        /// </summary>
        public static LocalisableString EchoFeedback => new TranslatableString(getKey(@"echo_feedback"), "Feedback");

        /// <summary>
        /// "Delay"
        /// </summary>
        public static LocalisableString EchoDelay => new TranslatableString(getKey(@"echo_delay"), "Delay");

        /// <summary>
        /// "Echo"
        /// </summary>
        public static LocalisableString EchoEffect => new TranslatableString(getKey(@"echo_effect"), "Echo");

        /// <summary>
        /// "Distortion"
        /// </summary>
        public static LocalisableString DistortionEffect => new TranslatableString(getKey(@"distortion_effect"), "Distortion");

        /// <summary>
        /// "Distortion volume"
        /// </summary>
        public static LocalisableString DistortionVolume => new TranslatableString(getKey(@"distortion_volume"), "Distortion volume");

        /// <summary>
        /// "Video bloom level"
        /// </summary>
        public static LocalisableString VideoBloomLevel => new TranslatableString(getKey(@"bloom"), "Video bloom level");

        /// <summary>
        /// "Chromatic aberration effect strength"
        /// </summary>
        public static LocalisableString ChromaticAberration => new TranslatableString(getKey(@"chromatic_aberration"), "Chromatic aberration effect strength");

        /// <summary>
        /// "Visual effects"
        /// </summary>
        public static LocalisableString VisualEffects => new TranslatableString(getKey(@"visual_effects"), "Visual effects");

        /// <summary>
        /// "Video grayscale level"
        /// </summary>
        public static LocalisableString VideoGrayscaleLevel => new TranslatableString(getKey(@"grayscale"), "Video grayscale level");

        /// <summary>
        /// "Video hue shift"
        /// </summary>
        public static LocalisableString VideoHueShift => new TranslatableString(getKey(@"hue_shift"), "Video hue shift");

        /// <summary>
        /// "Discord Rich Presence"
        /// </summary>
        public static LocalisableString DiscordRichPresence => new TranslatableString(getKey(@"discord_rich_presence"), @"Discord Rich Presence");

        /// <summary>
        /// "Hide video information"
        /// </summary>
        public static LocalisableString HideIdentifiableInformation => new TranslatableString(getKey(@"hide_identifiable_information"), @"Hide video information");

        /// <summary>
        /// "Full"
        /// </summary>
        public static LocalisableString DiscordPresenceFull => new TranslatableString(getKey(@"discord_presence_full"), @"Full");

        /// <summary>
        /// "Off"
        /// </summary>
        public static LocalisableString DiscordPresenceOff => new TranslatableString(getKey(@"discord_presence_off"), @"Off");

        /// <summary>
        /// "Cancel"
        /// </summary>
        public static LocalisableString Cancel => new TranslatableString(getKey(@"cancel"), @"Cancel");

        /// <summary>
        /// "Yes"
        /// </summary>
        public static LocalisableString Yes => new TranslatableString(getKey(@"yes"), @"Yes");

        /// <summary>
        /// "Are you sure you want to unsubscribe this channel?"
        /// </summary>
        public static LocalisableString UnsubscribeDesc => new TranslatableString(getKey(@"unsubscribe_desc"), @"Are you sure you want to unsubscribe this channel?");

        /// <summary>
        /// "Subscribe"
        /// </summary>
        public static LocalisableString Subscribe => new TranslatableString(getKey(@"subscribe"), @"Subscribe");

        /// <summary>
        /// "Unsubscribe"
        /// </summary>
        public static LocalisableString Unsubscribe => new TranslatableString(getKey(@"unsubscribe"), @"Unsubscribe");

        /// <summary>
        /// "Subscription removed."
        /// </summary>
        public static LocalisableString SubscriptionRemoved => new TranslatableString(getKey(@"subscription_removed"), @"Subscription removed.");

        /// <summary>
        /// "Subscription added."
        /// </summary>
        public static LocalisableString SubscriptionAdded => new TranslatableString(getKey(@"subscription_added"), @"Subscription added.");

        /// <summary>
        /// "Closed captions"
        /// </summary>
        public static LocalisableString ClosedCaptions => new TranslatableString(getKey(@"closed_captions"), @"Closed captions");

        /// <summary>
        /// "Summarize video"
        /// </summary>
        public static LocalisableString SummarizeViaGPT => new TranslatableString(getKey(@"summarize_via_gpt"), @"Summarize video");

        /// <summary>
        /// "{0} Summarize this video"
        /// </summary>
        public static LocalisableString GPTSummarizePrompt(LocalisableString videoUrl) => new TranslatableString(getKey(@"gpt_summarize_prompt"), @"{0} Summarize this video", videoUrl);

        /// <summary>
        /// "Public"
        /// </summary>
        public static LocalisableString Public => new TranslatableString(getKey(@"public"), @"Public");

        /// <summary>
        /// "Unlisted"
        /// </summary>
        public static LocalisableString Unlisted => new TranslatableString(getKey(@"unlisted"), @"Unlisted");

        /// <summary>
        /// "Private"
        /// </summary>
        public static LocalisableString Private => new TranslatableString(getKey(@"private"), @"Private");

        /// <summary>
        /// "Save"
        /// </summary>
        public static LocalisableString Save => new TranslatableString(getKey(@"save"), @"Save");

        /// <summary>
        /// "Save or remove"
        /// </summary>
        public static LocalisableString SaveOrRemove => new TranslatableString(getKey(@"save_or_remove"), @"Save or remove");

        /// <summary>
        /// "Save location"
        /// </summary>
        public static LocalisableString SaveLocation => new TranslatableString(getKey(@"save_location"), @"Save location");

        /// <summary>
        /// "Add new playlist"
        /// </summary>
        public static LocalisableString AddNewPlaylist => new TranslatableString(getKey(@"add_new_playlist"), @"Add new playlist");

        /// <summary>
        /// "Privacy status"
        /// </summary>
        public static LocalisableString PrivacyStatus => new TranslatableString(getKey(@"privacy_status"), @"Privacy status");

        /// <summary>
        /// "Enter title here"
        /// </summary>
        public static LocalisableString TitlePlaceholder => new TranslatableString(getKey(@"title_placeholder"), @"Enter title here");

        /// <summary>
        /// "Title"
        /// </summary>
        public static LocalisableString Title => new TranslatableString(getKey(@"title"), @"Title");

        /// <summary>
        /// "Create"
        /// </summary>
        public static LocalisableString Create => new TranslatableString(getKey(@"create"), @"Create");

        /// <summary>
        /// "Added current video to playlist {0}."
        /// </summary>
        public static LocalisableString VideoSavedToPlaylist(string playlistName) => new TranslatableString(getKey(@"video_saved_to_playlist"), @"Added current video to playlist {0}.", playlistName);

        /// <summary>
        /// "Removed current video from playlist {0}."
        /// </summary>
        public static LocalisableString VideoRemovedFromPlaylist(string playlistName) => new TranslatableString(getKey(@"video_removed_from_playlist"), @"Removed current video from playlist {0}.", playlistName);

        /// <summary>
        /// "Menu"
        /// </summary>
        public static LocalisableString Menu => new TranslatableString(getKey(@"menu"), @"Menu");

        /// <summary>
        /// "Exit"
        /// </summary>
        public static LocalisableString Exit => new TranslatableString(getKey(@"exit"), @"Exit");

        /// <summary>
        /// "Normalize Audio"
        /// </summary>
        public static LocalisableString AudioNormalization => new TranslatableString(getKey(@"audio_normalization"), @"Normalize Audio");

        /// <summary>
        /// "Use SDL3 host (Experimental)"
        /// </summary>
        public static LocalisableString UseSDL3 => new TranslatableString(getKey(@"use_sdl3_host"), "Use SDL3 host (Experimental)");

        /// <summary>
        /// "Logout"
        /// </summary>
        public static LocalisableString Logout => new TranslatableString(getKey(@"logout"), @"Logout");

        /// <summary>
        /// "View channel"
        /// </summary>
        public static LocalisableString ViewChannel => new TranslatableString(getKey(@"view_channel"), @"View channel");

        /// <summary>
        /// "video not loaded!"
        /// </summary>
        public static LocalisableString VideoNotLoaded => new TranslatableString(getKey(@"video_not_loaded"), @"video not loaded!");

        /// <summary>
        /// "please load a video to watch!"
        /// </summary>
        public static LocalisableString VideoNotLoadedDesc => new TranslatableString(getKey(@"video_not_loaded_desc"), @"please load a video to watch!");

        /// <summary>
        /// "My playlists"
        /// </summary>
        public static LocalisableString MyPlaylists => new TranslatableString(getKey(@"my_playlists"), @"My playlists");

        /// <summary>
        /// "playlist not loaded!"
        /// </summary>
        public static LocalisableString PlaylistNotLoaded => new TranslatableString(getKey(@"playlist_not_loaded"), @"playlist not loaded!");

        /// <summary>
        /// "please load a playlist to watch!"
        /// </summary>
        public static LocalisableString PlaylistNotLoadedDesc => new TranslatableString(getKey(@"playlist_not_loaded_desc"), @"please load a playlist to watch!");

        /// <summary>
        /// "Exit options"
        /// </summary>
        public static LocalisableString ExitOptions => new TranslatableString(getKey(@"exit_options"), @"Exit options");

        /// <summary>
        /// "Power off system"
        /// </summary>
        public static LocalisableString PowerOff => new TranslatableString(getKey(@"power_off"), @"Power off system");

        /// <summary>
        /// "Restart system"
        /// </summary>
        public static LocalisableString Restart => new TranslatableString(getKey(@"restart_system"), @"Restart system");

        /// <summary>
        /// "Audio quality"
        /// </summary>
        public static LocalisableString AudioQuality => new TranslatableString(getKey(@"audio_quality"), @"Audio quality");

        /// <summary>
        /// "Prefer high quality"
        /// </summary>
        public static LocalisableString PreferHighQualityAudio => new TranslatableString(getKey(@"prefer_high_quality_audio"), "Prefer high quality");

        /// <summary>
        /// "Prefer opus codecs first"
        /// </summary>
        public static LocalisableString PreferOpus => new TranslatableString(getKey(@"prefer_opus"), @"Prefer opus codecs first");

        /// <summary>
        /// "Prefer mp4a codecs first"
        /// </summary>
        public static LocalisableString PreferMp4a => new TranslatableString(getKey(@"prefer_mp4a"), @"Prefer mp4a codecs first");

        /// <summary>
        /// "Low quality"
        /// </summary>
        public static LocalisableString LowQuality => new TranslatableString(getKey(@"low_quality"), @"Low quality");

        /// <summary>
        /// "Action when the close button is pressed"
        /// </summary>
        public static LocalisableString CloseButtonAction => new TranslatableString(getKey(@"close_button_action"), @"Action when the close button is pressed");

        /// <summary>
        /// "Hide to tray icon"
        /// </summary>
        public static LocalisableString HideToTrayIcon => new TranslatableString(getKey(@"hide_to_tray_icon"), @"Hide to tray icon");

        /// <summary>
        /// "Close the app"
        /// </summary>
        public static LocalisableString CloseApp => new TranslatableString(getKey(@"close_app"), @"Close the app");

        /// <summary>
        /// "Repeat On/Off"
        /// </summary>
        public static LocalisableString Repeat => new TranslatableString(getKey(@"repeat"), @"Repeat On/Off");

        /// <summary>
        /// "When you log in, you may see an "Google hasn’t verified this app" screen. Please feel free to log in."
        /// </summary>
        public static LocalisableString OAuthNote => new TranslatableString(getKey(@"oauth_note"), @"When you log in, you may see an ""Google hasn’t verified this app"" screen. Please feel free to log in.");

        /// <summary>
        /// "System volume"
        /// </summary>
        public static LocalisableString SystemVolume => new TranslatableString(getKey(@"system_volume"), "System volume");

        /// <summary>
        /// "System volume ({0})"
        /// </summary>
        public static LocalisableString SystemVolumeWithDevice(string deviceName) => new TranslatableString(getKey(@"system_volume_with_device"), "System volume ({0})", deviceName);

        /// <summary>
        /// "App colour scheme"
        /// </summary>
        public static LocalisableString ColourScheme => new TranslatableString(getKey(@"colour_scheme"), "App colour scheme");

        /// <summary>
        /// "Circle"
        /// </summary>
        public static LocalisableString Circle => new TranslatableString(getKey(@"circle"), @"Circle");

        /// <summary>
        /// "Rounded square"
        /// </summary>
        public static LocalisableString Square => new TranslatableString(getKey(@"square"), @"Rounded square");

        /// <summary>
        /// "Profile image shape"
        /// </summary>
        public static LocalisableString ProfileImageShape => new TranslatableString(getKey(@"profile_image_shape"), @"Profile image shape");

        /// <summary>
        /// "Sort by"
        /// </summary>
        public static LocalisableString SortDefault => new TranslatableString(getKey(@"sort_title"), @"Sort by");

        /// <summary>
        /// "Top"
        /// </summary>
        public static LocalisableString CommentsSortTop => new TranslatableString(getKey(@"sort_top"), @"Top");

        /// <summary>
        /// "Newest"
        /// </summary>
        public static LocalisableString CommentsSortNewest => new TranslatableString(getKey(@"sort_newest"), @"Newest");

        /// <summary>
        /// "User interface"
        /// </summary>
        public static LocalisableString UserInterface => new TranslatableString(getKey(@"user_interface"), @"User interface");

        /// <summary>
        /// "No comments here..."
        /// </summary>
        public static LocalisableString NoComments => new TranslatableString(getKey(@"no_comments"), @"No comments here...");

        /// <summary>
        /// "well played👍"
        /// </summary>
        public static LocalisableString NoCommentsDesc => new TranslatableString(getKey(@"no_comments_desc"), @"well played👍");

        /// <summary>
        /// "Mute system sounds"
        /// </summary>
        public static LocalisableString SystemMute => new TranslatableString(getKey(@"system_sound_mute"), @"Mute system sounds");

        /// <summary>
        /// "This setting mutes AX Player sounds and other apps, and system sounds."
        /// </summary>
        public static LocalisableString SystemMuteDesc => new TranslatableString(getKey(@"system_sound_mute_desc"), @"This setting mutes AX Player sounds and other apps, and system sounds.");

        /// <summary>
        /// "Send a feedback"
        /// </summary>
        public static LocalisableString ReportBugs => new TranslatableString(getKey(@"report_bugs"), @"Send a feedback");

        /// <summary>
        /// "Send a feedback with the app to the developers."
        /// </summary>
        public static LocalisableString ReportBugsDesc => new TranslatableString(getKey(@"report_bugs_desc"), @"Send a feedback with the app to the developers.");

        /// <summary>
        /// "Discord is not installed. Please install Discord and share your activities with your friends!"
        /// </summary>
        public static LocalisableString DiscordNotInstalled => new TranslatableString(getKey(@"discord_not_installed"), @"Discord is not installed. Please install Discord and share your activities with your friends!");

        /// <summary>
        /// "Release stream"
        /// </summary>
        public static LocalisableString ReleaseStream => new TranslatableString(getKey(@"release_stream"), @"Release stream");

        /// <summary>
        /// "Stable"
        /// </summary>
        public static LocalisableString Stable => new TranslatableString(getKey(@"release_stream_stable"), @"Stable");

        /// <summary>
        /// "Nightly build (unstable)"
        /// </summary>
        public static LocalisableString Beta => new TranslatableString(getKey(@"release_stream_beta"), @"Nightly build (unstable)");

        /// <summary>
        /// "Video quality ({0})"
        /// </summary>
        public static LocalisableString VideoQualityWithLabel(string quality) => new TranslatableString(getKey(@"video_quality_with_label"), @"Video quality ({0})", quality);

        /// <summary>
        /// "Audio quality ({0})"
        /// </summary>
        public static LocalisableString AudioQualityWithLabel(string quality) => new TranslatableString(getKey(@"audio_quality_with_label"), @"Audio quality ({0})", quality);

        /// <summary>
        /// "Jump to {0}"
        /// </summary>
        public static LocalisableString JumpTo(string timestamp) => new TranslatableString(getKey(@"jump_to"), @"Jump to {0}", timestamp);

        /// <summary>
        /// "Edit playlist"
        /// </summary>
        public static LocalisableString EditPlaylist => new TranslatableString(getKey(@"edit_playlist"), @"Edit playlist");

        /// <summary>
        /// "Apply"
        /// </summary>
        public static LocalisableString Apply => new TranslatableString(getKey(@"apply"), @"Apply");

        /// <summary>
        /// "Download video"
        /// </summary>
        public static LocalisableString DownloadVideo => new TranslatableString(getKey(@"download_video"), @"Download video");

        /// <summary>
        /// "Download"
        /// </summary>
        public static LocalisableString Download => new TranslatableString(getKey(@"download"), @"Download");

        /// <summary>
        /// "Downloading video stream: {0}"
        /// </summary>
        public static LocalisableString DownloadingVideoStream(string value) => new TranslatableString(getKey(@"downloading_video_stream"), @"Downloading video stream: {0}", value);

        /// <summary>
        /// "Downloading audio stream: {0}"
        /// </summary>
        public static LocalisableString DownloadingAudioStream(string value) => new TranslatableString(getKey(@"downloading_audio_stream"), @"Downloading audio stream: {0}", value);

        /// <summary>
        /// "Merging streams..."
        /// </summary>
        public static LocalisableString MergingStreams => new TranslatableString(getKey(@"merging_stream"), @"Merging streams...");

        /// <summary>
        /// "The download has been completed."
        /// </summary>
        public static LocalisableString DownloadCompleted => new TranslatableString(getKey(@"download_completed"), @"The download has been completed.");

        /// <summary>
        /// "UI Font"
        /// </summary>
        public static LocalisableString UIFont => new TranslatableString(getKey(@"ui_font"), @"UI Font");

        /// <summary>
        /// "Translating..."
        /// </summary>
        public static LocalisableString Translating => new TranslatableString(getKey(@"translating"), "Translating...");

        /// <summary>
        /// "{0} • {1} likes • {2} views • {3}"
        /// </summary>
        public static LocalisableString VideoMetadataDescWithLikeCount(string username, string likeCount, string views, string daysAgo) => new TranslatableString(getKey(@"video_metadata_desc_with_likecount"), "{0} • {1} likes • {2} views • {3}", username, likeCount, views, daysAgo);

        /// <summary>
        /// "Show video metadata on window title"
        /// </summary>
        public static LocalisableString ShowVideoMetadataOnWindowTitle => new TranslatableString(getKey(@"show_video_metadata_on_window_title"), "Show video metadata on window title");

        /// <summary>
        /// "Always show player control"
        /// </summary>
        public static LocalisableString PlayerControlPin => new TranslatableString(getKey(@"player_control_pin"), "Always show player control");

        /// <summary>
        /// "Vocal Remover (Experimental)"
        /// </summary>
        public static LocalisableString KaraokeMode => new TranslatableString(getKey(@"karaoke_mode"), "Vocal Remover (Experimental)");

        /// <summary>
        /// "Chorus"
        /// </summary>
        public static LocalisableString ChorusEffect => new TranslatableString(getKey(@"chorus_effect"), "Chorus");

        /// <summary>
        /// "Minimum sweep"
        /// </summary>
        public static LocalisableString ChorusMinSweep => new TranslatableString(getKey(@"chorus_min_sweep"), "Minimum sweep");

        /// <summary>
        /// "Maximum sweep"
        /// </summary>
        public static LocalisableString ChorusMaxSweep => new TranslatableString(getKey(@"chorus_max_sweep"), "Maximum sweep");

        /// <summary>
        /// "Rate"
        /// </summary>
        public static LocalisableString ChorusRate => new TranslatableString(getKey(@"chorus_rate"), "Rate");

        /// <summary>
        /// "Relevance"
        /// </summary>
        public static LocalisableString SearchSortRelevance => new TranslatableString(getKey(@"sort_relevance"), "Relevance");

        /// <summary>
        /// "Alphabet (A-Z)"
        /// </summary>
        public static LocalisableString SearchSortAlphabet => new TranslatableString(getKey(@"sort_alphabet"), "Alphabet (A-Z)");

        /// <summary>
        /// "Log in to send comments (Menu - Settings - Google account)"
        /// </summary>
        public static LocalisableString LoginToComment => new TranslatableString(getKey(@"login_to_comment"), "Log in to send comments (Menu - Settings - Google account)");

        /// <summary>
        /// "If you changes this settings, app will restart and applied."
        /// </summary>
        public static LocalisableString SettingsItem_RestartRequired => new TranslatableString(getKey(@"settings_item_restart_required"), "If you changes this settings, app will restart and applied.");

        /// <summary>
        /// "Video Settings"
        /// </summary>
        public static LocalisableString VideoSettings => new TranslatableString(getKey(@"video_settings"), "Video Settings");

        /// <summary>
        /// "Note that you've selected a device with an exclusive-access driver, which means that you won't be able to hear sounds from other apps while in use by AXPlayer. Also, to initialize it correctly, first ensure it's not being actively used by other applications. You may need to restart AX Player and wait a moment to for it de-initialize if you've just used it through other audio driver."
        /// </summary>
        public static LocalisableString AlsaExclusiveNotice => new TranslatableString(getKey(@"alsa_exclusive_notice"), @"Note that you've selected a device with an exclusive-access driver, which means that you won't be able to hear sounds from other apps while in use by AXPlayer. Also, to initialize it correctly, first ensure it's not being actively used by other applications. You may need to restart AX Player and wait a moment to for it de-initialize if you've just used it through other audio driver.");

        /// <summary>
        /// "Overlay SFX Type"
        /// </summary>
        public static LocalisableString SFXType => new TranslatableString(getKey(@"sfx_type"), "Overlay SFX Type");

        /// <summary>
        /// "New SFX"
        /// </summary>
        public static LocalisableString SFXNew => new TranslatableString(getKey(@"sfx_new"), "New SFX");

        /// <summary>
        /// "Legacy"
        /// </summary>
        public static LocalisableString SFXLegacy => new TranslatableString(getKey(@"sfx_legacy"), "Legacy");

        /// <summary>
        /// "Play Overlay SFX"
        /// </summary>
        public static LocalisableString PlayOverlaySFX => new TranslatableString(getKey(@"play_overlay_sfx"), "Play Overlay SFX");

        /// <summary>
        /// "Closed captions background opacity"
        /// </summary>
        public static LocalisableString CaptionBGOpacity => new TranslatableString(getKey(@"closed_captions_bg_opacity"), @"Closed captions background opacity");

        /// <summary>
        /// "This is how the subtitles appear."
        /// </summary>
        public static LocalisableString ClosedCaptionPreview => new TranslatableString(getKey(@"closed_captions_preview"), @"This is how the subtitles appear.");

        /// <summary>
        /// "I like this"
        /// </summary>
        public static LocalisableString LikeButton => new TranslatableString(getKey(@"like_button"), "I like this");

        /// <summary>
        /// "I dislike this"
        /// </summary>
        public static LocalisableString DislikeButton => new TranslatableString(getKey(@"dislike_button"), "I dislike this");

        /// <summary>
        /// "About AX Player"
        /// </summary>
        public static LocalisableString AppInfo => new TranslatableString(getKey(@"app_info"), "About AX Player");

        /// <summary>
        /// "View profile for {0}"
        /// </summary>
        public static LocalisableString YouTubeHandleViewProfile(string handle) => new TranslatableString(getKey(@"yt_handle_view_profile"), "View profile for {0}", handle);

        /// <summary>
        /// "Hashtag {0}"
        /// </summary>
        public static LocalisableString Hashtag(string handle) => new TranslatableString(getKey(@"hashtag"), "Hashtag {0}", handle);

        /// <summary>
        /// "View GitHub Profile"
        /// </summary>
        public static LocalisableString ViewGitHubProfile => new TranslatableString(getKey(@"view_github_profile"), "View GitHub Profile");

        /// <summary>
        /// "View more comments"
        /// </summary>
        public static LocalisableString ViewMoreComments => new TranslatableString(getKey(@"view_more_comments"), "View more comments");

        /// <summary>
        /// "Updates"
        /// </summary>
        public static LocalisableString Updates => new TranslatableString(getKey(@"updates"), "Updates");

        /// <summary>
        /// "Reduces latency by leveraging the NVIDIA Reflex API on NVIDIA GPUs. Recommended to have On, turn Off only if experiencing issues."
        /// </summary>
        public static LocalisableString ReflexHint => new TranslatableString(getKey(@"reflex_hint"), "Reduces latency by leveraging the NVIDIA Reflex API on NVIDIA GPUs. Recommended to have On, turn Off only if experiencing issues.");

        /// <summary>
        /// "Boost increases GPU power consumption and may increase latency in some cases. Disable Boost if experiencing issues."
        /// </summary>
        public static LocalisableString ReflexNotice => new TranslatableString(getKey(@"reflex_notice"), "Boost increases GPU power consumption and may increase latency in some cases. Disable Boost if experiencing issues.");

        /// <summary>
        /// "Off"
        /// </summary>
        public static LocalisableString ReflexOff => new TranslatableString(getKey(@"reflex_off"), "Off");

        /// <summary>
        /// "On"
        /// </summary>
        public static LocalisableString ReflexOn => new TranslatableString(getKey(@"reflex_on"), "On");

        /// <summary>
        /// "Boost"
        /// </summary>
        public static LocalisableString ReflexBoost => new TranslatableString(getKey(@"reflex_boost"), "Boost");

        /// <summary>
        /// "This features are experimental, have many bugs, and are not complete."
        /// </summary>
        public static LocalisableString SRV3Notice => new TranslatableString(getKey(@"srv3_notice"), "This features are experimental, have many bugs, and are not complete.");

        /// <summary>
        /// "Reset playback speed when loading a video"
        /// </summary>
        public static LocalisableString ResetPlaybackSpeedWhenLoadingAVideo => new TranslatableString(getKey(@"reset_playback_speed_when_loading_a_video"), "Reset playback speed when loading a video");

        /// <summary>
        /// "Closed captions background colour"
        /// </summary>
        public static LocalisableString CaptionBGColour => new TranslatableString(getKey(@"closed_captions_bg_colour"), @"Closed captions background colour");

        /// <summary>
        /// "Closed captions corner radius"
        /// </summary>
        public static LocalisableString CaptionCornerRadius => new TranslatableString(getKey(@"closed_captions_radius"), @"Closed captions corner radius");

        /// <summary>
        /// "Use advanced subtitles style"
        /// </summary>
        public static LocalisableString AdvancedSubtitleStyle => new TranslatableString(getKey(@"advanced_subtitle_style"), @"Use advanced subtitles style");

        /// <summary>
        /// "Screenshot quality"
        /// </summary>
        public static LocalisableString ScreenshotQuality => new TranslatableString(getKey(@"screenshot_quality"), @"Screenshot quality");

        /// <summary>
        /// "Notifications"
        /// </summary>
        public static LocalisableString Notifications => new TranslatableString(getKey(@"notifications"), @"Notifications");

        /// <summary>
        /// "Audio tracks"
        /// </summary>
        public static LocalisableString AudioTracks => new TranslatableString(getKey(@"audio_tracks"), @"Audio tracks");

        /// <summary>
        /// "Vocal volume"
        /// </summary>
        public static LocalisableString KaraokeVocalVolume => new TranslatableString(getKey(@"karaoke_vocal_volume"), @"Vocal volume");

        /// <summary>
        /// "8-bit effect"
        /// </summary>
        public static LocalisableString EightBitEffectEnabled => new TranslatableString(getKey(@"eight_bit_effect_enabled"), @"8-bit effect");

        /// <summary>
        /// "Bit depth"
        /// </summary>
        public static LocalisableString BitDepth => new TranslatableString(getKey(@"bit_depth"), @"Bit depth");

        /// <summary>
        /// "Downsample ratio"
        /// </summary>
        public static LocalisableString DownsampleRatio => new TranslatableString(getKey(@"downsample_ratio"), @"Downsample ratio");

        /// <summary>
        /// "VSync (VRR low latency)"
        /// </summary>
        public static LocalisableString VSyncVRR => new TranslatableString(getKey(@"vertical_sync_vrr"), "VSync (VRR low latency)");

        /// <summary>
        /// "Preparing..."
        /// </summary>
        public static LocalisableString Preparing => new TranslatableString(getKey(@"preparing"), "Preparing...");

        /// <summary>
        /// "If the distortion volume exceeds 50%, sound quality loss will occur!"
        /// </summary>
        public static LocalisableString DistortionWarning => new TranslatableString(getKey(@"distortion_warning"), "If the distortion volume exceeds 50%, sound quality loss will occur!");

        /// <summary>
        /// "Modern"
        /// </summary>
        public static LocalisableString Modern => new TranslatableString(getKey(@"modern"), "Modern");

        /// <summary>
        /// "Player controller fade style"
        /// </summary>
        public static LocalisableString PlayerControllerFadeStyle => new TranslatableString(getKey(@"player_controller_fade_style"), "Player controller fade style");

        /// <summary>
        /// "Seek bar wave animation"
        /// </summary>
        public static LocalisableString WavySeekbar => new TranslatableString(getKey(@"wavy_seekbar"), "Seek bar wave animation");

        /// <summary>
        /// "Use legacy audio mode"
        /// </summary>
        public static LocalisableString LegacyAudioLabel => new TranslatableString(getKey(@"legacy_audio_label"), @"Use legacy audio mode");

        /// <summary>
        /// "Use this if you are experiencing audio issues. Note that audio latency will be higher when this is toggled on."
        /// </summary>
        public static LocalisableString LegacyAudioTooltip => new TranslatableString(getKey(@"legacy_audio_tooltip"), @"Use this if you are experiencing audio issues. Note that audio latency will be higher when this is toggled on.");

        /// <summary>
        /// "Version {0}"
        /// </summary>
        public static LocalisableString VersionText(string version) => new TranslatableString(getKey(@"version_text"), "Version {0}", version);

        /// <summary>
        /// "Due to limitations of the official YouTube API, a maximum of 5 replies can be displayed."
        /// </summary>
        public static LocalisableString RepliesLimited => new TranslatableString(getKey(@"limited_replies"), "Due to limitations of the official YouTube API, a maximum of 5 replies can be displayed.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Threading;
using Google.Apis.YouTube.v3.Data;
using NekoPlayer.App.Config;
using NekoPlayer.App.Extensions;
using NekoPlayer.App.Graphics;
using NekoPlayer.App.Graphics.Containers;
using NekoPlayer.App.Graphics.Shaders;
using NekoPlayer.App.Graphics.Sprites;
using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Graphics.UserInterfaceV2;
using NekoPlayer.App.Graphics.UserInterfaceV3;
using NekoPlayer.App.Input;
using NekoPlayer.App.Input.Binding;
using NekoPlayer.App.Online;
using NekoPlayer.App.Overlays;
using NekoPlayer.App.Overlays.Containers;
using NekoPlayer.App.Utils;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Video;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using Container = osu.Framework.Graphics.Containers.Container;
using Language = NekoPlayer.App.Localisation.Language;
using OverlayContainer = NekoPlayer.App.Graphics.Containers.OverlayContainer;

namespace NekoPlayer.App.Screens
{
    public partial class MainAppView : NekoPlayerScreen, IKeyBindingHandler<GlobalAction>, INekoPlayerAppMessageHandler
    {
        private BufferedContainer videoContainer;
        private ProjectYomiButton commentSendButton, searchButton, loadPlaylistBtn, downloadBtn;
        private RoundedButton acceptButton, updatePlaylistButton, loadBtn, viewChannelButton;
        private RoundedAltButton logoutButton, declineButton;
        private ControlBarIconButton prevVideoButton, nextVideoButton;
        private EnhancedFocusedTextBox videoIdBox, playlistIdBox, searchTextBox;
        private EnhancedFocusedTextBoxWithProfileImage commentTextBox;
        //private NekoPlayerLoadingSpinner spinner;
        private ScheduledDelegate spinnerShow;
        private IdleTracker idleTracker;
        private BufferedContainer uiContainer;
        private DrawSizePreservingFillContainer uiGradientContainer;
        private SettingsContainer settingsContainer;
        private OverlayContainer loadVideoContainer, videoDescriptionContainer, commentsContainer, searchContainer, reportAbuseOverlay, loadPlaylistContainer, unsubscribeDialog, addPlaylistOverlay, videoSaveLocationOverlay, myChannelDialog, editPlaylistOverlay, downloadReadyContainer, downloadOverlay, downloadCompletedOverlay;
        private SideOverlayContainer playlistOverlay, audioEffectsOverlay, menuOverlay, myPlaylistsOverlay, exitOptions;
        private IconButton menuOverlayShow, editChannelButton;
        private MenuButtonItem loadBtnOverlayShow, settingsOverlayShowBtn, commentOpenButton, searchOpenButton, reportOpenButton, playlistOpenButton, audioEffectsOpenButton, saveVideoOpenButton, newPlaylistOpenButton, myPlaylistsOpenButton;
        private VideoMetadataDisplayWithoutProfile videoMetadataDisplay;
        private VideoMetadataDisplay videoMetadataDisplayDetails;
        private RoundedButtonContainer commentOpenButtonDetails, likeButton;

        private static readonly string[] empty_comments_samples =
        {
            @"cuayo",
            @"cuayo1",
            @"cuayo2",
            @"cuayo3",
            @"ner",
            @"speaki_ner",
            @"speaki1",
        };

        private FormEnumDropdown<PrivacyStatus> playlistPrivacyStatusDropdown, editPlaylistPrivacyStatusDropdown;

        private YouTubeChannelMetadataDisplay youtubeChannelMetadataDisplay, youtubeChannelMetadataDisplay2;

        private SettingsItemV2 audioLanguageItem, audioLanguageItem2;

        private Sample overlayShowSample, overlayHideSample;
        private ProjectYomiMaterialButton reportButton;
        private FormTextBox reportComment, playlistTitleBox, editPlaylistTitleBox;

        private FormDropdown<Playlist> myPlaylistsDropdown;

        private Container overlayFadeContainer;
        private Container commentsEmpty;
        private RoundedButtonContainer dislikeButton;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // Be sure to dispose the track, otherwise memory will be leaked!
            // This is automatic for DrawableTrack.
            overlayShowSample.Dispose();
            overlayHideSample.Dispose();
            currentVideoSource?.Expire();
        }

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private CircularContainer videoLoadingProgressBase;
        private NekoPlayerLoadingSpinner videoLoadingProgressSpinner;
        private Box videoLoadingProgressBaseBG;

        private Bindable<float> DistortionVolume;
        private FormSliderBar<float> DistortionVolumeSlider;
        private readonly Bindable<SettingsNote.Data?> DistortionVolumeNotice = new Bindable<SettingsNote.Data?>();

        private ProjectYomiSpriteText videoLoadingProgress, videoInfoDetails, likeCount, dislikeCount, commentCount, commentsContainerTitle, currentTime, totalTime, volumeText;
        private ProjectYomiSpriteText speedText;
        private LinkFlowContainer videoDescription;
        private FillFlowContainer commentContainer, searchResultContainer, playlistItemsView, myPlaylistItemsView;

        [Resolved]
        private GoogleOAuth2 googleOAuth2 { get; set; } = null!;

        private ReportDropdown reportReason, reportSubReason;

        private BindableNumber<double> videoProgress = new BindableNumber<double>()
        {
            MinValue = 0,
            MaxValue = 1,
        };

        private Bindable<double> windowedPositionX = null!;
        private Bindable<double> windowedPositionY = null!;
        private Bindable<WindowMode> windowMode = null!;
        private Bindable<List<InternalShader>> appliedEffects = new Bindable<List<InternalShader>>();

        private Bindable<ClosedCaptionLanguage> captionLanguage = null!;
        private bool isControlVisible = true;

        private Bindable<Config.AudioQuality> audioQuality;
        private Bindable<Config.VideoQuality> videoQuality;
        private Bindable<HardwareVideoDecoder> hardwareVideoDecoder;
        private Bindable<Localisation.Language> audioLanguage;
        private Bindable<bool> adjustPitch;
        private Bindable<string> localeBindable = new Bindable<string>();
        private ThumbnailContainerBackground thumbnailContainer;
        private NekoPlayerSeekBar<double> seekbar;
        private Bindable<LocalisableString> updateInfomationText;
        private Bindable<bool> updateButtonEnabled, fpsDisplay, captionEnabled, use_sdl3;
        private Bindable<AspectRatioMethod> aspectRatioMethod;
        private Bindable<DiscordRichPresenceMode> discordRichPresence;

        [Resolved]
        private AudioEffectsConfigManager audioEffectsConfig { get; set; } = null!;

        private ProjectYomiTextFlowContainer debugInfo;

        private BufferedContainer videoScalingContainer;

        private Box likeButtonBackground, dislikeButtonBackground, likeButtonBackgroundSelected, dislikeButtonBackgroundSelected, speedBarBG, volumeBarBG, timeBG;

        private FillFlowContainer likeButtonForeground, dislikeButtonForeground;

        private RoundedAdaptiveButtonV2 subscribeButton;
        private ProfileImage profileImage;

        private Container userInterfaceContainer;

        private Bindable<bool> alwaysUseOriginalAudio;

        [Resolved]
        private ProjectYomiColour colours { get; set; } = null!;

        private Bindable<SettingsNote.Data> videoQualityWarning = new Bindable<SettingsNote.Data>();
        private Bindable<SettingsNote.Data> oauth_note = new Bindable<SettingsNote.Data>();
        private Bindable<SettingsNote.Data> hwAccelNote = new Bindable<SettingsNote.Data>();

        private Bindable<OverlayColourScheme> colourSchemeBindable;
        private Bindable<ProfileImageShape> profileImageShape;
        private Bindable<CloseButtonAction> closeButtonAction;

        private Bindable<VideoMetadataDisplayAlignment> videoMetadataDisplayAlignment;

        private Bindable<UIFont> ui_font;
        private Bindable<CaptionFonts> caption_font;

        private Bindable<float> scalingBackgroundDim = null!;

        private Bindable<double> speedTextRolling;
        private Bindable<double> volumeTextRolling;

        private SpriteIcon volumeIcon, speedBarIcon;

        private PlaybackSpeedSliderBar speedBarSlider;
        private RoundedSliderBar<double> volumeBarSlider;

        private LinkFlowContainer playlistAuthor;

        private Bindable<bool> signedIn;

        //private ParallaxContainer thumbnailContainerBase;

        [Resolved]
        private ShaderManager shaderManager { get; set; } = null!;

        private Bindable<double> videoVolume;

        private GhostIcon ghostIcon, ghostIcon2;

#nullable enable
        [Resolved(canBeNull: true)]
        private Online.DiscordRPC? discordRPC { get; set; }
#nullable disable

        //effects
        private Bindable<bool> reverbEnabled, rotateEnabled, echoEnabled, distortionEnabled, karaokeEnabled, chorusEnabled, eightBitEnabled;
        private FillFlowContainer reverbSettings, rotateSettings, echoSettings, distortionSettings, karaokeSettings, chorusSettings, eightBitSettings;

        private Bindable<bool> repeat = new Bindable<bool>();
        private Bindable<bool> alwaysShowControl = new Bindable<bool>();

        protected T GetShaderByType<T>() where T : InternalShader, new()
            => shaderManager.LocalInternalShader<T>();

        private ControlBarIconButton repeatButton, pinButton, captionButton, videoSettingsButton, playlistButton, quickLikeButton, quickDislikeButton, quickCommentOpenButton;

        private ProjectYomiSpriteText timeText;

        private Bindable<bool> trayIconVisible;

        private Bindable<CommentsSortCriteria> commentsSort;
        private Bindable<SearchSortCriteria> searchSort;

        protected Bindable<ReleaseStream> ReleaseStream;

        private Bindable<SFXType> overlaySFXType;
        private Bindable<bool> playOverlaySFX, resetPlaybackSpeedWhenLoadingAVideo, advancedCaptions;

        private Bindable<float> captionBGOpacity;

        private CancellationTokenSource videoLoadProcess;

        private Bindable<Language> uiLanguage;

        private bool commentTextBoxContainerFocused, searchTextBoxContainerFocused;
        private Container commentTextBoxContainer, searchTextBoxContainer;

        private Container topUIContainer, bottomUIContainer, videoMetadataDisplayBase;

        private Sprite playlistThumbnail;

        [Resolved]
        private PushNotificationOverlay notificationOverlay { get; set; }

    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Caption;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Shaders.New;
using AXPlayer.App.Graphics.Shaders.New.Bloom;
using AXPlayer.App.Graphics.Shaders.New.Chromatic;
using AXPlayer.App.Graphics.Shaders.New.CRT;
using AXPlayer.App.Graphics.Shaders.New.Grayscale;
using AXPlayer.App.Graphics.Shaders.New.HueShift;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Video;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osuTK;
using osuTK.Graphics;
using YoutubeExplode.Videos.ClosedCaptions;

namespace AXPlayer.App.Graphics.Videos
{
    public partial class YouTubeVideoPlayer : Container
    {
        private const double milliseconds_per_second = 1000;
        private const double video_sync_tolerance = 10;
        private const double seek_interval = 5000;

        private Video video = null!;
        private Track track = null!;
        private DrawableTrack drawableTrack = null!;
        private readonly Google.Apis.YouTube.v3.Data.Video videoData;

        private readonly string fileName_Video;
        private readonly string fileName_Audio;
        private string srv3Contents = null!;
        private ClosedCaptionTrack closedCaptionTrack;

        private StopwatchClock rateAdjustClock = null!;
        private DecouplingFramedClock framedClock = null!;

        private BufferedContainer blurContainer = null!;

        public Bindable<double> PlaybackSpeed = null!;

        private bool playbackAvailable = true;

        /// <summary>
        /// Whether this player can still receive playback commands. Becomes <c>false</c> once the player is disposed.
        /// </summary>
        public bool IsPlaybackAvailable => playbackAvailable;

        private readonly double resumeFromTime;
        private readonly double initialVolume;
        private readonly bool startPaused;
        private bool mediaSessionEnabled;
        private bool trackFinished;

        public Action? OnVideoCompleted = null!;

        public Action? OnSeekAction = null!;
        public Action? OnUpdateAction = null!;

        private Container liveChatOverlayContainer;

        private MediaSessionControls mediaSessionControls = null!;

        private LiveChatReplayOverlay liveChatOverlay = null!;
        private IconButton liveChatToggleButton = null!;
        private readonly Task<IReadOnlyList<LiveChatReplayMessage>>? preloadedLiveChatReplay;
        private CancellationTokenSource? liveChatLoadCancellation;
        private string? liveChatVideoId;

        /// <summary>
        /// Whether the live chat replay overlay is open. Toggling this value shows or hides the overlay.
        /// </summary>
        public Bindable<bool> LiveChatOverlayVisible { get; } = new BindableBool();

        /// <summary>
        /// Toggles the live chat replay overlay of this player.
        /// </summary>
        public void ToggleLiveChatOverlay() => LiveChatOverlayVisible.Value = !LiveChatOverlayVisible.Value;

        [Resolved]
        private YouTubeAPI api { get; set; }

#nullable enable
        [Resolved]
        private MediaSession? mediaSession { get; set; }
#nullable disable

        public YouTubeVideoPlayer(string fileName_Video, string fileName_Audio, ClosedCaptionTrack closedCaptionTrack, string srv3Contents, Google.Apis.YouTube.v3.Data.Video videoData, double resumeFromTime, double initialVolume = 1, bool startPaused = false, bool enableMediaSession = true, Task<IReadOnlyList<LiveChatReplayMessage>>? preloadedLiveChatReplay = null)
        {
            this.fileName_Video = fileName_Video;
            this.fileName_Audio = fileName_Audio;
            this.srv3Contents = srv3Contents;
            this.closedCaptionTrack = closedCaptionTrack;
            this.videoData = videoData;
            this.resumeFromTime = resumeFromTime;
            this.initialVolume = initialVolume;
            this.startPaused = startPaused;
            this.preloadedLiveChatReplay = preloadedLiveChatReplay;
            mediaSessionEnabled = enableMediaSession;
        }

        public void UpdateCaptionTrack(ClosedCaptionTrack closedCaptionTrack, string srv3Contents)
        {
            this.srv3Contents = srv3Contents;
            this.closedCaptionTrack = closedCaptionTrack;

            if (!string.IsNullOrEmpty(srv3Contents))
            {
                if (useNewSubtitlesFeature.Value)
                    closedCaption.UpdateSrv3CaptionTrack(srv3Contents);
                else
                    closedCaption.UpdateCaptionTrack(closedCaptionTrack);
            }
            else
                closedCaption.UpdateCaptionTrack(closedCaptionTrack);
        }

        public BindableNumber<double> VideoProgress { get; } = new BindableNumber<double>()
        {
            MinValue = 0,
            MaxValue = 1,
        };

        private KeyBindingAnimations keyBindingAnimations = null!;
        private ClosedCaptionContainer closedCaption = null!;
        private Bindable<AspectRatioMethod> aspectRatioMethod = null!;
        private Bindable<float> videoBloomLevel, chromaticAberrationStrength, videoGrayscaleLevel, videoHueShift = null!;

        private Bindable<bool> videoCrtEffectEnabled = null!, smoothPlaybackSpeed = null!;

        private VideoNewShaderContainer bloom, chromatic, grayscale, hueShift, crt = null!;

        private Bindable<bool> useNewSubtitlesFeature = null!;

        private Bindable<Localisation.Language> uiLanguage = null!;

        private bool lastPlayingState = false;
        private bool isSeeking = false;

        public void UpdateSeekingState(bool seeking)
        {
            if (isSeeking == seeking)
                return;

            isSeeking = seeking;

            if (seeking)
            {
                lastPlayingState = IsPlaying();
                if (lastPlayingState)
                {
                    Pause();
                }
                blurContainer.BlurTo(new osuTK.Vector2(10), 250, Easing.OutQuint);
                blurContainer.FadeColour(Color4.Gray, 250, Easing.OutQuint);
            }
            else
            {
                if (lastPlayingState)
                {
                    Play();
                }
                blurContainer.BlurTo(new osuTK.Vector2(0), 250, Easing.OutQuint);
                blurContainer.FadeColour(Color4.White, 250, Easing.OutQuint);
            }
        }

        private Bindable<bool> isLiveChatInvisible = null!;

        [BackgroundDependencyLoader]
        private void load(ITrackStore tracks, AXPlayerConfigManager config, ScreenshotManager screenshotManager, OverlayColourProvider overlayColourProvider, SessionStatics sessionStatics)
        {
            uiVisible = screenshotManager.CursorVisibility.GetBoundCopy();
            aspectRatioMethod = config.GetBindable<AspectRatioMethod>(AXPlayerSetting.AspectRatioMethod);
            smoothPlaybackSpeed = config.GetBindable<bool>(AXPlayerSetting.SmoothPlaybackSpeed);
            videoBloomLevel = config.GetBindable<float>(AXPlayerSetting.VideoBloomLevel);
            videoGrayscaleLevel = config.GetBindable<float>(AXPlayerSetting.VideoGrayscaleLevel);
            videoHueShift = config.GetBindable<float>(AXPlayerSetting.VideoHueShift);
            videoCrtEffectEnabled = config.GetBindable<bool>(AXPlayerSetting.VideoCRTEffectEnabled);
            chromaticAberrationStrength = config.GetBindable<float>(AXPlayerSetting.ChromaticAberrationStrength);
            useNewSubtitlesFeature = config.GetBindable<bool>(AXPlayerSetting.UseNewSubtitlesFeature);
            track = tracks.GetFromStream(File.OpenRead(fileName_Audio), fileName_Audio);
            PlaybackSpeed = new Bindable<double>(1);
            uiLanguage = app.CurrentLanguage.GetBoundCopy();

            isLiveChatInvisible = sessionStatics.GetBindable<bool>(Static.IsLiveChatInvisible);

            rateAdjustClock = new StopwatchClock(false);
            framedClock = new DecouplingFramedClock(rateAdjustClock);
            drawableTrack = new DrawableTrack(track)
            {
                Clock = framedClock,
            };
            drawableTrack.Volume.Value = initialVolume;

            mediaSessionControls = new MediaSessionControls()
            {
                NextButtonPressed = () => Schedule(() => FastForward10Sec()),
                PrevButtonPressed = () => Schedule(() => FastRewind10Sec()),
                PlayButtonPressed = () => Schedule(() => Play()),
                PauseButtonPressed = () => Schedule(() => Pause()),
                OnSeek = pos =>
                {
                    Schedule(() => SeekTo(pos));
                },
            };

            if (mediaSessionEnabled)
            {
                mediaSession?.CreateMediaSession(api, fileName_Audio);
                mediaSession?.RegisterControlEvents(mediaSessionControls);
            }

            AddRange(new Drawable[] {
                drawableTrack,
                new ScalingContainerNew(ScalingMode.Video)
                {
                    Children = new Drawable[] {
                        new DimmableContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = crt = new CrtContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = grayscale = new GrayscaleContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = chromatic = new ChromaticContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = bloom = new BloomContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Child = hueShift = new HueShiftContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Children = new Drawable[]
                                                {
                                                    blurContainer = new BufferedContainer
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Children = new Drawable[]
                                                        {
                                                            new Box
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                Colour = Color4.Black,
                                                            },
                                                            video = new Video(fileName_Video, false)
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                FillMode = FillMode.Fit,
                                                                Anchor = Anchor.Centre,
                                                                Origin = Anchor.Centre,
                                                                Clock = framedClock,
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                }
                            }
                        },
                    }
                },
                liveChatOverlayContainer = new Container
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Size = new Vector2(320, 300),
                    Alpha = 0,
                    Margin = new MarginPadding
                    {
                        Right = 16,
                        Bottom = 96,
                    },
                    Child = liveChatOverlay = new LiveChatReplayOverlay(true)
                    {
                        CloseAction = () => LiveChatOverlayVisible.Value = false,
                    },
                },
                keyBindingAnimations = new KeyBindingAnimations
                {
                    RelativeSizeAxes = Axes.Both,
                },
                closedCaption = new ClosedCaptionContainer(this, closedCaptionTrack, srv3Contents, useNewSubtitlesFeature),
                liveChatToggleButton = new IconButton
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Size = new Vector2(40),
                    Margin = new MarginPadding
                    {
                        Right = 16,
                        Bottom = 96,
                    },
                    Icon = FontAwesome.Solid.CommentAlt,
                    IconScale = new Vector2(0.85f),
                    BackgroundColour = overlayColourProvider.Background5.Opacity(0.85f),
                    TooltipText = AXPlayerStrings.LiveChatReplay("0"),
                    Action = ToggleLiveChatOverlay,
                }
            });

            UpdatePreservePitch(config.Get<bool>(AXPlayerSetting.AdjustPitchOnSpeedChange));

            SeekTo(resumeFromTime * milliseconds_per_second);
            if (!startPaused)
                Play();

            uiVisible.BindValueChanged(visible =>
            {
                Schedule(() =>
                {
                    if (visible.NewValue)
                    {
                        keyBindingAnimations.Show();
                        closedCaption.Show();

                        if (!LiveChatOverlayVisible.Value && videoData.LiveStreamingDetails != null)
                            liveChatToggleButton.Show();
                    }
                    else
                    {
                        keyBindingAnimations.Hide();
                        closedCaption.Hide();

                        if (!LiveChatOverlayVisible.Value && videoData.LiveStreamingDetails != null)
                            liveChatToggleButton.Hide();
                    }

                    updateLiveChatToggleVisibility();
                });
            }, true);

            if (videoData.LiveStreamingDetails != null)
                liveChatToggleButton.Show();
            else
                liveChatToggleButton.Hide();

            LiveChatOverlayVisible.BindValueChanged(visible =>
            {
                liveChatToggleButton.IconColour = visible.NewValue ? overlayColourProvider.Highlight1 : overlayColourProvider.Content2;

                if (visible.NewValue)
                {
                    liveChatOverlayContainer.ScaleTo(1, 500, Easing.OutElasticHalf);
                    liveChatOverlayContainer.FadeIn(250, Easing.OutQuint);
                    loadLiveChatReplay();
                }
                else
                {
                    liveChatOverlayContainer.ScaleTo(0.7f, 500, Easing.OutQuint);
                    liveChatOverlayContainer.FadeOut(250, Easing.OutQuint);
                }

                updateLiveChatToggleVisibility();
            }, true);

            isLiveChatInvisible.BindValueChanged(invisible =>
            {
                if (invisible.NewValue)
                {
                    if (LiveChatOverlayVisible.Value)
                        liveChatOverlayContainer.Hide();

                    if (!LiveChatOverlayVisible.Value && videoData.LiveStreamingDetails != null)
                        liveChatToggleButton.Hide();
                }
                else
                {
                    if (LiveChatOverlayVisible.Value)
                        liveChatOverlayContainer.Show();

                    if (!LiveChatOverlayVisible.Value && videoData.LiveStreamingDetails != null)
                        liveChatToggleButton.Show();
                }
            });

            aspectRatioMethod.BindValueChanged(value =>
            {
                video.FillMode = value.NewValue == AspectRatioMethod.Letterbox ? FillMode.Fit : FillMode.Stretch;
            }, true);

            videoBloomLevel.BindValueChanged(value =>
            {
                bloom.Strength = value.NewValue;
            }, true);

            videoGrayscaleLevel.BindValueChanged(value =>
            {
                grayscale.Strength = value.NewValue;
            }, true);

            chromaticAberrationStrength.BindValueChanged(value =>
            {
                chromatic.Strength = value.NewValue;
            }, true);

            videoHueShift.BindValueChanged(value =>
            {
                hueShift.Strength = value.NewValue / 360;
            }, true);

            videoCrtEffectEnabled.BindValueChanged(value =>
            {
                crt.Strength = value.NewValue ? 1 : 0;
            }, true);

            //drawableTrack.Completed += trackCompleted;
        }

        private IBindable<bool> uiVisible = null!;

        /*
        private void trackCompleted()
        {
            trackFinished = true;
            //SeekTo(0);
            //Pause();
            OnVideoCompleted?.Invoke();
        }
        */

        public void UpdatePreservePitch(bool value)
        {
            drawableTrack?.RemoveAllAdjustments(AdjustableProperty.Tempo);
            drawableTrack?.RemoveAllAdjustments(AdjustableProperty.Frequency);

            if (value)
                drawableTrack?.AddAdjustment(AdjustableProperty.Frequency, PlaybackSpeed);
            else
                drawableTrack?.AddAdjustment(AdjustableProperty.Tempo, PlaybackSpeed);
        }

        /// <summary>
        /// The toggle button is only shown while the player controls are visible and the overlay is closed.
        /// </summary>
        private void updateLiveChatToggleVisibility()
        {
            bool visible = uiVisible.Value && !LiveChatOverlayVisible.Value;

            if (videoData.LiveStreamingDetails != null)
            {
                liveChatToggleButton.FadeTo(visible ? 1 : 0, 250, Easing.OutQuint);
                liveChatToggleButton.Enabled.Value = visible;
            }
        }

        /// <summary>
        /// Loads the live chat replay of the current video once, the first time the overlay is opened.
        /// </summary>
        private void loadLiveChatReplay()
        {
            string? videoId = videoData?.Id;

            if (string.IsNullOrEmpty(videoId))
            {
                liveChatOverlay.ShowUnavailable();
                return;
            }

            if (liveChatVideoId == videoId)
                return;

            liveChatVideoId = videoId;

            liveChatLoadCancellation?.Cancel();
            liveChatLoadCancellation?.Dispose();
            liveChatLoadCancellation = new CancellationTokenSource();

            CancellationToken cancellationToken = liveChatLoadCancellation.Token;

            liveChatOverlay.SetLoading();

            // The player usually receives a fetch that was already started by SetVideoSource.
            // Fall back to fetching here if the player was created without a preload.
            Task<IReadOnlyList<LiveChatReplayMessage>> loadTask = preloadedLiveChatReplay ?? api.GetLiveChatReplayAsync(videoId, cancellationToken);

            completeLiveChatReplayLoadAsync(loadTask, videoId, cancellationToken).FireAndForget();
        }

        private async Task completeLiveChatReplayLoadAsync(Task<IReadOnlyList<LiveChatReplayMessage>> loadTask, string videoId, CancellationToken cancellationToken)
        {
            try
            {
                IReadOnlyList<LiveChatReplayMessage> messages = await loadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                Schedule(() =>
                {
                    if (cancellationToken.IsCancellationRequested || liveChatVideoId != videoId)
                        return;

                    if (messages.Count == 0)
                    {
                        liveChatOverlay.ShowUnavailable();
                        return;
                    }

                    liveChatOverlay.SetMessages(messages);
                    liveChatToggleButton.TooltipText = AXPlayerStrings.LiveChatReplay(messages.Count.ToString(CultureInfo.InvariantCulture));
                });
            }
            catch (OperationCanceledException)
            {
                // The player was disposed, or its live chat load was superseded.
            }
            catch (Exception exception)
            {
                Logger.Error(exception, exception.GetDescription());

                Schedule(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                        liveChatOverlay.ShowUnavailable();
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            playbackAvailable = false;

            uiLanguage.UnbindAll();
            uiVisible.UnbindAll();
            PlaybackSpeed?.UnbindAll();
            LiveChatOverlayVisible.UnbindAll();
            liveChatLoadCancellation?.Cancel();
            liveChatLoadCancellation?.Dispose();
            liveChatLoadCancellation = null;
            DeactivateMediaSession();

            video.Dispose();
            drawableTrack.Dispose();

            /*
            if (drawableTrack != null)
                drawableTrack.Completed -= trackCompleted;
            */

            base.Dispose(isDisposing);
        }

        public bool IsPlaying()
        {
            if (drawableTrack == null)
                return false;

            if (drawableTrack.HasCompleted)
                return false;

            return drawableTrack.IsRunning;
        }

        [Resolved]
        private AXPlayerApp app { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            uiLanguage.BindValueChanged(lang =>
            {
                Schedule(() =>
                {
                    if (mediaSessionEnabled)
                        mediaSession?.UpdateMediaSession(videoData);
                });
            }, true);

            PlaybackSpeed.BindValueChanged(v =>
            {
                rateAdjustClock.Rate = v.NewValue;
                if (mediaSessionEnabled)
                    mediaSession?.UpdatePlaybackSpeed(v.NewValue);
            }, true);

            if (mediaSessionEnabled)
                mediaSession?.UpdateTimestamp(videoData, 0);
        }

        protected override void Update()
        {
            base.Update();

            if (drawableTrack != null)
            {
                double length = drawableTrack.Length / milliseconds_per_second;

                if (VideoProgress.MaxValue != length)
                    VideoProgress.MaxValue = length;

                if (!trackFinished && drawableTrack.CurrentTime >= drawableTrack.Length - 100) //this will fix app hang bugs
                {
                    trackFinished = true;
                    Pause();
                    SeekTo(1);
                    OnVideoCompleted.Invoke();
                }

                VideoProgress.Value = drawableTrack.CurrentTime / milliseconds_per_second;
                liveChatOverlay.PlaybackPosition.Value = VideoProgress.Value;

                if (Math.Abs(drawableTrack.CurrentTime - video.Time.Current) > video_sync_tolerance)
                    video.Seek(drawableTrack.CurrentTime);

                OnUpdateAction?.Invoke();
            }
        }

        public void SeekTo(double pos)
        {
            if (drawableTrack == null)
                return;

            double targetPosition = Math.Clamp(pos, 0, drawableTrack.Length);

            drawableTrack.Seek(targetPosition);
            video.Seek(targetPosition);
            if (mediaSessionEnabled)
                mediaSession?.UpdateTimestamp(videoData, targetPosition);

            Schedule(() => OnSeekAction?.Invoke());
        }

        public void FastForward10Sec()
        {
            if (drawableTrack == null)
                return;

            double targetPosition = Math.Min(drawableTrack.CurrentTime + seek_interval, drawableTrack.Length);
            SeekTo(targetPosition);

            if (targetPosition >= drawableTrack.Length)
            {
                trackFinished = true;
                Pause();
            }

            keyBindingAnimations.PlaySeekAnimation(KeyBindingAnimations.SeekAction.FastForward10sec, FontAwesome.Solid.Box);
        }

        public void FastRewind10Sec()
        {
            if (drawableTrack == null)
                return;

            SeekTo(drawableTrack.CurrentTime - seek_interval);
            keyBindingAnimations.PlaySeekAnimation(KeyBindingAnimations.SeekAction.FastRewind10sec, FontAwesome.Solid.Box);
        }

        public void Pause(bool isKeyboardOrMouseAction = false)
        {
            if (drawableTrack == null)
                return;

            drawableTrack.Stop();
            framedClock.Stop();

            if (mediaSessionEnabled)
            {
                mediaSession?.UpdatePlayingState(false);
                mediaSession?.UpdateTimestamp(videoData, drawableTrack.CurrentTime);
            }

            if (isKeyboardOrMouseAction)
                keyBindingAnimations.PlaySeekAnimation(KeyBindingAnimations.SeekAction.PlayPause, FontAwesome.Solid.Pause);
        }

        public void Play(bool isKeyboardOrMouseAction = false)
        {
            if (drawableTrack == null)
                return;

            if (trackFinished)
            {
                if (drawableTrack.CurrentTime >= drawableTrack.Length)
                    SeekTo(0);

                trackFinished = false;
            }

            if (mediaSessionEnabled)
            {
                mediaSession?.UpdatePlayingState(true);
                mediaSession?.UpdateTimestamp(videoData, drawableTrack.CurrentTime);
            }

            drawableTrack.Start();
            framedClock.Start();

            if (isKeyboardOrMouseAction)
                keyBindingAnimations.PlaySeekAnimation(KeyBindingAnimations.SeekAction.PlayPause, FontAwesome.Solid.Play);
        }

        [Resolved]
        private SessionStatics sessionStatics { get; set; } = null!;

        public void SetPlaybackSpeed(double speed)
        {
            if (smoothPlaybackSpeed.Value)
                this.TransformBindableTo(PlaybackSpeed, speed, 1000, Easing.OutSine);
            else
                PlaybackSpeed.Value = speed;
        }

        public bool IsReady => drawableTrack != null && video != null;

        public void SetTransitionVolume(double volume)
        {
            if (drawableTrack != null)
                drawableTrack.Volume.Value = Math.Clamp(volume, 0, 1);
        }

        public void ActivateMediaSession()
        {
            if (mediaSessionEnabled)
                return;

            mediaSessionEnabled = true;
            mediaSession?.CreateMediaSession(api, fileName_Audio);
            mediaSession?.RegisterControlEvents(mediaSessionControls);
            Schedule(() =>
            {
                mediaSession?.UpdateMediaSession(videoData);
            });
            mediaSession?.UpdatePlayingState(IsPlaying());
            if (drawableTrack != null)
                mediaSession?.UpdateTimestamp(videoData, drawableTrack.CurrentTime);
        }

        public void DeactivateMediaSession()
        {
            if (!mediaSessionEnabled)
                return;

            mediaSessionEnabled = false;
            mediaSession?.UnregisterControlEvents();
            mediaSession?.DeleteMediaSession();
        }
    }
}

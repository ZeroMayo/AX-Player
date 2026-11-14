// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Audio;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.Videos;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Overlays;
using AXPlayer.App.Overlays.OSD;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osuTK.Graphics;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.ClosedCaptions;
using YoutubeExplode.Videos.Streams;
using static AXPlayer.App.Graphics.Videos.YouTubeVideoPlayer.KeyBindingAnimations;
using Language = AXPlayer.App.Localisation.Language;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        private void addVideoToScreen()
        {
            //Task.Run(async () => await api.SendPlayerResponseAsync(videoId));

            string audioFile = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{videoId}") + @"/audio.ogg";

            AudioNormalization audioNormalization = new AudioNormalization(audioFile);

            if (audioNormalization.IntegratedLoudness == null)
            {
                Logger.Log($"Failed to calculate audio normalization values for {api.GetChannel(videoData.Snippet.ChannelId).Snippet.Title} - {videoData.Snippet.Title}", LoggingTarget.Runtime, LogLevel.Error);
            }

            app.CurrentTrackNormalizeVolume.Value = audioNormalization?.IntegratedLoudnessInVolumeOffset ?? AudioNormalizationManager.FALLBACK_VOLUME;

            videoContainer.Add(currentVideoSource);

            videoLoadingProgressBase.Hide();
            Schedule(() => videoLoadingProgress.Text = "");

            videoProgress.BindValueChanged(seek =>
            {
                if (currentVideoSource != null && currentVideoSource.IsPlaying() == false)
                    seekTo(seek.NewValue * 1000);
            });

            playbackSpeed.BindValueChanged(speed =>
            {
                setPlaybackSpeed(speed.NewValue);
            }, true);

            currentVideoSource.OnSeekAction = () =>
            {
                updatePresence(discordRichPresence.Value);
            };

            currentVideoSource.PlaybackSpeed.BindValueChanged(speed =>
            {
                updatePlaybackSpeedDisplay();
            });

            if (timestampInURL(videoUrl))
                Schedule(() => seekTo(parseTimestampFromURL(videoUrl)));

            configureVideoCompleted(currentVideoSource);
            currentVideoSource.OnUpdateAction = updateSmartCrossfade;
            queueNextPlaylistVideoForCrossfade();

            updatePresence(discordRichPresence.Value);
        }

        private void configureVideoCompleted(YouTubeVideoPlayer source)
        {
            source.OnVideoCompleted = () =>
            {
                if (smartCrossfadeOutgoing == source && smartCrossfadeIncoming != null)
                {
                    if (repeat.Value)
                    {
                        cancelSmartCrossfade();
                    }
                    else if (smartCrossfadeIncoming.IsReady && smartCrossfadeStarted)
                    {
                        finishSmartCrossfade();
                        return;
                    }
                    else
                    {
                        cancelSmartCrossfade();
                    }
                }

                if (source != currentVideoSource)
                    return;

                if (repeat.Value)
                {
                    source.Play();
                    return;
                }

                if (playlists == null || playlists.Count == 0 || playlistItemIndex >= playlists.Count - 1)
                    return;

                playlistItemIndex++;
                string nextVideoId = playlists[playlistItemIndex].Snippet.ResourceId.VideoId;
                Schedule(() => SetVideoSource(nextVideoId));
            };
        }

        private void updateSmartCrossfade()
        {
            bool enabled = appGlobalConfig.Get<bool>(AXPlayerSetting.SmartCrossfadeEnabled);
            double configuredDuration = Math.Clamp(appGlobalConfig.Get<float>(AXPlayerSetting.SmartCrossfadeDuration), 1f, 12f);
            bool trimSilence = appGlobalConfig.Get<bool>(AXPlayerSetting.SmartCrossfadeSilenceTrim);

            if (!enabled || playlists == null || repeat.Value || currentVideoSource == null || playlistItemIndex >= playlists.Count - 1)
            {
                cancelSmartCrossfade();
                return;
            }

            if (smartCrossfadeStarted)
            {
                updateSmartCrossfadeProgress();
                return;
            }

            double remaining = currentVideoSource.VideoProgress.MaxValue - currentVideoSource.VideoProgress.Value;
            double prepareLead = Math.Clamp(configuredDuration + smart_crossfade_prepare_padding_seconds, 10, 20);

            if (smartCrossfadeIncoming == null && !smartCrossfadePreparing && remaining > 0 && remaining <= prepareLead && DateTime.UtcNow >= smartCrossfadeNextPrepareAttemptUtc)
                beginSmartCrossfadePreparation(configuredDuration, trimSilence);

            if (smartCrossfadeIncoming == null || smartCrossfadePlan == null)
                return;

            double audibleRemaining = currentVideoSource.VideoProgress.MaxValue - currentVideoSource.VideoProgress.Value - smartCrossfadePlan.OutgoingTailSilence;

            if (!smartCrossfadeStarted && currentVideoSource.IsPlaying() && audibleRemaining <= smartCrossfadePlan.Duration)
            {
                if (!smartCrossfadeIncoming.IsReady)
                    return;

                startSmartCrossfade(configuredDuration);
            }

            if (!smartCrossfadeStarted && remaining > prepareLead + 1)
                cancelSmartCrossfade();
        }

        private void updateSmartCrossfadeProgress()
        {
            if (smartCrossfadeOutgoing == null || smartCrossfadeIncoming == null || smartCrossfadePlan == null)
            {
                cancelSmartCrossfade();
                return;
            }

            if (smartCrossfadeOutgoing.IsPlaying())
            {
                if (!smartCrossfadeIncoming.IsPlaying())
                    smartCrossfadeIncoming.Play();

                double outgoingRemaining = smartCrossfadeOutgoing.VideoProgress.MaxValue - smartCrossfadeOutgoing.VideoProgress.Value;
                double audibleRemaining = outgoingRemaining - smartCrossfadePlan.OutgoingTailSilence;
                double progress = Math.Clamp(1 - audibleRemaining / Math.Max(0.1, smartCrossfadeDurationSeconds), 0, 1);
                double easedProgress = progress * progress * progress * (progress * (progress * 6 - 15) + 10);
                double fadeAngle = easedProgress * Math.PI / 2;

                smartCrossfadeOutgoing.Alpha = (float)(1 - easedProgress);
                smartCrossfadeOutgoing.Scale = new osuTK.Vector2((float)(1 - easedProgress * 0.035));
                smartCrossfadeOutgoing.SetTransitionVolume(Math.Cos(fadeAngle));
                smartCrossfadeIncoming.Alpha = (float)easedProgress;
                smartCrossfadeIncoming.Scale = new osuTK.Vector2((float)(1.045 - easedProgress * 0.045));
                smartCrossfadeIncoming.SetTransitionVolume(Math.Sin(fadeAngle));

                if (!smartCrossfadeSourceSwitched && progress >= 0.5)
                    switchSmartCrossfadeSource();

                if (progress >= 1)
                    finishSmartCrossfade();
            }
            else
            {
                if (smartCrossfadeIncoming.IsPlaying())
                    smartCrossfadeIncoming.Pause();
            }
        }

        private void beginSmartCrossfadePreparation(double configuredDuration, bool trimSilence)
        {
            if (playlists == null || currentVideoSource == null || playlistItemIndex + 1 >= playlists.Count)
                return;

            int capturedIndex = playlistItemIndex;
            YouTubeVideoPlayer outgoing = currentVideoSource;
            string nextVideoId = playlists[capturedIndex + 1].Snippet.ResourceId.VideoId;

            if (string.IsNullOrEmpty(nextVideoId))
            {
                smartCrossfadeNextPrepareAttemptUtc = DateTime.UtcNow.AddSeconds(2);
                return;
            }

            // Never read files that are still being written by the background cache download.
            if (playlistVideoCachesInProgress.ContainsKey(nextVideoId))
            {
                smartCrossfadeNextPrepareAttemptUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            string videoDirectory = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath(nextVideoId);
            string audioFile = Path.Combine(videoDirectory, "audio.ogg");
            string mp4File = Path.Combine(videoDirectory, "video.mp4");
            string webmFile = Path.Combine(videoDirectory, "video.webm");
            string cachedVideoFile = File.Exists(mp4File) ? mp4File : File.Exists(webmFile) ? webmFile : null;

            if (cachedVideoFile == null || !File.Exists(audioFile))
            {
                // The next track is not cached yet. Download it in the background and try again shortly.
                queuePlaylistVideoCache(capturedIndex + 1);
                smartCrossfadeNextPrepareAttemptUtc = DateTime.UtcNow.AddSeconds(1.5);
                return;
            }

            string currentAudioFile = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath(videoId) + "/audio.ogg";
            string ffmpegPath = app.GetFFmpegPath();
            double outgoingDuration = outgoing.VideoProgress.MaxValue;
            double previousNormalizeVolume = app.CurrentTrackNormalizeVolume.Value;
            int attemptId = ++smartCrossfadeGeneration;

            smartCrossfadePreparing = true;
            smartCrossfadePreviousNormalizeVolume = previousNormalizeVolume;
            smartCrossfadeNextPrepareAttemptUtc = DateTime.MaxValue;

            Task.Run(async () =>
            {
                SmartCrossfadeAnalysis outgoingAnalysis = SmartCrossfadeAnalysis.Empty;
                SmartCrossfadeAnalysis incomingAnalysis = SmartCrossfadeAnalysis.Empty;
                Google.Apis.YouTube.v3.Data.Video nextVideoData = null;

                // Loudness analysis can take longer than the silence analysis, so run it separately and
                // do not block creating the incoming player on it.
                Task<double?> normalizeVolumeTask = Task.Run(() => analyseCrossfadeNormalizeVolume(audioFile));

                try
                {
                    Task<SmartCrossfadeAnalysis> outgoingAnalysisTask = SmartCrossfadeAnalyzer.AnalyseAsync(currentAudioFile, ffmpegPath);
                    Task<SmartCrossfadeAnalysis> incomingAnalysisTask = SmartCrossfadeAnalyzer.AnalyseAsync(audioFile, ffmpegPath);

                    await Task.WhenAll(outgoingAnalysisTask, incomingAnalysisTask).ConfigureAwait(false);

                    outgoingAnalysis = await outgoingAnalysisTask.ConfigureAwait(false);
                    incomingAnalysis = await incomingAnalysisTask.ConfigureAwait(false);
                    nextVideoData = api.GetVideo(nextVideoId);
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }

                Schedule(() =>
                {
                    if (attemptId != smartCrossfadeGeneration)
                        return;

                    smartCrossfadePreparing = false;

                    if (!ReferenceEquals(currentVideoSource, outgoing) || playlistItemIndex != capturedIndex)
                    {
                        smartCrossfadeNextPrepareAttemptUtc = DateTime.MinValue;
                        return;
                    }

                    if (nextVideoData == null)
                    {
                        smartCrossfadeNextPrepareAttemptUtc = DateTime.UtcNow.AddSeconds(2);
                        return;
                    }

                    try
                    {
                        SmartCrossfadePlan plan = SmartCrossfadePlan.Create(outgoingAnalysis, incomingAnalysis, configuredDuration, outgoingDuration, trimSilence);

                        smartCrossfadePlan = plan;
                        smartCrossfadeVideoId = nextVideoId;
                        smartCrossfadeVideoData = nextVideoData;
                        smartCrossfadeNormalizeVolume = null;
                        smartCrossfadeOutgoing = outgoing;
                        smartCrossfadeIncoming = new YouTubeVideoPlayer(cachedVideoFile, audioFile, null, string.Empty, nextVideoData, plan.IncomingStartOffset, 0, true, false, preloadedLiveChatReplay: getOrStartLiveChatReplayPreload(nextVideoId))
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Scale = new osuTK.Vector2(1.045f),
                            OnSeekAction = () => updatePresence(discordRichPresence.Value),
                        };
                        smartCrossfadeStarted = false;
                        smartCrossfadeSourceSwitched = false;
                        videoContainer.Add(smartCrossfadeIncoming);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, e.GetDescription());
                        cancelSmartCrossfade();
                    }
                });

                double? incomingNormalizeVolume = await normalizeVolumeTask.ConfigureAwait(false);

                Schedule(() =>
                {
                    if (attemptId != smartCrossfadeGeneration || incomingNormalizeVolume == null)
                        return;

                    smartCrossfadeNormalizeVolume = incomingNormalizeVolume;

                    // If the crossfade already completed before loudness analysis finished, still apply
                    // the correct normalisation for the track that is now playing.
                    if (videoId == nextVideoId && smartCrossfadeIncoming == null)
                        app.CurrentTrackNormalizeVolume.Value = incomingNormalizeVolume.Value;
                });
            });
        }

        private static double? analyseCrossfadeNormalizeVolume(string audioFile)
        {
            try
            {
                AudioNormalization normalization = new AudioNormalization(audioFile);
                return normalization.IntegratedLoudnessInVolumeOffset ?? AudioNormalizationManager.FALLBACK_VOLUME;
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
                return null;
            }
        }

        private void startSmartCrossfade(double configuredDuration)
        {
            if (smartCrossfadeIncoming == null || smartCrossfadePlan == null || currentVideoSource == null)
                return;

            double audibleRemaining = currentVideoSource.VideoProgress.MaxValue - currentVideoSource.VideoProgress.Value - smartCrossfadePlan.OutgoingTailSilence;
            smartCrossfadeDurationSeconds = Math.Clamp(Math.Min(configuredDuration, Math.Max(0.8, audibleRemaining)), 0.8, configuredDuration);
            smartCrossfadeStarted = true;
            smartCrossfadeIncoming.SetPlaybackSpeed(playbackSpeed.Value);
            smartCrossfadeIncoming.Play();
            cacheFollowingPlaylistVideo();
        }

        private void cacheFollowingPlaylistVideo() => queuePlaylistVideoCache(playlistItemIndex + 2);

        private void queueNextPlaylistVideoForCrossfade()
        {
            if (!appGlobalConfig.Get<bool>(AXPlayerSetting.SmartCrossfadeEnabled))
                return;

            queuePlaylistVideoCache(playlistItemIndex + 1);
        }

        private void queuePlaylistVideoCache(int playlistIndex)
        {
            if (playlists == null || playlistIndex < 0 || playlistIndex >= playlists.Count)
                return;

            string targetVideoId = playlists[playlistIndex].Snippet.ResourceId.VideoId;
            if (string.IsNullOrEmpty(targetVideoId) || !playlistVideoCachesInProgress.TryAdd(targetVideoId, 0))
                return;

            string preferredVideoQuality = settingsContainer.VideoQualitySettings.Current.Value;
            AudioQuality preferredAudioQuality = audioQuality.Value;
            bool useOriginalAudio = alwaysUseOriginalAudio.Value;
            string preferredAudioLanguage = appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString();
            string ffmpegPath = app.GetFFmpegPath();

            Task.Run(async () =>
            {
                try
                {
                    await cachePlaylistVideoAsync(targetVideoId, preferredVideoQuality, preferredAudioQuality, useOriginalAudio, preferredAudioLanguage, ffmpegPath);
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
                finally
                {
                    playlistVideoCachesInProgress.TryRemove(targetVideoId, out _);
                }
            });
        }

        private async Task cachePlaylistVideoAsync(string targetVideoId, string preferredVideoQuality, AudioQuality preferredAudioQuality, bool useOriginalAudio, string preferredAudioLanguage, string ffmpegPath)
        {
            string videoUrl = $"https://youtube.com/watch?v={targetVideoId}";
            string videoDirectory = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath(targetVideoId);
            string audioFile = Path.Combine(videoDirectory, "audio.ogg");
            string mp4File = Path.Combine(videoDirectory, "video.mp4");
            string webmFile = Path.Combine(videoDirectory, "video.webm");

            if (File.Exists(audioFile) && (File.Exists(mp4File) || File.Exists(webmFile)))
                return;

            Directory.CreateDirectory(videoDirectory);
            var manifest = await app.YouTubeClient.Videos.Streams.GetManifestAsync(videoUrl);
            List<IAudioStreamInfo> audioStreams = manifest.GetAudioOnlyStreams().Cast<IAudioStreamInfo>().ToList();
            if (audioStreams.Count == 0)
                return;

            IAudioStreamInfo selectedAudioStream = selectPlaylistCacheAudioStream(audioStreams, preferredAudioQuality, useOriginalAudio, preferredAudioLanguage);
            IEnumerable<IVideoStreamInfo> videoStreams = manifest.GetVideoOnlyStreams();
            List<IVideoStreamInfo> webmStreams = videoStreams
                .Where(stream => stream.Container == YoutubeExplode.Videos.Streams.Container.WebM)
                .ToList();
            List<IVideoStreamInfo> availableVideoStreams = webmStreams.Count > 0 ? webmStreams : videoStreams.ToList();
            if (availableVideoStreams.Count == 0)
                return;

            IVideoStreamInfo selectedVideoStream = availableVideoStreams
                .FirstOrDefault(stream => !string.IsNullOrEmpty(preferredVideoQuality) && stream.VideoQuality.Label.Contains(preferredVideoQuality))
                ?? availableVideoStreams.TryGetWithHighestVideoQuality();

            string targetVideoFile = selectedVideoStream.VideoCodec.Contains("avc1") ? mp4File : webmFile;
            if (!File.Exists(audioFile))
            {
                await app.YouTubeClient.Videos.DownloadAsync(
                    [selectedAudioStream],
                    new ConversionRequestBuilder(audioFile).SetFFmpegPath(ffmpegPath).Build());
            }

            if (!File.Exists(mp4File) && !File.Exists(webmFile))
            {
                await app.YouTubeClient.Videos.DownloadAsync(
                    [selectedVideoStream],
                    new ConversionRequestBuilder(targetVideoFile).SetFFmpegPath(ffmpegPath).Build());
            }
        }

        private IAudioStreamInfo selectPlaylistCacheAudioStream(IEnumerable<IAudioStreamInfo> availableStreams, AudioQuality preferredAudioQuality, bool useOriginalAudio, string preferredAudioLanguage)
        {
            List<IAudioStreamInfo> streams = availableStreams.ToList();
            IEnumerable<IAudioStreamInfo> preferredLanguageStreams;
            if (useOriginalAudio)
            {
                preferredLanguageStreams = streams.Where(stream => stream.IsAudioLanguageDefault == true);
            }
            else
            {
                preferredLanguageStreams = streams.Where(stream => stream.AudioLanguage.HasValue && stream.AudioLanguage.Value.Code.Contains(preferredAudioLanguage));
            }

            List<IAudioStreamInfo> languageMatches = preferredLanguageStreams.ToList();
            if (languageMatches.Count == 0)
                languageMatches = streams.Where(stream => stream.IsAudioLanguageDefault == true).ToList();
            if (languageMatches.Count == 0)
                languageMatches = streams.ToList();

            string preferredCodec = preferredAudioQuality == AudioQuality.PreferMp4a
                ? "mp4a"
                : preferredAudioQuality == AudioQuality.PreferOpus ? "opus" : null;
            List<IAudioStreamInfo> codecMatches = preferredCodec == null
                ? languageMatches
                : languageMatches.Where(stream => stream.AudioCodec.Contains(preferredCodec)).ToList();
            if (codecMatches.Count == 0)
                codecMatches = languageMatches;

            if (preferredAudioQuality == AudioQuality.LowQuality)
                return codecMatches[0];

            return (IAudioStreamInfo)codecMatches.TryGetWithHighestBitrate();
        }

        private void finishSmartCrossfade()
        {
            if (smartCrossfadeIncoming == null || smartCrossfadeOutgoing == null)
                return;

            if (!smartCrossfadeSourceSwitched)
                switchSmartCrossfadeSource();

            smartCrossfadeIncoming.Alpha = 1;
            smartCrossfadeIncoming.Scale = new osuTK.Vector2(1);
            smartCrossfadeIncoming.SetTransitionVolume(1);
            smartCrossfadeOutgoing.SetTransitionVolume(0);
            smartCrossfadeOutgoing.Expire();

            // Apply the incoming track's loudness normalisation only after the outgoing track has
            // been removed, since normalisation is applied globally to all tracks.
            if (smartCrossfadeNormalizeVolume.HasValue)
                app.CurrentTrackNormalizeVolume.Value = smartCrossfadeNormalizeVolume.Value;

            if (playlistItemViews.Count > 0)
            {
                foreach (PlaylistItemView playlistItemView in playlistItemViews)
                {
                    playlistItemView.UpdateState(false);
                }

                playlistItemViews[playlistItemIndex].UpdateState(true);
            }

            clearSmartCrossfadeState();
        }

        private void switchSmartCrossfadeSource()
        {
            if (smartCrossfadeIncoming == null || smartCrossfadeOutgoing == null || smartCrossfadeSourceSwitched)
                return;

            currentVideoSource = smartCrossfadeIncoming;
            videoId = smartCrossfadeVideoId;
            videoData = smartCrossfadeVideoData;
            playlistItemIndex++;
            configureVideoCompleted(smartCrossfadeIncoming);
            smartCrossfadeIncoming.OnUpdateAction = updateSmartCrossfade;
            smartCrossfadeOutgoing.OnUpdateAction = null;
            smartCrossfadeOutgoing.DeactivateMediaSession();
            smartCrossfadeIncoming.ActivateMediaSession();
            smartCrossfadeSourceSwitched = true;
            queueNextPlaylistVideoForCrossfade();

            if (playlistItemViews.Count > 0)
            {
                foreach (PlaylistItemView playlistItemView in playlistItemViews)
                {
                    playlistItemView.UpdateState(false);
                }

                playlistItemViews[playlistItemIndex].UpdateState(true);
            }

            updateVideoMetadata(videoId);
            updatePresence(discordRichPresence.Value);
        }

        private void cancelSmartCrossfade()
        {
            // If the incoming track is already the current one, complete the transition instead of
            // removing the track that is currently playing.
            if (smartCrossfadeSourceSwitched && smartCrossfadeIncoming != null)
            {
                finishSmartCrossfade();
                return;
            }

            smartCrossfadeGeneration++;
            smartCrossfadePreparing = false;

            if (smartCrossfadeIncoming != null)
                smartCrossfadeIncoming.Expire();

            if (smartCrossfadeOutgoing != null)
            {
                smartCrossfadeOutgoing.Alpha = 1;
                smartCrossfadeOutgoing.Scale = new osuTK.Vector2(1);
                smartCrossfadeOutgoing.SetTransitionVolume(1);
                app.CurrentTrackNormalizeVolume.Value = smartCrossfadePreviousNormalizeVolume;
            }

            clearSmartCrossfadeState();
        }

        private void clearSmartCrossfadeState()
        {
            smartCrossfadeOutgoing = null;
            smartCrossfadeIncoming = null;
            smartCrossfadeVideoId = string.Empty;
            smartCrossfadeVideoData = null;
            smartCrossfadePlan = null;
            smartCrossfadeNormalizeVolume = null;
            smartCrossfadeStarted = false;
            smartCrossfadeSourceSwitched = false;
            smartCrossfadePreparing = false;
            smartCrossfadeNextPrepareAttemptUtc = DateTime.MinValue;
        }

        private void seekTo(double pos)
        {
            currentVideoSource?.SeekTo(pos);
        }

        private void setPlaybackSpeed(double speed)
        {
            currentVideoSource?.SetPlaybackSpeed(speed);
        }

        private void playVideo()
        {
            currentVideoSource.Play();
        }

        private string videoUrl = string.Empty;
        private string videoId = string.Empty;
        private double pausedTime = 0;

        private string videoFile = string.Empty;

        private const double smart_crossfade_prepare_padding_seconds = 8;
        private YouTubeVideoPlayer smartCrossfadeOutgoing;
        private YouTubeVideoPlayer smartCrossfadeIncoming;
        private string smartCrossfadeVideoId = string.Empty;
        private Google.Apis.YouTube.v3.Data.Video smartCrossfadeVideoData;
        private SmartCrossfadePlan smartCrossfadePlan;
        private double? smartCrossfadeNormalizeVolume;
        private double smartCrossfadePreviousNormalizeVolume = 1;
        private double smartCrossfadeDurationSeconds = 4;
        private bool smartCrossfadeStarted;
        private bool smartCrossfadeSourceSwitched;
        private bool smartCrossfadePreparing;
        private int smartCrossfadeGeneration;
        private DateTime smartCrossfadeNextPrepareAttemptUtc = DateTime.MinValue;
        private readonly ConcurrentDictionary<string, byte> playlistVideoCachesInProgress = new ConcurrentDictionary<string, byte>();

        [Resolved]
        private AXPlayerConfigManager appGlobalConfig { get; set; }

        private string srv3Contents;

        //we need to download subtitles to preload subtitles
        private async void downloadSubtitles()
        {
            if (string.IsNullOrEmpty(videoUrl))
                return;

            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                string args = $"--sub-langs all --write-subs --skip-download --force-overwrites --sub-format srv3 {videoUrl} -o \"%(id)s.%(ext)s\" -P {app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}")}";

                ProcessStartInfo processStartInfo = new ProcessStartInfo(app.GetYtDlpPath(), args)
                {
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(processStartInfo))
                {
                    await process.WaitForExitAsync();
                }
            } else if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                string args = $"-c \"yt-dlp --sub-langs all --write-subs --skip-download --force-overwrites --sub-format srv3 {videoUrl} -o \"%(id)s.%(ext)s\" -P {app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}")}\"";

                ProcessStartInfo processStartInfo = new ProcessStartInfo("bash", args)
                {
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(processStartInfo))
                {
                    await process.WaitForExitAsync();
                }
            }
        }

        public void SetVideoSource(string videoId, bool clearCache = false, LoadType loadType = LoadType.Full)
        {
            cancelSmartCrossfade();
            srv3Contents = string.Empty;
            videoIdBox.Text = string.Empty;

            if (resetPlaybackSpeedWhenLoadingAVideo.Value)
            {
                if (playbackSpeed.Value != 1)
                {
                    Schedule(() =>
                    {
                        playbackSpeed.Value = 1;
                        osd.Display(new SpeedChangeToast(playbackSpeed.Value));
                    });
                }
            }

            if (loadVideoContainer.IsVisible == true)
            {
                Schedule(() => hideOverlayContainer(loadVideoContainer));
            }
            if (searchContainer.IsVisible == true)
            {
                Schedule(() => hideOverlayContainer(searchContainer));
            }
            if (playlistOverlay.IsVisible == true)
            {
                Schedule(() => hideOverlayContainer(playlistOverlay));
            }

            if (isVideoLoading)
                return;

            videoLoadProcess?.Cancel();

            videoLoadProcess = new CancellationTokenSource();
            CancellationToken cancellationToken = videoLoadProcess.Token;
            Task.Run(() =>
            {
                isVideoLoading = true;
                Schedule(async () =>
                {
                    loadBtnOverlayShow.Enabled.Value = false;
                    if (string.IsNullOrEmpty(videoId))
                    {
                        Schedule(() =>
                        {
                            //notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.MinusCircle, Color4.Red, AXPlayerStrings.NoVideoIdError, AXPlayerStrings.General));
                            newToastOverlay.ShowToast(AXPlayerStrings.NoVideoIdError);
                        });
                        /*
                        ToastBase toast = new ToastWithIcon(NekoPlayerStrings.General, NekoPlayerStrings.NoVideoIdError, FontAwesome.Solid.MinusCircle);

                        onScreenDisplay.Display(toast);
                        */
                        return;
                    }

                    try
                    {
                        YoutubeExplode.Videos.VideoId.Parse(videoId);
                    }
                    catch (Exception e)
                    {
                        return;
                    }
                    this.videoId = YoutubeExplode.Videos.VideoId.Parse(videoId);
                    _ = getOrStartLiveChatReplayPreload(this.videoId);
                    /*
                    resetLiveChatReplay();

                    if (liveChatReplayOverlay != null && liveChatReplayOverlay.IsVisible)
                        loadLiveChatReplay();
                    */
                    //ClearPlaylistItems();
                    if (currentVideoSource != null)
                        currentVideoSource.OnUpdateAction = null;

                    pausedTime = clearCache ? currentVideoSource.VideoProgress.Value : 0;
                    Schedule(() => currentVideoSource?.Expire());
                    commentsSort.UnbindEvents();

                    if (playlists.Count > 0)
                    {
                        if (playlistItemIndex == playlists.Count - 1)
                        {
                            Schedule(() => nextVideoButton.Enabled.Value = false);
                        }
                        else
                        {
                            Schedule(() => nextVideoButton.Enabled.Value = true);
                        }

                        if (playlistItemIndex == 0)
                        {
                            Schedule(() => prevVideoButton.Enabled.Value = false);
                        }
                        else
                        {
                            Schedule(() => prevVideoButton.Enabled.Value = true);
                        }
                    }
                    else
                    {
                        Schedule(() => prevVideoButton.Enabled.Value = false);
                        Schedule(() => nextVideoButton.Enabled.Value = false);
                    }

                    if (playlistItemViews.Count > 0)
                    {
                        foreach (PlaylistItemView playlistItemView in playlistItemViews)
                        {
                            playlistItemView.UpdateState(false);
                        }

                        playlistItemViews[playlistItemIndex].UpdateState(true);
                    }

                    foreach (var item in commentContainer.Children)
                    {
                        Schedule(() => item.Expire());
                    }

                    Schedule(() => videoProgress.Value = 0);

                    if (clearCache == true)
                    {
                        await Task.Delay(1000); // Wait for any ongoing operations to complete
                        switch (loadType)
                        {
                            case LoadType.Full:
                            {
                                foreach (var cacheItem in Directory.GetFiles(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}")))
                                {
                                    File.Delete(cacheItem);
                                }
                                break;
                            }
                            case LoadType.VideoOnly:
                            {
                                File.Delete(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/video.webm");
                                break;
                            }
                            case LoadType.AudioOnly:
                            {
                                File.Delete(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg");
                                break;
                            }
                        }
                    }

                    if (videoId.Length != 0)
                    {
                        Google.Apis.YouTube.v3.Data.Video videoData = api.GetVideo(this.videoId);

                        videoLoadingProgressBase.Show();
                        videoLoadingProgress.Text = AXPlayerStrings.Preparing;
                        IProgress<double> audioDownloadProgress = new Progress<double>((percent) => Schedule(() => videoLoadingProgress.Text = AXPlayerStrings.DownloadingAudioStream($"{(percent * 100):N0}%")));
                        IProgress<double> videoDownloadProgress = new Progress<double>((percent) => Schedule(() => videoLoadingProgress.Text = AXPlayerStrings.DownloadingVideoStream($"{(percent * 100):N0}%")));

                        //spinnerShow = Scheduler.AddDelayed(spinner.Show, 0);

                        Schedule(() => videoProgress.MaxValue = 1);

                        if (AXPlayerDescriptionParser.IsYouTubeVideo(videoId))
                        {
                            if (timestampInURL(videoId))
                                videoUrl = videoId;
                            else
                                videoUrl = $"https://youtube.com/watch?v={this.videoId}";
                        }
                        else
                        {
                            videoUrl = $"https://youtube.com/watch?v={this.videoId}";
                        }

                        copyVideoUrlButton.Enabled.Value = true;

                        downloadSubtitles();

                        if (loadType == LoadType.Full)
                        {
                            Schedule(() => updateVideoMetadata(this.videoId));
                        }
                        Schedule(() => thumbnailContainer.Show());

                        try
                        {
                            if (loadType == LoadType.Full)
                                await settingsContainer.AudioQualitySettings.RefreshAudioStreamList(videoUrl);
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, e.GetDescription());
                        }

                        try
                        {
                            await settingsContainer.VideoQualitySettings.RefreshQualityList(videoUrl);
                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, e.GetDescription());
                        }

                        settingsContainer.VideoQualitySettingsCore.Show();
                        settingsContainer.AudioQualitySettingsCore.Show();

                        settingsContainer.CaptionLangOptionsEnabled.Value = true;

                        //bool videoCached = !File.Exists(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg") || !File.Exists(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/video.webm");

                        isVideoLoading = true;
                        if (true) // always download cache
                        {
                            if (loadType == LoadType.Full)
                                Directory.CreateDirectory(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}"));

                            StreamManifest streamManifest;

                            try
                            {
                                streamManifest = await app.YouTubeClient.Videos.Streams.GetManifestAsync(videoUrl);
                            }
                            catch (Exception e)
                            {
                                Schedule(() =>
                                {
                                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.MinusCircle, Color4.Red, e.Message, AXPlayerStrings.General));
                                });
                                Schedule(() => thumbnailContainer.Hide());
                                isVideoLoading = false;
                                loadBtnOverlayShow.Enabled.Value = true;
                                Schedule(() => videoLoadingProgressBase.Hide());
                                return;
                            }

                            IAudioStreamInfo audioStreamInfo;

                            try
                            {
                                if (audioQuality.Value == Config.AudioQuality.PreferHighQuality)
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                                else if (audioQuality.Value == Config.AudioQuality.PreferMp4a)
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                                else if (audioQuality.Value == Config.AudioQuality.PreferOpus)
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                                else
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .First();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .First();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    /*
                                    // Select best audio stream (highest bitrate)
                                    audioStreamInfo = streamManifest
                                        .GetAudioOnlyStreams()
                                        .Where(s => s.IsAudioLanguageDefault == true)
                                        .TryGetWithHighestBitrate();
                                    */

                                    if (audioQuality.Value == Config.AudioQuality.PreferHighQuality)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferMp4a)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferOpus)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .First();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }

                                    Logger.Error(e, e.GetDescription());
                                    Logger.Log($"Prefer default audio language: {videoData.Snippet.DefaultLanguage}");
                                }
                                catch
                                {
                                    Logger.Log($"Prefer default audio language failed.\nFalling back to default audio language.");
                                    // Select best audio stream (highest bitrate)
                                    /*
                                    audioStreamInfo = streamManifest
                                        .GetAudioOnlyStreams()
                                        .TryGetWithHighestBitrate();
                                    */

                                    if (audioQuality.Value == Config.AudioQuality.PreferHighQuality)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferMp4a)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferOpus)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .First();
                                        //settingsContainer.AudioQualitySettings.Caption = NekoPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                            }

                            IVideoStreamInfo videoStreamInfo;

                            try
                            {
                                // Select best video stream (1080p60 in this example)
                                videoStreamInfo = streamManifest
                                    .GetVideoOnlyStreams()
                                    .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
                                    .Where(s => s.VideoQuality.Label.Contains(settingsContainer.VideoQualitySettings.Current.Value))
                                    .First();

                                Schedule(() =>
                                {
                                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Video, Color4.Green, videoStreamInfo.VideoQuality.Label, AXPlayerStrings.VideoQuality, true));
                                });
                                settingsContainer.VideoQualitySettings.Caption = AXPlayerStrings.VideoQualityWithLabel($"{videoStreamInfo.VideoQuality.Label}, {videoStreamInfo.VideoCodec}, {videoStreamInfo.VideoQuality.Framerate}fps");
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    Logger.Error(e, e.GetDescription());
                                    // Select best video stream (1080p60 in this example)
                                    videoStreamInfo = streamManifest
                                        .GetVideoOnlyStreams()
                                        .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
                                        .TryGetWithHighestVideoQuality();

                                    Schedule(() =>
                                    {
                                        notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Video, Color4.Green, videoStreamInfo.VideoQuality.Label, AXPlayerStrings.VideoQuality, true));
                                    });
                                    settingsContainer.VideoQualitySettings.Caption = AXPlayerStrings.VideoQualityWithLabel($"{videoStreamInfo.VideoQuality.Label}, {videoStreamInfo.VideoCodec}, {videoStreamInfo.VideoQuality.Framerate}fps");
                                }
                                catch (Exception e2)
                                {
                                    Logger.Error(e2, e2.GetDescription());
                                    // Select best video stream (1080p60 in this example)
                                    videoStreamInfo = streamManifest
                                        .GetVideoOnlyStreams()
                                        .Where(s => s.VideoQuality.Label.Contains(settingsContainer.VideoQualitySettings.Current.Value))
                                        .First();

                                    Schedule(() =>
                                    {
                                        notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Video, Color4.Green, videoStreamInfo.VideoQuality.Label, AXPlayerStrings.VideoQuality, true));
                                    });
                                    settingsContainer.VideoQualitySettings.Caption = AXPlayerStrings.VideoQualityWithLabel($"{videoStreamInfo.VideoQuality.Label}, {videoStreamInfo.VideoCodec}, {videoStreamInfo.VideoQuality.Framerate}fps");
                                }
                            }

                            if (videoStreamInfo.VideoCodec.Contains("avc1"))
                            {
                                videoFile = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/video.mp4";
                            }
                            else
                            {
                                videoFile = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/video.webm";
                            }

                            try
                            {
                                await settingsContainer.CaptionLangDropdown.RefreshCaptionLanguages(videoUrl);
                                captionEnabled.Disabled = false;
                                Schedule(() => captionButton.Enabled.Value = true);
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, e.GetDescription());
                            }

                            ClosedCaptionTrack captionTrack = null;

                            audioStreamInfo = settingsContainer.AudioQualitySettings.Current.Value;

                            try
                            {
                                if (captionEnabled.Value)
                                {
                                    var trackManifest = await game.YouTubeClient.Videos.ClosedCaptions.GetManifestAsync(videoUrl);

                                    var trackLists = trackManifest.Tracks;

                                    if (trackLists.Count == 0)
                                    {
                                        captionEnabled.Value = false;
                                        captionEnabled.Disabled = true;
                                        Schedule(() => captionButton.Enabled.Value = false);
                                    }

                                    string preferedLang = string.Empty;

                                    if (settingsContainer.CaptionLangDropdown.Current.Value != null)
                                    {
                                        preferedLang = settingsContainer.CaptionLangDropdown.Current.Value.Hl.ToString();
                                    }
                                    else
                                    {
                                        preferedLang = CultureInfo.CurrentCulture.Name;
                                    }

                                    settingsContainer.CaptionLangDropdown.Current.Value = settingsContainer.CaptionLangDropdown.Items.Where(lang => lang.Hl.Contains(preferedLang)).First();

                                    var trackInfo = trackLists.Where(track => track.Language.Code.Contains(preferedLang)).First();

                                    if (trackInfo != null)
                                    {
                                        captionTrack = await game.YouTubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                                        if (captionEnabled.Value)
                                        {
                                            Schedule(() =>
                                            {
                                                /*
                                                alert.Text = captionLanguage.Value != ClosedCaptionLanguage.Disabled ? (trackInfo.IsAutoGenerated ? NekoPlayerStrings.SelectedCaptionAutoGen(captionLanguage.Value.GetLocalisableDescription()) : NekoPlayerStrings.SelectedCaption(captionLanguage.Value.GetLocalisableDescription())) : NekoPlayerStrings.SelectedCaption(captionLanguage.Value.GetLocalisableDescription());
                                                alert.Show();
                                                spinnerShow = Scheduler.AddDelayed(alert.Hide, 3000);
                                                */

                                                try
                                                {
                                                    ToastBase toast = new Toast(AXPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name);
                                                    onScreenDisplay.Display(toast);
                                                }
                                                catch (Exception e)
                                                {
                                                    Logger.Error(e, e.GetDescription());
                                                }
                                            });
                                        }

                                        if (!trackInfo.IsAutoGenerated && File.Exists(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}") + @$"/{this.videoId}.{trackInfo.Language.Code}.srv3"))
                                            srv3Contents = File.ReadAllText(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}") + @$"/{this.videoId}.{trackInfo.Language.Code}.srv3");
                                    }
                                }
                                else
                                {
                                    srv3Contents = string.Empty;
                                    currentVideoSource?.UpdateCaptionTrack(null, srv3Contents);
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, e.GetDescription());
                            }

                            switch (loadType)
                            {
                                case LoadType.Full:
                                {
                                    await app.YouTubeClient.Videos.DownloadAsync([audioStreamInfo], new ConversionRequestBuilder(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg").SetFFmpegPath(app.GetFFmpegPath()).Build(), audioDownloadProgress);
                                    await app.YouTubeClient.Videos.DownloadAsync([videoStreamInfo], new ConversionRequestBuilder(videoFile).SetFFmpegPath(app.GetFFmpegPath()).Build(), videoDownloadProgress);
                                    break;
                                }
                                case LoadType.AudioOnly:
                                {
                                    await app.YouTubeClient.Videos.DownloadAsync([audioStreamInfo], new ConversionRequestBuilder(app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg").SetFFmpegPath(app.GetFFmpegPath()).Build(), audioDownloadProgress);
                                    break;
                                }
                                case LoadType.VideoOnly:
                                {
                                    await app.YouTubeClient.Videos.DownloadAsync([videoStreamInfo], new ConversionRequestBuilder(videoFile).SetFFmpegPath(app.GetFFmpegPath()).Build(), videoDownloadProgress);
                                    break;
                                }
                            }

                            currentVideoSource = new YouTubeVideoPlayer(videoFile, app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg", captionTrack, srv3Contents, videoData, pausedTime, preloadedLiveChatReplay: getOrStartLiveChatReplayPreload(this.videoId))
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            };

                            //spinnerShow = Scheduler.AddDelayed(spinner.Hide, 0);

                            spinnerShow = Scheduler.AddDelayed(addVideoToScreen, 0);

                            spinnerShow = Scheduler.AddDelayed(() => playVideo(), 0);
                            Schedule(() => thumbnailContainer.Hide());
                            isVideoLoading = false;
                            loadBtnOverlayShow.Enabled.Value = true;
                        }
                        else
                        {
                            await settingsContainer.CaptionLangDropdown.RefreshCaptionLanguages(videoUrl);
                            captionEnabled.Disabled = false;
                            Schedule(() => captionButton.Enabled.Value = true);

                            StreamManifest streamManifest;

                            try
                            {
                                streamManifest = await app.YouTubeClient.Videos.Streams.GetManifestAsync(videoUrl);
                            }
                            catch (Exception e)
                            {
                                Schedule(() =>
                                {
                                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.MinusCircle, Color4.Red, e.Message, AXPlayerStrings.General));
                                });
                                Schedule(() => thumbnailContainer.Hide());
                                isVideoLoading = false;
                                loadBtnOverlayShow.Enabled.Value = true;
                                Schedule(() => videoLoadingProgressBase.Hide());
                                return;
                            }

                            IAudioStreamInfo audioStreamInfo;

                            try
                            {
                                if (audioQuality.Value == Config.AudioQuality.PreferHighQuality)
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                                else if (audioQuality.Value == Config.AudioQuality.PreferMp4a)
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                                else if (audioQuality.Value == Config.AudioQuality.PreferOpus)
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                                else
                                {
                                    if (alwaysUseOriginalAudio.Value == true)
                                    {
                                        Logger.Log($"Preferred audio language is: {videoData.Snippet.DefaultLanguage}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .First();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        Logger.Log($"Preferred audio language is: {appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()}");
                                        // Select best audio stream (highest bitrate)
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioLanguage.Value.Code.Contains(appGlobalConfig.Get<Language>(AXPlayerSetting.AudioLanguage).ToString()))
                                            .First();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    /*
                                    // Select best audio stream (highest bitrate)
                                    audioStreamInfo = streamManifest
                                        .GetAudioOnlyStreams()
                                        .Where(s => s.IsAudioLanguageDefault == true)
                                        .TryGetWithHighestBitrate();
                                    */

                                    if (audioQuality.Value == Config.AudioQuality.PreferHighQuality)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferMp4a)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferOpus)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.IsAudioLanguageDefault == true)
                                            .First();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }

                                    Logger.Error(e, e.GetDescription());
                                    Logger.Log($"Prefer default audio language: {videoData.Snippet.DefaultLanguage}");
                                }
                                catch
                                {
                                    Logger.Log($"Prefer default audio language failed.\nFalling back to default audio language.");
                                    // Select best audio stream (highest bitrate)
                                    /*
                                    audioStreamInfo = streamManifest
                                        .GetAudioOnlyStreams()
                                        .TryGetWithHighestBitrate();
                                    */

                                    if (audioQuality.Value == Config.AudioQuality.PreferHighQuality)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferMp4a)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioCodec.Contains("mp4a"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else if (audioQuality.Value == Config.AudioQuality.PreferOpus)
                                    {
                                        audioStreamInfo = (IAudioStreamInfo)streamManifest
                                            .GetAudioOnlyStreams()
                                            .Where(s => s.AudioCodec.Contains("opus"))
                                            .TryGetWithHighestBitrate();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                    else
                                    {
                                        audioStreamInfo = streamManifest
                                            .GetAudioOnlyStreams()
                                            .First();
                                        settingsContainer.AudioQualitySettings.Caption = AXPlayerStrings.AudioQualityWithLabel($"{audioStreamInfo.AudioCodec}, {audioStreamInfo.Bitrate.KiloBitsPerSecond:N0}kbps");
                                    }
                                }
                            }

                            IVideoStreamInfo videoStreamInfo;

                            try
                            {
                                // Select best video stream (1080p60 in this example)
                                videoStreamInfo = streamManifest
                                    .GetVideoOnlyStreams()
                                    .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
                                    .Where(s => s.VideoQuality.Label.Contains(settingsContainer.VideoQualitySettings.Current.Value))
                                    .First();

                                Schedule(() =>
                                {
                                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Video, Color4.Green, videoStreamInfo.VideoQuality.Label, AXPlayerStrings.VideoQuality, true));
                                });
                                settingsContainer.VideoQualitySettings.Caption = AXPlayerStrings.VideoQualityWithLabel($"{videoStreamInfo.VideoQuality.Label}, {videoStreamInfo.VideoCodec}, {videoStreamInfo.VideoQuality.Framerate}fps");
                            }
                            catch (Exception e)
                            {
                                try
                                {
                                    Logger.Error(e, e.GetDescription());
                                    // Select best video stream (1080p60 in this example)
                                    videoStreamInfo = streamManifest
                                        .GetVideoOnlyStreams()
                                        .Where(s => s.Container == YoutubeExplode.Videos.Streams.Container.WebM)
                                        .TryGetWithHighestVideoQuality();

                                    Schedule(() =>
                                    {
                                        notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Video, Color4.Green, videoStreamInfo.VideoQuality.Label, AXPlayerStrings.VideoQuality, true));
                                    });
                                    settingsContainer.VideoQualitySettings.Caption = AXPlayerStrings.VideoQualityWithLabel($"{videoStreamInfo.VideoQuality.Label}, {videoStreamInfo.VideoCodec}, {videoStreamInfo.VideoQuality.Framerate}fps");
                                }
                                catch (Exception e2)
                                {
                                    Logger.Error(e2, e2.GetDescription());
                                    // Select best video stream (1080p60 in this example)
                                    videoStreamInfo = streamManifest
                                        .GetVideoOnlyStreams()
                                        .Where(s => s.VideoQuality.Label.Contains(settingsContainer.VideoQualitySettings.Current.Value))
                                        .First();

                                    Schedule(() =>
                                    {
                                        notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.Video, Color4.Green, videoStreamInfo.VideoQuality.Label, AXPlayerStrings.VideoQuality, true));
                                    });
                                    settingsContainer.VideoQualitySettings.Caption = AXPlayerStrings.VideoQualityWithLabel($"{videoStreamInfo.VideoQuality.Label}, {videoStreamInfo.VideoCodec}, {videoStreamInfo.VideoQuality.Framerate}fps");
                                }
                            }

                            ClosedCaptionTrack captionTrack = null;

                            audioStreamInfo = settingsContainer.AudioQualitySettings.Current.Value;

                            try
                            {
                                if (captionEnabled.Value)
                                {
                                    var trackManifest = await game.YouTubeClient.Videos.ClosedCaptions.GetManifestAsync(videoUrl);

                                    var trackLists = trackManifest.Tracks;

                                    if (trackLists.Count == 0)
                                    {
                                        captionEnabled.Value = false;
                                        captionEnabled.Disabled = true;
                                        Schedule(() => captionButton.Enabled.Value = false);
                                    }

                                    string preferedLang = string.Empty;

                                    if (settingsContainer.CaptionLangDropdown.Current.Value != null)
                                    {
                                        preferedLang = settingsContainer.CaptionLangDropdown.Current.Value.Hl.ToString();
                                    }
                                    else
                                    {
                                        preferedLang = CultureInfo.CurrentCulture.Name;
                                    }

                                    settingsContainer.CaptionLangDropdown.Current.Value = settingsContainer.CaptionLangDropdown.Items.Where(lang => lang.Hl.Contains(preferedLang)).First();

                                    var trackInfo = trackLists.Where(track => track.Language.Code.Contains(preferedLang)).First();

                                    if (trackInfo != null)
                                    {
                                        captionTrack = await game.YouTubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                                        if (captionEnabled.Value)
                                        {
                                            Schedule(() =>
                                            {
                                                try
                                                {
                                                    ToastBase toast = new Toast(AXPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name);
                                                    onScreenDisplay.Display(toast);
                                                }
                                                catch (Exception e)
                                                {
                                                    Logger.Error(e, e.GetDescription());
                                                }
                                            });
                                        }

                                        if (!trackInfo.IsAutoGenerated && File.Exists(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}") + @$"/{this.videoId}.{trackInfo.Language.Code}.srv3"))
                                            srv3Contents = File.ReadAllText(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}") + @$"/{this.videoId}.{trackInfo.Language.Code}.srv3");
                                    }
                                }
                                else
                                {
                                    srv3Contents = string.Empty;
                                    currentVideoSource?.UpdateCaptionTrack(null, srv3Contents);
                                }
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e, e.GetDescription());
                            }

                            if (videoStreamInfo.VideoCodec.Contains("avc1"))
                            {
                                videoFile = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/video.mp4";
                            }
                            else
                            {
                                videoFile = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/video.webm";
                            }

                            currentVideoSource = new YouTubeVideoPlayer(videoFile, app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg", captionTrack, srv3Contents, videoData, pausedTime, preloadedLiveChatReplay: getOrStartLiveChatReplayPreload(this.videoId))
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                            };

                            //spinnerShow = Scheduler.AddDelayed(spinner.Hide, 0);

                            spinnerShow = Scheduler.AddDelayed(addVideoToScreen, 0);

                            spinnerShow = Scheduler.AddDelayed(() => playVideo(), 0);
                            Schedule(() => thumbnailContainer.Hide());
                            isVideoLoading = false;
                            loadBtnOverlayShow.Enabled.Value = true;
                        }
                    }
                    else
                    {
                        /*
                        Toast toast = new Toast(NekoPlayerStrings.General, NekoPlayerStrings.NoVideoIdError);

                        onScreenDisplay.Display(toast);
                        */
                    }
                });
            }, cancellationToken).FireAndForget();
        }

        [Resolved]
        private OnScreenDisplay onScreenDisplay { get; set; }

        [Resolved]
        private AXPlayerApp game { get; set; }

        public void ShowSettingsOverlayAtName(string name)
        {
            if (!settingsContainer.IsVisible)
                showOverlayContainer(settingsContainer);

            settingsContainer.ShowSettingsOverlayAtName(name);
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public void OpenSettings()
        {
            Schedule(() =>
            {
                hideOverlays();
                showOverlayContainer(settingsContainer);
            });
        }

        public void TogglePreservePitch()
        {
            Schedule(() => adjustPitch.Value = !adjustPitch.Value);
        }

        public void SelectPlaylist(string id)
        {
            Task.Run(async () => SetPlaylist(id));
        }

        public void OpenMyPlaylists()
        {
            Schedule(() =>
            {
                hideOverlays();
                showOverlayContainer(myPlaylistsOverlay);
            });
        }

        public void OpenAudioEffects()
        {
            Schedule(() =>
            {
                hideOverlays();
                showOverlayContainer(audioEffectsOverlay);
            });
        }

        public void SelectVideo(string id)
        {
            Schedule(() => hideOverlays());
            ClearPlaylistItems();
            Task.Run(async () =>
            {
                Schedule(async () =>
                {
                    SetVideoSource(id);
                });
            });
        }

        public enum LoadType
        {
            Full,
            VideoOnly,
            AudioOnly,
        }
    }
}

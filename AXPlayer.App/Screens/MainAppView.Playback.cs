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

            if (timestampInURL(videoUrl))
                Schedule(() => seekTo(parseTimestampFromURL(videoUrl)));

            configureVideoCompleted(currentVideoSource);
            currentVideoSource.OnUpdateAction = updatePlaylistCrossfade;

            updatePresence(discordRichPresence.Value);
        }

        private void configureVideoCompleted(YouTubeVideoPlayer source)
        {
            source.OnVideoCompleted = () =>
            {
                if (crossfadeOutgoing == source && crossfadeIncoming != null)
                {
                    if (repeat.Value)
                    {
                        cancelCrossfadeCandidate();
                    }
                    else if (crossfadeIncoming.IsReady && crossfadeStarted)
                    {
                        finishCrossfade();
                        return;
                    }
                    else
                    {
                        cancelCrossfadeCandidate();
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

        private void updatePlaylistCrossfade()
        {
            bool crossfadeEnabled = appGlobalConfig.Get<bool>(AXPlayerSetting.PlaylistCrossfadeEnabled);
            double configuredDuration = Math.Clamp(appGlobalConfig.Get<float>(AXPlayerSetting.PlaylistCrossfadeDuration), 1f, 10f);

            if (!crossfadeEnabled || playlists == null || repeat.Value || currentVideoSource == null || playlistItemIndex >= playlists.Count - 1)
            {
                cancelCrossfadeCandidate();
                return;
            }

            double remaining = currentVideoSource.VideoProgress.MaxValue - currentVideoSource.VideoProgress.Value;
            double prepareTime = configuredDuration + crossfade_prepare_buffer_seconds;

            if (crossfadeIncoming == null && !crossfadeAttempted && remaining <= prepareTime && remaining > 0)
                prepareCrossfadeCandidate();

            if (crossfadeIncoming == null)
                return;

            if (!crossfadeStarted && currentVideoSource.IsPlaying() && remaining <= configuredDuration)
            {
                if (!crossfadeIncoming.IsReady)
                    return;

                crossfadeStarted = true;
                crossfadeDurationSeconds = configuredDuration;
                crossfadeIncoming.Play();
                cacheFollowingPlaylistVideo();
            }

            if (!crossfadeStarted)
            {
                if (remaining > prepareTime)
                    cancelCrossfadeCandidate();

                return;
            }

            if (currentVideoSource.IsPlaying())
            {
                YouTubeVideoPlayer inactiveSource = crossfadeSourceSwitched ? crossfadeOutgoing : crossfadeIncoming;
                if (!inactiveSource.IsPlaying())
                    inactiveSource.Play();

                if (!crossfadeSourceSwitched && !crossfadeIncoming.IsPlaying())
                    crossfadeIncoming.Play();

                double outgoingRemaining = crossfadeOutgoing.VideoProgress.MaxValue - crossfadeOutgoing.VideoProgress.Value;
                double progress = Math.Clamp(1 - outgoingRemaining / crossfadeDurationSeconds, 0, 1);
                double easedProgress = progress * progress * progress * (progress * (progress * 6 - 15) + 10);
                double fadeAngle = easedProgress * Math.PI / 2;
                crossfadeOutgoing.Alpha = (float)(1 - easedProgress);
                crossfadeOutgoing.Scale = new osuTK.Vector2((float)(1 - easedProgress * 0.035));
                crossfadeOutgoing.SetTransitionVolume(Math.Cos(fadeAngle));
                crossfadeIncoming.Alpha = (float)easedProgress;
                crossfadeIncoming.Scale = new osuTK.Vector2((float)(1.045 - easedProgress * 0.045));
                crossfadeIncoming.SetTransitionVolume(Math.Sin(fadeAngle));

                if (!crossfadeSourceSwitched && progress >= 0.5)
                    switchCrossfadeSource();

                if (progress >= 1)
                    finishCrossfade();
            }
            else
            {
                if (crossfadeOutgoing.IsPlaying())
                    crossfadeOutgoing.Pause();
                if (crossfadeIncoming.IsPlaying())
                    crossfadeIncoming.Pause();
            }
        }

        private void prepareCrossfadeCandidate()
        {
            crossfadeAttempted = true;

            try
            {
                string nextVideoId = playlists[playlistItemIndex + 1].Snippet.ResourceId.VideoId;
                string videoDirectory = app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath(nextVideoId);
                string audioFile = Path.Combine(videoDirectory, "audio.ogg");
                string mp4File = Path.Combine(videoDirectory, "video.mp4");
                string webmFile = Path.Combine(videoDirectory, "video.webm");
                string cachedVideoFile = File.Exists(mp4File) ? mp4File : File.Exists(webmFile) ? webmFile : null;

                bool videoCached = cachedVideoFile != null && File.Exists(audioFile);
                if (!videoCached)
                    return;

                crossfadeVideoData = api.GetVideo(nextVideoId);
                crossfadeVideoId = nextVideoId;
                crossfadeOutgoing = currentVideoSource;
                crossfadeIncoming = new YouTubeVideoPlayer(cachedVideoFile, audioFile, null, string.Empty, crossfadeVideoData, 0, 0, true, false)
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Scale = new osuTK.Vector2(1.045f),
                    OnSeekAction = () => updatePresence(discordRichPresence.Value),
                };
                crossfadeStarted = false;
                crossfadeSourceSwitched = false;
                videoContainer.Add(crossfadeIncoming);
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
                cancelCrossfadeCandidate();
            }
        }

        private void cacheFollowingPlaylistVideo()
        {
            int followingIndex = playlistItemIndex + 2;
            if (playlists == null || followingIndex >= playlists.Count)
                return;

            string followingVideoId = playlists[followingIndex].Snippet.ResourceId.VideoId;
            if (string.IsNullOrEmpty(followingVideoId) || !playlistVideoCachesInProgress.TryAdd(followingVideoId, 0))
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
                    await cachePlaylistVideoAsync(followingVideoId, preferredVideoQuality, preferredAudioQuality, useOriginalAudio, preferredAudioLanguage, ffmpegPath);
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
                finally
                {
                    playlistVideoCachesInProgress.TryRemove(followingVideoId, out _);
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

        private void finishCrossfade()
        {
            if (crossfadeIncoming == null || crossfadeOutgoing == null)
                return;

            if (!crossfadeSourceSwitched)
                switchCrossfadeSource();

            crossfadeIncoming.Alpha = 1;
            crossfadeIncoming.Scale = new osuTK.Vector2(1);
            crossfadeIncoming.SetTransitionVolume(1);
            crossfadeOutgoing.SetTransitionVolume(0);
            crossfadeOutgoing.Expire();

            if (playlistItemViews.Count > 0)
            {
                foreach (PlaylistItemView playlistItemView in playlistItemViews)
                {
                    playlistItemView.UpdateState(false);
                }

                playlistItemViews[playlistItemIndex].UpdateState(true);
            }

            crossfadeOutgoing = null;
            crossfadeIncoming = null;
            crossfadeVideoId = string.Empty;
            crossfadeVideoData = null;
            crossfadeStarted = false;
            crossfadeSourceSwitched = false;
            crossfadeAttempted = false;
        }

        private void switchCrossfadeSource()
        {
            if (crossfadeIncoming == null || crossfadeOutgoing == null || crossfadeSourceSwitched)
                return;

            currentVideoSource = crossfadeIncoming;
            videoId = crossfadeVideoId;
            videoData = crossfadeVideoData;
            playlistItemIndex++;
            configureVideoCompleted(crossfadeIncoming);
            crossfadeIncoming.OnUpdateAction = updatePlaylistCrossfade;
            crossfadeOutgoing.OnUpdateAction = null;
            crossfadeOutgoing.DeactivateMediaSession();
            crossfadeIncoming.ActivateMediaSession();
            crossfadeSourceSwitched = true;

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

        private void cancelCrossfadeCandidate()
        {
            if (crossfadeIncoming != null)
                crossfadeIncoming.Expire();

            if (crossfadeOutgoing != null)
            {
                crossfadeOutgoing.Alpha = 1;
                crossfadeOutgoing.Scale = new osuTK.Vector2(1);
                crossfadeOutgoing.SetTransitionVolume(1);
                if (crossfadeSourceSwitched)
                    crossfadeOutgoing.OnUpdateAction = null;
            }

            crossfadeOutgoing = null;
            crossfadeIncoming = null;
            crossfadeVideoId = string.Empty;
            crossfadeVideoData = null;
            crossfadeStarted = false;
            crossfadeSourceSwitched = false;
            crossfadeAttempted = false;
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

        private const double crossfade_prepare_buffer_seconds = 3;
        private YouTubeVideoPlayer crossfadeOutgoing;
        private YouTubeVideoPlayer crossfadeIncoming;
        private string crossfadeVideoId = string.Empty;
        private Google.Apis.YouTube.v3.Data.Video crossfadeVideoData;
        private double crossfadeDurationSeconds = 2;
        private bool crossfadeStarted;
        private bool crossfadeSourceSwitched;
        private bool crossfadeAttempted;
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
            cancelCrossfadeCandidate();
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

                            currentVideoSource = new YouTubeVideoPlayer(videoFile, app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg", captionTrack, srv3Contents, videoData, pausedTime)
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

                            currentVideoSource = new YouTubeVideoPlayer(videoFile, app.Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath($"{this.videoId}") + @"/audio.ogg", captionTrack, srv3Contents, videoData, pausedTime)
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

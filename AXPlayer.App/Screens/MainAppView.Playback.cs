// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
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

            if (timestampInURL(videoUrl))
                Schedule(() => seekTo(parseTimestampFromURL(videoUrl)));

            if (playlists.Count > 0)
            {
                currentVideoSource.OnVideoCompleted = async () =>
                {
                    if (repeat.Value)
                    {
                        currentVideoSource.Play();
                        return;
                    }

                    if (playlistItemIndex != playlists.Count - 1)
                        playlistItemIndex++;

                    Schedule(async () =>
                    {
                        SetVideoSource(playlists[playlistItemIndex].Snippet.ResourceId.VideoId);
                    });
                };
            }
            else
            {
                currentVideoSource.OnVideoCompleted = async () =>
                {
                    if (!repeat.Value)
                        return;

                    currentVideoSource.Play();
                };
            }

            updatePresence(discordRichPresence.Value);
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

        [Resolved]
        private AXPlayerConfigManager appGlobalConfig { get; set; }

        private string srv3Contents;

        //we need to download subtitles to preload subtitles
        private async void downloadSubtitles()
        {
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
                return; //crashs on appimage state

            if (string.IsNullOrEmpty(videoUrl))
                return;

            string args = $"--sub-langs all --write-subs --skip-download --force-overwrites --sub-format srv3 {videoUrl} -o \"%(id)s.%(ext)s\" -P {app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}")}";

            ProcessStartInfo processStartInfo = new ProcessStartInfo(app.GetYtDlpPath(), args)
            {
                CreateNoWindow = true
            };

            using (Process process = Process.Start(processStartInfo))
            {
                await process.WaitForExitAsync();
            }
        }

        public void SetVideoSource(string videoId, bool clearCache = false, LoadType loadType = LoadType.Full)
        {
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
                            notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.MinusCircle, Color4.Red, AXPlayerStrings.NoVideoIdError, AXPlayerStrings.General));
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
                                                    ToastBase toast = new ToastWithIcon(AXPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name, FontAwesome.Solid.ClosedCaptioning);
                                                    onScreenDisplay.Display(toast);
                                                }
                                                catch (Exception e)
                                                {
                                                    Logger.Error(e, e.GetDescription());
                                                }
                                            });
                                        }

                                        captionTrack = await game.YouTubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                                        if (File.Exists(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}") + @$"/{this.videoId}.{trackInfo.Language.Code}.srv3"))
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
                                RelativeSizeAxes = Axes.Both
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
                                        if (captionEnabled.Value)
                                        {
                                            Schedule(() =>
                                            {
                                                try
                                                {
                                                    ToastBase toast = new ToastWithIcon(AXPlayerStrings.CaptionLanguage, settingsContainer.CaptionLangDropdown.Current.Value.Name, FontAwesome.Solid.ClosedCaptioning);
                                                    onScreenDisplay.Display(toast);
                                                }
                                                catch (Exception e)
                                                {
                                                    Logger.Error(e, e.GetDescription());
                                                }
                                            });
                                        }

                                        captionTrack = await game.YouTubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                                        if (File.Exists(app.Host.CacheStorage.GetStorageForDirectory("subtitleCache").GetFullPath($"{this.videoId}") + @$"/{this.videoId}.{trackInfo.Language.Code}.srv3"))
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
                                RelativeSizeAxes = Axes.Both
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

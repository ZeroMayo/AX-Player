// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Graphics.Videos;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Overlays;
using AXPlayer.App.Overlays.OSD;
using AXPlayer.App.Updater;
using DiscordRPC;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using osuTK.Input;
using static Sentry.MeasurementUnit;
using OverlayContainer = AXPlayer.App.Graphics.Containers.OverlayContainer;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        [Resolved]
        private AXPlayerAppBase app { get; set; }

        [Resolved(canBeNull: true)]
        private UpdateManager updateManager { get; set; }

        private YouTubeVideoPlayer currentVideoSource;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private YouTubeAPI api { get; set; }

        public void Search()
        {
            foreach (var item in searchResultContainer.Children)
            {
                item.Expire();
            }

            Google.Apis.YouTube.v3.SearchResource.ListRequest.OrderEnum orderEnum = Google.Apis.YouTube.v3.SearchResource.ListRequest.OrderEnum.Relevance;

            switch (searchSort.Value)
            {
                case SearchSortCriteria.Date:
                {
                    orderEnum = SearchResource.ListRequest.OrderEnum.Date;
                    break;
                }
                case SearchSortCriteria.Alphabet:
                {
                    orderEnum = SearchResource.ListRequest.OrderEnum.Title;
                    break;
                }
                case SearchSortCriteria.Relevance:
                {
                    orderEnum = SearchResource.ListRequest.OrderEnum.Relevance;
                    break;
                }
            }

            IList<SearchResult> searchResults = api.GetSearchResult(searchTextBox.Text, orderEnum);
            foreach (SearchResult item in searchResults)
            {
                if (item.Id.Kind == "youtube#video")
                {
                    YouTubeSearchResultView trickcal_is_good_game = new YouTubeSearchResultView()
                    {
                        RelativeSizeAxes = Axes.X,
                    };

                    searchResultContainer.Add(trickcal_is_good_game);

                    trickcal_is_good_game.ClickAction = async _ =>
                    {
                        ClearPlaylistItems();
                        Schedule(async () =>
                        {
                            SetVideoSource(item.Id.VideoId);
                        });
                    };

                    trickcal_is_good_game.Enabled.Value = true;

                    trickcal_is_good_game.Data = item;

                    trickcal_is_good_game.UpdateData();
                }
                else if (item.Id.Kind == "youtube#playlist")
                {
                    YouTubeSearchResultView wth = new YouTubeSearchResultView()
                    {
                        RelativeSizeAxes = Axes.X,
                    };

                    searchResultContainer.Add(wth);

                    wth.ClickAction = async _ =>
                    {
                        SetPlaylist(item.Id.PlaylistId).FireAndForget();
                    };

                    wth.Enabled.Value = true;

                    wth.Data = item;

                    wth.UpdateData();
                }
            }
        }

        public string TruncateWithEllipsis(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // If the string is already short enough, return it as-is
            if (value.Length <= maxLength) return value;

            // Ensure we don't get a negative length if maxLength is smaller than the ellipsis
            int truncateLength = Math.Max(0, maxLength - 3);

            return value.Substring(0, truncateLength) + "...";
        }

        private void updatePresence(DiscordRichPresenceMode mode)
        {
            Task.Run(async () =>
            {
                DateTime startTime = DateTime.UtcNow;
                DateTime endTime = DateTime.UtcNow;

                Schedule(() =>
                {
                    if (currentVideoSource != null && videoData != null)
                    {
                        double playbackSpeedValue = playbackSpeed.Value;

                        if (playbackSpeedValue <= 0) playbackSpeedValue = 1.0;

                        TimeSpan realTimePassed = TimeSpan.FromSeconds(currentVideoSource.VideoProgress.Value / playbackSpeedValue);
                        TimeSpan realTimeRemaining = TimeSpan.FromSeconds((XmlConvert.ToTimeSpan(videoData.ContentDetails.Duration).TotalSeconds - currentVideoSource.VideoProgress.Value) / playbackSpeedValue);

                        startTime = DateTime.UtcNow - realTimePassed;
                        endTime = DateTime.UtcNow + realTimeRemaining;
                    }
                });

                Timestamps timestamps = Timestamps.Now;
                ActivityType activityType = ActivityType.Watching;

                string state = AXPlayer_DiscordRPCStrings.WatchingVideo;
                string buttonLabel = AXPlayer_DiscordRPCStrings.WatchOnYouTube;

                /*
                try
                {
                    if (videoData != null)
                    {
                        //state = api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId));
                        if (trayIconVisible.Value)
                        {
                            state = videoData.TopicDetails.TopicCategories.Contains("https://en.wikipedia.org/wiki/Music") ? AXPlayer_DiscordRPCStrings.ListeningMusic : AXPlayer_DiscordRPCStrings.ListeningOnBackground;
                            activityType = ActivityType.Listening;
                        }
                        else
                        {
                            state = videoData.TopicDetails.TopicCategories.Contains("https://en.wikipedia.org/wiki/Music") ? AXPlayer_DiscordRPCStrings.ListeningMusic : AXPlayer_DiscordRPCStrings.WatchingVideo;
                            activityType = videoData.TopicDetails.TopicCategories.Contains("https://en.wikipedia.org/wiki/Music") ? ActivityType.Listening : ActivityType.Watching;
                        }
                        buttonLabel = videoData.TopicDetails.TopicCategories.Contains("https://en.wikipedia.org/wiki/Music") ? AXPlayer_DiscordRPCStrings.ListenOnYouTube : AXPlayer_DiscordRPCStrings.WatchOnYouTube;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                    state = AXPlayer_DiscordRPCStrings.WatchingVideo;
                    activityType = ActivityType.Watching;
                    buttonLabel = AXPlayer_DiscordRPCStrings.WatchOnYouTube;
                }
                */

                switch (mode)
                {
                    case DiscordRichPresenceMode.Full:
                    {
                        if (videoData != null)
                        {
                            if (videoPlaying.Value)
                            {
                                discordRPC?.UpdatePresence(new RichPresence()
                                {
                                    Name = "YouTube",
                                    Type = activityType,
                                    Details = TruncateWithEllipsis(api.GetLocalizedVideoTitle(videoData), 128),
                                    State = TruncateWithEllipsis(api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId)), 128),
                                    StatusDisplay = StatusDisplayType.Details,
                                    StateUrl = $"https://www.youtube.com/channel/{api.GetChannel(videoData.Snippet.ChannelId).Id}",
                                    DetailsUrl = $"https://youtu.be/{videoData.Id}",
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = videoData.Snippet.Thumbnails.High.Url,
                                        LargeImageUrl = $"https://youtu.be/{videoData.Id}",
                                        SmallImageText = "AX Player",
                                        SmallImageKey = "ax_player_new"
                                    },
                                    Timestamps = new Timestamps()
                                    {
                                        Start = startTime,
                                        End = endTime // End 값이 들어가야 프로그레스 바(남은 시간) 형태로 표시됩니다.
                                    },
                                    Buttons =
                                    [
                                        new DiscordRPC.Button
                                        {
                                            Label = buttonLabel,
                                            Url = $"https://youtu.be/{videoData.Id}",
                                        }
                                    ]
                                });
                            }
                            else
                            {
                                discordRPC?.ClearPresence();
                            }
                        }
                        else
                        {
                            discordRPC?.ClearPresence();
                        }
                        break;
                    }
                    case DiscordRichPresenceMode.Limited:
                    {
                        if (videoData != null)
                        {
                            if (videoPlaying.Value)
                            {
                                discordRPC?.UpdatePresence(new RichPresence()
                                {
                                    Type = activityType,
                                    State = state,
                                    Timestamps = timestamps,
                                    Assets = new Assets()
                                    {
                                        LargeImageKey = "ax_player_new"
                                    },
                                    Buttons =
                                    [
                                        new DiscordRPC.Button
                                    {
                                        Label = buttonLabel,
                                        Url = $"https://youtu.be/{videoData.Id}",
                                    }
                                    ]
                                });
                            }
                            else
                            {
                                discordRPC?.ClearPresence();
                            }
                        }
                        else
                        {
                            discordRPC?.ClearPresence();
                        }
                        break;
                    }
                    case DiscordRichPresenceMode.Off:
                    {
                        discordRPC?.ClearPresence();
                        break;
                    }
                }
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            RegisterOverlayContainer(loadVideoContainer);
            overlayFadeContainer.Hide();
            RegisterOverlayContainer(settingsContainer);
            RegisterOverlayContainer(videoDescriptionContainer);
            RegisterOverlayContainer(commentsContainer);
            RegisterOverlayContainer(searchContainer);
            RegisterOverlayContainer(reportAbuseOverlay);
            RegisterOverlayContainer(playlistOverlay);
            RegisterOverlayContainer(loadPlaylistContainer);
            RegisterOverlayContainer(audioEffectsOverlay);
            RegisterOverlayContainer(unsubscribeDialog);
            RegisterOverlayContainer(videoSaveLocationOverlay);
            RegisterOverlayContainer(addPlaylistOverlay);
            RegisterOverlayContainer(menuOverlay);
            RegisterOverlayContainer(myChannelDialog);
            RegisterOverlayContainer(myPlaylistsOverlay);
            RegisterOverlayContainer(exitOptions);
            RegisterOverlayContainer(editPlaylistOverlay);

            ghostIcon.Loop(t =>
                t.MoveToY(-10, 2000, Easing.InOutSine)
                 .Then()
                 .MoveToY(0, 2000, Easing.InOutSine)
            );

            ghostIcon2.Loop(t =>
                t.MoveToY(-10, 2000, Easing.InOutSine)
                 .Then()
                 .MoveToY(0, 2000, Easing.InOutSine)
            );

            discordRichPresence.BindValueChanged(mode => updatePresence(mode.NewValue), true);
            usernameDisplayMode.BindValueChanged(_ => updatePresence(discordRichPresence.Value), true);
            localeBindable.BindValueChanged(_ => updatePresence(discordRichPresence.Value), true);
            trayIconVisible.BindValueChanged(_ => updatePresence(discordRichPresence.Value), true);

            //check updates for LoadComplete
            if (game.IsDeployedBuild)
                checkForUpdates().FireAndForget();

            if (appGlobalConfig.Get<string>(AXPlayerSetting.AccessToken) != string.Empty)
            {
                Task.Run(async () => await googleOAuth2.SignIn());
            }

            sessionStatics.GetBindable<bool>(Static.IsControlVisible).Value = true;

            cursorInWindow?.BindValueChanged(active =>
            {
                if (active.NewValue == false)
                {
                    Schedule(() => hideControls());
                }
                else
                {
                    Schedule(() => showControls());
                }
            });

            loadBtn.Action = async () =>
            {
                ClearPlaylistItems();
                Schedule(async () =>
                {
                    YoutubeExplode.Playlists.PlaylistId? playlistId = YoutubeExplode.Playlists.PlaylistId.TryParse(videoIdBox.Text);
                    YoutubeExplode.Videos.VideoId? videoId = YoutubeExplode.Videos.VideoId.TryParse(videoIdBox.Text);

                    if (videoId != null && !string.IsNullOrEmpty(videoId.Value))
                    {
                        SetVideoSource(videoIdBox.Text);
                    }
                    else
                    {
                        SetPlaylist(videoIdBox.Text).FireAndForget();
                    }
                });
            };

            loadPlaylistBtn.ClickAction = async _ =>
            {
                SetPlaylist(playlistIdBox.Text).FireAndForget();
            };

            searchButton.ClickAction = _ =>
            {
                Search();
            };

            searchOpenButton.Action = () =>
            {
                hideOverlays();
                showOverlayContainer(searchContainer);
                searchTextBox.TakeFocus();
            };

            reportOpenButton.Action = () =>
            {
                hideOverlays();
                showOverlayContainer(reportAbuseOverlay);
            };

            playlistOpenButton.Action = () =>
            {
                hideOverlays();
                showOverlayContainer(playlistOverlay);
            };

            audioEffectsOpenButton.Action = () =>
            {
                hideOverlays();
                showOverlayContainer(audioEffectsOverlay);
            };

            saveVideoOpenButton.Action = () =>
            {
                hideOverlays();
                showOverlayContainer(videoSaveLocationOverlay);
            };

            commentOpenButton.Action = () =>
            {
                if (!commentsDisabled)
                {
                    hideOverlays();
                    showOverlayContainer(commentsContainer);
                }
            };

            loadBtnOverlayShow.Action = () =>
            {
                hideOverlays();
                showOverlayContainer(loadVideoContainer);
                videoIdBox.TakeFocus();
            };

            settingsOverlayShowBtn.Action = () =>
            {
                hideOverlays();
                showOverlayContainer(settingsContainer);
            };
        }

        private bool isDownloading;

        private void hideOverlays()
        {
            if (isDownloading)
                return;

            foreach (var item in overlayContainers)
            {
                if (item.IsVisible == true)
                {
                    hideOverlayContainer(item);
                }
            }
        }

        public async Task SetPlaylist(string playlistId)
        {
            playlistId = YoutubeExplode.Playlists.PlaylistId.Parse(playlistId);
            Schedule(async () =>
            {
                videoIdBox.Text = string.Empty;
                if (loadVideoContainer.IsVisible == true)
                {
                    Schedule(() => hideOverlayContainer(loadVideoContainer));
                }

                Playlist playlist = api.GetPlaylistInfo(playlistId);
                IList<PlaylistItem> playlistItems = await api.GetPlaylistItems(playlistId);

                SetPlaylistInfo(playlist);
                await SetPlaylistItems(playlistItems);

                playlistIdBox.Text = string.Empty;
            });
        }

        private readonly List<OverlayContainer> overlayContainers = new List<OverlayContainer>();

        public void RegisterOverlayContainer(OverlayContainer overlayContainer)
        {
            overlayContainer.IsVisible = false;
            overlayContainer.Hide();
            overlayContainers.Add(overlayContainer);
        }

        [Resolved]
        private VolumeOverlay volume { get; set; }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // ignore seek shortcuts on focused to text box
            if (currentVideoSource == null || ((e.Target.GetType() == typeof(ProjectYomiTextBox)) || (e.Target.GetType() == typeof(FormTextBox)) || (e.Target.GetType() == typeof(FormNumberBox)) || (e.Target.GetType() == typeof(FormNumberBox.InnerNumberBox)) || (e.Target.GetType() == typeof(FormNumberBox.InnerTextBox))))
                return true;

            if (e.Key >= Key.Number0 && e.Key <= Key.Number9)
            {
                int digit = e.Key - Key.Number0;
                double target = currentVideoSource.VideoProgress.MaxValue * (digit / 10.0);

                currentVideoSource?.SeekTo(target * 1000);
                //updatePresence(discordRichPresence.Value);
                showControls();
            }

            if (e.Key >= Key.Keypad0 && e.Key <= Key.Keypad9)
            {
                int digit = e.Key - Key.Keypad0;
                double target = currentVideoSource.VideoProgress.MaxValue * (digit / 10.0);

                currentVideoSource?.SeekTo(target * 1000);
                //updatePresence(discordRichPresence.Value);
                showControls();
            }

            return true;
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Target is TextBox)
                return false;

            switch (e.Action)
            {
                case GlobalAction.DecreaseVolume:
                case GlobalAction.IncreaseVolume:
                    return volume.Adjust(e.Action);

                case GlobalAction.FastForward_10sec:
                    currentVideoSource?.FastForward10Sec();
                    //updatePresence(discordRichPresence.Value);
                    return true;

                case GlobalAction.FastRewind_10sec:
                    currentVideoSource?.FastRewind10Sec();
                    //updatePresence(discordRichPresence.Value);
                    return true;

                case GlobalAction.DecreasePlaybackSpeed:
                    playbackSpeed.Value -= 0.05;
                    //osd.Display(new SpeedChangeToast(playbackSpeed.Value));
                    newToastOverlay.ShowToast(AXPlayerStrings.PlaybackSpeed(playbackSpeed.Value));
                    return true;

                case GlobalAction.ResetPlaybackSpeed:
                    playbackSpeed.Value = 1;
                    //osd.Display(new SpeedChangeToast(playbackSpeed.Value));
                    newToastOverlay.ShowToast(AXPlayerStrings.PlaybackSpeed(playbackSpeed.Value));
                    return true;

                case GlobalAction.IncreasePlaybackSpeed:
                    playbackSpeed.Value += 0.05;
                    //osd.Display(new SpeedChangeToast(playbackSpeed.Value));
                    newToastOverlay.ShowToast(AXPlayerStrings.PlaybackSpeed(playbackSpeed.Value));
                    return true;

                case GlobalAction.DecreaseVideoVolume:
                    videoVolume.Value -= 0.05;
                    return true;

                case GlobalAction.IncreaseVideoVolume:
                    videoVolume.Value += 0.05;
                    return true;

                case GlobalAction.DecreaseVideoDim:
                    videoDim.Value -= 0.01;
                    osd.Display(new VideoDimChangeToast(videoDim.Value));
                    return true;

                case GlobalAction.IncreaseVideoDim:
                    videoDim.Value += 0.01;
                    osd.Display(new VideoDimChangeToast(videoDim.Value));
                    return true;

                case GlobalAction.DecreasePlaybackSpeed2:
                    playbackSpeed.Value -= 0.01;
                    newToastOverlay.ShowToast(AXPlayerStrings.PlaybackSpeed(playbackSpeed.Value));
                    return true;

                case GlobalAction.IncreasePlaybackSpeed2:
                    playbackSpeed.Value += 0.01;
                    newToastOverlay.ShowToast(AXPlayerStrings.PlaybackSpeed(playbackSpeed.Value));
                    return true;
            }

            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.ToggleMute:
                case GlobalAction.NextVolumeMeter:
                case GlobalAction.PreviousVolumeMeter:
                    return volume.Adjust(e.Action);

                case GlobalAction.RestartApp:
                    if (game?.RestartAppWhenExited() == true)
                    {
                        game.AttemptExit();
                    }
                    return true;

                case GlobalAction.OpenPlaybackSpeedMenu:
                    playbackSpeedContainer.ShowPopover();
                    return true;

                case GlobalAction.OpenVideoVolumeMenu:
                    videoVolumeControlContainer.ShowPopover();
                    return true;

                case GlobalAction.QuitApp:
                    game.AttemptExit();
                    return true;

                case GlobalAction.Back:
                    hideOverlays();
                    return true;

                case GlobalAction.ToggleRepeatVideo:
                    updateRepeatState();
                    return true;

                case GlobalAction.ToggleControlsPinState:
                    showControls();
                    updatePinState();
                    return true;

                case GlobalAction.OpenLoadVideo:
                    if (!loadBtnOverlayShow.Enabled.Value)
                        return true;

                    if (!loadVideoContainer.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(loadVideoContainer);
                        videoIdBox.TakeFocus();
                    }
                    else
                        hideOverlayContainer(loadVideoContainer);

                    return true;

                case GlobalAction.OpenSearch:
                    if (!searchContainer.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(searchContainer);
                        searchTextBox.TakeFocus();
                    }
                    else
                        hideOverlayContainer(searchContainer);

                    return true;

                case GlobalAction.OpenMyPlaylists:
                    if (signedIn.Value == false)
                        return true;

                    if (!myPlaylistsOverlay.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(myPlaylistsOverlay);
                    }
                    else
                        hideOverlayContainer(myPlaylistsOverlay);

                    return true;

                case GlobalAction.OpenMenu:
                    if (!menuOverlay.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(menuOverlay);
                    }
                    else
                        hideOverlayContainer(menuOverlay);

                    return true;

                case GlobalAction.AddPlaylistKey:
                    if (signedIn.Value == false)
                        return true;

                    if (!addPlaylistOverlay.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(addPlaylistOverlay);
                    }
                    else
                        hideOverlayContainer(addPlaylistOverlay);

                    return true;

                case GlobalAction.SaveVideoToPlaylist:
                    if (videoData == null)
                        return true;

                    if (signedIn.Value == false)
                        return true;

                    if (!videoSaveLocationOverlay.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(videoSaveLocationOverlay);
                    }
                    else
                        hideOverlayContainer(videoSaveLocationOverlay);

                    return true;

                case GlobalAction.OpenSettings:
                    if (!settingsContainer.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(settingsContainer);
                    }
                    else
                        hideOverlayContainer(settingsContainer);

                    return true;

                case GlobalAction.OpenDescription:
                    if (!videoDescriptionContainer.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(videoDescriptionContainer);
                    }
                    else
                        hideOverlayContainer(videoDescriptionContainer);

                    return true;

                case GlobalAction.ReportAbuse:
                    if (videoData == null)
                        return true;

                    if (signedIn.Value == false)
                        return true;

                    if (!reportAbuseOverlay.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(reportAbuseOverlay);
                    }
                    else
                        hideOverlayContainer(reportAbuseOverlay);

                    return true;

                /*
            case GlobalAction.DownloadVideo:
                if (videoData == null)
                    return true;

                if (!downloadReadyContainer.IsVisible)
                {
                    currentVideoSource?.Pause();
                    hideOverlays();
                    showOverlayContainer(downloadReadyContainer);
                }
                else
                    hideOverlayContainer(downloadReadyContainer);

                return true;
                */

                case GlobalAction.OpenComments:
                    if (videoData == null)
                        return true;

                    if (commentsDisabled)
                        return true;

                    if (!commentsContainer.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(commentsContainer);
                    }
                    else
                        hideOverlayContainer(commentsContainer);

                    return true;

                case GlobalAction.OpenPlaylist:
                    if (!playlistOverlay.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(playlistOverlay);
                    }
                    else
                        hideOverlayContainer(playlistOverlay);

                    return true;

                case GlobalAction.OpenAudioEffects:
                    if (!audioEffectsOverlay.IsVisible)
                    {
                        hideOverlays();
                        showOverlayContainer(audioEffectsOverlay);
                    }
                    else
                        hideOverlayContainer(audioEffectsOverlay);

                    return true;

                case GlobalAction.PlayPause:
                    if (currentVideoSource != null)
                    {
                        if (currentVideoSource.IsPlaying())
                            currentVideoSource.Pause(true);
                        else
                            currentVideoSource.Play(true);
                    }
                    //updatePresence(discordRichPresence.Value);
                    showControls();
                    return true;

                case GlobalAction.PrevVideo:
                    if (currentVideoSource != null)
                    {
                        if (playlists.Count > 0)
                        {
                            if (playlistItemIndex != 0)
                                playlistItemIndex--;

                            Task.Run(async () =>
                            {
                                Schedule(async () =>
                                {
                                    SetVideoSource(playlists[playlistItemIndex].Snippet.ResourceId.VideoId);
                                });
                            });
                        }
                    }
                    return true;

                case GlobalAction.NextVideo:
                    if (currentVideoSource != null)
                    {
                        if (playlists.Count > 0)
                        {
                            if (playlistItemIndex != playlists.Count - 1)
                                playlistItemIndex++;

                            Task.Run(async () =>
                            {
                                Schedule(async () =>
                                {
                                    SetVideoSource(playlists[playlistItemIndex].Snippet.ResourceId.VideoId);
                                });
                            });
                        }
                    }
                    return true;

                case GlobalAction.ToggleAdjustPitchOnSpeedChange:
                    adjustPitch.Value = !adjustPitch.Value;
                    return true;

                case GlobalAction.ToggleFPSDisplay:
                    fpsDisplay.Value = !fpsDisplay.Value;
                    return true;

                case GlobalAction.CycleCaptionLanguage:
                    CycleCaptionLanguage();
                    return true;

                case GlobalAction.CycleAspectRatio:
                    CycleAspectRatio();
                    return true;

                case GlobalAction.CycleProfileShape:
                    CycleProfileShape();
                    return true;

                case GlobalAction.CycleScalingMode:
                    CycleScalingMode();
                    return true;

                case GlobalAction.ToggleReverbEffect:
                    reverbEnabled.Value = !reverbEnabled.Value;
                    return true;

                case GlobalAction.ToggleRotateEffect:
                    rotateEnabled.Value = !rotateEnabled.Value;
                    return true;

                case GlobalAction.ToggleEchoEffect:
                    echoEnabled.Value = !echoEnabled.Value;
                    return true;

                case GlobalAction.ToggleDistortionEffect:
                    distortionEnabled.Value = !distortionEnabled.Value;
                    return true;

                case GlobalAction.ToggleKaraokeEffect:
                    karaokeEnabled.Value = !karaokeEnabled.Value;
                    return true;

                case GlobalAction.ToggleChorusEffect:
                    chorusEnabled.Value = !chorusEnabled.Value;
                    return true;

                case GlobalAction.ToggleEightBitEffect:
                    eightBitEnabled.Value = !eightBitEnabled.Value;
                    return true;

                case GlobalAction.Notifications:
                    hideOverlays();

                    if (notificationOverlay.IsOpened.Value)
                        notificationOverlay.HideOverlay();
                    else
                        notificationOverlay.OpenOverlay();

                    return true;
            }

            return false;
        }

        [Resolved]
        private OnScreenDisplay osd { get; set; } = null!;

        private int playlistItemIndex = 0;

        protected void CycleCaptionLanguage()
        {
            if (captionEnabled.Disabled)
                return;

            captionEnabled.Value = !captionEnabled.Value;
        }

        private IList<PlaylistItem> playlists = new List<PlaylistItem>();
        private List<PlaylistItemView> playlistItemViews = new List<PlaylistItemView>();

        private YoutubeExplode.Videos.Streams.VideoQuality currentVideoQuality;

        public void ClearPlaylistItems()
        {
            playlists.Clear();
            playlistItemViews.Clear();

            foreach (var item in playlistItemsView.Children)
            {
                Schedule(() => item.Expire());
            }

            playlistThumbnail.Texture = null;
            playlistName.Text = AXPlayerStrings.PlaylistNotLoaded;
            playlistAuthor.Text = AXPlayerStrings.PlaylistNotLoadedDesc;

            if (playlists.Count == 0)
            {
                Schedule(() => prevVideoButton.Enabled.Value = false);
                Schedule(() => nextVideoButton.Enabled.Value = false);
            }
        }

        [Resolved]
        private TextureStore textureStore { get; set; }

        private Google.Apis.YouTube.v3.Data.Video videoData;
        private Google.Apis.YouTube.v3.Data.Channel channelData;

        public async Task SetPlaylistItems(IList<PlaylistItem> playlists)
        {
            this.playlists = playlists;

            Schedule(async () =>
            {
                SetVideoSource(playlists[0].Snippet.ResourceId.VideoId);
            });

            playlistItemViews.Clear();

            foreach (var item in playlistItemsView.Children)
            {
                Schedule(() => item.Expire());
            }

            int i = 0;

            foreach (var item in playlists)
            {
                try
                {
                    Google.Apis.YouTube.v3.Data.Video videoData = api.GetVideo(item.Snippet.ResourceId.VideoId);

                    PlaylistItemView playlistItemView = new PlaylistItemView(i)
                    {
                        RelativeSizeAxes = Axes.X,
                        Enabled = { Value = true },
                    };

                    playlistItemView.ClickAction = async v =>
                    {
                        Schedule(async () =>
                        {
                            playlistItemIndex = playlistItemView.Index;
                            Schedule(async () =>
                            {
                                SetVideoSource(item.Snippet.ResourceId.VideoId);
                            });
                        });
                    };

                    playlistItemViews.Add(playlistItemView);

                    Schedule(() =>
                    {
                        playlistItemView.Data = videoData;
                        playlistItemsView.Add(playlistItemView);
                        playlistItemView.UpdateData();
                    });

                    i++;
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            }
        }

        public void SetPlaylistInfo(Playlist playlist)
        {
            Schedule(() =>
            {
                playlistThumbnail.Texture = textureStore.Get(playlist.Snippet.Thumbnails.High.Url);
                playlistName.Text = playlist.Snippet.Title;
                playlistAuthor.Text = string.Empty;
                playlistAuthor.AddLink(playlist.Snippet.ChannelTitle, $"https://www.youtube.com/channel/{playlist.Snippet.ChannelId}");
            });
        }

        protected void CycleScalingMode()
        {
            switch (scalingMode.Value)
            {
                case ScalingMode.Off:
                    scalingMode.Value = ScalingMode.Everything;
                    break;

                case ScalingMode.Everything:
                    scalingMode.Value = ScalingMode.Video;
                    break;

                case ScalingMode.Video:
                    scalingMode.Value = ScalingMode.Off;
                    break;
            }
        }

        protected void CycleAspectRatio()
        {
            switch (aspectRatioMethod.Value)
            {
                case AspectRatioMethod.Letterbox:
                    aspectRatioMethod.Value = AspectRatioMethod.Fill;
                    break;

                case AspectRatioMethod.Fill:
                    aspectRatioMethod.Value = AspectRatioMethod.Letterbox;
                    break;
            }
        }

        protected void CycleProfileShape()
        {
            switch (profileImageShape.Value)
            {
                case ProfileImageShape.Circle:
                    profileImageShape.Value = ProfileImageShape.Square;
                    break;

                case ProfileImageShape.Square:
                    profileImageShape.Value = ProfileImageShape.Circle;
                    break;
            }
        }

        private Bindable<bool> videoPlaying;

        private bool blurState;
        private bool notificationOverlayOpened => notificationOverlay.IsOpened.Value;

        protected override void Update()
        {
            base.Update();

            //seekbar.PlaybackSpeed.Value = playbackSpeed.Value;

            if (game.UseSystemCursor.Value == true)
            {
                game.SetCursorVisibility(CursorVisible);
            }

            if (commentTextBoxContainerFocused != commentTextBox.HasFocus)
            {
                commentTextBoxContainerFocused = commentTextBox.HasFocus;
                commentTextBoxContainer.TransformTo(nameof(Padding), commentTextBoxContainerFocused ? new MarginPadding { Horizontal = 32 } : new MarginPadding { Horizontal = 48 }, 1000, Easing.OutQuint);
                commentTextBoxContainer.TransformTo(nameof(Margin), commentTextBoxContainerFocused ? new MarginPadding { Bottom = 16 } : new MarginPadding { Bottom = 12 }, 500, Easing.OutBack);
            }

            if (searchTextBoxContainerFocused != searchTextBox.HasFocus)
            {
                searchTextBoxContainerFocused = searchTextBox.HasFocus;
                searchTextBoxContainer.TransformTo(nameof(Padding), searchTextBoxContainerFocused ? new MarginPadding { Horizontal = 32 } : new MarginPadding { Horizontal = 48 }, 1000, Easing.OutQuint);
                searchTextBoxContainer.TransformTo(nameof(Margin), searchTextBoxContainerFocused ? new MarginPadding { Bottom = 16 } : new MarginPadding { Bottom = 12 }, 500, Easing.OutBack);
            }

            if (notificationOverlayOpened != blurState)
            {
                blurState = notificationOverlayOpened;
                videoContainer.BlurTo(new Vector2((blurState || isAnyOverlayOpen.Value) ? 6 : 0), 250, Easing.OutQuart);
            }

            if (currentVideoSource != null)
            {
                playPause.Icon = (currentVideoSource.IsPlaying() ? FontAwesome.Solid.Pause : FontAwesome.Solid.Play);
                playPause.TooltipText = (currentVideoSource.IsPlaying() ? AXPlayerStrings.Pause : AXPlayerStrings.Play);
                videoProgress.MaxValue = currentVideoSource.VideoProgress.MaxValue;

                if (videoPlaying.Value != currentVideoSource.IsPlaying())
                {
                    seekbar.IsPlaying.Value = currentVideoSource.IsPlaying();
                    playPause.SetEnabledValue2(!currentVideoSource.IsPlaying());
                    playPause.IconObject.FadeColour(currentVideoSource.IsPlaying() ? bgColor : accentColor, 250, Easing.OutQuint);
                    //playPause.TransformTo(nameof(Width), currentVideoSource.IsPlaying() ? 50f : 40f, 1000, Easing.OutElastic);

                    //prevVideoButton.TransformTo(nameof(Width), currentVideoSource.IsPlaying() ? 35f : 40f, 1000, Easing.OutElastic);
                    //prevVideoButton.TransformTo(nameof(CornerRadius), currentVideoSource.IsPlaying() ? new CornersInfo(15f, 15f, AXPlayerApp.UI_CORNER_RADIUS / 1.5f, AXPlayerApp.UI_CORNER_RADIUS / 1.5f) : new CornersInfo(15), 250, Easing.OutQuint);

                    //nextVideoButton.TransformTo(nameof(Width), currentVideoSource.IsPlaying() ? 35f : 40f, 1000, Easing.OutElastic);
                    //nextVideoButton.TransformTo(nameof(CornerRadius), currentVideoSource.IsPlaying() ? new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 1.5f, AXPlayerApp.UI_CORNER_RADIUS / 1.5f, 15f, 15f) : new CornersInfo(15), 250, Easing.OutQuint);
                }

                videoPlaying.Value = currentVideoSource.IsPlaying();

                string currentTime;

                TimeSpan duration = TimeSpan.FromSeconds(currentVideoSource.VideoProgress.Value);
                /*
                if (duration.Hours > 0)
                {
                    currentTime.Text = $"{duration.Hours.ToString("00")}:{duration.Minutes.ToString("00")}:{duration.Seconds.ToString("00")}";
                }
                else
                {
                    currentTime.Text = $"{duration.Minutes.ToString("0")}:{duration.Seconds.ToString("00")}";
                }
                */

                if (duration.Hours > 0)
                {
                    currentTime = $"{duration.Hours.ToString("00")}:{duration.Minutes.ToString("00")}:{duration.Seconds.ToString("00")}";
                }
                else
                {
                    currentTime = $"{duration.Minutes.ToString("0")}:{duration.Seconds.ToString("00")}";
                }

                timeText.SetTime(currentTime, totalTimeText);

                currentVideoSource?.UpdateSeekingState(seekbar.IsDragged);

                if (seekbar.IsDragged == false)
                    videoProgress.Value = currentVideoSource.VideoProgress.Value;
            }
        }

        private TimeSpan totalTimeSpan;

        private ControlBarIconButton playPause;

        private bool commentsDisabled = false;

        public void GetLocalizedVideoDescriptionRemake(Google.Apis.YouTube.v3.Data.Video videoData)
        {
            string str = api.GetLocalizedVideoDescription(videoData);

            videoDescription.Text = string.Empty;

            List<YouTubeDescriptionTextToken> list = AXPlayerDescriptionParser.Parse(str);

            foreach (YouTubeDescriptionTextToken item in list)
            {
                switch (item.Type)
                {
                    case YouTubeDescriptionTokenType.Text:
                        videoDescription.AddText(item.Value);
                        break;
                    case YouTubeDescriptionTokenType.Url:
                        videoDescription.AddArbitraryDrawable(new UrlRedirectDisplay(item.Value));
                        break;
                    case YouTubeDescriptionTokenType.Mention:
                        videoDescription.AddLink(item.Value, $"https://www.youtube.com/{item.Value}", AXPlayerStrings.YouTubeHandleViewProfile(item.Value), s => s.Font = s.Font.With(weight: "Bold"));
                        break;
                    case YouTubeDescriptionTokenType.Hashtag:
                        videoDescription.AddLink(item.Value, $"https://www.youtube.com/hashtag/{item.Value.Replace("#", string.Empty)}", AXPlayerStrings.Hashtag(item.Value), s => s.Font = s.Font.With(weight: "Bold"));
                        break;
                    case YouTubeDescriptionTokenType.Timestamp:
                        videoDescription.AddArbitraryDrawable(new TimestampButton(item.Value)
                        {
                            TimestampClicked = second =>
                            {
                                Logger.Log(second.ToString());
                                hideOverlays();
                                seekTo((second / 60) * 1000);
                            },
                        });
                        break;
                }
            }
        }
    }
}

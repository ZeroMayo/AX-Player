// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading.Tasks;
using System.Xml;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using NekoPlayer.App;
using NekoPlayer.App.Online;

namespace NekoPlayer.Desktop.Windows
{
    public partial class WindowsMediaSessionHandler : MediaSession
    {
        private SystemMediaTransportControls smtc;
        private MediaPlayer mediaPlayer;

#nullable enable
        private MediaSessionControls? controls;
#nullable disable

        public override void CreateMediaSession(YouTubeAPI youtubeAPI, string audioPath)
        {
            Task.Run(async () =>
            {
                mediaPlayer = new MediaPlayer
                {
                    Source = MediaSource.CreateFromUri(new Uri($"file:///{audioPath}")),
                    Volume = 0,
                };

                smtc = mediaPlayer.SystemMediaTransportControls;
                smtc.PlaybackStatus = MediaPlaybackStatus.Paused;
                smtc.IsEnabled = true;
                smtc.IsPlayEnabled = true;
                smtc.IsPauseEnabled = true;
                smtc.IsNextEnabled = true;
                smtc.IsPreviousEnabled = true;
                smtc.IsFastForwardEnabled = true;
                smtc.IsRewindEnabled = true;

                smtc.ButtonPressed += smtc_ButtonPressed;

                smtc.PlaybackPositionChangeRequested += smtc_PlaybackPositionChangeRequested;

                smtc.DisplayUpdater.Type = MediaPlaybackType.Music;

                smtc.DisplayUpdater.MusicProperties.Title = "(unknown)";
                smtc.DisplayUpdater.MusicProperties.Artist = "(unknown)";

                smtc.DisplayUpdater.Update();
                IsLoaded = true;

                base.YouTubeAPI = youtubeAPI;
            });
        }

        private void smtc_PlaybackPositionChangeRequested(SystemMediaTransportControls sender, PlaybackPositionChangeRequestedEventArgs args)
        {
            TimeSpan timeSpan = args.RequestedPlaybackPosition;

            double ms = timeSpan.TotalMilliseconds;

            controls.OnSeek.Invoke(ms);
        }

        public override void UpdateMediaSession(Video video)
        {
            Task.Run(async () =>
            {
                try
                {
                    smtc.DisplayUpdater.MusicProperties.Title = !string.IsNullOrEmpty(YouTubeAPI.GetLocalizedVideoTitle(video)) ? YouTubeAPI.GetLocalizedVideoTitle(video) : video.Snippet.Title;
                    smtc.DisplayUpdater.MusicProperties.Artist = !string.IsNullOrEmpty(YouTubeAPI.GetLocalizedChannelTitle(YouTubeAPI.GetChannel(video.Snippet.ChannelId))) ? YouTubeAPI.GetLocalizedChannelTitle(YouTubeAPI.GetChannel(video.Snippet.ChannelId)) : video.Snippet.ChannelTitle;
                    smtc.DisplayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(video.Snippet.Thumbnails.High.Url));

                    smtc.DisplayUpdater.Update();
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            });
        }

        public override void UpdatePlayingState(bool playing)
        {
            Task.Run(async () =>
            {
                try
                {
                    smtc.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
                    smtc.DisplayUpdater.Update();
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            });
        }

        public override void UpdateTimestamp(Video video, double pos)
        {
            try
            {
                if (IsLoaded)
                {
                    smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
                    {
                        StartTime = TimeSpan.Zero,
                        EndTime = XmlConvert.ToTimeSpan(video.ContentDetails.Duration),
                        Position = TimeSpan.FromSeconds(pos * 0.001f)
                    });
                    mediaPlayer.Position = TimeSpan.FromSeconds(pos * 0.001f);
                    smtc.DisplayUpdater.Update();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.GetDescription());
            }
        }

        public override void DeleteMediaSession()
        {
            smtc.DisplayUpdater.ClearAll();
            //smtc = null;
            IsLoaded = false;
            mediaPlayer.Dispose();
        }

        private void smtc_ButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    controls?.PlayButtonPressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    controls?.PauseButtonPressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Rewind:
                    controls?.PrevButtonPressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.FastForward:
                    controls?.NextButtonPressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    controls?.PrevButtonPressed?.Invoke();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    controls?.NextButtonPressed?.Invoke();
                    break;
            }
        }

        public override void RegisterControlEvents(MediaSessionControls controls)
        {
            this.controls = controls;
        }

        public override void UnregisterControlEvents()
        {
            controls = null;
        }

        public override void UpdatePlaybackSpeed(double speed)
        {
            try
            {
                smtc.PlaybackRate = speed;
                mediaPlayer.PlaybackRate = speed;
            } catch (Exception ex)
            {
                Logger.Error(ex, ex.GetDescription());
            }
        }
    }
}

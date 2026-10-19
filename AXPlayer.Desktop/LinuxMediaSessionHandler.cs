// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using Tmds.DBus;
using AXPlayer.App;
using AXPlayer.App.Online;

namespace AXPlayer.Desktop
{
    public partial class LinuxMediaSessionHandler : MediaSession
    {
        private Connection dbusConnection;
        private MprisPlayer mprisPlayer;

#nullable enable
        private MediaSessionControls? controls;
#nullable disable

        public override void CreateMediaSession(YouTubeAPI youtubeAPI, string audioPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    mprisPlayer = new MprisPlayer(this);

                    // [기존 코드 - 오류 발생]
                    // dbusConnection = Connection.Session;

                    // [수정 코드]
                    // Address.Session 주소로 인스턴스를 직접 생성하고 explicit하게 ConnectAsync()를 호출합니다.
                    dbusConnection = new Connection(Address.Session);
                    await dbusConnection.ConnectAsync();

                    // 연결이 성공적으로 맺어진 후 D-Bus 객체 및 서비스 등록
                    await dbusConnection.RegisterObjectAsync(mprisPlayer);
                    await dbusConnection.RegisterServiceAsync("org.mpris.MediaPlayer2.NekoPlayer");

                    IsLoaded = true;
                    base.YouTubeAPI = youtubeAPI;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, ex.GetDescription());
                }
            });
        }

        public override void UpdateMediaSession(Video video)
        {
            Task.Run(async () =>
            {
                if (!IsLoaded || mprisPlayer == null) return;

                try
                {
                    string title = YouTubeAPI.GetLocalizedVideoTitle(video);
                    string artist = YouTubeAPI.GetLocalizedChannelTitle(YouTubeAPI.GetChannel(video.Snippet.ChannelId));
                    string artUrl = video.Snippet.Thumbnails.High.Url;
                    long durationMicroseconds = (long)(XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds * 1000);

                    mprisPlayer.SetMetadata(title, artist, artUrl, durationMicroseconds);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, ex.GetDescription());
                }
            });
        }

        public override void UpdatePlayingState(bool playing)
        {
            Task.Run(async () =>
            {
                if (!IsLoaded || mprisPlayer == null) return;
                mprisPlayer.PlaybackStatus = playing ? "Playing" : "Paused";
            });
        }

        public override void UpdateTimestamp(Video video, double pos)
        {
            try
            {
                if (IsLoaded && mprisPlayer != null)
                {
                    // pos는 ms 단위 -> MPRIS는 마이크로초(microseconds) 단위 사용
                    mprisPlayer.Position = (long)(pos * 1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.GetDescription());
            }
        }

        public override void UpdatePlaybackSpeed(double speed)
        {
            if (!IsLoaded || mprisPlayer == null) return;
            mprisPlayer.Rate = speed;
        }

        public override void DeleteMediaSession()
        {
            IsLoaded = false;
            mprisPlayer = null;
            dbusConnection?.Dispose();
            dbusConnection = null;
        }

        public override void RegisterControlEvents(MediaSessionControls controls)
        {
            this.controls = controls;
        }

        public override void UnregisterControlEvents()
        {
            controls = null;
        }

        #region MPRIS D-Bus Implementation

        [DBusInterface("org.mpris.MediaPlayer2")]
        public interface IMediaPlayer2 : IDBusObject
        {
            Task RaiseAsync();
            Task QuitAsync();
            Task<bool> GetCanQuitAsync();
            Task<bool> GetCanRaiseAsync();
            Task<string> GetIdentityAsync();
            Task<string> GetDesktopEntryAsync();
        }

        [DBusInterface("org.mpris.MediaPlayer2.Player")]
        public interface IPlayer : IDBusObject
        {
            Task NextAsync();
            Task PreviousAsync();
            Task PauseAsync();
            Task PlayPauseAsync();
            Task StopAsync();
            Task PlayAsync();
            Task SeekAsync(long offset);
            Task SetPositionAsync(ObjectPath trackId, long position);
            Task OpenUriAsync(string uri);

            Task<string> GetPlaybackStatusAsync();
            Task<IDictionary<string, object>> GetMetadataAsync();
            Task<double> GetVolumeAsync();
            Task SetVolumeAsync(double volume);
            Task<long> GetPositionAsync();
            Task<double> GetRateAsync();
            Task<bool> GetCanControlAsync();
            Task<bool> GetCanPlayAsync();
            Task<bool> GetCanPauseAsync();
            Task<bool> GetCanGoNextAsync();
            Task<bool> GetCanGoPreviousAsync();
            Task<bool> GetCanSeekAsync();
        }

        public class MprisPlayer : IMediaPlayer2, IPlayer
        {
            private readonly LinuxMediaSessionHandler handler;
            private readonly Dictionary<string, object> metadata = new();

            public ObjectPath ObjectPath => new ObjectPath("/org/mpris/MediaPlayer2");

            public string PlaybackStatus { get; set; } = "Paused";
            public long Position { get; set; }
            public double Rate { get; set; } = 1.0;

            public MprisPlayer(LinuxMediaSessionHandler handler)
            {
                this.handler = handler;
            }

            public void SetMetadata(string title, string artist, string artUrl, long lengthMicroseconds)
            {
                metadata["mpris:trackid"] = new ObjectPath("/org/mpris/MediaPlayer2/Track/1");
                metadata["mpris:length"] = lengthMicroseconds;
                metadata["xesam:title"] = title;
                metadata["xesam:artist"] = new string[] { artist };
                metadata["mpris:artUrl"] = artUrl;
            }

            // IMediaPlayer2
            public Task RaiseAsync() => Task.CompletedTask;
            public Task QuitAsync() => Task.CompletedTask;
            public Task<bool> GetCanQuitAsync() => Task.FromResult(false);
            public Task<bool> GetCanRaiseAsync() => Task.FromResult(false);
            public Task<string> GetIdentityAsync() => Task.FromResult("NekoPlayer");
            public Task<string> GetDesktopEntryAsync() => Task.FromResult("nekoplayer");

            // IPlayer - Event Handlers
            public Task PlayAsync()
            {
                handler.controls?.PlayButtonPressed?.Invoke();
                return Task.CompletedTask;
            }

            public Task PauseAsync()
            {
                handler.controls?.PauseButtonPressed?.Invoke();
                return Task.CompletedTask;
            }

            public Task PlayPauseAsync()
            {
                if (PlaybackStatus == "Playing")
                    handler.controls?.PauseButtonPressed?.Invoke();
                else
                    handler.controls?.PlayButtonPressed?.Invoke();

                return Task.CompletedTask;
            }

            public Task StopAsync()
            {
                handler.controls?.PauseButtonPressed?.Invoke();
                return Task.CompletedTask;
            }

            public Task NextAsync()
            {
                handler.controls?.NextButtonPressed?.Invoke();
                return Task.CompletedTask;
            }

            public Task PreviousAsync()
            {
                handler.controls?.PrevButtonPressed?.Invoke();
                return Task.CompletedTask;
            }

            public Task SeekAsync(long offset)
            {
                // offset: microseconds -> ms
                double newPosMs = (Position + offset) / 1000.0;
                handler.controls?.OnSeek?.Invoke(newPosMs);
                return Task.CompletedTask;
            }

            public Task SetPositionAsync(ObjectPath trackId, long position)
            {
                // position: microseconds -> ms
                double posMs = position / 1000.0;
                handler.controls?.OnSeek?.Invoke(posMs);
                return Task.CompletedTask;
            }

            public Task OpenUriAsync(string uri) => Task.CompletedTask;

            // Property Getters
            public Task<string> GetPlaybackStatusAsync() => Task.FromResult(PlaybackStatus);
            public Task<IDictionary<string, object>> GetMetadataAsync() => Task.FromResult<IDictionary<string, object>>(metadata);
            public Task<double> GetVolumeAsync() => Task.FromResult(1.0);
            public Task SetVolumeAsync(double volume) => Task.CompletedTask;
            public Task<long> GetPositionAsync() => Task.FromResult(Position);
            public Task<double> GetRateAsync() => Task.FromResult(Rate);

            public Task<bool> GetCanControlAsync() => Task.FromResult(true);
            public Task<bool> GetCanPlayAsync() => Task.FromResult(true);
            public Task<bool> GetCanPauseAsync() => Task.FromResult(true);
            public Task<bool> GetCanGoNextAsync() => Task.FromResult(true);
            public Task<bool> GetCanGoPreviousAsync() => Task.FromResult(true);
            public Task<bool> GetCanSeekAsync() => Task.FromResult(true);
        }

        #endregion
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Diagnostics;
using System.Xml;
using Android.App;
using Android.Content;
using Android.Media;
using Android.OS;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Logging;
using AXPlayer.App;
using AXPlayer.App.Online;
using AndroidSession = Android.Media.Session;

namespace AXPlayer.Android
{
    /// <summary>
    /// Exposes AX Player's playback to Android's media session, allowing control from the notification shade,
    /// lock screen, Bluetooth headsets and other system media integrations.
    /// </summary>
    public partial class AndroidMediaSessionHandler : MediaSession
    {
        private const string channel_id = "axplayer_media_session";
        private const int notification_id = 1;

        private readonly Handler mainHandler = new Handler(Looper.MainLooper!);
        private readonly Stopwatch playbackStateUpdateClock = new Stopwatch();

#nullable disable
        private AndroidSession.MediaSession session;
        private NotificationManager notificationManager;
        private MediaSessionControls controls;
#nullable enable

        private string title = string.Empty;
        private string artist = string.Empty;
        private string? artUrl;
        private long duration;
        private double position;
        private double rate = 1;
        private bool playing;

        public override void CreateMediaSession(YouTubeAPI youtubeAPI, string audioPath)
        {
            YouTubeAPI = youtubeAPI;
            IsLoaded = true;
            runOnMainThread(createOrUpdateSession);
        }

        public override void UpdateMediaSession(Video video)
        {
            if (!IsLoaded)
                return;

            try
            {
                title = YouTubeAPI.GetLocalizedVideoTitle(video);
                artist = YouTubeAPI.GetLocalizedChannelTitle(YouTubeAPI.GetChannel(video.Snippet.ChannelId));
                artUrl = video.Snippet.Thumbnails.High.Url;
                duration = (long)XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalMilliseconds;

                runOnMainThread(() =>
                {
                    applyMetadata();
                    updateNotification();
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, @"Failed to update Android media session.");
            }
        }

        public override void UpdatePlayingState(bool playing)
        {
            this.playing = playing;
            position = Math.Max(0, position);

            if (!IsLoaded)
                return;

            runOnMainThread(() =>
            {
                applyPlaybackState(true);
                updateNotification();
            });
        }

        public override void UpdateTimestamp(Video video, double pos)
        {
            position = pos;

            if (!IsLoaded || playbackStateUpdateClock.ElapsedMilliseconds < 1000)
                return;

            runOnMainThread(() => applyPlaybackState(true));
        }

        public override void UpdatePlaybackSpeed(double speed)
        {
            rate = speed;

            if (!IsLoaded)
                return;

            runOnMainThread(() => applyPlaybackState(true));
        }

        public override void DeleteMediaSession()
        {
            IsLoaded = false;

            runOnMainThread(() =>
            {
                try
                {
                    notificationManager?.Cancel(notification_id);
                    if (session != null)
                        session.Active = false;
                    session?.Release();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, @"Failed to tear down Android media session.");
                }

                session = null;
                notificationManager = null;
            });
        }

        public override void RegisterControlEvents(MediaSessionControls controls) => this.controls = controls;

        public override void UnregisterControlEvents() => controls = null;

        private void createOrUpdateSession()
        {
            try
            {
                if (session == null)
                {
                    Context context = global::Android.App.Application.Context;

                    session = new AndroidSession.MediaSession(context, @"AXPlayer");
                    session.SetCallback(new SessionCallback(this), mainHandler);
                    session.SetFlags(AndroidSession.MediaSessionFlags.HandlesMediaButtons | AndroidSession.MediaSessionFlags.HandlesTransportControls);
                    session.Active = true;

                    notificationManager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
                    createNotificationChannel(context);
                }

                applyMetadata();
                applyPlaybackState(true);
                updateNotification();
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                Logger.Error(ex, @"Failed to create Android media session.");
            }
        }

        private void createNotificationChannel(Context context)
        {
            if (notificationManager == null || !OperatingSystem.IsAndroidVersionAtLeast(26))
                return;

            var channel = new NotificationChannel(channel_id, getAppLabel(context), NotificationImportance.Low)
            {
                Description = getAppLabel(context),
            };

            channel.SetShowBadge(false);
            notificationManager.CreateNotificationChannel(channel);
        }

        private void applyMetadata()
        {
            if (session == null)
                return;

            var builder = new MediaMetadata.Builder()
                          .PutString(MediaMetadata.MetadataKeyTitle, title)
                          .PutString(MediaMetadata.MetadataKeyArtist, artist)
                          .PutLong(MediaMetadata.MetadataKeyDuration, duration);

            if (!string.IsNullOrEmpty(artUrl))
                builder.PutString(MediaMetadata.MetadataKeyArtUri, artUrl);

            session.SetMetadata(builder.Build());
        }

        private void applyPlaybackState(bool force = false)
        {
            if (session == null)
                return;

            if (!force && playbackStateUpdateClock.ElapsedMilliseconds < 1000)
                return;

            playbackStateUpdateClock.Restart();

            const long actions = AndroidSession.PlaybackState.ActionPlay
                                 | AndroidSession.PlaybackState.ActionPause
                                 | AndroidSession.PlaybackState.ActionPlayPause
                                 | AndroidSession.PlaybackState.ActionSkipToNext
                                 | AndroidSession.PlaybackState.ActionSkipToPrevious
                                 | AndroidSession.PlaybackState.ActionSeekTo
                                 | AndroidSession.PlaybackState.ActionStop;

            var state = playing ? AndroidSession.PlaybackStateCode.Playing : AndroidSession.PlaybackStateCode.Paused;

            session.SetPlaybackState(new AndroidSession.PlaybackState.Builder()
                                     .SetActions(actions)
                                     .SetState(state, (long)position, (float)rate)
                                     .Build());
        }

        private void updateNotification()
        {
            if (session == null || notificationManager == null)
                return;

            Context context = global::Android.App.Application.Context;

            Notification.Builder builder;

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                builder = new Notification.Builder(context, channel_id);
            else
                builder = new Notification.Builder(context);

            var contentIntent = new Intent(context, typeof(MainActivity));
            contentIntent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

            builder.SetContentTitle(string.IsNullOrEmpty(title) ? getAppLabel(context) : title)
                   .SetContentText(artist)
                   .SetSmallIcon(global::AXPlayer.Android.Resource.Drawable.ic_notification)
                   .SetStyle(new Notification.MediaStyle().SetMediaSession(session.SessionToken))
                   .SetContentIntent(PendingIntent.GetActivity(context, 0, contentIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable))
                   .SetCategory(Notification.CategoryTransport)
                   .SetVisibility(NotificationVisibility.Public)
                   .SetOngoing(playing)
                   .SetOnlyAlertOnce(true);

            notificationManager.Notify(notification_id, builder.Build());
        }

        private static string getAppLabel(Context context) => context.GetString(global::AXPlayer.Android.Resource.String.app_name);

        private void runOnMainThread(Action action) => mainHandler.Post(action);

        private sealed class SessionCallback : AndroidSession.MediaSession.Callback
        {
            private readonly AndroidMediaSessionHandler handler;

            public SessionCallback(AndroidMediaSessionHandler handler)
            {
                this.handler = handler;
            }

            public override void OnPlay() => handler.controls?.PlayButtonPressed?.Invoke();

            public override void OnPause() => handler.controls?.PauseButtonPressed?.Invoke();

            public override void OnStop() => handler.controls?.PauseButtonPressed?.Invoke();

            public override void OnSkipToNext() => handler.controls?.NextButtonPressed?.Invoke();

            public override void OnSkipToPrevious() => handler.controls?.PrevButtonPressed?.Invoke();

            public override void OnSeekTo(long pos) => handler.controls?.OnSeek?.Invoke(pos);
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using AXPlayer.App;
using AXPlayer.App.Online;

namespace AXPlayer.Desktop.Windows
{
    public partial class WindowsMediaSessionHandler : MediaSession
    {
        private const string unknown_metadata = "(unknown)";
        private const double default_playback_rate = 1;

        /// <summary>
        /// Guards the session fields below. The session is created on a background thread while it is consumed
        /// from the update thread, so every field used by both has to be read and written under this lock.
        /// </summary>
        private readonly object sessionLock = new object();

        private SystemMediaTransportControls smtc;
        private MediaPlayer mediaPlayer;

        /// <summary>
        /// Incremented whenever a session is created or deleted, so an in-flight <see cref="CreateMediaSession"/>
        /// can detect that it has been superseded/cancelled and discard its result instead of reviving a dead session.
        /// </summary>
        private int sessionGeneration;

        private TimeSpan trackDuration = TimeSpan.Zero;
        private double playbackRate = default_playback_rate;

        /// <summary>
        /// Incremented for every metadata request, so a slow (network bound) localisation lookup cannot overwrite
        /// the metadata of a newer request.
        /// </summary>
        private int metadataRevision;

        /// <summary>
        /// Metadata that was requested before the session finished loading. It is replayed once the session is
        /// available, otherwise the very first video is displayed as "(unknown)" until the user seeks or pauses.
        /// </summary>
        private Video pendingVideo;
        private int pendingVideoGeneration = -1;
        private int pendingVideoRevision = -1;

        /// <summary>
        /// Whether the current session already has metadata published, so a replay after creation does not repeat
        /// the (network bound) localisation lookup.
        /// </summary>
        private bool metadataPublished;

#nullable enable
        private MediaSessionControls? controls;
#nullable disable

        public override void CreateMediaSession(YouTubeAPI youtubeAPI, string audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath))
            {
                Logger.Log("Media session was not created because no audio path was supplied.", LoggingTarget.Runtime, LogLevel.Important);
                return;
            }

            Uri audioUri;

            try
            {
                // new Uri($"file:///{audioPath}") corrupts paths containing '#'/'%'/'?', so let Uri build the file URI.
                audioUri = new Uri(Path.GetFullPath(audioPath));
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
                return;
            }

            // Assign synchronously: UpdateMediaSession() is called immediately after this method returns and needs
            // a usable API instance, while the session itself is still being created on the background thread.
            base.YouTubeAPI = youtubeAPI;

            int generation = Interlocked.Increment(ref sessionGeneration);

            Task.Run(() =>
            {
                MediaPlayer player = null;

                try
                {
                    player = new MediaPlayer
                    {
                        Source = MediaSource.CreateFromUri(audioUri),
                        Volume = 0,
                    };

                    SystemMediaTransportControls transportControls = player.SystemMediaTransportControls;

                    transportControls.PlaybackStatus = MediaPlaybackStatus.Paused;
                    transportControls.IsEnabled = true;
                    transportControls.IsPlayEnabled = true;
                    transportControls.IsPauseEnabled = true;
                    transportControls.IsNextEnabled = true;
                    transportControls.IsPreviousEnabled = true;
                    transportControls.IsFastForwardEnabled = true;
                    transportControls.IsRewindEnabled = true;

                    transportControls.DisplayUpdater.Type = MediaPlaybackType.Music;
                    transportControls.DisplayUpdater.MusicProperties.Title = unknown_metadata;
                    transportControls.DisplayUpdater.MusicProperties.Artist = unknown_metadata;

                    transportControls.ButtonPressed += smtc_ButtonPressed;
                    transportControls.PlaybackPositionChangeRequested += smtc_PlaybackPositionChangeRequested;

                    transportControls.DisplayUpdater.Update();

                    SystemMediaTransportControls previousControls = null;
                    MediaPlayer previousPlayer = null;

                    bool superseded;

                    lock (sessionLock)
                    {
                        superseded = generation != sessionGeneration;

                        if (!superseded)
                        {
                            previousControls = smtc;
                            previousPlayer = mediaPlayer;

                            smtc = transportControls;
                            mediaPlayer = player;
                            IsLoaded = true;
                            metadataPublished = false;

                            if (pendingVideoGeneration != generation)
                            {
                                // Drop state that belongs to a session which is no longer active.
                                pendingVideo = null;
                                trackDuration = TimeSpan.Zero;
                            }
                        }
                    }

                    if (superseded)
                    {
                        // The session was deleted (or replaced) while this one was being created.
                        MediaPlayer discardedPlayer = player;
                        player = null;
                        disposeSession(transportControls, discardedPlayer);
                        return;
                    }

                    // Tear down the previous session before replaying metadata: publishing can involve network calls
                    // and the old session must not stay registered in the system UI meanwhile.
                    disposeSession(previousControls, previousPlayer);

                    applyPendingState(transportControls, player, generation);
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());

                    if (player != null)
                        disposeSession(null, player);
                }
            });
        }

        private void smtc_PlaybackPositionChangeRequested(SystemMediaTransportControls sender, PlaybackPositionChangeRequestedEventArgs args)
        {
            double ms = args.RequestedPlaybackPosition.TotalMilliseconds;

            // 'controls' is null until RegisterControlEvents() runs (and after UnregisterControlEvents()).
            controls?.OnSeek?.Invoke(Math.Max(0, ms));
        }

        public override void UpdateMediaSession(Video video)
        {
            TimeSpan duration = getDuration(video);
            int revision = Interlocked.Increment(ref metadataRevision);

            SystemMediaTransportControls transportControls;

            lock (sessionLock)
            {
                // Remember the request even when the session is not ready yet; it is replayed on creation.
                pendingVideo = video;
                pendingVideoGeneration = sessionGeneration;
                pendingVideoRevision = revision;
                trackDuration = duration;

                transportControls = smtc;

                if (transportControls != null)
                    metadataPublished = true;
            }

            if (transportControls == null)
                return;

            // Resolving the localised title/artist performs blocking network calls, so keep it off the update thread.
            Task.Run(() => publishMetadata(transportControls, video, revision));
        }

        public override void UpdatePlayingState(bool playing)
        {
            if (getSession() is not { } session)
                return;

            try
            {
                // Applied synchronously on purpose: queuing this on the thread pool (as before) allowed a
                // Play/Pause pair to be applied in the wrong order, leaving the system UI showing a stale state.
                session.TransportControls.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }
        }

        public override void UpdateTimestamp(Video video, double pos)
        {
            if (getSession() is not { } session)
                return;

            TimeSpan duration = session.Duration;

            if (duration <= TimeSpan.Zero)
                duration = getDuration(video);

            applyTimestamp(session.TransportControls, session.Player, duration, toPosition(pos, duration));
        }

        public override void DeleteMediaSession()
        {
            SystemMediaTransportControls transportControls;
            MediaPlayer player;

            lock (sessionLock)
            {
                // Invalidate any CreateMediaSession() call that is still in flight.
                Interlocked.Increment(ref sessionGeneration);

                IsLoaded = false;

                transportControls = smtc;
                player = mediaPlayer;

                smtc = null;
                mediaPlayer = null;

                pendingVideo = null;
                pendingVideoGeneration = -1;
                trackDuration = TimeSpan.Zero;
            }

            disposeSession(transportControls, player);
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
            if (double.IsNaN(speed) || double.IsInfinity(speed) || !(speed > 0))
                speed = default_playback_rate;

            lock (sessionLock)
                playbackRate = speed;

            if (getSession() is not { } session)
                return;

            applyPlaybackRate(session.TransportControls, session.Player, speed);
        }

        /// <summary>
        /// Replays everything that was requested while the session was still being created.
        /// </summary>
        private void applyPendingState(SystemMediaTransportControls transportControls, MediaPlayer player, int generation)
        {
            TimeSpan duration;
            double rate;

            lock (sessionLock)
            {
                if (generation != sessionGeneration)
                    return;

                duration = trackDuration;
                rate = playbackRate;
            }

            publishPendingMetadata(transportControls, generation);

            applyTimestamp(transportControls, player, duration, TimeSpan.Zero);
            applyPlaybackRate(transportControls, player, rate);
        }

        private void publishMetadata(SystemMediaTransportControls transportControls, Video video, int revision)
        {
            lock (sessionLock)
            {
                if (revision != metadataRevision || smtc != transportControls)
                    return;
            }

            (string title, string artist, string thumbnailUrl) = resolveMetadata(video);

            lock (sessionLock)
            {
                if (revision != metadataRevision || smtc != transportControls)
                    return;

                writeMetadata(transportControls, title, artist, thumbnailUrl);
                metadataPublished = true;
            }
        }

        private void publishPendingMetadata(SystemMediaTransportControls transportControls, int generation)
        {
            // Two attempts are enough: the second one picks up metadata that arrived while the first lookup ran.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Video video;
                int revision;

                lock (sessionLock)
                {
                    if (generation != sessionGeneration || pendingVideo == null || metadataPublished)
                        return;

                    video = pendingVideo;
                    revision = pendingVideoRevision;
                }

                publishMetadata(transportControls, video, revision);

                lock (sessionLock)
                {
                    if (metadataPublished || revision == metadataRevision || generation != sessionGeneration)
                        return;
                }
            }
        }

        private (string Title, string Artist, string ThumbnailUrl) resolveMetadata(Video video)
        {
            string title = video?.Snippet?.Title;
            string artist = video?.Snippet?.ChannelTitle;

            YouTubeAPI youtubeAPI = YouTubeAPI;

            if (video != null && youtubeAPI != null)
            {
                try
                {
                    // Resolve every localised value exactly once: GetChannel() performs a blocking network request,
                    // so querying it repeatedly (as the previous implementation did) wastes time and API quota.
                    string localizedTitle = youtubeAPI.GetLocalizedVideoTitle(video);

                    if (!string.IsNullOrEmpty(localizedTitle))
                        title = localizedTitle;

                    string channelId = video.Snippet?.ChannelId;

                    if (!string.IsNullOrEmpty(channelId))
                    {
                        Channel channel = youtubeAPI.GetChannel(channelId);
                        string localizedArtist = youtubeAPI.GetLocalizedChannelTitle(channel);

                        if (!string.IsNullOrEmpty(localizedArtist))
                            artist = localizedArtist;
                    }
                }
                catch (Exception e)
                {
                    // Localisation is optional; fall back to the raw YouTube metadata.
                    Logger.Error(e, e.GetDescription());
                }
            }

            return (
                string.IsNullOrEmpty(title) ? unknown_metadata : title,
                string.IsNullOrEmpty(artist) ? unknown_metadata : artist,
                getThumbnailUrl(video));
        }

        /// <summary>
        /// Publishes metadata. Must be called while holding <see cref="sessionLock"/>.
        /// </summary>
        private static void writeMetadata(SystemMediaTransportControls transportControls, string title, string artist, string thumbnailUrl)
        {
            try
            {
                SystemMediaTransportControlsDisplayUpdater displayUpdater = transportControls.DisplayUpdater;

                displayUpdater.MusicProperties.Title = title;
                displayUpdater.MusicProperties.Artist = artist;

                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    try
                    {
                        displayUpdater.Thumbnail = RandomAccessStreamReference.CreateFromUri(new Uri(thumbnailUrl));
                    }
                    catch (Exception e)
                    {
                        // A broken thumbnail must never prevent the title/artist from being published.
                        Logger.Error(e, e.GetDescription());
                    }
                }

                displayUpdater.Update();
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }
        }

        private static void applyTimestamp(SystemMediaTransportControls transportControls, MediaPlayer player, TimeSpan duration, TimeSpan position)
        {
            try
            {
                var timelineProperties = new SystemMediaTransportControlsTimelineProperties
                {
                    StartTime = TimeSpan.Zero,
                    Position = position,
                };

                // Do not report an end time when the length is unknown (live streams): a bogus value
                // makes the system seek bar show nonsense, while zero simply hides the duration.
                if (duration > TimeSpan.Zero)
                    timelineProperties.EndTime = duration;

                transportControls.UpdateTimelineProperties(timelineProperties);
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }

            try
            {
                if (player.Position != position)
                    player.Position = position;
            }
            catch (Exception e)
            {
                // The silent player may not have opened its media source yet; the timeline above is already correct.
                Logger.Log($"Failed to update the media player position: {e.GetDescription()}", LoggingTarget.Runtime, LogLevel.Debug);
            }
        }

        private static void applyPlaybackRate(SystemMediaTransportControls transportControls, MediaPlayer player, double rate)
        {
            try
            {
                transportControls.PlaybackRate = rate;
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }

            try
            {
                player.PlaybackRate = rate;
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }
        }

        private static TimeSpan toPosition(double ms, TimeSpan duration)
        {
            if (double.IsNaN(ms) || double.IsInfinity(ms) || ms < 0)
                ms = 0;

            TimeSpan position = TimeSpan.FromMilliseconds(ms);

            return duration > TimeSpan.Zero && position > duration ? duration : position;
        }

        private void disposeSession(SystemMediaTransportControls transportControls, MediaPlayer player)
        {
            if (transportControls != null)
            {
                try
                {
                    transportControls.ButtonPressed -= smtc_ButtonPressed;
                    transportControls.PlaybackPositionChangeRequested -= smtc_PlaybackPositionChangeRequested;
                    transportControls.DisplayUpdater.ClearAll();
                    transportControls.PlaybackStatus = MediaPlaybackStatus.Closed;
                    transportControls.IsEnabled = false;
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            }

            if (player != null)
            {
                try
                {
                    player.Pause();
                    player.Source = null;
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }

                try
                {
                    player.Dispose();
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                }
            }
        }

        /// <summary>
        /// Returns a consistent snapshot of the active session, or <c>null</c> when no session is loaded.
        /// </summary>
        private (SystemMediaTransportControls TransportControls, MediaPlayer Player, TimeSpan Duration)? getSession()
        {
            lock (sessionLock)
            {
                if (smtc == null || mediaPlayer == null)
                    return null;

                return (smtc, mediaPlayer, trackDuration);
            }
        }

        private static TimeSpan getDuration(Video video)
        {
            string isoDuration = video?.ContentDetails?.Duration;

            if (string.IsNullOrEmpty(isoDuration))
                return TimeSpan.Zero;

            try
            {
                return XmlConvert.ToTimeSpan(isoDuration);
            }
            catch (FormatException)
            {
                return TimeSpan.Zero;
            }
            catch (OverflowException)
            {
                return TimeSpan.Zero;
            }
        }

        private static string getThumbnailUrl(Video video)
        {
            ThumbnailDetails thumbnails = video?.Snippet?.Thumbnails;

            if (thumbnails == null)
                return null;

            string url = thumbnails.Maxres?.Url;

            if (string.IsNullOrEmpty(url))
                url = thumbnails.Standard?.Url;

            if (string.IsNullOrEmpty(url))
                url = thumbnails.High?.Url;

            if (string.IsNullOrEmpty(url))
                url = thumbnails.Medium?.Url;

            if (string.IsNullOrEmpty(url))
                url = thumbnails.Default__?.Url;

            return url;
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
    }
}

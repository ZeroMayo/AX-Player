// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Utils;
using DiscordRPC;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        /// <summary>
        /// Everything a presence update needs, snapshotted on the update thread so the build never
        /// touches bindables, the video source or the config from a background thread.
        /// </summary>
        private readonly record struct PresenceRequest(
            DiscordRichPresenceMode Mode,
            Video? Video,
            bool IsPlaying,
            double ProgressSeconds,
            double PlaybackSpeed,
            DateTime CapturedAtUtc,
            string ContentKey);

        /// <summary>
        /// The presence strings of the current video, resolved once per video and settings change.
        /// </summary>
        /// <remarks>
        /// Resolving them costs a channel request and possibly a translation, so they are never
        /// recomputed for updates that only change the playback position or the play state.
        /// </remarks>
        private sealed class PresenceContent
        {
            public string VideoTitle = string.Empty;
            public string ChannelTitle = string.Empty;
            public string VideoUrl = string.Empty;
            public string ChannelUrl = string.Empty;
            public string ThumbnailUrl = string.Empty;
            public double DurationSeconds;
        }

        private readonly object presenceRequestLock = new object();

        private PresenceRequest? pendingPresenceRequest;
        private bool presenceBuildRunning;

        // Only touched by the single background presence worker.
        private PresenceContent? cachedPresenceContent;
        private string? cachedPresenceContentKey;

        private void updatePresence(DiscordRichPresenceMode mode)
        {
            if (discordRPC == null)
                return;

            // Callers may run on other threads, so the snapshot is taken on the update thread.
            Schedule(() =>
            {
                queuePresenceRequest(new PresenceRequest(
                    mode,
                    videoData,
                    videoPlaying.Value,
                    currentVideoSource != null ? currentVideoSource.VideoProgress.Value : 0,
                    playbackSpeed.Value,
                    DateTime.UtcNow,
                    buildPresenceContentKey()));
            });
        }

        /// <summary>
        /// The key the resolved presence content is cached under. It covers everything the resolved
        /// titles depend on, so changing a related setting rebuilds them exactly once.
        /// </summary>
        private string buildPresenceContentKey()
        {
            if (videoData == null)
                return string.Empty;

            return $"{videoData.Id}|{localeBindable.Value}|{usernameDisplayMode.Value}|{config.Get<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource)}";
        }

        private void queuePresenceRequest(PresenceRequest request)
        {
            lock (presenceRequestLock)
            {
                // Only the newest state matters: a request that arrived while the worker was busy is
                // replaced instead of being built and sent one after another.
                pendingPresenceRequest = request;

                if (presenceBuildRunning)
                    return;

                presenceBuildRunning = true;
            }

            Task.Run(processPresenceRequests).FireAndForget();
        }

        /// <summary>
        /// Drains the queued presence requests on a background thread until nothing is left, so a
        /// burst of calls (seeking, skipping, settings) costs a single worker.
        /// </summary>
        private void processPresenceRequests()
        {
            while (true)
            {
                PresenceRequest request;

                lock (presenceRequestLock)
                {
                    if (pendingPresenceRequest == null)
                    {
                        presenceBuildRunning = false;
                        return;
                    }

                    request = pendingPresenceRequest.Value;
                    pendingPresenceRequest = null;
                }

                try
                {
                    applyPresence(request);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"[Discord] Failed to build the rich presence: {e.GetDescription()}");
                }
            }
        }

        private void applyPresence(PresenceRequest request)
        {
            DiscordRPCClient? rpc = discordRPC;

            if (rpc == null)
                return;

            Video? video = request.Video;

            if (request.Mode == DiscordRichPresenceMode.Off || !request.IsPlaying || video == null)
            {
                rpc.ClearPresence();
                return;
            }

            string videoUrl = $"https://youtu.be/{video.Id}";

            DiscordRPC.Button watchButton = new DiscordRPC.Button
            {
                Label = AXPlayer_DiscordRPCStrings.WatchOnYouTube,
                Url = videoUrl,
            };

            if (request.Mode == DiscordRichPresenceMode.Limited)
            {
                rpc.UpdatePresence(new RichPresence
                {
                    Type = ActivityType.Watching,
                    State = AXPlayer_DiscordRPCStrings.WatchingVideo,
                    Timestamps = new Timestamps { Start = request.CapturedAtUtc },
                    Assets = new Assets { LargeImageKey = "ax_player_new" },
                    Buttons = [watchButton],
                });

                return;
            }

            PresenceContent content = getOrCreatePresenceContent(request);

            // The speed is only announced where the presence advertises the video itself.
            bool showVideoInfo = request.Mode == DiscordRichPresenceMode.Full;

            Assets assets = new Assets
            {
                LargeImageUrl = content.VideoUrl,
                SmallImageText = "AX Player",
                SmallImageKey = "ax_player_new",
            };

            if (!string.IsNullOrEmpty(content.ThumbnailUrl))
                assets.LargeImageKey = content.ThumbnailUrl;

            RichPresence presence = new RichPresence
            {
                Name = "YouTube",
                Type = ActivityType.Watching,
                Details = DiscordPresenceContent.FormatDetails(content.VideoTitle, showVideoInfo ? request.PlaybackSpeed : 1),
                State = DiscordPresenceContent.TruncateWithEllipsis(content.ChannelTitle, DiscordPresenceContent.MaximumLength),
                StatusDisplay = StatusDisplayType.Details,
                DetailsUrl = content.VideoUrl,
                Assets = assets,
                Buttons = [watchButton],
            };

            if (!string.IsNullOrEmpty(content.ChannelUrl))
                presence.StateUrl = content.ChannelUrl;

            if (showVideoInfo && DiscordPresenceContent.TryResolveTimestamps(request.ProgressSeconds, content.DurationSeconds, request.PlaybackSpeed, request.CapturedAtUtc, out DateTime start, out DateTime end))
                presence.Timestamps = new Timestamps { Start = start, End = end };

            rpc.UpdatePresence(presence);
        }

        private PresenceContent getOrCreatePresenceContent(PresenceRequest request)
        {
            PresenceContent? cached = cachedPresenceContent;

            if (cached != null && string.Equals(cachedPresenceContentKey, request.ContentKey, StringComparison.Ordinal))
                return cached;

            Video video = request.Video!;

            PresenceContent content = new PresenceContent
            {
                VideoTitle = api.GetLocalizedVideoTitle(video),
                VideoUrl = $"https://youtu.be/{video.Id}",
                ThumbnailUrl = video.Snippet?.Thumbnails?.High?.Url ?? string.Empty,
            };

            if (DiscordPresenceContent.TryParseDurationSeconds(video.ContentDetails?.Duration, out double durationSeconds))
                content.DurationSeconds = durationSeconds;

            string? channelId = video.Snippet?.ChannelId;

            if (!string.IsNullOrWhiteSpace(channelId))
            {
                // A single lookup instead of the three the previous version performed per update.
                Channel? channel = api.TryGetChannel(channelId);

                if (channel != null)
                {
                    content.ChannelTitle = api.GetLocalizedChannelTitle(channel) ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(channel.Id))
                        content.ChannelUrl = $"https://www.youtube.com/channel/{channel.Id}";
                }
            }

            cachedPresenceContentKey = request.ContentKey;
            cachedPresenceContent = content;

            return content;
        }
    }
}

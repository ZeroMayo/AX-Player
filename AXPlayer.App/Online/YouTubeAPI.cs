// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Localisation;
using AXPlayer.App.Utils;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using YoutubeExplode.Exceptions;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// The YouTube API layer of the player.
    /// </summary>
    /// <remarks>
    /// Everything is backed by the internal Innertube API instead of the quota-limited
    /// YouTube Data API v3. See <c>YouTubeAPI.Innertube.cs</c> for the transport and parsers.
    /// </remarks>
    public partial class YouTubeAPI
    {
        private GoogleTranslate translateApi;
        private GoogleOAuth2 googleOAuth2;

        private FrameworkConfigManager frameworkConfig;
        private AXPlayerConfigManager appConfig;
        private AXPlayerAppBase app;

        public YouTubeAPI(AXPlayerAppBase app, FrameworkConfigManager frameworkConfig, GoogleTranslate translateApi, AXPlayerConfigManager appConfig, GoogleOAuth2 googleOAuth2, bool isTestClient, HttpClient httpClient)
        {
            Http = httpClient;
            this.app = app;
            this.frameworkConfig = frameworkConfig;
            this.translateApi = translateApi;
            this.appConfig = appConfig;
            this.googleOAuth2 = googleOAuth2;

            // The resolved own channel belongs to the account that was signed in, so cache
            // invalidation is tied to the sign-in state instead of the application lifetime.
            googleOAuth2.SignedIn.BindValueChanged(_ =>
            {
                mineChannelIdCache = null;
                mineChannelCache = null;
            });

            Logger.Log(@"Google Translate, YouTube Innertube API loaded");
        }

        public Channel? TryGetChannel(string channelId)
            => GetChannel(channelId) ?? null;

        public Channel? GetChannel(string channelId)
            => runSync(() => fetchChannelAsync(channelId, CancellationToken.None));

        public bool GetChannelExistsViaHandle(string handle)
            => runSync(() => fetchChannelExistsViaHandleAsync(handle, CancellationToken.None));

        public Channel? TryToGetMineChannel()
        {
            if (!googleOAuth2.SignedIn.Value)
                return null;

            return GetMineChannel();
        }

        public Channel GetMineChannel()
            => runSync(GetMineChannelAsync);

        public async Task<Channel> GetMineChannelAsync()
        {
            Channel? channel = await fetchMineChannelAsync(CancellationToken.None).ConfigureAwait(false);

            return channel ?? new Channel
            {
                Id = string.Empty,
                Kind = @"youtube#channel",
                Snippet = new ChannelSnippet
                {
                    Title = string.Empty,
                    Description = string.Empty,
                    Thumbnails = new ThumbnailDetails(),
                },
                Statistics = new ChannelStatistics(),
                Localizations = new Dictionary<string, ChannelLocalization>(StringComparer.Ordinal),
            };
        }

        public void SendComment(string videoId, string commentText)
            => runSync(() => sendCommentAsync(videoId, commentText, CancellationToken.None));

        public Task UpdatePlaylistInfo(string playlistId, string title, PrivacyStatus privacyStatus)
            => updatePlaylistInfoAsync(playlistId, title, privacyStatus, CancellationToken.None);

        public IList<VideoAbuseReportReasonItem>? TryToGetVideoAbuseReportReasons()
        {
            if (!googleOAuth2.SignedIn.Value)
                return null;

            return GetVideoAbuseReportReasons();
        }

        public IList<VideoAbuseReportReasonItem> GetVideoAbuseReportReasons()
            => getBuiltInAbuseReportReasons();

        public void ReportAbuse(string videoID, string reasonId, string? secondaryReasonId = null, string? comments = null)
            => runSync(() => reportAbuseAsync(videoID, reasonId, secondaryReasonId, comments, CancellationToken.None));

        public Task<CommentThreadListResponse> GetCommentThreadAsync(string videoId, string pageToken, CommentThreadsResource.ListRequest.OrderEnum orderEnum = CommentThreadsResource.ListRequest.OrderEnum.Time)
        {
            Logger.Log($"[NekoPlayer] GetCommentThread called with params ({videoId}, {orderEnum.ToString()})");
            return fetchCommentThreadsAsync(videoId, pageToken, orderEnum, CancellationToken.None);
        }

        public IList<SearchResult> GetSearchResult(string query, SearchResource.ListRequest.OrderEnum orderEnum)
            => runSync(() => fetchSearchResultsAsync(query, orderEnum, CancellationToken.None));

        public Task<Comment> GetComment(string commentId)
            => throw new NotSupportedException(@"Fetching a single comment is not supported by the Innertube API.");

        public Task<bool> IsChannelSubscribed(string channelId)
            => isChannelSubscribedAsync(channelId, CancellationToken.None);

        public Task<string> GetSubscriptionId(string channelId)
            => getSubscriptionIdAsync(channelId, CancellationToken.None);

        public Task SubscribeChannel(string channelId)
            => subscribeChannelAsync(channelId, CancellationToken.None);

        public Task UnsubscribeChannel(string subscriptionId)
            => unsubscribeChannelAsync(subscriptionId, CancellationToken.None);

        public string GetLocalizedChannelTitleDisplayBoth(Channel channel)
        {
            string language = frameworkConfig.Get<string>(FrameworkSetting.Locale);

            if (!string.IsNullOrEmpty(channel.Snippet.CustomUrl))
            {
                try
                {
                    return channel.Localizations.Where(locale => locale.Key.Contains(language)).First().Value.Title.Normalize() + $" ({channel.Snippet.CustomUrl})";
                }
                catch
                {
                    return channel.Snippet.Title.Normalize() + $" ({channel.Snippet.CustomUrl})";
                }
            }

            try
            {
                return channel.Localizations.Where(locale => locale.Key.Contains(language)).First().Value.Title.Normalize();
            }
            catch
            {
                return channel.Snippet.Title.Normalize();
            }
        }

        public string GetLocalizedChannelTitleOnlyOne(Channel channel)
        {
            string language = frameworkConfig.Get<string>(FrameworkSetting.Locale);

            try
            {
                return channel.Localizations.Where(locale => locale.Key.Contains(language)).First().Value.Title.Normalize();
            }
            catch
            {
                return channel.Snippet.Title.Normalize();
            }
        }

        public string GetLocalizedChannelTitle(Channel channel, bool displayBoth = false, bool forceUsernameDisplay = false)
        {
            if (channel == null)
                return string.Empty;

            if (displayBoth)
            {
                if (appConfig.Get<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource) == VideoMetadataTranslateSource.YouTube)
                {
                    return GetLocalizedChannelTitleDisplayBoth(channel);
                }

                try
                {
                    string originalTitle = channel.Snippet.Title.Normalize();
                    string translatedTitle = translateApi.Translate(originalTitle, GoogleTranslateLanguage.auto);

                    if (!string.IsNullOrEmpty(channel.Snippet.CustomUrl))
                        return translatedTitle + $" ({channel.Snippet.CustomUrl})";

                    return translatedTitle;
                }
                catch
                {
                    if (!string.IsNullOrEmpty(channel.Snippet.CustomUrl))
                        return channel.Snippet.Title + $" ({channel.Snippet.CustomUrl})";

                    return channel.Snippet.Title;
                }
            }

            if (appConfig.Get<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode) == UsernameDisplayMode.DisplayName)
            {
                if (appConfig.Get<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource) == VideoMetadataTranslateSource.YouTube)
                {
                    return GetLocalizedChannelTitleOnlyOne(channel);
                }

                try
                {
                    string originalTitle = channel.Snippet.Title.Normalize();
                    string translatedTitle = translateApi.Translate(originalTitle, GoogleTranslateLanguage.auto);
                    return translatedTitle;
                }
                catch
                {
                    return channel.Snippet.Title;
                }
            }

            if (forceUsernameDisplay)
                return GetLocalizedChannelTitleOnlyOne(channel);

            if (!string.IsNullOrEmpty(channel.Snippet.CustomUrl))
                return channel.Snippet.CustomUrl;

            return GetLocalizedChannelTitleOnlyOne(channel);
        }

        public string ParseCaptionLanguage(ClosedCaptionLanguage captionLanguage)
        {
            switch (captionLanguage)
            {
                case ClosedCaptionLanguage.Disabled:
                {
                    return string.Empty;
                }
                case ClosedCaptionLanguage.English:
                {
                    return "en";
                }
                case ClosedCaptionLanguage.Korean:
                {
                    return "ko";
                }
                case ClosedCaptionLanguage.Japanese:
                {
                    return "ja";
                }
            }
            return string.Empty;
        }

        public string GetLocalizedVideoTitle(Video video)
        {
            if (video == null)
                return string.Empty;

            if (appConfig.Get<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource) == VideoMetadataTranslateSource.GoogleTranslate)
            {
                try
                {
                    string originalTitle = video.Snippet.Title.Normalize();
                    string translatedTitle = translateApi.Translate(originalTitle, GoogleTranslateLanguage.auto);
                    return translatedTitle;
                }
                catch
                {
                    return video.Snippet.Title.Normalize();
                }
            }

            string language = frameworkConfig.Get<string>(FrameworkSetting.Locale);

            try
            {
                return video.Localizations[language].Title.Normalize();
            }
            catch
            {
                return video.Snippet.Title.Normalize();
            }
        }

        public string GetLocalizedVideoDescription(Video video)
        {
            if (video == null)
                return string.Empty;

            if (appConfig.Get<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource) == VideoMetadataTranslateSource.GoogleTranslate)
            {
                try
                {
                    string originalDescription = video.Snippet.Description.Normalize();
                    string translatedDescription = translateApi.Translate(originalDescription, GoogleTranslateLanguage.auto);
                    return translatedDescription;
                }
                catch
                {
                    return video.Snippet.Description.Normalize();
                }
            }

            string language = frameworkConfig.Get<string>(FrameworkSetting.Locale);

            try
            {
                return video.Localizations[language].Description.Normalize();
            }
            catch
            {
                return video.Snippet.Description.Normalize();
            }
        }

        public Video GetVideo(string videoId)
            => runSync(() => fetchVideoAsync(videoId, CancellationToken.None))
               ?? throw new InvalidOperationException($@"The video '{videoId}' could not be loaded from YouTube.");

        public Playlist GetPlaylistInfo(string playlistId)
            => runSync(() => fetchPlaylistInfoAsync(playlistId, CancellationToken.None));

        public Task<IList<Playlist>> GetMyPlaylistItemsAsync()
            => fetchMyPlaylistsAsync(CancellationToken.None);

        public IList<Playlist> GetMyPlaylistItems()
            => runSync(() => fetchMyPlaylistsAsync(CancellationToken.None));

        private string? _visitorData;

        protected HttpClient Http { get; }

        private async ValueTask<string> ResolveVisitorDataAsync(
            CancellationToken cancellationToken = default
        )
        {
            if (!string.IsNullOrWhiteSpace(_visitorData))
                return _visitorData;

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://www.youtube.com/sw.js_data"
            );

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            request.Headers.Add(
                "User-Agent",
                $"NekoPlayer/{(app.IsDeployedBuild ? app.Version : "Development")}"
            );

            if (googleOAuth2.SignedIn.Value)
            {
                request.Headers.Add(
                    "Authorization",
                    $"Bearer {googleOAuth2.GetAccessToken()}"
                );
            }

            using var response = await Http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // TODO: move this to a bridge wrapper
            var jsonString = await response.Content.ReadAsStringAsync(cancellationToken);
            if (jsonString.StartsWith(")]}'"))
                jsonString = jsonString[4..];

            var json = Json.Parse(jsonString);

            // This is just an ordered (but unstructured) blob of data
            var value = json[0][2][0][0][13].GetStringOrNull();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new YoutubeExplodeException("Failed to resolve visitor data.");
            }

            return _visitorData = value;
        }

        public async Task SendPlayerResponseAsync(
            string videoId,
            CancellationToken cancellationToken = default
        )
        {
            var visitorData = await ResolveVisitorDataAsync(cancellationToken);

            // The most optimal client to impersonate is any mobile client, because they
            // don't require signature deciphering (for both normal and n-parameter signatures).
            // YouTube now requires Proof of Origin (PO) tokens for most Innertube clients (iOS, Android, etc.),
            // causing stream downloads to fail with 403 Forbidden errors. The ANDROID_VR client (Oculus Quest)
            // still works without PO tokens and provides full format access.
            // https://github.com/Tyrrrz/YoutubeExplode/issues/933
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://www.youtube.com/youtubei/v1/player"
            );

            request.Content = new StringContent(
                // lang=json
                $$"""
            {
              "videoId": {{Json.Encode(videoId)}},
              "contentCheckOk": true,
              "context": {
                "client": {
                  "clientName": "ANDROID_VR",
                  "clientVersion": "1.60.19",
                  "deviceMake": "Oculus",
                  "deviceModel": "Quest 3",
                  "osName": "Android",
                  "osVersion": "12L",
                  "platform": "MOBILE",
                  "visitorData": {{Json.Encode(visitorData)}},
                  "hl": "en",
                  "gl": "US",
                  "utcOffsetMinutes": 0
                }
              }
            }
            """
            );

            // User agent appears to be sometimes required when impersonating Android
            // https://github.com/iv-org/invidious/issues/3230#issuecomment-1226887639
            request.Headers.Add(
                "User-Agent",
                "com.google.android.apps.youtube.vr.oculus/1.60.19 (Linux; U; Android 12L; Quest 3 Build/SQ3A.220605.009.A1) gzip"
            );

            if (googleOAuth2.SignedIn.Value)
            {
                request.Headers.Add(
                    "Authorization",
                    $"Bearer {googleOAuth2.GetAccessToken()}"
                );
            }

            using var response = await Http.SendAsync(request, cancellationToken);

            Logger.Log(response.ToString());

            response.EnsureSuccessStatusCode();
        }

        public Task SaveVideoToPlaylist(string playlistId, string videoId)
            => saveVideoToPlaylistAsync(playlistId, videoId, CancellationToken.None);

        public Task RemoveVideoFromPlaylist(string playlistId, string videoId)
            => removeVideoFromPlaylistAsync(playlistId, videoId, CancellationToken.None);

        public Task<bool> IsVideoExistsOnPlaylist(string playlistId, string videoId)
            => isVideoExistsOnPlaylistAsync(playlistId, videoId, CancellationToken.None);

        public string ParsePrivacyStatus(PrivacyStatus privacyStatus)
        {
            switch (privacyStatus)
            {
                case PrivacyStatus.Public:
                    return "public";
                case PrivacyStatus.Unlisted:
                    return "unlisted";
                case PrivacyStatus.Private:
                    return "private";
            }
            return string.Empty;
        }

        public Task AddPlaylist(string playlistTitle, PrivacyStatus privacyStatus)
            => addPlaylistAsync(playlistTitle, privacyStatus, CancellationToken.None);

        public Task<IList<PlaylistItem>> GetPlaylistItems(string playlistId)
            => fetchPlaylistItemsAsync(playlistId, CancellationToken.None);

        public Task<VideosResource.RateRequest.RatingEnum> GetVideoRating(string videoId)
            => getVideoRatingAsync(videoId, CancellationToken.None);

        public Task RateVideo(string videoId, VideosResource.RateRequest.RatingEnum videoRating)
            => rateVideoAsync(videoId, videoRating, CancellationToken.None);
    }
}

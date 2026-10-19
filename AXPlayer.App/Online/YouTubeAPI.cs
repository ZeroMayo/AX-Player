// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Crypto.AES;
using Google.Apis.Services;
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
    public partial class YouTubeAPI
    {
        private YouTubeService youtubeService;
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
            var apiKey = isTestClient ? "K/1395zhx/B49AZcHQpAUn5HZSBGtbLrAHnY3QGYieBQpx0gOkZdL5xDPUB7+BnM" : "3T8gSwQR7sprXV/OZDZyTCqbT9Qrt/j8xd7prlHrFMh4Y8Dsp4H2HG+eu+UJ7FOb";

            using (AES aes_1 = new AES("youtube-player-ex"))
            {
                string decryptedKey = aes_1.Decrypt("faF4XblghnrR37EA3y5Aag==");
                using (AES aes = new AES(decryptedKey))
                {
                    string decryptedApiKey = aes.Decrypt(apiKey);

                    youtubeService = new YouTubeService(new BaseClientService.Initializer()
                    {
                        ApiKey = decryptedApiKey,
                        ApplicationName = GetType().ToString()
                    });
                }
            }

            Logger.Log($"Google Translate, YouTube API loaded");
        }

        public Channel? TryGetChannel(string channelId)
            => GetChannel(channelId) ?? null;

        public Channel? GetChannel(string channelId)
        {
            var part = "statistics,snippet,brandingSettings,id,localizations,topicDetails";
            var request = youtubeService.Channels.List(part);

            request.Id = channelId;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = request.Execute();

            var result = response.Items.First();

            if (result != null)
                return result;

            return null;
        }

        public bool GetChannelExistsViaHandle(string handle)
        {
            var part = "statistics,snippet,brandingSettings,id,localizations";
            var request = youtubeService.Channels.List(part);

            request.ForHandle = handle;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = request.Execute();

            if (response.Items.Count == 0)
                return false;

            return true;
        }

        public IList<I18nLanguage> GetAvailableLanguages()
        {
            var part = "snippet";
            var request = youtubeService.I18nLanguages.List(part);

            var response = request.Execute();

            var result = response.Items;

            return result;
        }

        public Channel? TryToGetMineChannel()
        {
            if (!googleOAuth2.SignedIn.Value)
                return null;

            return GetMineChannel();
        }

        public Channel GetMineChannel()
        {
            var part = "statistics,snippet,brandingSettings,id,localizations";
            var request = youtubeService.Channels.List(part);

            request.AccessToken = googleOAuth2.GetAccessToken();

            request.Mine = true;

            var response = request.Execute();

            var result = response.Items.First();

            return result;
        }

        public async Task<Channel> GetMineChannelAsync()
        {
            var part = "statistics,snippet,brandingSettings,id,localizations";
            var request = youtubeService.Channels.List(part);

            request.AccessToken = googleOAuth2.GetAccessToken();

            request.Mine = true;

            var response = await request.ExecuteAsync();

            var result = response.Items.First();

            return result;
        }

        public void SendComment(string videoId, string commentText)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            var part = "snippet";
            var request = youtubeService.CommentThreads.Insert(new CommentThread
            {
                Snippet = new CommentThreadSnippet
                {
                    VideoId = videoId,
                    TopLevelComment = new Comment
                    {
                        Snippet = new CommentSnippet
                        {
                            TextOriginal = commentText
                        }
                    }
                }
            }, part);

            request.AccessToken = googleOAuth2.GetAccessToken();

            request.Execute();
        }

        public async Task UpdatePlaylistInfo(string playlistId, string title, PrivacyStatus privacyStatus)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            string parsedPrivacyStatus = ParsePrivacyStatus(privacyStatus);

            var part = "id,snippet,status";
            var request = youtubeService.Playlists.Update(new Playlist
            {
                Id = playlistId,
                Snippet = new()
                {
                    Title = title,
                },
                Status = new()
                {
                    PrivacyStatus = parsedPrivacyStatus,
                }
            }, part);

            request.AccessToken = googleOAuth2.GetAccessToken();

            await request.ExecuteAsync();
        }

        public IList<VideoAbuseReportReasonItem>? TryToGetVideoAbuseReportReasons()
        {
            if (!googleOAuth2.SignedIn.Value)
                return null;

            return GetVideoAbuseReportReasons();
        }

        public void ReportAbuse(string videoID, string reasonId, string? secondaryReasonId = null, string? comments = null)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            if (secondaryReasonId != null)
            {
                if (comments != null)
                {
                    var request = youtubeService.Videos.ReportAbuse(new VideoAbuseReport
                    {
                        VideoId = videoID,
                        ReasonId = reasonId,
                        SecondaryReasonId = secondaryReasonId,
                        Comments = comments,
                        Language = CultureInfo.CurrentCulture.Name,
                    });

                    request.AccessToken = googleOAuth2.GetAccessToken();
                    request.Execute();
                }
                else
                {
                    var request = youtubeService.Videos.ReportAbuse(new VideoAbuseReport
                    {
                        VideoId = videoID,
                        ReasonId = reasonId,
                        SecondaryReasonId = secondaryReasonId,
                        Language = CultureInfo.CurrentCulture.Name,
                    });

                    request.AccessToken = googleOAuth2.GetAccessToken();
                    request.Execute();
                }
            }
            else
            {
                if (comments != null)
                {
                    var request = youtubeService.Videos.ReportAbuse(new VideoAbuseReport
                    {
                        VideoId = videoID,
                        ReasonId = reasonId,
                        Comments = comments,
                        Language = CultureInfo.CurrentCulture.Name,
                    });

                    request.AccessToken = googleOAuth2.GetAccessToken();
                    request.Execute();
                }
                else
                {
                    var request = youtubeService.Videos.ReportAbuse(new VideoAbuseReport
                    {
                        VideoId = videoID,
                        ReasonId = reasonId,
                        Language = CultureInfo.CurrentCulture.Name,
                    });

                    request.AccessToken = googleOAuth2.GetAccessToken();
                    request.Execute();
                }
            }
        }

        public IList<VideoAbuseReportReasonItem> GetVideoAbuseReportReasons()
        {
            var part = "snippet";
            var request = youtubeService.VideoAbuseReportReasons.List(part);

            request.AccessToken = googleOAuth2.GetAccessToken();
            request.Hl = frameworkConfig.Get<string>(FrameworkSetting.Locale);

            Logger.Log($"Using access token {googleOAuth2.GetAccessToken()}");
            Logger.Log($"called GetVideoAbuseReportReasons()");

            var response = request.Execute();

            IList<VideoAbuseReportReasonItem> result = new List<VideoAbuseReportReasonItem>();

            try
            {
                foreach (VideoAbuseReportReason videoAbuseReportReason in response.Items)
                {
                    VideoAbuseReportReasonItem item = new VideoAbuseReportReasonItem
                    {
                        Id = videoAbuseReportReason.Id,
                        Label = videoAbuseReportReason.Snippet.Label,
                        ContainsSecondaryReasons = (videoAbuseReportReason.Snippet.SecondaryReasons != null)
                    };
                    Logger.Log($"Created new item instance: (Id: {videoAbuseReportReason.Id}, Label: {videoAbuseReportReason.Snippet.Label})");

                    if (videoAbuseReportReason.Snippet.SecondaryReasons != null)
                    {
                        foreach (VideoAbuseReportSecondaryReason item1 in videoAbuseReportReason.Snippet.SecondaryReasons)
                        {
                            VideoAbuseReportReasonItem item2 = new VideoAbuseReportReasonItem
                            {
                                Id = item1.Id,
                                Label = item1.Label
                            };
                            Logger.Log($"Created new sub item instance: (Id: {item1.Id}, Label: {item1.Label})");

                            item.SecondaryReasons.Add(item2);
                            Logger.Log($"SecondaryReasons added: {item1.Label}");
                        }
                    }

                    result.Add(item);
                    Logger.Log($"Reasons added: {item.Label}");
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }

            Logger.Log($"Reasons all added!");
            return result;
        }

        public CommentThreadListResponse GetCommentThread(string videoId, string pageToken, CommentThreadsResource.ListRequest.OrderEnum orderEnum = CommentThreadsResource.ListRequest.OrderEnum.Time)
        {
            Logger.Log($"[NekoPlayer] GetCommentThread called with params ({videoId}, {orderEnum.ToString()})");

            var part = "snippet,replies";
            var request = youtubeService.CommentThreads.List(part);

            request.MaxResults = 10;
            request.VideoId = videoId;
            request.Order = orderEnum;

            if (!string.IsNullOrEmpty(pageToken))
                request.PageToken = pageToken;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = request.Execute();

            return response;
        }

        public IList<SearchResult> GetSearchResult(string query, SearchResource.ListRequest.OrderEnum orderEnum)
        {
            //calling search on youtube api is 100 units is so intresting
            var part = "snippet";
            var request = youtubeService.Search.List(part);

            request.MaxResults = 50; // <------ why 50? dues to quota limits
            request.Q = query;

            request.Order = orderEnum;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = request.Execute();

            var result = response.Items;

            return result;
        }

        public async Task<Comment> GetComment(string commentId)
        {
            var part = "snippet";
            var request = youtubeService.Comments.List(part);

            request.Id = commentId;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            var result = response.Items.First();

            return result;
        }

        public async Task<bool> IsChannelSubscribed(string channelId)
        {
            if (googleOAuth2.SignedIn.Value == false)
                return false;

            var part = "snippet";
            var request = youtubeService.Subscriptions.List(part);

            request.ForChannelId = channelId;
            request.Mine = true;
            request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            if (response.Items.Count > 0)
                return true;
            else
                return false;
        }

        public async Task<string> GetSubscriptionId(string channelId)
        {
            if (googleOAuth2.SignedIn.Value == false)
                return string.Empty;

            var part = "snippet";
            var request = youtubeService.Subscriptions.List(part);

            request.ForChannelId = channelId;
            request.Mine = true;
            request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            if (response.Items.Count > 0)
                return response.Items.First().Id;
            else
                return string.Empty;
        }

        public async Task SubscribeChannel(string channelId)
        {
            if (googleOAuth2.SignedIn.Value == false)
                return;

            var body = new Subscription
            {
                Snippet = new()
                {
                    ResourceId = new()
                    {
                        Kind = "youtube#channel",
                        ChannelId = channelId
                    }
                }
            };

            var part = "snippet";
            var request = youtubeService.Subscriptions.Insert(body, part);

            request.AccessToken = googleOAuth2.GetAccessToken();

            await request.ExecuteAsync();

            return;
        }

        public async Task UnsubscribeChannel(string subscriptionId)
        {
            if (googleOAuth2.SignedIn.Value == false)
                return;

            var request = youtubeService.Subscriptions.Delete(subscriptionId);

            request.AccessToken = googleOAuth2.GetAccessToken();

            await request.ExecuteAsync();

            return;
        }

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
            else
            {
                try
                {
                    return channel.Localizations.Where(locale => locale.Key.Contains(language)).First().Value.Title.Normalize();
                }
                catch
                {
                    return channel.Snippet.Title.Normalize();
                }
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
                else
                {
                    try
                    {
                        string originalTitle = channel.Snippet.Title.Normalize();
                        string translatedTitle = translateApi.Translate(originalTitle, GoogleTranslateLanguage.auto);

                        if (!string.IsNullOrEmpty(channel.Snippet.CustomUrl))
                            return translatedTitle + $" ({channel.Snippet.CustomUrl})";
                        else
                            return translatedTitle;
                    }
                    catch
                    {
                        if (!string.IsNullOrEmpty(channel.Snippet.CustomUrl))
                            return channel.Snippet.Title + $" ({channel.Snippet.CustomUrl})";
                        else
                            return channel.Snippet.Title;
                    }
                }
            }

            if (appConfig.Get<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode) == UsernameDisplayMode.DisplayName)
            {
                if (appConfig.Get<VideoMetadataTranslateSource>(AXPlayerSetting.VideoMetadataTranslateSource) == VideoMetadataTranslateSource.YouTube)
                {
                    return GetLocalizedChannelTitleOnlyOne(channel);
                }
                else
                {
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
            }
            else
            {
                if (forceUsernameDisplay)
                    return GetLocalizedChannelTitleOnlyOne(channel);

                if (!string.IsNullOrEmpty(channel.Snippet.CustomUrl))
                    return channel.Snippet.CustomUrl;
                else
                    return GetLocalizedChannelTitleOnlyOne(channel);
            }
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
        {
            var part = "statistics,snippet,localizations,contentDetails,status,topicDetails";
            var request = youtubeService.Videos.List(part);

            request.Id = videoId;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = request.Execute();

            var result = response.Items.First();

            return result;
        }

        public Playlist GetPlaylistInfo(string playlistId)
        {
            var part = "snippet,status";
            var request = youtubeService.Playlists.List(part);

            request.Id = playlistId;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = request.Execute();

            var result = response.Items.First();

            return result;
        }

        public async Task<IList<Playlist>> GetMyPlaylistItemsAsync()
        {
            if (!googleOAuth2.SignedIn.Value)
                return new List<Playlist>();

            var part = "snippet,status";
            var request = youtubeService.Playlists.List(part);

            request.Mine = true;

            request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            var result = response.Items;

            return result;
        }

        public IList<Playlist> GetMyPlaylistItems()
        {
            if (!googleOAuth2.SignedIn.Value)
                return new List<Playlist>();

            var part = "snippet,status";
            var request = youtubeService.Playlists.List(part);

            request.Mine = true;

            request.AccessToken = googleOAuth2.GetAccessToken();

            var response = request.Execute();

            var result = response.Items;

            return result;
        }

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
                var plainTextBytes = Encoding.UTF8.GetBytes(googleOAuth2.GetAccessToken());

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
                var plainTextBytes = Encoding.UTF8.GetBytes(googleOAuth2.GetAccessToken());

                request.Headers.Add(
                    "Authorization",
                    $"Bearer {googleOAuth2.GetAccessToken()}"
                );
            }

            using var response = await Http.SendAsync(request, cancellationToken);

            Logger.Log(response.ToString());

            response.EnsureSuccessStatusCode();
        }

        public async Task SaveVideoToPlaylist(string playlistId, string videoId)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            PlaylistItem item = new PlaylistItem
            {
                Snippet = new()
                {
                    PlaylistId = playlistId,
                    ResourceId = new()
                    {
                        Kind = "youtube#video",
                        VideoId = videoId,
                    }
                }
            };

            var part = "snippet";
            var request = youtubeService.PlaylistItems.Insert(item, part);

            request.AccessToken = googleOAuth2.GetAccessToken();

            await request.ExecuteAsync();
        }

        public async Task RemoveVideoFromPlaylist(string playlistId, string videoId)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            var part = "snippet";
            var request = youtubeService.PlaylistItems.List(part);

            request.PlaylistId = playlistId;
            request.VideoId = videoId;

            request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            var result = response.Items.First();

            var request2 = youtubeService.PlaylistItems.Delete(result.Id);

            request2.OauthToken = googleOAuth2.GetAccessToken();

            await request2.ExecuteAsync();
        }

        public async Task<bool> IsVideoExistsOnPlaylist(string playlistId, string videoId)
        {
            if (!googleOAuth2.SignedIn.Value)
                return false;

            var part = "snippet";
            var request = youtubeService.PlaylistItems.List(part);

            request.PlaylistId = playlistId;
            request.VideoId = videoId;

            request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            var result = response.Items;

            return result.Count > 0;
        }

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

        public async Task AddPlaylist(string playlistTitle, PrivacyStatus privacyStatus)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            Playlist item = new Playlist
            {
                Snippet = new()
                {
                    Title = playlistTitle,
                },
                Status = new()
                {
                    PrivacyStatus = ParsePrivacyStatus(privacyStatus),
                }
            };

            var part = "snippet,status";
            var request = youtubeService.Playlists.Insert(item, part);

            request.AccessToken = googleOAuth2.GetAccessToken();

            await request.ExecuteAsync();
        }

        public async Task<IList<PlaylistItem>> GetPlaylistItems(string playlistId)
        {
            var part = "snippet,contentDetails";
            var request = youtubeService.PlaylistItems.List(part);

            request.MaxResults = 25; // <------ why 25? dues to quota limits
            request.PlaylistId = playlistId;

            if (googleOAuth2.SignedIn.Value == true)
                request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            var result = response.Items;

            return result;
        }

        public async Task<VideosResource.RateRequest.RatingEnum> GetVideoRating(string videoId)
        {
            if (!googleOAuth2.SignedIn.Value)
                return VideosResource.RateRequest.RatingEnum.None;

            var request = youtubeService.Videos.GetRating(videoId);

            request.AccessToken = googleOAuth2.GetAccessToken();

            var response = await request.ExecuteAsync();

            var result = response.Items.First();

            switch (result.Rating)
            {
                case "none":
                {
                    return VideosResource.RateRequest.RatingEnum.None;
                }
                case "like":
                {
                    return VideosResource.RateRequest.RatingEnum.Like;
                }
                case "dislike":
                {
                    return VideosResource.RateRequest.RatingEnum.Dislike;
                }
                default:
                {
                    return VideosResource.RateRequest.RatingEnum.None;
                }
            }
        }

        public async Task RateVideo(string videoId, VideosResource.RateRequest.RatingEnum videoRating)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            var request = youtubeService.Videos.Rate(videoId, videoRating);

            request.AccessToken = googleOAuth2.GetAccessToken();

            await request.ExecuteAsync();
        }
    }
}

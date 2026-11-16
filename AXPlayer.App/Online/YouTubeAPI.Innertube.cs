// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Extensions;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// The Innertube (internal YouTube web API) implementation of <see cref="YouTubeAPI" />.
    /// </summary>
    /// <remarks>
    /// Every public method of the API is backed by an Innertube endpoint instead of the
    /// quota-limited YouTube Data API v3. The Google DTOs are still used as the transport
    /// objects between the API layer and the rest of the application, but they are now
    /// populated from Innertube JSON responses.
    /// </remarks>
    public partial class YouTubeAPI
    {
        private enum innertube_client
        {
            Web,
            Tv,
        }

        /// <summary>
        /// The words that a relative timestamp is rendered with, per interface language, paired with
        /// the unit they stand for (0 = second, 1 = minute, 2 = hour, 3 = day, 4 = week, 5 = month,
        /// 6 = year). The words of a shorter unit have to be matched first because some of them are a
        /// substring of a longer one.
        /// </summary>
        private static readonly (string[] Keywords, int Unit)[] relative_date_units =
        {
            (new[] { @"second", @"sec", @"초", @"秒", @"sekunde", @"seconde", @"segundo", @"secondo" }, 0),
            (new[] { @"minute", @"min", @"분", @"分", @"minuto" }, 1),
            (new[] { @"hour", @"시간", @"時間", @"小时", @"小時", @"stunde", @"heure", @"hora", @"ora" }, 2),
            (new[] { @"day", @"일", @"日", @"天", @"tag", @"jour", @"día", @"dia", @"giorno" }, 3),
            (new[] { @"week", @"주", @"週", @"周", @"woche", @"semaine", @"semana", @"settimana" }, 4),
            (new[] { @"month", @"개월", @"달", @"か月", @"ヶ月", @"カ月", @"个月", @"個月", @"monat", @"mois", @"mese", @"mes" }, 5),
            (new[] { @"year", @"년", @"年", @"jahr", @"año", @"ano", @"anno" }, 6),
        };

        private const string youtube_origin = @"https://www.youtube.com";
        private const string music_topic_category = @"https://en.wikipedia.org/wiki/Music";

        /// <summary>
        /// The words that precede the subscriber count of a channel, in the languages the page can be
        /// rendered in. The count itself is read from the page header (see
        /// <see cref="findCountWithKeyword" />).
        /// </summary>
        private static readonly string[] subscriber_count_keywords = { @"subscriber", @"구독자", @"登録者", @"チャンネル登録", @"abonn", @"подписчик", @"订阅", @"訂閱" };

        /// <summary>
        /// The words that follow the video count of a channel, in the languages the page can be
        /// rendered in.
        /// </summary>
        private static readonly string[] video_count_keywords = { @"video", @"동영상", @"動画", @"vídeo", @"vidéo", @"видео", @"视频", @"影片" };

        private readonly Dictionary<string, bool> subscriptionStateCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, VideosResource.RateRequest.RatingEnum> ratingStateCache = new Dictionary<string, VideosResource.RateRequest.RatingEnum>(StringComparer.Ordinal);

        private string? mineChannelIdCache;
        private Channel? mineChannelCache;

        /// <summary>
        /// The playlists read from the library tabs, keyed by ID. They carry the title and thumbnail
        /// of the playlist, which are used as a fallback when the playlist page cannot be loaded
        /// (for example for an unlisted playlist of another account).
        /// </summary>
        private readonly Dictionary<string, Playlist> playlistLibraryCache = new Dictionary<string, Playlist>(StringComparer.Ordinal);

        /// <summary>
        /// The "your playlists" library pages, paired with the client that natively serves them.
        /// </summary>
        /// <remarks>
        /// <c>FEplaylists</c> is a web page; the TV client answers it with a different
        /// (recommendation-style) page, so it has to be requested as the web client.
        /// <c>FEmy_youtube</c> is the TV library page.
        /// </remarks>
        private static readonly (string BrowseId, innertube_client Client)[] playlist_library_pages =
        {
            (@"FEmy_youtube", innertube_client.Tv),
            (@"FEplaylists", innertube_client.Web),
        };

        /// <summary>
        /// Upper bound for the playlist library continuations, so a broken chain cannot keep the
        /// library fetching forever.
        /// </summary>
        private const int playlist_library_max_pages = 10;

        /// <summary>
        /// The amount of search results a single <c>GetSearchResult</c> call collects, matching what
        /// the Data API returned before the API layer moved to Innertube.
        /// </summary>
        private const int search_max_results = 50;

        private const int search_max_pages = 5;

        /// <summary>
        /// The upper bound of the pages that are fetched for a single playlist. A page holds up to a
        /// hundred items, so this covers playlists of up to two thousand items.
        /// </summary>
        private const int playlist_item_max_pages = 20;

        /// <summary>
        /// Endpoints that expose the identities of the signed-in account.
        /// </summary>
        private static readonly string[] own_channel_account_endpoints =
        {
            @"account/account_menu",
            @"account/accounts_list",
        };

        /// <summary>
        /// Prefixes the watch page renders in front of the publish date.
        /// </summary>
        private static readonly string[] date_text_prefixes =
        {
            @"Streamed live on ",
            @"Started streaming on ",
            @"Streamed live ",
            @"Premiered on ",
            @"Premiered ",
            @"Scheduled for ",
            @"Published on ",
            @"Uploaded on ",
        };

        #region transport

        private async Task<JsonDocument?> sendInnertubeAsync(string endpoint, object payload, CancellationToken cancellationToken, innertube_client client = innertube_client.Web, bool authenticate = false, string? dumpTag = null)
        {
            bool tv = client == innertube_client.Tv;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $@"{youtube_origin}/youtubei/v1/{endpoint}?key={innertube_api_key}&prettyPrint=false");

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, @"application/json");
                request.Headers.TryAddWithoutValidation(@"User-Agent", tv ? tv_browse_user_agent : web_user_agent);
                request.Headers.TryAddWithoutValidation(@"Accept-Language", @"en-US,en;q=0.9");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Name", tv ? @"7" : @"1");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Version", tv ? tv_browse_client_version : web_client_version);
                request.Headers.TryAddWithoutValidation(@"X-Goog-Api-Key", innertube_api_key);
                request.Headers.TryAddWithoutValidation(@"Origin", youtube_origin);

                await addAuthenticationHeadersAsync(request, authenticate, cancellationToken).ConfigureAwait(false);

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    if (body.Length > 400)
                        body = body[..400];

                    Logger.Log($@"Innertube request to '{endpoint}' ({(tv ? @"TVHTML5" : @"WEB")}) failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}", LoggingTarget.Network);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                if (innertube_dump_enabled)
                    return await dumpAndParseResponseAsync(endpoint, dumpTag, stream, cancellationToken).ConfigureAwait(false);

                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Innertube request to '{endpoint}' failed: {exception.GetDescription()}");
                return null;
            }
        }

        /// <summary>
        /// Whether raw Innertube responses are written to <c>%TEMP%/axplayer-innertube</c>.
        /// The dumps are always written; <c>AXPLAYER_INNERTUBE_DUMP=0</c> turns them off.
        /// </summary>
        private static readonly bool innertube_dump_enabled = shouldDumpInnertube(Environment.GetEnvironmentVariable(@"AXPLAYER_INNERTUBE_DUMP"));

        /// <summary>
        /// Reads whether the dumps are enabled from the value of the <c>AXPLAYER_INNERTUBE_DUMP</c>
        /// environment variable. Only an explicit <c>0</c> disables them.
        /// </summary>
        internal static bool shouldDumpInnertube(string? environmentValue)
            => !string.Equals(environmentValue?.Trim(), @"0", StringComparison.Ordinal);

        /// <summary>
        /// The tag that marks the dumps of the comment requests in their file name.
        /// </summary>
        internal const string comments_dump_tag = @"comments";

        /// <summary>
        /// Builds the file name of a dumped response, for example
        /// <c>20261115-074244-461-next (comments).json</c>.
        /// </summary>
        internal static string buildDumpFileName(string endpoint, string? dumpTag, DateTime utcNow)
        {
            string name = $@"{utcNow:yyyyMMdd-HHmmss-fff}-{endpoint.Replace('/', '_')}";

            if (!string.IsNullOrEmpty(dumpTag))
                name += $@" ({dumpTag})";

            return name + @".json";
        }

        private static async Task<JsonDocument> dumpAndParseResponseAsync(string endpoint, string? dumpTag, Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                string directory = Path.Combine(Path.GetTempPath(), @"axplayer-innertube");
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, buildDumpFileName(endpoint, dumpTag, DateTime.UtcNow));
                File.WriteAllText(path, json);

                Logger.Log($@"Dumped the Innertube response for '{endpoint}' to '{path}'.", LoggingTarget.Network);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to dump the Innertube response for '{endpoint}': {exception.GetDescription()}");
            }

            return JsonDocument.Parse(json);
        }

        private async Task<JsonDocument?> sendTvInnertubeAsync(string endpoint, object payload, CancellationToken cancellationToken)
            => await sendInnertubeAsync(endpoint, payload, cancellationToken, innertube_client.Tv, googleOAuth2.SignedIn.Value).ConfigureAwait(false);

        /// <summary>
        /// Sends an authenticated request as the TV client, then retries as the web client when
        /// the endpoint is not supported by the TV client.
        /// </summary>
        private async Task<JsonDocument?> sendAuthenticatedInnertubeAsync(string endpoint, Func<innertube_client, object> payloadFactory, CancellationToken cancellationToken, string? dumpTag = null)
        {
            JsonDocument? document = await sendInnertubeAsync(endpoint, payloadFactory(innertube_client.Tv), cancellationToken, innertube_client.Tv, true, dumpTag).ConfigureAwait(false);

            if (document != null)
                return document;

            return await sendInnertubeAsync(endpoint, payloadFactory(innertube_client.Web), cancellationToken, innertube_client.Web, true, dumpTag).ConfigureAwait(false);
        }

        /// <summary>
        /// Browses an account-bound page with the preferred client, falling back to the other one.
        /// </summary>
        private async Task<JsonDocument?> browseAuthenticatedAsync(string browseId, CancellationToken cancellationToken, innertube_client preferred = innertube_client.Tv)
        {
            JsonDocument? document = await browseAuthenticatedWithClientAsync(browseId, preferred, cancellationToken).ConfigureAwait(false);

            if (document != null)
                return document;

            innertube_client fallback = preferred == innertube_client.Tv ? innertube_client.Web : innertube_client.Tv;

            return await browseAuthenticatedWithClientAsync(browseId, fallback, cancellationToken).ConfigureAwait(false);
        }

        private async Task<JsonDocument?> browseAuthenticatedWithClientAsync(string browseId, innertube_client client, CancellationToken cancellationToken)
            => await sendInnertubeAsync(@"browse", new Dictionary<string, object?>
            {
                [@"browseId"] = browseId,
                [@"context"] = client == innertube_client.Tv ? createTvBrowseContext() : createWebClientContext(null),
            }, cancellationToken, client, true).ConfigureAwait(false);

        private async Task addAuthenticationHeadersAsync(HttpRequestMessage request, bool authenticate, CancellationToken cancellationToken)
        {
            if (!authenticate || !googleOAuth2.SignedIn.Value)
                return;

            string accessToken = await googleOAuth2.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(accessToken))
                return;

            request.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {accessToken}");
            request.Headers.TryAddWithoutValidation(@"X-Goog-AuthUser", @"0");
            request.Headers.TryAddWithoutValidation(@"X-Youtube-Bootstrap-Logged-In", @"true");
        }

        private object createWebClientContext(string? visitorData, string? interfaceLanguage = null)
        {
            var client = new Dictionary<string, object?>
            {
                [@"clientName"] = @"WEB",
                [@"clientVersion"] = web_client_version,
                [@"hl"] = string.IsNullOrEmpty(interfaceLanguage) ? @"en" : interfaceLanguage,
                [@"gl"] = @"US",
            };

            if (!string.IsNullOrWhiteSpace(visitorData))
                client[@"visitorData"] = visitorData;

            return new { client };
        }

        private async Task<string?> tryResolveVisitorDataAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await ResolveVisitorDataAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to resolve visitor data: {exception.GetDescription()}");
                return null;
            }
        }

        #endregion

        #region video

        private async Task<Video?> fetchVideoAsync(string videoId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            string? visitorData = await tryResolveVisitorDataAsync(cancellationToken).ConfigureAwait(false);

            // The player response is requested with the original interface language so the snippet
            // keeps the original title/description, while the watch-next response is requested in
            // the player's language to expose the translated title/description (localizations).
            string? interfaceLanguage = getInterfaceLanguage();

            Task<JsonDocument?> playerTask = sendInnertubeAsync(@"player", new Dictionary<string, object?>
            {
                [@"videoId"] = videoId,
                [@"contentCheckOk"] = true,
                [@"racyCheckOk"] = true,
                [@"context"] = createWebClientContext(visitorData),
            }, cancellationToken);

            Task<JsonDocument?> nextTask = sendInnertubeAsync(@"next", new Dictionary<string, object?>
            {
                [@"videoId"] = videoId,
                [@"context"] = createWebClientContext(visitorData, interfaceLanguage),
            }, cancellationToken);

            await Task.WhenAll(playerTask, nextTask).ConfigureAwait(false);

            using JsonDocument? player = await playerTask.ConfigureAwait(false);
            using JsonDocument? next = await nextTask.ConfigureAwait(false);

            if (player == null)
                return null;

            Video? video = parseVideo(player.RootElement, videoId);

            if (video == null)
                return null;

            if (next != null)
            {
                applyWatchMetadata(video, next.RootElement);
                applyFullDescription(video, next.RootElement);
            }

            applyPublishedAt(video, player.RootElement, next?.RootElement);
            applyLiveStreamingDetails(video, player.RootElement, next?.RootElement);
            applyLocalizations(video, next?.RootElement, interfaceLanguage, getFrameworkLocale());

            return video;
        }

        /// <summary>
        /// Returns the primary language subtag of the framework locale (for example <c>ko</c> for
        /// <c>ko-KR</c>), which is what the Innertube <c>hl</c> parameter expects.
        /// </summary>
        private string? getInterfaceLanguage()
        {
            string? locale = getFrameworkLocale();

            if (string.IsNullOrWhiteSpace(locale))
                return null;

            int separator = locale.IndexOfAny(new[] { '-', '_' });

            return separator > 0 ? locale[..separator] : locale;
        }

        private string? getFrameworkLocale()
        {
            try
            {
                return frameworkConfig.Get<string>(FrameworkSetting.Locale);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to read the framework locale: {exception.GetDescription()}");
                return null;
            }
        }

        internal static Video? parseVideo(JsonElement root, string videoId)
        {
            if (!root.TryGetProperty(@"videoDetails", out JsonElement details) || details.ValueKind != JsonValueKind.Object)
                return null;

            string title = getString(details, @"title") ?? string.Empty;
            string description = getString(details, @"shortDescription") ?? string.Empty;
            string? channelId = getString(details, @"channelId");
            string? author = getString(details, @"author");

            bool isLiveContent = getBool(details, @"isLiveContent");
            bool isLiveNow = getBool(details, @"isLive");

            JsonElement? microformat = root.TryGetProperty(@"microformat", out JsonElement microformatRoot)
                                        && microformatRoot.TryGetProperty(@"playerMicroformatRenderer", out JsonElement renderer)
                ? renderer
                : null;

            // shortDescription is truncated for long descriptions, while the microformat copy is
            // usually the complete text.
            if (microformat != null)
            {
                string? microformatDescription = extractText(getObject(microformat.Value, @"description") ?? default);

                if (!string.IsNullOrEmpty(microformatDescription) && microformatDescription.Length > description.Length)
                    description = microformatDescription;
            }

            string? publishDate = microformat != null ? getString(microformat.Value, @"publishDate") : null;
            string? uploadDate = microformat != null ? getString(microformat.Value, @"uploadDate") : null;
            string? dateRaw = publishDate ?? uploadDate;
            DateTimeOffset? published = parseDateTimeOffset(dateRaw);

            string? defaultLanguage = getDefaultAudioLanguage(root);

            var video = new Video
            {
                Id = videoId,
                Kind = @"youtube#video",
                ETag = null,
                Snippet = new VideoSnippet
                {
                    Title = title,
                    Description = description,
                    ChannelId = channelId,
                    ChannelTitle = author,
                    Thumbnails = createVideoThumbnails(videoId),
                    PublishedAtRaw = dateRaw,
                    PublishedAtDateTimeOffset = published,
                    LiveBroadcastContent = isLiveContent ? (isLiveNow ? @"live" : @"none") : @"none",
                    DefaultLanguage = defaultLanguage,
                    DefaultAudioLanguage = defaultLanguage,
                    Localized = new VideoLocalization
                    {
                        Title = title,
                        Description = description,
                    },
                },
                ContentDetails = new VideoContentDetails
                {
                    Duration = toIsoDuration(getDouble(details, @"lengthSeconds")),
                    Caption = root.TryGetProperty(@"captions", out _) ? @"true" : @"false",
                    LicensedContent = false,
                    Projection = @"rectangular",
                },
                Statistics = new VideoStatistics
                {
                    ViewCount = parseRoughCount(getString(details, @"viewCount")),
                },
                TopicDetails = new VideoTopicDetails
                {
                    TopicCategories = new List<string>(),
                },
                Localizations = new Dictionary<string, VideoLocalization>(StringComparer.Ordinal),
            };

            applyLiveStreamingDetails(video, root, null);

            bool categoryIsMusic = microformat != null
                                   && string.Equals(getString(microformat.Value, @"category"), @"Music", StringComparison.OrdinalIgnoreCase);

            if (categoryIsMusic)
                video.TopicDetails.TopicCategories.Add(music_topic_category);

            return video;
        }

        internal static void applyWatchMetadata(Video video, JsonElement root)
        {
            // Comment count is only exposed when the uploader allows comments, mirroring the
            // behaviour of the Data API (null comment count == comments disabled).
            ulong? commentCount = null;

            foreach (JsonElement header in enumerateRenderers(root, @"commentsEntryPointHeaderRenderer"))
            {
                if (header.TryGetProperty(@"commentCount", out JsonElement countElement))
                {
                    commentCount = parseRoughCount(extractText(countElement));

                    if (commentCount != null)
                        break;
                }
            }

            if (commentCount == null)
            {
                foreach (JsonElement value in enumeratePropertyValues(root, @"commentCount"))
                {
                    commentCount = parseRoughCount(extractText(value));

                    if (commentCount != null)
                        break;
                }
            }

            video.Statistics.CommentCount = commentCount;

            ulong? likeCount = findLikeCount(root);

            if (likeCount != null)
                video.Statistics.LikeCount = likeCount;

            if (video.TopicDetails.TopicCategories.Count == 0 && hasMusicCategoryRow(root))
                video.TopicDetails.TopicCategories.Add(music_topic_category);
        }

        private static ulong? findLikeCount(JsonElement root)
        {
            string[] names = { @"likeCount", @"likeCountIfLiked", @"likeCountIfNotLiked", @"expandedLikeCountIfLiked", @"expandedLikeCountIfDisliked" };

            foreach (string name in names)
            {
                foreach (JsonElement value in enumeratePropertyValues(root, name))
                {
                    ulong? parsed = parseUlong(value);

                    if (parsed != null)
                        return parsed;
                }
            }

            // The current watch page only renders the like count into the button's text.
            foreach (JsonElement likeButton in enumerateRenderers(root, @"likeButtonViewModel"))
            {
                foreach (string propertyName in new[] { @"accessibilityText", @"title" })
                {
                    foreach (JsonElement value in enumeratePropertyValues(likeButton, propertyName))
                    {
                        string? text = extractText(value);

                        if (string.IsNullOrEmpty(text)
                            || !(text.Contains(@"like", StringComparison.OrdinalIgnoreCase)
                                 || text.Contains(@"좋아요", StringComparison.Ordinal)
                                 || text.Contains(@"高評価", StringComparison.Ordinal)))
                            continue;

                        ulong? count = parseRoughCount(text);

                        if (count != null)
                            return count;
                    }
                }
            }

            return null;
        }

        private static bool hasMusicCategoryRow(JsonElement root)
        {
            foreach (JsonElement metadataRow in enumerateRenderers(root, @"metadataRowRenderer"))
            {
                if (!metadataRow.TryGetProperty(@"title", out JsonElement titleElement))
                    continue;

                string title = extractText(titleElement) ?? string.Empty;

                if (!title.Equals(@"Category", StringComparison.OrdinalIgnoreCase)
                    && !title.Equals(@"카테고리", StringComparison.Ordinal)
                    && !title.Equals(@"カテゴリ", StringComparison.Ordinal))
                    continue;

                if (!metadataRow.TryGetProperty(@"contents", out JsonElement contents) || contents.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement content in contents.EnumerateArray())
                {
                    string value = extractText(content) ?? string.Empty;

                    if (string.Equals(value, @"Music", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value, @"음악", StringComparison.Ordinal)
                        || string.Equals(value, @"音楽", StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Fills <see cref="Video.LiveStreamingDetails"/> from the player and watch-next responses.
        /// </summary>
        /// <remarks>
        /// The Data API returned these values as a single <c>liveStreamingDetails</c> object, while
        /// Innertube spreads them over the response: the broadcast timestamps live in
        /// <c>microformat.liveBroadcastDetails</c>, an upcoming stream exposes its start in
        /// <c>playabilityStatus</c> and the watch-next response carries the live viewer count.
        /// </remarks>
        internal static void applyLiveStreamingDetails(Video video, JsonElement playerRoot, JsonElement? nextRoot)
        {
            JsonElement details = getObject(playerRoot, @"videoDetails") ?? default;
            JsonElement liveBroadcast = getNestedObject(playerRoot, @"microformat", @"playerMicroformatRenderer", @"liveBroadcastDetails") ?? default;

            JsonElement offlineSlate = getNestedObject(playerRoot, @"playabilityStatus", @"liveStreamability", @"liveStreamabilityRenderer", @"offlineSlate",
                @"liveStreamOfflineSlateRenderer") ?? default;

            JsonElement playability = getObject(playerRoot, @"playabilityStatus") ?? default;

            JsonElement primaryInfo = nextRoot != null ? firstRenderer(nextRoot.Value, @"videoPrimaryInfoRenderer") ?? default : default;
            JsonElement upcoming = getObject(primaryInfo, @"upcomingEventData") ?? default;
            JsonElement viewerCountRenderer = getNestedObject(primaryInfo, @"viewCount", @"videoViewCountRenderer") ?? default;

            bool isLiveNow = getBool(details, @"isLive")
                             || getBool(liveBroadcast, @"isLiveNow")
                             || getBool(viewerCountRenderer, @"isLive");

            DateTimeOffset? broadcastStart = parseDateTimeOffset(getString(liveBroadcast, @"startTimestamp"));
            DateTimeOffset? broadcastEnd = parseDateTimeOffset(getString(liveBroadcast, @"endTimestamp"));

            // An upcoming stream has no broadcast timestamps yet, only the (re)scheduled start.
            DateTimeOffset? scheduledStart = parseEpochSeconds(getString(offlineSlate, @"scheduledStartTime"))
                                             ?? parseEpochSeconds(getString(upcoming, @"startTime"));

            bool isUpcoming = !isLiveNow
                              && broadcastEnd == null
                              && (offlineSlate.ValueKind == JsonValueKind.Object
                                  || upcoming.ValueKind == JsonValueKind.Object
                                  || string.Equals(getString(playability, @"status"), @"LIVE_STREAM_OFFLINE", StringComparison.Ordinal));

            bool isLiveContent = getBool(details, @"isLiveContent")
                                 || liveBroadcast.ValueKind == JsonValueKind.Object
                                 || isUpcoming;

            if (!isLiveContent)
            {
                video.LiveStreamingDetails = null;
                return;
            }

            DateTimeOffset? actualStart = null;
            DateTimeOffset? actualEnd = null;
            ulong? concurrentViewers = null;

            if (isLiveNow)
            {
                actualStart = broadcastStart ?? scheduledStart;

                concurrentViewers = parseRoughCount(getString(details, @"viewCount"))
                                    ?? parseRoughCount(extractText(getObject(viewerCountRenderer, @"viewCount") ?? viewerCountRenderer));
            }
            else if (isUpcoming)
            {
                // Only the schedule is known before the broadcast starts.
                scheduledStart ??= broadcastStart;
            }
            else
            {
                actualStart = broadcastStart ?? scheduledStart;

                if (broadcastEnd != null)
                {
                    actualEnd = broadcastEnd;
                }
                else if (actualStart != null)
                {
                    // A finished stream keeps the length of the VOD, which is the stream duration.
                    double lengthSeconds = getDouble(details, @"lengthSeconds");

                    if (lengthSeconds > 0)
                        actualEnd = actualStart.Value.AddSeconds(lengthSeconds);
                }
            }

            // YouTube treats the schedule of a broadcast that already started as its start time.
            scheduledStart ??= actualStart;

            video.LiveStreamingDetails = new VideoLiveStreamingDetails
            {
                ActualStartTimeRaw = formatTimestamp(actualStart),
                ActualStartTimeDateTimeOffset = actualStart,
                ActualEndTimeRaw = formatTimestamp(actualEnd),
                ActualEndTimeDateTimeOffset = actualEnd,
                ScheduledStartTimeRaw = formatTimestamp(scheduledStart),
                ScheduledStartTimeDateTimeOffset = scheduledStart,
                ConcurrentViewers = isLiveNow ? concurrentViewers : null,
            };
        }

        private static DateTimeOffset? parseEpochSeconds(string? raw)
        {
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long seconds) || seconds <= 0)
                return null;

            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        private static string? formatTimestamp(DateTimeOffset? value)
            => value?.ToString(@"o", CultureInfo.InvariantCulture);

        /// <summary>
        /// Replaces the truncated player description with the full description of the watch page,
        /// with every link resolved to its target URL.
        /// </summary>
        internal static void applyFullDescription(Video video, JsonElement nextRoot)
        {
            if (video.Snippet == null)
                return;

            string? description = extractFullDescription(nextRoot);

            if (string.IsNullOrEmpty(description))
                return;

            // The rendered description of the watch page is authoritative, but the fallbacks can be a
            // preview, so those never replace a longer description.
            if (description.Length < (video.Snippet.Description?.Length ?? 0) && findProperty(nextRoot, @"attributedDescription") == null)
                return;

            video.Snippet.Description = description;

            if (video.Snippet.Localized != null)
                video.Snippet.Localized.Description = description;
        }

        /// <summary>
        /// Extracts the full description of a watch page, including the target of each embedded link.
        /// </summary>
        internal static string? extractFullDescription(JsonElement root)
        {
            foreach (JsonElement secondaryInfo in enumerateRenderers(root, @"videoSecondaryInfoRenderer"))
            {
                if (getObject(secondaryInfo, @"attributedDescription") is { } attributed
                    && getString(attributed, @"content") is { Length: > 0 } content)
                    return expandDescriptionLinks(content, attributed);

                if (getObject(secondaryInfo, @"description") is { } description
                    && buildDescriptionFromRuns(description) is { Length: > 0 } descriptionText)
                    return descriptionText;
            }

            foreach (JsonElement body in enumerateRenderers(root, @"expandableVideoDescriptionBodyRenderer"))
            {
                if (getObject(body, @"descriptionBodyText") is { } bodyText
                    && buildDescriptionFromRuns(bodyText) is { Length: > 0 } bodyDescription)
                    return bodyDescription;
            }

            return null;
        }

        /// <summary>
        /// Replaces the abbreviated links of an <c>attributedDescription</c> with the URL they point
        /// at. The label of a link that is not a URL itself (a hashtag, a channel name, ...) is kept
        /// verbatim.
        /// </summary>
        private static string expandDescriptionLinks(string content, JsonElement attributedDescription)
        {
            if (!attributedDescription.TryGetProperty(@"commandRuns", out JsonElement commandRuns) || commandRuns.ValueKind != JsonValueKind.Array)
                return content;

            var links = new List<(int Start, int Length, string Replacement)>();

            foreach (JsonElement run in commandRuns.EnumerateArray())
            {
                int start = getInt(run, @"startIndex");
                int length = getInt(run, @"length");

                if (start < 0 || length <= 0 || start + length > content.Length)
                    continue;

                string? url = findDescriptionLink(run);

                if (string.IsNullOrEmpty(url))
                    continue;

                if (!tryResolveTruncatedUrl(content.Substring(start, length), url, out string replacement))
                    continue;

                links.Add((start, length, replacement));
            }

            if (links.Count == 0)
                return content;

            var builder = new StringBuilder(content);

            // Replace from the end so the earlier indices stay valid.
            for (int i = links.Count - 1; i >= 0; i--)
            {
                builder.Remove(links[i].Start, links[i].Length);
                builder.Insert(links[i].Start, links[i].Replacement);
            }

            return builder.ToString();
        }

        private static string? buildDescriptionFromRuns(JsonElement description)
        {
            if (getString(description, @"simpleText") is { Length: > 0 } simpleText)
                return simpleText;

            if (!description.TryGetProperty(@"runs", out JsonElement runs) || runs.ValueKind != JsonValueKind.Array)
                return null;

            var builder = new StringBuilder();

            foreach (JsonElement run in runs.EnumerateArray())
            {
                string text = getString(run, @"text") ?? string.Empty;
                string? url = findDescriptionLink(run);

                if (!string.IsNullOrEmpty(url) && tryResolveTruncatedUrl(text, url, out string replacement))
                    builder.Append(replacement);
                else
                    builder.Append(text);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Resolves the placeholder of an abbreviated link (for example
        /// <c>https://example.com/shopdetail...</c>) into the URL it points at.
        /// </summary>
        /// <returns><see langword="true" /> when the text of the link has to be replaced.</returns>
        private static bool tryResolveTruncatedUrl(string text, string url, out string replacement)
        {
            replacement = string.Empty;

            int start = 0;

            while (start < text.Length && char.IsWhiteSpace(text[start]))
                start++;

            int end = text.Length;

            while (end > start && char.IsWhiteSpace(text[end - 1]))
                end--;

            if (end - start < 9)
                return false;

            string label = text[start..end];

            // Hashtags and other inline links keep the label they are rendered with.
            if (label.StartsWith('#') || label.StartsWith('＃') || !isUrlLabel(label))
                return false;

            if (string.Equals(label, url, StringComparison.Ordinal))
                return false;

            // The rendered label is cut off with an ellipsis, or is a prefix of the target URL.
            string trimmed = label.TrimEnd('.', '…');
            bool truncated = trimmed.Length != label.Length || (url.Length > trimmed.Length && url.StartsWith(trimmed, StringComparison.Ordinal));

            if (!truncated)
                return false;

            replacement = string.Concat(text.AsSpan(0, start), url, text.AsSpan(end));
            return true;
        }

        private static bool isUrlLabel(string label)
            => label.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase)
               || label.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase)
               || label.StartsWith(@"www.", StringComparison.OrdinalIgnoreCase);

        private static string? findDescriptionLink(JsonElement element)
        {
            string? url = null;

            JsonElement? urlEndpoint = findProperty(element, @"urlEndpoint");

            if (urlEndpoint != null)
                url = getString(urlEndpoint.Value, @"url");

            if (string.IsNullOrEmpty(url))
            {
                JsonElement? webCommandMetadata = findProperty(element, @"webCommandMetadata");

                if (webCommandMetadata != null)
                    url = getString(webCommandMetadata.Value, @"url");
            }

            return normalizeDescriptionUrl(url);
        }

        private static string? normalizeDescriptionUrl(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // Links of a description are wrapped in a youtube.com/redirect URL, which carries the
            // real target in its q parameter.
            if (url.Contains(@"/redirect", StringComparison.OrdinalIgnoreCase))
            {
                string? target = getQueryParameter(url, @"q");

                if (!string.IsNullOrEmpty(target))
                    return Uri.UnescapeDataString(target);
            }

            return url.StartsWith(@"/", StringComparison.Ordinal) ? youtube_origin + url : url;
        }

        private static string? getQueryParameter(string url, string name)
        {
            int queryStart = url.IndexOf('?');

            if (queryStart < 0)
                return null;

            foreach (string pair in url[(queryStart + 1)..].Split('&'))
            {
                int separator = pair.IndexOf('=');

                if (separator <= 0)
                    continue;

                if (string.Equals(pair[..separator], name, StringComparison.Ordinal))
                    return pair[(separator + 1)..];
            }

            return null;
        }

        private static int getInt(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
                return -1;

            return value.ValueKind switch
            {
                JsonValueKind.Number => value.TryGetInt32(out int number) ? number : -1,
                JsonValueKind.String => int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : -1,
                _ => -1,
            };
        }

        /// <summary>
        /// Resolves the publish timestamp of a video from the player microformat, falling back to the
        /// watch-next primary info when the microformat does not carry one.
        /// </summary>
        internal static void applyPublishedAt(Video video, JsonElement playerRoot, JsonElement? nextRoot)
        {
            if (video.Snippet == null)
                return;

            JsonElement microformat = getNestedObject(playerRoot, @"microformat", @"playerMicroformatRenderer") ?? default;

            string? raw = getString(microformat, @"publishDate")
                          ?? getString(microformat, @"uploadDate")
                          ?? getString(getObject(microformat, @"liveBroadcastDetails") ?? default, @"startTimestamp");

            DateTimeOffset? published = parseDateTimeOffset(raw);

            if (published == null && nextRoot != null)
            {
                JsonElement primaryInfo = firstRenderer(nextRoot.Value, @"videoPrimaryInfoRenderer") ?? default;

                published = parseDateText(extractText(getObject(primaryInfo, @"dateText") ?? default))
                            ?? parseRelativeDate(extractText(getObject(primaryInfo, @"relativeDateText") ?? default));

                if (published != null)
                    raw = formatTimestamp(published);
            }

            if (published == null)
                return;

            video.Snippet.PublishedAtRaw = raw;
            video.Snippet.PublishedAtDateTimeOffset = published;
        }

        /// <summary>
        /// Parses the human readable date rendered by the watch page (for example
        /// <c>Streamed live on May 1, 2024</c> or <c>May 1, 2024</c>).
        /// </summary>
        internal static DateTimeOffset? parseDateText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string candidate = text.Trim();

            foreach (string prefix in date_text_prefixes)
            {
                if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                candidate = candidate[prefix.Length..].Trim();
                break;
            }

            // Drop a trailing note, e.g. "May 1, 2024 (edited)".
            int noteStart = candidate.IndexOf('(');

            if (noteStart > 0)
                candidate = candidate[..noteStart].Trim();

            if (DateTimeOffset.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset value))
                return value;

            // Localized dates are not always parseable with the invariant culture (for example
            // "2024. 5. 1."), so the numeric form is read directly as a last resort.
            Match numeric = Regex.Match(candidate, @"(\d{4})[.\-/]\s*(\d{1,2})[.\-/]\s*(\d{1,2})");

            if (!numeric.Success
                || !int.TryParse(numeric.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)
                || !int.TryParse(numeric.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int month)
                || !int.TryParse(numeric.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
                return null;

            return month is >= 1 and <= 12 && day is >= 1 and <= 31
                ? new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero)
                : null;
        }

        /// <summary>
        /// Adds the default language and the interface language entries to <see cref="Video.Localizations"/>.
        /// </summary>
        /// <remarks>
        /// The Data API returned every uploader-provided translation at once. Innertube only localizes
        /// the watch-next response to the requested <c>hl</c>, so the translation of the player's
        /// language is collected here and the snippet stays in the original language.
        /// </remarks>
        internal static void applyLocalizations(Video video, JsonElement? nextRoot, string? interfaceLanguage, string? locale)
        {
            var localizations = video.Localizations ?? new Dictionary<string, VideoLocalization>(StringComparer.Ordinal);

            string? defaultLanguage = video.Snippet?.DefaultLanguage;

            if (!string.IsNullOrEmpty(defaultLanguage) && video.Snippet?.Localized != null)
                addLocalization(localizations, defaultLanguage, video.Snippet.Localized.Title, video.Snippet.Localized.Description);

            if (nextRoot == null || string.IsNullOrEmpty(interfaceLanguage))
            {
                video.Localizations = localizations;
                return;
            }

            JsonElement primaryInfo = firstRenderer(nextRoot.Value, @"videoPrimaryInfoRenderer") ?? default;
            string? localizedTitle = extractText(getObject(primaryInfo, @"title") ?? default);

            if (string.IsNullOrEmpty(localizedTitle))
            {
                video.Localizations = localizations;
                return;
            }

            // The localized entry carries the same expanded description as the snippet, so that a
            // translated title never brings back the truncated text with abbreviated links.
            string? localizedDescription = extractFullDescription(nextRoot.Value);

            localizedDescription ??= video.Snippet?.Description;

            addLocalization(localizations, interfaceLanguage, localizedTitle, localizedDescription);

            // GetLocalizedVideoTitle() looks the translation up with the full framework locale.
            if (!string.IsNullOrEmpty(locale) && !string.Equals(locale, interfaceLanguage, StringComparison.OrdinalIgnoreCase))
                addLocalization(localizations, locale, localizedTitle, localizedDescription);

            video.Localizations = localizations;
        }

        private static void addLocalization(IDictionary<string, VideoLocalization> localizations, string language, string? title, string? description)
        {
            if (string.IsNullOrEmpty(title))
                return;

            localizations[language] = new VideoLocalization
            {
                Title = title,
                Description = description,
            };
        }

        private static string? getDefaultAudioLanguage(JsonElement root)
        {
            if (!root.TryGetProperty(@"captions", out JsonElement captions)
                || !captions.TryGetProperty(@"playerCaptionsTracklistRenderer", out JsonElement tracklist))
                return null;

            if (!tracklist.TryGetProperty(@"captionTracks", out JsonElement tracks) || tracks.ValueKind != JsonValueKind.Array)
                return null;

            int defaultAudioIndex = 0;

            if (tracklist.TryGetProperty(@"defaultAudioTrackIndex", out JsonElement indexElement) && indexElement.TryGetInt32(out int parsedIndex))
                defaultAudioIndex = parsedIndex;

            if (tracklist.TryGetProperty(@"audioTracks", out JsonElement audioTracks)
                && audioTracks.ValueKind == JsonValueKind.Array
                && defaultAudioIndex >= 0
                && defaultAudioIndex < audioTracks.GetArrayLength())
            {
                JsonElement audioTrack = audioTracks[defaultAudioIndex];

                if (audioTrack.TryGetProperty(@"audioTrackId", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String)
                {
                    string? audioTrackId = idElement.GetString();

                    if (!string.IsNullOrEmpty(audioTrackId))
                        return audioTrackId.Split('.')[0];
                }

                if (audioTrack.TryGetProperty(@"captionTrackIndices", out JsonElement indices)
                    && indices.ValueKind == JsonValueKind.Array
                    && indices.GetArrayLength() > 0
                    && indices[0].TryGetInt32(out int captionIndex)
                    && captionIndex >= 0
                    && captionIndex < tracks.GetArrayLength())
                {
                    return getString(tracks[captionIndex], @"languageCode");
                }
            }

            return tracks.GetArrayLength() > 0 ? getString(tracks[0], @"languageCode") : null;
        }

        internal static string toIsoDuration(double seconds)
        {
            int totalSeconds = (int)Math.Max(0, Math.Round(seconds));
            int hours = totalSeconds / 3600;
            int minutes = totalSeconds % 3600 / 60;
            int remainingSeconds = totalSeconds % 60;

            var builder = new StringBuilder(@"PT");

            if (hours > 0)
                builder.Append(hours).Append('H');

            if (minutes > 0 || hours > 0)
                builder.Append(minutes).Append('M');

            builder.Append(remainingSeconds).Append('S');

            return builder.ToString();
        }

        #endregion

        #region channel

        private async Task<Channel?> fetchChannelAsync(string channelId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(channelId))
                return null;

            using JsonDocument? document = await sendInnertubeAsync(@"browse", new Dictionary<string, object?>
            {
                [@"browseId"] = channelId,
                [@"context"] = createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return null;

            return parseChannel(document.RootElement, channelId);
        }

        internal static Channel? parseChannel(JsonElement root, string channelId)
        {
            if (!root.TryGetProperty(@"metadata", out JsonElement metadata)
                || !metadata.TryGetProperty(@"channelMetadataRenderer", out JsonElement channelMetadata))
                return null;

            string id = getString(channelMetadata, @"externalId") ?? channelId;
            string title = getString(channelMetadata, @"title") ?? string.Empty;
            string description = getString(channelMetadata, @"description") ?? string.Empty;
            string? customUrl = extractChannelHandle(channelMetadata, root);
            string? avatarUrl = getLastThumbnailUrl(channelMetadata.TryGetProperty(@"avatar", out JsonElement avatar) ? avatar : (JsonElement?)null);

            return new Channel
            {
                Id = id,
                Kind = @"youtube#channel",
                Snippet = new ChannelSnippet
                {
                    Title = title,
                    Description = description,
                    CustomUrl = customUrl,
                    Thumbnails = createThumbnailDetails(avatarUrl),
                    Localized = new ChannelLocalization
                    {
                        Title = title,
                        Description = description,
                    },
                },
                Statistics = new ChannelStatistics
                {
                    SubscriberCount = findCountWithKeyword(root, subscriber_count_keywords),
                    VideoCount = findCountWithKeyword(root, video_count_keywords),
                },
                ContentDetails = new ChannelContentDetails
                {
                    RelatedPlaylists = new ChannelContentDetails.RelatedPlaylistsData
                    {
                        Uploads = id.StartsWith(@"UC", StringComparison.Ordinal) ? @"UU" + id[2..] : null,
                    },
                },
                TopicDetails = new ChannelTopicDetails
                {
                    TopicCategories = new List<string>(),
                },
                Localizations = new Dictionary<string, ChannelLocalization>(StringComparer.Ordinal),
            };
        }

        private static string? extractChannelHandle(JsonElement channelMetadata, JsonElement root)
        {
            string? vanityUrl = getString(channelMetadata, @"vanityChannelUrl");

            if (!string.IsNullOrEmpty(vanityUrl))
            {
                string trimmed = vanityUrl.TrimEnd('/');
                int slash = trimmed.LastIndexOf('/');

                if (slash >= 0 && slash + 1 < trimmed.Length)
                {
                    string handle = Uri.UnescapeDataString(trimmed[(slash + 1)..]);

                    if (handle.StartsWith('@'))
                        return handle;
                }
            }

            // Newer page headers expose the handle as "@name" in the metadata rows. The channels that
            // are recommended further down the page carry their own handle, so the header is checked
            // first instead of the first handle that appears anywhere in the document.
            JsonElement? header = findPageHeader(root);

            if (header != null && findHandleInScope(header.Value) is { } headerHandle)
                return headerHandle;

            return findHandleInScope(root);
        }

        private static string? findHandleInScope(JsonElement scope)
        {
            foreach (string propertyName in new[] { @"content", @"text", @"simpleText" })
            {
                foreach (JsonElement value in enumeratePropertyValues(scope, propertyName))
                {
                    string? text = getStringValue(value) ?? extractText(value);

                    if (!string.IsNullOrEmpty(text) && text.StartsWith('@') && text.Length > 1 && !text.Contains(' '))
                        return text;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads a value that is expected to be a string.
        /// </summary>
        /// <remarks>
        /// The values of a property name are not guaranteed to be strings; the newer renderers use
        /// the same name for objects as well (for example <c>pageHeaderRenderer.content</c>), so they
        /// must not be read blindly.
        /// </remarks>
        private static string? getStringValue(JsonElement element)
            => element.ValueKind == JsonValueKind.String ? element.GetString() : null;

        private async Task<bool> fetchChannelExistsViaHandleAsync(string handle, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(handle))
                return false;

            string normalizedHandle = handle.StartsWith('@') ? handle : $@"@{handle}";

            if (!string.IsNullOrEmpty(await resolveChannelIdFromHandleAsync(normalizedHandle, cancellationToken).ConfigureAwait(false)))
                return true;

            // Fall back to fetching the channel page directly, which resolves handles server-side.
            using var request = new HttpRequestMessage(HttpMethod.Get, $@"{youtube_origin}/{normalizedHandle}?hl=en&gl=US");
            request.Headers.TryAddWithoutValidation(@"User-Agent", web_user_agent);
            request.Headers.TryAddWithoutValidation(@"Accept-Language", @"en-US,en;q=0.9");

            try
            {
                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to resolve the channel handle '{handle}': {exception.GetDescription()}");
                return false;
            }
        }

        /// <summary>
        /// Resolves the channel ID a handle (<c>@name</c>) points at.
        /// </summary>
        private async Task<string?> resolveChannelIdFromHandleAsync(string handle, CancellationToken cancellationToken)
        {
            string normalizedHandle = handle.StartsWith('@') ? handle : $@"@{handle}";

            using JsonDocument? document = await sendInnertubeAsync(@"navigation/resolve_url", new Dictionary<string, object?>
            {
                [@"url"] = $@"{youtube_origin}/{normalizedHandle}",
                [@"context"] = createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return null;

            JsonElement? browseEndpoint = findProperty(document.RootElement, @"browseEndpoint");

            if (browseEndpoint == null)
                return null;

            string? channelId = getString(browseEndpoint.Value, @"browseId");

            return !string.IsNullOrEmpty(channelId) && channelId.StartsWith(@"UC", StringComparison.Ordinal) ? channelId : null;
        }

        private async Task<Channel?> fetchMineChannelAsync(CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
            {
                mineChannelIdCache = null;
                mineChannelCache = null;
                return null;
            }

            if (mineChannelCache != null)
                return mineChannelCache;

            string? channelId = await resolveOwnChannelIdAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(channelId))
            {
                Logger.Log(@"Failed to resolve the signed-in channel.", LoggingTarget.Network);
                return null;
            }

            Channel? channel = await fetchChannelAsync(channelId, cancellationToken).ConfigureAwait(false);

            if (channel != null)
                mineChannelCache = channel;

            return channel;
        }

        /// <summary>
        /// Resolves the channel ID of the signed-in account.
        /// </summary>
        /// <remarks>
        /// The account menu is account-scoped and exposes the handle of the selected identity, so
        /// it identifies the own channel reliably. Guessing a channel from a browse page is not
        /// safe: the TV client serves a subscription-style page for <c>FEchannels</c>, which made
        /// the player resolve an unrelated channel.
        /// </remarks>
        private async Task<string?> resolveOwnChannelIdAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(mineChannelIdCache))
                return mineChannelIdCache;

            string? resolved = await resolveOwnChannelIdCoreAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(resolved))
                mineChannelIdCache = resolved;

            return resolved;
        }

        private async Task<string?> resolveOwnChannelIdCoreAsync(CancellationToken cancellationToken)
        {
            // The account menu/account list is account-scoped: it exposes the handle or the
            // channel id of the selected identity.
            foreach (string endpoint in own_channel_account_endpoints)
            {
                foreach (innertube_client client in new[] { innertube_client.Tv, innertube_client.Web })
                {
                    using JsonDocument? document = await sendInnertubeAsync(endpoint, new Dictionary<string, object?>
                    {
                        [@"context"] = client == innertube_client.Tv ? createTvBrowseContext() : createWebClientContext(null),
                    }, cancellationToken, client, true).ConfigureAwait(false);

                    if (document == null)
                        continue;

                    string? channelId = await resolveOwnChannelIdFromAccountMenuAsync(document.RootElement, cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(channelId))
                    {
                        Logger.Log($@"Resolved the signed-in channel from '{endpoint}' ({(client == innertube_client.Tv ? @"TVHTML5" : @"WEB")}).", LoggingTarget.Network);
                        return channelId;
                    }
                }
            }

            // "Your channels" page, which only lists channels owned by the account.
            using (JsonDocument? channels = await sendInnertubeAsync(@"browse", new Dictionary<string, object?>
            {
                [@"browseId"] = @"FEchannels",
                [@"context"] = createWebClientContext(null),
            }, cancellationToken, innertube_client.Web, true).ConfigureAwait(false))
            {
                if (channels != null)
                {
                    string? channelId = tryReadFirstChannelRendererId(channels.RootElement);

                    if (!string.IsNullOrEmpty(channelId))
                    {
                        Logger.Log(@"Resolved the signed-in channel from 'FEchannels'.", LoggingTarget.Network);
                        return channelId;
                    }
                }
            }

            // Innertube does not expose the channel bound to an OAuth token without cookie
            // authentication, so fall back to a single, quota-cheap Data API identity probe.
            string? dataApiChannelId = await fetchOwnChannelIdFromDataApiAsync(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(dataApiChannelId))
            {
                Logger.Log(@"Resolved the signed-in channel with the Data API identity probe.", LoggingTarget.Network);
                return dataApiChannelId;
            }

            return null;
        }

        private async Task<string?> fetchOwnChannelIdFromDataApiAsync(CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return null;

            string accessToken = await googleOAuth2.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(accessToken))
                return null;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, @"https://www.googleapis.com/youtube/v3/channels?part=id&mine=true");
                request.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {accessToken}");
                request.Headers.TryAddWithoutValidation(@"Accept", @"application/json");

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($@"The own-channel identity probe failed with status {(int)response.StatusCode}.", LoggingTarget.Network);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!document.RootElement.TryGetProperty(@"items", out JsonElement items)
                    || items.ValueKind != JsonValueKind.Array
                    || items.GetArrayLength() == 0)
                    return null;

                string? channelId = getString(items[0], @"id");

                return !string.IsNullOrEmpty(channelId) && channelId.StartsWith(@"UC", StringComparison.Ordinal) ? channelId : null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"The own-channel identity probe failed: {exception.GetDescription()}");
                return null;
            }
        }

        private async Task<string?> resolveOwnChannelIdFromAccountMenuAsync(JsonElement root, CancellationToken cancellationToken)
        {
            foreach (JsonElement accountItem in prioritizeAccountItems(root))
            {
                string? channelId = await resolveOwnChannelIdFromAccountItemAsync(accountItem, cancellationToken).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(channelId))
                    return channelId;
            }

            return null;
        }

        /// <summary>
        /// Orders the account items so the selected identity is tried first. Items that cannot
        /// be identified are never guessed at when several accounts are present.
        /// </summary>
        internal static IReadOnlyList<JsonElement> prioritizeAccountItems(JsonElement root)
        {
            var items = enumerateRenderers(root, @"accountItem").ToList();

            if (items.Count == 0)
                items = enumerateRenderers(root, @"accountItemRenderer").ToList();

            if (items.Count == 0)
                return Array.Empty<JsonElement>();

            JsonElement? selected = null;

            foreach (JsonElement accountItem in items)
            {
                if (!getBool(accountItem, @"isSelected"))
                    continue;

                selected = accountItem;
                break;
            }

            // Never fall back to another identity when the selected one is known but unreadable:
            // that would resolve the channel of a different account.
            if (selected != null)
                return new[] { selected.Value };

            List<JsonElement> identifiable = items.Where(item => tryReadChannelId(item) != null || tryReadChannelHandle(item) != null).ToList();

            if (identifiable.Count == 1)
                return new[] { identifiable[0] };

            return items.Count == 1 ? new[] { items[0] } : Array.Empty<JsonElement>();
        }

        private async Task<string?> resolveOwnChannelIdFromAccountItemAsync(JsonElement accountItem, CancellationToken cancellationToken)
        {
            string? channelId = tryReadChannelId(accountItem);

            if (!string.IsNullOrEmpty(channelId))
                return channelId;

            string? handle = tryReadChannelHandle(accountItem);

            if (string.IsNullOrEmpty(handle))
                return null;

            return await resolveChannelIdFromHandleAsync(handle, cancellationToken).ConfigureAwait(false);
        }

        internal static string? tryReadChannelId(JsonElement element)
        {
            string? directId = getString(element, @"channelId") ?? getString(element, @"externalId");

            if (!string.IsNullOrEmpty(directId) && directId.StartsWith(@"UC", StringComparison.Ordinal))
                return directId;

            foreach (JsonElement value in enumeratePropertyValues(element, @"browseId"))
            {
                string? browseId = getStringValue(value);

                if (!string.IsNullOrEmpty(browseId) && browseId.StartsWith(@"UC", StringComparison.Ordinal))
                    return browseId;
            }

            return null;
        }

        internal static string? tryReadChannelHandle(JsonElement element)
        {
            string? channelHandle = getString(element, @"channelHandle");

            if (!string.IsNullOrEmpty(channelHandle))
                return channelHandle;

            // Some responses expose the handle as a set of runs ("@" + "name").
            foreach (string propertyName in new[] { @"accountByline", @"accountName" })
            {
                if (!element.TryGetProperty(propertyName, out JsonElement node))
                    continue;

                string? text = extractText(node);

                if (!string.IsNullOrEmpty(text) && text.StartsWith('@') && text.Length > 1 && !text.Contains(' '))
                    return text;
            }

            foreach (string propertyName in new[] { @"text", @"simpleText", @"content" })
            {
                foreach (JsonElement value in enumeratePropertyValues(element, propertyName))
                {
                    string? text = getStringValue(value) ?? extractText(value);

                    if (!string.IsNullOrEmpty(text) && text.StartsWith('@') && text.Length > 1 && !text.Contains(' '))
                        return text;
                }
            }

            return null;
        }

        internal static string? tryReadFirstChannelRendererId(JsonElement root)
        {
            foreach (string rendererName in new[] { @"gridChannelRenderer", @"channelRenderer" })
            {
                foreach (JsonElement renderer in enumerateRenderers(root, rendererName))
                {
                    string? id = getString(renderer, @"channelId") ?? getBrowseId(renderer);

                    if (!string.IsNullOrEmpty(id) && id.StartsWith(@"UC", StringComparison.Ordinal))
                        return id;
                }
            }

            return null;
        }

        #endregion

        #region search

        private async Task<IList<SearchResult>> fetchSearchResultsAsync(string query, SearchResource.ListRequest.OrderEnum orderEnum, CancellationToken cancellationToken)
        {
            var results = new List<SearchResult>();

            if (string.IsNullOrWhiteSpace(query))
                return results;

            var payload = new Dictionary<string, object?>
            {
                [@"query"] = query,
                [@"context"] = createWebClientContext(null),
            };

            // Innertube encodes the search sort order as a base64 protobuf `params` value.
            switch (orderEnum)
            {
                case SearchResource.ListRequest.OrderEnum.Date:
                    payload[@"params"] = @"CAI%3D";
                    break;

                case SearchResource.ListRequest.OrderEnum.ViewCount:
                    payload[@"params"] = @"CAM%3D";
                    break;

                case SearchResource.ListRequest.OrderEnum.Rating:
                    payload[@"params"] = @"CAE%3D";
                    break;
            }

            using JsonDocument? document = await sendInnertubeAsync(@"search", payload, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return results;

            collectSearchResults(document.RootElement, results);

            // A search page holds a single shelf of results, so the rest is fetched through the
            // continuation tokens until the amount the Data API returned before is reached.
            string? continuation = tryFindContinuationToken(document.RootElement);

            for (int page = 1; continuation != null && results.Count < search_max_results && page < search_max_pages; page++)
            {
                using JsonDocument? nextPage = await sendInnertubeAsync(@"search", new Dictionary<string, object?>
                {
                    [@"continuation"] = continuation,
                    [@"context"] = createWebClientContext(null),
                }, cancellationToken).ConfigureAwait(false);

                if (nextPage == null)
                    break;

                collectSearchResults(nextPage.RootElement, results);
                continuation = tryFindContinuationToken(nextPage.RootElement);
            }

            if (orderEnum == SearchResource.ListRequest.OrderEnum.Title)
                results.Sort(static (a, b) => string.Compare(a.Snippet?.Title, b.Snippet?.Title, StringComparison.CurrentCultureIgnoreCase));

            return results;
        }

        private static void collectSearchResults(JsonElement root, List<SearchResult> results)
        {
            var seen = new HashSet<string>(results.Select(result => result.Id?.VideoId ?? result.Id?.PlaylistId ?? string.Empty), StringComparer.Ordinal);

            foreach (JsonElement renderer in enumerateSearchRenderers(root))
            {
                SearchResult? result = null;

                if (renderer.TryGetProperty(@"videoRenderer", out JsonElement videoRenderer))
                    result = parseVideoSearchResult(videoRenderer);
                else if (renderer.TryGetProperty(@"playlistRenderer", out JsonElement playlistRenderer))
                    result = parsePlaylistSearchResult(playlistRenderer);

                if (result == null)
                    continue;

                string id = result.Id?.VideoId ?? result.Id?.PlaylistId ?? string.Empty;

                // A continuation can overlap with the page it was requested from.
                if (id.Length > 0 && !seen.Add(id))
                    continue;

                results.Add(result);
            }
        }

        private static IEnumerable<JsonElement> enumerateSearchRenderers(JsonElement root)
        {
            foreach (JsonElement section in enumerateRenderers(root, @"itemSectionRenderer"))
            {
                if (!section.TryGetProperty(@"contents", out JsonElement contents) || contents.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement item in contents.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    foreach (JsonProperty property in item.EnumerateObject())
                    {
                        if (property.Value.ValueKind != JsonValueKind.Object)
                            continue;

                        yield return item;
                        break;
                    }
                }
            }
        }

        internal static SearchResult? parseVideoSearchResult(JsonElement renderer)
        {
            string? videoId = getString(renderer, @"videoId");

            if (string.IsNullOrEmpty(videoId))
                return null;

            JsonElement? ownerText = renderer.TryGetProperty(@"ownerText", out JsonElement owner) ? owner
                : renderer.TryGetProperty(@"longBylineText", out JsonElement longByline) ? longByline
                : renderer.TryGetProperty(@"shortBylineText", out JsonElement shortByline) ? shortByline
                : null;

            string? channelId = ownerText != null ? getFirstRunBrowseId(ownerText.Value) : null;
            string? channelTitle = ownerText != null ? getFirstRunText(ownerText.Value) : null;
            string? publishedRaw = renderer.TryGetProperty(@"publishedTimeText", out JsonElement publishedElement) ? extractText(publishedElement) : null;
            DateTimeOffset? published = parseRelativeDate(publishedRaw);

            string? description = null;

            if (renderer.TryGetProperty(@"detailedMetadataSnippets", out JsonElement snippets)
                && snippets.ValueKind == JsonValueKind.Array
                && snippets.GetArrayLength() > 0
                && snippets[0].TryGetProperty(@"snippetText", out JsonElement snippetText))
                description = extractText(snippetText);

            if (string.IsNullOrEmpty(description) && renderer.TryGetProperty(@"descriptionSnippet", out JsonElement descriptionSnippet))
                description = extractText(descriptionSnippet);

            bool isLive = isLiveBadge(renderer);

            return new SearchResult
            {
                Id = new ResourceId
                {
                    Kind = @"youtube#video",
                    VideoId = videoId,
                },
                Snippet = new SearchResultSnippet
                {
                    Title = extractText(renderer.TryGetProperty(@"title", out JsonElement title) ? title : default),
                    Description = description,
                    ChannelId = channelId,
                    ChannelTitle = channelTitle,
                    PublishedAtRaw = publishedRaw,
                    PublishedAtDateTimeOffset = published,
                    Thumbnails = createVideoThumbnails(videoId),
                    LiveBroadcastContent = isLive ? @"live" : @"none",
                },
            };
        }

        internal static SearchResult? parsePlaylistSearchResult(JsonElement renderer)
        {
            string? playlistId = getString(renderer, @"playlistId");

            if (string.IsNullOrEmpty(playlistId))
                return null;

            JsonElement? ownerText = renderer.TryGetProperty(@"shortBylineText", out JsonElement shortByline) ? shortByline
                : renderer.TryGetProperty(@"longBylineText", out JsonElement longByline) ? longByline
                : null;

            string? publishedRaw = renderer.TryGetProperty(@"publishedTimeText", out JsonElement publishedElement) ? extractText(publishedElement) : null;
            DateTimeOffset? published = parseRelativeDate(publishedRaw);
            string? thumbnailUrl = getLastThumbnailUrl(renderer.TryGetProperty(@"thumbnail", out JsonElement thumbnail) ? thumbnail : (JsonElement?)null);

            return new SearchResult
            {
                Id = new ResourceId
                {
                    Kind = @"youtube#playlist",
                    PlaylistId = playlistId,
                },
                Snippet = new SearchResultSnippet
                {
                    Title = extractText(renderer.TryGetProperty(@"title", out JsonElement title) ? title : default),
                    Description = getString(renderer, @"videoCount") is string videoCount ? $@"{videoCount} videos" : null,
                    ChannelId = ownerText != null ? getFirstRunBrowseId(ownerText.Value) : null,
                    ChannelTitle = ownerText != null ? getFirstRunText(ownerText.Value) : null,
                    PublishedAtRaw = publishedRaw,
                    PublishedAtDateTimeOffset = published,
                    Thumbnails = createThumbnailDetails(thumbnailUrl),
                    LiveBroadcastContent = @"none",
                },
            };
        }

        private static bool isLiveBadge(JsonElement renderer)
        {
            if (!renderer.TryGetProperty(@"badges", out JsonElement badges) || badges.ValueKind != JsonValueKind.Array)
                return false;

            foreach (JsonElement badge in badges.EnumerateArray())
            {
                if (!badge.TryGetProperty(@"metadataBadgeRenderer", out JsonElement metadataBadge))
                    continue;

                // The label of the badge is localized, its style is not.
                if (getString(metadataBadge, @"style") is { } style
                    && string.Equals(style, @"BADGE_STYLE_TYPE_LIVE_NOW", StringComparison.Ordinal))
                    return true;

                if (getString(metadataBadge, @"label") is { } label
                    && (string.Equals(label, @"LIVE", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(label, @"실시간", StringComparison.Ordinal)
                        || string.Equals(label, @"ライブ", StringComparison.Ordinal)))
                    return true;
            }

            return false;
        }

        #endregion

        /// <summary>
        /// Builds the web client context for comment requests. The visitor data is included because
        /// the continuation tokens of the watch page are bound to the visitor that requested it.
        /// </summary>
        private async Task<object> createCommentsContextAsync(CancellationToken cancellationToken)
        {
            string? visitorData = await tryResolveVisitorDataAsync(cancellationToken).ConfigureAwait(false);

            return createWebClientContext(visitorData);
        }

        #region comments

        private async Task<CommentThreadListResponse> fetchCommentThreadsAsync(string videoId, string pageToken, CommentThreadsResource.ListRequest.OrderEnum orderEnum, CancellationToken cancellationToken)
        {
            var response = new CommentThreadListResponse
            {
                Kind = @"youtube#commentThreadListResponse",
                Items = new List<CommentThread>(),
            };

            string? continuation = pageToken;

            if (string.IsNullOrEmpty(continuation))
            {
                continuation = await resolveCommentsContinuationAsync(videoId, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(continuation))
                {
                    Logger.Log($@"The comments continuation for '{videoId}' could not be resolved.", LoggingTarget.Network);
                    return response;
                }
            }

            comment_page page;

            if (string.IsNullOrEmpty(pageToken) && orderEnum == CommentThreadsResource.ListRequest.OrderEnum.Time)
            {
                // The sort menu is rendered in the section header, which is not part of the
                // continuation items, so it has to be resolved from the page response itself.
                page = await fetchCommentPageAsync(continuation, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                page = await fetchContinuationItemsAsync(continuation, cancellationToken).ConfigureAwait(false);
            }

            string? newestFirst = page.NewestSortContinuation;

            if (!string.IsNullOrEmpty(newestFirst) && newestFirst != continuation)
            {
                comment_page sortedPage = await fetchContinuationItemsAsync(newestFirst, cancellationToken).ConfigureAwait(false);

                if (sortedPage.Items.Count > 0)
                {
                    page = sortedPage;
                    continuation = newestFirst;
                }
            }

            List<JsonElement> items = page.Items;
            Dictionary<string, JsonElement> entities = page.Entities;

            var replyContinuations = new Dictionary<string, string>(StringComparer.Ordinal);
            int parsedThreads = 0;

            foreach (JsonElement item in items)
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                bool isThread = item.TryGetProperty(@"commentThreadRenderer", out JsonElement threadRenderer);
                bool isFlatComment = item.TryGetProperty(@"commentViewModel", out _);

                if (isThread || isFlatComment)
                {
                    CommentThread? thread = parseCommentThread(isThread ? threadRenderer : item, videoId, entities);

                    if (thread == null)
                        continue;

                    parsedThreads++;

                    if (thread.Snippet?.TopLevelComment != null)
                        response.Items.Add(thread);

                    // Older responses returned a handful of replies inline instead of a continuation.
                    List<Comment>? inlineReplies = parseInlineReplies(threadRenderer, videoId, thread.Id, entities);

                    if (inlineReplies != null)
                    {
                        thread.Replies = new CommentThreadReplies
                        {
                            Comments = inlineReplies,
                        };
                    }

                    string? repliesContinuation = findRepliesContinuation(threadRenderer);
                    string threadId = thread.Id ?? string.Empty;

                    if (!string.IsNullOrEmpty(repliesContinuation) && !string.IsNullOrEmpty(threadId))
                        replyContinuations[threadId] = repliesContinuation;
                }
                else if (item.TryGetProperty(@"continuationItemRenderer", out JsonElement continuationRenderer))
                {
                    string? nextPageToken = findContinuation(continuationRenderer);

                    if (!string.IsNullOrEmpty(nextPageToken))
                        response.NextPageToken = nextPageToken;
                }
            }

            if (items.Count > 0 && parsedThreads == 0)
                logUnparsedCommentItems(items, entities.Count);

            if (replyContinuations.Count > 0)
            {
                Dictionary<string, List<Comment>> replies = await fetchRepliesAsync(replyContinuations, videoId, cancellationToken).ConfigureAwait(false);

                foreach (CommentThread thread in response.Items)
                {
                    if (thread.Id == null)
                        continue;

                    if (!replies.TryGetValue(thread.Id, out List<Comment>? threadReplies) || threadReplies.Count == 0)
                        continue;

                    thread.Replies = new CommentThreadReplies
                    {
                        Comments = threadReplies,
                    };
                }
            }

            return response;
        }

        private async Task<comment_page> fetchContinuationItemsAsync(string continuation, CancellationToken cancellationToken)
        {
            using JsonDocument? document = await sendInnertubeAsync(@"next", new Dictionary<string, object?>
            {
                [@"continuation"] = continuation,
                [@"context"] = await createCommentsContextAsync(cancellationToken).ConfigureAwait(false),
            }, cancellationToken, innertube_client.Web, false, comments_dump_tag).ConfigureAwait(false);

            if (document == null)
                return comment_page.Empty;

            // Only the first page of the "newest first" order has to be resolved through the sort menu,
            // so a plain continuation never replaces the order the caller asked for.
            return readCommentPage(document.RootElement) with
            {
                NewestSortContinuation = null,
            };
        }

        private async Task<comment_page> fetchCommentPageAsync(string continuation, CancellationToken cancellationToken)
        {
            using JsonDocument? document = await sendInnertubeAsync(@"next", new Dictionary<string, object?>
            {
                [@"continuation"] = continuation,
                [@"context"] = await createCommentsContextAsync(cancellationToken).ConfigureAwait(false),
            }, cancellationToken, innertube_client.Web, false, comments_dump_tag).ConfigureAwait(false);

            if (document == null)
                return comment_page.Empty;

            return readCommentPage(document.RootElement) with
            {
                NewestSortContinuation = tryFindNewestSortContinuation(document.RootElement),
            };
        }

        /// <summary>
        /// The items of a comment page together with the entity payloads that carry the text of the
        /// comments.
        /// </summary>
        /// <remarks>
        /// The items outlive the document they were parsed from, so they have to be detached from its
        /// pooled buffer before the document is disposed.
        /// </remarks>
        internal sealed record comment_page(List<JsonElement> Items, Dictionary<string, JsonElement> Entities, string? NewestSortContinuation)
        {
            public static comment_page Empty { get; } = new(new List<JsonElement>(), new Dictionary<string, JsonElement>(StringComparer.Ordinal), null);
        }

        private static comment_page readCommentPage(JsonElement root)
            => new(detachItems(extractContinuationItems(root)), extractCommentEntities(root), tryFindNewestSortContinuation(root));

        /// <summary>
        /// Collects the <c>commentEntityPayload</c> entities of a response, keyed by every identifier
        /// the payload is referenced with.
        /// </summary>
        /// <remarks>
        /// The current watch page only renders the text, the author and the like count of a comment
        /// into <c>frameworkUpdates</c>, while the comment item itself keeps the identifiers.
        /// </remarks>
        internal static Dictionary<string, JsonElement> extractCommentEntities(JsonElement root)
        {
            var entities = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            JsonElement? batch = findProperty(root, @"entityBatchUpdate");

            if (batch == null || !batch.Value.TryGetProperty(@"mutations", out JsonElement mutations) || mutations.ValueKind != JsonValueKind.Array)
                return entities;

            foreach (JsonElement mutation in mutations.EnumerateArray())
            {
                if (getObject(mutation, @"payload") is not { } payload
                    || getObject(payload, @"commentEntityPayload") is not { } commentEntity)
                    continue;

                JsonElement detached = commentEntity.Clone();
                JsonElement? properties = getObject(detached, @"properties");

                foreach (string? key in new[]
                         {
                             getString(mutation, @"entityKey"),
                             getString(detached, @"key"),
                             getString(properties ?? default, @"commentKey"),
                             getString(properties ?? default, @"commentId"),
                         })
                {
                    if (!string.IsNullOrEmpty(key))
                        entities[key] = detached;
                }
            }

            return entities;
        }

        /// <summary>
        /// Finds the entity payload that belongs to a comment item or view model.
        /// </summary>
        internal static JsonElement? findCommentEntity(JsonElement item, IReadOnlyDictionary<string, JsonElement>? entities)
        {
            if (entities == null || entities.Count == 0)
                return null;

            foreach (string propertyName in new[] { @"commentKey", @"commentId" })
            {
                if (getString(item, propertyName) is { Length: > 0 } key && entities.TryGetValue(key, out JsonElement entity))
                    return entity;
            }

            JsonElement? nested = getObject(item, @"properties");

            foreach (string propertyName in new[] { @"commentKey", @"commentId" })
            {
                if (getString(nested ?? default, propertyName) is { Length: > 0 } key && entities.TryGetValue(key, out JsonElement entity))
                    return entity;
            }

            return null;
        }

        internal static List<JsonElement> detachItems(IEnumerable<JsonElement> items)
            => items.Select(item => item.Clone()).ToList();

        internal static List<JsonElement> extractContinuationItems(JsonElement root)
        {
            var items = new List<JsonElement>();

            if (root.TryGetProperty(@"onResponseReceivedEndpoints", out JsonElement endpoints) && endpoints.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement endpoint in endpoints.EnumerateArray())
                {
                    collectContinuationItems(endpoint, items);
                }
            }

            collectContinuationItems(root, items);

            return items;
        }

        private static void collectContinuationItems(JsonElement element, List<JsonElement> items)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return;

            foreach (string propertyName in new[] { @"reloadContinuationItemsCommand", @"appendContinuationItemsAction" })
            {
                if (!element.TryGetProperty(propertyName, out JsonElement command) || command.ValueKind != JsonValueKind.Object)
                    continue;

                if (!command.TryGetProperty(@"continuationItems", out JsonElement continuationItems) || continuationItems.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement item in continuationItems.EnumerateArray())
                {
                    // Avoid duplicating items when the command was already collected from onResponseReceivedEndpoints.
                    bool duplicate = items.Any(existing => JsonElement.DeepEquals(existing, item));

                    if (!duplicate)
                        items.Add(item);
                }
            }

            if (!element.TryGetProperty(@"continuationContents", out JsonElement continuationContents) || continuationContents.ValueKind != JsonValueKind.Object)
                return;

            if (!continuationContents.TryGetProperty(@"commentSectionContents", out JsonElement sectionContents)
                || !sectionContents.TryGetProperty(@"continuationItems", out JsonElement sectionItems)
                || sectionItems.ValueKind != JsonValueKind.Array)
                return;

            foreach (JsonElement item in sectionItems.EnumerateArray())
            {
                if (!items.Any(existing => JsonElement.DeepEquals(existing, item)))
                    items.Add(item);
            }
        }

        private async Task<string?> resolveCommentsContinuationAsync(string videoId, CancellationToken cancellationToken)
        {
            using JsonDocument? document = await sendInnertubeAsync(@"next", new Dictionary<string, object?>
            {
                [@"videoId"] = videoId,
                [@"context"] = await createCommentsContextAsync(cancellationToken).ConfigureAwait(false),
            }, cancellationToken, innertube_client.Web, false, comments_dump_tag).ConfigureAwait(false);

            if (document == null)
                return null;

            string? continuation = tryFindCommentsContinuation(document.RootElement);

            if (string.IsNullOrEmpty(continuation))
                logCommentsContinuationDiagnostics(document.RootElement);

            return continuation;
        }

        private static void logCommentsContinuationDiagnostics(JsonElement root)
        {
            var panels = new List<string>();

            foreach (JsonElement panel in enumerateRenderers(root, @"engagementPanelSectionListRenderer"))
                panels.Add($@"{getString(panel, @"panelIdentifier") ?? @"?"}/{getString(panel, @"targetId") ?? @"?"}");

            var sections = new List<string>();

            foreach (JsonElement section in enumerateRenderers(root, @"itemSectionRenderer"))
                sections.Add(getString(section, @"sectionIdentifier") ?? @"?");

            Logger.Log($@"Could not find a comment section. Engagement panels: [{string.Join(@", ", panels)}], item sections: [{string.Join(@", ", sections)}], "
                       + $@"entry point present: {findProperty(root, @"commentsEntryPointHeaderRenderer") != null}.", LoggingTarget.Network);
        }

        /// <summary>
        /// Logs the shape of the comment items that could not be parsed.
        /// </summary>
        private static void logUnparsedCommentItems(List<JsonElement> items, int entityCount)
        {
            JsonElement first = items[0];
            var shapes = new List<string>();

            foreach (JsonProperty property in first.EnumerateObject())
                shapes.Add(property.Name);

            Logger.Log($@"None of the {items.Count} comment items could be parsed. First item: [{string.Join(@", ", shapes)}], "
                       + $@"entity payloads: {entityCount}.", LoggingTarget.Network);
        }

        /// <summary>
        /// Finds the continuation that opens the comment section of a watch page.
        /// </summary>
        /// <remarks>
        /// Older responses rendered the section as an <c>itemSectionRenderer</c>, while current ones
        /// open it as an engagement panel and only keep a teaser next to the player.
        /// </remarks>
        internal static string? tryFindCommentsContinuation(JsonElement root)
        {
            foreach (JsonElement section in enumerateRenderers(root, @"itemSectionRenderer"))
            {
                if (!string.Equals(getString(section, @"sectionIdentifier"), @"comment-item-section", StringComparison.Ordinal))
                    continue;

                string? continuation = findContinuation(section);

                if (!string.IsNullOrEmpty(continuation))
                    return continuation;
            }

            foreach (JsonElement panel in enumerateRenderers(root, @"engagementPanelSectionListRenderer"))
            {
                if (!isCommentPanel(panel))
                    continue;

                string? continuation = findContinuation(panel);

                if (!string.IsNullOrEmpty(continuation))
                    return continuation;
            }

            foreach (JsonElement header in enumerateRenderers(root, @"commentsHeaderRenderer"))
            {
                string? continuation = findContinuation(header);

                if (!string.IsNullOrEmpty(continuation))
                    return continuation;
            }

            foreach (string rendererName in new[] { @"commentsEntryPointHeaderRenderer", @"commentsEntryPointTeaserRenderer" })
            {
                JsonElement? entryPoint = findProperty(root, rendererName);

                if (entryPoint == null)
                    continue;

                string? continuation = findContinuation(entryPoint.Value);

                if (!string.IsNullOrEmpty(continuation))
                    return continuation;
            }

            return null;
        }

        private static bool isCommentPanel(JsonElement panel)
        {
            string? identifier = getString(panel, @"panelIdentifier");
            string? targetId = getString(panel, @"targetId");

            return (identifier != null && identifier.Contains(@"comment", StringComparison.OrdinalIgnoreCase))
                   || (targetId != null && targetId.Contains(@"comment", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds the continuation that switches the comment section to "newest first".
        /// </summary>
        /// <remarks>
        /// The sort menu is rendered in the section header, not in the continuation items, and its
        /// label is localized, so the entry other than the selected one is used instead of
        /// matching a hard-coded label.
        /// </remarks>
        internal static string? tryFindNewestSortContinuation(JsonElement root)
        {
            foreach (JsonElement menu in enumerateRenderers(root, @"sortFilterSubMenuRenderer"))
            {
                if (!menu.TryGetProperty(@"subMenuItems", out JsonElement subMenuItems) || subMenuItems.ValueKind != JsonValueKind.Array || subMenuItems.GetArrayLength() < 2)
                    continue;

                foreach (JsonElement subMenuItem in subMenuItems.EnumerateArray())
                {
                    if (getBool(subMenuItem, @"selected"))
                        continue;

                    string? continuation = findContinuation(subMenuItem);

                    if (!string.IsNullOrEmpty(continuation))
                        return continuation;
                }
            }

            foreach (JsonElement chip in enumerateRenderers(root, @"chipCloudChipRenderer"))
            {
                if (getBool(chip, @"isSelected"))
                    continue;

                string text = extractText(chip.TryGetProperty(@"text", out JsonElement textElement) ? textElement : default) ?? string.Empty;

                if (!text.Contains(@"newest", StringComparison.OrdinalIgnoreCase)
                    && !text.Contains(@"최신", StringComparison.Ordinal)
                    && !text.Contains(@"最新", StringComparison.Ordinal))
                    continue;

                string? continuation = findContinuation(chip);

                if (!string.IsNullOrEmpty(continuation))
                    return continuation;
            }

            return null;
        }

        internal static CommentThread? parseCommentThread(JsonElement threadRenderer, string videoId, IReadOnlyDictionary<string, JsonElement>? entities = null)
        {
            JsonElement? commentRenderer = null;

            if (threadRenderer.TryGetProperty(@"comment", out JsonElement commentWrapper)
                && commentWrapper.TryGetProperty(@"commentRenderer", out JsonElement renderer))
                commentRenderer = renderer;
            else if (threadRenderer.TryGetProperty(@"commentViewModel", out JsonElement commentViewModel))
                commentRenderer = commentViewModel;

            if (commentRenderer == null)
                return null;

            JsonElement? entity = findCommentEntity(commentRenderer.Value, entities);

            Comment? comment = parseComment(commentRenderer.Value, videoId, null, entity);

            if (comment == null)
                return null;

            ulong? replyCount = parseUlong(commentRenderer.Value.TryGetProperty(@"replyCount", out JsonElement replyCountElement) ? replyCountElement : default);

            if (replyCount == null && commentRenderer.Value.TryGetProperty(@"viewReplies", out JsonElement viewReplies))
            {
                JsonElement? buttonRenderer = findProperty(viewReplies, @"buttonRenderer");

                if (buttonRenderer != null && buttonRenderer.Value.TryGetProperty(@"text", out JsonElement buttonText))
                    replyCount = parseRoughCount(extractText(buttonText));
            }

            // The current watch page keeps the amount of replies in the entity payload.
            if (replyCount == null && entity != null && getObject(entity.Value, @"toolbar") is { } toolbar)
                replyCount = parseRoughCount(getString(toolbar, @"replyCount"));

            return new CommentThread
            {
                Id = comment.Id,
                Kind = @"youtube#commentThread",
                Snippet = new CommentThreadSnippet
                {
                    VideoId = videoId,
                    ChannelId = comment.Snippet?.AuthorChannelId?.Value,
                    TopLevelComment = comment,
                    TotalReplyCount = toLong(replyCount),
                    CanReply = true,
                    IsPublic = true,
                },
            };
        }

        internal static Comment? parseComment(JsonElement renderer, string? videoId, string? parentId, JsonElement? entity = null)
        {
            // The text, the author and the counters of a comment are rendered into an entity
            // payload that the item only references by its key.
            JsonElement? properties = entity != null ? getObject(entity.Value, @"properties") : null;
            JsonElement? author = entity != null ? getObject(entity.Value, @"author") : null;
            JsonElement? toolbar = entity != null ? getObject(entity.Value, @"toolbar") : null;

            if (entity == null && (renderer.TryGetProperty(@"commentKey", out _) || renderer.TryGetProperty(@"commentId", out _)))
            {
                // The item can carry its own entity payload when the caller did not resolve it.
                entity = getObject(renderer, @"commentEntityPayload");
                properties = entity != null ? getObject(entity.Value, @"properties") : properties;
                author = entity != null ? getObject(entity.Value, @"author") : author;
                toolbar = entity != null ? getObject(entity.Value, @"toolbar") : toolbar;
            }

            string? commentId = getString(renderer, @"commentId");

            if (string.IsNullOrEmpty(commentId))
                commentId = getString(renderer, @"commentKey");

            if (string.IsNullOrEmpty(commentId))
                commentId = getString(properties ?? default, @"commentId");

            if (string.IsNullOrEmpty(commentId))
                return null;

            string? content = extractText(renderer.TryGetProperty(@"contentText", out JsonElement contentText) ? contentText : default);

            if (string.IsNullOrEmpty(content))
                content = extractText(getObject(properties ?? default, @"content") ?? default);

            if (string.IsNullOrEmpty(content))
                content = getString(properties ?? default, @"content");

            if (string.IsNullOrEmpty(content))
                content = findFirstString(renderer, new[] { @"contentText", @"content", @"text" });

            string? authorName = extractText(renderer.TryGetProperty(@"authorText", out JsonElement authorText) ? authorText : default);

            if (string.IsNullOrEmpty(authorName))
                authorName = getString(author ?? default, @"displayName");

            if (string.IsNullOrEmpty(authorName))
                authorName = findFirstString(renderer, new[] { @"authorText", @"authorName", @"authorButtonA11y" });

            string? authorChannelId = null;

            if (renderer.TryGetProperty(@"authorEndpoint", out JsonElement authorEndpoint))
                authorChannelId = getBrowseId(authorEndpoint);

            if (string.IsNullOrEmpty(authorChannelId))
                authorChannelId = getString(renderer, @"authorChannelId");

            if (string.IsNullOrEmpty(authorChannelId))
            {
                JsonElement? channelIdProperty = findProperty(renderer, @"authorChannelId");

                if (channelIdProperty != null)
                    authorChannelId = getString(channelIdProperty.Value, @"value") ?? getStringValue(channelIdProperty.Value);
            }

            if (string.IsNullOrEmpty(authorChannelId))
                authorChannelId = getString(author ?? default, @"channelId");

            string? authorThumbnail = getLastThumbnailUrl(renderer.TryGetProperty(@"authorThumbnail", out JsonElement authorThumbnailElement) ? authorThumbnailElement : (JsonElement?)null);

            if (string.IsNullOrEmpty(authorThumbnail))
                authorThumbnail = getString(author ?? default, @"avatarThumbnailUrl");

            string? publishedTimeText = extractText(renderer.TryGetProperty(@"publishedTimeText", out JsonElement publishedElement) ? publishedElement : default);

            if (string.IsNullOrEmpty(publishedTimeText))
                publishedTimeText = getString(properties ?? default, @"publishedTime");

            if (string.IsNullOrEmpty(publishedTimeText))
                publishedTimeText = findFirstString(renderer, new[] { @"publishedTimeText" });

            DateTimeOffset? published = parseRelativeDate(publishedTimeText);

            ulong? likeCount = parseUlong(renderer.TryGetProperty(@"voteCount", out JsonElement voteCount) ? voteCount : default);

            if (likeCount == null)
            {
                JsonElement? voteStatus = findProperty(renderer, @"voteCount");

                if (voteStatus != null)
                    likeCount = parseRoughCount(extractText(voteStatus.Value));
            }

            if (likeCount == null)
                likeCount = parseRoughCount(getString(toolbar ?? default, @"likeCountNotliked") ?? getString(toolbar ?? default, @"likeCountLiked"));

            return new Comment
            {
                Id = commentId,
                Kind = @"youtube#comment",
                Snippet = new CommentSnippet
                {
                    VideoId = videoId,
                    ParentId = parentId,
                    AuthorDisplayName = authorName,
                    AuthorProfileImageUrl = authorThumbnail,
                    AuthorChannelUrl = string.IsNullOrEmpty(authorChannelId) ? null : $@"{youtube_origin}/channel/{authorChannelId}",
                    AuthorChannelId = string.IsNullOrEmpty(authorChannelId)
                        ? null
                        : new CommentSnippetAuthorChannelId
                        {
                            Value = authorChannelId,
                        },
                    TextDisplay = content,
                    TextOriginal = content,
                    LikeCount = toLong(likeCount),
                    PublishedAtRaw = published?.ToString(@"o", CultureInfo.InvariantCulture),
                    PublishedAtDateTimeOffset = published,
                    CanRate = true,
                    ViewerRating = @"none",
                },
            };
        }

        private static List<Comment>? parseInlineReplies(JsonElement threadRenderer, string videoId, string? threadId, IReadOnlyDictionary<string, JsonElement>? entities = null)
        {
            if (!threadRenderer.TryGetProperty(@"replies", out JsonElement replies))
                return null;

            var result = new List<Comment>();

            foreach (JsonElement commentRenderer in enumerateRenderers(replies, @"commentRenderer").Concat(enumerateRenderers(replies, @"commentViewModel")))
            {
                Comment? comment = parseComment(commentRenderer, videoId, threadId, findCommentEntity(commentRenderer, entities));

                if (comment != null)
                    result.Add(comment);
            }

            return result.Count > 0 ? result : null;
        }

        internal static string? findRepliesContinuation(JsonElement threadRenderer)
        {
            if (!threadRenderer.TryGetProperty(@"replies", out JsonElement replies))
                return null;

            return findContinuation(replies);
        }

        private async Task<Dictionary<string, List<Comment>>> fetchRepliesAsync(Dictionary<string, string> continuations, string videoId, CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, List<Comment>>(StringComparer.Ordinal);

            // The Data API returned up to five replies per thread inline. Innertube requires a
            // request per thread, so only the first few threads are expanded to keep the page
            // load bounded.
            const int max_expanded_threads = 5;
            const int max_replies_per_thread = 5;

            var tasks = new List<Task<(string ThreadId, List<Comment> Replies)>>();

            foreach (KeyValuePair<string, string> continuation in continuations)
            {
                if (tasks.Count >= max_expanded_threads)
                    break;

                tasks.Add(fetchRepliesForThreadAsync(continuation.Key, continuation.Value, videoId, max_replies_per_thread, cancellationToken));
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach ((string threadId, List<Comment> replies) in results)
            {
                if (replies.Count > 0)
                    result[threadId] = replies;
            }

            return result;
        }

        private async Task<(string ThreadId, List<Comment> Replies)> fetchRepliesForThreadAsync(string threadId, string continuation, string videoId, int maxReplies, CancellationToken cancellationToken)
        {
            var replies = new List<Comment>();

            try
            {
                comment_page page = await fetchContinuationItemsAsync(continuation, cancellationToken).ConfigureAwait(false);

                foreach (JsonElement item in page.Items)
                {
                    if (replies.Count >= maxReplies)
                        break;

                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    if (!item.TryGetProperty(@"commentRenderer", out JsonElement commentRenderer))
                    {
                        if (!item.TryGetProperty(@"commentViewModel", out commentRenderer))
                            continue;
                    }

                    Comment? comment = parseComment(commentRenderer, videoId, threadId, findCommentEntity(commentRenderer, page.Entities));

                    if (comment != null)
                        replies.Add(comment);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to fetch replies for comment thread '{threadId}': {exception.GetDescription()}");
            }

            return (threadId, replies);
        }

        private async Task sendCommentAsync(string videoId, string commentText, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            string? continuation = await resolveCommentsContinuationAsync(videoId, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(continuation))
            {
                Logger.Log($@"Cannot post a comment because no comments continuation is available for '{videoId}'.", LoggingTarget.Network);
                return;
            }

            using JsonDocument? comments = await sendInnertubeAsync(@"next", new Dictionary<string, object?>
            {
                [@"continuation"] = continuation,
                [@"context"] = await createCommentsContextAsync(cancellationToken).ConfigureAwait(false),
            }, cancellationToken, innertube_client.Web, false, comments_dump_tag).ConfigureAwait(false);

            string? createCommentParams = comments != null ? findFirstString(comments.RootElement, new[] { @"createCommentParams" }) : null;

            if (string.IsNullOrEmpty(createCommentParams))
            {
                Logger.Log($@"Cannot post a comment because no createCommentParams were returned for '{videoId}'.", LoggingTarget.Network);
                return;
            }

            using JsonDocument? response = await sendAuthenticatedInnertubeAsync(@"comment/create_comment", client => new Dictionary<string, object?>
            {
                [@"createCommentParams"] = createCommentParams,
                [@"richTextBody"] = new Dictionary<string, object?>
                {
                    [@"message"] = commentText,
                },
                [@"context"] = client == innertube_client.Tv ? createTvBrowseContext() : createWebClientContext(null),
            }, cancellationToken, comments_dump_tag).ConfigureAwait(false);

            if (response == null)
                Logger.Log($@"Posting the comment to '{videoId}' failed.", LoggingTarget.Network);
            else
                Logger.Log($@"Posted a comment to '{videoId}'.", LoggingTarget.Network);
        }

        #endregion

        #region playlists

        private async Task<IList<Playlist>> fetchMyPlaylistsAsync(CancellationToken cancellationToken)
        {
            var playlists = new List<Playlist>();

            if (!googleOAuth2.SignedIn.Value)
            {
                Logger.Log(@"The playlist library was skipped because no account is signed in.", LoggingTarget.Network);
                return playlists;
            }

            foreach ((string browseId, innertube_client client) in playlist_library_pages)
            {
                using JsonDocument? document = await browseAuthenticatedAsync(browseId, cancellationToken, client).ConfigureAwait(false);

                if (document == null)
                    continue;

                collectPlaylists(document.RootElement, playlists);

                // The TV library opens on its own tab, so the playlists tab has to be entered first.
                if (playlists.Count == 0)
                {
                    string? tabContinuation = findPlaylistTabContinuation(document.RootElement);

                    if (!string.IsNullOrEmpty(tabContinuation))
                    {
                        comment_page tabPage = await fetchContinuationItemsAsync(tabContinuation, cancellationToken).ConfigureAwait(false);

                        foreach (JsonElement item in tabPage.Items)
                            collectPlaylists(item, playlists);
                    }
                }

                // The library only renders the first shelf initially; the rest of the playlists
                // arrive through continuations.
                string? continuation = tryFindPlaylistContinuation(document.RootElement);

                for (int page = 0; page < playlist_library_max_pages && !string.IsNullOrEmpty(continuation); page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    List<JsonElement> items = (await fetchContinuationItemsAsync(continuation, cancellationToken).ConfigureAwait(false)).Items;

                    if (items.Count == 0)
                        break;

                    foreach (JsonElement item in items)
                        collectPlaylists(item, playlists);

                    string? next = null;

                    for (int i = items.Count - 1; i >= 0 && next == null; i--)
                        next = findContinuation(items[i]);

                    if (string.IsNullOrEmpty(next) || next == continuation)
                        break;

                    continuation = next;
                }

                if (playlists.Count > 0)
                {
                    string? ownChannelId = await resolveOwnChannelIdAsync(cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(ownChannelId))
                        applyOwnerToPlaylists(playlists, ownChannelId, mineChannelCache?.Snippet?.Title);

                    foreach (Playlist playlist in playlists)
                        playlistLibraryCache[playlist.Id] = playlist;

                    break;
                }

                Logger.Log($@"The playlist library page '{browseId}' returned no playlists (grids: {enumerateRenderers(document.RootElement, @"gridRenderer").Count()}, "
                           + $"shelves: {enumerateRenderers(document.RootElement, @"shelfRenderer").Count()}, "
                           + $"lockups: {enumerateRenderers(document.RootElement, @"lockupViewModel").Count()}, "
                           + $"playlist renderers: {enumerateRenderers(document.RootElement, @"gridPlaylistRenderer").Count() + enumerateRenderers(document.RootElement, @"playlistRenderer").Count()}).", LoggingTarget.Network);
            }

            return playlists;
        }

        /// <summary>
        /// Finds the continuation of the "playlists" tab of a library page.
        /// </summary>
        internal static string? findPlaylistTabContinuation(JsonElement root)
        {
            foreach (JsonElement chip in enumerateRenderers(root, @"chipCloudChipRenderer"))
            {
                string text = extractText(chip.TryGetProperty(@"text", out JsonElement textElement) ? textElement : default) ?? string.Empty;

                if (!text.Contains(@"playlist", StringComparison.OrdinalIgnoreCase)
                    && !text.Contains(@"재생목록", StringComparison.Ordinal)
                    && !text.Contains(@"プレイリスト", StringComparison.Ordinal))
                    continue;

                string? continuation = findContinuation(chip);

                if (!string.IsNullOrEmpty(continuation))
                    return continuation;
            }

            return null;
        }

        /// <summary>
        /// Finds the continuation of the container that renders the playlists of a library page.
        /// </summary>
        internal static string? tryFindPlaylistContinuation(JsonElement root)
        {
            foreach (string containerName in new[] { @"gridRenderer", @"shelfRenderer", @"itemSectionRenderer", @"gridContinuationItemRenderer" })
            {
                foreach (JsonElement container in enumerateRenderers(root, containerName))
                {
                    if (!containsPlaylist(container))
                        continue;

                    string? continuation = findContinuation(container);

                    if (!string.IsNullOrEmpty(continuation))
                        return continuation;
                }
            }

            return null;
        }

        private static bool containsPlaylist(JsonElement container)
        {
            if (enumerateRenderers(container, @"gridPlaylistRenderer").Any() || enumerateRenderers(container, @"playlistRenderer").Any())
                return true;

            return enumerateRenderers(container, @"lockupViewModel").Any(isPlaylistLockup);
        }

        private static void collectPlaylists(JsonElement root, List<Playlist> playlists)
        {
            collectLibraryPlaylistTabs(root, playlists);

            foreach (JsonElement renderer in enumerateRenderers(root, @"gridPlaylistRenderer"))
            {
                Playlist? playlist = parsePlaylistRenderer(renderer);

                if (playlist != null && playlists.All(existing => existing.Id != playlist.Id))
                    playlists.Add(playlist);
            }

            foreach (JsonElement renderer in enumerateRenderers(root, @"playlistRenderer"))
            {
                Playlist? playlist = parsePlaylistRenderer(renderer);

                if (playlist != null && playlists.All(existing => existing.Id != playlist.Id))
                    playlists.Add(playlist);
            }

            foreach (JsonElement renderer in enumerateRenderers(root, @"lockupViewModel"))
            {
                Playlist? playlist = parsePlaylistLockup(renderer);

                if (playlist != null && playlists.All(existing => existing.Id != playlist.Id))
                    playlists.Add(playlist);
            }
        }

        /// <summary>
        /// Points the library playlists at their owner channel so the playlist rows can render the
        /// channel name. The TV library tabs do not carry the owner themselves.
        /// </summary>
        internal static void applyOwnerToPlaylists(IEnumerable<Playlist> playlists, string channelId, string? channelTitle = null)
        {
            foreach (Playlist playlist in playlists)
            {
                if (playlist.Snippet == null)
                    continue;

                if (string.IsNullOrEmpty(playlist.Snippet.ChannelId))
                    playlist.Snippet.ChannelId = channelId;

                if (!string.IsNullOrEmpty(channelTitle) && string.IsNullOrEmpty(playlist.Snippet.ChannelTitle))
                    playlist.Snippet.ChannelTitle = channelTitle;
            }
        }

        /// <summary>
        /// Reads the playlists of the TV library page, which renders every playlist as a library tab
        /// (<c>tvSecondaryNavRenderer</c> tab) instead of a playlist renderer.
        /// </summary>
        internal static void collectLibraryPlaylistTabs(JsonElement root, List<Playlist> playlists)
        {
            foreach (JsonElement tab in enumerateRenderers(root, @"tabRenderer"))
            {
                JsonElement? browseEndpoint = getObject(getObject(tab, @"endpoint") ?? default, @"browseEndpoint");

                if (browseEndpoint == null || !string.Equals(getString(browseEndpoint.Value, @"browseId"), @"FEmy_youtube", StringComparison.Ordinal))
                    continue;

                string? playlistId = tryDecodePlaylistId(getString(browseEndpoint.Value, @"params"));

                if (string.IsNullOrEmpty(playlistId) || playlists.Any(existing => existing.Id == playlistId))
                    continue;

                string? title = getString(tab, @"title");
                string? thumbnailUrl = getLastThumbnailUrl(getObject(tab, @"thumbnail") ?? default);

                playlists.Add(new Playlist
                {
                    Id = playlistId,
                    Kind = @"youtube#playlist",
                    Snippet = new PlaylistSnippet
                    {
                        Title = title,
                        Thumbnails = createThumbnailDetails(thumbnailUrl),
                    },
                    Status = new PlaylistStatus(),
                    ContentDetails = new PlaylistContentDetails(),
                    Localizations = new Dictionary<string, PlaylistLocalization>(StringComparer.Ordinal),
                });
            }
        }

        /// <summary>
        /// Extracts the playlist ID from the base64 encoded <c>params</c> of a library tab.
        /// </summary>
        /// <remarks>
        /// The parameters are a serialized protobuf message that embeds the playlist ID as its only
        /// long ASCII run (for example <c>…²"PLJW_JNo7Tjj1WG8s5m6xl8bVDVa8U6hhB</c>). The
        /// built-in library tabs encode short IDs instead ("LL" for liked videos), which are not
        /// real playlists and are filtered out by the length requirement.
        /// </remarks>
        internal static string? tryDecodePlaylistId(string? encodedParams)
        {
            if (string.IsNullOrEmpty(encodedParams))
                return null;

            try
            {
                string base64 = Uri.UnescapeDataString(encodedParams).Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

                string decoded = Encoding.ASCII.GetString(Convert.FromBase64String(base64));

                MatchCollection matches = Regex.Matches(decoded, @"[A-Za-z0-9_-]{10,}");

                return matches.Count > 0 ? matches[^1].Value : null;
            }
            catch (Exception exception) when (exception is FormatException or ArgumentException)
            {
                Logger.Log($@"Failed to decode the library tab params '{encodedParams}': {exception.Message}", LoggingTarget.Network);
                return null;
            }
        }

        private static Playlist? parsePlaylistRenderer(JsonElement renderer)
        {
            string? playlistId = getString(renderer, @"playlistId");

            if (string.IsNullOrEmpty(playlistId))
                return null;

            string? title = extractText(renderer.TryGetProperty(@"title", out JsonElement titleElement) ? titleElement : default);
            string? thumbnailUrl = getLastThumbnailUrl(renderer.TryGetProperty(@"thumbnail", out JsonElement thumbnail) ? thumbnail : (JsonElement?)null);
            string? channelId = tryGetNestedBrowseId(renderer, @"shortBylineText") ?? tryGetNestedBrowseId(renderer, @"longBylineText") ?? findFirstBrowseId(renderer);

            return new Playlist
            {
                Id = playlistId,
                Kind = @"youtube#playlist",
                Snippet = new PlaylistSnippet
                {
                    Title = title,
                    ChannelId = channelId,
                    Thumbnails = createThumbnailDetails(thumbnailUrl),
                },
                Status = new PlaylistStatus
                {
                    PrivacyStatus = findPrivacyStatus(renderer) ?? @"public",
                },
                ContentDetails = new PlaylistContentDetails
                {
                    ItemCount = toLong(parseRoughCount(getString(renderer, @"videoCount") is string count ? count : null)),
                },
                Localizations = new Dictionary<string, PlaylistLocalization>(StringComparer.Ordinal),
            };
        }

        /// <summary>
        /// Returns whether the lockup renders a playlist. Lockups also cover videos and channels.
        /// </summary>
        internal static bool isPlaylistLockup(JsonElement renderer)
        {
            if (renderer.TryGetProperty(@"contentType", out JsonElement contentType)
                && contentType.ValueKind == JsonValueKind.String
                && !string.Equals(contentType.GetString(), @"LOCKUP_CONTENT_TYPE_PLAYLIST", StringComparison.Ordinal))
                return false;

            string? playlistId = getString(renderer, @"contentId");

            return !string.IsNullOrEmpty(playlistId) && isPlaylistId(playlistId);
        }

        private static bool isPlaylistId(string id)
            => id.StartsWith(@"PL", StringComparison.Ordinal)
               || id.StartsWith(@"VL", StringComparison.Ordinal)
               || id.StartsWith(@"UU", StringComparison.Ordinal)
               || id.StartsWith(@"FL", StringComparison.Ordinal)
               || id.StartsWith(@"OL", StringComparison.Ordinal);

        private static Playlist? parsePlaylistLockup(JsonElement renderer)
        {
            if (!isPlaylistLockup(renderer))
                return null;

            string? playlistId = getString(renderer, @"contentId");

            if (string.IsNullOrEmpty(playlistId))
                return null;

            string? title = findFirstString(renderer, new[] { @"title" });
            string? thumbnailUrl = getLastThumbnailUrl(renderer);

            return new Playlist
            {
                Id = playlistId,
                Kind = @"youtube#playlist",
                Snippet = new PlaylistSnippet
                {
                    Title = title,
                    ChannelId = findFirstBrowseId(renderer),
                    Thumbnails = createThumbnailDetails(thumbnailUrl),
                },
                Status = new PlaylistStatus
                {
                    PrivacyStatus = findPrivacyStatus(renderer) ?? @"public",
                },
                ContentDetails = new PlaylistContentDetails(),
                Localizations = new Dictionary<string, PlaylistLocalization>(StringComparer.Ordinal),
            };
        }

        /// <summary>
        /// Browses a playlist page. Anonymous browsing works for public playlists, while
        /// private playlists of the signed-in account require an authenticated request.
        /// </summary>
        private async Task<JsonDocument?> browsePlaylistAsync(string playlistId, CancellationToken cancellationToken)
        {
            string browseId = playlistId.StartsWith(@"VL", StringComparison.Ordinal) ? playlistId : $@"VL{playlistId}";

            JsonDocument? document = await sendInnertubeAsync(@"browse", new Dictionary<string, object?>
            {
                [@"browseId"] = browseId,
                [@"context"] = createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            if (document != null && isUsablePlaylistResponse(document.RootElement))
                return document;

            document?.Dispose();

            // Public playlists load anonymously; private and unlisted playlists of the account only
            // load with the account credentials, where the anonymous request fails outright.
            if (!googleOAuth2.SignedIn.Value)
                return null;

            return await browseAuthenticatedAsync(browseId, cancellationToken).ConfigureAwait(false);
        }

        private static bool isUsablePlaylistResponse(JsonElement root)
        {
            if (root.TryGetProperty(@"metadata", out JsonElement metadata) && metadata.TryGetProperty(@"playlistMetadataRenderer", out _))
                return true;

            if (findProperty(root, @"playlistHeaderRenderer") != null)
                return true;

            return enumerateRenderers(root, @"playlistVideoRenderer").Any()
                   || enumerateRenderers(root, @"lockupViewModel").Any();
        }

        private async Task<Playlist> fetchPlaylistInfoAsync(string playlistId, CancellationToken cancellationToken)
        {
            using JsonDocument? document = await browsePlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);

            playlistLibraryCache.TryGetValue(playlistId, out Playlist? libraryPlaylist);

            if (document == null)
                return libraryPlaylist ?? createEmptyPlaylist(playlistId);

            JsonElement root = document.RootElement;
            string? title = null;
            string? description = null;
            string? ownerChannelId = null;
            string? ownerChannelTitle = null;
            string? privacy = findPrivacyStatus(root);
            string? thumbnailUrl = null;

            if (root.TryGetProperty(@"metadata", out JsonElement metadata)
                && metadata.TryGetProperty(@"playlistMetadataRenderer", out JsonElement playlistMetadata))
            {
                title = getString(playlistMetadata, @"title");
                description = getString(playlistMetadata, @"description");
            }

            JsonElement? header = findProperty(root, @"playlistHeaderRenderer");

            if (header != null)
            {
                title ??= extractText(header.Value.TryGetProperty(@"title", out JsonElement headerTitle) ? headerTitle : default);
                description ??= extractText(header.Value.TryGetProperty(@"descriptionText", out JsonElement descriptionText) ? descriptionText : default);
                ownerChannelId = getBrowseId(header.Value.TryGetProperty(@"ownerText", out JsonElement ownerText) ? ownerText : default);
                privacy ??= getString(header.Value, @"privacy")?.ToLowerInvariant();
                thumbnailUrl = getLastThumbnailUrl(header.Value);
            }

            ownerChannelTitle = findOwnerChannelTitle(root);

            Playlist? parsed = null;

            foreach (JsonElement renderer in enumerateRenderers(root, @"lockupViewModel"))
            {
                parsed = parsePlaylistLockup(renderer);

                if (parsed != null && parsed.Id == playlistId)
                    break;

                parsed = null;
            }

            if (title == null && parsed?.Snippet?.Title != null)
                title = parsed.Snippet.Title;

            // The library tabs already know the title, thumbnail and owner of the playlist, which
            // keeps the playlist rows readable when the playlist page itself is unavailable.
            if (libraryPlaylist != null)
            {
                if (string.IsNullOrEmpty(title))
                    title = libraryPlaylist.Snippet?.Title;

                ownerChannelId ??= libraryPlaylist.Snippet?.ChannelId;
                ownerChannelTitle ??= libraryPlaylist.Snippet?.ChannelTitle;
                thumbnailUrl ??= libraryPlaylist.Snippet?.Thumbnails?.High?.Url;
            }

            // The account knows the title of its own channel, which fills the owner of a playlist that
            // only exposed its channel ID.
            if (string.IsNullOrEmpty(ownerChannelTitle)
                && !string.IsNullOrEmpty(ownerChannelId)
                && string.Equals(mineChannelCache?.Id, ownerChannelId, StringComparison.Ordinal))
                ownerChannelTitle = mineChannelCache?.Snippet?.Title;

            var playlist = createEmptyPlaylist(playlistId);

            playlist.Snippet = new PlaylistSnippet
            {
                Title = title,
                Description = description,
                ChannelId = ownerChannelId,
                ChannelTitle = ownerChannelTitle,
                Thumbnails = createThumbnailDetails(thumbnailUrl ?? parsed?.Snippet?.Thumbnails?.High?.Url),
            };

            playlist.Status = new PlaylistStatus
            {
                PrivacyStatus = privacy ?? @"public",
            };

            playlist.ContentDetails = new PlaylistContentDetails
            {
                ItemCount = findPlaylistItemCount(root),
            };

            return playlist;
        }

        /// <summary>
        /// Reads the title of the channel that owns a playlist.
        /// </summary>
        internal static string? findOwnerChannelTitle(JsonElement root)
        {
            foreach (JsonElement header in enumerateRenderers(root, @"playlistHeaderRenderer"))
            {
                string? ownerTitle = readBylineChannelTitle(header, @"ownerText")
                                     ?? readBylineChannelTitle(header, @"shortBylineText")
                                     ?? getString(header, @"ownerChannelName");

                if (!string.IsNullOrEmpty(ownerTitle))
                    return ownerTitle;
            }

            return readOwnerChannelTitleFromPageHeader(root);
        }

        /// <summary>
        /// Reads the channel name of a byline (for example <c>ownerText</c>), preferring the run that
        /// links to the channel.
        /// </summary>
        private static string? readBylineChannelTitle(JsonElement element, string propertyName)
        {
            if (getObject(element, propertyName) is not { } byline
                || !byline.TryGetProperty(@"runs", out JsonElement runs)
                || runs.ValueKind != JsonValueKind.Array)
                return null;

            string? firstText = null;

            foreach (JsonElement run in runs.EnumerateArray())
            {
                if (getString(run, @"text") is not { Length: > 0 } text)
                    continue;

                firstText ??= text;

                if (findProperty(run, @"browseEndpoint") is { } endpoint
                    && getString(endpoint, @"browseId") is { Length: > 1 } browseId
                    && browseId.StartsWith(@"UC", StringComparison.Ordinal))
                    return text;
            }

            return firstText;
        }

        /// <summary>
        /// Reads the channel name from the metadata rows of a page header, which is where the current
        /// playlist page renders its owner.
        /// </summary>
        private static string? readOwnerChannelTitleFromPageHeader(JsonElement root)
        {
            JsonElement? metadata = findProperty(root, @"contentMetadataViewModel");

            if (metadata == null || !metadata.Value.TryGetProperty(@"metadataRows", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
                return null;

            foreach (JsonElement row in rows.EnumerateArray())
            {
                if (!row.TryGetProperty(@"metadataParts", out JsonElement parts) || parts.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement part in parts.EnumerateArray())
                {
                    if (findProperty(part, @"browseEndpoint") is not { } endpoint)
                        continue;

                    if (getString(endpoint, @"browseId") is not { Length: > 1 } browseId || !browseId.StartsWith(@"UC", StringComparison.Ordinal))
                        continue;

                    string? text = extractText(getObject(part, @"text") ?? default);

                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }

            return null;
        }

        private static Playlist createEmptyPlaylist(string playlistId)
            => new Playlist
            {
                Id = playlistId,
                Kind = @"youtube#playlist",
                Snippet = new PlaylistSnippet
                {
                    Title = string.Empty,
                    Thumbnails = new ThumbnailDetails(),
                },
                Status = new PlaylistStatus
                {
                    PrivacyStatus = @"public",
                },
                ContentDetails = new PlaylistContentDetails(),
                Localizations = new Dictionary<string, PlaylistLocalization>(StringComparer.Ordinal),
            };

        private static long? findPlaylistItemCount(JsonElement root)
        {
            foreach (JsonElement renderer in enumerateRenderers(root, @"playlistHeaderRenderer"))
            {
                foreach (string propertyName in new[] { @"numVideosText", @"videoCountText" })
                {
                    if (!renderer.TryGetProperty(propertyName, out JsonElement element))
                        continue;

                    ulong? count = parseRoughCount(extractText(element));

                    if (count != null)
                        return toLong(count);
                }
            }

            return null;
        }

        private async Task<IList<PlaylistItem>> fetchPlaylistItemsAsync(string playlistId, CancellationToken cancellationToken, string? stopWhenVideoId = null)
        {
            var items = new List<PlaylistItem>();
            var seenVideoIds = new HashSet<string>(StringComparer.Ordinal);
            var seenContinuations = new HashSet<string>(StringComparer.Ordinal);

            using JsonDocument? document = await browsePlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return items;

            string? continuation = collectPlaylistItems(document.RootElement, playlistId, items, seenVideoIds);

            // The playlist page only renders its first page, so the rest of a long playlist is
            // fetched through the continuation tokens.
            for (int page = 1; continuation != null && page < playlist_item_max_pages; page++)
            {
                if (stopWhenVideoId != null && seenVideoIds.Contains(stopWhenVideoId))
                    break;

                if (!seenContinuations.Add(continuation))
                    break;

                using JsonDocument? nextPage = await fetchPlaylistItemPageAsync(continuation, cancellationToken).ConfigureAwait(false);

                if (nextPage == null)
                    break;

                continuation = collectPlaylistItems(nextPage.RootElement, playlistId, items, seenVideoIds);
            }

            return items;
        }

        private async Task<JsonDocument?> fetchPlaylistItemPageAsync(string continuation, CancellationToken cancellationToken)
        {
            var payload = new Dictionary<string, object?>
            {
                [@"continuation"] = continuation,
                [@"context"] = createWebClientContext(null),
            };

            JsonDocument? document = await sendInnertubeAsync(@"browse", payload, cancellationToken).ConfigureAwait(false);

            if (document == null && googleOAuth2.SignedIn.Value)
            {
                // A continuation of a playlist that only the account can read needs the credentials of
                // the account, which only the TV client accepts as a bearer token.
                payload[@"context"] = createTvBrowseContext();
                document = await sendInnertubeAsync(@"browse", payload, cancellationToken, innertube_client.Tv, true).ConfigureAwait(false);
            }

            return document;
        }

        /// <summary>
        /// Appends the items of a single playlist page and returns its continuation token.
        /// </summary>
        private static string? collectPlaylistItems(JsonElement root, string playlistId, List<PlaylistItem> items, HashSet<string> seenVideoIds)
        {
            int added = 0;

            foreach (JsonElement renderer in enumerateRenderers(root, @"playlistVideoRenderer"))
            {
                PlaylistItem? item = parsePlaylistVideoRenderer(renderer, playlistId, items.Count);

                if (item == null || !seenVideoIds.Add(item.ContentDetails.VideoId))
                    continue;

                items.Add(item);
                added++;
            }

            if (added == 0)
            {
                foreach (JsonElement renderer in enumerateRenderers(root, @"lockupViewModel"))
                {
                    PlaylistItem? item = parsePlaylistItemLockup(renderer, playlistId, items.Count);

                    if (item == null || !seenVideoIds.Add(item.ContentDetails.VideoId))
                        continue;

                    items.Add(item);
                }
            }

            return tryFindContinuationToken(root);
        }

        /// <summary>
        /// Returns the token of the next page of a list (playlist items, search results, ...).
        /// </summary>
        internal static string? tryFindContinuationToken(JsonElement root)
        {
            foreach (JsonElement renderer in enumerateRenderers(root, @"continuationItemRenderer"))
            {
                if (getNestedObject(renderer, @"continuationEndpoint", @"continuationCommand") is { } command
                    && getString(command, @"token") is { Length: > 0 } token)
                    return token;
            }

            return null;
        }

        internal static PlaylistItem? parsePlaylistVideoRenderer(JsonElement renderer, string playlistId, int position)
        {
            string? videoId = getString(renderer, @"videoId");

            if (string.IsNullOrEmpty(videoId))
                return null;

            string? setVideoId = getString(renderer, @"setVideoId");
            string? title = extractText(renderer.TryGetProperty(@"title", out JsonElement titleElement) ? titleElement : default);
            string? channelId = tryGetNestedBrowseId(renderer, @"shortBylineText") ?? tryGetNestedBrowseId(renderer, @"videoOwnerText");
            string? channelTitle = getFirstRunText(renderer.TryGetProperty(@"shortBylineText", out JsonElement byline) ? byline : default)
                                   ?? getFirstRunText(renderer.TryGetProperty(@"videoOwnerText", out JsonElement ownerText) ? ownerText : default);
            string? publishedRaw = renderer.TryGetProperty(@"publishedTimeText", out JsonElement publishedElement) ? extractText(publishedElement) : null;
            DateTimeOffset? published = parseRelativeDate(publishedRaw);

            return new PlaylistItem
            {
                Id = setVideoId ?? videoId,
                Kind = @"youtube#playlistItem",
                Snippet = new PlaylistItemSnippet
                {
                    PlaylistId = playlistId,
                    Position = position,
                    Title = title,
                    ChannelId = channelId,
                    ChannelTitle = channelTitle,
                    ResourceId = new ResourceId
                    {
                        Kind = @"youtube#video",
                        VideoId = videoId,
                    },
                    Thumbnails = createVideoThumbnails(videoId),
                    PublishedAtRaw = publishedRaw,
                    PublishedAtDateTimeOffset = published,
                },
                ContentDetails = new PlaylistItemContentDetails
                {
                    VideoId = videoId,
                },
            };
        }

        private static PlaylistItem? parsePlaylistItemLockup(JsonElement renderer, string playlistId, int position)
        {
            if (renderer.TryGetProperty(@"contentType", out JsonElement contentType)
                && contentType.ValueKind == JsonValueKind.String
                && !string.Equals(contentType.GetString(), @"LOCKUP_CONTENT_TYPE_VIDEO", StringComparison.Ordinal))
                return null;

            string? videoId = getString(renderer, @"contentId");

            if (string.IsNullOrEmpty(videoId) || videoId.Length != 11)
                return null;

            return new PlaylistItem
            {
                Id = videoId,
                Kind = @"youtube#playlistItem",
                Snippet = new PlaylistItemSnippet
                {
                    PlaylistId = playlistId,
                    Position = position,
                    Title = findFirstString(renderer, new[] { @"title" }),
                    ResourceId = new ResourceId
                    {
                        Kind = @"youtube#video",
                        VideoId = videoId,
                    },
                    Thumbnails = createVideoThumbnails(videoId),
                },
                ContentDetails = new PlaylistItemContentDetails
                {
                    VideoId = videoId,
                },
            };
        }

        private async Task updatePlaylistInfoAsync(string playlistId, string title, PrivacyStatus privacyStatus, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            var payload = new
            {
                context = createTvBrowseContext(),
                playlistId,
                actions = new object[]
                {
                    new
                    {
                        action = @"ACTION_SET_PLAYLIST_NAME",
                        playlistName = title,
                    },
                    new
                    {
                        action = @"ACTION_SET_PLAYLIST_PRIVACY",
                        playlistPrivacy = toInnertubePrivacy(privacyStatus),
                    },
                },
            };

            using JsonDocument? document = await sendTvEditRequestAsync(payload, cancellationToken).ConfigureAwait(false);

            if (document == null)
                Logger.Log($@"Updating the playlist '{playlistId}' failed.", LoggingTarget.Network);
        }

        private async Task addPlaylistAsync(string playlistTitle, PrivacyStatus privacyStatus, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            using JsonDocument? document = await sendTvInnertubeAsync(@"playlist/create", new Dictionary<string, object?>
            {
                [@"title"] = playlistTitle,
                [@"privacy"] = toInnertubePrivacy(privacyStatus),
                [@"context"] = createTvBrowseContext(),
            }, cancellationToken).ConfigureAwait(false);

            if (document == null)
                Logger.Log(@"Creating the playlist failed.", LoggingTarget.Network);
        }

        private async Task saveVideoToPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            var payload = new
            {
                context = createTvBrowseContext(),
                playlistId,
                actions = new object[]
                {
                    new
                    {
                        action = @"ACTION_ADD_VIDEO",
                        addedVideoId = videoId,
                    },
                },
            };

            using JsonDocument? document = await sendTvEditRequestAsync(payload, cancellationToken).ConfigureAwait(false);

            if (document == null)
                Logger.Log($@"Adding '{videoId}' to the playlist '{playlistId}' failed.", LoggingTarget.Network);
        }

        private async Task removeVideoFromPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            IList<PlaylistItem> items = await fetchPlaylistItemsAsync(playlistId, cancellationToken).ConfigureAwait(false);
            PlaylistItem? target = items.FirstOrDefault(item =>
                string.Equals(item.ContentDetails?.VideoId, videoId, StringComparison.Ordinal)
                || string.Equals(item.Snippet?.ResourceId?.VideoId, videoId, StringComparison.Ordinal));

            if (target == null)
            {
                Logger.Log($@"'{videoId}' is not part of the playlist '{playlistId}'.", LoggingTarget.Network);
                return;
            }

            // Removing an entry of a playlist needs the entry ID (setVideoId) that the playlist page
            // assigned to it; the entries of the special playlists (for example the watch later list)
            // do not have one and are removed by their video ID instead.
            bool hasSetVideoId = !string.Equals(target.Id, videoId, StringComparison.Ordinal);

            object action = hasSetVideoId
                ? new
                {
                    action = @"ACTION_REMOVE_VIDEO",
                    setVideoId = target.Id,
                    removedVideoId = videoId,
                }
                : new
                {
                    action = @"ACTION_REMOVE_VIDEO_BY_VIDEO_ID",
                    removedVideoId = videoId,
                };

            var payload = new
            {
                context = createTvBrowseContext(),
                playlistId,
                actions = new[] { action },
            };

            using JsonDocument? document = await sendTvEditRequestAsync(payload, cancellationToken).ConfigureAwait(false);

            if (document == null)
                Logger.Log($@"Removing '{videoId}' from the playlist '{playlistId}' failed.", LoggingTarget.Network);
        }

        private async Task<bool> isVideoExistsOnPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return false;

            IList<PlaylistItem> items = await fetchPlaylistItemsAsync(playlistId, cancellationToken, videoId).ConfigureAwait(false);

            return items.Any(item => string.Equals(item.ContentDetails?.VideoId, videoId, StringComparison.Ordinal)
                                     || string.Equals(item.Snippet?.ResourceId?.VideoId, videoId, StringComparison.Ordinal));
        }

        private static string toInnertubePrivacy(PrivacyStatus privacyStatus)
        {
            switch (privacyStatus)
            {
                case PrivacyStatus.Public:
                    return @"PUBLIC";

                case PrivacyStatus.Unlisted:
                    return @"UNLISTED";

                case PrivacyStatus.Private:
                    return @"PRIVATE";

                default:
                    return @"PRIVATE";
            }
        }

        private static string? findPrivacyStatus(JsonElement root)
        {
            foreach (string propertyName in new[] { @"privacy", @"privacyStatus", @"visibility" })
            {
                foreach (JsonElement value in enumeratePropertyValues(root, propertyName))
                {
                    string? text = getStringValue(value)?.ToLowerInvariant();

                    if (text is @"public" or @"unlisted" or @"private")
                        return text;
                }
            }

            foreach (JsonElement value in enumeratePropertyValues(root, @"content"))
            {
                string? text = getStringValue(value)?.ToLowerInvariant();

                if (text is @"public" or @"unlisted" or @"private")
                    return text;
            }

            return null;
        }

        #endregion

        #region subscriptions

        private async Task<bool> isChannelSubscribedAsync(string channelId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value || string.IsNullOrEmpty(channelId))
                return false;

            if (subscriptionStateCache.TryGetValue(channelId, out bool cached))
                return cached;

            bool state = await fetchSubscribedStateAsync(channelId, cancellationToken).ConfigureAwait(false);
            subscriptionStateCache[channelId] = state;

            return state;
        }

        private async Task<bool> fetchSubscribedStateAsync(string channelId, CancellationToken cancellationToken)
        {
            using JsonDocument? document = await browseAuthenticatedAsync(channelId, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return false;

            return tryReadSubscribedState(document.RootElement) ?? false;
        }

        internal static bool? tryReadSubscribedState(JsonElement root)
        {
            // The page header reports the state of the channel itself, while the shelves further down
            // the page repeat the state of every recommended channel.
            JsonElement? header = findPageHeader(root);

            if (header != null && readSubscribedState(header.Value) is { } headerState)
                return headerState;

            return readSubscribedState(root);
        }

        private static bool? readSubscribedState(JsonElement root)
        {
            foreach (JsonElement value in enumeratePropertyValues(root, @"subscribed"))
            {
                if (value.ValueKind == JsonValueKind.True)
                    return true;

                if (value.ValueKind == JsonValueKind.False)
                    return false;
            }

            // The current page header only exposes the state through the button text.
            foreach (string propertyName in new[] { @"accessibilityText", @"title", @"buttonText" })
            {
                foreach (JsonElement value in enumeratePropertyValues(root, propertyName))
                {
                    string? text = extractText(value) ?? getStringValue(value);

                    if (string.IsNullOrEmpty(text))
                        continue;

                    if (text.Contains(@"unsubscribe", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (text.Contains(@"subscribed", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return null;
        }

        private async Task<string> getSubscriptionIdAsync(string channelId, CancellationToken cancellationToken)
        {
            bool subscribed = await isChannelSubscribedAsync(channelId, cancellationToken).ConfigureAwait(false);
            return subscribed ? channelId : string.Empty;
        }

        private async Task subscribeChannelAsync(string channelId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            using JsonDocument? document = await sendAuthenticatedInnertubeAsync(@"subscription/subscribe", client => new Dictionary<string, object?>
            {
                [@"channelIds"] = new[] { channelId },
                [@"context"] = client == innertube_client.Tv ? createTvBrowseContext() : createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            subscriptionStateCache[channelId] = document != null;
        }

        private async Task unsubscribeChannelAsync(string subscriptionId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value || string.IsNullOrEmpty(subscriptionId))
                return;

            using JsonDocument? document = await sendAuthenticatedInnertubeAsync(@"subscription/unsubscribe", client => new Dictionary<string, object?>
            {
                [@"channelIds"] = new[] { subscriptionId },
                [@"context"] = client == innertube_client.Tv ? createTvBrowseContext() : createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            if (document != null)
                subscriptionStateCache[subscriptionId] = false;
        }

        #endregion

        #region rating

        private async Task<VideosResource.RateRequest.RatingEnum> getVideoRatingAsync(string videoId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return VideosResource.RateRequest.RatingEnum.None;

            if (ratingStateCache.TryGetValue(videoId, out VideosResource.RateRequest.RatingEnum cached))
                return cached;

            using JsonDocument? document = await sendTvInnertubeAsync(@"next", new Dictionary<string, object?>
            {
                [@"videoId"] = videoId,
                [@"context"] = createTvBrowseContext(),
            }, cancellationToken).ConfigureAwait(false);

            if (document != null)
            {
                VideosResource.RateRequest.RatingEnum? parsed = tryReadRating(document.RootElement);

                if (parsed != null)
                {
                    ratingStateCache[videoId] = parsed.Value;
                    return parsed.Value;
                }
            }

            return VideosResource.RateRequest.RatingEnum.None;
        }

        private static VideosResource.RateRequest.RatingEnum? tryReadRating(JsonElement root)
        {
            foreach (JsonElement value in enumeratePropertyValues(root, @"likeStatus"))
            {
                string? status = getStringValue(value);

                switch (status)
                {
                    case @"LIKE":
                        return VideosResource.RateRequest.RatingEnum.Like;

                    case @"DISLIKE":
                        return VideosResource.RateRequest.RatingEnum.Dislike;

                    case @"INDIFFERENT":
                        return VideosResource.RateRequest.RatingEnum.None;
                }
            }

            return null;
        }

        private async Task rateVideoAsync(string videoId, VideosResource.RateRequest.RatingEnum rating, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            string endpoint = rating switch
            {
                VideosResource.RateRequest.RatingEnum.Like => @"like/like",
                VideosResource.RateRequest.RatingEnum.Dislike => @"like/dislike",
                _ => @"like/removelike",
            };

            using JsonDocument? document = await sendAuthenticatedInnertubeAsync(endpoint, client => new Dictionary<string, object?>
            {
                [@"target"] = new Dictionary<string, object?>
                {
                    [@"videoId"] = videoId,
                },
                [@"context"] = client == innertube_client.Tv ? createTvBrowseContext() : createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            if (document != null)
                ratingStateCache[videoId] = rating;
        }

        #endregion

        #region abuse reports

        private static IList<VideoAbuseReportReasonItem> getBuiltInAbuseReportReasons()
        {
            // Innertube only exposes the flag form for a specific video, so the (unused by the
            // report flow) global reason list cannot be fetched without one. A built-in list is
            // returned so the report overlay always has selectable entries.
            return new List<VideoAbuseReportReasonItem>
            {
                new VideoAbuseReportReasonItem
                {
                    Id = @"1",
                    Label = @"Sexual content",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"2",
                    Label = @"Violent or repulsive content",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"3",
                    Label = @"Hateful or abusive content",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"4",
                    Label = @"Harassment or bullying",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"5",
                    Label = @"Harmful or dangerous acts",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"6",
                    Label = @"Misinformation",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"7",
                    Label = @"Child abuse",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"8",
                    Label = @"Promotes terrorism",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"9",
                    Label = @"Spam or misleading",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"10",
                    Label = @"Infringes my rights",
                },
                new VideoAbuseReportReasonItem
                {
                    Id = @"11",
                    Label = @"Captions issue",
                },
            };
        }

        private async Task reportAbuseAsync(string videoId, string reasonId, string? secondaryReasonId, string? comments, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return;

            var flagParams = new Dictionary<string, object?>
            {
                [@"flagReasonId"] = reasonId,
            };

            if (!string.IsNullOrEmpty(secondaryReasonId))
                flagParams[@"flagSecondaryReasonId"] = secondaryReasonId;

            using JsonDocument? document = await sendAuthenticatedInnertubeAsync(@"flag/flag", client =>
            {
                var payload = new Dictionary<string, object?>
                {
                    [@"videoId"] = videoId,
                    [@"flagParams"] = flagParams,
                    [@"context"] = client == innertube_client.Tv ? createTvBrowseContext() : createWebClientContext(null),
                };

                if (!string.IsNullOrEmpty(comments))
                    payload[@"comments"] = comments;

                return payload;
            }, cancellationToken).ConfigureAwait(false);

            if (document == null)
                Logger.Log($@"Reporting the video '{videoId}' failed.", LoggingTarget.Network);
            else
                Logger.Log($@"Reported the video '{videoId}'.", LoggingTarget.Network);
        }

        #endregion

        #region json helpers

        private static string? getString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                _ => null,
            };
        }

        private static bool getBool(JsonElement element, string propertyName)
            => element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out JsonElement value)
               && value.ValueKind == JsonValueKind.True;

        internal static JsonElement? getObject(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
                return null;

            return value;
        }

        private static JsonElement? getNestedObject(JsonElement element, params string[] propertyPath)
        {
            JsonElement current = element;

            foreach (string propertyName in propertyPath)
            {
                JsonElement? next = getObject(current, propertyName);

                if (next == null)
                    return null;

                current = next.Value;
            }

            return current;
        }

        private static JsonElement? firstRenderer(JsonElement root, string rendererName)
        {
            foreach (JsonElement renderer in enumerateRenderers(root, rendererName))
                return renderer;

            return null;
        }

        private static double getDouble(JsonElement element, string propertyName)
        {
            string? raw = getString(element, propertyName);

            return raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
        }

        private static string? getBrowseId(JsonElement element)
        {
            JsonElement? endpoint = findProperty(element, @"browseEndpoint");

            return endpoint != null ? getString(endpoint.Value, @"browseId") : null;
        }

        private static string? tryGetNestedBrowseId(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
                return null;

            return getFirstRunBrowseId(value) ?? getBrowseId(value);
        }

        private static string? getFirstRunBrowseId(JsonElement element)
        {
            if (!element.TryGetProperty(@"runs", out JsonElement runs) || runs.ValueKind != JsonValueKind.Array)
                return null;

            foreach (JsonElement run in runs.EnumerateArray())
            {
                string? browseId = getBrowseId(run);

                if (!string.IsNullOrEmpty(browseId))
                    return browseId;
            }

            return null;
        }

        private static string? getFirstRunText(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            if (!element.TryGetProperty(@"runs", out JsonElement runs) || runs.ValueKind != JsonValueKind.Array)
                return extractText(element);

            foreach (JsonElement run in runs.EnumerateArray())
            {
                string? text = getString(run, @"text");

                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return null;
        }

        private static string? findFirstString(JsonElement root, IEnumerable<string> propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                foreach (JsonElement value in enumeratePropertyValues(root, propertyName))
                {
                    string? text = extractText(value);

                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }

            return null;
        }

        private static string? findFirstBrowseId(JsonElement root)
        {
            foreach (JsonElement value in enumeratePropertyValues(root, @"browseId"))
            {
                string? id = getStringValue(value);

                if (!string.IsNullOrEmpty(id) && id.StartsWith(@"UC", StringComparison.Ordinal))
                    return id;
            }

            return null;
        }

        internal static string? extractText(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.ToString();

                case JsonValueKind.Object:
                    if (element.TryGetProperty(@"simpleText", out JsonElement simpleText) && simpleText.ValueKind == JsonValueKind.String)
                        return simpleText.GetString();

                    if (element.TryGetProperty(@"content", out JsonElement content) && content.ValueKind == JsonValueKind.String)
                        return content.GetString();

                    if (element.TryGetProperty(@"text", out JsonElement text))
                    {
                        string? textValue = extractText(text);

                        if (!string.IsNullOrEmpty(textValue))
                            return textValue;
                    }

                    if (element.TryGetProperty(@"runs", out JsonElement runs) && runs.ValueKind == JsonValueKind.Array)
                    {
                        var builder = new StringBuilder();

                        foreach (JsonElement run in runs.EnumerateArray())
                        {
                            if (run.ValueKind == JsonValueKind.Object
                                && run.TryGetProperty(@"text", out JsonElement runText)
                                && runText.ValueKind == JsonValueKind.String)
                                builder.Append(runText.GetString());
                        }

                        return builder.Length > 0 ? builder.ToString() : null;
                    }

                    if (element.TryGetProperty(@"label", out JsonElement label))
                        return extractText(label);

                    break;
            }

            return null;
        }

        private static IEnumerable<JsonElement> enumerateRenderers(JsonElement element, string rendererName)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty(rendererName, out JsonElement renderer) && renderer.ValueKind == JsonValueKind.Object)
                    yield return renderer;

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    foreach (JsonElement nested in enumerateRenderers(property.Value, rendererName))
                        yield return nested;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in element.EnumerateArray())
                {
                    foreach (JsonElement nested in enumerateRenderers(child, rendererName))
                        yield return nested;
                }
            }
        }

        private static IEnumerable<JsonElement> enumeratePropertyValues(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals(propertyName) || string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                        yield return property.Value;

                    foreach (JsonElement nested in enumeratePropertyValues(property.Value, propertyName))
                        yield return nested;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in element.EnumerateArray())
                {
                    foreach (JsonElement nested in enumeratePropertyValues(child, propertyName))
                        yield return nested;
                }
            }
        }

        private static ulong? findCountWithKeyword(JsonElement root, params string[] keywords)
        {
            JsonElement? header = findPageHeader(root);

            if (header != null && findCountInScope(header.Value, keywords) is { } headerCount)
                return headerCount;

            return findCountInScope(root, keywords);
        }

        private static ulong? findCountInScope(JsonElement scope, string[] keywords)
        {
            foreach (string propertyName in new[] { @"content", @"simpleText", @"text", @"subscriberCountText", @"videoCountText" })
            {
                foreach (JsonElement value in enumeratePropertyValues(scope, propertyName))
                {
                    string? text = extractText(value);

                    if (string.IsNullOrEmpty(text) || !keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    ulong? count = parseRoughCount(text);

                    if (count != null)
                        return count;
                }
            }

            return null;
        }

        /// <summary>
        /// Locates the header of a channel page, which is rendered differently per client.
        /// </summary>
        private static JsonElement? findPageHeader(JsonElement root)
            => findProperty(root, @"pageHeaderRenderer")
               ?? findProperty(root, @"channelHeaderRenderer")
               ?? findProperty(root, @"c4TabbedHeaderRenderer");

        private static ulong? parseUlong(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    if (element.TryGetUInt64(out ulong number))
                        return number;

                    if (element.TryGetDouble(out double doubleValue))
                        return (ulong)Math.Max(0, Math.Round(doubleValue));

                    break;

                case JsonValueKind.String:
                    return parseRoughCount(element.GetString());
            }

            return null;
        }

        private static long? toLong(ulong? value)
            => value == null ? null : (long)Math.Min(value.Value, long.MaxValue);

        internal static ulong? parseRoughCount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Counts are localized with the interface language, so the CJK scale suffixes have to
            // be understood in addition to the latin ones.
            Match match = Regex.Match(text, @"([0-9][0-9.,']*)\s*(K|M|B|천|만|억|万|億)?", RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            double multiplier = match.Groups[2].Value.ToUpperInvariant() switch
            {
                @"K" => 1_000,
                @"M" => 1_000_000,
                @"B" => 1_000_000_000,
                @"천" => 1_000,
                @"만" or @"万" => 10_000,
                @"억" or @"億" => 100_000_000,
                _ => 1,
            };

            string numberPart = normalizeCountNumber(match.Groups[1].Value, multiplier != 1);

            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return null;

            return (ulong)Math.Round(value * multiplier);
        }

        /// <summary>
        /// Normalizes the separators of a localized count (for example <c>1,234</c>, <c>1.234</c>
        /// or <c>1.2천</c>) into an invariant representation.
        /// </summary>
        private static string normalizeCountNumber(string raw, bool hasMultiplier)
        {
            string number = raw.Trim().Replace(" ", string.Empty).Replace("\u00a0", string.Empty).Replace("'", string.Empty).TrimEnd('.', ',');

            if (number.Length == 0)
                return number;

            // A scaled count uses the separator as its decimal point (1.2K, 1,2만).
            if (hasMultiplier)
                return number.Replace(@",", @".");

            // Otherwise a separator followed by exactly three digits groups thousands.
            return Regex.IsMatch(number, @"^[0-9]{1,3}([.,][0-9]{3})+$")
                ? number.Replace(@",", string.Empty).Replace(@".", string.Empty)
                : number.Replace(@",", @".");
        }

        private static DateTimeOffset? parseDateTimeOffset(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset value)
                ? value
                : null;
        }

        internal static DateTimeOffset? parseRelativeDate(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            Match match = Regex.Match(text, @"(\d+)");

            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                return parseDateText(text);

            string lower = text.ToLowerInvariant();

            foreach ((string[] keywords, int unit) in relative_date_units)
            {
                if (!keywords.Any(keyword => lower.Contains(keyword, StringComparison.Ordinal)))
                    continue;

                DateTimeOffset now = DateTimeOffset.UtcNow;

                return unit switch
                {
                    0 => now.AddSeconds(-amount),
                    1 => now.AddMinutes(-amount),
                    2 => now.AddHours(-amount),
                    3 => now.AddDays(-amount),
                    4 => now.AddDays(-7 * amount),
                    5 => now.AddMonths(-amount),
                    _ => now.AddYears(-amount),
                };
            }

            // Pages that show the upload date instead of a relative one (for example the watch page)
            // render it as an absolute date.
            return parseDateText(text);
        }

        private static string? getLastThumbnailUrl(JsonElement? element)
        {
            if (element == null || element.Value.ValueKind != JsonValueKind.Object)
                return null;

            if (!element.Value.TryGetProperty(@"thumbnails", out JsonElement thumbnails) || thumbnails.ValueKind != JsonValueKind.Array || thumbnails.GetArrayLength() == 0)
                return null;

            string? url = null;

            foreach (JsonElement thumbnail in thumbnails.EnumerateArray())
            {
                string? candidate = getString(thumbnail, @"url");

                if (!string.IsNullOrEmpty(candidate))
                    url = candidate;
            }

            return url;
        }

        private static ThumbnailDetails createVideoThumbnails(string videoId)
        {
            string baseUrl = $@"https://i.ytimg.com/vi/{videoId}/";

            return new ThumbnailDetails
            {
                Default__ = new Thumbnail
                {
                    Url = baseUrl + @"default.jpg",
                    Width = 120,
                    Height = 90,
                },
                Medium = new Thumbnail
                {
                    Url = baseUrl + @"mqdefault.jpg",
                    Width = 320,
                    Height = 180,
                },
                High = new Thumbnail
                {
                    Url = baseUrl + @"hqdefault.jpg",
                    Width = 480,
                    Height = 360,
                },
                Standard = new Thumbnail
                {
                    Url = baseUrl + @"sddefault.jpg",
                    Width = 640,
                    Height = 480,
                },
                Maxres = new Thumbnail
                {
                    Url = baseUrl + @"maxresdefault.jpg",
                    Width = 1280,
                    Height = 720,
                },
            };
        }

        private static ThumbnailDetails createThumbnailDetails(string? url)
        {
            var details = new ThumbnailDetails();

            if (string.IsNullOrEmpty(url))
                return details;

            details.Default__ = new Thumbnail { Url = url };
            details.Medium = new Thumbnail { Url = url };
            details.High = new Thumbnail { Url = url };
            details.Standard = new Thumbnail { Url = url };
            details.Maxres = new Thumbnail { Url = url };

            return details;
        }

        #endregion

        #region sync bridges

        internal static T runSync<T>(Func<Task<T>> taskFactory)
            // Starting the work on the thread pool keeps the continuation of every inner await off
            // the caller's synchronization context, so blocking an update-thread caller cannot
            // deadlock the operation.
            => Task.Run(taskFactory).GetAwaiter().GetResult();

        internal static void runSync(Func<Task> taskFactory)
            => Task.Run(taskFactory).GetAwaiter().GetResult();

        #endregion
    }
}

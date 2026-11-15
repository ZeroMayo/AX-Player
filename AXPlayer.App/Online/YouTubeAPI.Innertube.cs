// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
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

        private const string youtube_origin = @"https://www.youtube.com";
        private const string music_topic_category = @"https://en.wikipedia.org/wiki/Music";

        private readonly Dictionary<string, bool> subscriptionStateCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, VideosResource.RateRequest.RatingEnum> ratingStateCache = new Dictionary<string, VideosResource.RateRequest.RatingEnum>(StringComparer.Ordinal);

        private string? mineChannelIdCache;
        private Channel? mineChannelCache;

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
            (@"FEplaylists", innertube_client.Web),
            (@"FEmy_youtube", innertube_client.Tv),
        };

        /// <summary>
        /// Endpoints that expose the identities of the signed-in account.
        /// </summary>
        private static readonly string[] own_channel_account_endpoints =
        {
            @"account/account_menu",
            @"account/accounts_list",
        };

        #region transport

        private async Task<JsonDocument?> sendInnertubeAsync(string endpoint, object payload, CancellationToken cancellationToken, innertube_client client = innertube_client.Web, bool authenticate = false)
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

        private async Task<JsonDocument?> sendTvInnertubeAsync(string endpoint, object payload, CancellationToken cancellationToken)
            => await sendInnertubeAsync(endpoint, payload, cancellationToken, innertube_client.Tv, googleOAuth2.SignedIn.Value).ConfigureAwait(false);

        /// <summary>
        /// Sends an authenticated request as the TV client, then retries as the web client when
        /// the endpoint is not supported by the TV client.
        /// </summary>
        private async Task<JsonDocument?> sendAuthenticatedInnertubeAsync(string endpoint, Func<innertube_client, object> payloadFactory, CancellationToken cancellationToken)
        {
            JsonDocument? document = await sendInnertubeAsync(endpoint, payloadFactory(innertube_client.Tv), cancellationToken, innertube_client.Tv, true).ConfigureAwait(false);

            if (document != null)
                return document;

            return await sendInnertubeAsync(endpoint, payloadFactory(innertube_client.Web), cancellationToken, innertube_client.Web, true).ConfigureAwait(false);
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

        private object createWebClientContext(string? visitorData)
        {
            var client = new Dictionary<string, object?>
            {
                [@"clientName"] = @"WEB",
                [@"clientVersion"] = web_client_version,
                [@"hl"] = @"en",
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
                [@"context"] = createWebClientContext(visitorData),
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
                applyWatchMetadata(video, next.RootElement);

            return video;
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

            if (isLiveContent)
            {
                DateTimeOffset? start = parseDateTimeOffset(getLiveBroadcastTimestamp(microformat, @"startTimestamp"));

                video.LiveStreamingDetails = new VideoLiveStreamingDetails
                {
                    ActualStartTimeRaw = start?.ToString(@"o", CultureInfo.InvariantCulture),
                    ActualStartTimeDateTimeOffset = start,
                    ScheduledStartTimeRaw = start?.ToString(@"o", CultureInfo.InvariantCulture),
                    ScheduledStartTimeDateTimeOffset = start,
                    ConcurrentViewers = isLiveNow ? parseRoughCount(getString(details, @"viewCount")) : null,
                };
            }

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

                        if (string.IsNullOrEmpty(text) || !text.Contains(@"like", StringComparison.OrdinalIgnoreCase))
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

                if (!title.Equals(@"Category", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!metadataRow.TryGetProperty(@"contents", out JsonElement contents) || contents.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement content in contents.EnumerateArray())
                {
                    if (string.Equals(extractText(content), @"Music", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static string? getLiveBroadcastTimestamp(JsonElement? microformat, string propertyName)
        {
            if (microformat == null)
                return null;

            if (!microformat.Value.TryGetProperty(@"liveBroadcastDetails", out JsonElement details))
                return null;

            return getString(details, propertyName);
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
                    SubscriberCount = findCountWithKeyword(root, @"subscriber"),
                    VideoCount = findCountWithKeyword(root, @"video"),
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

            // Newer page headers expose the handle as "@name" in the metadata rows.
            foreach (JsonElement value in enumeratePropertyValues(root, @"content"))
            {
                string? text = value.GetString();

                if (!string.IsNullOrEmpty(text) && text.StartsWith('@') && !text.Contains(' '))
                    return text;
            }

            return null;
        }

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
                string? browseId = value.GetString();

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
                    string? text = value.GetString();

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

            foreach (JsonElement renderer in enumerateSearchRenderers(document.RootElement))
            {
                SearchResult? result = null;

                if (renderer.TryGetProperty(@"videoRenderer", out JsonElement videoRenderer))
                    result = parseVideoSearchResult(videoRenderer);
                else if (renderer.TryGetProperty(@"playlistRenderer", out JsonElement playlistRenderer))
                    result = parsePlaylistSearchResult(playlistRenderer);

                if (result != null)
                    results.Add(result);
            }

            if (orderEnum == SearchResource.ListRequest.OrderEnum.Title)
                results.Sort(static (a, b) => string.Compare(a.Snippet?.Title, b.Snippet?.Title, StringComparison.CurrentCultureIgnoreCase));

            return results;
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
                if (badge.TryGetProperty(@"metadataBadgeRenderer", out JsonElement metadataBadge)
                    && metadataBadge.TryGetProperty(@"label", out JsonElement label)
                    && label.ValueKind == JsonValueKind.String
                    && string.Equals(label.GetString(), @"LIVE", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        #endregion

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

            List<JsonElement> items = await fetchContinuationItemsAsync(continuation, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(pageToken) && orderEnum == CommentThreadsResource.ListRequest.OrderEnum.Time)
            {
                string? newestFirst = findSortContinuation(items, @"newest");

                if (!string.IsNullOrEmpty(newestFirst) && newestFirst != continuation)
                {
                    List<JsonElement> sortedItems = await fetchContinuationItemsAsync(newestFirst, cancellationToken).ConfigureAwait(false);

                    if (sortedItems.Count > 0)
                    {
                        items = sortedItems;
                        continuation = newestFirst;
                    }
                }
            }

            var replyContinuations = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (JsonElement item in items)
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                if (item.TryGetProperty(@"commentThreadRenderer", out JsonElement threadRenderer))
                {
                    CommentThread? thread = parseCommentThread(threadRenderer, videoId);

                    if (thread == null)
                        continue;

                    if (thread.Snippet?.TopLevelComment != null)
                        response.Items.Add(thread);

                    // Older responses returned a handful of replies inline instead of a continuation.
                    List<Comment>? inlineReplies = parseInlineReplies(threadRenderer, videoId, thread.Id);

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

        private async Task<List<JsonElement>> fetchContinuationItemsAsync(string continuation, CancellationToken cancellationToken)
        {
            using JsonDocument? document = await sendInnertubeAsync(@"next", new Dictionary<string, object?>
            {
                [@"continuation"] = continuation,
                [@"context"] = createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return new List<JsonElement>();

            return extractContinuationItems(document.RootElement);
        }

        private static List<JsonElement> extractContinuationItems(JsonElement root)
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
                [@"context"] = createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return null;

            foreach (JsonElement section in enumerateRenderers(document.RootElement, @"itemSectionRenderer"))
            {
                if (!section.TryGetProperty(@"sectionIdentifier", out JsonElement identifier))
                    continue;

                if (!string.Equals(identifier.GetString(), @"comment-item-section", StringComparison.Ordinal))
                    continue;

                string? continuation = findContinuation(section);

                if (!string.IsNullOrEmpty(continuation))
                    return continuation;
            }

            JsonElement? commentsHeader = findProperty(document.RootElement, @"commentsEntryPointHeaderRenderer");

            return commentsHeader != null ? findContinuation(commentsHeader.Value) : null;
        }

        private static string? findSortContinuation(IEnumerable<JsonElement> items, string keyword)
        {
            foreach (JsonElement item in items)
            {
                foreach (JsonElement chip in enumerateRenderers(item, @"chipCloudChipRenderer"))
                {
                    if (!chip.TryGetProperty(@"text", out JsonElement textElement))
                        continue;

                    string text = extractText(textElement) ?? string.Empty;

                    if (!text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string? continuation = findContinuation(chip);

                    if (!string.IsNullOrEmpty(continuation))
                        return continuation;
                }
            }

            return null;
        }

        internal static CommentThread? parseCommentThread(JsonElement threadRenderer, string videoId)
        {
            JsonElement? commentRenderer = null;

            if (threadRenderer.TryGetProperty(@"comment", out JsonElement commentWrapper)
                && commentWrapper.TryGetProperty(@"commentRenderer", out JsonElement renderer))
                commentRenderer = renderer;
            else if (threadRenderer.TryGetProperty(@"commentViewModel", out JsonElement commentViewModel))
                commentRenderer = commentViewModel;

            if (commentRenderer == null)
                return null;

            Comment? comment = parseComment(commentRenderer.Value, videoId, null);

            if (comment == null)
                return null;

            ulong? replyCount = parseUlong(commentRenderer.Value.TryGetProperty(@"replyCount", out JsonElement replyCountElement) ? replyCountElement : default);

            if (replyCount == null && commentRenderer.Value.TryGetProperty(@"viewReplies", out JsonElement viewReplies))
            {
                JsonElement? buttonRenderer = findProperty(viewReplies, @"buttonRenderer");

                if (buttonRenderer != null && buttonRenderer.Value.TryGetProperty(@"text", out JsonElement buttonText))
                    replyCount = parseRoughCount(extractText(buttonText));
            }

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

        internal static Comment? parseComment(JsonElement renderer, string? videoId, string? parentId)
        {
            string? commentId = getString(renderer, @"commentId");

            if (string.IsNullOrEmpty(commentId))
                commentId = getString(renderer, @"commentKey");

            if (string.IsNullOrEmpty(commentId))
                return null;

            string? content = extractText(renderer.TryGetProperty(@"contentText", out JsonElement contentText) ? contentText : default);

            if (string.IsNullOrEmpty(content))
                content = findFirstString(renderer, new[] { @"contentText", @"content", @"text" });

            string? authorName = extractText(renderer.TryGetProperty(@"authorText", out JsonElement authorText) ? authorText : default);

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
                    authorChannelId = getString(channelIdProperty.Value, @"value") ?? channelIdProperty.Value.GetString();
            }

            string? authorThumbnail = getLastThumbnailUrl(renderer.TryGetProperty(@"authorThumbnail", out JsonElement authorThumbnailElement) ? authorThumbnailElement : (JsonElement?)null);

            string? publishedTimeText = extractText(renderer.TryGetProperty(@"publishedTimeText", out JsonElement publishedElement) ? publishedElement : default);

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

        private static List<Comment>? parseInlineReplies(JsonElement threadRenderer, string videoId, string? threadId)
        {
            if (!threadRenderer.TryGetProperty(@"replies", out JsonElement replies))
                return null;

            var result = new List<Comment>();

            foreach (JsonElement commentRenderer in enumerateRenderers(replies, @"commentRenderer"))
            {
                Comment? comment = parseComment(commentRenderer, videoId, threadId);

                if (comment != null)
                    result.Add(comment);
            }

            return result.Count > 0 ? result : null;
        }

        private static string? findRepliesContinuation(JsonElement threadRenderer)
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
                List<JsonElement> items = await fetchContinuationItemsAsync(continuation, cancellationToken).ConfigureAwait(false);

                foreach (JsonElement item in items)
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

                    Comment? comment = parseComment(commentRenderer, videoId, threadId);

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
                [@"context"] = createWebClientContext(null),
            }, cancellationToken).ConfigureAwait(false);

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
            }, cancellationToken).ConfigureAwait(false);

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
                return playlists;

            foreach ((string browseId, innertube_client client) in playlist_library_pages)
            {
                using JsonDocument? document = await browseAuthenticatedAsync(browseId, cancellationToken, client).ConfigureAwait(false);

                if (document == null)
                    continue;

                collectPlaylists(document.RootElement, playlists);

                if (playlists.Count > 0)
                    break;
            }

            return playlists;
        }

        private static void collectPlaylists(JsonElement root, List<Playlist> playlists)
        {
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

        private static Playlist? parsePlaylistLockup(JsonElement renderer)
        {
            if (renderer.TryGetProperty(@"contentType", out JsonElement contentType)
                && contentType.ValueKind == JsonValueKind.String
                && !string.Equals(contentType.GetString(), @"LOCKUP_CONTENT_TYPE_PLAYLIST", StringComparison.Ordinal))
                return null;

            string? playlistId = getString(renderer, @"contentId");

            if (string.IsNullOrEmpty(playlistId))
                return null;

            // Lockups also cover videos; only keep content that looks like a playlist.
            if (!playlistId.StartsWith(@"PL", StringComparison.Ordinal)
                && !playlistId.StartsWith(@"VL", StringComparison.Ordinal)
                && !playlistId.StartsWith(@"UU", StringComparison.Ordinal)
                && !playlistId.StartsWith(@"FL", StringComparison.Ordinal)
                && !playlistId.StartsWith(@"OL", StringComparison.Ordinal))
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

            if (document != null && googleOAuth2.SignedIn.Value && !isUsablePlaylistResponse(document.RootElement))
            {
                document.Dispose();
                return await browseAuthenticatedAsync(browseId, cancellationToken).ConfigureAwait(false);
            }

            return document;
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

            if (document == null)
                return createEmptyPlaylist(playlistId);

            JsonElement root = document.RootElement;
            string? title = null;
            string? description = null;
            string? ownerChannelId = null;
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

            var playlist = createEmptyPlaylist(playlistId);

            playlist.Snippet = new PlaylistSnippet
            {
                Title = title,
                Description = description,
                ChannelId = ownerChannelId,
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

        private async Task<IList<PlaylistItem>> fetchPlaylistItemsAsync(string playlistId, CancellationToken cancellationToken)
        {
            var items = new List<PlaylistItem>();

            using JsonDocument? document = await browsePlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return items;

            JsonElement root = document.RootElement;

            foreach (JsonElement renderer in enumerateRenderers(root, @"playlistVideoRenderer"))
            {
                PlaylistItem? item = parsePlaylistVideoRenderer(renderer, playlistId, items.Count);

                if (item != null)
                    items.Add(item);
            }

            if (items.Count > 0)
                return items;

            foreach (JsonElement renderer in enumerateRenderers(root, @"lockupViewModel"))
            {
                PlaylistItem? item = parsePlaylistItemLockup(renderer, playlistId, items.Count);

                if (item != null)
                    items.Add(item);
            }

            return items;
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

            var payload = new
            {
                context = createTvBrowseContext(),
                playlistId,
                actions = new object[]
                {
                    new
                    {
                        action = @"ACTION_REMOVE_VIDEO",
                        setVideoId = target.Id,
                        removedVideoId = videoId,
                    },
                },
            };

            using JsonDocument? document = await sendTvEditRequestAsync(payload, cancellationToken).ConfigureAwait(false);

            if (document == null)
                Logger.Log($@"Removing '{videoId}' from the playlist '{playlistId}' failed.", LoggingTarget.Network);
        }

        private async Task<bool> isVideoExistsOnPlaylistAsync(string playlistId, string videoId, CancellationToken cancellationToken)
        {
            if (!googleOAuth2.SignedIn.Value)
                return false;

            IList<PlaylistItem> items = await fetchPlaylistItemsAsync(playlistId, cancellationToken).ConfigureAwait(false);

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
                    string? text = value.GetString()?.ToLowerInvariant();

                    if (text is @"public" or @"unlisted" or @"private")
                        return text;
                }
            }

            foreach (JsonElement value in enumeratePropertyValues(root, @"content"))
            {
                string? text = value.GetString()?.ToLowerInvariant();

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
                    string? text = extractText(value) ?? value.GetString();

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
                string? status = value.GetString();

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
                string? id = value.GetString();

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

        private static ulong? findCountWithKeyword(JsonElement root, string keyword)
        {
            foreach (string propertyName in new[] { @"content", @"simpleText", @"text", @"subscriberCountText", @"videoCountText" })
            {
                foreach (JsonElement value in enumeratePropertyValues(root, propertyName))
                {
                    string? text = extractText(value);

                    if (string.IsNullOrEmpty(text) || !text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        continue;

                    ulong? count = parseRoughCount(text);

                    if (count != null)
                        return count;
                }
            }

            return null;
        }

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

            Match match = Regex.Match(text, @"([0-9][0-9.,]*)\s*([KMB])?", RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            string numberPart = match.Groups[1].Value.Replace(@",", string.Empty).TrimEnd('.');

            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return null;

            double multiplier = match.Groups[2].Value.ToUpperInvariant() switch
            {
                @"K" => 1_000,
                @"M" => 1_000_000,
                @"B" => 1_000_000_000,
                _ => 1,
            };

            return (ulong)Math.Round(value * multiplier);
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

            Match match = Regex.Match(text, @"(\d+)\s*(second|minute|hour|day|week|month|year)s?\s*ago", RegexOptions.IgnoreCase);

            if (!match.Success || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                return null;

            DateTimeOffset now = DateTimeOffset.UtcNow;

            return match.Groups[2].Value.ToLowerInvariant() switch
            {
                @"second" => now.AddSeconds(-amount),
                @"minute" => now.AddMinutes(-amount),
                @"hour" => now.AddHours(-amount),
                @"day" => now.AddDays(-amount),
                @"week" => now.AddDays(-7 * amount),
                @"month" => now.AddMonths(-amount),
                @"year" => now.AddYears(-amount),
                _ => null,
            };
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

        private static T runSync<T>(Func<Task<T>> taskFactory) => taskFactory().GetAwaiter().GetResult();

        private static void runSync(Func<Task> taskFactory) => taskFactory().GetAwaiter().GetResult();

        #endregion
    }
}

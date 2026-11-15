// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Account-bound YouTube tracking information for a single playback session.
    /// </summary>
    public sealed class WatchHistoryTracking
    {
        public string VideoId { get; init; } = string.Empty;

        public string ClientPlaybackNonce { get; init; } = string.Empty;

        public string VideoLengthSeconds { get; init; } = @"0";

        public string? VisitorData { get; init; }

        public string PlaybackUrl { get; init; } = string.Empty;

        public string WatchtimeUrl { get; init; } = string.Empty;
    }

    public partial class YouTubeAPI
    {
        /// <summary>
        /// The Innertube browse ID of the "History" page of the signed-in account.
        /// </summary>
        private const string watch_history_browse_id = @"FEhistory";

        // OAuth access tokens are only accepted by the TV Innertube client. The web client
        // requires cookie (SAPISIDHASH) authentication, which this application doesn't have.
        private const string tv_browse_client_version = @"7.20250209.19.00";
        private const string tv_browse_user_agent = @"Mozilla/5.0 (SMART-TV; Linux; Tizen 5.0) AppleWebKit/538.1 (KHTML, like Gecko) Version/5.0 NativeTVAds Safari/538.1,gzip(gfe)";

        // The regular TVHTML5 7.x client requires a living-room BotGuard PO token and answers
        // signed-in /player requests with UNPLAYABLE. The downgraded 5.x client still returns
        // account-bound playbackTracking URLs. YouTube currently rejects the Cobalt user agent
        // with UNPLAYABLE, so the PlayStation 4 user agent is used instead.
        private const string tv_player_client_version = @"5.20260901";
        private const string tv_player_user_agent = @"Mozilla/5.0 (PlayStation; PlayStation 4/12.00) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Safari/605.1.15";

        private const string tv_browse_endpoint = @"https://www.googleapis.com/youtubei/v1/browse?key=AIzaSyDCU8hByM-4DrUqRUYnGn-3llEO78bcxq8";
        private const string tv_browse_edit_endpoint = @"https://www.youtube.com/youtubei/v1/browse/edit_playlist?prettyPrint=false";
        private const string tv_player_endpoint = @"https://www.youtube.com/youtubei/v1/player?prettyPrint=false";
        private const string innertube_api_key = @"AIzaSyAO_FJ2SlqU8Q4STEHLGCilw_Y9_11qcW8";

        private const string cpn_alphabet = @"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

        private int? signatureTimestamp;

        /// <summary>
        /// Fetches the watch history of the signed-in account.
        /// </summary>
        /// <remarks>
        /// The history is returned newest first. YouTube only returns it for signed-in accounts,
        /// so an empty list is returned when the user is signed out.
        /// </remarks>
        public async Task<IReadOnlyList<WatchHistoryItem>> GetWatchHistoryAsync(int maxPages = 5, CancellationToken cancellationToken = default)
        {
            var items = new List<WatchHistoryItem>();

            if (!googleOAuth2.SignedIn.Value)
                return items;

            string? continuation = null;

            for (int page = 0; page < Math.Max(1, maxPages); page++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                WatchHistoryPage result = await GetWatchHistoryPageAsync(continuation, cancellationToken).ConfigureAwait(false);

                if (result.Items.Count == 0 && page == 0)
                    break;

                items.AddRange(result.Items);

                if (string.IsNullOrEmpty(result.Continuation) || result.Continuation == continuation)
                    break;

                continuation = result.Continuation;
            }

            return items;
        }

        /// <summary>
        /// Fetches a single page of the watch history of the signed-in account.
        /// Pass the <see cref="WatchHistoryPage.Continuation"/> of a previous page to fetch the next one.
        /// </summary>
        public async Task<WatchHistoryPage> GetWatchHistoryPageAsync(string? continuation = null, CancellationToken cancellationToken = default)
        {
            if (!googleOAuth2.SignedIn.Value)
                return WatchHistoryPage.Empty;

            object payload = string.IsNullOrEmpty(continuation)
                ? new
                {
                    context = createTvBrowseContext(),
                    browseId = watch_history_browse_id,
                }
                : new
                {
                    context = createTvBrowseContext(),
                    continuation,
                };

            using JsonDocument? document = await sendTvBrowseRequestAsync(payload, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return WatchHistoryPage.Empty;

            WatchHistoryPage page = WatchHistoryParser.Parse(document);

            if (page.Items.Count == 0)
                Logger.Log(@"The watch history response did not contain any videos.", LoggingTarget.Network);

            return page;
        }

        /// <summary>
        /// Adds a video to the signed-in account's watch history playlist (HL).
        /// </summary>
        public async Task<bool> AddVideoToWatchHistoryAsync(string videoId, CancellationToken cancellationToken = default)
        {
            if (!googleOAuth2.SignedIn.Value || string.IsNullOrWhiteSpace(videoId))
                return false;

            var payload = new
            {
                context = createTvBrowseContext(),
                playlistId = @"HL",
                actions = new[]
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
                return false;

            JsonElement root = document.RootElement;

            if (root.TryGetProperty(@"actions", out JsonElement actions)
                && actions.ValueKind == JsonValueKind.Array
                && actions.GetArrayLength() > 0)
            {
                Logger.Log($@"Added '{videoId}' to the watch history playlist.", LoggingTarget.Network);
                return true;
            }

            Logger.Log($@"The watch history playlist response for '{videoId}' did not contain actions.", LoggingTarget.Network);
            return false;
        }

        /// <summary>
        /// Resolves the account-bound playback tracking URLs of a video.
        /// </summary>
        public async Task<WatchHistoryTracking?> GetWatchHistoryTrackingAsync(string videoId, CancellationToken cancellationToken = default)
        {
            if (!googleOAuth2.SignedIn.Value || string.IsNullOrWhiteSpace(videoId))
                return null;

            string cpn = generateCpn();

            using JsonDocument? document = await sendTvPlayerRequestWithFallbackAsync(videoId, cpn, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return null;

            JsonElement root = document.RootElement;

            if (!root.TryGetProperty(@"playbackTracking", out JsonElement playbackTracking)
                || playbackTracking.ValueKind != JsonValueKind.Object)
            {
                string status = string.Empty;

                if (root.TryGetProperty(@"playabilityStatus", out JsonElement playabilityStatus))
                {
                    string? statusValue = playabilityStatus.TryGetProperty(@"status", out JsonElement statusElement) ? statusElement.GetString() : null;
                    string? reasonValue = playabilityStatus.TryGetProperty(@"reason", out JsonElement reasonElement) ? reasonElement.GetString() : null;
                    status = $@"{statusValue}: {reasonValue}";
                }

                Logger.Log($@"The player response for '{videoId}' did not contain playback tracking URLs ({status}).", LoggingTarget.Network);
                return null;
            }

            string? playbackUrl = getTrackingBaseUrl(playbackTracking, @"videostatsPlaybackUrl");
            string? watchtimeUrl = getTrackingBaseUrl(playbackTracking, @"videostatsWatchtimeUrl");

            if (string.IsNullOrEmpty(playbackUrl) || string.IsNullOrEmpty(watchtimeUrl))
            {
                Logger.Log($@"The player response for '{videoId}' did not contain both tracking URLs.", LoggingTarget.Network);
                return null;
            }

            bool accountBound = watchtimeUrl.Contains(@"plid=", StringComparison.OrdinalIgnoreCase)
                                && watchtimeUrl.Contains(@"el=leanback", StringComparison.OrdinalIgnoreCase);

            string watchtimeHost = Uri.TryCreate(watchtimeUrl, UriKind.Absolute, out Uri? watchtimeUri) ? watchtimeUri.Host : @"?";

            Logger.Log($@"Resolved watch history tracking URLs for '{videoId}' (account-bound: {accountBound}, host: {watchtimeHost}).", LoggingTarget.Network);

            double videoLengthSeconds = 0;

            if (root.TryGetProperty(@"videoDetails", out JsonElement videoDetails)
                && videoDetails.TryGetProperty(@"lengthSeconds", out JsonElement lengthSecondsElement))
            {
                if (lengthSecondsElement.ValueKind == JsonValueKind.String)
                    double.TryParse(lengthSecondsElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out videoLengthSeconds);
                else if (lengthSecondsElement.ValueKind == JsonValueKind.Number)
                    videoLengthSeconds = lengthSecondsElement.GetDouble();
            }

            string? visitorData = null;

            if (root.TryGetProperty(@"responseContext", out JsonElement responseContext)
                && responseContext.TryGetProperty(@"visitorData", out JsonElement visitorDataElement)
                && visitorDataElement.ValueKind == JsonValueKind.String)
                visitorData = visitorDataElement.GetString();

            return new WatchHistoryTracking
            {
                VideoId = videoId,
                ClientPlaybackNonce = cpn,
                VideoLengthSeconds = videoLengthSeconds.ToString(@"0.###", CultureInfo.InvariantCulture),
                VisitorData = visitorData,
                PlaybackUrl = playbackUrl,
                WatchtimeUrl = watchtimeUrl,
            };
        }

        /// <summary>
        /// Sends the playback start event of a watch history session.
        /// </summary>
        public Task SendWatchHistoryPlaybackAsync(WatchHistoryTracking tracking, CancellationToken cancellationToken = default)
            => sendStatsPingAsync(tracking.PlaybackUrl, @"videostatsPlaybackUrl", tracking, null, null, true, cancellationToken);

        /// <summary>
        /// Sends a watched segment of a watch history session.
        /// </summary>
        public Task SendWatchHistoryWatchTimeAsync(WatchHistoryTracking tracking, double startSeconds, double endSeconds, bool final = false, CancellationToken cancellationToken = default)
            => sendStatsPingAsync(tracking.WatchtimeUrl, @"videostatsWatchtimeUrl", tracking,
                startSeconds.ToString(@"0.###", CultureInfo.InvariantCulture),
                endSeconds.ToString(@"0.###", CultureInfo.InvariantCulture),
                final,
                cancellationToken);

        /// <summary>
        /// Marks a video as fully watched. Used when no playtime tracking is available.
        /// </summary>
        public async Task MarkVideoAsWatchedAsync(string videoId, CancellationToken cancellationToken = default)
        {
            WatchHistoryTracking? tracking = await GetWatchHistoryTrackingAsync(videoId, cancellationToken).ConfigureAwait(false);

            if (tracking == null)
                return;

            await SendWatchHistoryPlaybackAsync(tracking, cancellationToken).ConfigureAwait(false);
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);

            double.TryParse(tracking.VideoLengthSeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out double length);
            await SendWatchHistoryWatchTimeAsync(tracking, 0, length, true, cancellationToken).ConfigureAwait(false);
        }

        private async Task sendStatsPingAsync(string baseUrl, string trackingName, WatchHistoryTracking tracking, string? start, string? end, bool final, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return;

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [@"ns"] = @"yt",
                [@"ver"] = @"2",
                [@"docid"] = tracking.VideoId,
                [@"cpn"] = tracking.ClientPlaybackNonce,
                [@"cmt"] = end ?? @"0",
            };

            if (!string.IsNullOrEmpty(tracking.VideoLengthSeconds) && tracking.VideoLengthSeconds != @"0")
                parameters[@"len"] = tracking.VideoLengthSeconds;

            if (!string.IsNullOrEmpty(start))
                parameters[@"st"] = start;

            if (!string.IsNullOrEmpty(end))
                parameters[@"et"] = end;

            if (final)
                parameters[@"final"] = @"1";

            string url = normalizeStatsUrl(setQueryParameters(baseUrl, parameters));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                request.Headers.TryAddWithoutValidation(@"User-Agent", tv_player_user_agent);
                request.Headers.TryAddWithoutValidation(@"Referer", @"https://www.youtube.com/tv");
                request.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {googleOAuth2.GetAccessToken()}");
                request.Headers.TryAddWithoutValidation(@"X-Goog-AuthUser", @"0");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Name", @"7");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Version", tv_player_client_version);
                request.Headers.TryAddWithoutValidation(@"Origin", @"https://www.youtube.com");

                if (!string.IsNullOrEmpty(tracking.VisitorData))
                    request.Headers.TryAddWithoutValidation(@"X-Goog-Visitor-Id", tracking.VisitorData);

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    if (body.Length > 300)
                        body = body[..300];

                    Logger.Log($@"Watch history {trackingName} ping failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}", LoggingTarget.Network);
                }
                else
                {
                    // 204 No Content is the normal success response of the stats beacon endpoints.
                    string targetHost = Uri.TryCreate(url, UriKind.Absolute, out Uri? targetUri) ? targetUri.Host : @"?";
                    Logger.Log($@"Watch history {trackingName} ping accepted ({(int)response.StatusCode} {response.StatusCode}, host: {targetHost}, st: {start ?? @"-"}, et: {end ?? @"-"}).", LoggingTarget.Network);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Watch history {trackingName} ping failed: {exception.GetDescription()}");
            }
        }

        /// <summary>
        /// The tracking URLs returned by the player often point at s.youtube.com. That host
        /// redirects to www.youtube.com, and HttpClient drops the Authorization header while
        /// following a cross-host redirect, which makes the ping anonymous. Send the ping to
        /// www.youtube.com directly so the Bearer token is kept.
        /// </summary>
        private static string normalizeStatsUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return url;

            if (uri.Host.Equals(@"www.youtube.com", StringComparison.OrdinalIgnoreCase))
                return url;

            if (!uri.Host.Equals(@"youtube.com", StringComparison.OrdinalIgnoreCase)
                && !uri.Host.EndsWith(@".youtube.com", StringComparison.OrdinalIgnoreCase))
                return url;

            var builder = new UriBuilder(uri)
            {
                Host = @"www.youtube.com",
                Port = -1,
            };

            return builder.Uri.ToString();
        }

        private static string? getTrackingBaseUrl(JsonElement playbackTracking, string trackingName)
        {
            if (playbackTracking.TryGetProperty(trackingName, out JsonElement tracking)
                && tracking.TryGetProperty(@"baseUrl", out JsonElement baseUrlElement)
                && baseUrlElement.ValueKind == JsonValueKind.String)
                return baseUrlElement.GetString();

            return null;
        }

        private async Task<JsonDocument?> sendTvPlayerRequestWithFallbackAsync(string videoId, string cpn, CancellationToken cancellationToken)
        {
            JsonDocument? document = await sendTvPlayerRequestAsync(videoId, cpn, false, cancellationToken).ConfigureAwait(false);

            if (document == null)
                return null;

            if (document.RootElement.TryGetProperty(@"playbackTracking", out _))
                return document;

            document.Dispose();
            Logger.Log($@"The player response for '{videoId}' did not contain tracking URLs without params; retrying with params.", LoggingTarget.Network);

            return await sendTvPlayerRequestAsync(videoId, cpn, true, cancellationToken).ConfigureAwait(false);
        }

        private async Task<JsonDocument?> sendTvPlayerRequestAsync(string videoId, string cpn, bool includeParams, CancellationToken cancellationToken)
        {
            try
            {
                string? visitorData = null;

                try
                {
                    visitorData = await ResolveVisitorDataAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $@"Failed to resolve visitor data for watch history: {exception.GetDescription()}");
                }

                int? sts = await resolveSignatureTimestampAsync(videoId, cancellationToken).ConfigureAwait(false);

                var payload = new Dictionary<string, object?>
                {
                    [@"videoId"] = videoId,
                    [@"cpn"] = cpn,
                    [@"contentCheckOk"] = true,
                    [@"racyCheckOk"] = true,
                    [@"context"] = createTvPlayerContext(visitorData),
                };

                if (includeParams)
                    payload[@"params"] = @"2AMB";

                if (sts != null)
                    payload[@"playbackContext"] = new { contentPlaybackContext = new { signatureTimestamp = sts.Value } };

                using var request = new HttpRequestMessage(HttpMethod.Post, tv_player_endpoint);

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, @"application/json");
                request.Headers.TryAddWithoutValidation(@"User-Agent", tv_player_user_agent);
                request.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {googleOAuth2.GetAccessToken()}");
                request.Headers.TryAddWithoutValidation(@"X-Goog-AuthUser", @"0");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Bootstrap-Logged-In", @"true");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Name", @"7");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Version", tv_player_client_version);
                request.Headers.TryAddWithoutValidation(@"X-Goog-Api-Key", innertube_api_key);
                request.Headers.TryAddWithoutValidation(@"Origin", @"https://www.youtube.com");

                if (!string.IsNullOrEmpty(visitorData))
                    request.Headers.TryAddWithoutValidation(@"X-Goog-Visitor-Id", visitorData);

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    if (body.Length > 500)
                        body = body[..500];

                    Logger.Log($@"Watch history player request for '{videoId}' failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}", LoggingTarget.Network);
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
                Logger.Error(exception, $@"Watch history player request for '{videoId}' failed: {exception.GetDescription()}");
                return null;
            }
        }

        private async Task<int?> resolveSignatureTimestampAsync(string videoId, CancellationToken cancellationToken)
        {
            if (signatureTimestamp != null)
                return signatureTimestamp;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $@"https://www.youtube.com/watch?v={videoId}&hl=en&gl=US");
                request.Headers.TryAddWithoutValidation(@"User-Agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                request.Headers.TryAddWithoutValidation(@"Accept-Language", @"en-US,en;q=0.9");

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return null;

                string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                Match stsMatch = Regex.Match(html, @"""(?:STS|signatureTimestamp)""\s*:\s*(\d{5,})");

                if (!stsMatch.Success)
                {
                    Match jsUrlMatch = Regex.Match(html, @"""jsUrl""\s*:\s*""([^""]+)""");

                    if (jsUrlMatch.Success)
                    {
                        string jsUrl = jsUrlMatch.Groups[1].Value;

                        if (jsUrl.StartsWith(@"/"))
                            jsUrl = @"https://www.youtube.com" + jsUrl;

                        using var jsRequest = new HttpRequestMessage(HttpMethod.Get, jsUrl);
                        jsRequest.Headers.TryAddWithoutValidation(@"User-Agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

                        using HttpResponseMessage jsResponse = await Http.SendAsync(jsRequest, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);

                        if (jsResponse.IsSuccessStatusCode)
                        {
                            string js = await jsResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                            stsMatch = Regex.Match(js, @"(?:signatureTimestamp|sts)\s*:\s*(\d{5,})");
                        }
                    }
                }

                if (stsMatch.Success && int.TryParse(stsMatch.Groups[1].Value, out int value))
                {
                    signatureTimestamp = value;
                    Logger.Log($@"Resolved YouTube signature timestamp: {value}.", LoggingTarget.Network);
                    return value;
                }

                Logger.Log(@"Failed to resolve the YouTube signature timestamp.", LoggingTarget.Network);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to resolve the YouTube signature timestamp: {exception.GetDescription()}");
            }

            return null;
        }

        private static string generateCpn()
        {
            var builder = new StringBuilder(16);

            for (int i = 0; i < 16; i++)
                builder.Append(cpn_alphabet[Random.Shared.Next(cpn_alphabet.Length)]);

            return builder.ToString();
        }

        private static string setQueryParameters(string url, IReadOnlyDictionary<string, string> parameters)
        {
            int questionMark = url.IndexOf('?');
            string basePart = questionMark < 0 ? url : url[..questionMark];
            string query = questionMark < 0 ? string.Empty : url[(questionMark + 1)..];

            var replaced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pairs = new List<string>();

            foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = part.IndexOf('=');
                string key = equals < 0 ? part : part[..equals];

                if (parameters.TryGetValue(Uri.UnescapeDataString(key), out string? value))
                {
                    pairs.Add($@"{key}={Uri.EscapeDataString(value)}");
                    replaced.Add(key);
                }
                else
                {
                    pairs.Add(part);
                }
            }

            foreach (var parameter in parameters)
            {
                if (!replaced.Contains(parameter.Key))
                    pairs.Add($@"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}");
            }

            return pairs.Count == 0 ? basePart : $@"{basePart}?{string.Join(@"&", pairs)}";
        }

        private async Task<JsonDocument?> sendTvEditRequestAsync(object payload, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, tv_browse_edit_endpoint);

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, @"application/json");
                request.Headers.TryAddWithoutValidation(@"User-Agent", tv_browse_user_agent);
                request.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {googleOAuth2.GetAccessToken()}");
                request.Headers.TryAddWithoutValidation(@"X-Goog-AuthUser", @"0");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Bootstrap-Logged-In", @"true");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Name", @"7");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Version", tv_browse_client_version);
                request.Headers.TryAddWithoutValidation(@"X-Goog-Api-Key", innertube_api_key);
                request.Headers.TryAddWithoutValidation(@"Origin", @"https://www.youtube.com");

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    if (body.Length > 500)
                        body = body[..500];

                    Logger.Log($@"Watch history playlist request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}", LoggingTarget.Network);
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
                Logger.Error(exception, $@"Watch history playlist request failed: {exception.GetDescription()}");
                return null;
            }
        }

        private async Task<JsonDocument?> sendTvBrowseRequestAsync(object payload, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, tv_browse_endpoint);

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, @"application/json");
                request.Headers.TryAddWithoutValidation(@"User-Agent", tv_browse_user_agent);
                request.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {googleOAuth2.GetAccessToken()}");
                request.Headers.TryAddWithoutValidation(@"X-Goog-AuthUser", @"0");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Bootstrap-Logged-In", @"true");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Name", @"7");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Version", tv_browse_client_version);
                request.Headers.TryAddWithoutValidation(@"Origin", @"https://www.youtube.com");

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    if (body.Length > 500)
                        body = body[..500];

                    Logger.Log($@"Watch history request failed with status {(int)response.StatusCode} ({response.StatusCode}): {body}", LoggingTarget.Network);
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
                Logger.Error(exception, $@"Watch history request failed: {exception.GetDescription()}");
                return null;
            }
        }

        private static object createTvBrowseContext() => new
        {
            client = new
            {
                hl = @"en",
                gl = @"US",
                deviceMake = @"Samsung",
                deviceModel = @"SmartTV",
                userAgent = tv_browse_user_agent,
                clientName = @"TVHTML5",
                clientVersion = tv_browse_client_version,
                osName = @"Tizen",
                osVersion = @"5.0",
                platform = @"TV",
                clientFormFactor = @"UNKNOWN_FORM_FACTOR",
                screenPixelDensity = 1,
            },
        };

        private static object createTvPlayerContext(string? visitorData)
        {
            var client = new Dictionary<string, string>
            {
                [@"hl"] = @"en",
                [@"gl"] = @"US",
                [@"clientName"] = @"TVHTML5",
                [@"clientVersion"] = tv_player_client_version,
                [@"userAgent"] = tv_player_user_agent,
            };

            if (!string.IsNullOrEmpty(visitorData))
                client[@"visitorData"] = visitorData;

            return new { client };
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    public partial class YouTubeAPI
    {
        private const string web_client_version = @"2.20260101.00.00";
        private const string web_user_agent = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        /// <summary>
        /// YouTube returns chat in relatively small pages. This is an upper bound so a broken
        /// continuation chain cannot keep the player fetching forever.
        /// </summary>
        private const int live_chat_replay_max_pages = 200;

        private static readonly string[] continuation_data_names =
        {
            @"reloadContinuationData",
            @"replayContinuationData",
            @"invalidationContinuationData",
            @"timedContinuationData",
            @"liveChatReplayContinuationData",
        };

        /// <summary>
        /// Fetches the full live chat replay of a video, ordered by the video offset at which each message appeared.
        /// </summary>
        /// <remarks>
        /// Returns an empty list when the video has no live chat replay (which is the case for regular uploads
        /// and for streams whose chat replay has expired).
        /// </remarks>
        public async Task<IReadOnlyList<LiveChatReplayMessage>> GetLiveChatReplayAsync(string videoId, CancellationToken cancellationToken = default)
        {
            var messages = new List<LiveChatReplayMessage>();

            if (string.IsNullOrWhiteSpace(videoId))
                return messages;

            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                string? continuation = await resolveLiveChatReplayContinuationAsync(videoId, cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrEmpty(continuation))
                    return messages;

                for (int page = 0; page < live_chat_replay_max_pages; page++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using JsonDocument? document = await sendInnertubeRequestAsync(@"live_chat/get_live_chat_replay", new
                    {
                        context = createWebContext(),
                        continuation,
                        currentPlayerState = new
                        {
                            playerOffsetMs = @"0",
                        },
                    }, cancellationToken).ConfigureAwait(false);

                    if (document == null)
                        break;

                    if (!document.RootElement.TryGetProperty("continuationContents", out JsonElement continuationContents)
                        || continuationContents.ValueKind != JsonValueKind.Object)
                        break;

                    if (continuationContents.TryGetProperty("liveChatContinuation", out JsonElement liveChatContinuation)
                        && liveChatContinuation.TryGetProperty("actions", out JsonElement actions))
                    {
                        foreach (LiveChatReplayMessage message in LiveChatReplayParser.Parse(actions))
                        {
                            if (!string.IsNullOrEmpty(message.Id) && !seenIds.Add(message.Id))
                                continue;

                            messages.Add(message);
                        }
                    }

                    string? nextContinuation = liveChatContinuation.ValueKind == JsonValueKind.Object
                                                && liveChatContinuation.TryGetProperty("continuations", out JsonElement continuations)
                        ? findContinuation(continuations)
                        : null;

                    if (string.IsNullOrEmpty(nextContinuation) || nextContinuation == continuation)
                        break;

                    continuation = nextContinuation;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to fetch the live chat replay for video '{videoId}': {exception.GetDescription()}");
            }

            messages.Sort(static (a, b) => a.OffsetTimeMsec.CompareTo(b.OffsetTimeMsec));

            return messages;
        }

        /// <summary>
        /// Resolves the continuation token that starts the chat replay of a video.
        /// </summary>
        private async Task<string?> resolveLiveChatReplayContinuationAsync(string videoId, CancellationToken cancellationToken)
        {
            using JsonDocument? document = await sendInnertubeRequestAsync(@"next", new
            {
                videoId,
                context = createWebContext(),
            }, cancellationToken).ConfigureAwait(false);

            if (document != null)
            {
                JsonElement? liveChatRenderer = findProperty(document.RootElement, @"liveChatRenderer");

                if (liveChatRenderer != null)
                {
                    string? continuation = findContinuation(liveChatRenderer.Value);

                    if (!string.IsNullOrEmpty(continuation))
                        return continuation;
                }
            }

            return await resolveContinuationFromWatchPageAsync(videoId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Some chat replays are only advertised in the watch page's initial data, so this is used
        /// as a fallback when the innertube "next" response does not contain a continuation.
        /// </summary>
        private async Task<string?> resolveContinuationFromWatchPageAsync(string videoId, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $@"https://www.youtube.com/watch?v={videoId}&hl=en&gl=US");
                request.Headers.TryAddWithoutValidation(@"User-Agent", web_user_agent);
                request.Headers.TryAddWithoutValidation(@"Accept-Language", @"en-US,en;q=0.9");

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return null;

                string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                const string renderer_key = "\"liveChatRenderer\"";
                int rendererIndex = html.IndexOf(renderer_key, StringComparison.Ordinal);

                if (rendererIndex < 0)
                    return null;

                int objectStart = html.IndexOf('{', rendererIndex + renderer_key.Length);

                if (objectStart < 0)
                    return null;

                string? json = extractBalancedJson(html, objectStart);

                if (json == null)
                    return null;

                using JsonDocument document = JsonDocument.Parse(json);
                return findContinuation(document.RootElement);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to resolve the live chat replay continuation from the watch page: {exception.GetDescription()}");
                return null;
            }
        }

        /// <summary>
        /// Extracts a complete JSON object starting at <paramref name="start" /> from an HTML document.
        /// </summary>
        private static string? extractBalancedJson(string text, int start)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < text.Length; i++)
            {
                char character = text[i];

                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (character == '\\')
                        escaped = true;
                    else if (character == '"')
                        inString = false;

                    continue;
                }

                switch (character)
                {
                    case '"':
                        inString = true;
                        break;

                    case '{':
                        depth++;
                        break;

                    case '}':
                        depth--;

                        if (depth == 0)
                            return text.Substring(start, i - start + 1);

                        break;
                }
            }

            return null;
        }

        private async Task<JsonDocument?> sendInnertubeRequestAsync(string endpoint, object payload, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $@"https://www.youtube.com/youtubei/v1/{endpoint}?prettyPrint=false");

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, @"application/json");
                request.Headers.TryAddWithoutValidation(@"User-Agent", web_user_agent);
                request.Headers.TryAddWithoutValidation(@"Accept-Language", @"en-US,en;q=0.9");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Name", @"1");
                request.Headers.TryAddWithoutValidation(@"X-Youtube-Client-Version", web_client_version);

                if (googleOAuth2.SignedIn.Value)
                    request.Headers.TryAddWithoutValidation(@"Authorization", $@"Bearer {googleOAuth2.GetAccessToken()}");

                using HttpResponseMessage response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($@"Live chat replay request to '{endpoint}' failed with status code {(int)response.StatusCode}.", LoggingTarget.Network);
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
                Logger.Error(exception, $@"Live chat replay request to '{endpoint}' failed: {exception.GetDescription()}");
                return null;
            }
        }

        private static object createWebContext() => new
        {
            client = new
            {
                clientName = @"WEB",
                clientVersion = web_client_version,
                hl = @"en",
                gl = @"US",
            },
        };

        private static JsonElement? findProperty(JsonElement element, string propertyName)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (element.TryGetProperty(propertyName, out JsonElement value))
                        return value;

                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        JsonElement? result = findProperty(property.Value, propertyName);

                        if (result != null)
                            return result;
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        JsonElement? result = findProperty(child, propertyName);

                        if (result != null)
                            return result;
                    }

                    break;
            }

            return null;
        }

        private static string? findContinuation(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (string dataName in continuation_data_names)
                    {
                        if (element.TryGetProperty(dataName, out JsonElement data)
                            && data.ValueKind == JsonValueKind.Object
                            && data.TryGetProperty("continuation", out JsonElement continuation)
                            && continuation.ValueKind == JsonValueKind.String)
                        {
                            string? token = continuation.GetString();

                            if (!string.IsNullOrEmpty(token))
                                return token;
                        }
                    }

                    if (element.TryGetProperty("continuationCommand", out JsonElement command)
                        && command.ValueKind == JsonValueKind.Object
                        && command.TryGetProperty("token", out JsonElement commandToken)
                        && commandToken.ValueKind == JsonValueKind.String)
                    {
                        string? token = commandToken.GetString();

                        if (!string.IsNullOrEmpty(token))
                            return token;
                    }

                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        string? result = findContinuation(property.Value);

                        if (!string.IsNullOrEmpty(result))
                            return result;
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        string? result = findContinuation(child);

                        if (!string.IsNullOrEmpty(result))
                            return result;
                    }

                    break;
            }

            return null;
        }
    }
}

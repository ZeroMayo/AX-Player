// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// A single video that was watched on the signed-in account.
    /// </summary>
    public sealed class WatchHistoryItem
    {
        /// <summary>
        /// The ID of the watched video.
        /// </summary>
        public string VideoId { get; init; } = string.Empty;

        /// <summary>
        /// The (already localised by YouTube) title of the video.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// The (already localised by YouTube) name of the channel that uploaded the video.
        /// </summary>
        public string ChannelTitle { get; init; } = string.Empty;

        /// <summary>
        /// The URL of the video thumbnail, if any.
        /// </summary>
        public string? ThumbnailUrl { get; init; }

        /// <summary>
        /// A human readable view count, for example "1.2M views".
        /// </summary>
        public string? ViewCountText { get; init; }

        /// <summary>
        /// A human readable relative publish time, for example "2 days ago".
        /// </summary>
        public string? PublishedTimeText { get; init; }

        /// <summary>
        /// The length of the video, for example "12:34".
        /// </summary>
        public string? LengthText { get; init; }

        /// <summary>
        /// How much of the video was watched, in percent (0 - 100).
        /// </summary>
        public double ResumePercent { get; init; }

        /// <summary>
        /// The title of the history section this item belongs to, for example "Today".
        /// </summary>
        public string? SectionTitle { get; init; }

        public override string ToString() => $@"{VideoId} - {Title}";
    }

    /// <summary>
    /// One page of the watch history.
    /// </summary>
    public sealed class WatchHistoryPage
    {
        public static WatchHistoryPage Empty { get; } = new WatchHistoryPage(Array.Empty<WatchHistoryItem>(), null);

        public WatchHistoryPage(IReadOnlyList<WatchHistoryItem> items, string? continuation)
        {
            Items = items;
            Continuation = continuation;
        }

        /// <summary>
        /// The videos of this page, in the order YouTube returned them.
        /// </summary>
        public IReadOnlyList<WatchHistoryItem> Items { get; }

        /// <summary>
        /// The token that can be used to fetch the next page, if there is one.
        /// </summary>
        public string? Continuation { get; }
    }

    /// <summary>
    /// Parses the response of the Innertube "browse" endpoint for the watch history (<c>FEhistory</c>).
    /// </summary>
    public static class WatchHistoryParser
    {
        public static WatchHistoryPage Parse(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return Parse(document.RootElement);
        }

        public static WatchHistoryPage Parse(JsonDocument document) => Parse(document.RootElement);

        public static WatchHistoryPage Parse(JsonElement root)
        {
            var items = new List<WatchHistoryItem>();
            var seenVideoIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (JsonElement section in enumerateItemSections(root))
            {
                if (!section.TryGetProperty(@"contents", out JsonElement contents))
                    continue;

                string? sectionTitle = getSectionTitle(section);
                collectItems(contents, sectionTitle, items, seenVideoIds);
            }

            // Some (mostly older) responses don't wrap the history in item sections.
            if (items.Count == 0)
                collectItems(root, null, items, seenVideoIds);

            return new WatchHistoryPage(items, findContinuation(root));
        }

        private static IEnumerable<JsonElement> enumerateItemSections(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    // Don't descend into a section after finding it; its contents are handled
                    // separately so that section titles can be attached to the items.
                    if (element.TryGetProperty(@"itemSectionRenderer", out JsonElement section)
                        && section.ValueKind == JsonValueKind.Object)
                    {
                        yield return section;
                        yield break;
                    }

                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        foreach (JsonElement child in enumerateItemSections(property.Value))
                            yield return child;
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        foreach (JsonElement nested in enumerateItemSections(child))
                            yield return nested;
                    }

                    break;
            }
        }

        private static void collectItems(JsonElement element, string? sectionTitle, List<WatchHistoryItem> items, HashSet<string> seenVideoIds)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (element.TryGetProperty(@"videoRenderer", out JsonElement videoRenderer)
                        && videoRenderer.ValueKind == JsonValueKind.Object)
                    {
                        addItem(parseVideoRenderer(videoRenderer, sectionTitle), items, seenVideoIds);
                        return;
                    }

                    foreach (string rendererName in new[] { @"gridVideoRenderer", @"compactVideoRenderer", @"playlistVideoRenderer" })
                    {
                        if (element.TryGetProperty(rendererName, out JsonElement alternateVideoRenderer)
                            && alternateVideoRenderer.ValueKind == JsonValueKind.Object)
                        {
                            addItem(parseVideoRenderer(alternateVideoRenderer, sectionTitle), items, seenVideoIds);
                            return;
                        }
                    }

                    if (element.TryGetProperty(@"tileRenderer", out JsonElement tileRenderer)
                        && tileRenderer.ValueKind == JsonValueKind.Object)
                    {
                        addItem(parseTileRenderer(tileRenderer, sectionTitle), items, seenVideoIds);
                        return;
                    }

                    if (element.TryGetProperty(@"lockupViewModel", out JsonElement lockup)
                        && lockup.ValueKind == JsonValueKind.Object)
                    {
                        addItem(parseLockupViewModel(lockup, sectionTitle), items, seenVideoIds);
                        return;
                    }

                    if (element.TryGetProperty(@"richItemRenderer", out JsonElement richItem))
                    {
                        collectItems(richItem, sectionTitle, items, seenVideoIds);
                        return;
                    }

                    foreach (JsonProperty property in element.EnumerateObject())
                        collectItems(property.Value, sectionTitle, items, seenVideoIds);

                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                        collectItems(child, sectionTitle, items, seenVideoIds);

                    break;
            }
        }

        private static void addItem(WatchHistoryItem? item, List<WatchHistoryItem> items, HashSet<string> seenVideoIds)
        {
            if (item == null || string.IsNullOrEmpty(item.VideoId))
                return;

            if (!seenVideoIds.Add(item.VideoId))
                return;

            items.Add(item);
        }

        private static WatchHistoryItem? parseVideoRenderer(JsonElement video, string? sectionTitle)
        {
            string? videoId = getString(video, @"videoId");

            if (string.IsNullOrEmpty(videoId))
                return null;

            string title = getText(video, @"title");
            string channelTitle = getText(video, @"ownerText");

            if (string.IsNullOrEmpty(channelTitle))
                channelTitle = getText(video, @"longBylineText");

            if (string.IsNullOrEmpty(channelTitle))
                channelTitle = getText(video, @"shortBylineText");

            string? thumbnailUrl = video.TryGetProperty(@"thumbnail", out JsonElement thumbnail)
                ? findImageUrl(thumbnail)
                : null;

            string? lengthText = getTextOrNull(video, @"lengthText");
            double resumePercent = parseResumePercent(video);

            return new WatchHistoryItem
            {
                VideoId = videoId,
                Title = title,
                ChannelTitle = channelTitle,
                ThumbnailUrl = thumbnailUrl,
                ViewCountText = getTextOrNull(video, @"viewCountText"),
                PublishedTimeText = getTextOrNull(video, @"publishedTimeText"),
                LengthText = lengthText,
                ResumePercent = resumePercent,
                SectionTitle = sectionTitle,
            };
        }

        private static WatchHistoryItem? parseLockupViewModel(JsonElement lockup, string? sectionTitle)
        {
            string? videoId = getString(lockup, @"contentId");

            if (string.IsNullOrEmpty(videoId))
                return null;

            string? contentType = getString(lockup, @"contentType");

            // Playlists, channels and mixes can also appear in the history. Only videos are supported.
            if (!string.IsNullOrEmpty(contentType)
                && (contentType.Contains(@"PLAYLIST", StringComparison.OrdinalIgnoreCase)
                    || contentType.Contains(@"CHANNEL", StringComparison.OrdinalIgnoreCase)
                    || contentType.Contains(@"MIX", StringComparison.OrdinalIgnoreCase)))
                return null;

            string title = string.Empty;
            var metadataParts = new List<string>();

            JsonElement? metadata = findProperty(lockup, @"lockupMetadataViewModel");

            if (metadata != null)
            {
                if (metadata.Value.TryGetProperty(@"title", out JsonElement titleElement))
                    title = extractText(titleElement);

                JsonElement? contentMetadata = findProperty(metadata.Value, @"contentMetadataViewModel");

                if (contentMetadata != null
                    && contentMetadata.Value.TryGetProperty(@"metadataRows", out JsonElement rows)
                    && rows.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement row in rows.EnumerateArray())
                    {
                        if (!row.TryGetProperty(@"metadataParts", out JsonElement parts) || parts.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (JsonElement part in parts.EnumerateArray())
                        {
                            if (!part.TryGetProperty(@"text", out JsonElement text))
                                continue;

                            string value = extractText(text);

                            if (!string.IsNullOrWhiteSpace(value))
                                metadataParts.Add(value);
                        }
                    }
                }
            }

            string? thumbnailUrl = lockup.TryGetProperty(@"contentImage", out JsonElement contentImage)
                ? findImageUrl(contentImage)
                : null;

            string? lengthText = null;
            JsonElement? badge = findProperty(lockup, @"thumbnailBadgeViewModel");

            if (badge != null)
                lengthText = getTextOrNull(badge.Value, @"text");

            classifyMetadataParts(metadataParts, out string channelTitle, out string? viewCountText, out string? publishedTimeText);

            return new WatchHistoryItem
            {
                VideoId = videoId,
                Title = title,
                ChannelTitle = channelTitle,
                ThumbnailUrl = thumbnailUrl,
                ViewCountText = viewCountText,
                PublishedTimeText = publishedTimeText,
                LengthText = lengthText,
                ResumePercent = parseResumePercent(lockup),
                SectionTitle = sectionTitle,
            };
        }

        /// <summary>
        /// Parses a TV (TVHTML5) history tile. The TV client is the only Innertube client that
        /// accepts OAuth access tokens, and it returns tiles instead of regular video renderers.
        /// </summary>
        private static WatchHistoryItem? parseTileRenderer(JsonElement tile, string? sectionTitle)
        {
            string? videoId = null;

            if (findProperty(tile, @"watchEndpoint") is JsonElement watchEndpoint)
                videoId = getString(watchEndpoint, @"videoId");

            if (string.IsNullOrEmpty(videoId))
                return null;

            string title = string.Empty;
            var metadataParts = new List<string>();

            JsonElement? tileMetadata = findProperty(tile, @"tileMetadataRenderer");

            if (tileMetadata != null)
            {
                if (tileMetadata.Value.TryGetProperty(@"title", out JsonElement titleElement))
                    title = extractText(titleElement);

                if (tileMetadata.Value.TryGetProperty(@"lines", out JsonElement lines) && lines.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement line in lines.EnumerateArray())
                    {
                        JsonElement? lineRenderer = findProperty(line, @"lineRenderer");

                        if (lineRenderer == null)
                            continue;

                        if (!lineRenderer.Value.TryGetProperty(@"items", out JsonElement lineItems) || lineItems.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (JsonElement lineItem in lineItems.EnumerateArray())
                        {
                            JsonElement? lineItemRenderer = findProperty(lineItem, @"lineItemRenderer");

                            if (lineItemRenderer == null)
                                continue;

                            string value = getText(lineItemRenderer.Value, @"text");

                            if (string.IsNullOrWhiteSpace(value))
                            {
                                JsonElement? badge = findProperty(lineItemRenderer.Value, @"metadataBadgeRenderer");

                                if (badge != null)
                                    value = getText(badge.Value, @"label");
                            }

                            if (!string.IsNullOrWhiteSpace(value))
                                metadataParts.Add(value);
                        }
                    }
                }
            }

            string? thumbnailUrl = null;
            string? lengthText = null;

            JsonElement? header = findProperty(tile, @"tileHeaderRenderer");

            if (header != null)
            {
                thumbnailUrl = findImageUrl(header.Value);

                JsonElement? timeStatus = findProperty(header.Value, @"thumbnailOverlayTimeStatusRenderer");

                if (timeStatus != null)
                    lengthText = getTextOrNull(timeStatus.Value, @"text");
            }

            classifyMetadataParts(metadataParts, out string channelTitle, out string? viewCountText, out string? publishedTimeText);

            return new WatchHistoryItem
            {
                VideoId = videoId,
                Title = title,
                ChannelTitle = channelTitle,
                ThumbnailUrl = thumbnailUrl,
                ViewCountText = viewCountText,
                PublishedTimeText = publishedTimeText,
                LengthText = lengthText,
                ResumePercent = parseResumePercent(tile),
                SectionTitle = sectionTitle,
            };
        }

        private static void classifyMetadataParts(IEnumerable<string> metadataParts, out string channelTitle, out string? viewCountText, out string? publishedTimeText)
        {
            channelTitle = string.Empty;
            viewCountText = null;
            publishedTimeText = null;

            foreach (string part in metadataParts)
            {
                if (part.Contains(@"view", StringComparison.OrdinalIgnoreCase) && viewCountText == null)
                {
                    viewCountText = part;
                    continue;
                }

                if (isRelativeTime(part) && publishedTimeText == null)
                {
                    publishedTimeText = part;
                    continue;
                }

                if (string.IsNullOrEmpty(channelTitle))
                {
                    channelTitle = part;
                    continue;
                }

                if (publishedTimeText == null)
                    publishedTimeText = part;
            }
        }

        private static bool isRelativeTime(string value)
            => value.Contains(@"ago", StringComparison.OrdinalIgnoreCase)
               || value.Contains(@"yesterday", StringComparison.OrdinalIgnoreCase);

        private static double parseResumePercent(JsonElement element)
        {
            JsonElement? resumeRenderer = findProperty(element, @"thumbnailOverlayResumePlaybackRenderer");

            if (resumeRenderer == null)
                return 0;

            if (!resumeRenderer.Value.TryGetProperty(@"percentDurationWatched", out JsonElement percent))
                return 0;

            switch (percent.ValueKind)
            {
                case JsonValueKind.Number:
                    return Math.Clamp(percent.GetDouble(), 0, 100);

                case JsonValueKind.String:
                    return double.TryParse(percent.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                        ? Math.Clamp(parsed, 0, 100)
                        : 0;

                default:
                    return 0;
            }
        }

        private static string? getSectionTitle(JsonElement section)
        {
            JsonElement? titleRenderer = findProperty(section, @"itemSectionTitleRenderer");

            if (titleRenderer != null)
            {
                string title = getText(titleRenderer.Value, @"title");

                if (!string.IsNullOrWhiteSpace(title))
                    return title;
            }

            JsonElement? header = findProperty(section, @"header");

            if (header != null)
            {
                string headerTitle = getText(header.Value, @"title");

                if (!string.IsNullOrWhiteSpace(headerTitle))
                    return headerTitle;
            }

            return null;
        }

        private static string? findContinuation(JsonElement root)
        {
            JsonElement? item = findProperty(root, @"continuationItemRenderer");

            if (item != null)
            {
                JsonElement? endpoint = findProperty(item.Value, @"continuationEndpoint");

                if (endpoint != null)
                {
                    JsonElement? command = findProperty(endpoint.Value, @"continuationCommand");

                    if (command != null)
                    {
                        string? token = getString(command.Value, @"token");

                        if (!string.IsNullOrEmpty(token))
                            return token;
                    }
                }
            }

            JsonElement? nextContinuation = findProperty(root, @"nextContinuationData");

            if (nextContinuation != null)
            {
                string? token = getString(nextContinuation.Value, @"continuation");

                if (!string.IsNullOrEmpty(token))
                    return token;
            }

            JsonElement? commandFallback = findProperty(root, @"continuationCommand");

            if (commandFallback != null)
            {
                string? token = getString(commandFallback.Value, @"token");

                if (!string.IsNullOrEmpty(token))
                    return token;
            }

            return null;
        }

        private static string getText(JsonElement element, string propertyName)
            => element.TryGetProperty(propertyName, out JsonElement value) ? extractText(value) : string.Empty;

        private static string? getTextOrNull(JsonElement element, string propertyName)
        {
            string value = getText(element, propertyName);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static string? getString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };
        }

        private static string extractText(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString() ?? string.Empty;

                case JsonValueKind.Array:
                {
                    var builder = new System.Text.StringBuilder();

                    foreach (JsonElement child in element.EnumerateArray())
                        builder.Append(extractText(child));

                    return builder.ToString();
                }

                case JsonValueKind.Object:
                    if (element.TryGetProperty(@"simpleText", out JsonElement simpleText) && simpleText.ValueKind == JsonValueKind.String)
                        return simpleText.GetString() ?? string.Empty;

                    if (element.TryGetProperty(@"content", out JsonElement content) && content.ValueKind == JsonValueKind.String)
                        return content.GetString() ?? string.Empty;

                    if (element.TryGetProperty(@"runs", out JsonElement runs) && runs.ValueKind == JsonValueKind.Array)
                        return extractText(runs);

                    if (element.TryGetProperty(@"text", out JsonElement text) && text.ValueKind != JsonValueKind.Object && text.ValueKind != JsonValueKind.Array)
                        return extractText(text);

                    return string.Empty;

                default:
                    return string.Empty;
            }
        }

        private static string? findImageUrl(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (string arrayName in new[] { @"thumbnails", @"sources" })
                    {
                        if (!element.TryGetProperty(arrayName, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                            continue;

                        string? url = null;

                        foreach (JsonElement entry in array.EnumerateArray())
                        {
                            string? candidate = getString(entry, @"url");

                            if (!string.IsNullOrEmpty(candidate))
                                url = candidate;
                        }

                        if (!string.IsNullOrEmpty(url))
                            return url;
                    }

                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        string? result = findImageUrl(property.Value);

                        if (!string.IsNullOrEmpty(result))
                            return result;
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        string? result = findImageUrl(child);

                        if (!string.IsNullOrEmpty(result))
                            return result;
                    }

                    break;
            }

            return null;
        }

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
    }
}

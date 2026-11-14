// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// The kind of message that occurred while a live chat replay was taking place.
    /// </summary>
    public enum LiveChatReplayMessageKind
    {
        /// <summary>A regular viewer message.</summary>
        Text,

        /// <summary>A system message, such as "Live chat replay is on.".</summary>
        ViewerEngagement,

        /// <summary>A message that came with a super chat / donation.</summary>
        Paid,

        /// <summary>A membership (sponsorships) announcement.</summary>
        Membership,

        /// <summary>A message that came with a paid sticker.</summary>
        Sticker,

        /// <summary>A message whose renderer is not understood by this parser.</summary>
        Unknown,
    }

    /// <summary>
    /// A badge that is displayed next to the author of a <see cref="LiveChatReplayMessage"/>.
    /// </summary>
    public sealed class LiveChatReplayBadge
    {
        /// <summary>
        /// The (already localised by YouTube) label of the badge, for example "Owner" or "Member (1 year)".
        /// </summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>
        /// The custom image of the badge, if any.
        /// </summary>
        public string? IconUrl { get; init; }
    }

    /// <summary>
    /// The kind of a <see cref="LiveChatReplayTextSegment"/>.
    /// </summary>
    public enum LiveChatReplayTextSegmentKind
    {
        /// <summary>Plain text.</summary>
        Text,

        /// <summary>An emoji, which may either be a Unicode character or a picture emoji (for example ":clapping_hands:").</summary>
        Emoji,
    }

    /// <summary>
    /// A piece of the message text of a <see cref="LiveChatReplayMessage"/>.
    /// Message text is split into runs so that picture emoji can be rendered as images.
    /// </summary>
    public sealed class LiveChatReplayTextSegment
    {
        public LiveChatReplayTextSegmentKind Kind { get; init; } = LiveChatReplayTextSegmentKind.Text;

        /// <summary>
        /// The text of the run. For emoji this is the shortcut representation, for example ":clapping_hands:".
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// The URL of the picture emoji, if this run is a picture emoji.
        /// </summary>
        public string? ImageUrl { get; init; }

        /// <summary>
        /// The accessibility label of the emoji, if any.
        /// </summary>
        public string? Label { get; init; }

        /// <summary>
        /// Whether this emoji is a channel-specific (custom) emoji rather than a standard one.
        /// </summary>
        public bool IsCustomEmoji { get; init; }

        /// <summary>
        /// Whether this segment should be rendered as an image rather than as text.
        /// </summary>
        public bool HasImage => Kind == LiveChatReplayTextSegmentKind.Emoji && !string.IsNullOrEmpty(ImageUrl);

        public override string ToString() => Text;
    }

    /// <summary>
    /// A single message of a YouTube live chat replay, positioned on the video timeline.
    /// </summary>
    public sealed class LiveChatReplayMessage
    {
        /// <summary>
        /// The renderer-provided identifier of this message. May be empty for some system messages.
        /// </summary>
        public string Id { get; init; } = string.Empty;

        public LiveChatReplayMessageKind Kind { get; init; } = LiveChatReplayMessageKind.Unknown;

        /// <summary>
        /// The offset from the start of the video at which this message was sent, in milliseconds.
        /// </summary>
        public double OffsetTimeMsec { get; init; }

        public string AuthorName { get; init; } = string.Empty;

        public string? AuthorChannelId { get; init; }

        public string? AuthorPhotoUrl { get; init; }

        /// <summary>
        /// The flattened message text. Emoji are expanded to their shortcut representation.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// The message text split into runs. Picture emoji (which carry an <see cref="LiveChatReplayTextSegment.ImageUrl"/>
        /// when the payload provides one) can be rendered as images from this list.
        /// The list is empty when the message has no runs, in which case <see cref="Text"/> is the whole message.
        /// </summary>
        public IReadOnlyList<LiveChatReplayTextSegment> Segments { get; init; } = Array.Empty<LiveChatReplayTextSegment>();

        /// <summary>
        /// The paid amount for <see cref="LiveChatReplayMessageKind.Paid"/> and <see cref="LiveChatReplayMessageKind.Sticker"/> messages.
        /// </summary>
        public string? AmountText { get; init; }

        public IReadOnlyList<LiveChatReplayBadge> Badges { get; init; } = Array.Empty<LiveChatReplayBadge>();

        public override string ToString() => $"[{OffsetTimeMsec:0}ms] {AuthorName}: {Text}";
    }

    /// <summary>
    /// Parses the action payloads returned by the live chat replay endpoints
    /// (<c>replayChatItemAction</c> / <c>addChatItemAction</c>) into <see cref="LiveChatReplayMessage"/>s.
    /// </summary>
    public static class LiveChatReplayParser
    {
        /// <summary>
        /// Parses a raw live chat replay JSON document.
        /// </summary>
        public static IReadOnlyList<LiveChatReplayMessage> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Array.Empty<LiveChatReplayMessage>();

            using JsonDocument document = JsonDocument.Parse(json);
            return Parse(document.RootElement);
        }

        /// <summary>
        /// Parses an already materialised live chat replay JSON element.
        /// The element may be a full response, a list of actions, or a single action.
        /// </summary>
        public static IReadOnlyList<LiveChatReplayMessage> Parse(JsonElement root)
        {
            var result = new List<LiveChatReplayMessage>();
            parseElement(root, 0, result);
            return result;
        }

        private static void parseElement(JsonElement element, double inheritedOffset, List<LiveChatReplayMessage> result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                        parseElement(child, inheritedOffset, result);

                    break;

                case JsonValueKind.Object:
                    if (element.TryGetProperty("replayChatItemAction", out JsonElement replayAction) && replayAction.ValueKind == JsonValueKind.Object)
                    {
                        double offset = getDouble(replayAction, "videoOffsetTimeMsec", inheritedOffset);

                        if (replayAction.TryGetProperty("actions", out JsonElement replayActions))
                            parseElement(replayActions, offset, result);
                    }
                    else if (element.TryGetProperty("addChatItemAction", out JsonElement addChatItemAction) && addChatItemAction.ValueKind == JsonValueKind.Object)
                    {
                        if (addChatItemAction.TryGetProperty("item", out JsonElement item))
                            addMessage(item, inheritedOffset, result);
                    }
                    else if (element.TryGetProperty("continuationContents", out JsonElement continuationContents) && continuationContents.ValueKind == JsonValueKind.Object)
                    {
                        if (continuationContents.TryGetProperty("liveChatContinuation", out JsonElement liveChatContinuation)
                            && liveChatContinuation.TryGetProperty("actions", out JsonElement actions))
                            parseElement(actions, inheritedOffset, result);
                    }
                    else if (element.TryGetProperty("actions", out JsonElement actions))
                    {
                        parseElement(actions, inheritedOffset, result);
                    }

                    break;
            }
        }

        private static void addMessage(JsonElement item, double offset, List<LiveChatReplayMessage> result)
        {
            if (item.ValueKind != JsonValueKind.Object)
                return;

            if (item.TryGetProperty("liveChatTextMessageRenderer", out JsonElement textRenderer))
                add(createMessage(textRenderer, LiveChatReplayMessageKind.Text, offset));
            else if (item.TryGetProperty("liveChatViewerEngagementMessageRenderer", out JsonElement engagementRenderer))
                add(createMessage(engagementRenderer, LiveChatReplayMessageKind.ViewerEngagement, offset));
            else if (item.TryGetProperty("liveChatPaidMessageRenderer", out JsonElement paidRenderer))
                add(createMessage(paidRenderer, LiveChatReplayMessageKind.Paid, offset));
            else if (item.TryGetProperty("liveChatMembershipItemRenderer", out JsonElement membershipRenderer))
                add(createMessage(membershipRenderer, LiveChatReplayMessageKind.Membership, offset));
            else if (item.TryGetProperty("liveChatPaidStickerRenderer", out JsonElement stickerRenderer))
                add(createMessage(stickerRenderer, LiveChatReplayMessageKind.Sticker, offset));
            else if (item.TryGetProperty("liveChatSponsorshipsGiftPurchaseAnnouncementRenderer", out JsonElement giftPurchaseRenderer))
                add(createMessage(giftPurchaseRenderer, LiveChatReplayMessageKind.Membership, offset));
            else if (item.TryGetProperty("liveChatSponsorshipsGiftRedemptionAnnouncementRenderer", out JsonElement giftRedemptionRenderer))
                add(createMessage(giftRedemptionRenderer, LiveChatReplayMessageKind.Membership, offset));
            else if (item.TryGetProperty("liveChatModeChangeMessageRenderer", out JsonElement modeChangeRenderer))
                add(createMessage(modeChangeRenderer, LiveChatReplayMessageKind.ViewerEngagement, offset));

            void add(LiveChatReplayMessage? message)
            {
                if (message != null)
                    result.Add(message);
            }
        }

        private static LiveChatReplayMessage? createMessage(JsonElement renderer, LiveChatReplayMessageKind kind, double offset)
        {
            if (renderer.ValueKind != JsonValueKind.Object)
                return null;

            var segments = new List<LiveChatReplayTextSegment>();
            string text = extractText(renderer, segments);

            if (string.IsNullOrEmpty(text))
            {
                if (renderer.TryGetProperty("headerSubtext", out JsonElement headerSubtext))
                    text = extractRunsText(headerSubtext);
                else if (renderer.TryGetProperty("headerPrimaryText", out JsonElement headerPrimaryText))
                    text = extractRunsText(headerPrimaryText);
                else if (renderer.TryGetProperty("header", out JsonElement header)
                         && header.TryGetProperty("liveChatSponsorshipsHeaderRenderer", out JsonElement sponsorshipsHeader)
                         && sponsorshipsHeader.TryGetProperty("primaryText", out JsonElement primaryText))
                    text = extractRunsText(primaryText);
            }

            string authorName = string.Empty;
            if (renderer.TryGetProperty("authorName", out JsonElement authorNameElement))
                authorName = extractRunsText(authorNameElement);

            string? amountText = null;
            if (renderer.TryGetProperty("purchaseAmountText", out JsonElement purchaseAmountText))
                amountText = extractRunsText(purchaseAmountText);

            string id = getString(renderer, "id") ?? string.Empty;

            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(authorName) && string.IsNullOrEmpty(text))
                return null;

            string? photoUrl = null;
            if (renderer.TryGetProperty("authorPhoto", out JsonElement authorPhoto))
                photoUrl = getThumbnailUrl(authorPhoto);

            return new LiveChatReplayMessage
            {
                Id = id,
                Kind = kind,
                // A replay action carries its own video offset. Some renderer-only payloads
                // (for example the live chat iframe bootstrap data) do not, and their
                // timestampUsec is a wall-clock time, so they are placed at the start instead.
                OffsetTimeMsec = offset,
                AuthorName = authorName,
                AuthorChannelId = getString(renderer, "authorExternalChannelId"),
                AuthorPhotoUrl = photoUrl,
                Text = text,
                Segments = segments,
                AmountText = string.IsNullOrEmpty(amountText) ? null : amountText,
                Badges = getBadges(renderer),
            };
        }

        private static string extractText(JsonElement renderer, List<LiveChatReplayTextSegment>? segments)
        {
            if (renderer.TryGetProperty("message", out JsonElement message))
                return extractRunsText(message, segments);

            return string.Empty;
        }

        private static string extractRunsText(JsonElement container) => extractRunsText(container, null);

        private static string extractRunsText(JsonElement container, List<LiveChatReplayTextSegment>? segments)
        {
            switch (container.ValueKind)
            {
                case JsonValueKind.String:
                    return appendText(container.GetString() ?? string.Empty, segments);

                case JsonValueKind.Object:
                    if (container.TryGetProperty("simpleText", out JsonElement simpleText) && simpleText.ValueKind == JsonValueKind.String)
                        return appendText(simpleText.GetString() ?? string.Empty, segments);

                    if (!container.TryGetProperty("runs", out JsonElement runs) || runs.ValueKind != JsonValueKind.Array)
                        return string.Empty;

                    var builder = new StringBuilder();

                    foreach (JsonElement run in runs.EnumerateArray())
                    {
                        if (run.ValueKind != JsonValueKind.Object)
                            continue;

                        if (run.TryGetProperty("text", out JsonElement text) && text.ValueKind == JsonValueKind.String)
                        {
                            builder.Append(appendText(text.GetString() ?? string.Empty, segments));
                            continue;
                        }

                        if (!run.TryGetProperty("emoji", out JsonElement emoji))
                            continue;

                        LiveChatReplayTextSegment segment = createEmojiSegment(emoji);

                        builder.Append(segment.Text);
                        segments?.Add(segment);
                    }

                    return builder.ToString();

                default:
                    return string.Empty;
            }
        }

        private static string appendText(string text, List<LiveChatReplayTextSegment>? segments)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (segments != null)
            {
                // Merge adjacent text runs so that messages do not end up with one segment per run.
                if (segments.Count > 0 && segments[^1].Kind == LiveChatReplayTextSegmentKind.Text)
                {
                    LiveChatReplayTextSegment previous = segments[^1];

                    segments[^1] = new LiveChatReplayTextSegment
                    {
                        Kind = LiveChatReplayTextSegmentKind.Text,
                        Text = previous.Text + text,
                    };
                }
                else
                {
                    segments.Add(new LiveChatReplayTextSegment
                    {
                        Kind = LiveChatReplayTextSegmentKind.Text,
                        Text = text,
                    });
                }
            }

            return text;
        }

        private static LiveChatReplayTextSegment createEmojiSegment(JsonElement emoji)
        {
            string shortcut = string.Empty;

            if (emoji.TryGetProperty("shortcuts", out JsonElement shortcuts)
                && shortcuts.ValueKind == JsonValueKind.Array
                && shortcuts.GetArrayLength() > 0
                && shortcuts[0].ValueKind == JsonValueKind.String)
            {
                shortcut = shortcuts[0].GetString() ?? string.Empty;
            }
            else if (emoji.TryGetProperty("emojiId", out JsonElement emojiId) && emojiId.ValueKind == JsonValueKind.String)
            {
                shortcut = emojiId.GetString() ?? string.Empty;
            }

            string? imageUrl = null;
            string? label = null;

            if (emoji.TryGetProperty("image", out JsonElement image) && image.ValueKind == JsonValueKind.Object)
            {
                imageUrl = getThumbnailUrl(image);

                if (image.TryGetProperty("accessibility", out JsonElement accessibility)
                    && accessibility.TryGetProperty("accessibilityData", out JsonElement accessibilityData)
                    && accessibilityData.TryGetProperty("label", out JsonElement accessibilityLabel)
                    && accessibilityLabel.ValueKind == JsonValueKind.String)
                    label = accessibilityLabel.GetString();
            }

            bool isCustomEmoji = emoji.TryGetProperty("isCustomEmoji", out JsonElement customEmoji) && customEmoji.ValueKind == JsonValueKind.True;

            return new LiveChatReplayTextSegment
            {
                Kind = LiveChatReplayTextSegmentKind.Emoji,
                Text = shortcut,
                ImageUrl = imageUrl,
                Label = label,
                IsCustomEmoji = isCustomEmoji,
            };
        }

        private static IReadOnlyList<LiveChatReplayBadge> getBadges(JsonElement renderer)
        {
            if (!renderer.TryGetProperty("authorBadges", out JsonElement authorBadges) || authorBadges.ValueKind != JsonValueKind.Array)
                return Array.Empty<LiveChatReplayBadge>();

            var badges = new List<LiveChatReplayBadge>();

            foreach (JsonElement badge in authorBadges.EnumerateArray())
            {
                if (!badge.TryGetProperty("liveChatAuthorBadgeRenderer", out JsonElement badgeRenderer) || badgeRenderer.ValueKind != JsonValueKind.Object)
                    continue;

                string label = getBadgeLabel(badgeRenderer);

                if (string.IsNullOrEmpty(label))
                    continue;

                string? iconUrl = null;
                if (badgeRenderer.TryGetProperty("customThumbnail", out JsonElement customThumbnail))
                    iconUrl = getThumbnailUrl(customThumbnail);

                badges.Add(new LiveChatReplayBadge
                {
                    Label = label,
                    IconUrl = iconUrl,
                });
            }

            return badges.Count == 0 ? Array.Empty<LiveChatReplayBadge>() : badges;
        }

        private static string getBadgeLabel(JsonElement badgeRenderer)
        {
            if (badgeRenderer.TryGetProperty("tooltip", out JsonElement tooltip) && tooltip.ValueKind == JsonValueKind.String)
                return tooltip.GetString() ?? string.Empty;

            if (badgeRenderer.TryGetProperty("label", out JsonElement label) && label.ValueKind == JsonValueKind.String)
                return label.GetString() ?? string.Empty;

            if (badgeRenderer.TryGetProperty("accessibility", out JsonElement accessibility)
                && accessibility.TryGetProperty("accessibilityData", out JsonElement accessibilityData)
                && accessibilityData.TryGetProperty("label", out JsonElement accessibilityLabel)
                && accessibilityLabel.ValueKind == JsonValueKind.String)
                return accessibilityLabel.GetString() ?? string.Empty;

            return string.Empty;
        }

        private static string? getThumbnailUrl(JsonElement container)
        {
            if (!container.TryGetProperty("thumbnails", out JsonElement thumbnails) || thumbnails.ValueKind != JsonValueKind.Array)
                return null;

            string? result = null;

            foreach (JsonElement thumbnail in thumbnails.EnumerateArray())
            {
                if (thumbnail.TryGetProperty("url", out JsonElement url) && url.ValueKind == JsonValueKind.String)
                    result = url.GetString();
            }

            return result;
        }

        private static string? getString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
                return null;

            return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        }

        private static double getDouble(JsonElement element, string propertyName, double fallback)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property))
                return fallback;

            switch (property.ValueKind)
            {
                case JsonValueKind.Number when property.TryGetDouble(out double number):
                    return number;

                case JsonValueKind.String when double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed):
                    return parsed;

                default:
                    return fallback;
            }
        }
    }
}

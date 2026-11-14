// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using AXPlayer.App.Online;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Online
{
    [TestFixture]
    public class TestLiveChatReplayParser
    {
        private const string attached_example = """
        {"clickTrackingParams":"CAEQl98BIhMIwr-Q3NiEmAMVpF0PAh1qUBVfygEES4EL_A==","replayChatItemAction":{"actions":[{"clickTrackingParams":"CAEQl98BIhMIwr-Q3NiEmAMVpF0PAh1qUBVfygEES4EL_A==","addChatItemAction":{"item":{"liveChatViewerEngagementMessageRenderer":{"id":"CiMKIVJFUExBWV9WRU0yMDI2LzExLzEzLTAyOjAzOjUxLjA3Ng%3D%3D","timestampUsec":"1794564231076533","icon":{"iconType":"YOUTUBE_ROUND"},"message":{"runs":[{"text":"Live chat replay is on. Messages that appeared during the Premiere will show up here."}]},"trackingParams":"CAEQl98BIhMIwr-Q3NiEmAMVpF0PAh1qUBVf"}}}}],"videoOffsetTimeMsec":"0"}}
        """;

        private const string rich_example = """
        [
          {
            "replayChatItemAction": {
              "actions": [
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatTextMessageRenderer": {
                        "id": "text-1",
                        "authorName": { "simpleText": "Retsi" },
                        "authorExternalChannelId": "UC0000000000000000000000",
                        "authorPhoto": { "thumbnails": [ { "url": "small.png" }, { "url": "large.png" } ] },
                        "message": {
                          "runs": [
                            { "text": "Hello " },
                            { "emoji": { "emojiId": "wave-emoji", "shortcuts": [":wave:"], "isCustomEmoji": true, "image": { "thumbnails": [ { "url": "emoji-small.png" }, { "url": "emoji-large.png" } ], "accessibility": { "accessibilityData": { "label": "waving hand" } } } } },
                            { "text": " chat!" }
                          ]
                        },
                        "authorBadges": [
                          { "liveChatAuthorBadgeRenderer": { "tooltip": "Moderator" } },
                          {
                            "liveChatAuthorBadgeRenderer": {
                              "accessibility": { "accessibilityData": { "label": "Member (1 year)" } },
                              "customThumbnail": { "thumbnails": [ { "url": "badge.png" } ] }
                            }
                          }
                        ]
                      }
                    }
                  }
                }
              ],
              "videoOffsetTimeMsec": "5000"
            }
          },
          {
            "replayChatItemAction": {
              "actions": [
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatPaidMessageRenderer": {
                        "id": "paid-1",
                        "authorName": { "simpleText": "Boombox" },
                        "message": { "runs": [ { "text": "Great stream!" } ] },
                        "purchaseAmountText": { "simpleText": "$5.00" }
                      }
                    }
                  }
                },
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatMembershipItemRenderer": {
                        "id": "membership-1",
                        "authorName": { "simpleText": "ZeroMayo" },
                        "headerSubtext": { "runs": [ { "text": "Member (1 year)" } ] }
                      }
                    }
                  }
                },
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatUnknownRenderer": {
                        "id": "unknown-1"
                      }
                    }
                  }
                }
              ],
              "videoOffsetTimeMsec": "30000"
            }
          }
        ]
        """;

        [Test]
        public void ParsesTheAttachedExample()
        {
            IReadOnlyList<LiveChatReplayMessage> messages = LiveChatReplayParser.Parse(attached_example);

            Assert.That(messages, Has.Count.EqualTo(1));

            LiveChatReplayMessage message = messages[0];

            Assert.That(message.Kind, Is.EqualTo(LiveChatReplayMessageKind.ViewerEngagement));
            Assert.That(message.OffsetTimeMsec, Is.EqualTo(0).Within(0.001));
            Assert.That(message.Id, Is.Not.Empty);
            Assert.That(message.Text, Does.StartWith(@"Live chat replay is on."));
            Assert.That(message.Badges, Is.Empty);
        }

        [Test]
        public void ParsesTextMessageWithEmojiAndBadges()
        {
            IReadOnlyList<LiveChatReplayMessage> messages = LiveChatReplayParser.Parse(rich_example);

            Assert.That(messages, Has.Count.EqualTo(3));

            LiveChatReplayMessage message = messages[0];

            Assert.That(message.Kind, Is.EqualTo(LiveChatReplayMessageKind.Text));
            Assert.That(message.OffsetTimeMsec, Is.EqualTo(5000).Within(0.001));
            Assert.That(message.AuthorName, Is.EqualTo(@"Retsi"));
            Assert.That(message.AuthorChannelId, Is.EqualTo(@"UC0000000000000000000000"));
            Assert.That(message.AuthorPhotoUrl, Is.EqualTo(@"large.png"));
            Assert.That(message.Text, Is.EqualTo(@"Hello :wave: chat!"));
            Assert.That(message.Segments, Has.Count.EqualTo(3));
            Assert.That(message.Segments[0].Kind, Is.EqualTo(LiveChatReplayTextSegmentKind.Text));
            Assert.That(message.Segments[0].Text, Is.EqualTo(@"Hello "));
            Assert.That(message.Segments[1].Kind, Is.EqualTo(LiveChatReplayTextSegmentKind.Emoji));
            Assert.That(message.Segments[1].Text, Is.EqualTo(@":wave:"));
            Assert.That(message.Segments[1].ImageUrl, Is.EqualTo(@"emoji-large.png"));
            Assert.That(message.Segments[1].Label, Is.EqualTo(@"waving hand"));
            Assert.That(message.Segments[1].IsCustomEmoji, Is.True);
            Assert.That(message.Segments[1].HasImage, Is.True);
            Assert.That(message.Segments[2].Kind, Is.EqualTo(LiveChatReplayTextSegmentKind.Text));
            Assert.That(message.Segments[2].Text, Is.EqualTo(@" chat!"));
            Assert.That(message.Badges, Has.Count.EqualTo(2));
            Assert.That(message.Badges[0].Label, Is.EqualTo(@"Moderator"));
            Assert.That(message.Badges[1].Label, Is.EqualTo(@"Member (1 year)"));
            Assert.That(message.Badges[1].IconUrl, Is.EqualTo(@"badge.png"));
        }

        [Test]
        public void ParsesPaidAndMembershipMessagesAndIgnoresUnknownRenderers()
        {
            IReadOnlyList<LiveChatReplayMessage> messages = LiveChatReplayParser.Parse(rich_example);

            Assert.That(messages[1].Kind, Is.EqualTo(LiveChatReplayMessageKind.Paid));
            Assert.That(messages[1].OffsetTimeMsec, Is.EqualTo(30000).Within(0.001));
            Assert.That(messages[1].AmountText, Is.EqualTo(@"$5.00"));
            Assert.That(messages[1].Text, Is.EqualTo(@"Great stream!"));

            Assert.That(messages[2].Kind, Is.EqualTo(LiveChatReplayMessageKind.Membership));
            Assert.That(messages[2].Text, Is.EqualTo(@"Member (1 year)"));
        }

        [Test]
        public void ParsesEmojiOnlyMessageAndEmojiWithoutImage()
        {
            const string json = """
            [
              {
                "replayChatItemAction": {
                  "actions": [
                    {
                      "addChatItemAction": {
                        "item": {
                          "liveChatTextMessageRenderer": {
                            "id": "emoji-only-1",
                            "authorName": { "simpleText": "Retsi" },
                            "message": {
                              "runs": [
                                { "emoji": { "emojiId": "clap-emoji", "shortcuts": [":clapping_hands:"], "image": { "thumbnails": [ { "url": "clap.png" } ] } } },
                                { "emoji": { "shortcuts": [":heart:"] } }
                              ]
                            }
                          }
                        }
                      }
                    }
                  ],
                  "videoOffsetTimeMsec": "1000"
                }
              }
            ]
            """;

            IReadOnlyList<LiveChatReplayMessage> messages = LiveChatReplayParser.Parse(json);

            Assert.That(messages, Has.Count.EqualTo(1));

            LiveChatReplayMessage message = messages[0];

            Assert.That(message.Text, Is.EqualTo(@":clapping_hands::heart:"));
            Assert.That(message.Segments, Has.Count.EqualTo(2));
            Assert.That(message.Segments[0].HasImage, Is.True);
            Assert.That(message.Segments[0].ImageUrl, Is.EqualTo(@"clap.png"));
            Assert.That(message.Segments[1].HasImage, Is.False);
            Assert.That(message.Segments[1].Text, Is.EqualTo(@":heart:"));
        }

        [Test]
        public void ReturnsNoMessagesForMalformedInput()
        {
            Assert.That(LiveChatReplayParser.Parse(string.Empty), Is.Empty);
            Assert.That(LiveChatReplayParser.Parse("[]"), Is.Empty);
            Assert.That(LiveChatReplayParser.Parse("{\"replayChatItemAction\":{\"actions\":[]}}"), Is.Empty);
        }
    }
}

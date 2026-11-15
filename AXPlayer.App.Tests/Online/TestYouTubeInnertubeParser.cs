// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using AXPlayer.App.Online;
using Google.Apis.YouTube.v3.Data;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Online
{
    /// <summary>
    /// Verifies that the Innertube JSON responses are mapped onto the DTOs the rest of the
    /// application expects.
    /// </summary>
    [TestFixture]
    public class TestYouTubeInnertubeParser
    {
        private const string player_response = """
        {
          "videoDetails": {
            "videoId": "dQw4w9WgXcQ",
            "title": "Never Gonna Give You Up",
            "lengthSeconds": "213",
            "channelId": "UCuAXFkgsw1L7xaCfnd5JJOw",
            "shortDescription": "The official video.",
            "viewCount": "1600000000",
            "author": "Rick Astley",
            "isLiveContent": false
          },
          "microformat": {
            "playerMicroformatRenderer": {
              "publishDate": "2009-10-25T00:00:00-07:00",
              "category": "Music"
            }
          },
          "captions": {
            "playerCaptionsTracklistRenderer": {
              "captionTracks": [
                { "languageCode": "en" }
              ],
              "audioTracks": [
                { "audioTrackId": "en-US.4", "captionTrackIndices": [ 0 ] }
              ],
              "defaultAudioTrackIndex": 0
            }
          }
        }
        """;

        private const string watch_next_response = """
        {
          "contents": {
            "twoColumnWatchNextResults": {
              "results": {
                "results": {
                  "contents": [
                    {
                      "itemSectionRenderer": {
                        "sectionIdentifier": "comment-item-section",
                        "contents": [
                          {
                            "commentsEntryPointHeaderRenderer": {
                              "commentCount": { "simpleText": "1,234" }
                            }
                          }
                        ]
                      }
                    },
                    {
                      "videoPrimaryInfoRenderer": {
                        "videoActions": {
                          "menuRenderer": {
                            "topLevelButtons": [
                              {
                                "segmentedLikeDislikeButtonViewModel": {
                                  "likeButtonViewModel": {
                                    "likeButtonViewModel": {
                                      "accessibilityText": "like this video along with 1.2M other people"
                                    }
                                  }
                                }
                              }
                            ]
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
          }
        }
        """;

        private const string channel_response = """
        {
          "metadata": {
            "channelMetadataRenderer": {
              "title": "Rick Astley",
              "description": "The official Rick Astley channel.",
              "externalId": "UCuAXFkgsw1L7xaCfnd5JJOw",
              "vanityChannelUrl": "http://www.youtube.com/@RickAstleyYT",
              "avatar": {
                "thumbnails": [
                  { "url": "https://yt3.googleusercontent.com/small.jpg" },
                  { "url": "https://yt3.googleusercontent.com/large.jpg" }
                ]
              }
            }
          },
          "header": {
            "pageHeaderRenderer": {
              "content": {
                "pageHeaderViewModel": {
                  "metadata": {
                    "metadataRows": [
                      {
                        "metadataParts": [
                          { "text": { "content": "1.2M subscribers" } },
                          { "text": { "content": "123 videos" } }
                        ]
                      }
                    ]
                  }
                }
              }
            }
          }
        }
        """;

        private const string video_search_renderer = """
        {
          "videoId": "dQw4w9WgXcQ",
          "title": { "runs": [ { "text": "Never Gonna Give You Up" } ] },
          "ownerText": {
            "runs": [
              {
                "text": "Rick Astley",
                "navigationEndpoint": {
                  "browseEndpoint": { "browseId": "UCuAXFkgsw1L7xaCfnd5JJOw" }
                }
              }
            ]
          },
          "publishedTimeText": { "simpleText": "2 days ago" },
          "detailedMetadataSnippets": [
            { "snippetText": { "runs": [ { "text": "The official video." } ] } }
          ]
        }
        """;

        private const string comment_renderer = """
        {
          "commentId": "Ugx1234",
          "authorText": { "simpleText": "@viewer" },
          "authorThumbnail": {
            "thumbnails": [ { "url": "https://yt3.googleusercontent.com/avatar.jpg" } ]
          },
          "authorEndpoint": {
            "browseEndpoint": { "browseId": "UCviewer" }
          },
          "contentText": { "runs": [ { "text": "Nice song" }, { "text": "!" } ] },
          "publishedTimeText": { "simpleText": "2 days ago" },
          "voteCount": { "simpleText": "1.2K" },
          "replyCount": 3
        }
        """;

        private const string playlist_video_renderer = """
        {
          "videoId": "dQw4w9WgXcQ",
          "setVideoId": "set-video-id",
          "title": { "runs": [ { "text": "Never Gonna Give You Up" } ] },
          "shortBylineText": {
            "runs": [
              {
                "text": "Rick Astley",
                "navigationEndpoint": {
                  "browseEndpoint": { "browseId": "UCuAXFkgsw1L7xaCfnd5JJOw" }
                }
              }
            ]
          },
          "publishedTimeText": { "simpleText": "3 weeks ago" }
        }
        """;

        private static JsonElement parse(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            return document.RootElement.Clone();
        }

        [Test]
        public void TestVideoDetailsAreMapped()
        {
            Video video = YouTubeAPI.parseVideo(parse(player_response), "dQw4w9WgXcQ")!;

            Assert.That(video, Is.Not.Null);
            Assert.That(video.Id, Is.EqualTo("dQw4w9WgXcQ"));
            Assert.That(video.Snippet.Title, Is.EqualTo("Never Gonna Give You Up"));
            Assert.That(video.Snippet.ChannelId, Is.EqualTo("UCuAXFkgsw1L7xaCfnd5JJOw"));
            Assert.That(video.Snippet.ChannelTitle, Is.EqualTo("Rick Astley"));
            Assert.That(video.ContentDetails.Duration, Is.EqualTo("PT3M33S"));
            Assert.That(video.Statistics.ViewCount, Is.EqualTo(1_600_000_000UL));
            Assert.That(video.Snippet.DefaultLanguage, Is.EqualTo("en-US"));
            Assert.That(video.Snippet.PublishedAtDateTimeOffset, Is.Not.Null);
            Assert.That(video.TopicDetails.TopicCategories, Contains.Item("https://en.wikipedia.org/wiki/Music"));
        }

        [Test]
        public void TestLiveStreamSetsLiveStreamingDetails()
        {
            const string live_response = """
            {
              "videoDetails": {
                "videoId": "liveVideo123",
                "title": "Live stream",
                "lengthSeconds": "0",
                "channelId": "UCchannel",
                "viewCount": "42",
                "author": "Streamer",
                "isLiveContent": true,
                "isLive": true
              }
            }
            """;

            Video video = YouTubeAPI.parseVideo(parse(live_response), "liveVideo123")!;

            Assert.That(video.LiveStreamingDetails, Is.Not.Null);
            Assert.That(video.Snippet.LiveBroadcastContent, Is.EqualTo("live"));
        }

        [Test]
        public void TestWatchMetadataIsMapped()
        {
            var video = new Video
            {
                Id = "dQw4w9WgXcQ",
                Statistics = new VideoStatistics(),
                TopicDetails = new VideoTopicDetails
                {
                    TopicCategories = new List<string>(),
                },
            };

            YouTubeAPI.applyWatchMetadata(video, parse(watch_next_response));

            Assert.That(video.Statistics.CommentCount, Is.EqualTo(1234UL));
            Assert.That(video.Statistics.LikeCount, Is.EqualTo(1_200_000UL));
        }

        [Test]
        public void TestChannelIsMapped()
        {
            Channel channel = YouTubeAPI.parseChannel(parse(channel_response), "UCuAXFkgsw1L7xaCfnd5JJOw")!;

            Assert.That(channel, Is.Not.Null);
            Assert.That(channel.Id, Is.EqualTo("UCuAXFkgsw1L7xaCfnd5JJOw"));
            Assert.That(channel.Snippet.Title, Is.EqualTo("Rick Astley"));
            Assert.That(channel.Snippet.CustomUrl, Is.EqualTo("@RickAstleyYT"));
            Assert.That(channel.Snippet.Thumbnails.High.Url, Is.EqualTo("https://yt3.googleusercontent.com/large.jpg"));
            Assert.That(channel.Statistics.SubscriberCount, Is.EqualTo(1_200_000UL));
        }

        [Test]
        public void TestVideoSearchResultIsMapped()
        {
            SearchResult result = YouTubeAPI.parseVideoSearchResult(parse(video_search_renderer))!;

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Id.Kind, Is.EqualTo("youtube#video"));
            Assert.That(result.Id.VideoId, Is.EqualTo("dQw4w9WgXcQ"));
            Assert.That(result.Snippet.Title, Is.EqualTo("Never Gonna Give You Up"));
            Assert.That(result.Snippet.ChannelId, Is.EqualTo("UCuAXFkgsw1L7xaCfnd5JJOw"));
            Assert.That(result.Snippet.ChannelTitle, Is.EqualTo("Rick Astley"));
            Assert.That(result.Snippet.Description, Is.EqualTo("The official video."));
            Assert.That(result.Snippet.PublishedAtDateTimeOffset, Is.Not.Null);
        }

        [Test]
        public void TestCommentIsMapped()
        {
            Comment comment = YouTubeAPI.parseComment(parse(comment_renderer), "dQw4w9WgXcQ", null)!;

            Assert.That(comment, Is.Not.Null);
            Assert.That(comment.Id, Is.EqualTo("Ugx1234"));
            Assert.That(comment.Snippet.AuthorDisplayName, Is.EqualTo("@viewer"));
            Assert.That(comment.Snippet.AuthorChannelId.Value, Is.EqualTo("UCviewer"));
            Assert.That(comment.Snippet.TextOriginal, Is.EqualTo("Nice song!"));
            Assert.That(comment.Snippet.LikeCount, Is.EqualTo(1200));
            Assert.That(comment.Snippet.VideoId, Is.EqualTo("dQw4w9WgXcQ"));
        }

        [Test]
        public void TestCommentThreadCountsReplies()
        {
            const string thread = """
            {
              "comment": { "commentRenderer": {
                "commentId": "Ugx1234",
                "authorText": { "simpleText": "@viewer" },
                "contentText": { "runs": [ { "text": "Nice song" } ] },
                "voteCount": { "simpleText": "12" },
                "replyCount": 3
              } }
            }
            """;

            CommentThread threadResult = YouTubeAPI.parseCommentThread(parse(thread), "dQw4w9WgXcQ")!;

            Assert.That(threadResult, Is.Not.Null);
            Assert.That(threadResult.Snippet.TotalReplyCount, Is.EqualTo(3));
            Assert.That(threadResult.Snippet.TopLevelComment.Id, Is.EqualTo("Ugx1234"));
        }

        [Test]
        public void TestPlaylistItemIsMapped()
        {
            PlaylistItem item = YouTubeAPI.parsePlaylistVideoRenderer(parse(playlist_video_renderer), "PLplaylist", 0)!;

            Assert.That(item, Is.Not.Null);
            Assert.That(item.Id, Is.EqualTo("set-video-id"));
            Assert.That(item.Snippet.ResourceId.VideoId, Is.EqualTo("dQw4w9WgXcQ"));
            Assert.That(item.Snippet.ChannelId, Is.EqualTo("UCuAXFkgsw1L7xaCfnd5JJOw"));
            Assert.That(item.ContentDetails.VideoId, Is.EqualTo("dQw4w9WgXcQ"));
        }

        [Test]
        public void TestSubscribedStateIsMapped()
        {
            Assert.That(YouTubeAPI.tryReadSubscribedState(parse("""{ "subscribeButtonRenderer": { "subscribed": true } }""")), Is.True);
            Assert.That(YouTubeAPI.tryReadSubscribedState(parse("""{ "subscribeButtonRenderer": { "subscribed": false } }""")), Is.False);
            Assert.That(YouTubeAPI.tryReadSubscribedState(parse("""{ "button": { "accessibilityText": "Unsubscribe from Rick Astley" } }""")), Is.True);
            Assert.That(YouTubeAPI.tryReadSubscribedState(parse("""{ "metadata": { "title": "Rick Astley" } }""")), Is.Null);
        }

        [Test]
        public void TestDurationIsConverted()
        {
            Assert.That(YouTubeAPI.toIsoDuration(213), Is.EqualTo("PT3M33S"));
            Assert.That(YouTubeAPI.toIsoDuration(0), Is.EqualTo("PT0S"));
            Assert.That(YouTubeAPI.toIsoDuration(3725), Is.EqualTo("PT1H2M5S"));
        }

        [Test]
        public void TestRoughCountsAreParsed()
        {
            Assert.That(YouTubeAPI.parseRoughCount("1,234 views"), Is.EqualTo(1234UL));
            Assert.That(YouTubeAPI.parseRoughCount("1.2M subscribers"), Is.EqualTo(1_200_000UL));
            Assert.That(YouTubeAPI.parseRoughCount("12K"), Is.EqualTo(12_000UL));
            Assert.That(YouTubeAPI.parseRoughCount("3B views"), Is.EqualTo(3_000_000_000UL));
            Assert.That(YouTubeAPI.parseRoughCount("No views"), Is.Null);
        }

        [Test]
        public void TestRelativeDatesAreParsed()
        {
            DateTimeOffset? twoDaysAgo = YouTubeAPI.parseRelativeDate("2 days ago");

            Assert.That(twoDaysAgo, Is.Not.Null);
            Assert.That((DateTimeOffset.UtcNow - twoDaysAgo!.Value).TotalHours, Is.EqualTo(48).Within(1));

            Assert.That(YouTubeAPI.parseRelativeDate("streamed 3 weeks ago"), Is.Not.Null);
            Assert.That(YouTubeAPI.parseRelativeDate(""), Is.Null);
        }

        [Test]
        public void TestAccountMenuSelectsTheCurrentIdentity()
        {
            const string account_menu = """
            {
              "actions": [{ "openPopupAction": { "popup": { "multiPageMenuRenderer": { "sections": [
                { "accountItemSectionRenderer": { "contents": [
                  { "accountItem": { "accountName": { "runs": [ { "text": "Other channel" } ] }, "channelHandle": "@other", "isSelected": false } },
                  { "accountItem": { "accountName": { "runs": [ { "text": "My channel" } ] }, "channelHandle": "@mine", "isSelected": true } }
                ] } }
              ] } } } }]
            }
            """;

            IReadOnlyList<JsonElement> items = YouTubeAPI.prioritizeAccountItems(parse(account_menu));

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(YouTubeAPI.tryReadChannelHandle(items[0]), Is.EqualTo("@mine"));
        }

        [Test]
        public void TestAccountMenuWithoutSelectionIsNotGuessed()
        {
            const string account_menu = """
            {
              "sections": [
                { "accountItemSectionRenderer": { "contents": [
                  { "accountItem": { "channelHandle": "@one" } },
                  { "accountItem": { "channelHandle": "@two" } }
                ] } }
              ]
            }
            """;

            Assert.That(YouTubeAPI.prioritizeAccountItems(parse(account_menu)), Is.Empty);
        }

        [Test]
        public void TestUnreadableSelectedAccountNeverFallsBackToAnotherAccount()
        {
            const string account_menu = """
            {
              "sections": [
                { "accountItemSectionRenderer": { "contents": [
                  { "accountItem": { "accountName": { "runs": [ { "text": "Me" } ] }, "isSelected": true } },
                  { "accountItem": { "channelHandle": "@someone-else" } }
                ] } }
              ]
            }
            """;

            IReadOnlyList<JsonElement> items = YouTubeAPI.prioritizeAccountItems(parse(account_menu));

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(YouTubeAPI.tryReadChannelHandle(items[0]), Is.Null);
        }

        [Test]
        public void TestSingleAccountItemIsUsed()
        {
            const string account_menu = """
            {
              "sections": [
                { "accountItemSectionRenderer": { "contents": [
                  { "accountItem": { "accountName": { "runs": [ { "text": "@" }, { "text": "mine" } ] } } }
                ] } }
              ]
            }
            """;

            IReadOnlyList<JsonElement> items = YouTubeAPI.prioritizeAccountItems(parse(account_menu));

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(YouTubeAPI.tryReadChannelHandle(items[0]), Is.EqualTo("@mine"));
        }

        [Test]
        public void TestChannelIdIsReadFromAccountItem()
        {
            const string account_item = """
            {
              "channelHandle": "@mine",
              "endpoint": { "browseEndpoint": { "browseId": "UCuAXFkgsw1L7xaCfnd5JJOw" } }
            }
            """;

            Assert.That(YouTubeAPI.tryReadChannelId(parse(account_item)), Is.EqualTo("UCuAXFkgsw1L7xaCfnd5JJOw"));
            Assert.That(YouTubeAPI.tryReadChannelId(parse("""{ "endpoint": { "browseEndpoint": { "browseId": "VLplaylist" } } }""")), Is.Null);
        }

        [Test]
        public void TestOwnedChannelRendererIdIsRead()
        {
            const string channels_page = """
            {
              "contents": [{ "gridRenderer": { "items": [
                { "gridChannelRenderer": { "channelId": "UCuAXFkgsw1L7xaCfnd5JJOw" } }
              ] } }]
            }
            """;

            Assert.That(YouTubeAPI.tryReadFirstChannelRendererId(parse(channels_page)), Is.EqualTo("UCuAXFkgsw1L7xaCfnd5JJOw"));
            Assert.That(YouTubeAPI.tryReadFirstChannelRendererId(parse("""{ "contents": [] }""")), Is.Null);
        }
    }
}

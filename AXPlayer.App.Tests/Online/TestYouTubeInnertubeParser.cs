// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.That(video.LiveStreamingDetails.ConcurrentViewers, Is.EqualTo(42));
        }

        [Test]
        public void TestLiveStreamDetailsComeFromTheMicroformatAndWatchNext()
        {
            const string live_response = """
            {
              "videoDetails": {
                "videoId": "liveVideo123",
                "lengthSeconds": "0",
                "viewCount": "12345",
                "isLiveContent": true
              },
              "microformat": {
                "playerMicroformatRenderer": {
                  "liveBroadcastDetails": {
                    "isLiveNow": true,
                    "startTimestamp": "2024-05-01T10:00:00+00:00"
                  }
                }
              }
            }
            """;

            const string live_next_response = """
            {
              "contents": {
                "twoColumnWatchNextResults": {
                  "results": {
                    "results": {
                      "contents": [
                        {
                          "videoPrimaryInfoRenderer": {
                            "viewCount": {
                              "videoViewCountRenderer": {
                                "isLive": true,
                                "viewCount": { "simpleText": "12,345 watching now" }
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

            Video video = YouTubeAPI.parseVideo(parse(live_response), "liveVideo123")!;
            YouTubeAPI.applyLiveStreamingDetails(video, parse(live_response), parse(live_next_response));

            VideoLiveStreamingDetails details = video.LiveStreamingDetails;

            Assert.That(details.ActualStartTimeDateTimeOffset, Is.EqualTo(new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero)));
            Assert.That(details.ScheduledStartTimeDateTimeOffset, Is.EqualTo(details.ActualStartTimeDateTimeOffset));
            Assert.That(details.ActualEndTimeDateTimeOffset, Is.Null);
            Assert.That(details.ConcurrentViewers, Is.EqualTo(12_345));
            Assert.That(details.ActualStartTimeRaw, Is.Not.Null);
        }

        [Test]
        public void TestUpcomingStreamUsesTheOfflineSlateSchedule()
        {
            const string upcoming_response = """
            {
              "videoDetails": {
                "videoId": "upcoming123",
                "lengthSeconds": "0",
                "isLiveContent": true
              },
              "playabilityStatus": {
                "status": "LIVE_STREAM_OFFLINE",
                "liveStreamability": {
                  "liveStreamabilityRenderer": {
                    "offlineSlate": {
                      "liveStreamOfflineSlateRenderer": {
                        "scheduledStartTime": "1717243200"
                      }
                    }
                  }
                }
              },
              "microformat": {
                "playerMicroformatRenderer": {
                  "liveBroadcastDetails": { "isLiveNow": false }
                }
              }
            }
            """;

            Video video = YouTubeAPI.parseVideo(parse(upcoming_response), "upcoming123")!;
            VideoLiveStreamingDetails details = video.LiveStreamingDetails;

            Assert.That(details, Is.Not.Null);
            Assert.That(details.ActualStartTimeDateTimeOffset, Is.Null);
            Assert.That(details.ScheduledStartTimeDateTimeOffset, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1_717_243_200)));
            Assert.That(details.ConcurrentViewers, Is.Null);
        }

        [Test]
        public void TestUpcomingPremiereKeepsItsStartTimestampAsTheSchedule()
        {
            const string premiere_response = """
            {
              "videoDetails": { "videoId": "premiere123", "lengthSeconds": "0", "isLiveContent": true },
              "playabilityStatus": { "status": "LIVE_STREAM_OFFLINE" },
              "microformat": {
                "playerMicroformatRenderer": {
                  "liveBroadcastDetails": {
                    "isLiveNow": false,
                    "startTimestamp": "2024-06-01T18:00:00+00:00"
                  }
                }
              }
            }
            """;

            VideoLiveStreamingDetails details = YouTubeAPI.parseVideo(parse(premiere_response), "premiere123")!.LiveStreamingDetails;

            Assert.That(details.ActualStartTimeDateTimeOffset, Is.Null);
            Assert.That(details.ActualEndTimeDateTimeOffset, Is.Null);
            Assert.That(details.ScheduledStartTimeDateTimeOffset, Is.EqualTo(new DateTimeOffset(2024, 6, 1, 18, 0, 0, TimeSpan.Zero)));
        }

        [Test]
        public void TestFinishedStreamReportsItsActualEndTime()
        {
            const string finished_response = """
            {
              "videoDetails": {
                "videoId": "finished123",
                "lengthSeconds": "3600",
                "viewCount": "5000",
                "isLiveContent": true
              },
              "microformat": {
                "playerMicroformatRenderer": {
                  "liveBroadcastDetails": {
                    "isLiveNow": false,
                    "startTimestamp": "2024-05-01T10:00:00+00:00",
                    "endTimestamp": "2024-05-01T11:00:00+00:00"
                  }
                }
              }
            }
            """;

            VideoLiveStreamingDetails details = YouTubeAPI.parseVideo(parse(finished_response), "finished123")!.LiveStreamingDetails;

            Assert.That(details.ActualStartTimeDateTimeOffset, Is.EqualTo(new DateTimeOffset(2024, 5, 1, 10, 0, 0, TimeSpan.Zero)));
            Assert.That(details.ActualEndTimeDateTimeOffset, Is.EqualTo(new DateTimeOffset(2024, 5, 1, 11, 0, 0, TimeSpan.Zero)));
            Assert.That(details.ConcurrentViewers, Is.Null);
        }

        [Test]
        public void TestFinishedStreamFallsBackToItsDurationForTheEndTime()
        {
            const string finished_response = """
            {
              "videoDetails": {
                "videoId": "finished123",
                "lengthSeconds": "1800",
                "isLiveContent": true
              },
              "microformat": {
                "playerMicroformatRenderer": {
                  "liveBroadcastDetails": {
                    "isLiveNow": false,
                    "startTimestamp": "2024-05-01T10:00:00+00:00"
                  }
                }
              }
            }
            """;

            VideoLiveStreamingDetails details = YouTubeAPI.parseVideo(parse(finished_response), "finished123")!.LiveStreamingDetails;

            Assert.That(details.ActualEndTimeDateTimeOffset, Is.EqualTo(new DateTimeOffset(2024, 5, 1, 10, 30, 0, TimeSpan.Zero)));
            Assert.That(details.ScheduledStartTimeDateTimeOffset, Is.EqualTo(details.ActualStartTimeDateTimeOffset));
        }

        [Test]
        public void TestRegularVideoHasNoLiveStreamingDetails()
        {
            Video video = YouTubeAPI.parseVideo(parse(player_response), "dQw4w9WgXcQ")!;

            Assert.That(video.LiveStreamingDetails, Is.Null);
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
        public void TestCommentCountAndContinuationAreReadFromTheCommentPanel()
        {
            // The current watch page has no "commentCount" property: the count and the sort menu only
            // exist inside the engagement panel of the comments, and its section renders a single
            // continuation item instead of the items themselves.
            const string watch_page = """
            {
              "engagementPanels": [
                {
                  "engagementPanelSectionListRenderer": {
                    "panelIdentifier": "engagement-panel-comments-section",
                    "targetId": "engagement-panel-comments-section",
                    "header": {
                      "engagementPanelTitleHeaderRenderer": {
                        "title": { "runs": [ { "text": "댓글" } ] },
                        "contextualInfo": { "runs": [ { "text": "25" } ] },
                        "menu": {
                          "sortFilterSubMenuRenderer": {
                            "subMenuItems": [
                              { "title": "인기 댓글", "selected": true, "serviceEndpoint": { "continuationCommand": { "token": "top-token" } } },
                              { "title": "최신순", "serviceEndpoint": { "continuationCommand": { "token": "newest-token" } } }
                            ]
                          }
                        }
                      }
                    },
                    "content": {
                      "sectionListRenderer": {
                        "contents": [
                          {
                            "itemSectionRenderer": {
                              "sectionIdentifier": "comment-item-section",
                              "targetId": "engagement-panel-comments-section",
                              "contents": [
                                { "continuationItemRenderer": { "continuationEndpoint": { "continuationCommand": { "token": "section-token" } } } }
                              ]
                            }
                          }
                        ]
                      }
                    }
                  }
                }
              ]
            }
            """;

            var video = new Video
            {
                Id = "dQw4w9WgXcQ",
                Statistics = new VideoStatistics(),
                TopicDetails = new VideoTopicDetails
                {
                    TopicCategories = new List<string>(),
                },
            };

            YouTubeAPI.applyWatchMetadata(video, parse(watch_page));

            // A null comment count means "comments are disabled" for the player, so the count of the
            // panel header has to be read.
            Assert.That(video.Statistics.CommentCount, Is.EqualTo(25UL));
            Assert.That(YouTubeAPI.findCommentCount(parse("""{ "contents": [] }""")), Is.Null);

            Assert.That(YouTubeAPI.tryFindCommentContinuations(parse(watch_page)), Is.EqualTo(new[] { "section-token", "top-token" }));

            // The requested order is already selected, and the newest one is reachable.
            Assert.That(YouTubeAPI.tryFindSortContinuation(parse(watch_page), false), Is.Null);
            Assert.That(YouTubeAPI.tryFindSortContinuation(parse(watch_page), true), Is.EqualTo("newest-token"));
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
            Assert.That(YouTubeAPI.parseRoughCount("1.234 Aufrufe"), Is.EqualTo(1234UL));
            Assert.That(YouTubeAPI.parseRoughCount("1.2천회"), Is.EqualTo(1200UL));
            Assert.That(YouTubeAPI.parseRoughCount("1.2만명"), Is.EqualTo(12_000UL));
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

        [Test]
        public void TestPublishedAtFallsBackToTheWatchPageDateText()
        {
            const string player_without_dates = """
            {
              "videoDetails": {
                "videoId": "liveVideo123",
                "title": "Live stream",
                "lengthSeconds": "3600",
                "isLiveContent": true
              }
            }
            """;

            const string live_next_response = """
            {
              "contents": {
                "twoColumnWatchNextResults": {
                  "results": {
                    "results": {
                      "contents": [
                        {
                          "videoPrimaryInfoRenderer": {
                            "dateText": { "simpleText": "Streamed live on May 1, 2024" }
                          }
                        }
                      ]
                    }
                  }
                }
              }
            }
            """;

            Video video = YouTubeAPI.parseVideo(parse(player_without_dates), "liveVideo123")!;
            YouTubeAPI.applyPublishedAt(video, parse(player_without_dates), parse(live_next_response));

            Assert.That(video.Snippet.PublishedAtDateTimeOffset, Is.EqualTo(new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(video.Snippet.PublishedAtRaw, Is.Not.Null);
        }

        [Test]
        public void TestPublishedAtKeepsTheNumericOffsetOfTheMicroformat()
        {
            // The microformat writes its timestamp with a numeric offset, which a DateTime cannot hold.
            // Parsing it with "yyyy-MM-dd\THH:mm:ss\Z" fails - the pattern ends with a literal 'Z' - and
            // leaves the default value 0001-01-01 behind, so the offset aware timestamp is used.
            const string player_response = """
            {
              "videoDetails": { "videoId": "v", "title": "t", "lengthSeconds": "10" },
              "microformat": { "playerMicroformatRenderer": { "publishDate": "2026-09-27T05:27:05-07:00" } }
            }
            """;

            const string hour_only_offset = """
            {
              "videoDetails": { "videoId": "v", "title": "t", "lengthSeconds": "10" },
              "microformat": { "playerMicroformatRenderer": { "publishDate": "2026-09-27T05:27:05-07" } }
            }
            """;

            foreach (string response in new[] { player_response, hour_only_offset })
            {
                Video video = YouTubeAPI.parseVideo(parse(response), "v")!;

                Assert.That(video.Snippet.PublishedAtDateTimeOffset, Is.Not.Null, response);
                Assert.That(video.Snippet.PublishedAtDateTimeOffset!.Value.Offset, Is.EqualTo(TimeSpan.FromHours(-7)), response);
                Assert.That(video.Snippet.PublishedAtDateTimeOffset.Value.ToUniversalTime(),
                    Is.EqualTo(new DateTimeOffset(2026, 9, 27, 12, 27, 5, TimeSpan.Zero)), response);
            }
        }

        [Test]
        public void TestPublishedAtFallsBackToTheRelativeDate()
        {
            const string player_without_dates = """
            { "videoDetails": { "videoId": "v", "title": "t", "lengthSeconds": "10" } }
            """;

            const string next_response = """
            {
              "contents": {
                "twoColumnWatchNextResults": {
                  "results": {
                    "results": {
                      "contents": [
                        { "videoPrimaryInfoRenderer": { "relativeDateText": { "simpleText": "2 years ago" } } }
                      ]
                    }
                  }
                }
              }
            }
            """;

            Video video = YouTubeAPI.parseVideo(parse(player_without_dates), "v")!;
            YouTubeAPI.applyPublishedAt(video, parse(player_without_dates), parse(next_response));

            Assert.That(video.Snippet.PublishedAtDateTimeOffset, Is.Not.Null);
            Assert.That(video.Snippet.PublishedAtDateTimeOffset!.Value, Is.LessThan(DateTimeOffset.UtcNow.AddYears(-1)));
        }

        [Test]
        public void TestPlaylistOwnerChannelTitleIsParsed()
        {
            const string page = """
            {
              "header": {
                "playlistHeaderRenderer": {
                  "title": { "simpleText": "My mix" },
                  "ownerText": {
                    "runs": [
                      {
                        "text": "Nanahira",
                        "navigationEndpoint": { "browseEndpoint": { "browseId": "UCnanahira" } }
                      },
                      { "text": " and 3 others" }
                    ]
                  }
                }
              }
            }
            """;

            Assert.That(YouTubeAPI.findOwnerChannelTitle(parse(page)), Is.EqualTo("Nanahira"));
        }

        [Test]
        public void TestPlaylistOwnerChannelTitleIsParsedFromThePageHeader()
        {
            const string page = """
            {
              "header": {
                "pageHeaderRenderer": {
                  "content": {
                    "pageHeaderViewModel": {
                      "metadata": {
                        "contentMetadataViewModel": {
                          "metadataRows": [
                            {
                              "metadataParts": [
                                {
                                  "text": { "content": "Nanahira" },
                                  "commandRuns": [ { "startIndex": 0, "length": 8, "onTap": { "innertubeCommand": { "browseEndpoint": { "browseId": "UCnanahira" } } } } ]
                                }
                              ]
                            },
                            { "metadataParts": [ { "text": { "content": "42 videos" } }, { "text": { "content": "Public" } } ] }
                          ]
                        }
                      }
                    }
                  }
                }
              }
            }
            """;

            Assert.That(YouTubeAPI.findOwnerChannelTitle(parse(page)), Is.EqualTo("Nanahira"));
        }

        [Test]
        public void TestCommentTextIsReadFromTheEntityPayload()
        {
            // The current watch page renders the text, the author and the counters of a comment into
            // frameworkUpdates, while the item itself only keeps the keys.
            const string response = """
            {
              "onResponseReceivedEndpoints": [
                {
                  "reloadContinuationItemsCommand": {
                    "targetId": "comments-section",
                    "continuationItems": [
                      {
                        "commentThreadRenderer": {
                          "commentViewModel": { "commentId": "UgxComment1", "commentKey": "Eg0SC2tleTE" },
                          "replies": { "commentRepliesRenderer": { "contents": [ { "continuationItemRenderer": { "continuationEndpoint": { "continuationCommand": { "token": "REPLIES" } } } } ] } }
                        }
                      }
                    ]
                  }
                }
              ],
              "frameworkUpdates": {
                "entityBatchUpdate": {
                  "mutations": [
                    {
                      "entityKey": "Eg0SC2tleTE",
                      "type": "ENTITY_MUTATION_TYPE_REPLACE",
                      "payload": {
                        "commentEntityPayload": {
                          "key": "Eg0SC2tleTE",
                          "properties": {
                            "commentId": "UgxComment1",
                            "content": { "content": "great song" },
                            "publishedTime": "3일 전"
                          },
                          "author": {
                            "channelId": "UCauthor",
                            "displayName": "Listener",
                            "avatarThumbnailUrl": "https://example.com/avatar.jpg"
                          },
                          "toolbar": {
                            "likeCountNotliked": "1,234",
                            "replyCount": "2"
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """;

            JsonElement document = parse(response);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(document);

            Assert.That(entities, Does.ContainKey("Eg0SC2tleTE"));

            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(document));

            Assert.That(items, Has.Count.EqualTo(1));

            JsonElement threadRenderer = items[0].GetProperty("commentThreadRenderer");
            CommentThread thread = YouTubeAPI.parseCommentThread(threadRenderer, "video", entities)!;

            Assert.That(thread.Snippet.TopLevelComment.Snippet.TextDisplay, Is.EqualTo("great song"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorDisplayName, Is.EqualTo("Listener"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorChannelId.Value, Is.EqualTo("UCauthor"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorProfileImageUrl, Is.EqualTo("https://example.com/avatar.jpg"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.LikeCount, Is.EqualTo(1234));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.PublishedAtDateTimeOffset, Is.Not.Null);
            Assert.That(thread.Snippet.TotalReplyCount, Is.EqualTo(2));

            Assert.That(YouTubeAPI.findRepliesContinuation(threadRenderer), Is.EqualTo("REPLIES"));
        }

        [Test]
        public void TestCommentEntitiesWithoutAPayloadAreIgnored()
        {
            const string response = """
            {
              "onResponseReceivedEndpoints": [
                { "reloadContinuationItemsCommand": { "continuationItems": [ { "commentThreadRenderer": { "comment": { "commentRenderer": { "commentId": "Ugx1", "contentText": { "runs": [ { "text": "plain" } ] } } } } } ] } }
              ],
              "frameworkUpdates": { "entityBatchUpdate": { "mutations": [ { "entityKey": "other", "payload": { "commentEntityPayload": { "key": "other" } } } ] } }
            }
            """;

            JsonElement document = parse(response);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(document);
            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(document));

            CommentThread thread = YouTubeAPI.parseCommentThread(items[0].GetProperty("commentThreadRenderer"), "video", entities)!;

            Assert.That(thread.Snippet.TopLevelComment.Snippet.TextDisplay, Is.EqualTo("plain"));
        }

        [Test]
        public void TestCommentThreadOfTheCurrentWatchPageIsMapped()
        {
            // The current watch page returns the pages through reloadContinuationItemsCommand of the
            // comments section, nests the view model of a thread into another view model
            // (commentViewModel.commentViewModel) and keeps the text, the author and the counters in
            // a commentEntityPayload that is referenced by the percent-encoded commentKey.
            const string response = """
            {
              "onResponseReceivedEndpoints": [
                {
                  "reloadContinuationItemsCommand": {
                    "targetId": "comments-section",
                    "continuationItems": [ { "commentsHeaderRenderer": { "countText": { "simpleText": "25 Comments" } } } ]
                  }
                },
                {
                  "reloadContinuationItemsCommand": {
                    "targetId": "comments-section",
                    "continuationItems": [
                      {
                        "commentThreadRenderer": {
                          "trackingParams": "thread-tracking",
                          "isModeratedElqComment": false,
                          "commentViewModel": {
                            "commentViewModel": {
                              "commentId": "UgwY7bWm7zRZO7J4PBV4AaABAg",
                              "commentKey": "EhpVZ3dZN2JXbTd6UlpPN0o0UEJWNEFhQUJBZyAoKAE%3D",
                              "inlineRepliesKey": "empty_inline_replies",
                              "toolbarStateKey": "EhpVZ3dZN2JXbTd6UlpPN0o0UEJWNEFhQUJBZyAsKAE%3D"
                            }
                          },
                          "loggingDirectives": { "trackingParams": "thread-logging" }
                        }
                      },
                      { "continuationItemRenderer": { "continuationEndpoint": { "continuationCommand": { "token": "NEXT-PAGE" } } } }
                    ]
                  }
                }
              ],
              "frameworkUpdates": {
                "entityBatchUpdate": {
                  "mutations": [
                    {
                      "entityKey": "EgAgKygB",
                      "type": "ENTITY_MUTATION_TYPE_REPLACE",
                      "payload": { "commentSharedEntityPayload": { "key": "EgAgKygB", "clientName": "WEB" } }
                    },
                    {
                      "entityKey": "EhpVZ3dZN2JXbTd6UlpPN0o0UEJWNEFhQUJBZyAoKAE%3D",
                      "type": "ENTITY_MUTATION_TYPE_REPLACE",
                      "payload": {
                        "commentEntityPayload": {
                          "key": "EhpVZ3dZN2JXbTd6UlpPN0o0UEJWNEFhQUJBZyAoKAE%3D",
                          "properties": {
                            "commentId": "UgwY7bWm7zRZO7J4PBV4AaABAg",
                            "content": { "content": "I have no idea how this song is this underrated" },
                            "publishedTime": "1 year ago (edited)",
                            "replyLevel": 0
                          },
                          "author": {
                            "channelId": "UCmigaisnotacat",
                            "displayName": "@migaisnotacat",
                            "avatarThumbnailUrl": "https://example.com/avatar.jpg"
                          },
                          "toolbar": {
                            "likeCountNotliked": "1.2K",
                            "replyCount": "3"
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """;

            JsonElement document = parse(response);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(document);
            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(document));

            Assert.That(items, Has.Count.EqualTo(3));

            CommentThread? thread = YouTubeAPI.parseCommentThread(items[1], "video", entities);

            Assert.That(thread, Is.Not.Null);
            Assert.That(thread!.Id, Is.EqualTo("UgwY7bWm7zRZO7J4PBV4AaABAg"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.TextDisplay, Is.EqualTo("I have no idea how this song is this underrated"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorDisplayName, Is.EqualTo("@migaisnotacat"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorChannelId!.Value, Is.EqualTo("UCmigaisnotacat"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorProfileImageUrl, Is.EqualTo("https://example.com/avatar.jpg"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.LikeCount, Is.EqualTo(1200));
            Assert.That(thread.Snippet.TotalReplyCount, Is.EqualTo(3));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.PublishedAtDateTimeOffset, Is.Not.Null);

            // The token of the next page comes from the trailing continuation item of the page.
            Assert.That(YouTubeAPI.tryGetCommentElement(items[2]), Is.Null);
            Assert.That(YouTubeAPI.tryFindContinuationToken(items[2]), Is.EqualTo("NEXT-PAGE"));
        }

        [Test]
        public void TestCommentImageAttachmentIsReadFromTheSurfaceEntity()
        {
            // A comment can carry an image or GIF instead of text. The attachment lives in a
            // commentSurfaceEntityPayload which the item references with its commentSurfaceKey.
            const string response = """
            {
              "onResponseReceivedEndpoints": [
                {
                  "reloadContinuationItemsCommand": {
                    "continuationItems": [
                      {
                        "commentThreadRenderer": {
                          "commentViewModel": {
                            "commentViewModel": {
                              "commentId": "UgxGifComment",
                              "commentKey": "Eg0Sgifcomment",
                              "commentSurfaceKey": "Eg1Sgifsurface",
                              "toolbarStateKey": "Eg0Stoolbar"
                            }
                          }
                        }
                      }
                    ]
                  }
                }
              ],
              "frameworkUpdates": {
                "entityBatchUpdate": {
                  "mutations": [
                    {
                      "entityKey": "Eg0Sgifcomment",
                      "payload": {
                        "commentEntityPayload": {
                          "key": "Eg0Sgifcomment",
                          "properties": {
                            "commentId": "UgxGifComment",
                            "content": { "content": "" },
                            "publishedTime": "2 days ago"
                          },
                          "author": { "channelId": "UCauthor", "displayName": "@gifposter" },
                          "toolbar": { "likeCountNotliked": "42" }
                        }
                      }
                    },
                    {
                      "entityKey": "Eg1Sgifsurface",
                      "payload": {
                        "commentSurfaceEntityPayload": {
                          "key": "Eg1Sgifsurface",
                          "imageAttachmentViewModel": {
                            "commentImageAttachmentViewModel": {
                              "width": 498,
                              "height": 374,
                              "aspectRatio": 1.3315508,
                              "image": {
                                "sources": [
                                  { "url": "https://i.ytimg.com/c_exp/example_webp_animated_original_aspect_ratio.webp", "width": 498, "height": 374 }
                                ],
                                "imageFormatHint": "IMAGE_FORMAT_ANIMATED_WEBP"
                              }
                            }
                          }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """;

            JsonElement document = parse(response);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(document);
            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(document));

            CommentThread thread = YouTubeAPI.parseCommentThread(items[0], "video", entities)!;
            Comment comment = thread.Snippet.TopLevelComment;

            Assert.That(comment, Is.TypeOf<AXPlayerComment>());
            Assert.That(comment.Snippet.TextDisplay, Is.Empty);
            Assert.That(((AXPlayerComment)comment).ImageAttachment, Is.Not.Null);
            Assert.That(((AXPlayerComment)comment).ImageAttachment!.Url, Is.EqualTo("https://i.ytimg.com/c_exp/example_webp_animated_original_aspect_ratio.webp"));
            Assert.That(((AXPlayerComment)comment).ImageAttachment!.Width, Is.EqualTo(498));
            Assert.That(((AXPlayerComment)comment).ImageAttachment!.Height, Is.EqualTo(374));
            Assert.That(((AXPlayerComment)comment).ImageAttachment!.IsAnimated, Is.True);
        }

        [Test]
        public void TestCommentThreadIsReadFromTheNestedViewModel()
        {
            // Current responses nest the view model into "comment" and keep the replies of the
            // thread inside that view model.
            const string response = """
            {
              "onResponseReceivedEndpoints": [
                {
                  "reloadContinuationItemsCommand": {
                    "targetId": "comments-section",
                    "continuationItems": [
                      {
                        "commentThreadRenderer": {
                          "comment": {
                            "commentViewModel": {
                              "commentId": "UgxThread1",
                              "commentKey": "Eg0Sthread1",
                              "replies": {
                                "commentRepliesRenderer": {
                                  "contents": [
                                    { "continuationItemRenderer": { "continuationEndpoint": { "continuationCommand": { "token": "REPLY-TOKEN" } } } }
                                  ]
                                }
                              }
                            }
                          }
                        }
                      },
                      { "continuationItemRenderer": { "continuationEndpoint": { "continuationCommand": { "token": "NEXT-PAGE" } } } }
                    ]
                  }
                }
              ],
              "frameworkUpdates": {
                "entityBatchUpdate": {
                  "mutations": [
                    {
                      "entityKey": "Eg0Sthread1",
                      "payload": {
                        "commentEntityPayload": {
                          "key": "Eg0Sthread1",
                          "properties": {
                            "commentId": "UgxThread1",
                            "content": { "content": "nested text" },
                            "publishedTime": "1 day ago"
                          },
                          "author": { "channelId": "UCnested", "displayName": "Nested" },
                          "toolbar": { "replyCount": "7" }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """;

            JsonElement document = parse(response);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(document);
            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(document));

            Assert.That(items, Has.Count.EqualTo(2));

            CommentThread thread = YouTubeAPI.parseCommentThread(items[0], "video", entities)!;

            Assert.That(thread.Id, Is.EqualTo("UgxThread1"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.TextDisplay, Is.EqualTo("nested text"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorDisplayName, Is.EqualTo("Nested"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorChannelId!.Value, Is.EqualTo("UCnested"));
            Assert.That(thread.Snippet.TotalReplyCount, Is.EqualTo(7));
            Assert.That(YouTubeAPI.findRepliesContinuation(items[0]), Is.EqualTo("REPLY-TOKEN"));
        }

        [Test]
        public void TestPercentEncodedEntityKeysAreMatched()
        {
            // The responses keep the keys of the items percent-encoded, while the payload can keep
            // the plain form, so both sides have to be compared unescaped.
            const string response = """
            {
              "onResponseReceivedEndpoints": [
                {
                  "reloadContinuationItemsCommand": {
                    "continuationItems": [
                      { "commentThreadRenderer": { "comment": { "commentViewModel": { "commentId": "Ugx1", "commentKey": "Eg0Skey%3D" } } } }
                    ]
                  }
                }
              ],
              "frameworkUpdates": {
                "entityBatchUpdate": {
                  "mutations": [
                    {
                      "entityKey": "Eg0Skey=",
                      "payload": {
                        "commentEntityPayload": {
                          "key": "Eg0Skey=",
                          "properties": { "commentId": "Ugx1", "content": { "content": "encoded key" } },
                          "author": { "displayName": "Author" }
                        }
                      }
                    }
                  ]
                }
              }
            }
            """;

            JsonElement document = parse(response);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(document);
            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(document));

            CommentThread thread = YouTubeAPI.parseCommentThread(items[0], "video", entities)!;

            Assert.That(thread.Snippet.TopLevelComment.Snippet.TextDisplay, Is.EqualTo("encoded key"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorDisplayName, Is.EqualTo("Author"));
        }

        [Test]
        public void TestInlineRepliesAreReadFromTheRepliesRenderer()
        {
            const string item = """
            {
              "commentThreadRenderer": {
                "comment": { "commentViewModel": { "commentId": "UgxThread1", "commentKey": "Eg0Sthread1" } },
                "replies": {
                  "commentRepliesRenderer": {
                    "contents": [
                      { "commentRenderer": { "commentId": "UgxReply1", "contentText": { "runs": [ { "text": "first reply" } ] } } }
                    ],
                    "viewReplies": { "buttonRenderer": { "text": { "simpleText": "3 replies" } } }
                  }
                }
              }
            }
            """;

            CommentThread thread = YouTubeAPI.parseCommentThread(parse(item), "video")!;
            List<Comment> replies = YouTubeAPI.parseInlineReplies(parse(item), "video", thread.Id)!;

            Assert.That(replies, Has.Count.EqualTo(1));
            Assert.That(replies[0].Id, Is.EqualTo("UgxReply1"));
            Assert.That(replies[0].Snippet.TextDisplay, Is.EqualTo("first reply"));
            Assert.That(replies[0].Snippet.ParentId, Is.EqualTo("UgxThread1"));

            // The amount of replies is only rendered into the button of the replies renderer.
            Assert.That(thread.Snippet.TotalReplyCount, Is.EqualTo(3));
        }

        [Test]
        public void TestCreateCommentParamsAreReadFromTheCommentComposer()
        {
            // The composer of the comment section renders the parameters that allow posting a comment
            // next to its placeholder. They only exist for a signed-in session, which is why the
            // request that reads them has to be authenticated.
            const string signed_in = """
            {
              "onResponseReceivedEndpoints": [
                {
                  "reloadContinuationItemsCommand": {
                    "continuationItems": [
                      {
                        "commentsHeaderRenderer": {
                          "createRenderer": {
                            "commentSimpleboxRenderer": {
                              "placeholderText": { "runs": [ { "text": "Add a comment..." } ] },
                              "createCommentParams": "CglWSURFT19JRA%3D%3D"
                            }
                          }
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

            const string signed_out = """
            {
              "commentsHeaderRenderer": {
                "createRenderer": {
                  "commentSimpleboxRenderer": {
                    "placeholderText": { "runs": [ { "text": "Add a comment..." } ] },
                    "prepareAccountEndpoint": { "modalEndpoint": { "modal": {} } }
                  }
                }
              }
            }
            """;

            Assert.That(YouTubeAPI.findFirstString(parse(signed_in), new[] { "createCommentParams" }), Is.EqualTo("CglWSURFT19JRA%3D%3D"));
            Assert.That(YouTubeAPI.findFirstString(parse(signed_out), new[] { "createCommentParams" }), Is.Null);
        }

        [Test]
        public void TestEditActionsAreRecognised()
        {
            // The TV edit endpoint answers with the actions it carried out, which is how a created
            // playlist is recognised when the response does not echo its id.
            Assert.That(YouTubeAPI.hasEditActions(parse("""{ "actions": [ { "clickTrackingParams": "x" } ] }""")), Is.True);
            Assert.That(YouTubeAPI.hasEditActions(parse("""{ "actions": [] }""")), Is.False);
            Assert.That(YouTubeAPI.hasEditActions(parse("""{ "responseContext": {} }""")), Is.False);
        }

        [Test]
        public void TestCommentComposerIsDetected()
        {
            // The composer renders the sign-in prompt instead of its parameters for a signed-out
            // session, so its presence tells the two cases apart.
            Assert.That(YouTubeAPI.hasCommentComposer(parse("""
            { "commentsHeaderRenderer": { "createRenderer": { "commentSimpleboxRenderer": { "placeholderText": { "simpleText": "Add a comment..." } } } } }
            """)), Is.True);

            Assert.That(YouTubeAPI.hasCommentComposer(parse("""{ "commentsHeaderRenderer": { "createRenderer": {} } }""")), Is.False);
            Assert.That(YouTubeAPI.hasCommentComposer(parse("""{ "commentsHeaderRenderer": {} }""")), Is.False);
        }

        [Test]
        public void TestCreatedCommentIsDetected()
        {
            Assert.That(YouTubeAPI.hasCreatedComment(parse("""{ "actions": [ { "createCommentAction": { "comment": { "commentId": "Ugx1" } } } ] }""")), Is.True);
            Assert.That(YouTubeAPI.hasCreatedComment(parse("""
            { "frameworkUpdates": { "entityBatchUpdate": { "mutations": [ { "payload": { "commentEntityPayload": { "properties": { "commentId": "Ugx1" } } } } ] } } }
            """)), Is.True);

            // An answer without the created comment did not post it.
            Assert.That(YouTubeAPI.hasCreatedComment(parse("""{ "actions": [] }""")), Is.False);
            Assert.That(YouTubeAPI.hasCreatedComment(parse("""{ "responseContext": {} }""")), Is.False);
        }

        [Test]
        public void TestRejectedCommentReportsItsReason()
        {
            // A rejected comment is answered with the reason instead of the created comment.
            Assert.That(YouTubeAPI.findCommentPostError(parse("""{ "error": { "code": 400, "message": "Comment text cannot be empty" } }""")),
                Is.EqualTo("Comment text cannot be empty"));

            Assert.That(YouTubeAPI.findCommentPostError(parse("""
            {
              "actions": [
                { "alertWithButtonRenderer": { "text": { "runs": [ { "text": "Comments are turned off" } ] } } }
              ]
            }
            """)), Is.EqualTo("Comments are turned off"));

            Assert.That(YouTubeAPI.findCommentPostError(parse("""
            {
              "actions": [ { "createCommentAction": { "comment": { "commentId": "Ugx1" } } } ]
            }
            """)), Is.Null);
        }

        [Test]
        public void TestDumpsAreWrittenWithoutTheEnvironmentVariable()
        {
            Assert.That(YouTubeAPI.shouldDumpInnertube(null), Is.True);
            Assert.That(YouTubeAPI.shouldDumpInnertube(string.Empty), Is.True);
            Assert.That(YouTubeAPI.shouldDumpInnertube(@"1"), Is.True);
            Assert.That(YouTubeAPI.shouldDumpInnertube(@"0"), Is.False);
            Assert.That(YouTubeAPI.shouldDumpInnertube(@" 0 "), Is.False);
        }

        [Test]
        public void TestCommentDumpsAreTaggedWithTheirEndpoint()
        {
            var timestamp = new DateTime(2026, 11, 15, 7, 42, 44, 461, DateTimeKind.Utc);

            Assert.That(YouTubeAPI.buildDumpFileName(@"next", YouTubeAPI.comments_dump_tag, timestamp),
                Is.EqualTo(@"20261115-074244-461-next (comments).json"));

            Assert.That(YouTubeAPI.buildDumpFileName(@"comment/create_comment", YouTubeAPI.comments_dump_tag, timestamp),
                Is.EqualTo(@"20261115-074244-461-comment_create_comment (comments).json"));

            Assert.That(YouTubeAPI.buildDumpFileName(@"browse", null, timestamp),
                Is.EqualTo(@"20261115-074244-461-browse.json"));
        }

        [Test]
        public void TestRelativeDatesAreParsedInTheInterfaceLanguage()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;

            void assertClose(DateTimeOffset? parsed, DateTimeOffset expected, double toleranceDays, string text)
            {
                Assert.That(parsed, Is.Not.Null, text);
                Assert.That(Math.Abs((parsed!.Value - expected).TotalDays), Is.LessThan(toleranceDays), text);
            }

            assertClose(YouTubeAPI.parseRelativeDate(@"22시간 전"), now.AddHours(-22), 1, @"22시간 전");
            assertClose(YouTubeAPI.parseRelativeDate(@"1일 전"), now.AddDays(-1), 1, @"1일 전");
            assertClose(YouTubeAPI.parseRelativeDate(@"3주 전"), now.AddDays(-21), 1, @"3주 전");
            assertClose(YouTubeAPI.parseRelativeDate(@"1개월 전"), now.AddMonths(-1), 3, @"1개월 전");
            assertClose(YouTubeAPI.parseRelativeDate(@"5년 전"), now.AddYears(-5), 3, @"5년 전");
            assertClose(YouTubeAPI.parseRelativeDate(@"5 分前"), now.AddMinutes(-5), 1, @"5 分前");
            assertClose(YouTubeAPI.parseRelativeDate(@"1 か月前"), now.AddMonths(-1), 3, @"1 か月前");
            assertClose(YouTubeAPI.parseRelativeDate(@"3 days ago"), now.AddDays(-3), 1, @"3 days ago");
            assertClose(YouTubeAPI.parseRelativeDate(@"Streamed live 2 hours ago"), now.AddHours(-2), 1, @"Streamed live 2 hours ago");
        }

        [Test]
        public void TestRelativeDateFallsBackToAnAbsoluteDate()
        {
            Assert.That(YouTubeAPI.parseRelativeDate(@"2020. 7. 13."), Is.EqualTo(new DateTimeOffset(2020, 7, 13, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(YouTubeAPI.parseRelativeDate(@"조회수 718,941회"), Is.Null);
        }

        [Test]
        public void TestChannelStatisticsComeFromThePageHeader()
        {
            // The recommended channels of the page are rendered before the header and repeat the
            // "N subscribers" wording, so their counts must not be mistaken for the channel's own.
            const string page = """
            {
              "contents": [
                { "shelfRenderer": { "headerRenderer": { "channelTitleText": { "runs": [ { "text": "@OtherChannel • 42.9K subscribers" } ] } } } },
                { "gridRenderer": { "items": [ { "gridChannelRenderer": { "title": { "simpleText": "@OtherChannel" } } } ] } }
              ],
              "header": {
                "pageHeaderRenderer": {
                  "pageTitle": "Real Channel",
                  "content": {
                    "pageHeaderViewModel": {
                      "metadata": {
                        "contentMetadataViewModel": {
                          "metadataRows": [
                            { "metadataParts": [ { "text": { "content": "@RealHandle" } } ] },
                            { "metadataParts": [ { "text": { "content": "258K subscribers" } }, { "text": { "content": "226 videos" } } ] }
                          ]
                        }
                      }
                    }
                  }
                }
              },
              "metadata": {
                "channelMetadataRenderer": { "externalId": "UCreal", "title": "Real Channel", "description": "d" }
              }
            }
            """;

            Channel channel = YouTubeAPI.parseChannel(parse(page), "UCreal")!;

            Assert.That(channel.Snippet.Title, Is.EqualTo("Real Channel"));
            Assert.That(channel.Snippet.CustomUrl, Is.EqualTo("@RealHandle"));
            Assert.That(channel.Statistics.SubscriberCount, Is.EqualTo(258_000));
            Assert.That(channel.Statistics.VideoCount, Is.EqualTo(226));
        }

        [Test]
        public void TestChannelStatisticsAreReadInTheInterfaceLanguage()
        {
            const string page = """
            {
              "header": {
                "pageHeaderRenderer": {
                  "content": {
                    "pageHeaderViewModel": {
                      "metadata": {
                        "contentMetadataViewModel": {
                          "metadataRows": [
                            { "metadataParts": [ { "text": { "content": "@RealHandle" } } ] },
                            { "metadataParts": [ { "text": { "content": "구독자 25.8만명" } }, { "text": { "content": "동영상 1,234개" } } ] }
                          ]
                        }
                      }
                    }
                  }
                }
              },
              "metadata": {
                "channelMetadataRenderer": { "externalId": "UCreal", "title": "Real Channel", "description": "d" }
              }
            }
            """;

            Channel channel = YouTubeAPI.parseChannel(parse(page), "UCreal")!;

            Assert.That(channel.Statistics.SubscriberCount, Is.EqualTo(258_000));
            Assert.That(channel.Statistics.VideoCount, Is.EqualTo(1_234));
        }

        [Test]
        public void TestSubscribedStateComesFromThePageHeader()
        {
            // The TV channel page renders its header after the shelves, which carry the subscribe
            // buttons of every recommended channel.
            const string page = """
            {
              "contents": [
                {
                  "tvBrowseRenderer": {
                    "content": {
                      "tvSurfaceContentRenderer": {
                        "content": {
                          "sectionListRenderer": {
                            "contents": [
                              { "shelfRenderer": { "content": { "channelRenderer": { "subscribeButtonRenderer": { "subscribed": true } } } } }
                            ]
                          }
                        },
                        "header": {
                          "channelHeaderRenderer": {
                            "title": { "simpleText": "Real Channel" },
                            "buttons": [ { "subscribeButtonRenderer": { "subscribed": false } } ]
                          }
                        }
                      }
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.tryReadSubscribedState(parse(page)), Is.False);
        }

        [Test]
        public void TestPlaylistItemsContinuationIsFound()
        {
            const string page = """
            {
              "contents": [
                {
                  "playlistVideoListRenderer": {
                    "contents": [
                      { "playlistVideoRenderer": { "videoId": "aaaaaaaaaaa", "title": { "runs": [ { "text": "a" } ] } } },
                      {
                        "continuationItemRenderer": {
                          "continuationEndpoint": {
                            "continuationCommand": { "token": "CONTINUATION", "request": "CONTINUATION_REQUEST_TYPE_BROWSE" }
                          }
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.tryFindContinuationToken(parse(page)), Is.EqualTo("CONTINUATION"));
        }

        [Test]
        public void TestPlaylistItemsWithoutAContinuationAreComplete()
        {
            const string page = """
            {
              "contents": [
                {
                  "playlistVideoListRenderer": {
                    "contents": [
                      { "playlistVideoRenderer": { "videoId": "aaaaaaaaaaa", "title": { "runs": [ { "text": "a" } ] } } }
                    ]
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.tryFindContinuationToken(parse(page)), Is.Null);
        }

        [Test]
        public void TestFullDescriptionReplacesTruncatedLinks()
        {
            // "Music " (6) + the abbreviated link (34) + " and " (5) + "#tag" (4).
            const string player_response = """
            {
              "videoDetails": {
                "videoId": "v",
                "title": "t",
                "shortDescription": "Music https://example.com/watch?v=abc... and #tag",
                "lengthSeconds": "10"
              }
            }
            """;

            const string watch_page = """
            {
              "contents": [
                {
                  "videoSecondaryInfoRenderer": {
                    "attributedDescription": {
                      "content": "Music https://example.com/watch?v=abc... and #tag",
                      "commandRuns": [
                        {
                          "startIndex": 6,
                          "length": 34,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": {
                                "url": "https://www.youtube.com/redirect?event=video_description&q=https%3A%2F%2Fexample.com%2Fwatch%3Fv%3Dabc&v=abc"
                              }
                            }
                          }
                        },
                        {
                          "startIndex": 45,
                          "length": 4,
                          "onTap": {
                            "innertubeCommand": {
                              "commandMetadata": { "webCommandMetadata": { "url": "/hashtag/tag" } }
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """;

            Video video = YouTubeAPI.parseVideo(parse(player_response), "v")!;
            YouTubeAPI.applyFullDescription(video, parse(watch_page));

            Assert.That(video.Snippet.Description, Is.EqualTo("Music https://example.com/watch?v=abc and #tag"));
            Assert.That(video.Snippet.Localized.Description, Is.EqualTo(video.Snippet.Description));
        }

        [Test]
        public void TestFullDescriptionShowsTheUrlOfLinksWithALabel()
        {
            // A handle and a hashtag are both rendered as a label; the handle has to show its target,
            // while the hashtag is readable on its own.
            const string content = "Twitter /cametek #nanahira";
            const string watch_page = """
            {
              "contents": [
                {
                  "videoSecondaryInfoRenderer": {
                    "attributedDescription": {
                      "content": "Twitter /cametek #nanahira",
                      "commandRuns": [
                        {
                          "startIndex": 8,
                          "length": 8,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": { "url": "https://www.youtube.com/redirect?q=https%3A%2F%2Ftwitter.com%2Fcametek" }
                            }
                          }
                        },
                        {
                          "startIndex": 17,
                          "length": 9,
                          "onTap": {
                            "innertubeCommand": {
                              "commandMetadata": { "webCommandMetadata": { "url": "/hashtag/nanahira" } }
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.extractFullDescription(parse(watch_page)), Is.EqualTo("Twitter https://twitter.com/cametek #nanahira"));
        }

        [Test]
        public void TestFullDescriptionKeepsThePaddingOfAReplacedLabel()
        {
            // The runs of a description are padded with non-breaking spaces, so the replacement keeps
            // them instead of joining the neighbouring text.
            const string watch_page = """
            {
              "contents": [
                {
                  "videoSecondaryInfoRenderer": {
                    "attributedDescription": {
                      "content": "\u00a0\u00a0/\u00a0cametek\u00a0\u00a0",
                      "commandRuns": [
                        {
                          "startIndex": 0,
                          "length": 13,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": { "url": "https://www.youtube.com/redirect?q=https%3A%2F%2Ftwitter.com%2Fcametek" }
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.extractFullDescription(parse(watch_page)),
                Is.EqualTo("\u00a0\u00a0https://twitter.com/cametek\u00a0\u00a0"));
        }

        [Test]
        public void TestFullDescriptionKeepsTimestamps()
        {
            // A timestamp link jumps into the video, so its label is kept instead of the URL, while a
            // video link shows its target.
            const string watch_page = """
            {
              "contents": [
                {
                  "videoSecondaryInfoRenderer": {
                    "attributedDescription": {
                      "content": "Intro 1:23 Part",
                      "commandRuns": [
                        {
                          "startIndex": 6,
                          "length": 4,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": { "url": "https://www.youtube.com/watch?v=abc&t=83s" }
                            }
                          }
                        },
                        {
                          "startIndex": 11,
                          "length": 4,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": { "url": "https://www.youtube.com/watch?v=abc" }
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.extractFullDescription(parse(watch_page)),
                Is.EqualTo("Intro 1:23 https://www.youtube.com/watch?v=abc"));
        }

        [Test]
        public void TestFullDescriptionShowsTheTargetOfACallToActionLink()
        {
            // A call-to-action link (the one YouTube renders as
            // "ytAttributedStringLink ytAttributedStringLinkCallToActionColor") keeps an arbitrary
            // label like "Visit my shop", so its target replaces the label.
            const string watch_page = """
            {
              "contents": [
                {
                  "videoSecondaryInfoRenderer": {
                    "attributedDescription": {
                      "content": "Merch store\nVisit my shop Watch the video",
                      "commandRuns": [
                        {
                          "startIndex": 12,
                          "length": 13,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": { "url": "https://www.youtube.com/redirect?q=https%3A%2F%2Fshop.example.com%2Fmerch" }
                            }
                          }
                        },
                        {
                          "startIndex": 26,
                          "length": 15,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": { "url": "https://www.youtube.com/watch?v=abc" }
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.extractFullDescription(parse(watch_page)),
                Is.EqualTo("Merch store\nhttps://shop.example.com/merch https://www.youtube.com/watch?v=abc"));
        }

        [Test]
        public void TestFullDescriptionShowsTheTargetOfALegacyCallToActionLink()
        {
            const string watch_page = """
            {
              "contents": [
                {
                  "videoSecondaryInfoRenderer": {
                    "description": {
                      "runs": [
                        { "text": "Tickets: " },
                        {
                          "text": "Buy now",
                          "navigationEndpoint": {
                            "urlEndpoint": { "url": "https://www.youtube.com/redirect?q=https%3A%2F%2Ftickets.example.com%2Fbuy" }
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.extractFullDescription(parse(watch_page)),
                Is.EqualTo("Tickets: https://tickets.example.com/buy"));
        }

        [Test]
        public void TestFullDescriptionFromLegacyRuns()
        {
            const string watch_page = """
            {
              "contents": [
                {
                  "videoSecondaryInfoRenderer": {
                    "description": {
                      "runs": [
                        { "text": "Store " },
                        {
                          "text": "https://example.com/shopdetail...",
                          "navigationEndpoint": {
                            "urlEndpoint": {
                              "url": "https://www.youtube.com/redirect?q=https%3A%2F%2Fexample.com%2Fshopdetail%2F123"
                            }
                          }
                        },
                        { "text": " " },
                        {
                          "text": "#nanahira",
                          "navigationEndpoint": {
                            "urlEndpoint": {
                              "url": "https://www.youtube.com/redirect?q=https%3A%2F%2Fwww.youtube.com%2Fhashtag%2Fnanahira"
                            }
                          }
                        }
                      ]
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.extractFullDescription(parse(watch_page)), Is.EqualTo("Store https://example.com/shopdetail/123 #nanahira"));
        }

        [Test]
        public void TestFullDescriptionDoesNotShrinkTheSnippet()
        {
            const string player_response = """
            {
              "videoDetails": {
                "videoId": "v",
                "title": "t",
                "shortDescription": "the player already had a longer description of the video",
                "lengthSeconds": "10"
              }
            }
            """;

            const string watch_page = """
            { "contents": [ { "videoSecondaryInfoRenderer": { "description": { "simpleText": "short" } } } ] }
            """;

            Video video = YouTubeAPI.parseVideo(parse(player_response), "v")!;
            YouTubeAPI.applyFullDescription(video, parse(watch_page));

            Assert.That(video.Snippet.Description, Is.EqualTo("the player already had a longer description of the video"));
        }

        [Test]
        public void TestLocalizedDateTextIsParsed()
        {
            Assert.That(YouTubeAPI.parseDateText("May 1, 2024"), Is.EqualTo(new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(YouTubeAPI.parseDateText("Premiered May 1, 2024"), Is.EqualTo(new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(YouTubeAPI.parseDateText("2024. 5. 1."), Is.EqualTo(new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(YouTubeAPI.parseDateText("2024년 5월 1일"), Is.EqualTo(new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(YouTubeAPI.parseDateText("no date"), Is.Null);
        }

        [Test]
        public void TestLocalizationsIncludeTheInterfaceTranslation()
        {
            const string player_response = """
            {
              "videoDetails": {
                "videoId": "v",
                "title": "Original title",
                "shortDescription": "Original description",
                "lengthSeconds": "10"
              },
              "captions": {
                "playerCaptionsTracklistRenderer": {
                  "captionTracks": [ { "languageCode": "en" } ],
                  "audioTracks": [ { "audioTrackId": "en-US.4" } ]
                }
              }
            }
            """;

            const string korean_next_response = """
            {
              "contents": {
                "twoColumnWatchNextResults": {
                  "results": {
                    "results": {
                      "contents": [
                        {
                          "videoPrimaryInfoRenderer": {
                            "title": { "runs": [ { "text": "번역된 제목" } ] }
                          }
                        },
                        {
                          "videoSecondaryInfoRenderer": {
                            "attributedDescription": { "content": "번역된 설명" }
                          }
                        }
                      ]
                    }
                  }
                }
              }
            }
            """;

            const string japanese_next_response = """
            {
              "contents": [
                { "videoPrimaryInfoRenderer": { "title": { "simpleText": "翻訳されたタイトル" } } }
              ]
            }
            """;

            Video video = YouTubeAPI.parseVideo(parse(player_response), "v")!;
            YouTubeAPI.applyLocalizations(video, new Dictionary<string, JsonElement>
            {
                ["ko"] = parse(korean_next_response),
                ["ja"] = parse(japanese_next_response),
            }, "ko-KR");

            Assert.That(video.Localizations["ko"].Title, Is.EqualTo("번역된 제목"));
            Assert.That(video.Localizations["ko"].Description, Is.EqualTo("번역된 설명"));
            Assert.That(video.Localizations["ko-KR"].Title, Is.EqualTo("번역된 제목"));
            Assert.That(video.Localizations["ja"].Title, Is.EqualTo("翻訳されたタイトル"));
            Assert.That(video.Localizations["en-US"].Title, Is.EqualTo("Original title"));
            Assert.That(video.Snippet.Title, Is.EqualTo("Original title"));
        }

        [Test]
        public void TestLocalizationsWithoutATranslationKeepTheDefaultLanguage()
        {
            const string player_response = """
            { "videoDetails": { "videoId": "v", "title": "Original title", "lengthSeconds": "10" } }
            """;

            Video video = YouTubeAPI.parseVideo(parse(player_response), "v")!;
            YouTubeAPI.applyLocalizations(video, new Dictionary<string, JsonElement>
            {
                ["ko"] = parse("""{ "contents": [] }"""),
            }, "ko-KR");

            Assert.That(video.Localizations, Is.Empty);
        }

        [Test]
        public void TestChannelLocalizationsIncludeEveryRequestedLanguage()
        {
            const string english_page = """
            {
              "metadata": {
                "channelMetadataRenderer": { "externalId": "UCreal", "title": "Real Channel", "description": "Original description" }
              }
            }
            """;

            const string korean_page = """
            {
              "metadata": {
                "channelMetadataRenderer": { "externalId": "UCreal", "title": "진짜 채널", "description": "번역된 설명" }
              }
            }
            """;

            Channel channel = YouTubeAPI.parseChannel(parse(english_page), "UCreal")!;
            YouTubeAPI.applyLocalizations(channel, new Dictionary<string, JsonElement>
            {
                ["en"] = parse(english_page),
                ["ko"] = parse(korean_page),
            }, "ko-KR");

            Assert.That(channel.Localizations["en"].Title, Is.EqualTo("Real Channel"));
            Assert.That(channel.Localizations["ko"].Title, Is.EqualTo("진짜 채널"));
            Assert.That(channel.Localizations["ko"].Description, Is.EqualTo("번역된 설명"));
            Assert.That(channel.Localizations["ko-KR"].Title, Is.EqualTo("진짜 채널"));
            Assert.That(channel.Snippet.Title, Is.EqualTo("Real Channel"));
        }

        [Test]
        public void TestCommentsContinuationIsFoundInAnEngagementPanel()
        {
            const string watch_page = """
            {
              "engagementPanels": [
                {
                  "engagementPanelSectionListRenderer": {
                    "panelIdentifier": "comment-item-section",
                    "content": {
                      "continuationItemRenderer": {
                        "continuationEndpoint": { "continuationCommand": { "token": "comments-token" } }
                      }
                    }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.tryFindCommentsContinuation(parse(watch_page)), Is.EqualTo("comments-token"));
        }

        [Test]
        public void TestCommentsContinuationIsFoundInAnItemSection()
        {
            const string watch_page = """
            {
              "contents": [
                {
                  "itemSectionRenderer": {
                    "sectionIdentifier": "comment-item-section",
                    "contents": [
                      {
                        "continuationItemRenderer": {
                          "continuationEndpoint": { "continuationCommand": { "token": "comments-token" } }
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.tryFindCommentsContinuation(parse(watch_page)), Is.EqualTo("comments-token"));
            Assert.That(YouTubeAPI.tryFindCommentsContinuation(parse("""{ "contents": [] }""")), Is.Null);
        }

        [Test]
        public void TestNewestSortContinuationUsesTheUnselectedMenuItem()
        {
            const string comments_page = """
            {
              "header": {
                "commentsHeaderRenderer": {
                  "sortMenu": {
                    "sortFilterSubMenuRenderer": {
                      "subMenuItems": [
                        {
                          "title": "인기 댓글 순",
                          "selected": true,
                          "serviceEndpoint": { "continuationCommand": { "token": "top-token" } }
                        },
                        {
                          "title": "최신순",
                          "selected": false,
                          "serviceEndpoint": { "continuationCommand": { "token": "newest-token" } }
                        }
                      ]
                    }
                  }
                }
              }
            }
            """;

            Assert.That(YouTubeAPI.tryFindSortContinuation(parse(comments_page), true), Is.EqualTo("newest-token"));
            Assert.That(YouTubeAPI.tryFindSortContinuation(parse("""{ "header": {} }"""), true), Is.Null);
        }

        [Test]
        public void TestSortContinuationIsNotRequestedWhenTheOrderIsAlreadySelected()
        {
            const string newest_is_selected = """
            {
              "header": {
                "commentsHeaderRenderer": {
                  "sortMenu": {
                    "sortFilterSubMenuRenderer": {
                      "subMenuItems": [
                        { "title": "Top comments", "serviceEndpoint": { "continuationCommand": { "token": "top-token" } } },
                        { "title": "Newest first", "selected": true }
                      ]
                    }
                  }
                }
              }
            }
            """;

            // Switching away from an order that is already selected would show the opposite order.
            Assert.That(YouTubeAPI.tryFindSortContinuation(parse(newest_is_selected), true), Is.Null);
            Assert.That(YouTubeAPI.tryFindSortContinuation(parse(newest_is_selected), false), Is.EqualTo("top-token"));
        }

        [Test]
        public void TestSortContinuationFallsBackToTheMenuPosition()
        {
            // The labels are localized, so an unknown translation has to be resolved by the position
            // of the entry: YouTube renders "top comments" first.
            const string localized_menu = """
            {
              "header": {
                "commentsHeaderRenderer": {
                  "sortMenu": {
                    "sortFilterSubMenuRenderer": {
                      "subMenuItems": [
                        { "title": { "simpleText": "Pertinenteste" }, "selected": true },
                        { "title": { "simpleText": "Nyeste" }, "serviceEndpoint": { "continuationCommand": { "token": "newest-token" } } }
                      ]
                    }
                  }
                }
              }
            }
            """;

            Assert.That(YouTubeAPI.tryFindSortContinuation(parse(localized_menu), true), Is.EqualTo("newest-token"));
            Assert.That(YouTubeAPI.tryFindSortContinuation(parse(localized_menu), false), Is.Null);
        }

        [Test]
        public void TestContinuationItemsAreReadFromResponseActions()
        {
            // A browse continuation appends its items through "onResponseReceivedActions" instead of
            // "onResponseReceivedEndpoints".
            const string response = """
            {
              "onResponseReceivedActions": [
                { "appendContinuationItemsAction": { "continuationItems": [ { "lockupViewModel": { "contentId": "PLone" } } ] } }
              ]
            }
            """;

            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(parse(response)));

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].TryGetProperty("lockupViewModel", out _), Is.True);
        }

        [Test]
        public void TestCommentContinuationsKeepTheSectionAndThePanelToken()
        {
            // A watch page renders the section as an engagement panel that carries one continuation
            // in its content and the sort continuations in its header, so both are kept as
            // candidates instead of picking one.
            const string watch_page = """
            {
              "engagementPanels": [
                {
                  "engagementPanelSectionListRenderer": {
                    "panelIdentifier": "comment-item-section",
                    "header": {
                      "engagementPanelTitleHeaderRenderer": {
                        "menu": {
                          "sortFilterSubMenuRenderer": {
                            "subMenuItems": [
                              { "title": "Top", "selected": true, "continuation": { "reloadContinuationData": { "continuation": "top-token" } } },
                              { "title": "Newest", "continuation": { "reloadContinuationData": { "continuation": "newest-token" } } }
                            ]
                          }
                        }
                      }
                    },
                    "content": {
                      "sectionListRenderer": {
                        "contents": [
                          {
                            "itemSectionRenderer": {
                              "continuations": [ { "reloadContinuationData": { "continuation": "section-token" } } ],
                              "sectionIdentifier": "comment-item-section"
                            }
                          }
                        ]
                      }
                    }
                  }
                }
              ]
            }
            """;

            List<string> continuations = YouTubeAPI.tryFindCommentContinuations(parse(watch_page));

            Assert.That(continuations, Is.EqualTo(new[] { "section-token", "top-token" }));
            Assert.That(YouTubeAPI.tryFindCommentsContinuation(parse(watch_page)), Is.EqualTo("section-token"));
        }

        [Test]
        public void TestPercentEncodedContinuationTokensAreDecoded()
        {
            // The responses echo the tokens of the sort menu percent-encoded.
            Assert.That(YouTubeAPI.normalizeContinuationToken("Eg0Skey%3D"), Is.EqualTo("Eg0Skey="));
            Assert.That(YouTubeAPI.normalizeContinuationToken("Eg0Skey"), Is.EqualTo("Eg0Skey"));
        }

        [Test]
        public void TestContinuationContentsAreReadFromTheSectionAndReplyRenderers()
        {
            const string section_page = """
            {
              "continuationContents": {
                "commentSectionRenderer": {
                  "contents": [ { "commentThreadRenderer": { "comment": { "commentRenderer": { "commentId": "c1" } } } } ]
                }
              }
            }
            """;

            const string reply_page = """
            {
              "continuationContents": {
                "commentRepliesRenderer": {
                  "contents": [ { "commentRenderer": { "commentId": "r1" } } ]
                }
              }
            }
            """;

            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(parse(section_page)));
            List<JsonElement> replies = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(parse(reply_page)));

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].TryGetProperty("commentThreadRenderer", out _), Is.True);

            Assert.That(replies, Has.Count.EqualTo(1));
            Assert.That(replies[0].GetProperty("commentRenderer").GetProperty("commentId").GetString(), Is.EqualTo("r1"));
        }

        [Test]
        public void TestResponseShapeDescriptionsDoNotThrow()
        {
            // The diagnostics are logged for responses of an unknown shape, so they have to survive
            // any value kind.
            Assert.That(YouTubeAPI.describeElement(parse("""{ "onResponseReceivedEndpoints": [], "frameworkUpdates": { "entityBatchUpdate": { "mutations": [] } } }""")),
                Does.Contain("onResponseReceivedEndpoints: [0 items]"));

            Assert.That(YouTubeAPI.describeElement(parse("""[1, 2]""")), Is.EqualTo("[2 items]"));
            Assert.That(YouTubeAPI.describeElement(default), Is.EqualTo("Undefined"));
        }

        [Test]
        public void TestFlatCommentViewModelIsMapped()
        {
            const string item = """
            {
              "commentViewModel": {
                "commentKey": "Ug123",
                "contentText": { "runs": [ { "text": "hello" } ] },
                "authorText": { "runs": [ { "text": "someone" } ] },
                "authorEndpoint": { "browseEndpoint": { "browseId": "UCauthor" } },
                "publishedTimeText": { "runs": [ { "text": "2 days ago" } ] },
                "voteCount": { "simpleText": "3" }
              }
            }
            """;

            CommentThread? thread = YouTubeAPI.parseCommentThread(parse(item), "video");

            Assert.That(thread, Is.Not.Null);
            Assert.That(thread!.Snippet.TopLevelComment.Snippet.TextDisplay, Is.EqualTo("hello"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorDisplayName, Is.EqualTo("someone"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.AuthorChannelId!.Value, Is.EqualTo("UCauthor"));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.LikeCount, Is.EqualTo(3));
            Assert.That(thread.Snippet.TopLevelComment.Snippet.PublishedAtDateTimeOffset, Is.Not.Null);
        }

        [Test]
        public void TestPlaylistTabContinuationIsFound()
        {
            const string library_tabs = """
            {
              "chips": [
                {
                  "chipCloudChipRenderer": {
                    "text": { "simpleText": "기록" },
                    "navigationEndpoint": { "continuationCommand": { "token": "history-tab" } }
                  }
                },
                {
                  "chipCloudChipRenderer": {
                    "text": { "simpleText": "재생목록" },
                    "navigationEndpoint": { "continuationCommand": { "token": "playlists-tab" } }
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.findPlaylistTabContinuation(parse(library_tabs)), Is.EqualTo("playlists-tab"));
            Assert.That(YouTubeAPI.findPlaylistTabContinuation(parse("""{ "chips": [] }""")), Is.Null);
        }

        [Test]
        public void TestPlaylistIdIsDecodedFromLibraryTabParams()
        {
            // Real parameters of the TV library tabs.
            Assert.That(YouTubeAPI.tryDecodePlaylistId("cASyBCJQTEpXX0pObzdUamoxV0c4czVtNnhsOGJWRFZhOFU2aGhC"), Is.EqualTo("PLJW_JNo7Tjj1WG8s5m6xl8bVDVa8U6hhB"));
            Assert.That(YouTubeAPI.tryDecodePlaylistId("cASyBCVMUlNSVEtFTS1OekJEVFlPQmVaMXU3MGhiZzJNd1lPTzh6dUtn"), Is.EqualTo("LRSRTKEM-NzBDTYOBeZ1u70hbg2MwYOO8zuKg"));
            Assert.That(YouTubeAPI.tryDecodePlaylistId("cACyBClPTEFLNXV5X2tJOEcyY19MVnlmb0VxT1RjN1NSQmRvSWlXZWc3QTNRaw%3D%3D"), Is.EqualTo("OLAK5uy_kI8G2c_LVyfoEqOTc7SRBdoIiWeg7A3Qk"));

            // Built-in library tabs are not playlists.
            Assert.That(YouTubeAPI.tryDecodePlaylistId("cAg%3D"), Is.Null);
            Assert.That(YouTubeAPI.tryDecodePlaylistId("cASyBAJMTA%3D%3D"), Is.Null);
            Assert.That(YouTubeAPI.tryDecodePlaylistId(null), Is.Null);
        }

        [Test]
        public void TestTvLibraryPlaylistsAreReadFromTabs()
        {
            const string library_page = """
            {
              "contents": {
                "tvBrowseRenderer": {
                  "content": {
                    "tvSecondaryNavRenderer": {
                      "sections": [
                        {
                          "tvSecondaryNavSectionRenderer": {
                            "tabs": [
                              {
                                "tabRenderer": {
                                  "title": "History",
                                  "endpoint": { "browseEndpoint": { "browseId": "FEmy_youtube", "params": "cAg%3D" } }
                                }
                              },
                              {
                                "tabRenderer": {
                                  "title": "Liked videos",
                                  "endpoint": { "browseEndpoint": { "browseId": "FEmy_youtube", "params": "cASyBAJMTA%3D%3D" } }
                                }
                              },
                              {
                                "tabRenderer": {
                                  "title": "리아의 플리!",
                                  "endpoint": { "browseEndpoint": { "browseId": "FEmy_youtube", "params": "cACyBCJQTFRXek45M0RBOV8yYm5wV094TzlrSTR1aTNxTGo3M0Fj" } },
                                  "thumbnail": {
                                    "thumbnails": [
                                      { "url": "https://i.ytimg.com/vi/S-hmpoAFm6M/hqdefault.jpg", "width": 320, "height": 180 }
                                    ]
                                  }
                                }
                              }
                            ]
                          }
                        }
                      ]
                    }
                  }
                }
              }
            }
            """;

            var playlists = new List<Playlist>();
            YouTubeAPI.collectLibraryPlaylistTabs(parse(library_page), playlists);

            Assert.That(playlists, Has.Count.EqualTo(1));
            Assert.That(playlists[0].Id, Is.EqualTo("PLTWzN93DA9_2bnpWOxO9kI4ui3qLj73Ac"));
            Assert.That(playlists[0].Snippet.Title, Is.EqualTo("리아의 플리!"));
            Assert.That(playlists[0].Snippet.Thumbnails.High.Url, Is.EqualTo("https://i.ytimg.com/vi/S-hmpoAFm6M/hqdefault.jpg"));
        }

        [Test]
        public void TestLibraryPlaylistsGetTheirOwnerChannel()
        {
            var playlists = new List<Playlist>
            {
                new Playlist { Id = "PLone", Snippet = new PlaylistSnippet { Title = "one" } },
                new Playlist { Id = "PLtwo", Snippet = new PlaylistSnippet { Title = "two", ChannelId = "UCother" } },
            };

            YouTubeAPI.applyOwnerToPlaylists(playlists, "UCmine");

            Assert.That(playlists[0].Snippet.ChannelId, Is.EqualTo("UCmine"));
            Assert.That(playlists[1].Snippet.ChannelId, Is.EqualTo("UCother"));
        }

        [Test]
        public void TestPlaylistContinuationIsFoundInThePlaylistGrid()
        {
            const string library_page = """
            {
              "contents": [
                {
                  "gridRenderer": {
                    "items": [
                      { "gridPlaylistRenderer": { "playlistId": "PLone" } },
                      {
                        "continuationItemRenderer": {
                          "continuationEndpoint": { "continuationCommand": { "token": "next-page" } }
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.tryFindPlaylistContinuation(parse(library_page)), Is.EqualTo("next-page"));

            const string mixed_grid = """
            {
              "contents": [
                {
                  "gridRenderer": {
                    "items": [
                      { "videoRenderer": { "videoId": "abc" } },
                      {
                        "continuationItemRenderer": {
                          "continuationEndpoint": { "continuationCommand": { "token": "unrelated" } }
                        }
                      }
                    ]
                  }
                }
              ]
            }
            """;

            Assert.That(YouTubeAPI.tryFindPlaylistContinuation(parse(mixed_grid)), Is.Null);
        }

        [Test]
        public void TestPlaylistLockupsAreFilteredByContentId()
        {
            Assert.That(YouTubeAPI.isPlaylistLockup(parse("""{ "contentId": "PLone", "contentType": "LOCKUP_CONTENT_TYPE_PLAYLIST" }""")), Is.True);
            Assert.That(YouTubeAPI.isPlaylistLockup(parse("""{ "contentId": "PLone" }""")), Is.True);
            Assert.That(YouTubeAPI.isPlaylistLockup(parse("""{ "contentId": "dQw4w9WgXcQ", "contentType": "LOCKUP_CONTENT_TYPE_VIDEO" }""")), Is.False);
        }

        [Test]
        public void TestCommentInsertPayloadCarriesTheTextAndTheVideo()
        {
            string json = JsonSerializer.Serialize(YouTubeAPI.buildCommentInsertPayload("dQw4w9WgXcQ", "hello"));

            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement snippet = document.RootElement.GetProperty("snippet");

            Assert.That(snippet.GetProperty("videoId").GetString(), Is.EqualTo("dQw4w9WgXcQ"));
            Assert.That(snippet.GetProperty("topLevelComment").GetProperty("snippet").GetProperty("textOriginal").GetString(), Is.EqualTo("hello"));
        }

        [Test]
        public void TestPlaylistInsertPayloadCarriesTheTitleAndThePrivacy()
        {
            string json = JsonSerializer.Serialize(YouTubeAPI.buildPlaylistInsertPayload("test2", PrivacyStatus.Unlisted));

            using JsonDocument document = JsonDocument.Parse(json);

            Assert.That(document.RootElement.GetProperty("snippet").GetProperty("title").GetString(), Is.EqualTo("test2"));
            Assert.That(document.RootElement.GetProperty("status").GetProperty("privacyStatus").GetString(), Is.EqualTo("unlisted"));
        }

        [Test]
        public void TestOwnPlaylistKeepsThePrivacyOfTheDataApi()
        {
            const string response = """
                                     {
                                       "id": "PLpublic",
                                       "snippet": {
                                         "title": "open list",
                                         "channelId": "UCme",
                                         "publishedAt": "2026-11-16T09:30:12Z",
                                         "thumbnails": { "high": { "url": "https://example.com/p.jpg" }, "default": { "url": "https://example.com/s.jpg" } }
                                       },
                                       "status": { "privacyStatus": "unlisted" },
                                       "contentDetails": { "itemCount": 25 }
                                     }
                                     """;

            Playlist? playlist = YouTubeAPI.parseDataApiPlaylist(parse(response));

            Assert.That(playlist, Is.Not.Null);
            Assert.That(playlist!.Id, Is.EqualTo("PLpublic"));
            Assert.That(playlist.Status.PrivacyStatus, Is.EqualTo("unlisted"));
            Assert.That(playlist.Snippet.Title, Is.EqualTo("open list"));
            Assert.That(playlist.Snippet.ChannelId, Is.EqualTo("UCme"));
            Assert.That(playlist.Snippet.Thumbnails.High.Url, Is.EqualTo("https://example.com/p.jpg"));
            Assert.That(playlist.ContentDetails.ItemCount, Is.EqualTo(25));
            Assert.That(playlist.Snippet.PublishedAtDateTimeOffset, Is.Not.Null);
        }

        [Test]
        public void TestPrivacyStatusIsReadForEverySpelling()
        {
            Assert.That(YouTubeAPI.parseDataApiPlaylist(parse("""{ "id": "PLone", "status": { "privacyStatus": "PRIVATE" } }"""))!.Status.PrivacyStatus,
                Is.EqualTo("private"));

            Assert.That(YouTubeAPI.parseDataApiPlaylist(parse("""{ "id": "PLone", "status": { "privacyStatus": "public" } }"""))!.Status.PrivacyStatus,
                Is.EqualTo("public"));

            // A response without the field is a playlist whose privacy was not reported.
            Assert.That(YouTubeAPI.parseDataApiPlaylist(parse("""{ "id": "PLone" }"""))!.Status.PrivacyStatus, Is.EqualTo("public"));
        }

        [Test]
        public void TestPrivacyIsReadFromTheBadgeOfALibraryPage()
        {
            Assert.That(YouTubeAPI.tryMapPrivacyToken("PRIVACY_PRIVATE"), Is.EqualTo("private"));
            Assert.That(YouTubeAPI.tryMapPrivacyToken("PRIVACY_UNLISTED"), Is.EqualTo("unlisted"));
            Assert.That(YouTubeAPI.tryMapPrivacyToken("PRIVACY_PUBLIC"), Is.EqualTo("public"));
            Assert.That(YouTubeAPI.tryMapPrivacyToken("비공개"), Is.EqualTo("private"));
            Assert.That(YouTubeAPI.tryMapPrivacyToken("일부 공개"), Is.EqualTo("unlisted"));
            Assert.That(YouTubeAPI.tryMapPrivacyToken("전체 공개"), Is.EqualTo("public"));

            // "비공개" contains "공개", so only whole texts may be compared.
            Assert.That(YouTubeAPI.tryMapPrivacyToken("동영상 25개"), Is.Null);
        }

        [Test]
        public void TestOnlyThePlaylistsThatTheAccountCreatedAreKept()
        {
            const string own_channel = "UCme";

            var playlists = new List<Playlist>
            {
                new Playlist { Id = "PLcreated", Snippet = new PlaylistSnippet { ChannelId = own_channel } },
                new Playlist { Id = "PLwithoutOwner", Snippet = new PlaylistSnippet() },
                new Playlist { Id = "PLotherChannel", Snippet = new PlaylistSnippet { ChannelId = "UCother" } },
                new Playlist { Id = "WL", Snippet = new PlaylistSnippet { ChannelId = own_channel } },
                new Playlist { Id = "LL" + own_channel, Snippet = new PlaylistSnippet { ChannelId = own_channel } },
                new Playlist { Id = "FL", Snippet = new PlaylistSnippet { ChannelId = own_channel } },
            };

            YouTubeAPI.removePlaylistsThatWereNotCreated(playlists, own_channel);

            Assert.That(playlists.Select(playlist => playlist.Id), Is.EqualTo(new[] { "PLcreated", "PLwithoutOwner" }));
        }

        [Test]
        public void TestTheListsOfTheAccountItselfAreNotCreatedOnes()
        {
            Assert.That(YouTubeAPI.isOwnCreatedPlaylist(new Playlist { Id = "WL" }, null), Is.False);
            Assert.That(YouTubeAPI.isOwnCreatedPlaylist(new Playlist { Id = "LLUCme" }, null), Is.False);
            Assert.That(YouTubeAPI.isOwnCreatedPlaylist(new Playlist { Id = "FL" }, null), Is.False);
            Assert.That(YouTubeAPI.isOwnCreatedPlaylist(new Playlist { Id = "PLone" }, null), Is.True);
            Assert.That(YouTubeAPI.isOwnCreatedPlaylist(new Playlist { Id = string.Empty }, null), Is.False);

            // A row of another channel is not a playlist the account created.
            Assert.That(YouTubeAPI.isOwnCreatedPlaylist(new Playlist { Id = "PLone", Snippet = new PlaylistSnippet { ChannelId = "UCother" } }, "UCme"), Is.False);
            Assert.That(YouTubeAPI.isOwnCreatedPlaylist(new Playlist { Id = "PLone", Snippet = new PlaylistSnippet { ChannelId = "UCme" } }, "UCme"), Is.True);
        }

        [Test]
        public void TestDetachedItemsSurviveTheirDocument()
        {
            List<JsonElement> items;

            using (JsonDocument document = JsonDocument.Parse("""{ "commentThreadRenderer": { "comment": { "commentRenderer": { "commentId": "c1" } } } }"""))
            {
                items = YouTubeAPI.detachItems(new[] { document.RootElement });
            }

            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].GetProperty("commentThreadRenderer").GetProperty("comment").GetProperty("commentRenderer").GetProperty("commentId").GetString(), Is.EqualTo("c1"));
        }
    }
}

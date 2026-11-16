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
        public void TestFullDescriptionKeepsTheLabelOfANonUrlLink()
        {
            // A link whose label is a channel name or a hashtag is not a link placeholder, so its
            // text has to stay untouched.
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
                          "length": 9,
                          "onTap": {
                            "innertubeCommand": {
                              "urlEndpoint": { "url": "https://www.youtube.com/redirect?q=https%3A%2F%2Ftwitter.com%2Fcametek" }
                            }
                          }
                        },
                        {
                          "startIndex": 18,
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

            Assert.That(YouTubeAPI.extractFullDescription(parse(watch_page)), Is.EqualTo(content));
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

            const string localized_nextResponse = """
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

            Video video = YouTubeAPI.parseVideo(parse(player_response), "v")!;
            YouTubeAPI.applyLocalizations(video, parse(localized_nextResponse), "ko", "ko-KR");

            Assert.That(video.Localizations["ko"].Title, Is.EqualTo("번역된 제목"));
            Assert.That(video.Localizations["ko"].Description, Is.EqualTo("번역된 설명"));
            Assert.That(video.Localizations["ko-KR"].Title, Is.EqualTo("번역된 제목"));
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
            YouTubeAPI.applyLocalizations(video, parse("""{ "contents": [] }"""), "ko", "ko-KR");

            Assert.That(video.Localizations, Is.Empty);
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

            Assert.That(YouTubeAPI.tryFindNewestSortContinuation(parse(comments_page)), Is.EqualTo("newest-token"));
            Assert.That(YouTubeAPI.tryFindNewestSortContinuation(parse("""{ "header": {} }""")), Is.Null);
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

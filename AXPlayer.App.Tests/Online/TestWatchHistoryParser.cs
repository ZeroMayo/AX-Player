// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using AXPlayer.App.Online;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Online
{
    [TestFixture]
    public class TestWatchHistoryParser
    {
        private const string video_renderer_example = """
        {
          "contents": {
            "twoColumnBrowseResultsRenderer": {
              "tabs": [
                {
                  "tabRenderer": {
                    "content": {
                      "sectionListRenderer": {
                        "contents": [
                          {
                            "itemSectionRenderer": {
                              "header": {
                                "itemSectionTitleRenderer": {
                                  "title": { "runs": [ { "text": "Today" } ] }
                                }
                              },
                              "contents": [
                                {
                                  "richItemRenderer": {
                                    "content": {
                                      "videoRenderer": {
                                        "videoId": "dQw4w9WgXcQ",
                                        "title": { "runs": [ { "text": "Never Gonna Give You Up" } ] },
                                        "ownerText": { "runs": [ { "text": "Rick Astley", "navigationEndpoint": {} } ] },
                                        "thumbnail": {
                                          "thumbnails": [
                                            { "url": "https://i.ytimg.com/vi/dQw4w9WgXcQ/default.jpg" },
                                            { "url": "https://i.ytimg.com/vi/dQw4w9WgXcQ/hq720.jpg" }
                                          ]
                                        },
                                        "viewCountText": { "simpleText": "1.6B views" },
                                        "publishedTimeText": { "simpleText": "2 days ago" },
                                        "lengthText": { "simpleText": "3:33" },
                                        "thumbnailOverlays": [
                                          {
                                            "thumbnailOverlayResumePlaybackRenderer": {
                                              "percentDurationWatched": 42
                                            }
                                          }
                                        ]
                                      }
                                    }
                                  }
                                },
                                {
                                  "lockupViewModel": {
                                    "contentId": "abc123def45",
                                    "contentType": "LOCKUP_CONTENT_TYPE_VIDEO",
                                    "contentImage": {
                                      "thumbnailViewModel": {
                                        "image": {
                                          "sources": [
                                            { "url": "https://i.ytimg.com/vi/abc123def45/default.jpg" },
                                            { "url": "https://i.ytimg.com/vi/abc123def45/hq720.jpg" }
                                          ]
                                        },
                                        "overlays": [
                                          {
                                            "thumbnailOverlayBadgeViewModel": {
                                              "thumbnailBadges": [
                                                { "thumbnailBadgeViewModel": { "text": "12:34" } }
                                              ]
                                            }
                                          }
                                        ]
                                      }
                                    },
                                    "metadata": {
                                      "lockupMetadataViewModel": {
                                        "title": { "content": "A Lockup Video" },
                                        "metadata": {
                                          "contentMetadataViewModel": {
                                            "metadataRows": [
                                              {
                                                "metadataParts": [
                                                  { "text": { "content": "Some Channel" } },
                                                  { "text": { "content": "120K views" } },
                                                  { "text": { "content": "3 hours ago" } }
                                                ]
                                              }
                                            ]
                                          }
                                        }
                                      }
                                    },
                                    "rendererContext": {
                                      "commandContext": {
                                        "onTap": {
                                          "innertubeCommand": {
                                            "watchEndpoint": { "videoId": "abc123def45" }
                                          }
                                        }
                                      }
                                    }
                                  }
                                },
                                {
                                  "lockupViewModel": {
                                    "contentId": "PLplaylist123",
                                    "contentType": "LOCKUP_CONTENT_TYPE_PLAYLIST",
                                    "metadata": {
                                      "lockupMetadataViewModel": {
                                        "title": { "content": "A playlist" }
                                      }
                                    }
                                  }
                                }
                              ]
                            }
                          }
                        ],
                        "continuations": [
                          {
                            "nextContinuationData": { "continuation": "next-page-token" }
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
        """;

        [Test]
        public void ParsesVideoRenderersAndLockups()
        {
            WatchHistoryPage page = WatchHistoryParser.Parse(video_renderer_example);

            Assert.That(page.Items, Has.Count.EqualTo(2));

            WatchHistoryItem video = page.Items[0];
            Assert.That(video.VideoId, Is.EqualTo(@"dQw4w9WgXcQ"));
            Assert.That(video.Title, Is.EqualTo(@"Never Gonna Give You Up"));
            Assert.That(video.ChannelTitle, Is.EqualTo(@"Rick Astley"));
            Assert.That(video.ThumbnailUrl, Is.EqualTo(@"https://i.ytimg.com/vi/dQw4w9WgXcQ/hq720.jpg"));
            Assert.That(video.ViewCountText, Is.EqualTo(@"1.6B views"));
            Assert.That(video.PublishedTimeText, Is.EqualTo(@"2 days ago"));
            Assert.That(video.LengthText, Is.EqualTo(@"3:33"));
            Assert.That(video.ResumePercent, Is.EqualTo(42).Within(0.001));
            Assert.That(video.SectionTitle, Is.EqualTo(@"Today"));

            WatchHistoryItem lockup = page.Items[1];
            Assert.That(lockup.VideoId, Is.EqualTo(@"abc123def45"));
            Assert.That(lockup.Title, Is.EqualTo(@"A Lockup Video"));
            Assert.That(lockup.ChannelTitle, Is.EqualTo(@"Some Channel"));
            Assert.That(lockup.ThumbnailUrl, Is.EqualTo(@"https://i.ytimg.com/vi/abc123def45/hq720.jpg"));
            Assert.That(lockup.ViewCountText, Is.EqualTo(@"120K views"));
            Assert.That(lockup.PublishedTimeText, Is.EqualTo(@"3 hours ago"));
            Assert.That(lockup.LengthText, Is.EqualTo(@"12:34"));
            Assert.That(lockup.SectionTitle, Is.EqualTo(@"Today"));
        }

        private const string tv_tile_example = """
        {
          "contents": {
            "tvBrowseRenderer": {
              "content": {
                "tvSurfaceContentRenderer": {
                  "content": {
                    "gridRenderer": {
                      "items": [
                        {
                          "tileRenderer": {
                            "onSelectCommand": { "watchEndpoint": { "videoId": "tvVideo1234" } },
                            "header": {
                              "tileHeaderRenderer": {
                                "thumbnail": {
                                  "thumbnails": [
                                    { "url": "https://i.ytimg.com/vi/tvVideo1234/hq720.jpg" }
                                  ]
                                },
                                "thumbnailOverlays": [
                                  { "thumbnailOverlayTimeStatusRenderer": { "text": { "simpleText": "10:01" } } },
                                  { "thumbnailOverlayResumePlaybackRenderer": { "percentDurationWatched": 77 } }
                                ]
                              }
                            },
                            "metadata": {
                              "tileMetadataRenderer": {
                                "title": { "simpleText": "TV Video" },
                                "lines": [
                                  { "lineRenderer": { "items": [ { "lineItemRenderer": { "text": { "simpleText": "TV Channel" } } } ] } },
                                  { "lineRenderer": { "items": [ { "lineItemRenderer": { "text": { "simpleText": "5M views" } } } ] } },
                                  { "lineRenderer": { "items": [ { "lineItemRenderer": { "text": { "simpleText": "1 week ago" } } } ] } }
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
          }
        }
        """;

        [Test]
        public void ParsesTvTileRenderers()
        {
            WatchHistoryPage page = WatchHistoryParser.Parse(tv_tile_example);

            Assert.That(page.Items, Has.Count.EqualTo(1));

            WatchHistoryItem item = page.Items[0];
            Assert.That(item.VideoId, Is.EqualTo(@"tvVideo1234"));
            Assert.That(item.Title, Is.EqualTo(@"TV Video"));
            Assert.That(item.ChannelTitle, Is.EqualTo(@"TV Channel"));
            Assert.That(item.ThumbnailUrl, Is.EqualTo(@"https://i.ytimg.com/vi/tvVideo1234/hq720.jpg"));
            Assert.That(item.ViewCountText, Is.EqualTo(@"5M views"));
            Assert.That(item.PublishedTimeText, Is.EqualTo(@"1 week ago"));
            Assert.That(item.LengthText, Is.EqualTo(@"10:01"));
            Assert.That(item.ResumePercent, Is.EqualTo(77).Within(0.001));
        }

        [Test]
        public void SkipsPlaylists()
        {
            WatchHistoryPage page = WatchHistoryParser.Parse(video_renderer_example);

            Assert.That(page.Items, Has.None.Matches<WatchHistoryItem>(item => item.VideoId == @"PLplaylist123"));
        }

        [Test]
        public void ReadsTheContinuationToken()
        {
            WatchHistoryPage page = WatchHistoryParser.Parse(video_renderer_example);

            Assert.That(page.Continuation, Is.EqualTo(@"next-page-token"));
        }

        [Test]
        public void DeduplicatesRepeatedVideos()
        {
            const string example = """
            {
              "contents": [
                { "videoRenderer": { "videoId": "same-id", "title": { "simpleText": "First" } } },
                { "videoRenderer": { "videoId": "same-id", "title": { "simpleText": "Second" } } }
              ]
            }
            """;

            WatchHistoryPage page = WatchHistoryParser.Parse(example);

            Assert.That(page.Items, Has.Count.EqualTo(1));
            Assert.That(page.Items[0].Title, Is.EqualTo(@"First"));
        }

        [Test]
        public void ReturnsAnEmptyPageForUnrelatedJson()
        {
            WatchHistoryPage page = WatchHistoryParser.Parse("""{ "contents": {} }""");

            Assert.That(page.Items, Is.Empty);
            Assert.That(page.Continuation, Is.Null);
        }
    }
}

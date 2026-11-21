// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Online;
using Google.Apis.YouTube.v3.Data;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    /// <summary>
    /// Renders the comment rows of the player from the item shape the current watch page returns,
    /// where the text of a comment lives in an entity payload and the replies of a thread sit inside
    /// the comment view model.
    /// </summary>
    [TestFixture]
    public partial class TestSceneCommentDisplay : AXPlayerTestScene
    {
        private const string comments_response = """
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
                  }
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
                        "content": { "content": "The bass line of this song never gets old." },
                        "publishedTime": "2 days ago"
                      },
                      "author": { "channelId": "UCtestchannel0000000000", "displayName": "@listener" },
                      "toolbar": { "replyCount": "2", "likeCountNotliked": "1.2K" }
                    }
                  }
                }
              ]
            }
          }
        }
        """;

        /// <summary>
        /// The first page of the replies of a thread, which returns the comments on their own.
        /// </summary>
        private const string reply_response = """
        {
          "onResponseReceivedEndpoints": [
            {
              "reloadContinuationItemsCommand": {
                "targetId": "comments-section",
                "continuationItems": [
                  {
                    "commentViewModel": { "commentId": "UgxReply1", "commentKey": "Eg0Sreply1" }
                  }
                ]
              }
            }
          ],
          "frameworkUpdates": {
            "entityBatchUpdate": {
              "mutations": [
                {
                  "entityKey": "Eg0Sreply1",
                  "payload": {
                    "commentEntityPayload": {
                      "key": "Eg0Sreply1",
                      "properties": {
                        "commentId": "UgxReply1",
                        "content": { "content": "Agreed, that groove is timeless." },
                        "publishedTime": "1 day ago"
                      },
                      "author": { "channelId": "UCtestchannel1111111111", "displayName": "@replier" },
                      "toolbar": { "likeCountNotliked": "12" }
                    }
                  }
                }
              ]
            }
          }
        }
        """;

        /// <summary>
        /// A thread in the shape the current watch page returns: the view model is nested into another
        /// view model (no <c>comment</c> wrapper), it has no replies renderer, and its entity key is
        /// percent-encoded.
        /// </summary>
        private const string current_thread_response = """
        {
          "onResponseReceivedEndpoints": [
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
                          "inlineRepliesKey": "empty_inline_replies"
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
                  "entityKey": "EhpVZ3dZN2JXbTd6UlpPN0o0UEJWNEFhQUJBZyAoKAE%3D",
                  "payload": {
                    "commentEntityPayload": {
                      "key": "EhpVZ3dZN2JXbTd6UlpPN0o0UEJWNEFhQUJBZyAoKAE%3D",
                      "properties": {
                        "commentId": "UgwY7bWm7zRZO7J4PBV4AaABAg",
                        "content": { "content": "I have no idea how this song is this underrated." },
                        "publishedTime": "1 year ago (edited)"
                      },
                      "author": { "channelId": "UCmigaisnotacat", "displayName": "@migaisnotacat" },
                      "toolbar": { "replyCount": "3", "likeCountNotliked": "1.2K" }
                    }
                  }
                }
              ]
            }
          }
        }
        """;

        /// <summary>
        /// A comment that carries an image or GIF instead of text. The attachment is kept in a
        /// commentSurfaceEntityPayload, which the comment view model references with its surface key.
        /// </summary>
        private const string image_thread_response = """
        {
          "onResponseReceivedEndpoints": [
            {
              "reloadContinuationItemsCommand": {
                "targetId": "comments-section",
                "continuationItems": [
                  {
                    "commentThreadRenderer": {
                      "commentViewModel": {
                        "commentViewModel": {
                          "commentId": "UgxGifComment",
                          "commentKey": "Eg0Sgifcomment%3D",
                          "commentSurfaceKey": "Eg1Sgifsurface%3D",
                          "toolbarStateKey": "Eg0Stoolbar%3D"
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
                  "entityKey": "Eg0Sgifcomment=",
                  "payload": {
                    "commentEntityPayload": {
                      "key": "Eg0Sgifcomment=",
                      "properties": {
                        "commentId": "UgxGifComment",
                        "content": { "content": "" },
                        "publishedTime": "2 days ago"
                      },
                      "author": { "channelId": "UCgifposter", "displayName": "@gifposter" },
                      "toolbar": { "likeCountNotliked": "42" }
                    }
                  }
                },
                {
                  "entityKey": "Eg1Sgifsurface=",
                  "payload": {
                    "commentSurfaceEntityPayload": {
                      "key": "Eg1Sgifsurface=",
                      "imageAttachmentViewModel": {
                        "commentImageAttachmentViewModel": {
                          "width": 498,
                          "height": 374,
                          "aspectRatio": 1.3315508,
                          "image": {
                            "sources": [
                              { "url": "https://example.com/comment-image.webp", "width": 498, "height": 374 }
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

        private FillFlowContainer commentRows = null!;

        private CommentThread thread = null!;

        public TestSceneCommentDisplay()
        {
            Add(commentRows = new FillFlowContainer
            {
                Width = 500,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            });
        }

        [Test]
        public void TestThreadWithItsReplies()
        {
            AddStep("show the parsed thread and its reply", () =>
            {
                commentRows.Clear();

                thread = parseThread(comments_response, "REPLY-TOKEN");
                Comment? reply = parseFirstComment(reply_response, thread.Id);

                commentRows.Add(new CommentDisplay(thread));

                if (reply != null)
                    commentRows.Add(new CommentDisplay(thread, reply));
            });

            AddAssert("the thread keeps the amount of replies", () => thread.Snippet.TotalReplyCount, () => Is.EqualTo(2));
            AddAssert("the reply is a reply of the thread", () => commentRows.Count, () => Is.EqualTo(2));
        }

        [Test]
        public void TestCommentWithoutItsEntityPayloadIsRendered()
        {
            // A comment whose entity payload is missing arrives without text, date, author channel and
            // counters, which used to throw while the row was filled in.
            AddStep("show a comment that arrived without its entity payload", () =>
            {
                commentRows.Clear();

                var thread = new CommentThread
                {
                    Id = "UgxWithoutPayload",
                    Snippet = new CommentThreadSnippet
                    {
                        VideoId = "dQw4w9WgXcQ",
                        TopLevelComment = new Comment
                        {
                            Id = "UgxWithoutPayload",
                            Snippet = new CommentSnippet
                            {
                                VideoId = "dQw4w9WgXcQ",
                                AuthorDisplayName = "@unknown",
                            },
                        },
                    },
                };

                commentRows.Add(new CommentDisplay(thread));
            });

            AddAssert("the row is shown", () => commentRows.Count, () => Is.EqualTo(1));
        }

        [Test]
        public void TestThreadOfTheCurrentWatchPage()
        {
            CommentThread current = null!;

            AddStep("show the thread of the current watch page", () =>
            {
                commentRows.Clear();

                // The current page has no replies renderer on the thread, so no replies are fetched inline.
                current = parseThread(current_thread_response, null);
                commentRows.Add(new CommentDisplay(current));
            });

            AddAssert("the row shows the thread", () => commentRows.Count, () => Is.EqualTo(1));
            AddAssert("the text of the comment is read from the entity payload",
                () => current.Snippet.TopLevelComment.Snippet.TextDisplay, () => Is.EqualTo("I have no idea how this song is this underrated."));
            AddAssert("the thread keeps the amount of replies", () => current.Snippet.TotalReplyCount, () => Is.EqualTo(3));
        }

        [Test]
        public void TestCommentWithAnImageAttachment()
        {
            CommentThread imageThread = null!;

            AddStep("show the comment which has only an image attachment", () =>
            {
                commentRows.Clear();

                imageThread = parseThread(image_thread_response, null);
                commentRows.Add(new CommentDisplay(imageThread));
            });

            AddAssert("the image attachment is parsed",
                () => imageThread.Snippet.TopLevelComment is AXPlayerComment { ImageAttachment: not null });
            AddUntilStep("the image attachment is rendered",
                () => commentRows.ChildrenOfType<CommentImageAttachmentDisplay>().Any(), () => Is.True);
            AddAssert("the image is rendered at half the attachment size",
                () => commentRows.ChildrenOfType<CommentImageAttachmentDisplay>().Single().Size.X, () => Is.EqualTo(180).Within(1));
            AddAssert("the image keeps its aspect ratio",
                () => commentRows.ChildrenOfType<CommentImageAttachmentDisplay>().Single().Size.Y, () => Is.EqualTo(135).Within(1));
        }

        private static CommentThread parseThread(string json, string? expectedRepliesToken)
        {
            JsonElement root = parseRoot(json);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(root);
            List<JsonElement> items = YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(root));

            CommentThread thread = YouTubeAPI.parseCommentThread(items[0], @"dQw4w9WgXcQ", entities)!;

            Assert.That(YouTubeAPI.findRepliesContinuation(items[0]), Is.EqualTo(expectedRepliesToken));

            return thread;
        }

        private static Comment? parseFirstComment(string json, string? parentId)
        {
            JsonElement root = parseRoot(json);
            Dictionary<string, JsonElement> entities = YouTubeAPI.extractCommentEntities(root);

            foreach (JsonElement item in YouTubeAPI.detachItems(YouTubeAPI.extractContinuationItems(root)))
            {
                JsonElement? element = YouTubeAPI.tryGetCommentElement(item);

                if (element == null)
                    continue;

                Comment? comment = YouTubeAPI.parseComment(element.Value, null, parentId, YouTubeAPI.findCommentEntity(element.Value, entities));

                if (comment != null)
                    return comment;
            }

            return null;
        }

        private static JsonElement parseRoot(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            return document.RootElement.Clone();
        }
    }
}

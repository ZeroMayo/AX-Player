// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading.Tasks;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterface.LiveChat;
using AXPlayer.App.Online;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osuTK;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneLiveChatReplayOverlay : AXPlayerTestScene
    {
        private LiveChatReplayOverlay overlay;
        private Task settleTask;

        private const string sample_replay_json = """
        [
          {
            "replayChatItemAction": {
              "actions": [
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatViewerEngagementMessageRenderer": {
                        "id": "engagement-1",
                        "timestampUsec": "1794564231076533",
                        "icon": { "iconType": "YOUTUBE_ROUND" },
                        "message": {
                          "runs": [
                            { "text": "Live chat replay is on. Messages that appeared during the Premiere will show up here." }
                          ]
                        }
                      }
                    }
                  }
                }
              ],
              "videoOffsetTimeMsec": "0"
            }
          },
          {
            "replayChatItemAction": {
              "actions": [
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatTextMessageRenderer": {
                        "id": "text-1",
                        "timestampUsec": "1794564235076533",
                        "authorName": { "simpleText": "Retsi" },
                        "authorExternalChannelId": "UC0000000000000000000000",
                        "authorPhoto": {
                          "thumbnails": [
                            { "url": "https://example.com/avatar.png", "width": 32, "height": 32 }
                          ]
                        },
                        "message": {
                          "runs": [
                            { "text": "Hello " },
                            { "emoji": { "emojiId": "wave-emoji", "shortcuts": [":wave:"], "isCustomEmoji": true, "image": { "thumbnails": [ { "url": "https://example.com/emoji.png", "width": 32, "height": 32 } ] } } },
                            { "text": " chat!" }
                          ]
                        },
                        "authorBadges": [
                          { "liveChatAuthorBadgeRenderer": { "tooltip": "Moderator" } }
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
                        "timestampUsec": "1794564240076533",
                        "authorName": { "simpleText": "Boombox" },
                        "message": { "runs": [ { "text": "Great stream!" } ] },
                        "purchaseAmountText": { "simpleText": "$5.00" },
                        "authorBadges": [
                          { "liveChatAuthorBadgeRenderer": { "tooltip": "Owner" } }
                        ]
                      }
                    }
                  }
                }
              ],
              "videoOffsetTimeMsec": "15000"
            }
          },
          {
            "replayChatItemAction": {
              "actions": [
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatMembershipItemRenderer": {
                        "id": "membership-1",
                        "timestampUsec": "1794564250076533",
                        "authorName": { "simpleText": "ZeroMayo" },
                        "headerSubtext": { "runs": [ { "text": "Member (1 year)" } ] },
                        "message": { "runs": [ { "text": "Welcome to the members!" } ] }
                      }
                    }
                  }
                }
              ],
              "videoOffsetTimeMsec": "30000"
            }
          },
          {
            "replayChatItemAction": {
              "actions": [
                {
                  "addChatItemAction": {
                    "item": {
                      "liveChatPaidStickerRenderer": {
                        "id": "sticker-1",
                        "timestampUsec": "1794564260076533",
                        "authorName": { "simpleText": "speaki" },
                        "purchaseAmountText": { "simpleText": "$2.00" }
                      }
                    }
                  }
                }
              ],
              "videoOffsetTimeMsec": "60000"
            }
          }
        ]
        """;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay", () =>
            {
                Child = overlay = new LiveChatReplayOverlay();
                overlay.Show();
            });

            AddStep("load sample replay", () => overlay.SetMessages(LiveChatReplayParser.Parse(sample_replay_json)));

            AddAssert("only the first message is revealed", () => displayedMessageCount == 1);
        }

        [Test]
        public void RevealMessagesWithPlayback()
        {
            AddStep("seek to 15 seconds", () => overlay.PlaybackPosition.Value = 15);
            AddAssert("three messages are revealed", () => displayedMessageCount == 3);

            AddStep("seek to the end", () => overlay.PlaybackPosition.Value = 60);
            AddAssert("all messages are revealed", () => displayedMessageCount == 5);

            AddStep("seek back to the start", () => overlay.PlaybackPosition.Value = 0);
            AddAssert("messages after the start are hidden again", () => displayedMessageCount == 1);
        }

        [Test]
        public void RendersPictureEmoji()
        {
            AddStep("seek to the text message", () => overlay.PlaybackPosition.Value = 5);
            AddAssert("the picture emoji is rendered as an image", () => overlay.ChildrenOfType<LiveChatEmojiSprite>().Any());
        }

        [Test]
        public void ShowUnavailableState()
        {
            AddStep("show unavailable", () => overlay.ShowUnavailable());
            AddAssert("no messages are displayed", () => displayedMessageCount == 0);
        }

        [Test]
        public void ShowLoadingThenClear()
        {
            AddStep("show loading", () => overlay.SetLoading());
            AddStep("clear messages", () => overlay.ClearMessages());
            AddAssert("no messages are displayed", () => displayedMessageCount == 0);
        }

        [Test]
        public void CompactModeRendersMessages()
        {
            AddStep("create compact overlay", () =>
            {
                Child = new Container
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Size = new Vector2(320, 300),
                    Child = overlay = new LiveChatReplayOverlay(true),
                };

                overlay.Show();
                overlay.SetMessages(LiveChatReplayParser.Parse(sample_replay_json));
                overlay.PlaybackPosition.Value = 60;
            });

            AddAssert("all messages are rendered", () => displayedMessageCount == 5);
        }

        [Test]
        public void CompactModeOnlyRendersARecentWindow()
        {
            AddStep("create compact overlay", () =>
            {
                Child = new Container
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Size = new Vector2(320, 300),
                    Child = overlay = new LiveChatReplayOverlay(true),
                };

                overlay.Show();
                overlay.SetMessages(createMessages(120, "bulk-window"));
            });

            AddStep("seek to the end", () => overlay.PlaybackPosition.Value = 60);
            AddUntilStep("only the recent compact window is rendered", () => displayedMessageCount == 25);
        }

        [Test]
        public void KeepsScrolledToTheBottomWhileMessagesArrive()
        {
            AddStep("load many messages", () => overlay.SetMessages(createMessages(120)));

            AddStep("seek near the end", () => overlay.PlaybackPosition.Value = 59.5);
            AddAssert("only a recent window is rendered", () => displayedMessageCount < 120);
            AddUntilStep("scrolled down to the newest message", () => scrollContainer.Current > 0);

            AddStep("reveal one more message", () => overlay.PlaybackPosition.Value = 60);
            AddAssert("still scrolled down instead of jumping to the top", () => scrollContainer.Current > 0);
        }

        [Test]
        public void DoesNotForceScrollWhenTheUserScrolledUp()
        {
            AddStep("load many messages", () => overlay.SetMessages(createMessages(120)));

            AddStep("seek to the end", () => overlay.PlaybackPosition.Value = 60);
            AddUntilStep("scrolled down to the newest message", () => scrollContainer.Current > 0);

            AddStep("scroll back to the top", () => scrollContainer.ScrollToStart(false));
            AddStep("reveal another message", () => overlay.PlaybackPosition.Value = 60.5);
            AddAssert("stays at the top", () => scrollContainer.Current == 0);
        }

        [Test]
        public void RenderingALongReplayContinuesAcrossFrames()
        {
            Task load = null;
            int renderedImmediately = 0;

            AddStep("seek near the end", () => overlay.PlaybackPosition.Value = 60);
            AddStep("load a long replay", () =>
            {
                load = overlay.SetMessages(createMessages(120));
                renderedImmediately = displayedMessageCount;
            });

            AddAssert("only the first batch is rendered in the calling frame", () => renderedImmediately is > 0 and < 60);
            AddUntilStep("the whole window is rendered", () => displayedMessageCount == 60);
            AddAssert("the returned task completes once the window is rendered", () => load.IsCompletedSuccessfully);
        }

        [Test]
        public void LoadingMessagesSettlesAsynchronously()
        {
            AddStep("load sample replay", () => { settleTask = overlay.SetMessages(LiveChatReplayParser.Parse(sample_replay_json)); });
            AddAssert("messages are rendered without waiting for the task", () => displayedMessageCount == 1);
            AddUntilStep("the returned task completes", () => settleTask.IsCompletedSuccessfully);
            AddAssert("the rendered messages are kept", () => displayedMessageCount == 1);
        }

        [Test]
        public void ClearingMessagesSettlesOnTheNextFrame()
        {
            AddStep("clear messages", () => settleTask = overlay.ClearMessages());
            AddAssert("no messages are displayed", () => displayedMessageCount == 0);
            AddUntilStep("the returned task completes", () => settleTask.IsCompletedSuccessfully);
        }

        [Test]
        public void LoadingAndUnavailableStatesSettle()
        {
            AddStep("show loading", () => settleTask = overlay.SetLoading());
            AddUntilStep("the loading task completes", () => settleTask.IsCompletedSuccessfully);

            AddStep("show unavailable", () => settleTask = overlay.ShowUnavailable());
            AddUntilStep("the unavailable task completes", () => settleTask.IsCompletedSuccessfully);

            AddAssert("no messages are displayed", () => displayedMessageCount == 0);
        }

        private static LiveChatReplayMessage[] createMessages(int count, string idPrefix = "bulk") => Enumerable.Range(0, count)
                                                                                                             .Select(i => new LiveChatReplayMessage
                                                                                                             {
                                                                                                                 Id = $"{idPrefix}-{i}",
                                                                                                                 Kind = LiveChatReplayMessageKind.Text,
                                                                                                                 OffsetTimeMsec = i * 500,
                                                                                                                 AuthorName = $"Author {i}",
                                                                                                                 Text = $"Message {i}",
                                                                                                             }).ToArray();

        private ProjectYomiScrollContainer scrollContainer => overlay.ChildrenOfType<ProjectYomiScrollContainer>().Single();

        private int displayedMessageCount => overlay.ChildrenOfType<LiveChatMessageDisplay>().Count();
    }
}

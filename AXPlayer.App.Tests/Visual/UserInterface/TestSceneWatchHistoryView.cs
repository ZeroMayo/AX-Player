// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Online;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneWatchHistoryView : AXPlayerTestScene
    {
        private WatchHistoryView historyView;

        public TestSceneWatchHistoryView()
        {
            Add(historyView = new WatchHistoryView
            {
                Width = 400,
                Enabled = { Value = true },
            });
        }

        [Test]
        public void TestWatchedVideo()
        {
            AddStep("show partially watched video", () =>
            {
                historyView.Data = new WatchHistoryItem
                {
                    VideoId = @"dQw4w9WgXcQ",
                    Title = @"Never Gonna Give You Up",
                    ChannelTitle = @"Rick Astley",
                    ViewCountText = @"1.6B views",
                    PublishedTimeText = @"2 days ago",
                    LengthText = @"3:33",
                    ResumePercent = 42,
                };

                historyView.UpdateData();
            });
        }

        [Test]
        public void TestUnwatchedVideo()
        {
            AddStep("show unwatched video", () =>
            {
                historyView.Data = new WatchHistoryItem
                {
                    VideoId = @"abc123def45",
                    Title = @"A very long video title that has to be truncated when it does not fit",
                    ChannelTitle = @"Some Channel",
                    ViewCountText = @"120K views",
                    PublishedTimeText = @"3 hours ago",
                };

                historyView.UpdateData();
            });
        }
    }
}

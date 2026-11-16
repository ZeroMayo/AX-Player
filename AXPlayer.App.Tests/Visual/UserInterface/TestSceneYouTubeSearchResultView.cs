// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Localisation;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneYouTubeSearchResultView : AXPlayerTestScene
    {
        private YouTubeSearchResultView videoMetadataDisplay = null!;
        private SpriteText metadataText = null!;

        public TestSceneYouTubeSearchResultView()
        {
            Add(videoMetadataDisplay = new YouTubeSearchResultView { Width = 400 });
            Add(metadataText = new SpriteText { Y = 200 });
        }

        [Test]
        public void TestAResultWithoutAPublishDateStillRenders()
        {
            // A search result that arrives without its publish date used to throw while its metadata
            // line was filled in, which ended the game.
            AddStep("show the metadata line without a date", () =>
            {
                metadataText.Text = AXPlayerStrings.VideoMetadataDescWithoutChannelName(
                    ((ulong?)15_000_000_000).toFormattedCount(),
                    ((DateTimeOffset?)null).toRelativeDate());
            });

            AddAssert("the counter is shown", () => metadataText.Text.ToString(), () => Does.Contain("15"));
            AddAssert("the line has no relative date", () => metadataText.Text.ToString().Trim().Length, () => Is.LessThan(30));
        }
    }
}

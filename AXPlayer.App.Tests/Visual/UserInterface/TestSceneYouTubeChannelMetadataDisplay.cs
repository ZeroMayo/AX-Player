// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using AXPlayer.App.Graphics.UserInterface;
using Google.Apis.YouTube.v3.Data;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneYouTubeChannelMetadataDisplay : AXPlayerTestScene
    {
        private YouTubeChannelMetadataDisplay display = null!;
        private YouTubeChannelMetadataDisplay secondDisplay = null!;
        private int backgroundColorChanges;
        private int secondBackgroundColorChanges;

        public TestSceneYouTubeChannelMetadataDisplay()
        {
            Add(display = new YouTubeChannelMetadataDisplay
            {
                Width = 400,
                Height = 60,
                OnBGColourChanged = _ => backgroundColorChanges++,
            });
        }

        /// <summary>
        /// A channel whose thumbnail can never be downloaded, which exercises the fallback colour path.
        /// </summary>
        private static Channel createUnavailableChannel() => new Channel
        {
            Id = @"UCuAXFkgsw1L7xaCfnd5JJOw",
            Snippet = new ChannelSnippet
            {
                Title = @"A channel with a name that is far too long to be shown in full",
                CustomUrl = @"@testchannel",
                Thumbnails = new ThumbnailDetails
                {
                    High = new Thumbnail { Url = @"http://127.0.0.1:1/thumbnail.png" },
                },
            },
            Statistics = new ChannelStatistics
            {
                SubscriberCount = 1_234_567,
            },
        };

        [Test]
        public void TestLongTitlesAreTruncated()
        {
            AddAssert("short titles are untouched", () => display.TruncateWithEllipsis(@"short title", 20), () => Is.EqualTo(@"short title"));
            AddAssert("long titles keep an ellipsis", () => display.TruncateWithEllipsis(new string('a', 40), 20), () => Is.EqualTo(new string('a', 17) + "..."));
            AddAssert("empty titles stay empty", () => display.TruncateWithEllipsis(string.Empty, 20), () => Is.Empty);
        }

        [Test]
        public void TestUnavailableThumbnailFallsBackToThemeColours()
        {
            AddStep("show the channel", () => display.UpdateUser(createUnavailableChannel()));

            AddUntilStep("a background colour was applied", () => backgroundColorChanges, () => Is.GreaterThan(0));
            AddAssert("the display has a background colour", () => display.GetBGColour(), () => Is.Not.EqualTo(default(ColourInfo)));
        }

        [Test]
        public void TestSameChannelShownTwiceAtOnce()
        {
            // The same channel can be shown by a metadata display and a tooltip at the same time. Both used to
            // download and write the same thumbnail cache file, which could throw while the other was writing it.
            AddStep("add a second display for the same channel", () => Add(secondDisplay = new YouTubeChannelMetadataDisplay
            {
                Width = 400,
                Height = 60,
                Y = 70,
                OnBGColourChanged = _ => secondBackgroundColorChanges++,
            }));

            AddStep("show the same channel in both displays", () =>
            {
                Channel channel = createUnavailableChannel();

                display.UpdateUser(channel);
                secondDisplay.UpdateUser(channel);
            });

            AddUntilStep("both displays got a background colour", () => backgroundColorChanges > 0 && secondBackgroundColorChanges > 0);
        }
    }
}

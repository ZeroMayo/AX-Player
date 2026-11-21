// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Utils;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Visual.Utils
{
    [TestFixture]
    public partial class TestSceneDiscordPresenceContent : AXPlayerTestScene
    {
        [Test]
        public void TestTruncationStaysWithinLimit()
        {
            AddAssert("short text is untouched", () => DiscordPresenceContent.TruncateWithEllipsis("abc", DiscordPresenceContent.MaximumLength) == "abc");
            AddAssert("empty text stays empty", () => DiscordPresenceContent.TruncateWithEllipsis(null, DiscordPresenceContent.MaximumLength) == string.Empty);
            AddAssert("long text is clipped", () => DiscordPresenceContent.TruncateWithEllipsis(new string('a', 200), 10) == new string('a', 7) + "...");
        }

        [Test]
        public void TestDetailsKeepSpeedSuffixWithinLimit()
        {
            AddAssert("normal speed is not annotated", () => DiscordPresenceContent.FormatDetails("title", 1) == "title");
            AddAssert("playback speed is annotated", () => DiscordPresenceContent.FormatDetails("title", 2) == "title (2x speed)");

            AddAssert("long titles still show the speed", () =>
            {
                string details = DiscordPresenceContent.FormatDetails(new string('a', 200), 2);

                return details.Length == DiscordPresenceContent.MaximumLength && details.EndsWith(" (2x speed)", StringComparison.Ordinal);
            });
        }

        [Test]
        public void TestTimestampsFollowPlaybackSpeed()
        {
            DateTime capturedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            AddAssert("timestamps account for the speed", () =>
            {
                bool resolved = DiscordPresenceContent.TryResolveTimestamps(30, 120, 2, capturedAt, out DateTime start, out DateTime end);

                return resolved
                       && start == capturedAt.AddSeconds(-15)
                       && end == capturedAt.AddSeconds(45);
            });

            AddAssert("progress is clamped to the duration", () =>
            {
                bool resolved = DiscordPresenceContent.TryResolveTimestamps(150, 120, 1, capturedAt, out DateTime start, out DateTime end);

                return resolved
                       && start == capturedAt.AddSeconds(-150)
                       && end == capturedAt;
            });

            AddAssert("unknown duration has no timestamps", () => !DiscordPresenceContent.TryResolveTimestamps(0, 0, 1, capturedAt, out _, out _));
        }

        [Test]
        public void TestDurationParsing()
        {
            AddAssert("iso duration is parsed", () => DiscordPresenceContent.TryParseDurationSeconds("PT1M30S", out double seconds) && seconds == 90);
            AddAssert("live stream has no duration", () => !DiscordPresenceContent.TryParseDurationSeconds("P0D", out _));
            AddAssert("invalid duration is rejected", () => !DiscordPresenceContent.TryParseDurationSeconds("not a duration", out _));
            AddAssert("missing duration is rejected", () => !DiscordPresenceContent.TryParseDurationSeconds(null, out _));
        }
    }
}

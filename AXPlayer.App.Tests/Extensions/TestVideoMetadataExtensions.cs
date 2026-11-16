// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using AXPlayer.App.Extensions;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Extensions
{
    /// <summary>
    /// The parts of a video metadata line that a response is not required to carry.
    /// </summary>
    [TestFixture]
    public class TestVideoMetadataExtensions
    {
        [Test]
        public void TestMissingPublishDateKeepsTheTextEmpty()
        {
            // A search result or a playlist entry may arrive without a publish date. Reading it used to
            // throw on the update thread, where the exception ends the game.
            Assert.That(((DateTimeOffset?)null).toRelativeDate(), Is.Empty);
        }

        [Test]
        public void TestPublishDateIsRenderedRelativeToNow()
        {
            DateTime now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

            Assert.That(((DateTimeOffset?)new DateTimeOffset(2026, 11, 13, 12, 0, 0, TimeSpan.Zero)).toRelativeDate(now), Is.Not.Empty);
        }

        [Test]
        public void TestMissingCounterIsRenderedAsZero()
        {
            Assert.That(((ulong?)null).toFormattedCount(), Is.EqualTo(((ulong?)0).toFormattedCount()));
            Assert.That(((long?)null).toFormattedCount(), Is.EqualTo(((long?)0).toFormattedCount()));
        }

        [Test]
        public void TestLargeCountersKeepTheirWidth()
        {
            // The most watched videos have more views than an int can hold.
            Assert.That(((ulong?)ulong.MaxValue).toFormattedCount(), Is.Not.Empty);
            Assert.That(((ulong?)15_000_000_000UL).toFormattedCount(), Is.EqualTo(((long?)15_000_000_000L).toFormattedCount()));
        }
    }
}

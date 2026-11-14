// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Utils;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Visual.Utils
{
    [TestFixture]
    public partial class TestScenePlaybackSpeedDisplay : AXPlayerTestScene
    {
        [Test]
        public void TestActiveSourceSpeedIsPreferred()
        {
            AddAssert("active source speed is used", () => PlaybackSpeedDisplay.ResolveSpeed(1.5, 2) == 2);
            AddAssert("active source speed is formatted", () => PlaybackSpeedDisplay.Format(2) == "200%");
        }

        [Test]
        public void TestFallsBackToConfiguredSpeedWithoutActiveSource()
        {
            AddAssert("configured speed is used when the source is disposed", () => PlaybackSpeedDisplay.ResolveSpeed(1.25, null) == 1.25);
            AddAssert("configured speed is formatted", () => PlaybackSpeedDisplay.Format(1.25) == "125%");
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Platform;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Visual.Platform
{
    [TestFixture]
    public partial class TestScenePlatformBridge : AXPlayerTestScene
    {
        [Test]
        public void TestHostHandlerIsUsed()
        {
            string openedUrl = null;

            AddStep("install handler", () => PlatformBridge.OpenUrlHandler = url =>
            {
                openedUrl = url;
                return true;
            });

            AddAssert("handler receives the url", () => PlatformBridge.OpenUrl("https://example.com") && openedUrl == "https://example.com");
            AddStep("reset handler", () => PlatformBridge.OpenUrlHandler = null);
        }

        [Test]
        public void TestHostHandlerFailureIsReported()
        {
            AddStep("install failing handler", () => PlatformBridge.OpenUrlHandler = _ => false);

            AddAssert("failure is reported", () => !PlatformBridge.OpenUrl("https://example.com"));
            AddStep("reset handler", () => PlatformBridge.OpenUrlHandler = null);
        }

        [Test]
        public void TestThrowingHandlerDoesNotCrash()
        {
            AddStep("install throwing handler", () => PlatformBridge.OpenUrlHandler = _ => throw new InvalidOperationException("no browser"));

            AddAssert("throwing handler reports failure", () => !PlatformBridge.OpenUrl("https://example.com"));
            AddStep("reset handler", () => PlatformBridge.OpenUrlHandler = null);
        }
    }
}

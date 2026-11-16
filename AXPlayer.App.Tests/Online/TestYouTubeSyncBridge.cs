// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Online;
using NUnit.Framework;

namespace AXPlayer.App.Tests.Online
{
    /// <summary>
    /// Regression coverage for the synchronous bridges that the player uses to call the
    /// asynchronous API from the update thread.
    /// </summary>
    [TestFixture]
    public class TestYouTubeSyncBridge
    {
        [Test]
        public void TestSyncBridgeDoesNotDeadlockOnACallerMessageLoop()
        {
            var context = new BlockingSynchronizationContext();
            SynchronizationContext? previous = SynchronizationContext.Current;

            SynchronizationContext.SetSynchronizationContext(context);

            try
            {
                int result = YouTubeAPI.runSync(async () =>
                {
                    // A bare yield captures the ambient context. If the bridge ran the factory on
                    // this blocked thread, the continuation would be posted to it and never run.
                    await Task.Yield();
                    return 42;
                });

                Assert.That(result, Is.EqualTo(42));
                Assert.That(context.PostedCallbacks, Is.Empty);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        [Test]
        public void TestSyncBridgePropagatesExceptions()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => YouTubeAPI.runSync<int>(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            }));

            Assert.That(exception!.Message, Is.EqualTo("boom"));
        }

        /// <summary>
        /// Mimics the update thread: posted callbacks are queued and never pumped while the
        /// thread is blocked waiting for the synchronous bridge.
        /// </summary>
        private class BlockingSynchronizationContext : SynchronizationContext
        {
            public List<SendOrPostCallback> PostedCallbacks { get; } = new List<SendOrPostCallback>();

            public override void Post(SendOrPostCallback d, object? state) => PostedCallbacks.Add(d);
        }
    }
}

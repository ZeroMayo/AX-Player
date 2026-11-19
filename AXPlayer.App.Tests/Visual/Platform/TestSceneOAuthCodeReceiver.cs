// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Online;
using NUnit.Framework;
using osu.Framework.Extensions;

namespace AXPlayer.App.Tests.Visual.Platform
{
    /// <summary>
    /// Covers the redirect receiver seam the Android host replaces with its intent-based receiver.
    /// </summary>
    [TestFixture]
    public partial class TestSceneOAuthCodeReceiver : AXPlayerTestScene
    {
        [Test]
        public void TestLoopbackReceiverRedirectUri()
        {
            LoopbackCodeReceiver receiver = null;

            AddStep("start receiver", () => receiver = LoopbackCodeReceiver.Start());

            AddAssert("redirect uri points at the loopback listener", () => receiver.RedirectUri.StartsWith(@"http://127.0.0.1:") && receiver.RedirectUri.EndsWith(@"/authorize/"));

            AddStep("dispose receiver", () => receiver.Dispose());
        }

        [Test]
        public void TestLoopbackReceiverIsCancellable()
        {
            LoopbackCodeReceiver receiver = null;
            Task<Dictionary<string, string>> callback = null;

            AddStep("start receiver", () => receiver = LoopbackCodeReceiver.Start());

            AddStep("cancel the wait", () =>
            {
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    callback = receiver.WaitForCallbackAsync(cancellation.Token);
                }
            });

            AddUntilStep("wait is cancelled", () => callback.IsCanceled);

            AddStep("dispose receiver", () => receiver.Dispose());
        }

        [Test]
        public void TestClientIsResolvableByTheScreens()
        {
            // Regression guard: the platform clients are subclasses, and DependencyContainer.Cache()
            // registers the runtime type, so the app has to cache the client as GoogleOAuth2.
            OAuthClientProbe probe = null;

            AddStep("add a drawable that resolves the client", () => Add(probe = new OAuthClientProbe()));

            AddAssert("client is resolved", () => probe.Resolved != null);
        }

        [Test]
        public void TestCustomSchemeReceiverTakesTheDeliveredRedirect()
        {
            const string redirect_uri = @"com.googleusercontent.apps.1234567890-abcdefgh:/oauth2redirect";

            CustomSchemeCodeReceiver receiver = null;
            Task<Dictionary<string, string>> callback = null;

            AddStep("create receiver", () => receiver = new CustomSchemeCodeReceiver(redirect_uri));

            AddAssert("redirect uri is the custom scheme", () => receiver.RedirectUri == redirect_uri);

            AddStep("wait for the redirect", () => callback = receiver.WaitForCallbackAsync(CancellationToken.None));

            AddStep("deliver the redirect", () => CustomSchemeCodeReceiver.TryDeliver(new System.Uri($@"{redirect_uri}?code=4/abc&state=xyz")));

            AddUntilStep("callback arrives", () => callback.IsCompleted);

            AddAssert("query parameters are parsed", () => callback.GetResultSafely()[@"code"] == @"4/abc");

            AddAssert("a redirect cannot be delivered twice", () => !CustomSchemeCodeReceiver.TryDeliver(new System.Uri(redirect_uri)));

            AddStep("dispose receiver", () => receiver.Dispose());
        }

        [Test]
        public void TestAndroidRedirectSharesTheCallbackParsing()
        {
            // The Android host receives this URI from its intent filter instead of a loopback request.
            var redirect = new System.Uri(@"com.googleusercontent.apps.1234567890-abcdefgh:/oauth2redirect?code=4/abc&state=xyz");

            AddAssert("query parameters are parsed", () =>
            {
                Dictionary<string, string> query = OAuthCallbackQuery.Parse(redirect.Query);

                return query[@"code"] == @"4/abc" && query[@"state"] == @"xyz";
            });
        }

        private partial class OAuthClientProbe : osu.Framework.Graphics.Drawable
        {
            [osu.Framework.Allocation.Resolved]
            private GoogleOAuth2 oauth { get; set; }

            public GoogleOAuth2 Resolved => oauth;
        }
    }
}

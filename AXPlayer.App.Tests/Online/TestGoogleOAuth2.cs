// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Online;
using NUnit.Framework;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Online
{
    /// <summary>
    /// Covers the parts of the Innertube-era OAuth client that do not require network access.
    /// </summary>
    [TestFixture]
    public class TestGoogleOAuth2
    {
        private TemporaryNativeStorage storage = null!;
        private AXPlayerConfigManager config = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage(@"google-oauth2-test");
            config = new AXPlayerConfigManager(storage);
        }

        [TearDown]
        public void TearDown()
        {
            storage.Dispose();
        }

        [Test]
        public void TestGetAccessTokenReturnsEmptyWhenSignedOut()
        {
            var oauth = new GoogleOAuth2(config, true);

            Assert.That(oauth.GetAccessToken(), Is.Empty);
            Assert.That(oauth.GetAccessTokenAsync().GetAwaiter().GetResult(), Is.Empty);
        }

        [Test]
        public void TestHasStoredSessionReflectsTheRefreshToken()
        {
            var oauth = new GoogleOAuth2(config, true);

            Assert.That(oauth.HasStoredSession, Is.False);

            config.SetValue(AXPlayerSetting.RefreshToken, @"refresh-token");

            Assert.That(oauth.HasStoredSession, Is.True);
        }

        [Test]
        public async Task TestRestoreSessionWithoutStoredTokenStaysSignedOut()
        {
            var oauth = new GoogleOAuth2(config, true);

            await oauth.RestoreSessionAsync();

            Assert.That(oauth.SignedIn.Value, Is.False);
        }

        [Test]
        public async Task TestSignOutClearsPersistedTokens()
        {
            config.SetValue(AXPlayerSetting.AccessToken, @"access-token");
            config.SetValue(AXPlayerSetting.RefreshToken, @"refresh-token");
            config.SetValue(AXPlayerSetting.AccessTokenExpiresAt, DateTimeOffset.UtcNow.ToString(@"o"));

            var oauth = new GoogleOAuth2(config, true);

            await oauth.SignOut();

            Assert.That(oauth.SignedIn.Value, Is.False);
            Assert.That(config.Get<string>(AXPlayerSetting.AccessToken), Is.Empty);
            Assert.That(config.Get<string>(AXPlayerSetting.RefreshToken), Is.Empty);
            Assert.That(config.Get<string>(AXPlayerSetting.AccessTokenExpiresAt), Is.Empty);
        }

        [Test]
        public void TestCodeChallengeMatchesTheRfcVector()
        {
            // RFC 7636, appendix B.
            Assert.That(
                GoogleOAuth2.createCodeChallenge(@"dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"),
                Is.EqualTo(@"E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM"));
        }

        [Test]
        public void TestCodeChallengeIsUrlSafe()
        {
            string challenge = GoogleOAuth2.createCodeChallenge(@"some-verifier-value");

            Assert.That(challenge, Does.Not.Contain(@"+"));
            Assert.That(challenge, Does.Not.Contain(@"/"));
            Assert.That(challenge, Does.Not.Contain(@"="));
        }

        [Test]
        public void TestExpiryIsParsed()
        {
            var expiry = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

            Assert.That(GoogleOAuth2.parseExpiry(expiry.ToString(@"o")), Is.EqualTo(expiry));
            Assert.That(GoogleOAuth2.parseExpiry(string.Empty), Is.EqualTo(default(DateTimeOffset)));
            Assert.That(GoogleOAuth2.parseExpiry(null), Is.EqualTo(default(DateTimeOffset)));
        }

        [Test]
        public void TestCallbackQueryWithoutQuestionMarkIsParsed()
        {
            Dictionary<string, string> query = OAuthCallbackQuery.Parse(@"code=4/abc&state=xyz&scope=https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fyoutube.force-ssl");

            Assert.That(query[@"code"], Is.EqualTo(@"4/abc"));
            Assert.That(query[@"state"], Is.EqualTo(@"xyz"));
            Assert.That(query[@"scope"], Is.EqualTo(@"https://www.googleapis.com/auth/youtube.force-ssl"));
        }

        [Test]
        public void TestCallbackQueryFromARedirectUriIsParsed()
        {
            // The Android redirect arrives as a URI, whose Query keeps the leading question mark.
            var redirect = new Uri(@"com.googleusercontent.apps.1234567890-abcdefgh:/oauth2redirect?error=access_denied&state=xyz");

            Dictionary<string, string> query = OAuthCallbackQuery.Parse(redirect.Query);

            Assert.That(query[@"error"], Is.EqualTo(@"access_denied"));
            Assert.That(query[@"state"], Is.EqualTo(@"xyz"));
        }

        [Test]
        public void TestEmptyCallbackQueryIsEmpty()
        {
            Assert.That(OAuthCallbackQuery.Parse(null), Is.Empty);
            Assert.That(OAuthCallbackQuery.Parse(string.Empty), Is.Empty);
            Assert.That(OAuthCallbackQuery.Parse(@"?"), Is.Empty);
        }
    }
}

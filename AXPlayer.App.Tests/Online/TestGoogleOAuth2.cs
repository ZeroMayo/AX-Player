// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Config;
using AXPlayer.App.Online;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util;
using Google.Apis.Util.Store;
using NUnit.Framework;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Online
{
    [TestFixture]
    public class TestGoogleOAuth2
    {
        private TemporaryNativeStorage storage;
        private AXPlayerConfigManager config;

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
        public void TestGetAccessTokenRefreshesStaleToken()
        {
            var flow = new FakeAuthorizationCodeFlow();
            GoogleOAuth2 oauth = createSignedInOAuth(flow, createToken("stale", "refresh-token", TimeSpan.FromHours(2)));

            Assert.That(oauth.GetAccessToken(), Is.EqualTo(@"refreshed"));
            Assert.That(flow.RefreshCount, Is.EqualTo(1));
            Assert.That(config.Get<string>(AXPlayerSetting.AccessToken), Is.EqualTo(@"refreshed"));
        }

        [Test]
        public void TestGetAccessTokenDoesNotRefreshFreshToken()
        {
            var flow = new FakeAuthorizationCodeFlow();
            GoogleOAuth2 oauth = createSignedInOAuth(flow, createToken("fresh", "refresh-token", TimeSpan.Zero));

            Assert.That(oauth.GetAccessToken(), Is.EqualTo(@"fresh"));
            Assert.That(flow.RefreshCount, Is.Zero);
        }

        [Test]
        public void TestGetAccessTokenKeepsStaleTokenWhenRefreshTokenIsMissing()
        {
            var flow = new FakeAuthorizationCodeFlow();
            GoogleOAuth2 oauth = createSignedInOAuth(flow, createToken("stale", null, TimeSpan.FromHours(2)));

            Assert.That(oauth.GetAccessToken(), Is.EqualTo(@"stale"));
            Assert.That(flow.RefreshCount, Is.Zero);
        }

        [Test]
        public void TestGetAccessTokenReturnsEmptyWhenSignedOut()
        {
            var oauth = new GoogleOAuth2(config, true);

            Assert.That(oauth.GetAccessToken(), Is.Empty);
        }

        private GoogleOAuth2 createSignedInOAuth(FakeAuthorizationCodeFlow flow, TokenResponse token)
        {
            var oauth = new GoogleOAuth2(config, true);
            var credential = new UserCredential(flow, @"user", token);

            // The credential is normally created by the interactive sign-in flow, which is not
            // available in tests. Inject it so the token refresh path can be exercised offline.
            typeof(GoogleOAuth2).GetField(@"credential", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(oauth, credential);
            oauth.SignedIn.Value = true;

            return oauth;
        }

        private static TokenResponse createToken(string accessToken, string refreshToken, TimeSpan age)
            => new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = @"Bearer",
                ExpiresInSeconds = 3600,
                IssuedUtc = DateTime.UtcNow - age,
            };

        private class FakeAuthorizationCodeFlow : IAuthorizationCodeFlow
        {
            public int RefreshCount { get; private set; }

            public IAccessMethod AccessMethod => null;

            public IClock Clock => SystemClock.Default;

            public IDataStore DataStore { get; } = new MemoryDataStore();

            public Task<TokenResponse> LoadTokenAsync(string userId, CancellationToken taskCancellationToken)
                => Task.FromResult<TokenResponse>(null);

            public Task DeleteTokenAsync(string userId, CancellationToken taskCancellationToken)
                => Task.CompletedTask;

            public AuthorizationCodeRequestUrl CreateAuthorizationCodeRequest(string redirectUri)
                => throw new NotSupportedException();

            public Task<TokenResponse> ExchangeCodeForTokenAsync(string userId, string code, string redirectUri, CancellationToken taskCancellationToken)
                => throw new NotSupportedException();

            public Task<TokenResponse> RefreshTokenAsync(string userId, string refreshToken, CancellationToken taskCancellationToken)
            {
                RefreshCount++;

                return Task.FromResult(new TokenResponse
                {
                    AccessToken = @"refreshed",
                    RefreshToken = refreshToken,
                    TokenType = @"Bearer",
                    ExpiresInSeconds = 3600,
                    IssuedUtc = DateTime.UtcNow,
                });
            }

            public Task RevokeTokenAsync(string userId, string token, CancellationToken taskCancellationToken)
                => Task.CompletedTask;

            public bool ShouldForceTokenRetrieval() => false;

            public void Dispose()
            {
            }
        }

        private class MemoryDataStore : IDataStore
        {
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();

            public Task StoreAsync<T>(string key, T value)
            {
                values[key] = value;
                return Task.CompletedTask;
            }

            public Task<T> GetAsync<T>(string key)
                => Task.FromResult(values.TryGetValue(key, out object value) ? (T)value : default);

            public Task DeleteAsync<T>(string key)
            {
                values.Remove(key);
                return Task.CompletedTask;
            }

            public Task ClearAsync()
            {
                values.Clear();
                return Task.CompletedTask;
            }
        }
    }
}

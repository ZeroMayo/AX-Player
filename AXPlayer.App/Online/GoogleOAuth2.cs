// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Crypto.AES;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.Util;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using AXPlayer.App.Config;
using osu.Framework.Development;

namespace AXPlayer.App.Online
{
    public partial class GoogleOAuth2
    {
        private AXPlayerConfigManager appConfig;
        private readonly bool isTestClient;
        private readonly SemaphoreSlim credentialLock = new SemaphoreSlim(1, 1);

        public GoogleOAuth2(AXPlayerConfigManager appConfig, bool isTestClient)
        {
            this.appConfig = appConfig;
            this.isTestClient = isTestClient;

            Logger.Log($"🗝️ Google OAuth system loaded");
        }

        public BindableBool SignedIn { get; private set; } = new BindableBool();

        private UserCredential credential;

        public async Task SignIn()
        {
            await credentialLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!SignedIn.Value)
                    credential = await getUserCredentialAsync().ConfigureAwait(false);

                await refreshCredentialIfNeededAsync().ConfigureAwait(false);

                Logger.Log("signed in to google");

                SignedIn.Value = true;
                appConfig.SetValue<string>(AXPlayerSetting.AccessToken, credential.Token.AccessToken);
            }
            finally
            {
                credentialLock.Release();
            }
        }

        public async Task SignOut()
        {
            await credentialLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (credential == null)
                    return;

                await credential.RevokeTokenAsync(CancellationToken.None);

                Logger.Log("signed out to google");
                SignedIn.Value = false;
                appConfig.SetValue<string>(AXPlayerSetting.AccessToken, string.Empty);
                credential = null;
            }
            finally
            {
                credentialLock.Release();
            }
        }

        public string GetAccessToken()
        {
            if (!SignedIn.Value)
                return string.Empty;

            credentialLock.Wait();
            try
            {
                refreshCredentialIfNeededAsync().GetAwaiter().GetResult();
                return credential?.Token?.AccessToken ?? string.Empty;
            }
            finally
            {
                credentialLock.Release();
            }
        }

        public UserCredential GetCredential()
        {
            if (!SignedIn.Value)
                throw new System.Exception();

            return credential;
        }

        private ClientSecrets getAuthConfig()
        {
            ClientSecrets wth = new ClientSecrets();

            string str1 = !isTestClient ? "ZZLYQ+6EpclmaSSEKGqfVglCIf3Qhk9C744YKjGg8nZ6GkIq/S5v6NMRLEiTO0L/bp/dIZCP/oZGgsfZO1EF8ngylv7MYPJswUrQand0plM=" : "GkAYdCHFjxBARXzB9XFVpm2jNC9hMnyTECc1mo36Xb841Qomeo7RVLqz/bbBs9TadbhgkqbrEuYNMJHLklgae11ToWTmc3VtYOiyyELMy6Q=";
            string str2 = isTestClient ? "NKGBVZGTYD2i++0r+GzyNiK6nFEgiIqvBcgKIUqS844d6X4Js0U4tykxMMzFsZdG" : "Hmg9SjNCg5BT774C1H0VEwcFTi5JLiBhyP++dnYnAqZkiPXA7VwCyW7aPHZxfIYf";

            using (AES aes_1 = new AES("youtube-player-ex"))
            {
                string decrypted_key = aes_1.Decrypt("MXvS9pkr+girnFMRjlUauQ==");
                using (AES aes = new AES(decrypted_key))
                {
                    string decrypted_str1 = aes.Decrypt(str1);
                    string decrypted_str2 = aes.Decrypt(str2);

                    wth.ClientId = decrypted_str1;
                    wth.ClientSecret = decrypted_str2;

                    return wth;
                }
            }
        }

        private async Task refreshCredentialIfNeededAsync()
        {
            if (credential?.Token == null)
                throw new System.InvalidOperationException("Google OAuth credentials are not available.");

            if (!credential.Token.IsExpired(SystemClock.Default))
                return;

            if (string.IsNullOrWhiteSpace(credential.Token.RefreshToken))
                throw new System.InvalidOperationException("The Google OAuth refresh token is unavailable. Sign in again.");

            if (!await credential.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false))
                throw new System.InvalidOperationException("Google OAuth access token refresh failed. Sign in again.");

            appConfig.SetValue<string>(AXPlayerSetting.AccessToken, credential.Token.AccessToken);
        }

        private async Task<UserCredential> getUserCredentialAsync()
        {
            string credPath = (isTestClient || DebugUtils.IsDebugBuild) ? @"AXPlayer-development/OAuth2" : @"AXPlayer/OAuth2";
            return await GoogleWebAuthorizationBroker.AuthorizeAsync(
                getAuthConfig(),
                new[] { Google.Apis.YouTube.v3.YouTubeService.Scope.YoutubeForceSsl },
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, false)
            ).ConfigureAwait(false);
        }
    }
}

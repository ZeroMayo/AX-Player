// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using Crypto.AES;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Oauth2.v2;
using Google.Apis.Oauth2.v2.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using AXPlayer.App.Config;
using osu.Framework.Development;

namespace AXPlayer.App.Online
{
    public partial class GoogleOAuth2
    {
        private static bool isTestClient_static;

        private AXPlayerConfigManager appConfig;

        private bool isTestClient
        {
            get => isTestClient_static;
            set => isTestClient_static = value;
        }

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
            credential = await getUserCredentialAsync();

            if (!string.IsNullOrEmpty(appConfig.Get<string>(AXPlayerSetting.AccessToken)))
            {
                await credential.RefreshTokenAsync(CancellationToken.None);
            }

            // OAuth2 API 클라이언트를 생성합니다.
            var oauth2Service = new Oauth2Service(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential
            });

            Logger.Log("signed in to google");

            SignedIn.Value = true;
            appConfig.SetValue<string>(AXPlayerSetting.AccessToken, GetAccessToken());
        }

        public async Task SignOut()
        {
            if (credential != null)
            {
                await credential.RevokeTokenAsync(CancellationToken.None);

                Logger.Log("signed out to google");

                SignedIn.Value = false;
                appConfig.SetValue<string>(AXPlayerSetting.AccessToken, string.Empty);
            }
        }

        public string GetAccessToken()
        {
            if (!SignedIn.Value)
                return string.Empty;

            return credential?.Token?.AccessToken;
        }

        public UserCredential GetCredential()
        {
            if (!SignedIn.Value)
                throw new System.Exception();

            return credential;
        }

        private static ClientSecrets getAuthConfig()
        {
            ClientSecrets wth = new ClientSecrets();

            string str1 = !isTestClient_static ? "ZZLYQ+6EpclmaSSEKGqfVglCIf3Qhk9C744YKjGg8nZ6GkIq/S5v6NMRLEiTO0L/bp/dIZCP/oZGgsfZO1EF8ngylv7MYPJswUrQand0plM=" : "GkAYdCHFjxBARXzB9XFVpm2jNC9hMnyTECc1mo36Xb841Qomeo7RVLqz/bbBs9TadbhgkqbrEuYNMJHLklgae11ToWTmc3VtYOiyyELMy6Q=";
            string str2 = isTestClient_static ? "NKGBVZGTYD2i++0r+GzyNiK6nFEgiIqvBcgKIUqS844d6X4Js0U4tykxMMzFsZdG" : "Hmg9SjNCg5BT774C1H0VEwcFTi5JLiBhyP++dnYnAqZkiPXA7VwCyW7aPHZxfIYf";

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

        private static async Task<UserCredential> getUserCredentialAsync()
        {
            UserCredential credential;

            string credPath = (isTestClient_static || DebugUtils.IsDebugBuild) ? @"AXPlayer-development/OAuth2" : @"AXPlayer/OAuth2";
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                getAuthConfig(),
                new[] { Google.Apis.YouTube.v3.YouTubeService.Scope.YoutubeForceSsl },
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, false)
            );

            return credential;
        }
    }
}

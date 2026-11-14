// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Crypto.AES;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using AXPlayer.App.Config;
using osu.Framework.Development;

namespace AXPlayer.App.Online
{
    public partial class GoogleOAuth2
    {
        private const string user_id = @"user";

        private readonly AXPlayerConfigManager appConfig;
        private readonly bool isTestClient;

        // Guards the credential reference and serialises token refreshes, so concurrent
        // API calls don't all try to refresh the same expired access token.
        private readonly object credentialLock = new object();

        private UserCredential credential;

        public GoogleOAuth2(AXPlayerConfigManager appConfig, bool isTestClient)
        {
            this.appConfig = appConfig;
            this.isTestClient = isTestClient;

            Logger.Log($"🗝️ Google OAuth system loaded");
        }

        public BindableBool SignedIn { get; private set; } = new BindableBool();

        public async Task SignIn()
        {
            UserCredential newCredential;

            try
            {
                newCredential = await getUserCredentialAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to sign in to Google");
                SignedIn.Value = false;
                return;
            }

            // Access tokens are short-lived and the persisted one may have expired while the
            // app was closed. Refresh it up front so the first API calls after launch succeed.
            // A refresh failure here is not fatal: the credential keeps its refresh token and
            // will retry on the next API call.
            try
            {
                await ensureValidAccessTokenAsync(newCredential).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to refresh the Google OAuth access token during sign-in");
            }

            lock (credentialLock)
            {
                credential = newCredential;
            }

            SignedIn.Value = true;
            persistAccessToken(newCredential);

            Logger.Log("signed in to google");
        }

        public async Task SignOut()
        {
            UserCredential currentCredential;

            lock (credentialLock)
            {
                currentCredential = credential;
                credential = null;
            }

            if (currentCredential != null)
            {
                try
                {
                    await currentCredential.RevokeTokenAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // Revocation requires network access. Even if it fails, the user should
                    // still be signed out locally.
                    Logger.Error(e, "Failed to revoke the Google OAuth token");
                }

                try
                {
                    // Delete the stored refresh token so it can't silently sign the user back in.
                    await currentCredential.Flow.DeleteTokenAsync(user_id, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to delete the stored Google OAuth token");
                }
            }

            SignedIn.Value = false;
            appConfig.SetValue<string>(AXPlayerSetting.AccessToken, string.Empty);

            Logger.Log("signed out to google");
        }

        public string GetAccessToken()
        {
            UserCredential currentCredential;

            lock (credentialLock)
            {
                currentCredential = credential;
            }

            if (!SignedIn.Value || currentCredential == null)
                return string.Empty;

            try
            {
                refreshAccessTokenIfStale(currentCredential);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to refresh the Google OAuth access token");
            }

            return currentCredential.Token?.AccessToken ?? string.Empty;
        }

        public UserCredential GetCredential()
        {
            UserCredential currentCredential;

            lock (credentialLock)
            {
                currentCredential = credential;
            }

            if (!SignedIn.Value || currentCredential == null)
                throw new InvalidOperationException("Not signed in to Google.");

            return currentCredential;
        }

        private void refreshAccessTokenIfStale(UserCredential userCredential)
        {
            lock (credentialLock)
            {
                TokenResponse token = userCredential.Token;

                if (token == null || !token.IsStale)
                    return;

                if (string.IsNullOrEmpty(token.RefreshToken))
                    throw new InvalidOperationException("The Google OAuth refresh token is unavailable. Sign in again.");

                // Refresh on the thread pool so callers on the update thread don't deadlock
                // waiting for a continuation that can't run until the update thread is free.
                bool refreshed = Task.Run(() => userCredential.RefreshTokenAsync(CancellationToken.None)).GetAwaiter().GetResult();

                if (!refreshed)
                    throw new InvalidOperationException("Failed to refresh the Google OAuth access token. Sign in again.");

                persistAccessToken(userCredential);
            }
        }

        private static async Task ensureValidAccessTokenAsync(UserCredential userCredential)
        {
            TokenResponse token = userCredential.Token;

            if (token == null || !token.IsStale)
                return;

            if (string.IsNullOrEmpty(token.RefreshToken))
                throw new InvalidOperationException("The Google OAuth refresh token is unavailable. Sign in again.");

            if (!await userCredential.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false))
                throw new InvalidOperationException("Failed to refresh the Google OAuth access token. Sign in again.");
        }

        private void persistAccessToken(UserCredential userCredential)
            => appConfig.SetValue<string>(AXPlayerSetting.AccessToken, userCredential.Token?.AccessToken ?? string.Empty);

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

        private async Task<UserCredential> getUserCredentialAsync()
        {
            GoogleAuthorizationCodeFlow flow = createAuthorizationCodeFlow();

            TokenResponse storedToken = await flow.LoadTokenAsync(user_id, CancellationToken.None).ConfigureAwait(false);

            // Credentials created before offline access was requested have no refresh token,
            // which means they can never be renewed silently. Drop them so the next
            // authorization grants a refresh token.
            if (storedToken != null && string.IsNullOrEmpty(storedToken.RefreshToken))
            {
                Logger.Log("Stored Google OAuth credential has no refresh token. Requesting a new one.");
                await flow.DeleteTokenAsync(user_id, CancellationToken.None).ConfigureAwait(false);
            }

            var installedApp = new AuthorizationCodeInstalledApp(flow, new NekoPlayerCodeReceiver());

            return await installedApp.AuthorizeAsync(user_id, CancellationToken.None).ConfigureAwait(false);
        }

        private GoogleAuthorizationCodeFlow createAuthorizationCodeFlow()
        {
            string credPath = (isTestClient || DebugUtils.IsDebugBuild) ? @"AXPlayer-development/OAuth2" : @"AXPlayer/OAuth2";

            return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = getAuthConfig(),
                Scopes = new[] { Google.Apis.YouTube.v3.YouTubeService.Scope.YoutubeForceSsl },
                DataStore = new FileDataStore(credPath, false),
                // Force the consent screen so Google always returns a refresh token. Without
                // this it is only returned the first time a user authorises the app.
                Prompt = "consent",
            });
        }
    }
}

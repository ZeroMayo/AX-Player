// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Crypto.AES;
using AXPlayer.App.Config;
using AXPlayer.App.Platform;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Google OAuth 2.0 client used for the Innertube backend.
    /// </summary>
    /// <remarks>
    /// Innertube only needs a Google Bearer access token, so this class implements the pieces
    /// the player actually uses instead of going through the Google API client libraries:
    /// <list type="bullet">
    /// <item>authorization code flow with PKCE and a platform-specific redirect
    /// (a loopback listener on desktop, a custom URI scheme on Android),</item>
    /// <item>direct token exchange/refresh against the OAuth token endpoint,</item>
    /// <item>persisting the refresh token so the session survives restarts,</item>
    /// <item>revocation on sign-out.</item>
    /// </list>
    /// The access token is handed to <see cref="YouTubeAPI" />, which sends it as a Bearer
    /// token to the TVHTML5 Innertube client.
    /// </remarks>
    public partial class GoogleOAuth2
    {
        private const string authorization_endpoint = @"https://accounts.google.com/o/oauth2/v2/auth";
        private const string token_endpoint = @"https://oauth2.googleapis.com/token";
        private const string revocation_endpoint = @"https://oauth2.googleapis.com/revoke";

        /// <summary>
        /// Same scope the player requested from the Data API, so existing grants keep working.
        /// </summary>
        private const string youtube_scope = @"https://www.googleapis.com/auth/youtube.force-ssl";

        /// <summary>
        /// Access tokens are refreshed this long before they actually expire so a request
        /// cannot be sent with a token that expires while it is in flight.
        /// </summary>
        private const int refresh_skew_seconds = 120;

        private static readonly string[] permanent_refresh_errors =
        {
            @"invalid_grant",
            @"invalid_client",
            @"unauthorized_client",
        };

        private readonly AXPlayerConfigManager appConfig;
        private readonly bool isTestClient;

        private readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        // Serialises refreshes so concurrent Innertube requests cannot rotate the token twice.
        private readonly SemaphoreSlim refreshLock = new SemaphoreSlim(1, 1);

        private OAuthToken? token;

        public GoogleOAuth2(AXPlayerConfigManager appConfig, bool isTestClient)
        {
            this.appConfig = appConfig;
            this.isTestClient = isTestClient;

            Logger.Log(@"🗝️ Google OAuth 2.0 (Innertube) system loaded");
        }

        public BindableBool SignedIn { get; } = new BindableBool();

        /// <summary>
        /// Whether a refresh token is persisted, i.e. a silent session restore is possible.
        /// </summary>
        /// <summary>
        /// Whether a refresh token is persisted in the player configuration.
        /// </summary>
        /// <remarks>
        /// A legacy credential store from an older build is imported by <see cref="RestoreSessionAsync" />,
        /// so it is not included here.
        /// </remarks>
        public bool HasStoredSession => !string.IsNullOrEmpty(appConfig.Get<string>(AXPlayerSetting.RefreshToken));

        /// <summary>
        /// Signs in interactively, or silently restores the persisted session when possible.
        /// </summary>
        public async Task SignIn()
        {
            if (SignedIn.Value)
                return;

            try
            {
                OAuthToken? restored = tryRestorePersistedToken();

                if (restored != null)
                {
                    token = restored;
                    OAuthToken? valid = isExpiring(restored)
                        ? await refreshAsync(CancellationToken.None).ConfigureAwait(false)
                        : restored;

                    if (valid != null)
                    {
                        SignedIn.Value = true;
                        Logger.Log(@"signed in to google (restored session)");
                        return;
                    }
                }

                OAuthToken? authorized = await authorizeInteractiveAsync(CancellationToken.None).ConfigureAwait(false);

                if (authorized == null)
                {
                    SignedIn.Value = false;
                    return;
                }

                token = authorized;
                persist(authorized);
                SignedIn.Value = true;

                Logger.Log(@"signed in to google");
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to sign in to Google");
                SignedIn.Value = false;
            }
        }

        /// <summary>
        /// Restores the persisted session without ever showing the consent screen. Used on launch.
        /// </summary>
        public async Task RestoreSessionAsync()
        {
            if (SignedIn.Value)
                return;

            try
            {
                OAuthToken? restored = tryRestorePersistedToken();

                if (restored == null)
                {
                    SignedIn.Value = false;
                    return;
                }

                token = restored;

                OAuthToken? valid = isExpiring(restored)
                    ? await refreshAsync(CancellationToken.None).ConfigureAwait(false)
                    : restored;

                SignedIn.Value = valid != null;

                if (valid != null)
                    Logger.Log(@"restored the google session");
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to restore the Google session");
                SignedIn.Value = false;
            }
        }

        public async Task SignOut()
        {
            OAuthToken? current = token;

            token = null;
            SignedIn.Value = false;

            if (current != null)
            {
                try
                {
                    await revokeAsync(current).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // Revocation requires network access. Even if it fails, the user should
                    // still be signed out locally.
                    Logger.Error(e, @"Failed to revoke the Google OAuth token");
                }
            }

            clearPersisted();

            Logger.Log(@"signed out to google");
        }

        /// <summary>
        /// Returns a usable access token, refreshing it synchronously when it is about to expire.
        /// </summary>
        /// <remarks>
        /// The refresh runs on the thread pool so callers on the update thread cannot deadlock
        /// waiting for a continuation that needs that same thread.
        /// </remarks>
        public string GetAccessToken()
        {
            if (!SignedIn.Value)
                return string.Empty;

            OAuthToken? current = token;

            if (current == null)
                return string.Empty;

            if (!isExpiring(current))
                return current.AccessToken;

            try
            {
                OAuthToken? refreshed = Task.Run(() => refreshAsync(CancellationToken.None)).GetAwaiter().GetResult();
                return refreshed?.AccessToken ?? string.Empty;
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to refresh the Google OAuth access token");
                return string.Empty;
            }
        }

        /// <summary>
        /// Asynchronous counterpart of <see cref="GetAccessToken" /> for request pipelines.
        /// </summary>
        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            if (!SignedIn.Value)
                return string.Empty;

            OAuthToken? current = token;

            if (current == null)
                return string.Empty;

            if (!isExpiring(current))
                return current.AccessToken;

            OAuthToken? refreshed = await refreshAsync(cancellationToken).ConfigureAwait(false);
            return refreshed?.AccessToken ?? string.Empty;
        }

        #region authorization

        private async Task<OAuthToken?> authorizeInteractiveAsync(CancellationToken cancellationToken)
        {
            ClientCredentials credentials = GetClientCredentials();

            string codeVerifier = createRandomToken(32);
            string codeChallenge = createCodeChallenge(codeVerifier);
            string state = createRandomToken(16);

            using IOAuthCodeReceiver receiver = CreateCodeReceiver();

            string authorizationUrl = buildAuthorizationUrl(credentials.ClientId, receiver.RedirectUri, codeChallenge, state);

            Logger.Log($@"Opening the browser for Google authorization: {authorizationUrl}");

            if (!PlatformBridge.OpenUrl(authorizationUrl))
                throw new NotSupportedException(@"Failed to launch a browser for the Google authorization.");

            Dictionary<string, string> callback = await receiver.WaitForCallbackAsync(cancellationToken).ConfigureAwait(false);

            if (callback.TryGetValue(@"error", out string? error))
                throw new InvalidOperationException($@"Google authorization was rejected: {error}");

            if (!callback.TryGetValue(@"state", out string? returnedState) || !string.Equals(returnedState, state, StringComparison.Ordinal))
                throw new InvalidOperationException(@"The Google authorization response contained an unexpected state value.");

            if (!callback.TryGetValue(@"code", out string? code) || string.IsNullOrEmpty(code))
                throw new InvalidOperationException(@"The Google authorization response did not contain a code.");

            return await exchangeCodeAsync(credentials, code, codeVerifier, receiver.RedirectUri, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the receiver for the authorization redirect of the current platform.
        /// </summary>
        /// <remarks>
        /// Desktop hosts use a loopback listener. Platform hosts that can only receive a custom
        /// URI scheme (Android) override this to return their own receiver.
        /// </remarks>
        protected virtual IOAuthCodeReceiver CreateCodeReceiver() => LoopbackCodeReceiver.Start();

        private string buildAuthorizationUrl(string clientId, string redirectUri, string codeChallenge, string state)
        {
            string scope = Uri.EscapeDataString(youtube_scope);

            return $@"{authorization_endpoint}"
                   + $@"?client_id={Uri.EscapeDataString(clientId)}"
                   + $@"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                   + @"&response_type=code"
                   + $@"&scope={scope}"
                   + @"&access_type=offline"
                   // Force the consent screen so Google always returns a refresh token.
                   + @"&prompt=consent"
                   + @"&include_granted_scopes=true"
                   + $@"&state={state}"
                   + $@"&code_challenge={codeChallenge}"
                   + @"&code_challenge_method=S256";
        }

        private async Task<OAuthToken?> exchangeCodeAsync(ClientCredentials credentials, string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
        {
            var form = new Dictionary<string, string>
            {
                [@"grant_type"] = @"authorization_code",
                [@"code"] = code,
                [@"redirect_uri"] = redirectUri,
                [@"code_verifier"] = codeVerifier,
            };

            addClientCredentials(form, credentials);

            TokenRequestResult result = await requestTokenAsync(form, cancellationToken).ConfigureAwait(false);

            if (result.Token == null)
                throw new InvalidOperationException($@"Failed to exchange the Google authorization code: {result.Error ?? @"unknown error"}");

            return result.Token;
        }

        #endregion

        #region token refresh

        private async Task<OAuthToken?> refreshAsync(CancellationToken cancellationToken)
        {
            await refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                OAuthToken? current = token;

                if (current == null || string.IsNullOrEmpty(current.RefreshToken))
                {
                    clearSession(deletePersistedTokens: false);
                    return null;
                }

                // Another request may have refreshed the token while this one was waiting.
                if (!isExpiring(current))
                    return current;

                ClientCredentials credentials = GetClientCredentials();

                var form = new Dictionary<string, string>
                {
                    [@"grant_type"] = @"refresh_token",
                    [@"refresh_token"] = current.RefreshToken,
                };

                addClientCredentials(form, credentials);

                TokenRequestResult result = await requestTokenAsync(form, cancellationToken).ConfigureAwait(false);

                if (result.Token == null)
                {
                    bool permanent = result.Error != null && permanent_refresh_errors.Contains(result.Error);

                    Logger.Log($@"Failed to refresh the Google OAuth access token ({result.Error ?? @"unknown error"}).");

                    if (permanent)
                    {
                        // A revoked/expired refresh token can never recover, so drop the session.
                        clearSession(deletePersistedTokens: true);
                    }
                    else
                    {
                        // Network failures are retried by the next request, so keep the session.
                        Logger.Log(@"Keeping the Google session so the refresh can be retried.");
                    }

                    return null;
                }

                // Google only returns a new refresh token when it rotates one.
                OAuthToken refreshed = string.IsNullOrEmpty(result.Token.RefreshToken)
                    ? result.Token with { RefreshToken = current.RefreshToken }
                    : result.Token;

                token = refreshed;
                persist(refreshed);

                return refreshed;
            }
            finally
            {
                refreshLock.Release();
            }
        }

        private async Task<TokenRequestResult> requestTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            try
            {
                using var content = new FormUrlEncodedContent(form);
                using HttpResponseMessage response = await httpClient.PostAsync(token_endpoint, content, cancellationToken).ConfigureAwait(false);

                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return new TokenRequestResult(null, parseErrorCode(body) ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture));

                return new TokenRequestResult(parseToken(body), null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"The Google OAuth token request failed: {exception.GetDescription()}");
                return new TokenRequestResult(null, @"network_error");
            }
        }

        private static OAuthToken? parseToken(string body)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                JsonElement root = document.RootElement;

                string? accessToken = getString(root, @"access_token");

                if (string.IsNullOrEmpty(accessToken))
                    return null;

                string refreshToken = getString(root, @"refresh_token") ?? string.Empty;
                long expiresIn = getInt64(root, @"expires_in") ?? 3600;

                return new OAuthToken(accessToken, refreshToken, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
            }
            catch (JsonException exception)
            {
                Logger.Error(exception, @"The Google OAuth token response could not be parsed.");
                return null;
            }
        }

        private static string? parseErrorCode(string body)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                return getString(document.RootElement, @"error");
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private async Task revokeAsync(OAuthToken current)
        {
            string target = string.IsNullOrEmpty(current.RefreshToken) ? current.AccessToken : current.RefreshToken;

            if (string.IsNullOrEmpty(target))
                return;

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [@"token"] = target,
            });

            using HttpResponseMessage response = await httpClient.PostAsync(revocation_endpoint, content, CancellationToken.None).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                Logger.Log($@"Revoking the Google OAuth token failed with status {(int)response.StatusCode}.", LoggingTarget.Network);
        }

        private bool isExpiring(OAuthToken candidate)
            => candidate.ExpiresAtUtc == default || candidate.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddSeconds(refresh_skew_seconds);

        private void clearSession(bool deletePersistedTokens)
        {
            token = null;
            SignedIn.Value = false;

            if (deletePersistedTokens)
                clearPersisted();
        }

        #endregion

        #region persistence

        private OAuthToken? tryRestorePersistedToken()
        {
            string refreshToken = appConfig.Get<string>(AXPlayerSetting.RefreshToken);

            if (string.IsNullOrEmpty(refreshToken))
                refreshToken = tryMigrateLegacyRefreshToken() ?? string.Empty;

            if (string.IsNullOrEmpty(refreshToken))
                return null;

            string accessToken = appConfig.Get<string>(AXPlayerSetting.AccessToken);
            DateTimeOffset expiresAt = parseExpiry(appConfig.Get<string>(AXPlayerSetting.AccessTokenExpiresAt));

            return new OAuthToken(accessToken, refreshToken, expiresAt);
        }

        private void persist(OAuthToken value)
        {
            appConfig.SetValue(AXPlayerSetting.AccessToken, value.AccessToken);
            appConfig.SetValue(AXPlayerSetting.RefreshToken, value.RefreshToken);
            appConfig.SetValue(AXPlayerSetting.AccessTokenExpiresAt, value.ExpiresAtUtc.ToString(@"o", CultureInfo.InvariantCulture));
        }

        private void clearPersisted()
        {
            appConfig.SetValue(AXPlayerSetting.AccessToken, string.Empty);
            appConfig.SetValue(AXPlayerSetting.RefreshToken, string.Empty);
            appConfig.SetValue(AXPlayerSetting.AccessTokenExpiresAt, string.Empty);
        }

        internal static DateTimeOffset parseExpiry(string? raw)
            => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value) ? value : default;

        /// <summary>
        /// Imports the refresh token an older build stored with the Google client library
        /// (<c>FileDataStore</c>), so users are not signed out by the migration.
        /// </summary>
        private string? tryMigrateLegacyRefreshToken()
        {
            foreach (string folder in legacy_token_folders)
            {
                try
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), folder, @"OAuth2", @"Google.Apis.Auth.OAuth2.Responses.TokenResponse-user");

                    if (!File.Exists(path))
                        continue;

                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                    string? refreshToken = getString(document.RootElement, @"refresh_token");

                    if (string.IsNullOrEmpty(refreshToken))
                        continue;

                    Logger.Log($@"Imported the Google OAuth session from the legacy credential store ({folder}).");
                    appConfig.SetValue(AXPlayerSetting.RefreshToken, refreshToken);

                    return refreshToken;
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $@"Failed to import the legacy Google OAuth session from '{folder}'.");
                }
            }

            return null;
        }

        #endregion

        #region client credentials

        private ClientCredentials? cachedCredentials;

        /// <summary>
        /// Resolves the OAuth client credentials of the current platform.
        /// </summary>
        /// <remarks>
        /// Installed application clients (Android) have no client secret, so
        /// <see cref="ClientCredentials.ClientSecret" /> may be empty and is then omitted from
        /// the token requests.
        /// </remarks>
        protected virtual ClientCredentials GetClientCredentials() => cachedCredentials ??= createDesktopClientCredentials();

        private ClientCredentials createDesktopClientCredentials()
        {
            string encryptedClientId = !isTestClient
                ? @"ZZLYQ+6EpclmaSSEKGqfVglCIf3Qhk9C744YKjGg8nZ6GkIq/S5v6NMRLEiTO0L/bp/dIZCP/oZGgsfZO1EF8ngylv7MYPJswUrQand0plM="
                : @"GkAYdCHFjxBARXzB9XFVpm2jNC9hMnyTECc1mo36Xb841Qomeo7RVLqz/bbBs9TadbhgkqbrEuYNMJHLklgae11ToWTmc3VtYOiyyELMy6Q=";

            string encryptedClientSecret = isTestClient
                ? @"NKGBVZGTYD2i++0r+GzyNiK6nFEgiIqvBcgKIUqS844d6X4Js0U4tykxMMzFsZdG"
                : @"Hmg9SjNCg5BT774C1H0VEwcFTi5JLiBhyP++dnYnAqZkiPXA7VwCyW7aPHZxfIYf";

            using (AES outerAes = new AES(@"youtube-player-ex"))
            {
                string decryptedKey = outerAes.Decrypt(@"MXvS9pkr+girnFMRjlUauQ==");

                using (AES aes = new AES(decryptedKey))
                {
                    return new ClientCredentials(
                        aes.Decrypt(encryptedClientId),
                        aes.Decrypt(encryptedClientSecret));
                }
            }
        }

        private static void addClientCredentials(Dictionary<string, string> form, ClientCredentials credentials)
        {
            form[@"client_id"] = credentials.ClientId;

            if (!string.IsNullOrEmpty(credentials.ClientSecret))
                form[@"client_secret"] = credentials.ClientSecret;
        }

        #endregion

        #region helpers

        private static string createRandomToken(int byteCount)
        {
            var bytes = new byte[byteCount];
            RandomNumberGenerator.Fill(bytes);

            return base64UrlEncode(bytes);
        }

        internal static string createCodeChallenge(string codeVerifier)
            => base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        private static string base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string? getString(JsonElement element, string propertyName)
            => element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out JsonElement value)
               && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static long? getInt64(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value))
                return null;

            switch (value.ValueKind)
            {
                case JsonValueKind.Number when value.TryGetInt64(out long number):
                    return number;

                case JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed):
                    return parsed;

                default:
                    return null;
            }
        }

        private static readonly string[] legacy_token_folders =
        {
            @"AXPlayer",
            @"AXPlayer-development",
        };

        /// <summary>
        /// OAuth client credentials. <see cref="ClientSecret" /> is empty for installed
        /// application clients (Android), which authenticate with PKCE only.
        /// </summary>
        protected sealed record ClientCredentials(string ClientId, string ClientSecret);

        private sealed record OAuthToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

        private sealed record TokenRequestResult(OAuthToken? Token, string? Error);

        #endregion
    }
}

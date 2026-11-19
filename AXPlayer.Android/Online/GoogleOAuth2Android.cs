// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using AXPlayer.App.Config;
using AXPlayer.App.Online;
using osu.Framework.Development;
using osu.Framework.Logging;

namespace AXPlayer.Android.Online
{
    /// <summary>
    /// Google OAuth 2.0 client for Android.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Android cannot use the loopback redirect of the desktop hosts, because the consent screen
    /// runs in a different application: the redirect is handed back to the player as an intent
    /// instead. This client therefore requests the custom URI scheme redirect Google requires for
    /// installed applications and receives the authorization code from
    /// <see cref="OAuthRedirectActivity" />:
    /// <code>
    /// com.googleusercontent.apps.&lt;client id&gt;:/oauth2redirect?code=...&amp;state=...
    /// </code>
    /// </para>
    /// <para>
    /// An installed application client has no client secret, so the code is exchanged with PKCE
    /// only. Refresh, persistence and revocation are inherited from <see cref="GoogleOAuth2" />.
    /// </para>
    /// <para>
    /// Until <see cref="client_id_suffix" /> is filled in, this client falls back to the desktop
    /// behaviour (the shared client credentials and the loopback redirect), so a build without a
    /// registered Android OAuth client keeps working the way it did before.
    /// </para>
    /// </remarks>
    internal sealed class GoogleOAuth2Android : GoogleOAuth2
    {
        /// <summary>
        /// The client ID suffix of the Android OAuth client registered in the Google Cloud console.
        /// </summary>
        /// <remarks>
        /// Register the client as an "Android" application with the package name
        /// <c>io.zeromayo.axplayer</c> and the SHA-1 of the certificate the build is signed with.
        /// Google then issues a client ID of the form <c>&lt;suffix&gt;.apps.googleusercontent.com</c>
        /// and hands the authorization code back to
        /// <c>com.googleusercontent.apps.&lt;suffix&gt;:/oauth2redirect</c>.
        /// Debug and release builds are signed with different certificates, so each one needs its
        /// own registered client, or its SHA-1 added to the same client.
        /// </remarks>
        private const string client_id_suffix = "41012199246-a9r2c6tkvadt3t90qrak85815052q70v";

        internal const string redirect_path = @"/oauth2redirect";

        /// <summary>
        /// The custom URI scheme the redirect comes back on, matched by the intent filter of
        /// <see cref="OAuthRedirectActivity" />.
        /// </summary>
        internal const string redirect_scheme = @"com.googleusercontent.apps." + client_id_suffix;

        /// <summary>
        /// Whether an Android OAuth client has been configured for this build.
        /// </summary>
        internal static bool IsConfigured => !client_id_suffix.StartsWith(@"REPLACE_", StringComparison.Ordinal);

        public GoogleOAuth2Android(AXPlayerConfigManager appConfig, bool isTestClient)
            : base(appConfig, isTestClient)
        {
            if (!IsConfigured)
                Logger.Log(@"No Android OAuth client is configured; using the desktop Google sign-in flow.");
        }

        /// <summary>
        /// Hands the redirect delivered to <see cref="OAuthRedirectActivity" /> to the pending sign-in.
        /// </summary>
        /// <returns><c>true</c> when a sign-in was waiting for the redirect.</returns>
        internal static bool TryHandleRedirect(Uri redirect)
        {
            bool delivered = CustomSchemeCodeReceiver.TryDeliver(redirect);

            if (!delivered)
            {
                // Most likely Android recreated the process while the browser was in the foreground.
                Logger.Log(@"Ignoring a Google OAuth redirect because no sign-in is waiting for it.");
            }

            return delivered;
        }

        protected override IOAuthCodeReceiver CreateCodeReceiver() => IsConfigured ? new CustomSchemeCodeReceiver(redirectUri) : base.CreateCodeReceiver();

        protected override ClientCredentials GetClientCredentials()
            => IsConfigured ? new ClientCredentials(clientId, string.Empty) : base.GetClientCredentials();

        private static string redirectUri => $@"{redirect_scheme}:{redirect_path}";

        private static string clientId => $@"{client_id_suffix}.apps.googleusercontent.com";
    }
}

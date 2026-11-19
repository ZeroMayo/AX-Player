// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AXPlayer.Android.Online;

namespace AXPlayer.Android
{
    /// <summary>
    /// Receives the custom URI scheme redirect of the Google OAuth 2.0 authorization and hands it
    /// back to the player.
    /// </summary>
    /// <remarks>
    /// This activity is transparent, invisible and never kept in the back stack: it only exists so
    /// the browser can return the authorization code to the application, and finishing it brings
    /// the player back to the foreground. See <see cref="GoogleOAuth2Android" />.
    /// </remarks>
    [Activity(
        Name = "io.zeromayo.axplayer.OAuthRedirectActivity",
        Exported = true,
        NoHistory = true,
        LaunchMode = LaunchMode.SingleTop,
        Theme = "@android:style/Theme.Translucent.NoTitleBar")]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = GoogleOAuth2Android.redirect_scheme,
        DataPath = GoogleOAuth2Android.redirect_path)]
    public class OAuthRedirectActivity : Activity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            handleIntent(Intent);
            Finish();
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);

            handleIntent(intent);
            Finish();
        }

        private static void handleIntent(Intent? intent)
        {
            // Android.Net.Uri is not a System.Uri, so round-trip through its string form to reuse
            // the platform-agnostic query parser.
            string? data = intent?.DataString;

            if (string.IsNullOrEmpty(data) || !Uri.TryCreate(data, UriKind.Absolute, out Uri? uri))
                return;

            GoogleOAuth2Android.TryHandleRedirect(uri);
        }
    }
}

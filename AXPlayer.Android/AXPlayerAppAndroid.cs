// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Android.Content;
using Android.Content.PM;
using AXPlayer.App;
using AXPlayer.App.Online;
using AXPlayer.App.Platform;
using AXPlayer.Android.Online;
using osu.Framework.Screens;

namespace AXPlayer.Android
{
    internal partial class AXPlayerAppAndroid : AXPlayerApp
    {
        private MainActivity mainActivity;

        public AXPlayerAppAndroid(MainActivity mainActivity)
            : base()
        {
            this.mainActivity = mainActivity;

            // Google sign-in and other links must be opened through the Android activity
            // instead of the desktop shell launcher.
            PlatformBridge.OpenUrlHandler = url =>
            {
                Intent intent = new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(url));
                intent.AddFlags(ActivityFlags.NewTask);
                mainActivity.StartActivity(intent);
                return true;
            };
        }

        public override MediaSession CreateMediaSession() => new AndroidMediaSessionHandler();

        // Google sign-in receives its redirect through a custom URI scheme intent on Android
        // instead of the loopback listener the desktop hosts use.
        protected override GoogleOAuth2 CreateGoogleOAuth2() => new GoogleOAuth2Android(LocalConfig, !IsDeployedBuild);

        protected override void ExitApp()
        {
            base.ExitApp();
            mainActivity.FinishAffinity();
        }
    }
}

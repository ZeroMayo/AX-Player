// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Android.Content;
using Android.Content.PM;
using AXPlayer.App;
using AXPlayer.App.Platform;
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

        protected override void ExitApp()
        {
            base.ExitApp();
            mainActivity.FinishAffinity();
        }
    }
}

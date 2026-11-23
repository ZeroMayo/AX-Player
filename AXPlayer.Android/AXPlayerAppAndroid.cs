// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using AXPlayer.Android.Online;
using AXPlayer.App;
using AXPlayer.App.Online;
using AXPlayer.App.Platform;
using AXPlayer.App.Updater;
using osu.Framework.Development;
using osu.Framework.Extensions.ObjectExtensions;

namespace AXPlayer.Android
{
    internal partial class AXPlayerAppAndroid : AXPlayerApp
    {
        private MainActivity mainActivity;

        private readonly PackageInfo packageInfo;

        public AXPlayerAppAndroid(MainActivity mainActivity)
            : base()
        {
            this.mainActivity = mainActivity;
            packageInfo = Application.Context.ApplicationContext!.PackageManager!.GetPackageInfo(Application.Context.ApplicationContext.PackageName!, 0).AsNonNull();

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

        public override string Version
        {
            get
            {
                if (!IsDeployedBuild)
                    return @"local " + (DebugUtils.IsDebugBuild ? @"debug" : @"release");

                return packageInfo.VersionName.AsNonNull();
            }
        }

        public override Version AssemblyVersion => new Version(packageInfo.VersionName.AsNonNull().Split('-').First());

        public override MediaSession CreateMediaSession() => new AndroidMediaSessionHandler();

        // Google sign-in receives its redirect through a custom URI scheme intent on Android
        // instead of the loopback listener the desktop hosts use.
        protected override GoogleOAuth2 CreateGoogleOAuth2() => new GoogleOAuth2Android(LocalConfig, !IsDeployedBuild);

        protected override UpdateManager CreateUpdateManager() => new NoActionUpdateManager();

        protected override void ExitApp()
        {
            // Tear down the media session before the activity closes, otherwise the media notification
            // and lock screen / Bluetooth controls would outlive the app.
            MediaSession?.DeleteMediaSession();

            base.ExitApp();
            mainActivity.FinishAffinity();
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Android.App;
using Android.Content.PM;
using Android.OS;
using osu.Framework;
using osu.Framework.Android;

namespace AXPlayer.Android
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Theme = "@android:style/Theme.NoTitleBar",
        HardwareAccelerated = true,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode)] // These are the config changes AndroidGameActivity expects not to restart for.
    public class MainActivity : AndroidGameActivity
    {
        private const int notification_permission_request_code = 1;

        protected override Game CreateGame() => new AXPlayerAppAndroid(this);

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            // This is a safe default. Localised usages should specify lower values as required.
            AppDomain.CurrentDomain.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", TimeSpan.FromMilliseconds(1000));

            base.OnCreate(savedInstanceState);

            applyLandscapeOrientation();

            // The media session notification is the Android equivalent of the desktop tray icon / MPRIS integration.
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                RequestPermissions(new[] { global::Android.Manifest.Permission.PostNotifications }, notification_permission_request_code);
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Launchers (and some OEM rotation policies) reset the activity's requested orientation
            // when it is resumed, so the manifest value alone is not always enough.
            applyLandscapeOrientation();
        }

        private void applyLandscapeOrientation()
        {
            if (RequestedOrientation != ScreenOrientation.SensorLandscape)
                RequestedOrientation = ScreenOrientation.SensorLandscape;
        }
    }
}

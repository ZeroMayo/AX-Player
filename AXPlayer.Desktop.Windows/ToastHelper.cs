// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using CommunityToolkit.WinUI.Notifications;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.Desktop.Windows
{
    public static class ToastHelper
    {
        public static void ShowToast(string title, string message, bool playSound = true)
        {
            try
            {
                var builder = new ToastContentBuilder()
                    .AddText(title)
                    .AddText(message);

                if (!playSound)
                    builder.AddAudio(null);
                else
                    builder.AddAudio(new Uri("ms-winsoundevent:Notification.Default"));

                builder.Show();
            }
            catch (Exception e)
            {
                Logger.Error(e, e.GetDescription());
            }
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace AXPlayer.Desktop.Linux
{
     /// <seealso>http://www.galago-project.org/specs/notification/0.9/x408.html</seealso>
     /// <summary>
     /// Interface for notifications
     /// </summary>
    [DBusInterface("org.freedesktop.Notifications")]
    interface IFreeDesktopNotifications : IDBusObject
    {
        Task<uint> NotifyAsync(string app_name, uint replaces_id, string app_icon, string summary, string body, string[] actions, IDictionary<string, object> hints, int expire_timeout);

        Task CloseNotificationAsync(uint id);

        Task<string[]> GetCapabilitiesAsync();

        Task<(string name, string vendor, string version, string spec_version)> GetServerInformationAsync();

        Task<IDisposable> WatchNotificationClosedAsync(Action<(uint id, uint reason)> handler, Action<Exception> onError = null);

        Task<IDisposable> WatchActionInvokedAsync(Action<(uint id, string action_key)> handler, Action<Exception> onError = null);
    }
}

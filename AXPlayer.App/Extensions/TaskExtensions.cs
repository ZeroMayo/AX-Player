// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using osu.Framework.Extensions.ExceptionExtensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Extensions
{
    public static class TaskExtensions
    {
        public static void FireAndForget(this Task task, Action? onSuccess = null, Action<Exception>? onError = null) =>
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.Assert(t.Exception != null);
                    Exception exception = t.Exception.AsSingular();

                    Logger.Error(exception, $"Unobserved exception occurred via {nameof(FireAndForget)} call: {exception.Message}");

                    onError?.Invoke(exception);
                }
                else
                {
                    onSuccess?.Invoke();
                }
            });
    }
}

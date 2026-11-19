// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Receiver for a redirect that the operating system hands back to the application instead of
    /// sending it to a loopback listener, e.g. the Android custom URI scheme redirect
    /// <c>com.googleusercontent.apps.&lt;client id&gt;:/oauth2redirect</c>.
    /// </summary>
    /// <remarks>
    /// The redirect arrives on a different component than the one that started the authorization
    /// (an Android activity), so the waiting receiver is tracked statically and matched by
    /// <see cref="TryDeliver" />.
    /// </remarks>
    public sealed class CustomSchemeCodeReceiver : IOAuthCodeReceiver
    {
        private static readonly object pending_lock = new object();

        private static TaskCompletionSource<Uri>? pending;

        private TaskCompletionSource<Uri>? completion;

        public CustomSchemeCodeReceiver(string redirectUri)
        {
            if (string.IsNullOrEmpty(redirectUri))
                throw new ArgumentException(@"A redirect URI is required.", nameof(redirectUri));

            RedirectUri = redirectUri;
        }

        public string RedirectUri { get; }

        public async Task<Dictionary<string, string>> WaitForCallbackAsync(CancellationToken cancellationToken)
        {
            completion = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (pending_lock)
            {
                pending = completion;
            }

            using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
            {
                Uri redirect = await completion.Task.ConfigureAwait(false);

                Logger.Log(@"Received the Google OAuth redirect from the operating system.");

                return OAuthCallbackQuery.Parse(redirect.Query);
            }
        }

        /// <summary>
        /// Hands a redirect the operating system delivered to the application to the waiting receiver.
        /// </summary>
        /// <returns><c>true</c> when an authorization was waiting for the redirect.</returns>
        public static bool TryDeliver(Uri redirect)
        {
            TaskCompletionSource<Uri>? waiting;

            lock (pending_lock)
            {
                waiting = pending;
                pending = null;
            }

            return waiting?.TrySetResult(redirect) == true;
        }

        public void Dispose()
        {
            lock (pending_lock)
            {
                if (ReferenceEquals(pending, completion))
                    pending = null;
            }
        }
    }
}

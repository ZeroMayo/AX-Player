// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Receives the redirect of the Google OAuth 2.0 authorization request.
    /// </summary>
    /// <remarks>
    /// Desktop hosts use a loopback HTTP listener, while Android has to use a custom URI scheme
    /// that the operating system delivers to the application as an intent. The receiver is
    /// created before the authorization URL is built, so <see cref="RedirectUri"/> is known and
    /// can be sent to Google.
    /// </remarks>
    public interface IOAuthCodeReceiver : IDisposable
    {
        /// <summary>
        /// The redirect URI that is sent to Google and has to be registered for the OAuth client.
        /// </summary>
        string RedirectUri { get; }

        /// <summary>
        /// Waits for the redirect and returns its query parameters, e.g. <c>code</c> and <c>state</c>.
        /// </summary>
        Task<Dictionary<string, string>> WaitForCallbackAsync(CancellationToken cancellationToken);
    }
}

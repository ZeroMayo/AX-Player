// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Net;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Parses the query string of an OAuth redirect, which is either a raw query string or the
    /// <see cref="Uri.Query" /> of a redirect URI (i.e. one with a leading <c>?</c>).
    /// </summary>
    public static class OAuthCallbackQuery
    {
        public static Dictionary<string, string> Parse(string? queryString)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            if (string.IsNullOrEmpty(queryString))
                return result;

            string query = queryString[0] == '?' ? queryString[1..] : queryString;

            foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = part.IndexOf('=');
                string key = equals < 0 ? part : part[..equals];
                string value = equals < 0 ? string.Empty : part[(equals + 1)..];

                result[WebUtility.UrlDecode(key)] = WebUtility.UrlDecode(value);
            }

            return result;
        }
    }
}

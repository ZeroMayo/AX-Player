// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    public partial class GoogleOAuth2
    {
        private const string callback_path = @"/authorize/";

        private const string close_page_response =
            @"<html>
  <body>
    <script>
      window.location.href = ""https://zeromayo.github.io/oauth2_success.html"";
    </script>
  </body>
</html>";

        /// <summary>
        /// Minimal loopback HTTP receiver for the OAuth 2.0 redirect.
        /// </summary>
        /// <remarks>
        /// The port is bound before the authorization URL is built, so the redirect URI is
        /// stable and there is no window where another process could take the port. Only a
        /// single <c>GET /authorize/</c> request is served, which is all the OAuth flow needs.
        /// </remarks>
        private sealed class LoopbackReceiver : IDisposable
        {
            private const int network_buffer_size = 1024;
            private const int max_request_line_length = 16 * 1024;

            private readonly TcpListener listener;

            private LoopbackReceiver(TcpListener listener, int port)
            {
                this.listener = listener;
                RedirectUri = $@"http://127.0.0.1:{port}{callback_path}";
            }

            public string RedirectUri { get; }

            public static LoopbackReceiver Start()
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();

                int port = ((IPEndPoint)listener.LocalEndpoint).Port;

                return new LoopbackReceiver(listener, port);
            }

            public async Task<Dictionary<string, string>> WaitForCallbackAsync(CancellationToken cancellationToken)
            {
                using (cancellationToken.Register(stopListener))
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        TcpClient client;

                        try
                        {
                            client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }

                        using (client)
                        {
                            Dictionary<string, string>? query = await handleRequestAsync(client, cancellationToken).ConfigureAwait(false);

                            if (query != null)
                                return query;
                        }
                    }
                }
            }

            private async Task<Dictionary<string, string>?> handleRequestAsync(TcpClient client, CancellationToken cancellationToken)
            {
                NetworkStream stream = client.GetStream();

                string requestLine = await readRequestLineAsync(stream, cancellationToken).ConfigureAwait(false);

                string[] parts = requestLine.Split(' ');

                if (parts.Length != 3)
                {
                    await writeResponseAsync(stream, 400, @"Bad request", cancellationToken).ConfigureAwait(false);
                    return null;
                }

                if (!string.Equals(parts[0], @"GET", StringComparison.Ordinal))
                {
                    await writeResponseAsync(stream, 405, @"Method not allowed", cancellationToken).ConfigureAwait(false);
                    return null;
                }

                string target = parts[1];
                string path = target;
                string queryString = string.Empty;

                int questionMark = target.IndexOf('?');

                if (questionMark >= 0)
                {
                    path = target[..questionMark];
                    queryString = target[(questionMark + 1)..];
                }

                if (!path.StartsWith(callback_path, StringComparison.Ordinal))
                {
                    // Browsers also request /favicon.ico; ignore anything that is not the callback.
                    await writeResponseAsync(stream, 404, @"Not found", cancellationToken).ConfigureAwait(false);
                    return null;
                }

                await writeResponseAsync(stream, 200, close_page_response, cancellationToken).ConfigureAwait(false);

                return parseQueryString(queryString);
            }

            private static async Task<string> readRequestLineAsync(NetworkStream stream, CancellationToken cancellationToken)
            {
                var buffer = new byte[network_buffer_size];
                var builder = new StringBuilder();

                while (true)
                {
                    int read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                    if (read == 0)
                        throw new InvalidOperationException(@"The browser closed the connection before sending the OAuth response.");

                    for (int i = 0; i < read; i++)
                    {
                        char character = (char)buffer[i];

                        if (character == '\n')
                        {
                            string line = builder.ToString();

                            if (line.EndsWith('\r'))
                                line = line[..^1];

                            return line;
                        }

                        builder.Append(character);

                        if (builder.Length > max_request_line_length)
                            throw new InvalidOperationException(@"The OAuth callback request line was too long.");
                    }
                }
            }

            private static Dictionary<string, string> parseQueryString(string queryString)
            {
                var result = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (string part in queryString.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    int equals = part.IndexOf('=');
                    string key = equals < 0 ? part : part[..equals];
                    string value = equals < 0 ? string.Empty : part[(equals + 1)..];

                    result[WebUtility.UrlDecode(key)] = WebUtility.UrlDecode(value);
                }

                return result;
            }

            private static async Task writeResponseAsync(NetworkStream stream, int statusCode, string content, CancellationToken cancellationToken)
            {
                byte[] body = Encoding.UTF8.GetBytes(content);

                string header = $@"HTTP/1.1 {statusCode} {getStatusDescription(statusCode)}" + "\r\n"
                                + @"Content-Type: text/html; charset=utf-8" + "\r\n"
                                + $@"Content-Length: {body.Length}" + "\r\n"
                                + @"Connection: close" + "\r\n\r\n";

                await stream.WriteAsync(Encoding.UTF8.GetBytes(header), cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            private static string getStatusDescription(int statusCode)
                => statusCode switch
                {
                    200 => @"OK",
                    400 => @"Bad Request",
                    404 => @"Not Found",
                    405 => @"Method Not Allowed",
                    _ => @"Error",
                };

            private void stopListener()
            {
                try
                {
                    listener.Stop();
                }
                catch
                {
                    // The listener may already be stopped.
                }
            }

            public void Dispose() => stopListener();
        }

        /// <summary>
        /// Opens a browser and navigates to a URL.
        /// </summary>
        /// <returns><c>true</c> when a browser could be launched.</returns>
        private static bool openBrowser(string url)
        {
            try
            {
                // See https://github.com/dotnet/corefx/issues/10361
                // This is best-effort only, but should work most of the time.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // See https://stackoverflow.com/a/6040946/44360 for why this is required
                    url = System.Text.RegularExpressions.Regex.Replace(url, @"(\\*)" + "\"", @"$1$1\" + "\"");
                    url = System.Text.RegularExpressions.Regex.Replace(url, @"(\\+)$", @"$1$1");
                    Process.Start(new ProcessStartInfo(@"cmd", $@"/c start """" ""{url}""") { CreateNoWindow = true });
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start(@"xdg-open", url);
                    return true;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start(@"open", url);
                    return true;
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $@"Failed to launch a browser for '{url}': {exception.GetDescription()}");
            }

            return false;
        }
    }
}

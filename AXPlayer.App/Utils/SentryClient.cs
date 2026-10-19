// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Net;
using System.Net.WebSockets;
using AXPlayer.App.Config;
using AXPlayer.App.Online;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Sentry;

namespace AXPlayer.App.Utils
{
    public partial class SentryClient : Component
    {
        private AXPlayerAppBase app { get; }
        private IDisposable session { get; }

        private GoogleOAuth2 googleOAuth2 { get; set; }

        private Bindable<bool> isLoginState { get; set; }

        private YouTubeAPI youtubeAPI { get; set; }

        public SentryClient(AXPlayerAppBase app, GoogleOAuth2 googleOAuth2, YouTubeAPI youtubeAPI, Storage? storage = null)
        {
            this.app = app;
            this.googleOAuth2 = googleOAuth2;
            this.youtubeAPI = youtubeAPI;

            Logger.NewEntry += onEntry;

            session = SentrySdk.Init(opt =>
            {
                opt.Dsn = "https://a0415fc53795c7a1bba363561bb8e41e@o4510361449988096.ingest.us.sentry.io/4510884657168384";
                opt.IsEnvironmentUser = false;
                opt.AutoSessionTracking = true;
                opt.IsGlobalModeEnabled = true;
                opt.CacheDirectoryPath = storage?.GetFullPath(string.Empty);
                opt.Release = app.Version;
            });
        }

        private void onEntry(LogEntry entry)
        {
            if (entry.Level != LogLevel.Error)
                return;

            var ex = entry.Exception;

            if (ex == null)
                return;

            if (shouldIgnore(ex))
            {
                Logger.Log($"Ignored {ex.GetType().Name}!", LoggingTarget.Runtime, LogLevel.Debug);
                return;
            }

            Logger.Log($"{ex.GetType().Name} would be reported!", LoggingTarget.Runtime, LogLevel.Debug);

            if (session is null)
                return;

            SentrySdk.CaptureEvent(new SentryEvent(ex)
            {
                Message = entry.Message,
                Level = entry.Level switch
                {
                    LogLevel.Debug => SentryLevel.Debug,
                    LogLevel.Verbose => SentryLevel.Info,
                    LogLevel.Important => SentryLevel.Warning,
                    LogLevel.Error => SentryLevel.Error,
                    _ => throw new ArgumentOutOfRangeException()
                }
            }, scope =>
            {
                scope.Contexts["config"] = new
                {
                    App = app.Dependencies.Get<AXPlayerConfigManager>()?.GetCurrentConfigurationForLogging(),
                    Framework = app.Dependencies.Get<FrameworkConfigManager>()?.GetCurrentConfigurationForLogging(),
                };

                scope.Contexts["hashes"] = new
                {
                    app = app.VersionHash,
                };

                scope.User = new SentryUser
                {
                    Username = googleOAuth2.SignedIn.Value ? youtubeAPI.GetMineChannel().Snippet.Title : "Guest User",
                    Id = googleOAuth2.SignedIn.Value ? $"{(youtubeAPI.GetMineChannel().Id ?? "{not logged in}")}" : "{not logged in}",
                };

                scope.SetTag(@"os", $"{RuntimeInfo.OS} ({Environment.OSVersion})");
                scope.SetTag(@"version hash", app.VersionHash);
                scope.SetTag(@"processor count", Environment.ProcessorCount.ToString());
            });
        }

        private bool shouldIgnore(Exception ex)
        {
            switch (ex)
            {
                case WebException web:
                    if (web.Response is HttpWebResponse { StatusCode: HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable })
                        return true;

                    return web.Status is WebExceptionStatus.Timeout or WebExceptionStatus.UnknownError;

                case WebSocketException ws:
                    return ws.WebSocketErrorCode == WebSocketError.NotAWebSocket;
            }

            return false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            Logger.NewEntry -= onEntry;
            session?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

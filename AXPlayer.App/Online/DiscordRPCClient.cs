// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using DiscordRPC;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Threading;

namespace AXPlayer.App.Online
{
    /// <summary>
    /// Keeps the Discord rich presence in sync with the player.
    /// </summary>
    /// <remarks>
    /// Discord accepts only a handful of presence updates per twenty seconds and discards the rest,
    /// so calls are coalesced here instead of being forwarded one by one: <see cref="UpdatePresence"/>
    /// just stores the newest payload and a single scheduled task sends whatever is current once the
    /// rate limit allows it again.
    /// </remarks>
    public partial class DiscordRPCClient : CompositeDrawable
    {
        public const string DISCORD_APP_ID = "1474920449442840586";

        /// <summary>
        /// Discord drops updates beyond roughly five per twenty seconds, so bursts are spread out.
        /// </summary>
        private const long minimum_update_interval_ms = 4000;

        private readonly object presenceLock = new object();

        private DiscordRpcClient client;

        private RichPresence pendingPresence;
        private bool hasPendingPresence;
        private ScheduledDelegate pendingFlush;

        private long lastUpdateTime;
        private bool disposed;

        private bool isConnected => !disposed && client != null && client.IsInitialized;

        [BackgroundDependencyLoader]
        private void load()
        {
            try
            {
                client = new DiscordRpcClient(DISCORD_APP_ID, -1)
                {
                    Logger = new DiscordRPCLogger(),
                    // Identical payloads never reach the pipe, so handlers that fire without changing
                    // anything (locale, tray, username display) stay free.
                    SkipIdenticalPresence = true,
                };

                client.OnReady += (_, e) =>
                {
                    Logger.Log($"[Discord] Connected to discord with user {e.User.Username}", LoggingTarget.Runtime);

                    // A presence requested while the connection was still being established is
                    // flushed as soon as Discord accepts one.
                    lock (presenceLock)
                        scheduleFlush();
                };

                client.OnConnectionFailed += (_, _) => Logger.Log("[Discord] Could not reach the Discord client; retrying in the background.", LoggingTarget.Runtime, LogLevel.Important);
                client.OnError += (_, e) => Logger.Log($"[Discord] {e.Message}", LoggingTarget.Runtime, LogLevel.Important);

                client.Initialize();
                Logger.Log("[Discord] Discord RPC initialized.", LoggingTarget.Runtime);
            }
            catch (Exception e)
            {
                // A missing or broken Discord installation must never take the player down with it.
                client = null;
                Logger.Error(e, "[Discord] Failed to initialise the Discord RPC client.");
            }
        }

        /// <summary>
        /// Requests the given presence. Safe to call from any thread.
        /// </summary>
        public void UpdatePresence(RichPresence richPresence) => enqueue(richPresence);

        /// <summary>
        /// Requests the presence to be cleared. Safe to call from any thread.
        /// </summary>
        public void ClearPresence() => enqueue(null);

        private void enqueue(RichPresence richPresence)
        {
            lock (presenceLock)
            {
                if (disposed)
                    return;

                pendingPresence = richPresence;
                hasPendingPresence = true;

                scheduleFlush();
            }
        }

        /// <summary>
        /// Sends the queued presence now, or schedules a single flush for when the rate limit allows.
        /// </summary>
        /// <remarks>Must be called with <see cref="presenceLock"/> held.</remarks>
        private void scheduleFlush()
        {
            if (pendingFlush != null || !isConnected)
                return;

            long delay = minimum_update_interval_ms - (Environment.TickCount64 - lastUpdateTime);

            pendingFlush = Scheduler.AddDelayed(flush, Math.Max(0, delay));
        }

        private void flush()
        {
            lock (presenceLock)
            {
                pendingFlush = null;

                if (!hasPendingPresence)
                    return;

                if (!isConnected)
                {
                    // Keep the request around for the next connection attempt (or for OnReady).
                    return;
                }

                RichPresence presence = pendingPresence;

                pendingPresence = null;
                hasPendingPresence = false;
                lastUpdateTime = Environment.TickCount64;

                try
                {
                    if (presence == null)
                        client.ClearPresence();
                    else
                        client.SetPresence(presence);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "[Discord] Failed to update the Discord rich presence.");
                }
            }
        }

        public User GetCurrentUser() => client?.CurrentUser;

        protected override void Dispose(bool isDisposing)
        {
            lock (presenceLock)
            {
                disposed = true;

                pendingFlush?.Cancel();
                pendingFlush = null;

                pendingPresence = null;
                hasPendingPresence = false;
            }

            if (isDisposing)
            {
                try
                {
                    client?.Dispose();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "[Discord] Failed to dispose the Discord RPC client.");
                }

                client = null;
            }

            base.Dispose(isDisposing);
        }
    }
}

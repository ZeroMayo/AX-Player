// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DiscordRPC.Logging;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    public class DiscordRPCLogger : ILogger
    {
        public DiscordRPC.Logging.LogLevel Level
        {
            get => field;
            set
            {
                field = value;
                Logger.Log($"[DiscordRPC] Log level set to {value}");
            }
        }

        public void Error(string message, params object[] args)
        {
            Logger.Log($"[DiscordRPC] {message}");
        }

        public void Info(string message, params object[] args)
        {
            Logger.Log($"[DiscordRPC] {message}");
        }

        public void Trace(string message, params object[] args)
        {
            Logger.Log($"[DiscordRPC] {message}");
        }

        public void Warning(string message, params object[] args)
        {
            Logger.Log($"[DiscordRPC] {message}");
        }
    }
}

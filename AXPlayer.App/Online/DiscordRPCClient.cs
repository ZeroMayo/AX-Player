// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DiscordRPC;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;

namespace AXPlayer.App.Online
{
    public partial class DiscordRPCClient : CompositeDrawable
    {
        public const string DISCORD_APP_ID = "1474920449442840586";
        private static DiscordRpcClient client;

        [BackgroundDependencyLoader]
        private void load()
        {
            // Create the client and setup some basic events
            client = new DiscordRpcClient(DISCORD_APP_ID, -1);

            client.Logger = new DiscordRPCLogger();

            client.OnReady += (sender, e) =>
            {
                Logger.Log($"[Discord] Connected to discord with user {e.User.Username}", LoggingTarget.Runtime);
                Logger.Log($"[Discord] Avatar: {e.User.GetAvatarURL(User.AvatarFormat.WebP)}", LoggingTarget.Runtime);
                Logger.Log($"[Discord] Decoration: {e.User.GetAvatarDecorationURL()}", LoggingTarget.Runtime);
            };

            //Connect to the RPC
            client.Initialize();
            Logger.Log("[Discord] Discord RPC initialized.", LoggingTarget.Runtime);
        }

        public void UpdatePresence(RichPresence richPresence) => client.SetPresence(richPresence);

        public void ClearPresence() => client.ClearPresence();

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            client.Dispose();
        }

        public User GetCurrentUser()
        {
            return client.CurrentUser;
        }
    }
}

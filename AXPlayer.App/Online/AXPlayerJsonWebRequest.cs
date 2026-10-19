// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;

namespace AXPlayer.App.Online
{
    public class AXPlayerJsonWebRequest<T> : JsonWebRequest<T>
    {
        public AXPlayerJsonWebRequest(string uri)
            : base(uri)
        {
        }

        public AXPlayerJsonWebRequest()
        {
        }

        protected override string UserAgent => "AX-Player";
    }
}

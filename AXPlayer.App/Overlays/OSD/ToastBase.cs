// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace AXPlayer.App.Overlays.OSD
{
    public partial class ToastBase : Container
    {
        private readonly Container content;

        protected override Container<Drawable> Content => content;

        public ToastBase()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }
    }
}

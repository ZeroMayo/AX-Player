// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Graphics;
using osu.Framework.Screens;

namespace AXPlayer.App.Screens
{
    public abstract partial class AXPlayerScreen : Screen, IAXPlayerScreen
    {
        protected new AXPlayerAppBase Game => base.Game as AXPlayerAppBase;

        public virtual bool CursorVisible => true;

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            this.FadeIn(250, Easing.OutQuint);
            this.ScaleTo(1.1f).Then().ScaleTo(1f, 250, Easing.OutQuint);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            this.FadeOut(250, Easing.OutQuint);
            this.ScaleTo(0.9f, 250, Easing.OutQuint);
            return base.OnExiting(e);
        }
    }
}

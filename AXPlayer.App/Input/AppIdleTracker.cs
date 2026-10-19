// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input;

namespace AXPlayer.App.Input
{
    public partial class AppIdleTracker : IdleTracker
    {
        private InputManager inputManager = null!;

        public AppIdleTracker(int time)
            : base(time)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            inputManager = GetContainingInputManager();
        }

        protected override bool AllowIdle => inputManager.FocusedDrawable == null;
    }
}

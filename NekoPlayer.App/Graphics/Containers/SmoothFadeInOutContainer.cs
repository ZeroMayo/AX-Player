// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace NekoPlayer.App.Graphics.Containers
{
    public partial class SmoothFadeInOutContainer : VisibilityContainer
    {
        public const float TRANSITION_DURATION = 500;
        private readonly Container content;
        protected override Container<Drawable> Content => content;

        public SmoothFadeInOutContainer()
        {
            InternalChild = content = new Container
            {
                AutoSizeAxes = Axes.Both,
            };
        }

        protected override void PopIn()
        {
            content.ScaleTo(1, TRANSITION_DURATION, Easing.OutQuint);
            this.FadeIn(TRANSITION_DURATION * 2, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            content.ScaleTo(0.8f, TRANSITION_DURATION / 2, Easing.In);
            this.FadeOut(TRANSITION_DURATION, Easing.OutQuint);
        }
    }
}

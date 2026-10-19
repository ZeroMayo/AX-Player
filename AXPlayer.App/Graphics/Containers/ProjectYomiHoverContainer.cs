// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osuTK.Graphics;
using AXPlayer.App.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.Containers
{
    public partial class ProjectYomiHoverContainer : ProjectYomiClickableContainer
    {
        protected const float FADE_DURATION = 500;

        public Color4? HoverColour { get; set; }
        private Color4 fallbackHoverColour;

        public Color4? IdleColour { get; set; }
        private Color4 fallbackIdleColour;

        protected virtual IEnumerable<Drawable> EffectTargets => new[] { Content };

        public ProjectYomiHoverContainer(HoverSampleSet sampleSet = HoverSampleSet.Default)
            : base(sampleSet)
        {
            Enabled.ValueChanged += e =>
            {
                if (isHovered)
                {
                    if (e.NewValue)
                        fadeIn();
                    else
                        fadeOut();
                }
            };
        }

        private bool isHovered;

        protected override bool OnHover(HoverEvent e)
        {
            if (isHovered)
                return false;

            isHovered = true;

            if (!Enabled.Value)
                return false;

            fadeIn();

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (!isHovered)
                return;

            isHovered = false;
            fadeOut();

            base.OnHoverLost(e);
        }

        [BackgroundDependencyLoader]
        private void load(ProjectYomiColour colours)
        {
            fallbackHoverColour = colours.Yellow;
            fallbackIdleColour = Color4.White;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            EffectTargets.ForEach(d => d.FadeColour(IdleColour ?? fallbackIdleColour));
        }

        private void fadeIn() => EffectTargets.ForEach(d => d.FadeColour(HoverColour ?? fallbackHoverColour, FADE_DURATION, Easing.OutQuint));

        private void fadeOut() => EffectTargets.ForEach(d => d.FadeColour(IdleColour ?? fallbackIdleColour, FADE_DURATION, Easing.OutQuint));
    }
}

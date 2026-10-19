// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProjectYomiClickableContainer : ClickableContainer, IHasTooltip
    {
        private readonly HoverSampleSet sampleSet;

        private readonly Container content = new Container { RelativeSizeAxes = Axes.Both };

        protected HoverSounds Samples = null!;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) =>
            // base call is checked for cases when `OsuClickableContainer` has masking applied to it directly (ie. externally in object initialisation).
            base.ReceivePositionalInputAt(screenSpacePos)
            // Implementations often apply masking / edge rounding at a content level, so it's imperative to check that as well.
            && Content.ReceivePositionalInputAt(screenSpacePos);

        protected override Container<Drawable> Content => content;

        protected virtual HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverClickSounds(sampleSet) { Enabled = { BindTarget = Enabled } };

        public ProjectYomiClickableContainer(HoverSampleSet sampleSet = HoverSampleSet.Default)
        {
            this.sampleSet = sampleSet;
        }

        public void TriggerClickWithSound()
        {
            TriggerClick();

            // TriggerClick doesn't recursively fire the event so we need to manually do this.
            (Samples as HoverClickSounds)?.PlayClickSample();
        }

        public virtual LocalisableString TooltipText { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (AutoSizeAxes != Axes.None)
            {
                content.RelativeSizeAxes = (Axes.Both & ~AutoSizeAxes);
                content.AutoSizeAxes = AutoSizeAxes;
            }

            AddRangeInternal(new Drawable[]
            {
                Samples = CreateHoverSounds(sampleSet),
                content,
            });
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Audio.Effects;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A single vertical slider of the 10-band equalizer.
    /// </summary>
    public partial class EqualizerBandSlider : CompositeDrawable, IHasCurrentValue<float>, IHasTooltip
    {
        public const float WIDTH = 30;
        public const float HEIGHT = 116;

        private const float track_padding = 6;
        private const float nub_size = 13;

        public Bindable<float> Current { get; set; } = new BindableNumber<float>(0f)
        {
            MinValue = TenBandEqualizer.MIN_GAIN_DB,
            MaxValue = TenBandEqualizer.MAX_GAIN_DB,
            Precision = TenBandEqualizer.GAIN_STEP_DB,
        };

        public LocalisableString TooltipText { get; private set; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private Box fill = null!;
        private Circle nub = null!;

        private float displayedGain;
        private int lastTooltipGainTenths = int.MinValue;

        public EqualizerBandSlider()
        {
            Width = WIDTH;
            Height = HEIGHT;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Width = 4,
                    RelativeSizeAxes = Axes.Y,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Margin = new MarginPadding { Vertical = track_padding },
                    Colour = colourProvider.Background4,
                },
                new Box
                {
                    Height = 1,
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = colourProvider.Content2,
                    Alpha = 0.35f,
                },
                fill = new Box
                {
                    Width = 4,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = colourProvider.Highlight1,
                    Alpha = 0,
                },
                nub = new Circle
                {
                    Size = new Vector2(nub_size),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = colourProvider.Content1,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(gain => displayedGain = gain.NewValue, true);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            float usableHeight = Math.Max(0, DrawHeight - track_padding * 2);
            float ratio = Math.Clamp(displayedGain / TenBandEqualizer.MAX_GAIN_DB, -1f, 1f);
            float offset = ratio * (usableHeight / 2f);

            nub.Y = -offset;

            fill.Height = Math.Abs(offset);
            fill.Origin = ratio >= 0 ? Anchor.BottomCentre : Anchor.TopCentre;
            fill.Alpha = Math.Abs(offset) < 0.5f ? 0 : 1;

            int gainTenths = (int)MathF.Round(displayedGain * 10);

            if (gainTenths != lastTooltipGainTenths)
            {
                lastTooltipGainTenths = gainTenths;
                TooltipText = $"{displayedGain:+0.0;-0.0;0.0} dB";
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            setFromPosition(ToLocalSpace(e.ScreenSpaceMousePosition));
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            setFromPosition(ToLocalSpace(e.ScreenSpaceMousePosition));
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            setFromPosition(ToLocalSpace(e.ScreenSpaceMousePosition));
            base.OnDrag(e);
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            Current.Value = 0;
            return true;
        }

        private void setFromPosition(Vector2 localPosition)
        {
            float usableHeight = Math.Max(1, DrawHeight - track_padding * 2);
            float ratio = Math.Clamp((DrawHeight / 2f - localPosition.Y) / (usableHeight / 2f), -1f, 1f);

            Current.Value = ratio * TenBandEqualizer.MAX_GAIN_DB;
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK.Graphics;
using System;
using osu.Framework.Localisation;
using osu.Framework.Bindables;
using AXPlayer.App.Graphics.Sprites;
using osu.Framework.Allocation;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProjectYomiButton : ProjectYomiClickableContainer
    {
        public Action<ProjectYomiButton>? ClickAction { get; set; }

        public LocalisableString Text
        {
            get => SpriteText.Text;
            set => SpriteText.Text = value;
        }

        public Color4 BackgroundColour
        {
            get => Background.Colour;
            set => Background.FadeColour(value, 500, Easing.OutQuint);
        }

        private Color4? flashColour;

        /// <summary>
        /// The colour the background will flash with when this button is clicked.
        /// </summary>
        public Color4 FlashColour
        {
            get => flashColour ?? BackgroundColour;
            set => flashColour = value;
        }

        /// <summary>
        /// The additive colour that is applied to the background when hovered.
        /// </summary>
        public Color4 HoverColour
        {
            get => Hover.Colour;
            set => Hover.FadeColour(value);
        }

        private Color4 disabledColour = Color4.Gray;

        /// <summary>
        /// The additive colour that is applied to this button when disabled.
        /// </summary>
        public Color4 DisabledColour
        {
            get => disabledColour;
            set
            {
                if (disabledColour == value)
                    return;

                disabledColour = value;
                Enabled.TriggerChange();
            }
        }

        /// <summary>
        /// The duration of the transition when hovering.
        /// </summary>
        public double HoverFadeDuration { get; set; } = 200;

        /// <summary>
        /// The duration of the flash when this button is clicked.
        /// </summary>
        public double FlashDuration { get; set; } = 200;

        /// <summary>
        /// The duration of the transition when toggling the Enabled state.
        /// </summary>
        public double DisabledFadeDuration { get; set; } = 200;

        protected Box Hover;
        protected Box Background;
        protected SpriteText SpriteText;
        private readonly Container content;

        protected Container ForegroundContent;

        public ProjectYomiButton(HoverSampleSet hoverSampleSet = HoverSampleSet.Default)
            : base(hoverSampleSet)
        {
            base.Content.Add(content = new Container
            {
                Origin = Anchor.Centre,
                Anchor = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                CornerRadius = 12,
                Masking = true,
                Children = new Drawable[]
                {
                    Background = new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 1,
                    },
                    Hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Blending = BlendingParameters.Additive,
                        Alpha = 0,
                    },
                    ForegroundContent = new Container
                    {
                        Origin = Anchor.Centre,
                        Anchor = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Child = SpriteText = CreateText()
                    }
                }
            });

            Enabled.BindValueChanged(enabledChanged, true);
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            Background.Colour = BackgroundColour;
            SpriteText.Colour = overlayColourProvider.Content2;
            content.CornerRadius = DrawHeight / 2;
        }

        protected virtual SpriteText CreateText() => new ProjectYomiSpriteText
        {
            Depth = -1,
            Origin = Anchor.Centre,
            Anchor = Anchor.Centre,
            Font = AXPlayerApp.DefaultFont.With(size: 24),
            Colour = Color4.White,
        };

        protected override bool OnHover(HoverEvent e)
        {
            Hover.FadeTo(0.1f, 500, Easing.OutQuint);

            return base.OnHover(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            content.ScaleTo(0.9f, 2000, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            content.ScaleTo(1, 1000, Easing.OutElastic);
            base.OnMouseUp(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            Hover.FadeOut(500, Easing.OutQuint);
        }

        private void enabledChanged(ValueChangedEvent<bool> e)
        {
            this.FadeColour(e.NewValue ? Color4.White : DisabledColour, DisabledFadeDuration, Easing.OutQuint);
        }

        private void trigger()
        {
            if (Enabled.Value)
                ClickAction?.Invoke(this);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (!IsHovered)
                Hover.FadeOutFromOne(1600);

            Hover.FlashColour(FlashColour, 800, Easing.OutQuint);
            trigger();

            return base.OnClick(e);
        }
    }
}

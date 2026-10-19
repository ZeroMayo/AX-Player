// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public partial class SettingsRevertToDefaultButton : ProjectYomiClickableContainer
    {
        public const float WIDTH = 28;

        public float IconSize { get; init; } = 10;

        private Box background = null!;
        private Box hover = null!;
        private SpriteIcon spriteIcon = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        // this is done to ensure a click on this button doesn't trigger focus on a parent element which contains the button.
        public override bool AcceptsFocus => true;

        public SettingsRevertToDefaultButton()
        {
            Width = Height = WIDTH;
            Position = new Vector2(WIDTH, 0);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Masking = true;
            CornerRadius = DrawHeight / 2;

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                spriteIcon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = colourProvider.Content2,
                    Icon = FontAwesome.Solid.Undo,
                    Size = new Vector2(IconSize),
                },
                hover = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Enabled.BindValueChanged(_ => updateDisplay(), true);
        }

        public override LocalisableString TooltipText => AXPlayerStrings.RevertToDefault;

        protected override bool OnHover(HoverEvent e)
        {
            updateDisplay();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateDisplay();
            base.OnHoverLost(e);
        }

        public override void Show()
        {
            this.FadeIn(200, Easing.OutQuint);
        }

        public override void Hide()
        {
            this.FadeOut(200, Easing.OutQuint);
        }

        private void updateDisplay()
        {
            hover.FadeTo(IsHovered ? 0.2f : 0f, 300, Easing.OutQuint);
        }
    }
}

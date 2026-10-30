// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public partial class FormGoogleOAuthButton : CompositeDrawable, IFormControl
    {
        /// <summary>
        /// Caption describing this button, displayed on the left of it.
        /// </summary>
        public LocalisableString Caption { get; init; }

        private LocalisableString textValue;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        /// <summary>
        /// Caption describing this button, displayed on the left of it.
        /// </summary>
        public LocalisableString Text
        {
            get => textValue;
            set
            {
                if (textValue.Equals(value))
                    return;

                textValue = value;

                if (text != null)
                    text.Text = value;
            }
        }

        /// <summary>
        /// Sets text inside the button.
        /// </summary>
        public LocalisableString ButtonText { get; init; }

        /// <summary>
        /// Hint text containing an extended description of this slider bar, displayed in a tooltip when hovering the caption.
        /// </summary>
        public LocalisableString HintText { get; init; }

        public FormControlCornerType CornerType { get; init; }

        public IconUsage Icon { get; set; }

        /// <summary>
        /// Sets a custom button icon. Not shown when <see cref="ButtonText"/> is set.
        /// </summary>
        public IconUsage ButtonIcon { get; init; } = FontAwesome.Solid.ChevronRight;

        private FormControlBackgroundWithProfileImage background = null!;
        private FormFieldCaption caption = null!;
        private ProjectYomiSpriteText text = null!;

        /// <summary>
        /// The action to invoke when the button is clicked.
        /// </summary>
        public Action? Action { get; set; }

        /// <summary>
        /// Whether the button is enabled.
        /// </summary>
        public readonly BindableBool Enabled = new BindableBool(true);

        private Button button = null!;

        event Action IFormControl.ValueChanged
        {
            add => throw new NotImplementedException();
            remove => throw new NotImplementedException();
        }

        public void UpdateLoginState()
        {
            background.UpdateLoginState();
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            Masking = true;
            CornerRadius = 5;
            CornerExponent = 2.5f;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackgroundWithProfileImage(CornerType),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(9),
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Right = 56 + 5 },
                            Spacing = new Vector2(0f, 4f),
                            Children = new Drawable[]
                            {
                                caption = new FormFieldCaption
                                {
                                    Caption = Caption,
                                    TooltipText = HintText,
                                    Icon = Icon,
                                },
                                text = new ProjectYomiSpriteText
                                {
                                    Text = Text,
                                    RelativeSizeAxes = Axes.X,
                                },
                            },
                        },
                        button = new Button
                        {
                            Action = () => Action?.Invoke(),
                            Text = ButtonText,
                            Icon = ButtonIcon,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Enabled = { BindTarget = Enabled },
                            Height = 30,
                        },
                    },
                },
            };

            if (ButtonText == default)
            {
                text.Padding = new MarginPadding { Right = 100 };
                button.Width = 90;
            }
            else
            {
                text.Width = 0.55f;
                text.Padding = new MarginPadding { Right = 10 };
                button.RelativeSizeAxes = Axes.X;
                button.Width = 0.45f;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        private void updateState()
        {
            caption.TextColour = colourProvider.Content2;
            text.Colour = Color4.White;

            if (!Enabled.Value)
                background.VisualStyle = VisualStyle.Disabled;
            else if (IsHovered)
                background.VisualStyle = VisualStyle.Hovered;
            else
                background.VisualStyle = VisualStyle.Normal;
        }

        public IEnumerable<LocalisableString> FilterTerms => Caption.Yield();

        public bool IsDefault => true;

        public void SetDefault()
        {
        }

        void IFormControl.SetDefault()
        {
            throw new NotImplementedException();
        }

        public bool IsDisabled => false;

        public partial class Button : ProjectYomiButtonV2
        {
            protected override float HoverLayerFinalAlpha => 0;

            private Color4? triangleGradientSecondColour;

            public override Color4 BackgroundColour
            {
                get => base.BackgroundColour;
                set
                {
                    base.BackgroundColour = value;
                    triangleGradientSecondColour = BackgroundColour.Lighten(0.2f);
                }
            }

            public IconUsage Icon { get; init; }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider overlayColourProvider)
            {
                DefaultBackgroundColour = overlayColourProvider.Colour3;
                triangleGradientSecondColour ??= DefaultBackgroundColour.Lighten(0.2f);

                if (Text == default)
                {
                    Add(new SpriteIcon
                    {
                        Icon = Icon,
                        Size = new Vector2(16),
                        Shadow = true,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    });
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Content.CornerRadius = DrawHeight / 2;
            }

            protected override bool OnHover(HoverEvent e)
            {
                Debug.Assert(triangleGradientSecondColour != null);

                Background.FadeColour(triangleGradientSecondColour.Value, 300, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                Background.FadeColour(BackgroundColour, 300, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}

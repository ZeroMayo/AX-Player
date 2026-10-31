// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using AXPlayer.App.Graphics.Sprites;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class DrawableAdaptiveMenuItem : Menu.DrawableMenuItem
    {
        public const int MARGIN_HORIZONTAL = 10;
        public const int MARGIN_VERTICAL = 4;
        public const int TEXT_SIZE = 17;
        public const int TRANSITION_LENGTH = 80;

        public BindableBool ShowCheckbox { get; } = new BindableBool();

        private TextContainer text;
        private HoverClickSounds hoverClickSounds;

        public DrawableAdaptiveMenuItem(MenuItem item)
            : base(item)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            BackgroundColour = Color4.Transparent;
            BackgroundColourHover = overlayColourProvider.Background5;

            AddInternal(hoverClickSounds = new HoverClickSounds());

            updateText();

            if (showChevron)
            {
                AddInternal(new SpriteIcon
                {
                    Margin = new MarginPadding { Horizontal = 10, },
                    Size = new Vector2(8),
                    Icon = FontAwesome.Solid.ChevronRight,
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                });
            }
        }

        // Only add right chevron if direction of menu items is vertical (i.e. width is relative size, see `DrawableMenuItem.SetFlowDirection()`).
        private bool showChevron => Item.Items.Any() && RelativeSizeAxes == Axes.X;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ShowCheckbox.BindValueChanged(_ => updateState());
            Item.Action.BindDisabledChanged(_ => updateState(), true);
            FinishTransforms();
        }

        private void updateText()
        {
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        private void updateState()
        {
            hoverClickSounds.Enabled.Value = IsActionable;
            Alpha = IsActionable ? 1 : 0.2f;

            if (IsHovered && IsActionable)
            {
                text.BoldText.FadeIn(TRANSITION_LENGTH, Easing.OutQuint);
                text.NormalText.FadeOut(TRANSITION_LENGTH, Easing.OutQuint);
            }
            else
            {
                text.BoldText.FadeOut(TRANSITION_LENGTH, Easing.OutQuint);
                text.NormalText.FadeIn(TRANSITION_LENGTH, Easing.OutQuint);
            }

            text.CheckboxContainer.Alpha = ShowCheckbox.Value ? 1 : 0;
        }

        protected sealed override Drawable CreateContent() => text = CreateTextContainer();
        protected virtual TextContainer CreateTextContainer() => new TextContainer();

        protected partial class TextContainer : Container, IHasText
        {
            public LocalisableString Text
            {
                get => NormalText.Text;
                set
                {
                    NormalText.Text = value;
                    BoldText.Text = value;
                }
            }

            public readonly SpriteText NormalText;
            public readonly SpriteText BoldText;
            public readonly Container CheckboxContainer;

            public TextContainer()
            {
                AutoSizeAxes = Axes.Both;

                Child = new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,

                    AutoSizeAxes = Axes.Both,
                    Spacing = new Vector2(10),
                    Direction = FillDirection.Horizontal,
                    Padding = new MarginPadding { Horizontal = MARGIN_HORIZONTAL, Vertical = MARGIN_VERTICAL, },

                    Children = new Drawable[]
                    {
                        CheckboxContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = MARGIN_HORIZONTAL,
                        },
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                NormalText = new ProjectYomiSpriteText
                                {
                                    AlwaysPresent = true, // ensures that the menu item does not change width when switching between normal and bold text.
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = AXPlayerApp.DefaultFont.With(size: TEXT_SIZE),
                                },
                                BoldText = new ProjectYomiSpriteText
                                {
                                    AlwaysPresent = true, // ensures that the menu item does not change width when switching between normal and bold text.
                                    Alpha = 0,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = AXPlayerApp.DefaultFont.With(size: TEXT_SIZE, weight: "Bold"),
                                }
                            }
                        },
                    }
                };
            }
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Text;
using osuTK;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ShearedSearchTextBox : CompositeDrawable, IHasCurrentValue<string>
    {
        private const float corner_radius = 7;

        private readonly Box background;
        protected readonly InnerSearchTextBox TextBox;

        public Bindable<string> Current
        {
            get => TextBox.Current;
            set => TextBox.Current = value;
        }

        public bool HoldFocus
        {
            get => TextBox.HoldFocus;
            set => TextBox.HoldFocus = value;
        }

        public LocalisableString PlaceholderText
        {
            get => TextBox.PlaceholderText;
            set => TextBox.PlaceholderText = value;
        }

        public new bool HasFocus => TextBox.HasFocus;

        public void TakeFocus() => TextBox.TakeFocus();

        public void KillFocus() => TextBox.KillFocus();

        public bool SelectAll() => TextBox.SelectAll();

        public ShearedSearchTextBox()
        {
            Height = 42;
            Shear = new Vector2(0.2f, 0);
            Masking = true;
            CornerRadius = corner_radius;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            TextBox = CreateInnerTextBox(),
                            new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.Search,
                                Origin = Anchor.Centre,
                                Anchor = Anchor.Centre,
                                Size = new Vector2(16),
                                Shear = -Shear
                            }
                        }
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 50),
                    }
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background3;
        }

        public override bool HandleNonPositionalInput => TextBox.HandleNonPositionalInput;

        protected virtual InnerSearchTextBox CreateInnerTextBox() => new InnerSearchTextBox();

        protected partial class InnerSearchTextBox : SearchTextBox
        {
            public InnerSearchTextBox()
            {
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                RelativeSizeAxes = Axes.Both;
                Size = Vector2.One;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                BackgroundFocused = colourProvider.Background4;
                BackgroundUnfocused = colourProvider.Background4;

                Placeholder.Font = AXPlayerApp.DefaultFont.With(size: FontSize, weight: "SemiBold");
                PlaceholderText = AXPlayerStrings.Search;

                CornerRadius = corner_radius;
                TextContainer.Shear = new Vector2(-0.2f, 0);
            }

            protected override SpriteText CreatePlaceholder() => new SearchPlaceholder();

            internal partial class SearchPlaceholder : SpriteText
            {
                public override void Show()
                {
                    this
                        .MoveToY(0, 250, Easing.OutQuint)
                        .FadeIn(250, Easing.OutQuint);
                }

                public override void Hide()
                {
                    this
                        .MoveToY(3, 250, Easing.OutQuint)
                        .FadeOut(250, Easing.OutQuint);
                }
            }

            protected override Drawable GetDrawableCharacter(Grapheme c) => new FallingDownContainer
            {
                AutoSizeAxes = Axes.Both,
                Child = new ProjectYomiSpriteText { Text = c.ToString(), Font = AXPlayerApp.DefaultFont.With(size: 20, weight: "SemiBold") },
            };
        }
    }
}

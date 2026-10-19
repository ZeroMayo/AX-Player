// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Diagnostics;
using System.Linq;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Input.Binding;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProjectYomiDropdown<T> : Dropdown<T>, IKeyBindingHandler<GlobalAction>
    {
        private const float corner_radius = AXPlayerApp.UI_CORNER_RADIUS / 2.5f;

        protected override DropdownHeader CreateHeader() => new AdaptiveDropdownHeader();

        protected override DropdownMenu CreateMenu() => new AdaptiveDropdownMenu();

        public ProjectYomiDropdown()
        {
            if (Header is AdaptiveDropdownHeader osuHeader)
                osuHeader.Dropdown = this;
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat) return false;

            if (e.Action == GlobalAction.Back)
                return Back();

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        #region AdaptiveDropdownMenu

        public partial class AdaptiveDropdownMenu : DropdownMenu
        {
            public override bool HandleNonPositionalInput => State == MenuState.Open;

            private Sample? sampleOpen;
            private Sample? sampleClose;

            [Resolved]
            private AXPlayerConfigManager config { get; set; }

            // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
            public AdaptiveDropdownMenu()
            {
                CornerRadius = corner_radius;

                MaskingContainer.CornerRadius = corner_radius;
                Alpha = 0;

                // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
                ItemsContainer.Padding = new MarginPadding(5);
            }

            [BackgroundDependencyLoader(true)]
            private void load(OverlayColourProvider? colourProvider, ProjectYomiColour colours, AudioManager audio)
            {
                BackgroundColour = colourProvider?.Background5 ?? Color4.Black;
                HoverColour = colourProvider?.Light4 ?? colours.PinkDarker;
                SelectionColour = colourProvider?.Background3 ?? colours.PinkDarker.Opacity(0.5f);

                sampleOpen = audio.Samples.Get(@"UI/dropdown-open");
                sampleClose = audio.Samples.Get(@"UI/dropdown-close");
            }

            // todo: this shouldn't be required after https://github.com/ppy/osu-framework/issues/4519 is fixed.
            private bool wasOpened;

            // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
            protected override void AnimateOpen()
            {
                wasOpened = true;
                this.FadeIn(300, Easing.OutQuint);

                if (config.Get<bool>(AXPlayerSetting.PlayOverlaySFX))
                    sampleOpen?.Play();
            }

            protected override void AnimateClose()
            {
                if (wasOpened)
                {
                    this.FadeOut(300, Easing.OutQuint);

                    if (config.Get<bool>(AXPlayerSetting.PlayOverlaySFX))
                        sampleClose?.Play();
                }
            }

            // todo: this uses the same styling as OsuMenu. hopefully we can just use OsuMenu in the future with some refactoring
            protected override void UpdateSize(Vector2 newSize)
            {
                if (Direction == Direction.Vertical)
                {
                    Width = newSize.X;
                    this.ResizeHeightTo(newSize.Y, 300, Easing.OutQuint);
                }
                else
                {
                    Height = newSize.Y;
                    this.ResizeWidthTo(newSize.X, 300, Easing.OutQuint);
                }
            }

            private Color4 hoverColour;

            public Color4 HoverColour
            {
                get => hoverColour;
                set
                {
                    hoverColour = value;
                    foreach (var c in Children.OfType<DrawableAdaptiveDropdownMenuItem>())
                        c.BackgroundColourHover = value;
                }
            }

            private Color4 selectionColour;

            public Color4 SelectionColour
            {
                get => selectionColour;
                set
                {
                    selectionColour = value;
                    foreach (var c in Children.OfType<DrawableAdaptiveDropdownMenuItem>())
                        c.BackgroundColourSelected = value;
                }
            }

            protected override Menu CreateSubMenu() => new ProjectYomiMenu(Direction.Vertical);

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new DrawableAdaptiveDropdownMenuItem(item)
            {
                BackgroundColourHover = HoverColour,
                BackgroundColourSelected = SelectionColour
            };

            protected override ScrollContainer<Drawable> CreateScrollContainer(Direction direction) => new ProjectYomiScrollContainer(direction);

            #region DrawableAdaptiveDropdownMenuItem

            public partial class DrawableAdaptiveDropdownMenuItem : DrawableDropdownMenuItem
            {
                // IsHovered is used
                public override bool HandlePositionalInput => true;

                public new Color4 BackgroundColourHover
                {
                    get => base.BackgroundColourHover;
                    set
                    {
                        base.BackgroundColourHover = value;
                        updateColours();
                    }
                }

                public new Color4 BackgroundColourSelected
                {
                    get => base.BackgroundColourSelected;
                    set
                    {
                        base.BackgroundColourSelected = value;
                        updateColours();
                    }
                }

                private void updateColours()
                {
                    BackgroundColour = BackgroundColourHover.Opacity(0);

                    UpdateBackgroundColour();
                    UpdateForegroundColour();
                }

                public DrawableAdaptiveDropdownMenuItem(MenuItem item)
                    : base(item)
                {
                    Foreground.Padding = new MarginPadding(2);
                    Foreground.AutoSizeAxes = Axes.Y;
                    Foreground.RelativeSizeAxes = Axes.X;

                    Masking = true;
                    CornerRadius = corner_radius;
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    AddInternal(new HoverSounds());
                }

                protected override void UpdateBackgroundColour()
                {
                    Background.FadeColour(IsPreSelected ? BackgroundColourHover : BackgroundColourSelected, 100, Easing.OutQuint);

                    if (IsPreSelected || IsSelected)
                        Background.FadeIn(100, Easing.OutQuint);
                    else
                        Background.FadeOut(600, Easing.OutQuint);
                }

                protected override void UpdateForegroundColour()
                {
                    base.UpdateForegroundColour();

                    if (Foreground.Children.FirstOrDefault() is Content content)
                        content.Hovering = IsHovered;
                }

                protected override Drawable CreateContent() => new Content();

                protected new partial class Content : CompositeDrawable, IHasText
                {
                    public LocalisableString Text
                    {
                        get => Label.Text;
                        set => Label.Text = value;
                    }

                    public readonly ProjectYomiSpriteText Label;
                    public readonly SpriteIcon Chevron;

                    private const float chevron_offset = -3;

                    public Content()
                    {
                        RelativeSizeAxes = Axes.X;
                        AutoSizeAxes = Axes.Y;

                        InternalChildren = new Drawable[]
                        {
                            Chevron = new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.ChevronRight,
                                Size = new Vector2(8),
                                Alpha = 0,
                                X = chevron_offset,
                                Y = 1,
                                Margin = new MarginPadding { Left = 3, Right = 3 },
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                            },
                            Label = new TruncatingSpriteText
                            {
                                Padding = new MarginPadding { Left = 15 },
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                            },
                        };
                    }

                    [BackgroundDependencyLoader(true)]
                    private void load(OverlayColourProvider? colourProvider)
                    {
                        Chevron.Colour = colourProvider?.Background5 ?? Color4.Black;
                    }

                    private bool hovering;

                    public bool Hovering
                    {
                        get => hovering;
                        set
                        {
                            if (value == hovering)
                                return;

                            hovering = value;

                            if (hovering)
                            {
                                Chevron.FadeIn(400, Easing.OutQuint);
                                Chevron.MoveToX(0, 400, Easing.OutQuint);
                            }
                            else
                            {
                                Chevron.FadeOut(200);
                                Chevron.MoveToX(chevron_offset, 200, Easing.In);
                            }
                        }
                    }
                }
            }

            #endregion
        }

        #endregion

        public partial class AdaptiveDropdownHeader : DropdownHeader
        {
            protected readonly SpriteText Text;

            protected override LocalisableString Label
            {
                get => Text.Text;
                set => Text.Text = value;
            }

            protected readonly SpriteIcon Chevron;

            public ProjectYomiDropdown<T>? Dropdown { get; set; }

            public AdaptiveDropdownHeader()
            {
                Foreground.Padding = new MarginPadding(10);

                AutoSizeAxes = Axes.None;
                Margin = new MarginPadding { Bottom = 4 };
                CornerRadius = corner_radius;
                Height = 40;

                Foreground.Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            Text = new TruncatingSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                            },
                            Chevron = new SpriteIcon
                            {
                                Icon = FontAwesome.Solid.ChevronDown,
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Size = new Vector2(10),
                                Margin = new MarginPadding { Right = 2 },
                            },
                        }
                    }
                };

                AddInternal(new HoverClickSounds());
            }

            [Resolved]
            private OverlayColourProvider? colourProvider { get; set; }

            [Resolved]
            private ProjectYomiColour colours { get; set; } = null!;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (Dropdown != null)
                    Dropdown.Menu.StateChanged += _ => updateChevron();

                SearchBar.State.ValueChanged += _ => updateColour();
                Enabled.BindValueChanged(_ => updateColour());
                updateColour();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateColour();
                return false;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateColour();
            }

            private void updateColour()
            {
                bool hovered = Enabled.Value && IsHovered;
                var hoveredColour = colourProvider?.Light4 ?? colours.PinkDarker;
                var unhoveredColour = colourProvider?.Background5 ?? Color4.Black;

                Colour = Color4.White;
                Alpha = Enabled.Value ? 1 : 0.3f;

                if (SearchBar.State.Value == Visibility.Visible)
                {
                    Chevron.Colour = hovered ? hoveredColour.Lighten(0.5f) : Colour4.White;
                    Background.Colour = unhoveredColour;
                }
                else
                {
                    Chevron.Colour = Color4.White;
                    Background.Colour = hovered ? hoveredColour : unhoveredColour;
                }
            }

            private void updateChevron()
            {
                Debug.Assert(Dropdown != null);
                bool open = Dropdown.Menu.State == MenuState.Open;
                Chevron.ScaleTo(open ? new Vector2(1f, -1f) : Vector2.One, 300, Easing.OutQuint);
            }

            protected override DropdownSearchBar CreateSearchBar() => new OsuDropdownSearchBar
            {
                Padding = new MarginPadding { Right = 26 },
            };

            private partial class OsuDropdownSearchBar : DropdownSearchBar
            {
                protected override void PopIn() => this.FadeIn();

                protected override void PopOut() => this.FadeOut();

                protected override TextBox CreateTextBox() => new DropdownSearchTextBox
                {
                    FontSize = AXPlayerApp.DefaultFont.Size,
                };

                private partial class DropdownSearchTextBox : ProjectYomiTextBox
                {
                    public DropdownSearchTextBox()
                    {
                        PlaceholderText = "search...";
                    }

                    [BackgroundDependencyLoader]
                    private void load(OverlayColourProvider? colourProvider)
                    {
                        BackgroundUnfocused = colourProvider?.Background5 ?? new Color4(10, 10, 10, 255);
                        BackgroundFocused = colourProvider?.Background5 ?? new Color4(10, 10, 10, 255);
                    }

                    protected override void OnFocus(FocusEvent e)
                    {
                        base.OnFocus(e);
                        BorderThickness = 0;
                    }
                }
            }
        }
    }
}

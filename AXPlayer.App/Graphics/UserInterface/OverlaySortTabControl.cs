// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using JetBrains.Annotations;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class OverlaySortTabControl<T> : CompositeDrawable, IHasCurrentValue<T>
    {
        public TabControl<T> TabControl { get; }

        private readonly BindableWithCurrent<T> current = new BindableWithCurrent<T>();

        public Bindable<T> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        public LocalisableString Title
        {
            get => text.Text;
            set => text.Text = value;
        }

        private readonly ProjectYomiSpriteText text;

        private Box bg;

        public OverlaySortTabControl()
        {
            AutoSizeAxes = Axes.Both;
            AddInternal(new Container
            {
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4, 0),
                        Children = new Drawable[]
                        {
                            text = new ProjectYomiSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Font = AXPlayerApp.DefaultFont.With(size: 12, weight: "Bold"),
                                Text = AXPlayerStrings.SortDefault
                            },
                            new CircularContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Masking = true,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                EdgeEffect = new osu.Framework.Graphics.Effects.EdgeEffectParameters
                                {
                                    Type = osu.Framework.Graphics.Effects.EdgeEffectType.Shadow,
                                    Colour = Color4.Black.Opacity(0.25f),
                                    Offset = new Vector2(0, 2),
                                    Radius = 16,
                                },
                                Children = new Drawable[]
                                {
                                    bg = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                    },
                                    TabControl = CreateControl().With(c =>
                                    {
                                        c.Anchor = Anchor.Centre;
                                        c.Origin = Anchor.Centre;
                                        c.Current = current;
                                        c.Margin = new MarginPadding()
                                        {
                                            Horizontal = 4,
                                            Vertical = 4,
                                        };
                                    })
                                }
                            }
                        }
                    }
                }
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            bg.Colour = overlayColourProvider.Background4;
        }

        [NotNull]
        protected virtual SortTabControl CreateControl() => new SortTabControl();

        protected partial class SortTabControl : ProjectYomiTabControl<T>
        {
            protected override Dropdown<T> CreateDropdown() => null;

            protected override TabItem<T> CreateTabItem(T value) => new SortTabItem(value);

            protected override TabFillFlowContainer CreateTabFlow() => new TabFillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(2, 0),
            };

            public SortTabControl()
            {
                AutoSizeAxes = Axes.Both;
            }
        }

        protected partial class SortTabItem : TabItem<T>
        {
            public SortTabItem(T value)
                : base(value)
            {
                AutoSizeAxes = Axes.Both;
                Child = CreateTabButton(value);
            }

            [NotNull]
            protected virtual TabButton CreateTabButton(T value) => new TabButton(value)
            {
                Active = { BindTarget = Active }
            };

            protected override void OnActivated()
            {
            }

            protected override void OnDeactivated()
            {
            }
        }

        public partial class TabButton : SortButton
        {
            public readonly BindableBool Active = new BindableBool();

            protected override Container<Drawable> Content => content;

            protected virtual Color4 ContentColour
            {
                set => text.Colour = value;
            }

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; }

            private readonly SpriteText text;
            private readonly FillFlowContainer content;

            public TabButton(T value)
            {
                base.Content.Add(content = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(0, 0),
                    Padding = new MarginPadding(8),
                    Children = new Drawable[]
                    {
                        text = new ProjectYomiSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = AXPlayerApp.DefaultFont.With(size: 12, weight: "Regular"),
                            Text = (value as Enum)?.GetLocalisableDescription() ?? value.ToString()
                        }
                    }
                });

                AddInternal(new HoverClickSounds(HoverSampleSet.TabSelect));
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Active.BindValueChanged(_ => UpdateState(), true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                UpdateState();
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e) => UpdateState();

            protected virtual void UpdateState()
            {
                if (Active.Value || IsHovered)
                    ShowBackground();
                else
                    HideBackground();

                if (Active.Value)
                    ItemFocused(IsHovered);

                ContentColour = Active.Value ? colourProvider.Light1 : Color4.White;

                text.Font = text.Font.With(weight: Active.Value ? "ExtraBold" : "SemiBold");
            }
        }
    }
}

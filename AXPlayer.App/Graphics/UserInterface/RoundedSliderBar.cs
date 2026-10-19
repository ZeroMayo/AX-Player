// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Numerics;
using osuTK.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using Vector2 = osuTK.Vector2;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class RoundedSliderBar<T> : ProjectYomiSliderBar<T>
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        protected readonly SliderNubRemake Nub;
        protected readonly Box LeftBox;
        protected readonly Box RightBox;
        protected readonly Container LeftBoxContainer;
        protected readonly Container RightBoxContainer;
        protected readonly Circle EndCircle;
        private readonly Container nubContainer;

        private readonly Container mainContent;

        private Color4 accentColour;

        public Color4 AccentColour
        {
            get => accentColour;
            set
            {
                accentColour = value;
                LeftBox.Colour = value;
                Nub.Colour = value;
            }
        }

        private Colour4 backgroundColour;

        public Color4 BackgroundColour
        {
            get => backgroundColour;
            set
            {
                backgroundColour = value;
                RightBox.Colour = value;
            }
        }

        /// <summary>
        /// The action to use to reset the value of <see cref="SliderBar{T}.Current"/> to the default.
        /// Triggered on double click.
        /// </summary>
        public Action ResetToDefault { get; internal set; }

        public RoundedSliderBar()
        {
            Height = SliderNubRemake.HEIGHT;
            RangePadding = SliderNubRemake.DEFAULT_EXPANDED_SIZE / 2;
            ResetToDefault = () =>
            {
                if (!Current.Disabled)
                    Current.SetDefault();
            };
            Children = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Horizontal = 2 },
                    Child = mainContent = new CircularContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            LeftBoxContainer = new Container
                            {
                                Height = SliderNubRemake.HEIGHT / 2.5f,
                                AutoSizeAxes = Axes.X,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Masking = true,
                                CornerRadius = new CornersInfo((SliderNubRemake.HEIGHT / 2.5f) / 2, (SliderNubRemake.HEIGHT / 2.5f) / 2, (SliderNubRemake.HEIGHT / 2.5f) / 3, (SliderNubRemake.HEIGHT / 2.5f) / 3),
                                Children = new Drawable[] {
                                    LeftBox = new Box
                                    {
                                        Height = SliderNubRemake.HEIGHT / 2.5f,
                                        Colour = AccentColour,
                                        RelativeSizeAxes = Axes.None,
                                    },
                                },
                            },
                            RightBoxContainer = new Container
                            {
                                Height = SliderNubRemake.HEIGHT / 2.5f,
                                AutoSizeAxes = Axes.X,
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Masking = true,
                                CornerRadius = new CornersInfo((SliderNubRemake.HEIGHT / 2.5f) / 3, (SliderNubRemake.HEIGHT / 2.5f) / 3, (SliderNubRemake.HEIGHT / 2.5f) / 2, (SliderNubRemake.HEIGHT / 2.5f) / 2),
                                Children = new Drawable[] {
                                    RightBox = new Box
                                    {
                                        Height = SliderNubRemake.HEIGHT / 2.5f,
                                        Colour = backgroundColour,
                                        RelativeSizeAxes = Axes.None,
                                    },
                                },
                             },
                        },
                    },
                },
                nubContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Nub = new SliderNub
                    {
                        Origin = Anchor.TopCentre,
                        Colour = AccentColour,
                        RelativePositionAxes = Axes.X,
                        Current = { Value = true },
                        OnDoubleClicked = () => ResetToDefault.Invoke(),
                    },
                },
            };
        }

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            AccentColour = Nub.Colour = overlayColourProvider.Content2;
            BackgroundColour = overlayColourProvider.Content2.Darken(1);

            mainContent.EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Colour = Color4.White.Opacity(0),
                Hollow = true,
                Radius = 5,
            };
        }

        protected override void Update()
        {
            base.Update();

            nubContainer.Padding = new MarginPadding { Horizontal = RangePadding };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindDisabledChanged(disabled =>
            {
                Alpha = disabled ? 0.3f : 1;
            }, true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateGlow();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateGlow();
            base.OnHoverLost(e);
        }

        protected override bool ShouldHandleAsRelativeDrag(MouseDownEvent e)
            => Nub.ReceivePositionalInputAt(e.ScreenSpaceMouseDownPosition);

        protected override void OnDragEnd(DragEndEvent e)
        {
            updateGlow();
            base.OnDragEnd(e);
        }

        private void updateGlow()
        {
            Nub.Glowing = !Current.Disabled && (IsHovered);
            if (!Current.Disabled && (IsHovered))
            {
                LeftBoxContainer.FadeEdgeEffectTo(AccentColour.Darken(1).Opacity(0.5f), 40, Easing.OutQuint);
                RightBoxContainer.FadeEdgeEffectTo(AccentColour.Darken(1).Opacity(0.5f), 40, Easing.OutQuint);
            }
            else
            {
                LeftBoxContainer.FadeEdgeEffectTo(AccentColour.Darken(1).Opacity(0f), 800, Easing.OutQuint);
                RightBoxContainer.FadeEdgeEffectTo(AccentColour.Darken(1).Opacity(0f), 800, Easing.OutQuint);
            }
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            LeftBox.Scale = new Vector2(Math.Clamp(RangePadding + (Nub.DrawPosition.X - 6), 0, Math.Max(0, DrawWidth)), 1);
            RightBox.Scale = new Vector2(Math.Clamp(DrawWidth - (Nub.DrawPosition.X + 6) - RangePadding, 0, Math.Max(0, DrawWidth)), 1);
        }

        protected override void UpdateValue(float value)
        {
            Nub.MoveToX(value, 250, Easing.OutQuint);
        }

        public partial class SliderNub : SliderNubRemake
        {
            public Action? OnDoubleClicked { get; init; }

            protected override bool OnClick(ClickEvent e) => true;

            protected override bool OnDoubleClick(DoubleClickEvent e)
            {
                OnDoubleClicked?.Invoke();
                return true;
            }
        }
    }
}

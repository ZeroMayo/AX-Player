// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Numerics;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Vector2 = osuTK.Vector2;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class WavySeekBar<T>
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        /// <summary>
        /// A glossy, softly glowing puck which sits on the seek bar and marks the current position.
        /// </summary>
        public partial class SliderNub : Container
        {
            public const float HEIGHT = 30;

            public const float DEFAULT_EXPANDED_SIZE = 20;

            private const float body_size = 18;
            private const float resting_border_thickness = 1.5f;
            private const float highlighted_border_thickness = 0.6f;

            private const float resting_glow_alpha = 0.16f;
            private const float highlighted_glow_alpha = 0.5f;

            private readonly CircularContainer body;
            private readonly Box fill;
            private readonly Box gloss;

            private bool highlightRequested;
            private bool highlighted;

            private Color4 accentColour = Color4.White;

            public Color4 AccentColour
            {
                get => accentColour;
                set
                {
                    if (accentColour == value)
                        return;

                    accentColour = value;

                    fill.FadeColour(value, 500, Easing.OutQuint);
                    body.FadeEdgeEffectTo(glowColour, 500, Easing.OutQuint);
                }
            }

            /// <summary>
            /// Whether the bar containing this nub is hovered or being dragged.
            /// </summary>
            public bool Highlighted
            {
                get => highlightRequested;
                set
                {
                    if (highlightRequested == value)
                        return;

                    highlightRequested = value;
                    updateHighlight();
                }
            }

            private Color4 glowColour => accentColour.Opacity(highlighted ? highlighted_glow_alpha : resting_glow_alpha);

            public Action? OnDoubleClicked { get; init; }

            public SliderNub(float expandedSize = DEFAULT_EXPANDED_SIZE)
            {
                Size = new Vector2(expandedSize, expandedSize);

                InternalChild = body = new CircularContainer
                {
                    Size = new Vector2(body_size),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderThickness = resting_border_thickness,
                    BorderColour = Color4.White.Opacity(0.9f),
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = glowColour,
                        Radius = 10,
                        Roundness = body_size / 2,
                    },
                    Children = new Drawable[]
                    {
                        fill = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = accentColour,
                            AlwaysPresent = true,
                        },
                        // A top-down sheen makes the puck read as a glossy sphere rather than a flat dot.
                        gloss = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientVertical(Color4.White.Opacity(0.3f), Color4.White.Opacity(0)),
                        },
                    },
                };
            }

            private void updateHighlight()
            {
                bool value = highlightRequested || IsHovered;

                if (value == highlighted)
                    return;

                highlighted = value;

                if (value)
                {
                    body.ScaleTo(1.15f, 200, Easing.OutQuint);
                    body.TransformTo(nameof(BorderThickness), highlighted_border_thickness, 200, Easing.OutQuint);
                    body.FadeEdgeEffectTo(glowColour, 200, Easing.OutQuint);
                }
                else
                {
                    body.ScaleTo(1f, 500, Easing.OutQuint);
                    body.TransformTo(nameof(BorderThickness), resting_border_thickness, 500, Easing.OutQuint);
                    body.FadeEdgeEffectTo(glowColour, 500, Easing.OutQuint);
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateHighlight();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateHighlight();
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e) => true;

            protected override bool OnDoubleClick(DoubleClickEvent e)
            {
                OnDoubleClicked?.Invoke();
                return true;
            }
        }
    }
}

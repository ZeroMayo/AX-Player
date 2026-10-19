// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProjectYomiTabDropdown<T> : ProjectYomiDropdown<T>, IHasAccentColour
    {
        private Color4 accentColour;

        public Color4 AccentColour
        {
            get => accentColour;
            set
            {
                accentColour = value;

                if (IsLoaded)
                    propagateAccentColour();
            }
        }

        public ProjectYomiTabDropdown()
        {
            RelativeSizeAxes = Axes.X;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            propagateAccentColour();
        }

        protected override DropdownMenu CreateMenu() => new AdaptiveTabDropdownMenu();

        protected override DropdownHeader CreateHeader() => new AdaptiveTabDropdownHeader
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight
        };

        private void propagateAccentColour()
        {
            if (Menu is AdaptiveDropdownMenu dropdownMenu)
            {
                dropdownMenu.HoverColour = accentColour;
                dropdownMenu.SelectionColour = accentColour.Opacity(0.5f);
            }

            if (Header is AdaptiveTabDropdownHeader tabDropdownHeader)
                tabDropdownHeader.AccentColour = accentColour;
        }

        private partial class AdaptiveTabDropdownMenu : AdaptiveDropdownMenu
        {
            public AdaptiveTabDropdownMenu()
            {
                Anchor = Anchor.TopRight;
                Origin = Anchor.TopRight;

                BackgroundColour = Color4.Black.Opacity(0.7f);
                MaxHeight = 200;
            }

            protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new DrawableAdaptiveTabDropdownMenuItem(item);

            private partial class DrawableAdaptiveTabDropdownMenuItem : DrawableAdaptiveDropdownMenuItem
            {
                public DrawableAdaptiveTabDropdownMenuItem(MenuItem item)
                    : base(item)
                {
                    ForegroundColourHover = Color4.Black;
                }
            }
        }

        protected partial class AdaptiveTabDropdownHeader : AdaptiveDropdownHeader, IHasAccentColour
        {
            private Color4 accentColour;

            public Color4 AccentColour
            {
                get => accentColour;
                set
                {
                    accentColour = value;
                    BackgroundColourHover = value;
                    updateColour();
                }
            }

            public AdaptiveTabDropdownHeader()
            {
                RelativeSizeAxes = Axes.None;
                AutoSizeAxes = Axes.X;

                BackgroundColour = Color4.Black.Opacity(0.5f);

                Background.Height = 0.5f;
                Background.CornerRadius = 5;
                Background.Masking = true;

                Foreground.RelativeSizeAxes = Axes.None;
                Foreground.AutoSizeAxes = Axes.X;
                Foreground.RelativeSizeAxes = Axes.Y;
                Foreground.Margin = new MarginPadding(5);

                Foreground.Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Icon = FontAwesome.Solid.EllipsisH,
                        Size = new Vector2(14),
                        Origin = Anchor.Centre,
                        Anchor = Anchor.Centre,
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateColour();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateColour();
                base.OnHoverLost(e);
            }

            private void updateColour()
            {
                Foreground.Colour = IsHovered ? BackgroundColour : BackgroundColourHover;
            }
        }
    }
}

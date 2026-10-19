// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class MenuButtonItem : ProjectYomiTweakedClickableContainer
    {
        private SpriteIcon icon;
        private TruncatingSpriteText titleText;
        private Container buttonContent, buttonContentAnchor;

        private Vector2 iconScale = new Vector2(1f);
        private LocalisableString text;
        private IconUsage iconUsage;
        private Hotkey hotkey;

        public Vector2 IconScale
        {
            get => iconScale;
            set
            {
                iconScale = value;

                if (icon != null)
                    icon.Scale = value;
            }
        }

        public LocalisableString Text
        {
            get => text;
            set
            {
                text = value;

                if (titleText != null)
                    titleText.Text = value;
            }
        }

        public IconUsage Icon
        {
            get => iconUsage;
            set
            {
                iconUsage = value;

                if (icon != null)
                    icon.Icon = value;
            }
        }

        public Hotkey Hotkey
        {
            get => hotkey;
            set
            {
                hotkey = value;

                if (hotkeyDisplay != null)
                    hotkeyDisplay.Hotkey = value;
            }
        }

        private Box hover, disabledOverlay;

        private HotkeyDisplay hotkeyDisplay;

        public CornersInfo RoundCorner
        {
            get => roundCorner;
            set
            {
                roundCorner = value;

                if (buttonContent != null)
                    buttonContent.CornerRadius = value;
            }
        }

        private CornersInfo roundCorner;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            Children = new Drawable[]
            {
                buttonContentAnchor = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Children = new Drawable[]
                    {
                        buttonContent = new Container
                        {
                            CornerRadius = roundCorner,
                            Masking = true,
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = overlayColourProvider.Background4,
                                    Alpha = 1f,
                                },
                                hover = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.White,
                                    Blending = BlendingParameters.Additive,
                                    Alpha = 0,
                                },
                                new Container {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(7),
                                    Children = new Drawable[]
                                    {
                                        icon = new SpriteIcon
                                        {
                                            Scale = iconScale * 0.4f,
                                            Width = 45,
                                            Height = 45,
                                            Margin = new MarginPadding()
                                            {
                                                Left = 16,
                                                Top = 10,
                                            },
                                            Icon = iconUsage,
                                            Colour = overlayColourProvider.Content2,
                                        },
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.Y,
                                            AutoSizeAxes = Axes.X,
                                            Direction = FillDirection.Horizontal,
                                            Padding = new MarginPadding
                                            {
                                                Vertical = 5,
                                                Left = 50,
                                                Right = 5,
                                            },
                                            Children = new Drawable[]
                                            {
                                                titleText = new TruncatingSpriteText
                                                {
                                                    Font = AXPlayerApp.DefaultFont.With(size: 20, weight: "Bold"),
                                                    Text = text,
                                                    Colour = overlayColourProvider.Content2,
                                                },
                                                hotkeyDisplay = new HotkeyDisplay
                                                {
                                                    Size = new Vector2(10),
                                                    Hotkey = hotkey,
                                                    Margin = new MarginPadding { Left = 5, Top = 4 },
                                                }
                                            }
                                        }
                                    }
                                },
                                disabledOverlay = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black,
                                    Alpha = 0,
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (Action != null)
                hover.FadeTo(0.1f, 500, Easing.OutQuint);

            return base.OnHover(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            buttonContentAnchor.ScaleTo(0.95f, 2000, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            buttonContentAnchor.ScaleTo(1, 1000, Easing.OutElastic);
            base.OnMouseUp(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            if (Action != null)
                hover.FadeOut(500, Easing.OutQuint);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Enabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    disabledOverlay.FadeOut(500, Easing.OutQuint);
                else
                    disabledOverlay.FadeTo(0.5f, 500, Easing.OutQuint);
            }, true);
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.Containers
{
    public partial class PushNotificationContainer : ProjectYomiTweakedClickableContainer
    {
        private IconUsage icon;

        private Box bg, hover;
        private ProjectYomiTextFlowContainer titleText, descText;
        private Container content;
        private IconButton closeBtn;

        public bool DoNotMoveToNotificationCenter;

        public PushNotificationContainer(IconUsage icon, Color4 iconColour, LocalisableString title, LocalisableString desc, bool doNotMoveToNotificationCenter = false)
        {
            DoNotMoveToNotificationCenter = doNotMoveToNotificationCenter;
            Enabled.Value = true;
            AutoSizeAxes = Axes.Y;
            Width = 380;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            Add(content = new Container
            {
                Masking = true,
                CornerRadius = 32,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                Children = new Drawable[]
                {
                    bg = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    new FillFlowContainer
                    {
                        Padding = new MarginPadding(8),
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Children = 
                            new Drawable[]
                            {
                                new Container
                                {
                                    Masking = true,
                                    Width = 48,
                                    Height = 48,
                                    CornerRadius = 24,
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = iconColour.Darken(2f),
                                        },
                                        new SpriteIcon
                                        {
                                            Width = 24,
                                            Height = 24,
                                            Icon = icon,
                                            Colour = iconColour,
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                        }
                                    }
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Y,
                                    RelativeSizeAxes = Axes.X,
                                    Margin = new MarginPadding { Left = 8 },
                                    Padding = new MarginPadding { Right = 100 },
                                    Direction = FillDirection.Vertical,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Children = new Drawable[]
                                    {
                                        titleText = new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 20, weight: "Bold"))
                                        {
                                            AutoSizeAxes = Axes.Y,
                                            RelativeSizeAxes = Axes.X,
                                            Text = title,
                                            Anchor = Anchor.TopLeft,
                                            Origin = Anchor.TopLeft,
                                            TextAnchor = Anchor.CentreLeft,
                                        },
                                        descText = new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 16, weight: "Bold"))
                                        {
                                            AutoSizeAxes = Axes.Y,
                                            RelativeSizeAxes = Axes.X,
                                            Text = desc,
                                            Anchor = Anchor.TopLeft,
                                            Origin = Anchor.TopLeft,
                                            TextAnchor = Anchor.CentreLeft,
                                        }
                                    }
                                }
                            }
                    },
                    closeBtn = new IconButton
                    {
                        Enabled = { Value = true },
                        Origin = Anchor.TopRight,
                        Anchor = Anchor.TopRight,
                        Size = new Vector2(35, 35),
                        IconScale = new Vector2(0.8f),
                        Margin = new MarginPadding(14),
                        Icon = FontAwesome.Solid.Times,
                        Action = () =>
                        {
                            CloseNotification();
                        }
                    },
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Blending = BlendingParameters.Additive,
                        Alpha = 0,
                    },
                }
            });
        }

        public void SetBackgroundColour(Color4 color)
        {
            bg.Colour = color;
        }

        public void SetCloseBtnBGColour(Color4 color)
        {
            closeBtn.BackgroundColour = color;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            bg.Colour = overlayColourProvider.Background4;
            titleText.Colour = overlayColourProvider.Content2;
            descText.Colour = overlayColourProvider.Foreground1;
            closeBtn.BackgroundColour = overlayColourProvider.Background3;
        }

        public void ShowCloseButton()
        {
            closeBtn.Show();
        }

        public void HideCloseButton()
        {
            closeBtn.Hide();
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            content.ScaleTo(0.9f, 2000, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            content.ScaleTo(1, 250, Easing.OutQuint);
            base.OnMouseUp(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hover.FadeTo(0.1f, 500, Easing.OutQuint);

            return base.OnHover(e);
        }

        private void CloseNotification()
        {
            this.FadeOut(500, Easing.InQuint).OnComplete(_ => Expire());
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);

            hover.FadeOut(500, Easing.OutQuint);
        }
    }
}

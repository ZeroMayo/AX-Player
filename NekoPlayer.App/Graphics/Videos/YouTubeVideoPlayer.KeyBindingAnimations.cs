// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using NekoPlayer.App.Graphics.Sprites;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Threading;
using osuTK.Graphics;

namespace NekoPlayer.App.Graphics.Videos
{
    public partial class YouTubeVideoPlayer
    {
        public partial class KeyBindingAnimations : Container
        {
            private SeekAnimation leftContent, centerContent, rightContent;

            [BackgroundDependencyLoader]
            private void load()
            {
                AddRange(new Drawable[] {
                    leftContent = new SeekAnimation(SeekAction.FastRewind10sec)
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.Y,
                        Width = 200,
                    },
                    centerContent = new SeekAnimation(SeekAction.PlayPause)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        RelativeSizeAxes = Axes.Both,
                    },
                    rightContent = new SeekAnimation(SeekAction.FastForward10sec)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        RelativeSizeAxes = Axes.Y,
                        Width = 200,
                    }
                });
            }

            public void PlaySeekAnimation(SeekAction seekAction, IconUsage icon)
            {
                switch (seekAction)
                {
                    case SeekAction.FastRewind10sec:
                    {
                        rightContent.RepeatCount = 0;
                        rightContent.HideNow();
                        leftContent.RepeatCount += 5;
                        leftContent.PlaySeekAnimation(FontAwesome.Solid.ChevronLeft);
                        break;
                    }
                    case SeekAction.FastForward10sec:
                    {
                        leftContent.RepeatCount = 0;
                        leftContent.HideNow();
                        rightContent.RepeatCount += 5;
                        rightContent.PlaySeekAnimation(FontAwesome.Solid.ChevronRight);
                        break;
                    }
                    case SeekAction.PlayPause:
                    {
                        centerContent.PlaySeekAnimation(icon);
                        break;
                    }
                }
            }

            private partial class SeekAnimation : BufferedContainer
            {
                private SeekAction trackAction;
                private SpriteIcon seekArrow;
                private ProjectYomiSpriteText seekValue;

                private Container content;
                public int RepeatCount = 0;

                private ScheduledDelegate fadeOut;

                public SeekAnimation(SeekAction trackAction)
                {
                    this.trackAction = trackAction;
                    if (trackAction == SeekAction.FastRewind10sec)
                    {
                        Add(content = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Children = new Drawable[]
                            {
                                new CircularContainer
                                {
                                    Width = 75,
                                    Height = 75,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Masking = true,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0.7f,
                                        Colour = Color4.Black,
                                    }
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Direction = FillDirection.Horizontal,
                                    Children = new Drawable[]
                                    {
                                        seekArrow = new SpriteIcon
                                        {
                                            Width = 15,
                                            Height = 15,
                                            Icon = FontAwesome.Solid.ChevronLeft,
                                            Margin = new MarginPadding { Right = 2 },
                                        },
                                        seekValue = new ProjectYomiSpriteText(false)
                                        {
                                            Text = $"- 5",
                                            Font = NekoPlayerApp.DefaultFont.With(size: 15, weight: "Bold"),
                                        },
                                    }
                                }
                            }
                        });
                    }
                    else if (trackAction == SeekAction.FastForward10sec)
                    {
                        Add(content = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Children = new Drawable[]
                            {
                                new CircularContainer
                                {
                                    Width = 75,
                                    Height = 75,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Masking = true,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0.7f,
                                        Colour = Color4.Black,
                                    }
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Direction = FillDirection.Horizontal,
                                    Children = new Drawable[]
                                    {
                                        seekValue = new ProjectYomiSpriteText(false)
                                        {
                                            Text = $"+ 5",
                                            Font = NekoPlayerApp.DefaultFont.With(size: 15, weight: "Bold"),
                                        },
                                        seekArrow = new SpriteIcon
                                        {
                                            Width = 15,
                                            Height = 15,
                                            Icon = FontAwesome.Solid.ChevronRight,
                                            Margin = new MarginPadding { Left = 2 },
                                        },
                                    }
                                }
                            }
                        });
                    }
                    else
                    {
                        Add(content = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Children = new Drawable[]
                            {
                                new CircularContainer
                                {
                                    Width = 100,
                                    Height = 100,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Masking = true,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0.7f,
                                        Colour = Color4.Black,
                                    }
                                },
                                new Container
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Children = new Drawable[]
                                    {
                                        seekArrow = new SpriteIcon
                                        {
                                            Width = 25,
                                            Height = 25,
                                            Icon = FontAwesome.Solid.ChevronRight,
                                        },
                                    }
                                }
                            }
                        });
                    }
                }

                private Bindable<int> seekTextRolling;

                [BackgroundDependencyLoader]
                private void load()
                {
                    seekTextRolling = new Bindable<int>(0);
                    content.FadeOut();

                    seekTextRolling.BindValueChanged(speed =>
                    {
                        int intValue = speed.NewValue;
                        seekValue.Text = $"{(trackAction == SeekAction.FastForward10sec ? "+" : "-")} {intValue}";
                    }, true);
                }

                private bool isShowing = false;

                public void HideNow()
                {
                    if (trackAction != SeekAction.PlayPause)
                    {
                        isShowing = false;
                        content.FadeOut(500, Easing.OutExpo);
                        content.ScaleTo(new osuTK.Vector2(.9f), 250, Easing.OutExpo);
                        content.MoveToX(trackAction != SeekAction.FastForward10sec ? -10 : 10, 250, Easing.OutExpo);
                        this.BlurTo(new osuTK.Vector2(4), 250, Easing.OutExpo);
                        RepeatCount = 0;
                    }
                }

                public void PlaySeekAnimation(IconUsage icon)
                {
                    seekArrow.Icon = icon;
                    if (trackAction != SeekAction.PlayPause)
                    {
                        this.TransformBindableTo(seekTextRolling, RepeatCount, 400, Easing.OutQuint);
                        if (!isShowing)
                        {
                            this.BlurTo(new osuTK.Vector2(4));
                            this.BlurTo(new osuTK.Vector2(0), 500, Easing.OutExpo);
                            content.FadeInFromZero(500, Easing.OutExpo);
                            content.MoveToX(trackAction != SeekAction.FastForward10sec ? -10 : 10).MoveToX(0, 750, Easing.OutExpo);
                            content.ScaleTo(new osuTK.Vector2(.9f)).ScaleTo(new osuTK.Vector2(1.5f), 750, Easing.OutExpo);
                        }
                        fadeOut?.Cancel();
                        isShowing = true;
                        fadeOut = Scheduler.AddDelayed(() =>
                        {
                            HideNow();
                        }, 750);
                    }
                    else
                    {
                        this.BlurTo(new osuTK.Vector2(4));
                        this.BlurTo(new osuTK.Vector2(0), 500, Easing.OutExpo);
                        content.FadeInFromZero(500, Easing.OutExpo);
                        content.ScaleTo(new osuTK.Vector2(.9f)).ScaleTo(new osuTK.Vector2(1.5f), 750, Easing.OutExpo);
                        fadeOut?.Cancel();
                        fadeOut = Scheduler.AddDelayed(() =>
                        {
                            content.FadeOut(500, Easing.OutExpo);
                            content.ScaleTo(new osuTK.Vector2(.9f), 250, Easing.OutExpo);
                            this.BlurTo(new osuTK.Vector2(4), 250, Easing.OutExpo);
                        }, 750);
                    }
                }
            }

            public enum SeekAction
            {
                FastForward10sec,
                FastRewind10sec,
                PlayPause,
            }
        }
    }
}

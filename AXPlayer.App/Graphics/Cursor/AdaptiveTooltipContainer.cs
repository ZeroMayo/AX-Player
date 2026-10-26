// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.Cursor
{
    public partial class AdaptiveTooltipContainer : TooltipContainer
    {
        private AdaptiveTooltip tooltip;

        protected override ITooltip CreateTooltip() => tooltip = new AdaptiveTooltip(ScreenshotManager);

        public ScreenshotManager ScreenshotManager {
            get;
            set
            {
                field = value;
                tooltip.ScreenshotManager = value;
            }
        }

        public AdaptiveTooltipContainer(CursorContainer cursor)
            : base(cursor)
        {
        }

        protected override double AppearDelay => (1 - CurrentTooltip.Alpha) * base.AppearDelay; // reduce appear delay if the tooltip is already partly visible.

        public partial class AdaptiveTooltip : Tooltip
        {
            private readonly IBindable<bool> screenshotCursorVisibility = new Bindable<bool>(true);
            public override bool IsPresent => screenshotCursorVisibility.Value && base.IsPresent;

            private const float max_width = 500;

            private readonly Box background;
            private readonly TextFlowContainer text;
            private readonly Container tooltip;
            public ScreenshotManager ScreenshotManager
            {
                get;
                set
                {
                    field = value;
                    SetScreenshotManager(value);
                }
            }
            private bool instantMovement = true;

            private LocalisableString lastContent;

            public override void SetContent(LocalisableString content)
            {
                if (content.Equals(lastContent))
                    return;

                text.Text = content;

                if (IsPresent)
                {
                    tooltip.AutoSizeDuration = 250;
                    background.FlashColour(new Color4(0.4f, 0.4f, 0.4f, 1f), 1000, Easing.OutQuint);
                }
                else
                    tooltip.AutoSizeDuration = 0;

                lastContent = content;
            }

            public AdaptiveTooltip(ScreenshotManager screenshotManager)
            {
                ScreenshotManager = screenshotManager;
                Children = new Drawable[]
                {
                    tooltip = new Container
                    {
                        AutoSizeEasing = Easing.OutQuint,
                        Masking = true,
                        CornerRadius = 13,
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[] {
                            background = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            text = new TextFlowContainer(f =>
                            {
                                f.Font = AXPlayerApp.DefaultFont;
                            })
                            {
                                Margin = new MarginPadding(5),
                                Padding = new MarginPadding()
                                {
                                    Horizontal = 5,
                                },
                                AutoSizeAxes = Axes.Both,
                                MaximumSize = new Vector2(max_width, float.PositiveInfinity),
                            }
                        }
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider overlayColourProvider)
            {
                background.Colour = overlayColourProvider.Background3;
                text.Colour = overlayColourProvider.Content2;

                screenshotCursorVisibility.BindValueChanged(cursor =>
                {
                    Schedule(() =>
                    {
                        if (cursor.NewValue)
                            tooltip.Show();
                        else
                            tooltip.Hide();
                    });
                }, true);
            }

            public void SetScreenshotManager(ScreenshotManager screenshotManager)
            {
                Logger.Log($"[AdaptiveTooltipContainer] screenshotManager binded to {ScreenshotManager}");
                if (screenshotManager != null)
                    screenshotCursorVisibility.BindTo(screenshotManager.CursorVisibility);
            }

            protected override void PopIn()
            {
                instantMovement |= !IsPresent;
                this.FadeIn(500, Easing.OutQuint);
            }

            protected override void PopOut() => this.Delay(150).FadeOut(500, Easing.OutQuint);

            public override void Move(Vector2 pos)
            {
                if (instantMovement)
                {
                    Position = pos;
                    instantMovement = false;
                }
                else
                {
                    this.MoveTo(pos, 200, Easing.OutQuint);
                }
            }
        }
    }
}

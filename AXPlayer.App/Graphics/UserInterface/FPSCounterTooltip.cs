// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class FPSCounterTooltip : CompositeDrawable, ITooltip
    {
        private ProjectYomiTextFlowContainer textFlow = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            AutoSizeAxes = Axes.Both;

            CornerRadius = 15;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Colour = overlayColourProvider.Background5,
                    Alpha = 1,
                    RelativeSizeAxes = Axes.Both,
                },
                new ProjectYomiTextFlowContainer(cp =>
                {
                    cp.Font = AXPlayerApp.DefaultFont.With(weight: "SemiBold");
                })
                {
                    AutoSizeAxes = Axes.Both,
                    TextAnchor = Anchor.TopRight,
                    Margin = new MarginPadding { Left = 5, Vertical = 10 },
                    Text = string.Join('\n', gameHost.Threads.Select(t => t.Name)),
                    Colour = overlayColourProvider.Content2,
                    ParagraphSpacing = 0,
                },
                textFlow = new ProjectYomiTextFlowContainer(cp =>
                {
                    cp.Font = AXPlayerApp.DefaultFont.With(fixedWidth: true, weight: "Regular");
                    cp.Spacing = new Vector2(-1);
                })
                {
                    Width = 220,
                    Margin = new MarginPadding { Left = 35, Right = 10, Vertical = 10 },
                    AutoSizeAxes = Axes.Y,
                    TextAnchor = Anchor.TopRight,
                    Colour = overlayColourProvider.Content2,
                    ParagraphSpacing = 0,
                },
            };
        }

        private int lastUpdate;

        protected override void Update()
        {
            int currentSecond = (int)(Clock.CurrentTime / 100);

            if (currentSecond != lastUpdate)
            {
                lastUpdate = currentSecond;

                textFlow.Clear();

                foreach (var thread in gameHost.Threads)
                {
                    var clock = thread.Clock;

                    string maximum = clock.Throttling
                        ? $"/{(clock.MaximumUpdateHz > 0 && clock.MaximumUpdateHz < 10000 ? clock.MaximumUpdateHz.ToString("0") : "∞"),4}"
                        : string.Empty;

                    textFlow.AddParagraph($"{clock.FramesPerSecond:0}{maximum} fps ({clock.ElapsedFrameTime:0.00} ms)");
                }
            }
        }

        public void SetContent(object content)
        {
        }

        public void Move(Vector2 pos)
        {
            Position = pos;
        }
    }
}

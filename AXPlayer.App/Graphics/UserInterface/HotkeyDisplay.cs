// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Platform;
using osuTK;
using AXPlayer.App.Graphics.Sprites;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class HotkeyDisplay : CompositeDrawable
    {
        private Hotkey hotkey;

        public Hotkey Hotkey
        {
            get => hotkey;
            set
            {
                if (EqualityComparer<Hotkey>.Default.Equals(hotkey, value))
                    return;

                hotkey = value;

                if (IsLoaded)
                    updateState();
            }
        }

        private FillFlowContainer flow = null!;

        [Resolved]
        private ReadableKeyCombinationProvider readableKeyCombinationProvider { get; set; } = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5)
            };

            updateState();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateState();
        }

        private void updateState()
        {
            flow.Clear();
            foreach (string h in hotkey.ResolveKeyCombination(readableKeyCombinationProvider, gameHost))
                flow.Add(new HotkeyBox(h));
        }

        private partial class HotkeyBox : CompositeDrawable
        {
            private readonly string hotkey;

            public HotkeyBox(string hotkey)
            {
                this.hotkey = hotkey;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider? colourProvider, ProjectYomiColour colours)
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 3;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider?.Background6 ?? Colour4.Black.Opacity(0.7f),
                    },
                    new ProjectYomiSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 5, Bottom = 1, },
                        Text = hotkey.ToUpperInvariant(),
                        Font = AXPlayerApp.DefaultFont.With(size: 12, weight: "Bold"),
                        Colour = colourProvider?.Light1 ?? colours.GrayA,
                    }
                };
            }
        }
    }
}

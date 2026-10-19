// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public partial class ProjectYomiHexColourPicker : HexColourPicker
    {
        public ProjectYomiHexColourPicker()
        {
            Padding = new MarginPadding(20);
            Spacing = 20;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider? overlayColourProvider, ProjectYomiColour osuColour)
        {
            Background.Colour = overlayColourProvider?.Dark6 ?? osuColour.GreySeaFoamDarker;
        }

        protected override TextBox CreateHexCodeTextBox() => new ProjectYomiTextBox();
        protected override ColourPreview CreateColourPreview() => new OsuColourPreview();

        private partial class OsuColourPreview : ColourPreview
        {
            private readonly Box preview;

            public OsuColourPreview()
            {
                InternalChild = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Child = preview = new Box
                    {
                        RelativeSizeAxes = Axes.Both
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(colour => preview.Colour = colour.NewValue, true);
            }
        }
    }
}

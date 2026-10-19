// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK.Graphics;
using AXPlayer.App.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public partial class RoundedButton : ProjectYomiMaterialButton, IFilterable, IHasTooltip
    {
        protected override float HoverLayerFinalAlpha => 0;

        public override Color4 BackgroundColour
        {
            get => base.BackgroundColour;
            set => base.BackgroundColour = value;
        }

        public override Color4 ForegroundColour { get => base.ForegroundColour; set => base.ForegroundColour = value; }

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider? overlayColourProvider, ProjectYomiColour colours)
        {
            // Many buttons have local colours, but this provides a sane default for all other cases.
            DefaultBackgroundColour = overlayColourProvider?.Colour3 ?? colours.Blue3;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
        }

        protected override bool OnHover(HoverEvent e)
        {
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            Background.FadeColour(BackgroundColour, 300, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        public virtual IEnumerable<LocalisableString> FilterTerms => new[] { Text };

        public bool MatchingFilter
        {
            set => this.FadeTo(value ? 1 : 0);
        }

        public bool FilteringActive { get; set; }

        public virtual LocalisableString TooltipText { get; set; }
    }
}

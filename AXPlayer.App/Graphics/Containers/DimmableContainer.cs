// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK.Graphics;
using AXPlayer.App.Config;

namespace AXPlayer.App.Graphics.Containers
{
    public partial class DimmableContainer : Container
    {
        public Bindable<double> UserDimLevel { get; set; } = null!;

        protected virtual float DimLevel => Math.Max((float)UserDimLevel.Value, 0);

        private readonly Container content;

        protected override Container<Drawable> Content => content;

        public DimmableContainer()
        {
            AddInternal(content = new Container { RelativeSizeAxes = Axes.Both });
        }

        [BackgroundDependencyLoader]
        private void load(AXPlayerConfigManager config)
        {
            UserDimLevel = config.GetBindable<double>(AXPlayerSetting.VideoDimLevel);

            UserDimLevel.ValueChanged += _ => UpdateVisuals();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            UpdateVisuals();
        }

        protected virtual void UpdateVisuals()
        {
            content.FadeColour(new Color4(1f - DimLevel, 1f - DimLevel, 1f - DimLevel, 1f), 800, Easing.OutQuint);
        }
    }
}

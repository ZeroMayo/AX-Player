// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using AXPlayer.App.Overlays.Volume;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Threading;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Overlays
{
    [Cached]
    public partial class VolumeOverlay : VisibilityContainer
    {
        public Bindable<bool> IsMuted { get; } = new Bindable<bool>();

        private const float offset = 10;

        private VolumeMeter volumeMeterMusic = null!;

        private FormVolumeSliderBarSmall<double> newVolumeSliderBar;
        private BindableDouble ActualVolume = new BindableDouble()
        {
            MinValue = 0,
            MaxValue = 1,
        };

        private SelectionCycleFillFlowContainer<VolumeMeter> volumeMeters = null!;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, ProjectYomiColour colours)
        {
            Height = 150;
            RelativeSizeAxes = Axes.X;

            AddRange(new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 100,
                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.25f), Color4.Black.Opacity(0))
                },
                newVolumeSliderBar = new FormVolumeSliderBarSmall<double>
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Margin = new MarginPadding()
                    {
                        Top = 8
                    },
                    Width = 500,
                    Caption = AXPlayerStrings.VideoVolume,
                    DisplayAsPercentage = true,
                    Icon = FontAwesome.Solid.VolumeUp,
                },
                new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AlwaysPresent = true,
                    Alpha = 0,
                    Spacing = new Vector2(0, offset),
                    Margin = new MarginPadding { Left = offset },
                    Children = new Drawable[]
                    {
                        volumeMeters = new SelectionCycleFillFlowContainer<VolumeMeter>
                        {
                            Scale = new Vector2(0.8f),
                            Direction = FillDirection.Vertical,
                            AutoSizeAxes = Axes.Both,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Spacing = new Vector2(0, -offset),
                            Children = new[]
                            {
                                volumeMeterMusic = new MasterVolumeMeter(AXPlayerStrings.VideoVolume, 125, colours.BlueDarker),
                            }
                        },
                    },
                },
            });

            volumeMeterMusic.Bindable.BindTo(audio.VolumeTrack);

            ActualVolume.BindTo(audio.VolumeTrack);
            newVolumeSliderBar.Current.BindTo(ActualVolume);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (var volumeMeter in volumeMeters)
                volumeMeter.Bindable.ValueChanged += _ => Show();
        }

        public bool Adjust(GlobalAction action, float amount = 1, bool isPrecise = false)
        {
            if (!IsLoaded) return false;

            switch (action)
            {
                case GlobalAction.DecreaseVolume:
                    if (State.Value == Visibility.Hidden)
                        Show();
                    else
                        volumeMeters.Selected?.Decrease(amount, isPrecise);
                    return true;

                case GlobalAction.IncreaseVolume:
                    if (State.Value == Visibility.Hidden)
                        Show();
                    else
                        volumeMeters.Selected?.Increase(amount, isPrecise);
                    return true;

                case GlobalAction.NextVolumeMeter:
                    if (State.Value != Visibility.Visible)
                        return false;

                    volumeMeters.SelectNext();
                    Show();
                    return true;

                case GlobalAction.PreviousVolumeMeter:
                    if (State.Value != Visibility.Visible)
                        return false;

                    volumeMeters.SelectPrevious();
                    Show();
                    return true;

                case GlobalAction.ToggleMute:
                    //Show();
                    //volumeMeters.OfType<MasterVolumeMeter>().First().ToggleMute();
                    return true;
            }

            return false;
        }

        public override void Show()
        {
            // Focus on the master meter as a default if previously hidden
            if (State.Value == Visibility.Hidden)
            {
                //FocusMasterVolume();
                volumeMeters.Select(volumeMeterMusic);
            }

            if (State.Value == Visibility.Visible)
                schedulePopOut();

            base.Show();
        }

        protected override void PopIn()
        {
            ClearTransforms();
            volumeMeters.ScaleTo(1, 500, Easing.OutQuint);
            volumeMeters.TransformTo("Spacing", new Vector2(0, offset), 500, Easing.OutQuint);

            newVolumeSliderBar.MoveToY(0, 500, Easing.OutBack);
            newVolumeSliderBar.FadeIn(500, Easing.OutQuint);

            this.FadeIn(500, Easing.OutQuint);

            schedulePopOut();
        }

        protected override void PopOut()
        {
            this.FadeOut(250, Easing.InQuint);
            volumeMeters.ScaleTo(0.8f, 250, Easing.InQuint);
            volumeMeters.TransformTo("Spacing", new Vector2(0, -offset), 250, Easing.InQuint);

            newVolumeSliderBar.MoveToY(-80, 250, Easing.InQuint);
            newVolumeSliderBar.FadeOut(250, Easing.InQuint);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            // keep the scheduled event correctly timed as long as we have movement.
            schedulePopOut();
            return base.OnMouseMove(e);
        }

        protected override bool OnHover(HoverEvent e)
        {
            schedulePopOut();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            schedulePopOut();
            base.OnHoverLost(e);
        }

        private ScheduledDelegate? popOutDelegate;

        private void schedulePopOut()
        {
            popOutDelegate?.Cancel();
            this.Delay(1000).Schedule(() =>
            {
                if (!IsHovered)
                    Hide();
            }, out popOutDelegate);
        }
    }
}

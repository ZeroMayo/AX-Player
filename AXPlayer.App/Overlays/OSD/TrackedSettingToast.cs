// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Overlays.OSD
{
    public partial class TrackedSettingToast : Toast
    {
        private const int lights_bottom_margin = 16;

        private readonly int optionCount;
        private readonly int selectedOption = -1;

        private Sample sampleOn;
        private Sample sampleOff;
        private Sample sampleChange;

        private Bindable<double?> lastPlaybackTime;

        public TrackedSettingToast(SettingDescription description, bool canPlaySound = true)
            : base(description.Name, description.Value)
        {
            this.canPlaySound = canPlaySound;
            ExtraText = description.Shortcut;

            FillFlowContainer<OptionLight> optionLights;

            Children = new Drawable[]
            {
                new Container
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Margin = new MarginPadding { Bottom = lights_bottom_margin },
                    Children = new Drawable[]
                    {
                        optionLights = new FillFlowContainer<OptionLight>
                        {
                            Margin = new MarginPadding { Bottom = 5, Right = 22 },
                            Spacing = new Vector2(5, 0),
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            AutoSizeAxes = Axes.Both
                        },
                    }
                }
            };

            switch (description.RawValue)
            {
                case bool val:
                    optionCount = 1;
                    if (val) selectedOption = 0;
                    break;

                case Enum:
                    var values = Enum.GetValues(description.RawValue.GetType());
                    optionCount = values.Length;
                    selectedOption = Convert.ToInt32(description.RawValue);
                    break;
            }

            ValueSpriteText.Padding = optionCount > 0 ? new MarginPadding { Horizontal = 22, Bottom = 16 } : new MarginPadding { Horizontal = 22, Vertical = 15 };

            for (int i = 0; i < optionCount; i++)
                optionLights.Add(new OptionLight { Glowing = i == selectedOption });
        }

        [Resolved]
        private SessionStatics statics { get; set; }

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        private bool canPlaySound;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (canPlaySound && config.Get<bool>(AXPlayerSetting.PlayOverlaySFX))
                playSound();
        }

        private void playSound()
        {
            // This debounce code roughly follows what we're using in HoverSampleDebounceComponent.
            // We're sharing the existing static for hover sounds because it doesn't really matter if they block each other.
            // This is a simple solution, but if this ever becomes a problem (or other performance issues arise),
            // the whole toast system should be rewritten to avoid recreating this drawable each time a value changes.
            lastPlaybackTime = statics.GetBindable<double?>(Static.LastHoverSoundPlaybackTime);

            bool enoughTimePassedSinceLastPlayback = !lastPlaybackTime.Value.HasValue || Time.Current - lastPlaybackTime.Value >= 20;

            if (!enoughTimePassedSinceLastPlayback) return;

            if (optionCount == 1)
            {
                if (selectedOption == 0)
                    sampleOn?.Play();
                else
                    sampleOff?.Play();
            }
            else
            {
                if (sampleChange == null) return;

                sampleChange.Frequency.Value = 1 + (double)selectedOption / (optionCount - 1) * 0.25f;
                sampleChange.Play();
            }

            lastPlaybackTime.Value = Time.Current;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleOn = audio.Samples.Get("UI/osd-on");
            sampleOff = audio.Samples.Get("UI/osd-off");
            sampleChange = audio.Samples.Get("UI/osd-change");
        }

        private partial class OptionLight : CircularContainer
        {
            private Color4 glowingColour, idleColour;

            private const float transition_speed = 300;

            private const float glow_strength = 0.5f;

            private readonly Box fill;

            public OptionLight()
            {
                Children = new[]
                {
                    fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 1,
                    },
                };
            }

            private bool glowing;

            public bool Glowing
            {
                set
                {
                    glowing = value;
                    if (!IsLoaded) return;

                    updateGlow();
                }
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider overlayColourProvider)
            {
                fill.Colour = idleColour = overlayColourProvider.Background1;
                glowingColour = overlayColourProvider.Content2;

                Size = new Vector2(10, 10);

                Masking = true;

                EdgeEffect = new EdgeEffectParameters
                {
                    Colour = glowingColour,
                    Type = EdgeEffectType.Glow,
                    Radius = 8,
                };
            }

            protected override void LoadComplete()
            {
                updateGlow();
                FinishTransforms(true);
            }

            private void updateGlow()
            {
                if (glowing)
                {
                    fill.FadeColour(glowingColour, transition_speed, Easing.OutQuint);
                    FadeEdgeEffectTo(glow_strength, transition_speed, Easing.OutQuint);
                }
                else
                {
                    FadeEdgeEffectTo(0, transition_speed, Easing.OutQuint);
                    fill.FadeColour(idleColour, transition_speed, Easing.OutQuint);
                }
            }
        }
    }
}

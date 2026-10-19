// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using AXPlayer.App.Graphics.Sprites;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProjectYomiTabControl<T> : TabControl<T>
    {
        private Color4 accentColour;

        public const float HORIZONTAL_SPACING = 2;

        public virtual Color4 AccentColour
        {
            get => accentColour;
            set
            {
                accentColour = value;

                if (Dropdown is IHasAccentColour dropdown)
                    dropdown.AccentColour = value;
                foreach (var i in TabContainer.OfType<IHasAccentColour>())
                    i.AccentColour = value;
            }
        }

        private readonly Box strip;

        protected override Dropdown<T> CreateDropdown() => new ProjectYomiTabDropdown<T>();

        protected override TabItem<T> CreateTabItem(T value) => new OsuTabItem(value);

        protected virtual float StripWidth => TabContainer.Sum(c => c.IsPresent ? c.DrawWidth + TabContainer.Spacing.X : 0) - TabContainer.Spacing.X;

        /// <summary>
        /// Whether entries should be automatically populated if <typeparamref name="T"/> is an <see cref="Enum"/> type.
        /// </summary>
        protected virtual bool AddEnumEntriesAutomatically => true;

        private static bool isEnumType => typeof(T).IsEnum;

        public ProjectYomiTabControl()
        {
            TabContainer.Spacing = new Vector2(HORIZONTAL_SPACING, 0f);

            AddInternal(strip = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Height = 1,
                Colour = Color4.White.Opacity(0),
            });

            if (isEnumType && AddEnumEntriesAutomatically)
            {
                foreach (var val in (T[])Enum.GetValues(typeof(T)))
                    AddItem(val);
            }
        }

        [BackgroundDependencyLoader]
        private void load(ProjectYomiColour colours)
        {
            if (accentColour == default)
                AccentColour = colours.Blue;
        }

        public Color4 StripColour
        {
            get => strip.Colour;
            set => strip.Colour = value;
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // dont bother calculating if the strip is invisible
            if (strip.Colour.MaxAlpha > 0)
                strip.Width = Interpolation.ValueAt(Math.Clamp(Clock.ElapsedFrameTime, 0, 1000), strip.Width, StripWidth, 0, 500, Easing.OutQuint);
        }

        public partial class OsuTabItem : TabItem<T>, IHasAccentColour
        {
            protected readonly SpriteText Text;
            protected readonly Box Bar;

            private Color4 accentColour;

            public Color4 AccentColour
            {
                get => accentColour;
                set
                {
                    accentColour = value;
                    if (!Active.Value)
                        Text.Colour = value;
                }
            }

            protected const float TRANSITION_LENGTH = 500;

            protected virtual void FadeHovered()
            {
                Bar.FadeIn(TRANSITION_LENGTH, Easing.OutQuint);
                Text.FadeColour(Color4.White, TRANSITION_LENGTH, Easing.OutQuint);
            }

            protected virtual void FadeUnhovered()
            {
                Bar.FadeTo(IsHovered ? 1 : 0, TRANSITION_LENGTH, Easing.OutQuint);
                Text.FadeColour(IsHovered ? Color4.White : AccentColour, TRANSITION_LENGTH, Easing.OutQuint);
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!Active.Value)
                    FadeHovered();
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!Active.Value)
                    FadeUnhovered();
            }

            public OsuTabItem(T value)
                : base(value)
            {
                AutoSizeAxes = Axes.X;
                RelativeSizeAxes = Axes.Y;

                LocalisableString text;

                switch (value)
                {
                    case IHasDescription hasDescription:
                        text = hasDescription.GetDescription();
                        break;

                    case Enum e:
                        text = e.GetLocalisableDescription();
                        break;

                    case LocalisableString l:
                        text = l;
                        break;

                    default:
                        text = value.ToString();
                        break;
                }

                Children = new Drawable[]
                {
                    Text = new ProjectYomiSpriteText
                    {
                        Margin = new MarginPadding { Top = 5, Bottom = 5 },
                        Origin = Anchor.BottomLeft,
                        Anchor = Anchor.BottomLeft,
                        Text = text,
                        Font = AXPlayerApp.DefaultFont.With(size: 14)
                    },
                    Bar = new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Alpha = 0,
                        Colour = Color4.White,
                        Origin = Anchor.BottomLeft,
                        Anchor = Anchor.BottomLeft,
                    },
                    new HoverSounds(HoverSampleSet.TabSelect)
                };
            }

            private Sample selectSample;

            [BackgroundDependencyLoader]
            private void load(ProjectYomiColour colours, AudioManager audio)
            {
                if (accentColour == default)
                    AccentColour = colours.Blue;

                selectSample = audio.Samples.Get(@"UI/tabselect-select");
            }

            protected override void OnActivated()
            {
                Text.Font = Text.Font.With(weight: "Bold");
                FadeHovered();
            }

            protected override void OnDeactivated()
            {
                Text.Font = Text.Font.With(weight: "Medium");
                FadeUnhovered();
            }

            protected override void OnActivatedByUser() => selectSample.Play();
        }
    }
}

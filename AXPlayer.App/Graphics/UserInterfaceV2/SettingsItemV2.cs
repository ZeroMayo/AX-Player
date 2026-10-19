// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;

namespace AXPlayer.App.Graphics.UserInterfaceV2
{
    public sealed partial class SettingsItemV2 : CompositeDrawable, IConditionalFilterable
    {
        public readonly IFormControl Control;

        private readonly SettingsRevertToDefaultButton revertButton;

        private readonly BindableBool controlDefault = new BindableBool(true);
        private readonly BindableBool controlEnabled = new BindableBool(true);

        /// <summary>
        /// Whether a revert button should be displayed when the control is modified away from default state.
        /// </summary>
        public bool ShowRevertToDefaultButton { get; init; } = true;

        /// <summary>
        /// A note to display underneath the setting.
        /// </summary>
        public readonly Bindable<SettingsNote.Data?> Note = new Bindable<SettingsNote.Data?>();

        public SettingsItemV2(IFormControl control)
        {
            Control = control;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 30 },
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new[]
                        {
                            revertButton = new SettingsRevertToDefaultButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Action = ApplyDefault,
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Child = (Drawable)control,
                            }
                        }
                    },
                    new SettingsNote
                    {
                        RelativeSizeAxes = Axes.X,
                        Current = { BindTarget = Note },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            controlDefault.Value = Control.IsDefault;
            controlEnabled.Value = !Control.IsDisabled;

            controlDefault.BindValueChanged(_ => updateDefaultState());
            controlEnabled.BindValueChanged(_ => updateDefaultState(), true);
            FinishTransforms(true);

            ScheduleAfterChildren(() =>
            {
                revertButton.RelativeSizeAxes = Axes.None;
            });
        }

        private void updateDefaultState()
        {
            bool showRevertButton = !controlDefault.Value && controlEnabled.Value && ShowRevertToDefaultButton;

            if (showRevertButton)
                revertButton.Show();
            else
                revertButton.Hide();
        }

        protected override void Update()
        {
            base.Update();
            controlDefault.Value = Control.IsDefault;
            controlEnabled.Value = !Control.IsDisabled;
        }

        #region ISettingsItem

        public bool HasClassicDefault => ApplyClassicDefault != null;

        /// <summary>
        /// If set, this setting is considered as having a "classic" default value,
        /// and this is the function for overwriting the control with that value.
        /// </summary>
        public Action<IFormControl>? ApplyClassicDefault { get; set; }

        public void ApplyDefault()
        {
            if (!Control.IsDisabled)
                Control.SetDefault();
        }

        public event Action SettingChanged
        {
            add => Control.ValueChanged += value;
            remove => Control.ValueChanged -= value;
        }

        #endregion

        #region Filtering

        public const string CLASSIC_DEFAULT_SEARCH_TERM = @"has-classic-default";

        public IEnumerable<string> Keywords { get; init; } = Enumerable.Empty<string>();

        public IEnumerable<LocalisableString> FilterTerms
        {
            get
            {
                var filterTerms = new List<LocalisableString>(Keywords.Select(k => (LocalisableString)k));
                filterTerms.AddRange(Control.FilterTerms);

                if (HasClassicDefault)
                    filterTerms.Add(CLASSIC_DEFAULT_SEARCH_TERM);

                return filterTerms;
            }
        }

        private bool matchingFilter = true;

        public bool MatchingFilter
        {
            get => matchingFilter;
            set
            {
                bool wasPresent = IsPresent;

                matchingFilter = value;

                if (IsPresent != wasPresent)
                    Invalidate(Invalidation.Presence);
            }
        }

        public override bool IsPresent => base.IsPresent && MatchingFilter;

        public bool FilteringActive { get; set; }

        public BindableBool CanBeShown { get; } = new BindableBool(true);

        IBindable<bool> IConditionalFilterable.CanBeShown => CanBeShown;

        #endregion
    }
}

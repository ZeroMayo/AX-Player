// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using AXPlayer.App.Overlays;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class PlaybackSpeedContainer : ProjectYomiClickableContainer, IHasPopover
    {
        private static readonly double[] speed_presets = { 0.5, 0.75, 1, 1.25, 1.5, 2 };

        public Bindable<double> Current;

        [Resolved]
        private AXPlayerConfigManager config { get; set; }

        [Resolved]
        private OverlayColourProvider overlayColourProvider { get; set; }

        [Resolved]
        private ProjectYomiColour colour { get; set; }

        [Resolved]
        private NewToastOverlay newToastOverlay { get; set; }

        protected override bool OnClick(ClickEvent e)
        {
            this.ShowPopover();
            return base.OnClick(e);
        }

        public Popover GetPopover()
        {
            List<double> customPresets = parsePresets(config.Get<string>(AXPlayerSetting.CustomPlaybackSpeedPresets));
            FillFlowContainer presetButtons = new FillFlowContainer
            {
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
            };

            void refreshPresetButtons()
            {
                presetButtons.Clear();

                foreach (double speed in speed_presets)
                {
                    presetButtons.Add(new RoundedButton
                    {
                        Width = 60,
                        Height = 50,
                        Text = formatSpeed(speed),
                        Action = () =>
                        {
                            Current.Value = speed;
                            newToastOverlay.ShowToast(AXPlayerStrings.PlaybackSpeed(speed));
                        },
                        BackgroundColour = overlayColourProvider.Background3,
                    });
                }

                foreach (double speed in customPresets)
                {
                    presetButtons.Add(new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(2, 0),
                        Children = new Drawable[]
                        {
                            new RoundedButton
                            {
                                Width = 60,
                                Height = 50,
                                Text = formatSpeed(speed),
                                Action = () =>
                                {
                                    Current.Value = speed;
                                    newToastOverlay.ShowToast(AXPlayerStrings.PlaybackSpeed(speed));
                                },
                                BackgroundColour = overlayColourProvider.Background3,
                            },
                            new RoundedButton
                            {
                                Width = 28,
                                Height = 50,
                                Text = "x",
                                Action = () =>
                                {
                                    customPresets.Remove(speed);
                                    savePresets();
                                },
                                BackgroundColour = colour.Red3,
                            },
                        },
                    });
                }
            }

            void savePresets()
            {
                config.GetBindable<string>(AXPlayerSetting.CustomPlaybackSpeedPresets).Value = string.Join(",",
                    customPresets.Select(speed => speed.ToString("0.##", CultureInfo.InvariantCulture)));
                refreshPresetButtons();
            }

            bool isValidNewPreset(string text, out double speed)
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out speed)
                    && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out speed))
                    return false;

                speed = Math.Round(speed, 2);
                return speed >= 0.1 && speed <= 8 && !speed_presets.Contains(speed) && !customPresets.Contains(speed);
            }

            FormNumberBox speedInput = new FormNumberBox(allowDecimals: true)
            {
                Icon = FontAwesome.Solid.TachometerAlt,
                Caption = AXPlayerStrings.PlaybackSpeedWithoutValue,
                PlaceholderText = "0.1 - 8",
                Current = { Value = string.Empty },
            };
            BindableBool canAddPreset = new BindableBool(false);
            speedInput.Current.ValueChanged += textChanged =>
            {
                canAddPreset.Value = isValidNewPreset(textChanged.NewValue, out _);
            };

            refreshPresetButtons();

            Drawable speedSlider = new SettingsItemV2(new FormSliderBar<double>
            {
                Icon = FontAwesome.Solid.TachometerAlt,
                Caption = AXPlayerStrings.PlaybackSpeedWithoutValue,
                Current = Current,
                DisplayAsPercentage = true,
                KeyboardStep = 0.05f,
            });

            Drawable presetsContent = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 50,
                Child = new ProjectYomiScrollContainer(Direction.Horizontal)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = presetButtons,
                    Padding = new MarginPadding { Horizontal = 30 },
                    ScrollbarVisible = false,
                },
            };

            Drawable addPresetContent = new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 50,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Padding = new MarginPadding { Horizontal = 30 },
                Content = new []
                {
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 50,
                            Size = new Vector2(0.97f, 1f),
                            Child = speedInput,
                        },
                        new RoundedButton
                        {
                            Width = 60,
                            Height = 50,
                            Text = "+",
                            Enabled = { BindTarget = canAddPreset },
                            Action = () =>
                            {
                                if (!isValidNewPreset(speedInput.Current.Value, out double speed))
                                    return;

                                customPresets.Add(speed);
                                speedInput.Current.Value = string.Empty;
                                savePresets();
                            },
                        },
                    },
                },
            };

            OverlaySortTabControl<LocalisableString> tabs = new OverlaySortTabControl<LocalisableString>(true)
            {
                Height = 32,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
            tabs.TabControl.AddItem(AXPlayerStrings.PlaybackSpeedWithoutValue);
            tabs.TabControl.AddItem(AXPlayerStrings.PlaybackSpeedPresets);
            tabs.TabControl.AddItem(AXPlayerStrings.AddPlaybackSpeedPreset);

            Container tabsContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    tabs,
                }
            };

            PlaybackSpeedTabPage speedSliderPage = new PlaybackSpeedTabPage(speedSlider)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                IsActive = true,
            };
            PlaybackSpeedTabPage presetsPage = new PlaybackSpeedTabPage(presetsContent)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };
            PlaybackSpeedTabPage addPresetPage = new PlaybackSpeedTabPage(addPresetContent)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
            };

            void updateTabContent(LocalisableString selectedTab)
            {
                speedSliderPage.IsActive = selectedTab.Equals(AXPlayerStrings.PlaybackSpeedWithoutValue);
                presetsPage.IsActive = selectedTab.Equals(AXPlayerStrings.PlaybackSpeedPresets);
                addPresetPage.IsActive = selectedTab.Equals(AXPlayerStrings.AddPlaybackSpeedPreset);
            }

            tabs.Current.ValueChanged += valueChanged => updateTabContent(valueChanged.NewValue);
            tabs.Current.Value = AXPlayerStrings.PlaybackSpeedWithoutValue;
            updateTabContent(AXPlayerStrings.PlaybackSpeedWithoutValue);

            FillFlowContainer tabContent = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[] { speedSliderPage, presetsPage, addPresetPage },
            };

            return new ProjectYomiPopover(false)
            {
                // Ensure the popover doesn't cover up the text input.
                AllowableAnchors = [Anchor.TopCentre, Anchor.BottomCentre],
                Child = new Container
                {
                    Width = 500,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Vertical = 8 },
                    CornerRadius = AXPlayerApp.UI_CORNER_RADIUS,
                    Masking = true,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Children = new Drawable[] { tabContent, tabsContainer },
                    },
                },
            };
        }

        private sealed partial class PlaybackSpeedTabPage : Container
        {
            private bool isActive;

            public bool IsActive
            {
                get => isActive;
                set
                {
                    if (isActive == value)
                        return;

                    bool wasPresent = IsPresent;
                    isActive = value;

                    if (IsPresent != wasPresent)
                        Invalidate(Invalidation.Presence);
                }
            }

            public override bool IsPresent => base.IsPresent && isActive;

            public PlaybackSpeedTabPage(Drawable content)
            {
                Child = content;
            }
        }

        private static List<double> parsePresets(string value)
        {
            List<double> presets = new List<double>();

            foreach (string part in value.Split(','))
            {
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double speed)
                    && speed >= 0.1 && speed <= 8)
                {
                    speed = Math.Round(speed, 2);
                    if (!speed_presets.Contains(speed) && !presets.Contains(speed))
                        presets.Add(speed);
                }
            }

            return presets;
        }

        private static string formatSpeed(double speed) => speed.ToString("0.##", CultureInfo.InvariantCulture) + "x";
    }
}

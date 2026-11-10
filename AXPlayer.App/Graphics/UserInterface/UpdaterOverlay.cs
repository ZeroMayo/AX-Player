// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using AXPlayer.App.Overlays;
using AXPlayer.App.Updater;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osuTK;
using OverlayContainer = AXPlayer.App.Graphics.Containers.OverlayContainer;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class UpdaterOverlay : SideOverlayContainer
    {
        public Action? CloseAction { get; set; }

        private LinkFlowContainer gameVersion;

        private Bindable<ReleaseStream> configReleaseStream;

        [Resolved]
        private UpdateManager updateManager { get; set; } = null!;

        private Container updateStatusContainer;
        private ProjectYomiTextFlowContainer updateStatusText;
        private ProjectYomiScrollContainer updatingList;

        public UpdaterOverlay()
        {
            Size = new Vector2(1f, 1f);
            Width = 540;
            RelativeSizeAxes = Axes.Y;
            CornerRadius = new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS, AXPlayerApp.UI_CORNER_RADIUS, 0, 0);
            Masking = true;
            Origin = Anchor.CentreRight;
            Anchor = Anchor.CentreRight;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider, TextureStore textures, AXPlayerApp game, AXPlayerConfigManager appConfig)
        {
            configReleaseStream = appConfig.GetBindable<ReleaseStream>(AXPlayerSetting.ReleaseStream);

            Children = new Drawable[]
            {
                new OverlayBackground
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new ProjectYomiScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 16, Top = 56 },
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Direction = FillDirection.Horizontal,
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        Name = "App Info",
                                        AutoSizeAxes = Axes.Both,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Margin = new MarginPadding { Top = 12 },
                                        Child = new Container
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Child = new Sprite
                                            {
                                                Width = 100,
                                                Height = 100,
                                                Texture = textures.Get(@"AXPlayer-Icon"),
                                                FillMode = FillMode.Fit,
                                            }
                                        },
                                    },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Children = new Drawable[]
                                        {
                                            new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"))
                                            {
                                                AutoSizeAxes = Axes.Both,
                                                Text = "AX Player",
                                                TextAnchor = Anchor.Centre,
                                                Colour = overlayColourProvider.Content2,
                                            },
                                            gameVersion = new LinkFlowContainer(f =>
                                            {
                                                f.Font = AXPlayerApp.DefaultFont.With(size: 15);
                                                f.Colour = overlayColourProvider.Content2;
                                            })
                                            {
                                                Margin = new MarginPadding { Top = 4 },
                                                AutoSizeAxes = Axes.Both,
                                                TextAnchor = Anchor.Centre,
                                            },
                                        },
                                    },
                                }
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                        Children = new Drawable[]
                                        {
                                            new SettingsItemV2(releaseStreamDropdown = new FormEnumDropdown<ReleaseStream>
                                            {
                                                Caption = AXPlayerStrings.ReleaseStream,
                                                Current = { Value = configReleaseStream.Value },
                                                Icon = FontAwesome.Solid.Sync,
                                                CornerType = FormControlCornerType.Top,
                                            }),
                                            new SettingsItemV2(new FormCheckBox
                                            {
                                                Caption = AXPlayerStrings.CheckUpdatesOnAppOpen,
                                                Current = appConfig.GetBindable<bool>(AXPlayerSetting.CheckUpdatesOnAppOpen),
                                                Icon = FontAwesome.Solid.Sync,
                                                CornerType = FormControlCornerType.Bottom,
                                            }),
                                        }
                                    },
                                    updateStatusContainer = new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Padding = new MarginPadding { Horizontal = 16 },
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                AutoSizeAxes = Axes.Both,
                                                Masking = true,
                                                CornerRadius = AXPlayerApp.UI_CORNER_RADIUS + 16,
                                                Children = new Drawable[]
                                                {
                                                    new Box
                                                    {
                                                        Colour = overlayColourProvider.Background4,
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                    updateStatusText = new ProjectYomiTextFlowContainer
                                                    {
                                                        AutoSizeAxes = Axes.Both,
                                                    }
                                                }
                                            }
                                        },
                                    },
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Padding = new MarginPadding { Horizontal = 16, Bottom = 16 },
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Masking = true,
                                                CornerRadius = AXPlayerApp.UI_CORNER_RADIUS + 16,
                                                Children = new Drawable[]
                                                {
                                                    new Box
                                                    {
                                                        Colour = overlayColourProvider.Background4,
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                    updatingList = new ProjectYomiScrollContainer
                                                    {
                                                        ProgressiveBlurEnabled = false,
                                                        RelativeSizeAxes = Axes.Both,
                                                        Padding = new MarginPadding(16),
                                                        Masking = true,
                                                        Children = new Drawable[]
                                                        {
                                                        }
                                                    }
                                                }
                                            }
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new Box
                {
                    Name = "masking of overlay",
                    RelativeSizeAxes = Axes.X,
                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5, overlayColourProvider.Background5.Opacity(0)),
                    Height = 76,
                },
                new Box
                {
                    Name = "masking of overlay",
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Colour = ColourInfo.GradientVertical(overlayColourProvider.Background5.Opacity(0), overlayColourProvider.Background5),
                    Height = 76,
                },
                new IconButton
                {
                    Enabled = { Value = true },
                    Origin = Anchor.TopRight,
                    Anchor = Anchor.TopRight,
                    Size = new Vector2(35, 35),
                    IconScale = new Vector2(0.8f),
                    Margin = new MarginPadding(14),
                    Icon = FontAwesome.Solid.Times,
                    BackgroundColour = overlayColourProvider.Background4,
                    Action = () => CloseAction?.Invoke(),
                },
                new ProjectYomiSpriteText
                {
                    Origin = Anchor.TopCentre,
                    Anchor = Anchor.TopCentre,
                    Text = AXPlayerStrings.UpdaterTitle,
                    Margin = new MarginPadding(16),
                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                    Colour = overlayColourProvider.Content2,
                },
            };

            if (game.IsDeployedBuild)
            {
                gameVersion.AddText(AXPlayerStrings.VersionText(string.Empty));
                gameVersion.AddLink(game.Version, $"https://github.com/ZeroMayo/AX-Player/releases/{game.Version}", tooltipText: AXPlayerStrings.ViewChangelog(game.Version));
            }
            else
            {
                gameVersion.AddText(AXPlayerStrings.VersionText(game.Version));
            }

            releaseStreamDropdown.Current.BindValueChanged(releaseStreamChanged);
        }

        public void UpdateStatus(UpdaterStatus updaterStatus)
        {
            switch (updaterStatus)
            {
                case UpdaterStatus.NoUpdates:
                case UpdaterStatus.Error:
                case UpdaterStatus.Checking:
                    updateStatusContainer.Show();
                    break;

                case UpdaterStatus.RestartRequired:
                case UpdaterStatus.Updating:
                    updateStatusContainer.Hide();
                    break;
            }
        }

        [Resolved]
        private PopupDialogOverlay popupDialogOverlay { get; set; }

        private FormEnumDropdown<ReleaseStream> releaseStreamDropdown;

        private void releaseStreamChanged(ValueChangedEvent<ReleaseStream> stream)
        {
            switch (stream.NewValue)
            {
                case ReleaseStream.Stable:
                    configReleaseStream.Value = stream.NewValue;
                    break;

                default:
                    popupDialogOverlay.ShowDialog(
                        AXPlayerStrings.ChangeReleaseStreamConfirmation,
                        AXPlayerStrings.ChangeReleaseStreamConfirmationInfo,
                        AXPlayerStrings.Yes,
                        AXPlayerStrings.Cancel,
                        okPressedAction: () => configReleaseStream.Value = stream.NewValue,
                        cancelPressedAction: () => releaseStreamDropdown.Current.Value = stream.OldValue);
                    break;
            }
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Data;
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

        public Action? CheckUpdateAction { get; set; }

        private LinkFlowContainer gameVersion;

        private Bindable<ReleaseStream> configReleaseStream;

        [Resolved]
        private UpdateManager updateManager { get; set; } = null!;

        private Container updateStatusContainer, updateInfoContainer;
        private ProjectYomiTextFlowContainer updateStatusText;
        private ProjectYomiScrollContainer updatingList;

        private Bindable<AXPlayerUpdateInfo> updateInfo;

        private Bindable<UpdaterStatus> updateStatus;

        private RoundedIconButton updateButton;

        private Bindable<bool> updateButtonEnabled;

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
        private void load(OverlayColourProvider overlayColourProvider, TextureStore textures, AXPlayerAppBase game, AXPlayerConfigManager appConfig)
        {
            updateStatus = game.UpdateStatus.GetBoundCopy();
            updateInfo = game.UpdateInfo.GetBoundCopy();
            updateButtonEnabled = game.UpdateButtonEnabled.GetBoundCopy();
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
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new BufferedContainer(pixelSnapping: false)
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 200,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Children = new Drawable[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Masking = true,
                                                Child = new Sprite
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Texture = textures.Get(@"AXPlayerBanner"),
                                                    FillMode = FillMode.Fill,
                                                }
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
                                                Height = 130,
                                            },
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Anchor = Anchor.Centre,
                                                Origin = Anchor.Centre,
                                                Masking = true,
                                                Child = new Sprite
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    Texture = textures.Get(@"AXPlayerBanner_Foreground"),
                                                    FillMode = FillMode.Fill,
                                                }
                                            },
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
                                            gameVersion = new LinkFlowContainer(f =>
                                            {
                                                f.Font = AXPlayerApp.DefaultFont.With(size: 18, weight: "ExtraBold");
                                                f.Colour = overlayColourProvider.Content2;
                                            })
                                            {
                                                Margin = new MarginPadding { Top = 95 },
                                                AutoSizeAxes = Axes.Both,
                                                TextAnchor = Anchor.Centre,
                                            },
                                        },
                                    },
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Child = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = overlayColourProvider.Background5,
                                        }
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                        Padding = new MarginPadding { Horizontal = 16 },
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
                                                    new CircularContainer
                                                    {
                                                        Anchor = Anchor.Centre,
                                                        Origin = Anchor.Centre,
                                                        AutoSizeAxes = Axes.Both,
                                                        Masking = true,
                                                        Children = new Drawable[]
                                                        {
                                                            new Box
                                                            {
                                                                Colour = overlayColourProvider.Background4,
                                                                RelativeSizeAxes = Axes.Both,
                                                            },
                                                            updateStatusText = new ProjectYomiTextFlowContainer
                                                            {
                                                                Margin = new MarginPadding(8),
                                                                AutoSizeAxes = Axes.Both,
                                                                Text = AXPlayerStrings.RunningLatestReleaseWithoutVersion,
                                                            }
                                                        }
                                                    }
                                                },
                                            },
                                            updateInfoContainer = new Container
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Height = 450,
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
                                }
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
                updateButton = new RoundedIconButton(FontAwesome.Solid.Sync)
                {
                    Enabled = { Value = true },
                    Origin = Anchor.BottomCentre,
                    Anchor = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Size = new Vector2(0.9f, 50),
                    Text = AXPlayerStrings.CheckUpdate,
                    Margin = new MarginPadding(16),
                    Action = () => CheckUpdateAction?.Invoke(),
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

            updateInfoContainer.Hide();

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

            updateStatus.BindValueChanged(status =>
            {
                Schedule(() => UpdateStatus(status.NewValue));
            });

            updateButtonEnabled.BindValueChanged(enabled =>
            {
                Schedule(() => updateButton.Enabled.Value = enabled.NewValue);
            });

            updateInfo.BindValueChanged(info =>
            {
                Schedule(() =>
                {
                    if (info.NewValue != null)
                    {
                        updatingList.Add(new VersionInfoContainer(info.NewValue));
                    }
                });
            });
        }

        public void UpdateStatus(UpdaterStatus updaterStatus)
        {
            switch (updaterStatus)
            {
                case UpdaterStatus.NoUpdates:
                    updateStatusContainer.Show();
                    updateInfoContainer.Hide();
                    updateStatusText.Text = AXPlayerStrings.RunningLatestReleaseWithoutVersion;
                    break;
                case UpdaterStatus.Error:
                    updateStatusContainer.Show();
                    updateInfoContainer.Hide();
                    updateStatusText.Text = AXPlayerStrings.UpdateFailed;
                    break;
                case UpdaterStatus.Checking:
                    updateStatusContainer.Show();
                    updateInfoContainer.Hide();
                    updateStatusText.Text = AXPlayerStrings.CheckingUpdate;
                    break;

                case UpdaterStatus.RestartRequired:
                case UpdaterStatus.Updating:
                    updateStatusContainer.Hide();
                    updateInfoContainer.Show();
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

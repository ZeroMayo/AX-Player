// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;

namespace AXPlayer.App.Updater
{
    public partial class VersionInfoContainer : Container
    {
        private AXPlayerUpdateInfo updateInfo;
        private Bindable<LocalisableString> updateState = null!;
        private ProjectYomiTextFlowContainer updateStateText = null!;
        private RoundedIconButton restartButton = null!;

        public VersionInfoContainer(AXPlayerUpdateInfo updateInfo)
        {
            this.updateInfo = updateInfo;
        }

        [BackgroundDependencyLoader]
        private void load(AXPlayerAppBase game, OverlayColourProvider overlayColourProvider)
        {
            RelativeSizeAxes = osu.Framework.Graphics.Axes.X;
            AutoSizeAxes = osu.Framework.Graphics.Axes.Y;
            Masking = true;
            CornerRadius = AXPlayerApp.UI_CORNER_RADIUS;

            updateState = game.UpdateManagerVersionText.GetBoundCopy();

            Children = new Drawable[]
            {
                new Box
                {
                    Colour = overlayColourProvider.Background3,
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Children = new Drawable[]
                            {
                                new ProjectYomiTextFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Text = updateInfo.Version
                                },
                                updateStateText = new ProjectYomiTextFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                }
                            }
                        },
                        restartButton = new RoundedIconButton(FontAwesome.Solid.Sync)
                        {
                            Size = new Vector2(102, 28),
                            Text = AXPlayerStrings.RestartNow,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Enabled = { Value = false },
                            Action = () => game.RestartAction.Invoke(),
                        },
                    },
                },
            };

            updateState.BindValueChanged(text =>
            {
                UpdateStatusText(text.NewValue);
            });
        }

        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n2} {suffixes[counter]}";
        }

        public void UpdateState(UpdaterStatus updaterStatus)
        {
            switch (updaterStatus)
            {
                case UpdaterStatus.RestartRequired:
                    restartButton.Enabled.Value = true;
                    break;
                default:
                    restartButton.Enabled.Value = false;
                    break;
            }
        }

        public void UpdateStatusText(LocalisableString text)
        {
            Schedule(() => updateStateText.Text = text);
        }
    }
}

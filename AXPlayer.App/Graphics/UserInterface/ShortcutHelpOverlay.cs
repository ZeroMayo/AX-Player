// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Linq;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Colour;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ShortcutHelpOverlay : SideOverlayContainer
    {
        public Action? CloseAction { get; set; }

        [Resolved]
        private GlobalShortcutManager shortcutManager { get; set; } = null!;

        private IBindable<string> shortcutBindings = null!;
        private RoundedIconButton resetAllButton = null!;

        public ShortcutHelpOverlay()
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
        private void load(OverlayColourProvider overlayColourProvider)
        {
            Children = new Drawable[]
            {
                new OverlayBackground
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new ProjectYomiScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ScrollbarVisible = true,
                        Padding = new MarginPadding
                        {
                            Horizontal = 16,
                            Top = 56,
                            Bottom = 72,
                        },
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(4),
                                Children = shortcutManager.GetBindings()
                                    .GroupBy(binding => (GlobalAction)binding.Action)
                                    .Select(group => (Drawable)new ShortcutRow(group.Key, AXPlayerStrings.ShortcutDescription(group.Key), shortcutManager))
                                    .ToArray(),
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
                resetAllButton = new RoundedIconButton(FontAwesome.Solid.Undo)
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Size = new Vector2(0.9f, 50),
                    Text = AXPlayerStrings.ResetAllShortcuts,
                    Margin = new MarginPadding(16),
                    Action = () => shortcutManager.ResetAllBindings(),
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
                    Text = AXPlayerStrings.ShortcutHelpTitle,
                    Margin = new MarginPadding(16),
                    Font = AXPlayerApp.DefaultFont.With(size: 30, weight: "ExtraBold"),
                    Colour = overlayColourProvider.Content2,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            shortcutBindings = shortcutManager.BindingsChanged.GetBoundCopy();
            shortcutBindings.BindValueChanged(updateResetAllButton);
            updateResetAllButton();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && shortcutBindings != null)
                shortcutBindings.UnbindEvents();

            base.Dispose(isDisposing);
        }

        private void updateResetAllButton() => resetAllButton.Enabled.Value = shortcutManager.HasCustomBindings;

        private void updateResetAllButton(ValueChangedEvent<string> _) => updateResetAllButton();

        private partial class ShortcutRow : CompositeDrawable
        {
            private readonly GlobalAction action;
            private readonly GlobalShortcutManager shortcutManager;
            private readonly IBindable<GlobalAction?> capturingAction;
            private readonly IBindable<bool> captureConflict;
            private readonly IBindable<string> shortcutBindings;
            private ProjectYomiTextFlowContainer captureMessage = null!;
            private RoundedButton changeButton = null!;
            private RoundedIconOnlyButton resetButton = null!;

            public ShortcutRow(GlobalAction action, LocalisableString description, GlobalShortcutManager shortcutManager)
            {
                this.action = action;
                this.shortcutManager = shortcutManager;
                capturingAction = shortcutManager.CapturingAction.GetBoundCopy();
                captureConflict = shortcutManager.CaptureConflict.GetBoundCopy();
                shortcutBindings = shortcutManager.BindingsChanged.GetBoundCopy();
                RelativeSizeAxes = Axes.X;
                Height = 72;
                Masking = true;
                CornerRadius = AXPlayerApp.UI_CORNER_RADIUS;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White.Opacity(0.04f),
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 5 },
                        Content = new []
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new ProjectYomiTextFlowContainer(t => t.Font = AXPlayerApp.DefaultFont.With(size: 16, weight: "SemiBold"))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = description,
                                            Colour = Colour4.White,
                                            TextAnchor = Anchor.CentreLeft,
                                        },
                                        captureMessage = new ProjectYomiTextFlowContainer(t => t.Font = AXPlayerApp.DefaultFont.With(size: 12))
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Colour = Colour4.Orange,
                                            TextAnchor = Anchor.CentreLeft,
                                        },
                                    },
                                },
                                new Container
                                {
                                    AutoSizeAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    Margin = new MarginPadding { Right = 12 },
                                    Child = new HotkeyDisplay
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Hotkey = new Hotkey(action),
                                    },
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(4),
                                    Children = new Drawable[]
                                    {
                                        changeButton = new RoundedButton
                                        {
                                            Size = new Vector2(102, 28),
                                            Text = AXPlayerStrings.ChangeShortcut,
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Action = () => shortcutManager.BeginCapture(action),
                                        },
                                        resetButton = new RoundedIconOnlyButton(FontAwesome.Solid.Undo)
                                        {
                                            Size = new Vector2(28, 28),
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Action = () => shortcutManager.ResetBinding(action),
                                        },
                                    },
                                },
                            },
                        }
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                capturingAction.BindValueChanged(updateState);
                captureConflict.BindValueChanged(updateState);
                shortcutBindings.BindValueChanged(updateState);
                updateState();
            }

            protected override void Dispose(bool isDisposing)
            {
                if (isDisposing)
                {
                    capturingAction.UnbindEvents();
                    captureConflict.UnbindEvents();
                    shortcutBindings.UnbindEvents();
                }

                base.Dispose(isDisposing);
            }

            private void updateState()
            {
                bool isCapturing = shortcutManager.CapturingAction.Value == action;
                changeButton.Text = isCapturing
                    ? AXPlayerStrings.PressShortcut
                    : AXPlayerStrings.ChangeShortcut;
                resetButton.Enabled.Value = shortcutManager.IsCustomized(action);
                captureMessage.Text = isCapturing && shortcutManager.CaptureConflict.Value
                    ? AXPlayerStrings.ShortcutAlreadyAssigned
                    : string.Empty;

                if (isCapturing && shortcutManager.CaptureConflict.Value)
                    captureMessage.Show();
                else
                    captureMessage.Hide();
            }

            private void updateState<T>(ValueChangedEvent<T> _) => updateState();
        }
    }
}

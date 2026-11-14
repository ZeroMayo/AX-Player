// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV3;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Overlays;
using AXPlayer.App.Utils;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.Input
{
    [TestFixture]
    public partial class TestScenePlaybackSpeedPresetShortcuts : AXPlayerTestScene
    {
        [Resolved]
        private AXPlayerConfigManager config { get; set; } = null!;

        [Test]
        public void TestDefaultPresetBindings()
        {
            AddAssert("ctrl+1 applies preset 1", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset1, InputKey.Control, InputKey.Number1));
            AddAssert("ctrl+2 applies preset 2", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset2, InputKey.Control, InputKey.Number2));
            AddAssert("ctrl+3 applies preset 3", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset3, InputKey.Control, InputKey.Number3));
            AddAssert("ctrl+4 applies preset 4", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset4, InputKey.Control, InputKey.Number4));
            AddAssert("ctrl+5 applies preset 5", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset5, InputKey.Control, InputKey.Number5));
            AddAssert("ctrl+6 applies preset 6", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset6, InputKey.Control, InputKey.Number6));
            AddAssert("ctrl+7 applies preset 7", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset7, InputKey.Control, InputKey.Number7));
            AddAssert("ctrl+8 applies preset 8", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset8, InputKey.Control, InputKey.Number8));
            AddAssert("ctrl+9 applies preset 9", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset9, InputKey.Control, InputKey.Number9));
            AddAssert("ctrl+0 applies preset 10", () => hasBinding(GlobalAction.ApplyCustomPlaybackSpeedPreset10, InputKey.Control, InputKey.Number0));
        }

        [Test]
        public void TestPresetIndexMapping()
        {
            AddAssert("first preset action maps to index 0", () => getPresetIndex(GlobalAction.ApplyCustomPlaybackSpeedPreset1) == 0);
            AddAssert("last preset action maps to index 9", () => getPresetIndex(GlobalAction.ApplyCustomPlaybackSpeedPreset10) == 9);
            AddAssert("other actions are ignored", () => getPresetIndex(GlobalAction.IncreasePlaybackSpeed) == null);
        }

        [Test]
        public void TestPresetParsing()
        {
            AddAssert("built-in, invalid and duplicate presets are ignored", () =>
                PlaybackSpeedPresets.Parse("1.1,1.5,2,0.9,1.1,8.5,abc").SequenceEqual(new[] { 1.1, 0.9 }));

            AddAssert("custom presets round-trip", () =>
                PlaybackSpeedPresets.Serialize(PlaybackSpeedPresets.Parse("1.1,0.9")) == "1.1,0.9");
        }

        [Test]
        public void TestCustomPresetShortcutDisplay()
        {
            PlaybackSpeedContainer speedContainer = null!;
            Drawable popover = null!;

            AddStep("set custom presets", () => config.GetBindable<string>(AXPlayerSetting.CustomPlaybackSpeedPresets).Value = "1.1,0.9");
            AddStep("create playback speed container", () => Add(new ToastOverlayCache(speedContainer = new PlaybackSpeedContainer
            {
                Current = new BindableNumber<double>(1)
                {
                    MinValue = 0.1,
                    MaxValue = 8,
                    Precision = 0.01,
                },
            })));
            AddStep("create preset popover", () =>
            {
                popover = (Drawable)speedContainer.GetPopover();
                Add(popover);
            });

            AddAssert("custom presets display their shortcuts", () =>
            {
                HotkeyDisplay[] displays = popover.ChildrenOfType<HotkeyDisplay>().ToArray();

                return displays.Length == 2
                       && displays.Select(display => display.Hotkey.GlobalAction).SequenceEqual(new GlobalAction?[]
                       {
                           GlobalAction.ApplyCustomPlaybackSpeedPreset1,
                           GlobalAction.ApplyCustomPlaybackSpeedPreset2,
                       });
            });
        }

        private static bool hasBinding(GlobalAction action, params InputKey[] keys)
        {
            InputKey[] expected = keys.OrderBy(key => key).ToArray();

            return GlobalActionContainer.GlobalKeyBindings.Any(binding =>
                (GlobalAction)binding.Action == action
                && binding.KeyCombination.Keys.OrderBy(key => key).SequenceEqual(expected));
        }

        private static int? getPresetIndex(GlobalAction action) =>
            GlobalActionContainer.TryGetCustomPlaybackSpeedPresetIndex(action, out int index) ? index : null;

        private partial class ToastOverlayCache : Container
        {
            [Cached]
            private readonly NewToastOverlay toastOverlay = new NewToastOverlay();

            public ToastOverlayCache(Drawable child)
            {
                RelativeSizeAxes = Axes.Both;
                Child = child;
            }
        }
    }
}

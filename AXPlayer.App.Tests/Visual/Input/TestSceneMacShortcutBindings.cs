// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using AXPlayer.App.Input.Binding;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Input.Bindings;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.Input
{
    [TestFixture]
    public partial class TestSceneMacShortcutBindings : AXPlayerTestScene
    {
        [Test]
        public void TestCommandMirrorsControlShortcuts()
        {
            AddAssert("every ctrl shortcut has a command equivalent", () =>
            {
                KeyBinding[] macBindings = GlobalActionContainer.MacKeyBindings.ToArray();

                return GlobalActionContainer.GlobalKeyBindings
                    .Where(binding => binding.KeyCombination.Keys.Contains(InputKey.Control))
                    .All(control => macBindings.Any(mac =>
                        (GlobalAction)mac.Action == (GlobalAction)control.Action
                        && mac.KeyCombination.Keys.SequenceEqual(control.KeyCombination.Keys.Select(toCommand))));
            });
        }

        [Test]
        public void TestStandardMacShortcuts()
        {
            AddAssert("cmd+q quits the app", () => hasMacBinding(GlobalAction.QuitApp, InputKey.Super, InputKey.Q));
            AddAssert("cmd+, opens settings", () => hasMacBinding(GlobalAction.OpenSettings, InputKey.Super, InputKey.Comma));
        }

        [Test]
        public void TestMacShortcutsDoNotConflict()
        {
            AddAssert("command shortcuts are unique and do not collide with existing shortcuts", () =>
            {
                KeyBinding[] macBindings = GlobalActionContainer.MacKeyBindings.ToArray();

                KeyCombination[] existing = GlobalActionContainer.GlobalKeyBindings
                    .Where(binding => !binding.KeyCombination.Keys.Contains(InputKey.Super))
                    .Select(binding => binding.KeyCombination)
                    .ToArray();

                return macBindings.Select(binding => binding.KeyCombination).Distinct().Count() == macBindings.Length
                       && macBindings.All(binding => !existing.Contains(binding.KeyCombination));
            });
        }

        [Test]
        public void TestMacShortcutsArePlatformSpecific()
        {
            AddAssert("mac shortcuts are only active on macOS", () =>
            {
                KeyBinding[] macBindings = GlobalActionContainer.MacKeyBindings.ToArray();
                KeyBinding[] activeBindings = GlobalActionContainer.GlobalKeyBindings.ToArray();

                if (RuntimeInfo.OS == RuntimeInfo.Platform.macOS)
                    return macBindings.All(mac => activeBindings.Any(active => active.KeyCombination.Equals(mac.KeyCombination)));

                return activeBindings.All(binding => !binding.KeyCombination.Keys.Contains(InputKey.Super));
            });
        }

        private static InputKey toCommand(InputKey key) => key == InputKey.Control ? InputKey.Super : key;

        private static bool hasMacBinding(GlobalAction action, params InputKey[] keys)
        {
            InputKey[] expected = keys.OrderBy(key => key).ToArray();

            return GlobalActionContainer.MacKeyBindings.Any(binding =>
                (GlobalAction)binding.Action == action
                && binding.KeyCombination.Keys.OrderBy(key => key).SequenceEqual(expected));
        }
    }
}

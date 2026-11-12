// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AXPlayer.App.Config;
using osu.Framework.Bindables;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osuTK.Input;

namespace AXPlayer.App.Input.Binding
{
    public class GlobalShortcutManager
    {
        private readonly Bindable<string> storedOverrides;
        private Dictionary<GlobalAction, KeyCombination> overrides = new();

        public Bindable<GlobalAction?> CapturingAction { get; } = new();
        public Bindable<bool> CaptureConflict { get; } = new();
        public Bindable<string> BindingsChanged => storedOverrides;
        public bool HasCustomBindings => overrides.Count > 0;

        public GlobalShortcutManager(AXPlayerConfigManager config)
        {
            storedOverrides = config.GetBindable<string>(AXPlayerSetting.GlobalShortcutBindings);
            storedOverrides.BindValueChanged(e => loadOverrides(e.NewValue), true);
        }

        public IEnumerable<KeyBinding> GetBindings()
        {
            var overriddenActions = overrides.Keys.ToHashSet();
            return GlobalActionContainer.GlobalKeyBindings
                .Where(binding => !overriddenActions.Contains((GlobalAction)binding.Action))
                .Concat(overrides.Select(entry => new KeyBinding(entry.Value, entry.Key)))
                .ToArray();
        }

        public bool IsCustomized(GlobalAction action) => overrides.ContainsKey(action);

        public string GetShortcutLabel(GlobalAction action, string fallback)
        {
            var bindings = GetBindings()
                .Where(binding => (GlobalAction)binding.Action == action)
                .Select(binding => string.Join("+", binding.KeyCombination.Keys
                    .OrderBy(getDisplayKeyOrder)
                    .Select(getDisplayKey)))
                .ToArray();

            return bindings.Length == 0 ? fallback : string.Join(", ", bindings);
        }

        public void BeginCapture(GlobalAction action)
        {
            CaptureConflict.Value = false;
            CapturingAction.Value = action;
        }

        public void CancelCapture()
        {
            CapturingAction.Value = null;
            CaptureConflict.Value = false;
        }

        public void ResetBinding(GlobalAction action)
        {
            if (!overrides.Remove(action))
                return;

            saveOverrides();
            CaptureConflict.Value = false;
        }

        public void ResetAllBindings()
        {
            CancelCapture();

            if (overrides.Count == 0)
                return;

            overrides.Clear();
            saveOverrides();
        }

        public bool Capture(KeyDownEvent e)
        {
            if (CapturingAction.Value == null)
                return false;

            if (e.Repeat)
                return true;

            if (e.Key.ToString() == nameof(Key.Escape))
            {
                CancelCapture();
                return true;
            }

            var keys = new HashSet<InputKey>();
            InputKey? primaryKey = null;

            foreach (Key pressedKey in e.PressedKeys.Append(e.Key).Distinct())
            {
                string keyName = pressedKey.ToString();

                if (keyName.Contains("Shift", StringComparison.Ordinal))
                    keys.Add(InputKey.Shift);
                else if (keyName.Contains("Control", StringComparison.Ordinal))
                    keys.Add(InputKey.Control);
                else if (keyName.Contains("Alt", StringComparison.Ordinal))
                    keys.Add(InputKey.Alt);
                else if (keyName.Contains("Win", StringComparison.Ordinal) || keyName.Contains("Super", StringComparison.Ordinal))
                    keys.Add(InputKey.Super);
                else if (Enum.TryParse(keyName, ignoreCase: true, out InputKey inputKey)
                         && inputKey != InputKey.None
                         && inputKey != InputKey.LastKey)
                {
                    primaryKey ??= inputKey;
                }
            }

            if (primaryKey == null)
                return true;

            keys.Add(primaryKey.Value);
            var combination = new KeyCombination(keys.OrderBy(key => key).ToArray());
            GlobalAction action = CapturingAction.Value.Value;

            if (hasConflict(action, combination))
            {
                CaptureConflict.Value = true;
                return true;
            }

            overrides[action] = combination;
            saveOverrides();
            CapturingAction.Value = null;
            CaptureConflict.Value = false;
            return true;
        }

        private void loadOverrides(string serialized)
        {
            overrides = new Dictionary<GlobalAction, KeyCombination>();
            if (string.IsNullOrWhiteSpace(serialized))
                return;

            Dictionary<string, string>? stored;
            try
            {
                stored = JsonSerializer.Deserialize<Dictionary<string, string>>(serialized);
            }
            catch (JsonException exception)
            {
                Logger.Error(exception, "Could not read custom keyboard shortcuts.");
                return;
            }

            if (stored == null)
                return;

            foreach (var entry in stored)
            {
                if (!Enum.TryParse(entry.Key, ignoreCase: false, out GlobalAction action)
                    || !Enum.IsDefined(action))
                {
                    Logger.Log($"Ignoring unknown custom keyboard shortcut action '{entry.Key}'.");
                    continue;
                }

                var keys = new List<InputKey>();
                bool valid = true;
                foreach (string keyName in entry.Value?.Split('+', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
                {
                    if (!Enum.TryParse(keyName, ignoreCase: false, out InputKey key)
                        || key == InputKey.None
                        || key == InputKey.LastKey)
                    {
                        valid = false;
                        break;
                    }

                    keys.Add(key);
                }

                if (!valid || keys.Count == 0 || keys.All(isModifier))
                {
                    Logger.Log($"Ignoring invalid custom keyboard shortcut for '{entry.Key}'.");
                    continue;
                }

                var combination = new KeyCombination(keys);
                if (hasConflict(action, combination))
                {
                    Logger.Log($"Ignoring conflicting custom keyboard shortcut for '{entry.Key}'.");
                    continue;
                }

                overrides[action] = combination;
            }
        }

        private bool hasConflict(GlobalAction action, KeyCombination combination) =>
            GetBindings().Any(binding =>
                (GlobalAction)binding.Action != action
                && binding.KeyCombination.Keys.OrderBy(key => key).SequenceEqual(combination.Keys.OrderBy(key => key)));

        private void saveOverrides()
        {
            var serialized = overrides.ToDictionary(
                entry => entry.Key.ToString(),
                entry => string.Join("+", entry.Value.Keys.OrderBy(key => key)));

            storedOverrides.Value = JsonSerializer.Serialize(serialized);
        }

        private static bool isModifier(InputKey key) =>
            key is InputKey.Shift or InputKey.Control or InputKey.Alt or InputKey.Super;

        private static int getDisplayKeyOrder(InputKey key) => key switch
        {
            InputKey.Control => 0,
            InputKey.Shift => 1,
            InputKey.Alt => 2,
            InputKey.Super => 3,
            _ => 4,
        };

        private static string getDisplayKey(InputKey key) => key switch
        {
            InputKey.Control => "Ctrl",
            InputKey.Shift => "Shift",
            InputKey.Alt => "Alt",
            InputKey.Super => "Super",
            _ => key.ToString(),
        };
    }
}

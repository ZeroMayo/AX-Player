// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using AXPlayer.App.Input.Binding;

namespace AXPlayer.App.Graphics.UserInterface
{
    public readonly record struct Hotkey
    {
        public KeyCombination[]? KeyCombinations { get; init; }
        public GlobalAction? GlobalAction { get; init; }
        public PlatformAction? PlatformAction { get; init; }
        public FrameworkAction? FrameworkAction { get; init; }

        public Hotkey(params KeyCombination[] keyCombinations)
        {
            KeyCombinations = keyCombinations;
        }

        public Hotkey(GlobalAction globalAction)
        {
            GlobalAction = globalAction;
        }

        public Hotkey(PlatformAction platformAction)
        {
            PlatformAction = platformAction;
        }

        public IEnumerable<string> ResolveKeyCombination(ReadableKeyCombinationProvider keyCombinationProvider, GameHost gameHost)
        {
            var result = new List<string>();

            if (KeyCombinations != null)
            {
                result.AddRange(KeyCombinations.Select(keyCombinationProvider.GetReadableString));
            }

            if (GlobalAction != null)
            {
                var action = GlobalAction.Value;
                var bindings = GlobalActionContainer.GlobalKeyBindings.Where(kb => (GlobalAction)kb.Action == action);
                result.AddRange(bindings.Select(b => keyCombinationProvider.GetReadableString(b.KeyCombination)));
            }

            if (PlatformAction != null)
            {
                var action = PlatformAction.Value;
                var bindings = gameHost.PlatformKeyBindings.Where(kb => (PlatformAction)kb.Action == action);
                result.AddRange(bindings.Select(b => keyCombinationProvider.GetReadableString(b.KeyCombination)));
            }

            return result;
        }
    }
}

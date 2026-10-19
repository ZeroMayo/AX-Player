// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace AXPlayer.App.Localisation
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public enum GoogleTranslateLanguage
    {
        [Description(@"Auto detect")]
        auto,

        [Description(@"English")]
        en,

        [Description(@"日本語")]
        ja,

        [Description(@"한국어")]
        ko,
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Screens;

namespace AXPlayer.App.Screens
{
    public interface IAXPlayerScreen : IScreen
    {
        /// <summary>
        /// Whether this <see cref="AXPlayerScreen"/> allows the cursor to be displayed.
        /// </summary>
        bool CursorVisible { get; }
    }
}

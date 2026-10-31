// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using AXPlayer.App.Graphics.UserInterface;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.Cursor
{
    public partial class ProjectYomiContextMenuContainer : ContextMenuContainer
    {
        private ProjectYomiContextMenu menu = null!;

        protected override Menu CreateMenu() => menu = new ProjectYomiContextMenu(true);

        public void CloseMenu()
        {
            menu.Close();
        }
    }
}

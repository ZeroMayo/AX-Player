// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK.Input;
using AXPlayer.App.Localisation;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class SearchTextBox : FocusedTextBox
    {
        protected virtual bool AllowCommit => false;

        public SearchTextBox()
        {
            Height = 35;
            PlaceholderText = AXPlayerStrings.SearchPlaceholder;
        }

        public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            switch (e.Action)
            {
                case PlatformAction.MoveBackwardLine:
                case PlatformAction.MoveForwardLine:
                // Shift+delete is handled via PlatformAction on macOS. this is not so useful in the context of a SearchTextBox
                // as we do not allow arrow key navigation in the first place (ie. the caret should always be at the end of text)
                // Avoid handling it here to allow other components to potentially consume the shortcut.
                case PlatformAction.DeleteForwardChar:
                    return false;
            }

            return base.OnPressed(e);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!e.ControlPressed && !e.ShiftPressed)
            {
                switch (e.Key)
                {
                    case Key.Left:
                    case Key.Right:
                    case Key.Up:
                    case Key.Down:
                        return false;
                }
            }

            if (!AllowCommit)
            {
                switch (e.Key)
                {
                    case Key.KeypadEnter:
                    case Key.Enter:
                        // even if committing per se is not allowed for this textbox,
                        // the commit flow is also responsible for terminating any active IME.
                        // ensure that the Enter press terminates IME correctly
                        // and is also handled if it needs to be, so that it doesn't leak to some other non-focused drawable and cause breakage.
                        bool wasImeComposing = ImeCompositionActive;
                        FinalizeImeComposition(true);
                        return wasImeComposing;
                }
            }

            if (e.ShiftPressed)
            {
                switch (e.Key)
                {
                    case Key.Delete:
                        return false;
                }
            }

            return base.OnKeyDown(e);
        }
    }
}

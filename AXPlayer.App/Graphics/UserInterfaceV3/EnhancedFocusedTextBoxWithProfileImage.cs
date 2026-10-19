// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Graphics.UserInterface;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    public partial class EnhancedFocusedTextBoxWithProfileImage : FocusedTextBoxWithProfileImage
    {
        public Action OnEnterKeyPressed;

        protected override void OnTextCommitted(bool textChanged)
        {
            base.OnTextCommitted(textChanged);
            OnEnterKeyPressed.Invoke();
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Platform;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    /// <summary>
    /// Contrary to <see cref="Display.Equals(osu.Framework.Platform.Display?)"/>, this comparer disregards the value of <see cref="Display.Bounds"/>.
    /// We want to just show a list of displays, and for the purposes of settings we don't care about their bounds when it comes to the list.
    /// However, <see cref="IWindow.DisplaysChanged"/> fires even if only the resolution of the current display was changed
    /// (because it causes the bounds of all displays to also change).
    /// We're not interested in those changes, so compare only the rest that we actually care about.
    /// This helps to avoid a bindable/event feedback loop, in which a resolution change
    /// would trigger a display "change", which would in turn reset resolution again.
    /// </summary>
    public class DisplayListComparer : IEqualityComparer<Display>
    {
        public static readonly DisplayListComparer DEFAULT = new DisplayListComparer();

        public bool Equals(Display? x, Display? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;

            return x.Index == y.Index
                   && x.Name == y.Name
                   && x.DisplayModes.SequenceEqual(y.DisplayModes);
        }

        public int GetHashCode(Display obj)
        {
            var hashCode = new HashCode();

            hashCode.Add(obj.Index);
            hashCode.Add(obj.Name);
            hashCode.Add(obj.DisplayModes.Length);
            foreach (var displayMode in obj.DisplayModes)
                hashCode.Add(displayMode);

            return hashCode.ToHashCode();
        }
    }
}

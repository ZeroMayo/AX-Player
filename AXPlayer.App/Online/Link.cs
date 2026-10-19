// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace AXPlayer.App.Online
{
    public class Link : IComparable<Link>
    {
        public string Url;
        public int Index;
        public int Length;
        public object Argument;

        public Link(string url, int startIndex, int length, object argument)
        {
            Url = url;
            Index = startIndex;
            Length = length;
            Argument = argument;
        }

        public bool Overlaps(Link otherLink) => Overlaps(otherLink.Index, otherLink.Length);

        public bool Overlaps(int otherIndex, int otherLength) => Index < otherIndex + otherLength && otherIndex < Index + Length;

        public int CompareTo(Link? otherLink) => Index > otherLink?.Index ? 1 : -1;
    }
}

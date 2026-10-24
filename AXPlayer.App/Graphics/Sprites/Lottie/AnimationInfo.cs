// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace AXPlayer.App.Graphics.Sprites.Lottie
{
    public sealed record AnimationInfo(string Version, TimeSpan Duration, double FramesPerSecond, double InPoint, double OutPoint);
}

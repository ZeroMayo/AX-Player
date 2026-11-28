// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace AXPlayer.App.Graphics.Transitions
{
    /// <summary>
    /// Plays Material Design shared element transitions (the "container transform" pattern) above the rest of the UI.
    /// </summary>
    /// <remarks>
    /// A transition draws a proxy of the source element at the source's bounds and animates it into the bounds of the
    /// destination element while interpolating the corner radius. The source stays hidden for the duration of the
    /// transition, so only the proxy is visible.
    /// </remarks>
    public partial class SharedElementTransitionLayer : Container
    {
        /// <summary>
        /// The name assigned to the proxy that is animated across the screen.
        /// </summary>
        public const string PROXY_NAME = @"shared element transition";

        /// <summary>
        /// The default duration of the bounds animation, in milliseconds.
        /// </summary>
        public const double DEFAULT_DURATION = 400;

        /// <summary>
        /// The default duration used to fade the proxy out once the bounds animation has finished, in milliseconds.
        /// </summary>
        public const double DEFAULT_FADE_OUT_DURATION = 160;

        /// <summary>
        /// The corner radius of a source element when none is specified.
        /// </summary>
        public const float DEFAULT_SOURCE_CORNER_RADIUS = 12;

        /// <summary>
        /// The number of transitions that are currently being played by this layer.
        /// </summary>
        public int ActiveTransitionCount { get; private set; }

        /// <summary>
        /// Whether any transition is currently being played by this layer.
        /// </summary>
        public bool IsTransitioning => ActiveTransitionCount > 0;

        public SharedElementTransitionLayer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Plays a transition between two elements, rendering the given texture as the moving proxy.
        /// </summary>
        /// <param name="source">The element the transition starts from. It is hidden while the transition plays.</param>
        /// <param name="destination">The element the transition ends at.</param>
        /// <param name="texture">The texture displayed by the moving proxy.</param>
        /// <param name="sourceCornerRadius">The corner radius of the source element.</param>
        /// <param name="destinationCornerRadius">The corner radius of the destination element.</param>
        /// <param name="duration">The duration of the bounds animation, in milliseconds.</param>
        /// <param name="easing">The easing applied to the bounds animation.</param>
        /// <param name="fadeOutDuration">The duration used to fade the proxy out after the bounds animation, in milliseconds.</param>
        /// <param name="completed">An optional callback invoked once the transition has finished.</param>
        /// <param name="restoreSource">Whether the source becomes visible again once the transition has finished.</param>
        /// <returns><c>true</c> if the transition was started; <c>false</c> if it was skipped.</returns>
        public bool Play(Drawable source, Drawable destination, Texture texture, float sourceCornerRadius = DEFAULT_SOURCE_CORNER_RADIUS,
                         float destinationCornerRadius = 0, double duration = DEFAULT_DURATION, Easing easing = Easing.OutQuint,
                         double fadeOutDuration = DEFAULT_FADE_OUT_DURATION, Action? completed = null, bool restoreSource = true)
        {
            ArgumentNullException.ThrowIfNull(texture);

            return Play(source, destination, () => new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Stretch,
                Texture = texture,
            }, sourceCornerRadius, destinationCornerRadius, duration, easing, fadeOutDuration, completed, restoreSource);
        }

        /// <summary>
        /// Plays a transition between two elements, rendering the given content as the moving proxy.
        /// </summary>
        /// <param name="source">The element the transition starts from. It is hidden while the transition plays.</param>
        /// <param name="destination">The element the transition ends at.</param>
        /// <param name="contentFactory">Creates the content displayed by the moving proxy.</param>
        /// <param name="sourceCornerRadius">The corner radius of the source element.</param>
        /// <param name="destinationCornerRadius">The corner radius of the destination element.</param>
        /// <param name="duration">The duration of the bounds animation, in milliseconds.</param>
        /// <param name="easing">The easing applied to the bounds animation.</param>
        /// <param name="fadeOutDuration">The duration used to fade the proxy out after the bounds animation, in milliseconds.</param>
        /// <param name="completed">An optional callback invoked once the transition has finished.</param>
        /// <param name="restoreSource">Whether the source becomes visible again once the transition has finished.</param>
        /// <returns><c>true</c> if the transition was started; <c>false</c> if it was skipped.</returns>
        public bool Play(Drawable source, Drawable destination, Func<Drawable> contentFactory, float sourceCornerRadius = DEFAULT_SOURCE_CORNER_RADIUS,
                         float destinationCornerRadius = 0, double duration = DEFAULT_DURATION, Easing easing = Easing.OutQuint,
                         double fadeOutDuration = DEFAULT_FADE_OUT_DURATION, Action? completed = null, bool restoreSource = true)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentNullException.ThrowIfNull(contentFactory);

            if (!IsAlive || !source.IsAlive || !destination.IsAlive)
                return false;

            // An invisible source has nothing to animate from. This also prevents starting a second transition
            // from the same source, whose completion would restore the alpha captured by the first transition.
            if (source.Alpha <= 0)
                return false;

            RectangleF startBounds = boundsOf(source);
            RectangleF endBounds = boundsOf(destination);

            // A drawable that has not been laid out yet has no meaningful bounds to animate between.
            if (startBounds.Width <= 0 || startBounds.Height <= 0 || endBounds.Width <= 0 || endBounds.Height <= 0)
                return false;

            Drawable content = contentFactory();

            if (content == null)
                return false;

            float sourceAlpha = source.Alpha;
            source.Alpha = 0;

            var proxy = new Container
            {
                Name = PROXY_NAME,
                Position = new Vector2(startBounds.X, startBounds.Y),
                Size = new Vector2(startBounds.Width, startBounds.Height),
                Masking = true,
                CornerRadius = new CornersInfo(sourceCornerRadius),
                Child = content,
            };

            AddInternal(proxy);
            ActiveTransitionCount++;

            proxy.MoveTo(new Vector2(endBounds.X, endBounds.Y), duration, easing);
            proxy.ResizeTo(new Vector2(endBounds.Width, endBounds.Height), duration, easing);
            proxy.TransformTo(nameof(proxy.CornerRadius), new CornersInfo(destinationCornerRadius), duration, easing);

            proxy.Delay(duration).FadeOut(fadeOutDuration, Easing.OutQuad).OnComplete(_ =>
            {
                if (restoreSource && source.IsAlive)
                    source.Alpha = sourceAlpha;

                ActiveTransitionCount--;
                completed?.Invoke();
                proxy.Expire();
            });

            return true;
        }

        /// <summary>
        /// Computes the axis-aligned bounds of a drawable in this layer's coordinate space.
        /// </summary>
        private RectangleF boundsOf(Drawable drawable)
        {
            RectangleF rectangle = drawable.DrawRectangle;

            Vector2 topLeft = drawable.ToSpaceOfOtherDrawable(rectangle.TopLeft, this);
            Vector2 bottomRight = drawable.ToSpaceOfOtherDrawable(rectangle.BottomRight, this);

            return new RectangleF(
                MathF.Min(topLeft.X, bottomRight.X),
                MathF.Min(topLeft.Y, bottomRight.Y),
                MathF.Abs(bottomRight.X - topLeft.X),
                MathF.Abs(bottomRight.Y - topLeft.Y));
        }
    }
}

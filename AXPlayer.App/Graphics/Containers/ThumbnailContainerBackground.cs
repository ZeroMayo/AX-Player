// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Transforms;
using osuTK;
using osuTK.Graphics;
using AXPlayer.App.Config;

namespace AXPlayer.App.Graphics.Containers
{
    /// <summary>
    /// A background which offers blurring via a <see cref="BufferedContainer"/> on demand.
    /// </summary>
    public partial class ThumbnailContainerBackground : BufferedContainer, IEquatable<ThumbnailContainerBackground>
    {
        public Sprite Sprite = null!;

        private string textureName;

        private TextureStore textureStore = null!;

        public ThumbnailContainerBackground(string textureName = @"")
        {
            this.textureName = textureName;
        }

        private Bindable<string> currentThumbnailUrl = null!;

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, SessionStatics sessionStatics)
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    Sprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        Texture = textures.Get(@"black")
                    },
                }
            };
            textureStore = textures;

            currentThumbnailUrl = sessionStatics.GetBindable<string>(Static.CurrentThumbnailUrl);

            currentThumbnailUrl.BindValueChanged(url =>
            {
                setImageUrl(url.NewValue);
            });

            BlurTo(Vector2.Divide(new Vector2(10, 10), 1));
        }

        private void setImageUrl(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                Sprite.Texture = textureStore.Get(@"black");
                return;
            }
            Texture tex = textureStore.Get(url);
            Sprite.Texture = tex;
        }

        /// <summary>
        /// Smoothly adjusts <see cref="IBufferedContainer.BlurSigma"/> over time.
        /// </summary>
        /// <returns>A <see cref="TransformSequence{T}"/> to which further transforms can be added.</returns>
        public void BlurTo(Vector2 newBlurSigma, double duration = 0, Easing easing = Easing.None)
        {
            transformBlurSigma(newBlurSigma, duration, easing);
        }

        private void transformBlurSigma(Vector2 newBlurSigma, double duration, Easing easing)
            => this.TransformTo(nameof(blurSigma), newBlurSigma, duration, easing);

        private Vector2 blurSigmaBacking = Vector2.Zero;
        private Vector2 blurScale = Vector2.One;

        private Vector2 blurSigma
        {
            get => blurSigmaBacking;
            set
            {
                blurSigmaBacking = value;
                blurScale = new Vector2(calculateBlurDownscale(value.X), calculateBlurDownscale(value.Y));

                FrameBufferScale = blurScale;
                BlurSigma = value * blurScale; // If the image is scaled down, the blur radius also needs to be reduced to cover the same pixel block.
            }
        }

        /// <summary>
        /// Determines a factor to downscale the background based on a given blur sigma, in order to reduce the computational complexity of blurs.
        /// </summary>
        /// <param name="sigma">The blur sigma.</param>
        /// <returns>The scale-down factor.</returns>
        private float calculateBlurDownscale(float sigma)
        {
            // If we're blurring within one pixel, scaling down will always result in an undesirable loss of quality.
            // The algorithm below would also cause this value to go above 1, which is likewise undesirable.
            if (sigma <= 1)
                return 1;

            // A good value is one where the loss in quality as a result of downscaling the image is not easily perceivable.
            // The constants here have been experimentally chosen to yield nice transitions by approximating a log curve through the points {{ 1, 1 }, { 4, 0.75 }, { 16, 0.5 }, { 32, 0.25 }}.
            float scale = -0.18f * MathF.Log(0.004f * sigma);

            // To reduce shimmering, the scaling transitions are limited to happen only in increments of 0.2.
            return MathF.Round(scale / 0.2f, MidpointRounding.AwayFromZero) * 0.2f;
        }

        public virtual bool Equals(ThumbnailContainerBackground? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType()
                   && other.textureName == textureName;
        }
    }
}

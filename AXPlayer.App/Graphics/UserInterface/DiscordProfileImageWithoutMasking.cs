// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class DiscordProfileImageWithoutMasking : CompositeDrawable
    {
        private Sprite profileImage;
        private BufferedContainer blur;

        [Resolved]
        private TextureStore textureStore { get; set; }

        [Resolved]
        private Online.DiscordRPCClient api { get; set; }

        public DiscordProfileImageWithoutMasking(float size = 30)
        {
            Width = Height = size;
            //CornerRadius = size / 2;
            InternalChildren = new Drawable[]
            {
                blur = new BufferedContainer
                {
                    BlurSigma = new osuTK.Vector2(2),
                    RelativeSizeAxes = Axes.Both,
                    Child = profileImage = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            blur.Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(0.5f), Color4.White.Opacity(0));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            profileImage.Dispose();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            _ = Task.Run(async () =>
            {
                await GetProfileImage();
            });
        }

        public async Task GetProfileImage(CancellationToken cancellationToken = default)
        {
            Texture north = await textureStore.GetAsync(api.GetCurrentUser().GetAvatarURL(DiscordRPC.User.AvatarFormat.PNG, DiscordRPC.User.AvatarSize.x2048), cancellationToken);
            //GetPalette();
            Schedule(() => { profileImage.Texture = north; });
        }
    }
}

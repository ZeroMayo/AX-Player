// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class ProfileImage
    {
        public partial class ProfileImageTooltip : VisibilityContainer, ITooltip<Channel>
        {
            private YouTubeChannelMetadataDisplayWithShadow youtubeChannelMetadataDisplay;
            private Channel lastChannel;
            private bool instantMovement = true;

            [BackgroundDependencyLoader]
            private void load()
            {
                Width = 320;
                AutoSizeAxes = Axes.Y;

                InternalChild = youtubeChannelMetadataDisplay = new YouTubeChannelMetadataDisplayWithShadow()
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 60,
                };
            }

            protected override void PopIn()
            {
                instantMovement |= !IsPresent;
                this.FadeIn(500, Easing.OutQuint);
            }

            protected override void PopOut() => this.Delay(150).FadeOut(500, Easing.OutQuint);

            public void Move(Vector2 pos)
            {
                if (instantMovement)
                {
                    Position = pos;
                    instantMovement = false;
                }
                else
                {
                    this.MoveTo(pos, 200, Easing.OutQuint);
                }
            }

            public void SetContent(Channel channel)
            {
                if (lastChannel != null && lastChannel.Equals(channel))
                    return;

                lastChannel = channel;
                youtubeChannelMetadataDisplay.UpdateUser(channel);
            }
        }
    }
}

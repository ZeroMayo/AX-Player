// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AXPlayer.App.Graphics.UserInterface;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneProfileImages : AXPlayerTestScene
    {
        private ProfileImageWithoutMasking profileImage = null!;
        private DiscordProfileImageWithoutMasking discordProfileImage = null!;

        public TestSceneProfileImages()
        {
            Add(profileImage = new ProfileImageWithoutMasking());
            Add(discordProfileImage = new DiscordProfileImageWithoutMasking());
        }

        [Test]
        public void TestChannelWithoutIdIsIgnored()
        {
            AddAssert("no avatar is shown", () => !hasAvatar());

            AddStep("request an avatar without an id", () => profileImage.UpdateProfileImage(string.Empty));
            AddStep("request an avatar with a blank id", () => profileImage.UpdateProfileImage(@"   "));
            AddStep("request an avatar with a missing id", () => profileImage.UpdateProfileImage(null!));

            AddAssert("still no avatar is shown", () => !hasAvatar());
        }

        /// <summary>
        /// Discord does not have to be running, in which case there is no user and therefore no avatar to load.
        /// </summary>
        [Test]
        public void TestMissingDiscordUserIsIgnored()
        {
            Task load = null!;

            AddStep("request the Discord avatar", () => load = discordProfileImage.GetProfileImage());
            AddUntilStep("the request completed", () => load.IsCompleted);
            AddAssert("the request succeeded", () => load.IsCompletedSuccessfully);
        }

        private bool hasAvatar() => profileImage.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null);
    }
}

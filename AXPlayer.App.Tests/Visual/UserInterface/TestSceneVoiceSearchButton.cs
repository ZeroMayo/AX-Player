// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Graphics.UserInterfaceV3;
using AXPlayer.App.Utils.VoiceSearch;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;

namespace AXPlayer.App.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneVoiceSearchButton : AXPlayerTestScene
    {
        private FakeVoiceSearchService service = null!;
        private VoiceSearchButton button = null!;
        private string? requestedText;
        private int notificationCount;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create button", () =>
            {
                service = new FakeVoiceSearchService();
                requestedText = null;
                notificationCount = 0;

                Child = button = new VoiceSearchButton(service, () => "ko", _ => notificationCount++)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };

                button.SearchRequested = text => requestedText = text;
            });
        }

        [Test]
        public void TestServiceIsRegisteredAsInterface()
        {
            ServiceProbe probe = null!;

            AddStep("create service probe", () => Child = probe = new ServiceProbe());
            AddUntilStep("service resolved", () => probe.Service != null);
        }

        [Test]
        public void TestRecognisedSpeechIsRequested()
        {
            AddStep("click search by voice", () => button.TriggerClick());

            AddUntilStep("search requested", () => requestedText == "test query");
            AddAssert("button is no longer listening", () => !button.IsListening);
            AddAssert("preferred culture is passed", () => service.LastCultureCode == "ko");
        }

        [Test]
        public void TestNoSpeechShowsNotification()
        {
            AddStep("configure no speech result", () => service.Result = new VoiceSearchResult(VoiceSearchStatus.NoSpeech));
            AddStep("click search by voice", () => button.TriggerClick());

            AddUntilStep("result notification shown", () => notificationCount >= 2);
            AddAssert("no search requested", () => requestedText == null);
        }

        [Test]
        public void TestUnavailableDoesNotListen()
        {
            AddStep("configure unavailable", () => service.IsAvailable = false);
            AddStep("click search by voice", () => button.TriggerClick());

            AddUntilStep("notification shown", () => notificationCount == 1);
            AddAssert("recognition was not attempted", () => service.RecognitionCount == 0);
            AddAssert("no search requested", () => requestedText == null);
        }

        [Test]
        public void TestCancellationDiscardsResult()
        {
            AddStep("configure pending result", () => service.PendingResult = new TaskCompletionSource<VoiceSearchResult>());
            AddStep("click search by voice", () => button.TriggerClick());
            AddUntilStep("listening", () => button.IsListening);

            AddStep("cancel", () => button.Cancel());
            AddStep("complete recognition", () => service.PendingResult.SetResult(new VoiceSearchResult(VoiceSearchStatus.Success, "stale query")));

            AddUntilStep("not listening", () => !button.IsListening);
            AddAssert("no search requested", () => requestedText == null);
        }

        private partial class ServiceProbe : Drawable
        {
            [Resolved]
            public IVoiceSearchService Service { get; private set; } = null!;
        }

        private sealed class FakeVoiceSearchService : IVoiceSearchService
        {
            public bool IsAvailable { get; set; } = true;

            public VoiceSearchResult Result { get; set; } = new VoiceSearchResult(VoiceSearchStatus.Success, "test query");

            public TaskCompletionSource<VoiceSearchResult>? PendingResult { get; set; }

            public int RecognitionCount { get; private set; }

            public string? LastCultureCode { get; private set; }

            public Task<VoiceSearchResult> RecognizeAsync(string? cultureCode, CancellationToken cancellationToken = default)
            {
                RecognitionCount++;
                LastCultureCode = cultureCode;

                return PendingResult?.Task ?? Task.FromResult(Result);
            }
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Localisation;
using AXPlayer.App.Utils.VoiceSearch;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.UserInterfaceV3
{
    /// <summary>
    /// A microphone button which fills a search box using speech recognition.
    /// </summary>
    public partial class VoiceSearchButton : IconButton
    {
        private static readonly Color4 listening_colour = new Color4(255, 92, 92, 255);

        private readonly IVoiceSearchService service;
        private readonly Func<string?>? cultureCodeProvider;
        private readonly Action<LocalisableString>? notify;

        private CancellationTokenSource? recognitionCancellation;
        private Color4 idleIconColour = Color4.White;

        private Sample? openSample;
        private Sample? successSample;
        private Sample? failureSample;
        private Sample? noInputSample;

        /// <summary>
        /// Invoked on the update thread with the recognised transcript.
        /// </summary>
        public Action<string>? SearchRequested;

        public bool IsListening { get; private set; }

        public VoiceSearchButton(IVoiceSearchService service, Func<string?>? cultureCodeProvider = null, Action<LocalisableString>? notify = null)
        {
            this.service = service;
            this.cultureCodeProvider = cultureCodeProvider;
            this.notify = notify;

            Icon = FontAwesome.Solid.Microphone;
            TooltipText = AXPlayerStrings.VoiceSearch;

            Action = () => _ = recogniseAsync();
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            openSample = audio.Samples.Get(@"VoiceSearch/open");
            successSample = audio.Samples.Get(@"VoiceSearch/success");
            failureSample = audio.Samples.Get(@"VoiceSearch/failure");
            noInputSample = audio.Samples.Get(@"VoiceSearch/no_input");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            idleIconColour = IconColour;
        }

        /// <summary>
        /// Stops any active recognition request.
        /// </summary>
        public void Cancel() => recognitionCancellation?.Cancel();

        private async Task recogniseAsync()
        {
            if (IsListening || !Enabled.Value)
                return;

            if (!service.IsAvailable)
            {
                failureSample?.Play();
                notify?.Invoke(AXPlayerStrings.VoiceSearchUnavailable);
                return;
            }

            using (var cancellation = new CancellationTokenSource())
            {
                recognitionCancellation = cancellation;

                setListening(true);
                openSample?.Play();
                notify?.Invoke(AXPlayerStrings.VoiceSearchListening);

                VoiceSearchResult result;

                try
                {
                    result = await service.RecognizeAsync(cultureCodeProvider?.Invoke(), cancellation.Token).ConfigureAwait(false);

                    if (cancellation.IsCancellationRequested)
                        result = new VoiceSearchResult(VoiceSearchStatus.Cancelled);
                }
                catch (OperationCanceledException)
                {
                    result = new VoiceSearchResult(VoiceSearchStatus.Cancelled);
                }
                catch (Exception e)
                {
                    result = new VoiceSearchResult(VoiceSearchStatus.Error, Error: e.Message);
                }
                finally
                {
                    recognitionCancellation = null;
                }

                Schedule(() => handleResult(result));
            }
        }

        private void handleResult(VoiceSearchResult result)
        {
            setListening(false);

            switch (result.Status)
            {
                case VoiceSearchStatus.Success:
                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        successSample?.Play();
                        SearchRequested?.Invoke(result.Text.Trim());
                    }
                    else
                    {
                        noInputSample?.Play();
                        notify?.Invoke(AXPlayerStrings.VoiceSearchNoSpeech);
                    }
                    break;

                case VoiceSearchStatus.NoSpeech:
                    noInputSample?.Play();
                    notify?.Invoke(AXPlayerStrings.VoiceSearchNoSpeech);
                    break;

                case VoiceSearchStatus.Unavailable:
                    failureSample?.Play();
                    notify?.Invoke(AXPlayerStrings.VoiceSearchUnavailable);
                    break;

                case VoiceSearchStatus.PermissionDenied:
                    failureSample?.Play();
                    notify?.Invoke(AXPlayerStrings.VoiceSearchPermissionDenied);
                    break;

                case VoiceSearchStatus.Error:
                    failureSample?.Play();
                    notify?.Invoke(AXPlayerStrings.VoiceSearchFailed);
                    break;
            }
        }

        private void setListening(bool listening)
        {
            IsListening = listening;
            Enabled.Value = !listening;

            TooltipText = listening ? AXPlayerStrings.VoiceSearchListening : AXPlayerStrings.VoiceSearch;
            IconColour = listening ? listening_colour : idleIconColour;

            if (listening)
                this.ScaleTo(new Vector2(1.1f), 200, Easing.OutQuint);
            else
                this.ScaleTo(Vector2.One, 200, Easing.OutQuint);
        }

        protected override void Dispose(bool isDisposing)
        {
            recognitionCancellation?.Cancel();
            recognitionCancellation?.Dispose();
            recognitionCancellation = null;

            base.Dispose(isDisposing);
        }
    }
}

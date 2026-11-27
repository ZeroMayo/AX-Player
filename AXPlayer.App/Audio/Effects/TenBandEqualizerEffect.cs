// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using ManagedBass;
using osu.Framework.Audio.Mixing;

namespace AXPlayer.App.Audio.Effects
{
    /// <summary>
    /// Attaches a <see cref="TenBandEqualizer"/> to a BASS mixer through a DSP callback.
    /// </summary>
    public sealed class TenBandEqualizerEffect : IDisposable
    {
        private const int format_fallback_attempts = 32;

        private readonly AudioMixer mixer;
        private readonly DSPProcedure procedure;
        private readonly TenBandEqualizer equalizer = new TenBandEqualizer();

        private float[] sampleBuffer = Array.Empty<float>();
        private int sampleRate;
        private int channels;
        private bool formatKnown;
        private int formatFailureCount;

        /// <summary>
        /// Whether the equalizer is currently processing the mixer output.
        /// </summary>
        public bool IsAttached { get; private set; }

        /// <summary>
        /// The equalizer whose gains this effect applies.
        /// </summary>
        public TenBandEqualizer Equalizer => equalizer;

        public TenBandEqualizerEffect(AudioMixer mixer)
        {
            this.mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
            procedure = new DSPProcedure(process);
        }

        /// <summary>
        /// Sets the gain of a band.
        /// </summary>
        public void SetGain(int band, float gainDb) => equalizer.SetGain(band, gainDb);

        /// <summary>
        /// Adds or removes the equalizer from the mixer.
        /// </summary>
        public void SetAttached(bool attached)
        {
            if (attached)
                Attach();
            else
                Detach();
        }

        public void Attach()
        {
            if (IsAttached)
                return;

            formatKnown = false;
            formatFailureCount = 0;
            equalizer.Reset();
            mixer.AddDSP(procedure, 1);
            IsAttached = true;
        }

        public void Detach()
        {
            if (!IsAttached)
                return;

            mixer.RemoveDSP(procedure);
            IsAttached = false;
            equalizer.Reset();
        }

        private unsafe void process(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            if (length <= 0)
                return;

            if (!formatKnown && !resolveFormat(handle))
                return;

            int sampleCount = length / sizeof(float);

            if (sampleCount <= 0)
                return;

            if (sampleBuffer.Length < sampleCount)
                sampleBuffer = new float[sampleCount];

            Marshal.Copy(buffer, sampleBuffer, 0, sampleCount);
            equalizer.Process(sampleBuffer, sampleCount, channels, sampleRate);
            Marshal.Copy(sampleBuffer, 0, buffer, sampleCount);
        }

        private bool resolveFormat(int handle)
        {
            if (tryReadChannelFormat(handle))
                return true;

            // BASS can briefly report an invalid handle while devices are being initialised or freed.
            // Never let that escape the audio thread, and do not keep querying forever.
            if (++formatFailureCount < format_fallback_attempts)
                return false;

            return useKnownMixerFormat();
        }

        private bool tryReadChannelFormat(int handle)
        {
            try
            {
                ChannelInfo info = Bass.ChannelGetInfo(handle);

                if (info.Frequency <= 0 || info.Channels <= 0)
                    return false;

                sampleRate = info.Frequency;
                channels = info.Channels;
                formatKnown = true;
                return true;
            }
            catch (BassException)
            {
                return false;
            }
        }

        private bool useKnownMixerFormat()
        {
            // osu! mixers are always created with a fixed 44100 Hz, stereo format (see BassAudioMixer.createMixer).
            // Using that known format keeps the equalizer working even when BASS cannot report the channel info.
            sampleRate = 44100;
            channels = 2;
            formatKnown = true;
            return true;
        }

        public void Dispose() => Detach();
    }
}

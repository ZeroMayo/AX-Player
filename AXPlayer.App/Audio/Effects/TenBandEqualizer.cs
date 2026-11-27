// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace AXPlayer.App.Audio.Effects
{
    /// <summary>
    /// A simple 10-band graphic equalizer built from peaking biquad filters.
    /// Operates on interleaved 32-bit float samples, as delivered by BASS DSP callbacks.
    /// </summary>
    public sealed class TenBandEqualizer
    {
        /// <summary>
        /// The number of bands in the equalizer.
        /// </summary>
        public const int BAND_COUNT = 10;

        /// <summary>
        /// The center frequency of each band, in hertz.
        /// </summary>
        public static readonly int[] BAND_FREQUENCIES = { 32, 64, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

        /// <summary>
        /// The lowest gain a band can be set to, in decibels.
        /// </summary>
        public const float MIN_GAIN_DB = -12f;

        /// <summary>
        /// The highest gain a band can be set to, in decibels.
        /// </summary>
        public const float MAX_GAIN_DB = 12f;

        /// <summary>
        /// The smallest gain adjustment that can be made in the UI, in decibels.
        /// </summary>
        public const float GAIN_STEP_DB = 0.1f;

        /// <summary>
        /// The Q of each band. The bands are one octave apart, so this gives a fairly smooth response.
        /// </summary>
        private const float band_q = 1.4142135f;

        /// <summary>
        /// How quickly the filter follows gain changes. Prevents audible zipper noise while a slider is dragged.
        /// </summary>
        private const float smoothing_factor = 0.35f;

        private const float settled_gain_db = 0.005f;
        private const float coefficient_update_threshold_db = 0.0005f;

        private readonly float[] targetGainsDb = new float[BAND_COUNT];
        private readonly float[] currentGainsDb = new float[BAND_COUNT];
        private readonly float[] appliedGainsDb = new float[BAND_COUNT];

        private readonly float[] b0 = new float[BAND_COUNT];
        private readonly float[] b1 = new float[BAND_COUNT];
        private readonly float[] b2 = new float[BAND_COUNT];
        private readonly float[] a1 = new float[BAND_COUNT];
        private readonly float[] a2 = new float[BAND_COUNT];

        // Four values per band and channel: x1, x2, y1, y2.
        private float[] state = Array.Empty<float>();
        private int stateChannels;
        private int coefficientSampleRate;

        /// <summary>
        /// Whether every band is currently applying no gain.
        /// </summary>
        public bool IsFlat { get; private set; } = true;

        public TenBandEqualizer()
        {
            Reset();
        }

        /// <summary>
        /// Sets the gain of a band.
        /// </summary>
        /// <param name="band">The index of the band, see <see cref="BAND_FREQUENCIES"/>.</param>
        /// <param name="gainDb">The gain in decibels, clamped to the supported range.</param>
        public void SetGain(int band, float gainDb)
        {
            if (band < 0 || band >= BAND_COUNT)
                throw new ArgumentOutOfRangeException(nameof(band));

            targetGainsDb[band] = Math.Clamp(gainDb, MIN_GAIN_DB, MAX_GAIN_DB);
        }

        /// <summary>
        /// Returns the gain of a band, in decibels.
        /// </summary>
        public float GetGain(int band)
        {
            if (band < 0 || band >= BAND_COUNT)
                throw new ArgumentOutOfRangeException(nameof(band));

            return targetGainsDb[band];
        }

        /// <summary>
        /// Clears all filter history and jumps to the currently configured gains.
        /// The configured gains themselves are left untouched.
        /// </summary>
        public void Reset()
        {
            Array.Clear(state, 0, state.Length);

            for (int band = 0; band < BAND_COUNT; band++)
            {
                currentGainsDb[band] = targetGainsDb[band];
                appliedGainsDb[band] = float.NaN;
            }

            coefficientSampleRate = 0;
        }

        /// <summary>
        /// Processes a block of interleaved samples in place.
        /// </summary>
        /// <param name="buffer">The sample buffer.</param>
        /// <param name="sampleCount">The number of samples in the buffer.</param>
        /// <param name="channels">The number of interleaved channels.</param>
        /// <param name="sampleRate">The sample rate of the buffer, in hertz.</param>
        public void Process(float[] buffer, int sampleCount, int channels, int sampleRate)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (sampleCount <= 0 || channels <= 0 || sampleRate <= 0)
                return;

            sampleCount = Math.Min(sampleCount, buffer.Length);

            ensureState(channels);

            if (coefficientSampleRate != sampleRate)
            {
                coefficientSampleRate = sampleRate;

                for (int band = 0; band < BAND_COUNT; band++)
                    appliedGainsDb[band] = float.NaN;
            }

            bool isFlat = true;

            for (int band = 0; band < BAND_COUNT; band++)
            {
                float current = currentGainsDb[band];
                float target = targetGainsDb[band];

                if (current != target)
                {
                    current += (target - current) * smoothing_factor;

                    if (Math.Abs(target - current) < settled_gain_db)
                        current = target;

                    currentGainsDb[band] = current;
                }

                if (float.IsNaN(appliedGainsDb[band]) || Math.Abs(appliedGainsDb[band] - current) > coefficient_update_threshold_db)
                {
                    computeCoefficients(band, current, sampleRate);
                    appliedGainsDb[band] = current;
                }

                if (Math.Abs(appliedGainsDb[band]) > 0.0001f)
                    isFlat = false;
            }

            // A flat equalizer is exactly an identity filter, so the whole block can be skipped.
            if (isFlat)
            {
                if (!IsFlat)
                {
                    // Drop the history of the previous, non-flat response so a later boost does not start with stale samples.
                    IsFlat = true;
                    Array.Clear(state, 0, state.Length);
                }

                return;
            }

            IsFlat = false;

            int frames = sampleCount / channels;

            for (int frame = 0; frame < frames; frame++)
            {
                int sampleOffset = frame * channels;

                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = buffer[sampleOffset + channel];
                    int channelStateOffset = channel * BAND_COUNT * 4;

                    for (int band = 0; band < BAND_COUNT; band++)
                    {
                        int stateOffset = channelStateOffset + band * 4;

                        float output = b0[band] * sample
                                       + b1[band] * state[stateOffset]
                                       + b2[band] * state[stateOffset + 1]
                                       - a1[band] * state[stateOffset + 2]
                                       - a2[band] * state[stateOffset + 3];

                        state[stateOffset + 1] = state[stateOffset];
                        state[stateOffset] = sample;
                        state[stateOffset + 3] = state[stateOffset + 2];
                        state[stateOffset + 2] = output;

                        sample = output;
                    }

                    buffer[sampleOffset + channel] = sample;
                }
            }
        }

        private void ensureState(int channels)
        {
            if (stateChannels == channels)
                return;

            stateChannels = channels;
            state = new float[channels * BAND_COUNT * 4];
        }

        private void computeCoefficients(int band, float gainDb, int sampleRate)
        {
            // Keep the band below Nyquist so the filter stays well-defined at low sample rates.
            double frequency = Math.Min(BAND_FREQUENCIES[band], sampleRate * 0.45);
            double a = Math.Pow(10, gainDb / 40.0);
            double w0 = 2 * Math.PI * frequency / sampleRate;
            double alpha = Math.Sin(w0) / (2 * band_q);
            double cosW0 = Math.Cos(w0);
            double a0 = 1 + alpha / a;

            b0[band] = (float)((1 + alpha * a) / a0);
            b1[band] = (float)(-2 * cosW0 / a0);
            b2[band] = (float)((1 - alpha * a) / a0);
            a1[band] = (float)(-2 * cosW0 / a0);
            a2[band] = (float)((1 - alpha / a) / a0);
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Audio.Effects;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.UserInterfaceV2;
using NUnit.Framework;
using osu.Framework.Allocation;

namespace AXPlayer.App.Tests.Visual.Audio
{
    [TestFixture]
    public partial class TestSceneTenBandEqualizer : AXPlayerTestScene
    {
        [Resolved]
        private AudioEffectsConfigManager audioEffectsConfig { get; set; } = null!;

        [Test]
        public void TestFlatEqualizerIsTransparent()
        {
            var equalizer = new TenBandEqualizer();
            float[] buffer = createSineWave(48000, 2, 1000, 0.5f);
            float[] expected = (float[])buffer.Clone();

            equalizer.Process(buffer, buffer.Length, 2, 48000);

            Assert.That(equalizer.IsFlat, Is.True);
            Assert.That(buffer, Is.EqualTo(expected));
        }

        [Test]
        public void TestBoostedBandRaisesLevel()
        {
            var equalizer = new TenBandEqualizer();
            equalizer.SetGain(5, 6);

            // Let the gain smoothing settle before measuring a fresh buffer.
            for (int i = 0; i < 12; i++)
            {
                float[] settleBuffer = createSineWave(48000, 2, 1000, 0.25f);
                equalizer.Process(settleBuffer, settleBuffer.Length, 2, 48000);
            }

            float[] buffer = createSineWave(48000, 2, 1000, 0.25f);
            double before = rms(buffer, 0, buffer.Length);

            equalizer.Process(buffer, buffer.Length, 2, 48000);

            double after = rms(buffer, 0, buffer.Length);

            Assert.That(after, Is.GreaterThan(before * 1.5));
        }

        [Test]
        public void TestGainsAreClamped()
        {
            var equalizer = new TenBandEqualizer();

            equalizer.SetGain(0, 100);
            equalizer.SetGain(1, -100);

            Assert.That(equalizer.GetGain(0), Is.EqualTo(TenBandEqualizer.MAX_GAIN_DB));
            Assert.That(equalizer.GetGain(1), Is.EqualTo(TenBandEqualizer.MIN_GAIN_DB));
            Assert.Throws<ArgumentOutOfRangeException>(() => equalizer.SetGain(TenBandEqualizer.BAND_COUNT, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => equalizer.GetGain(-1));
        }

        [Test]
        public void TestDisplayResetsGains()
        {
            TenBandEqualizerDisplay display = null!;
            bool changed = false;

            AddStep("reset gains", () =>
            {
                for (int band = 0; band < TenBandEqualizer.BAND_COUNT; band++)
                    audioEffectsConfig.GetEqualizerGain(band).Value = 0;
            });

            AddStep("create display", () =>
            {
                Add(display = new TenBandEqualizerDisplay());
                display.ValueChanged += () => changed = true;
            });

            AddAssert("display starts flat", () => display.IsDefault);

            AddStep("raise and lower bands", () =>
            {
                audioEffectsConfig.GetEqualizerGain(0).Value = 6;
                audioEffectsConfig.GetEqualizerGain(9).Value = -6;
            });

            AddAssert("values are forwarded to the config", () =>
                audioEffectsConfig.GetEqualizerGain(0).Value == 6
                && audioEffectsConfig.GetEqualizerGain(9).Value == -6);

            AddAssert("display is no longer flat", () => !display.IsDefault);
            AddAssert("value changes are announced", () => changed);

            AddStep("reset to default", () => display.SetDefault());

            AddAssert("all gains are flat again", () =>
            {
                for (int band = 0; band < TenBandEqualizer.BAND_COUNT; band++)
                {
                    if (audioEffectsConfig.GetEqualizerGain(band).Value != 0)
                        return false;
                }

                return true;
            });
        }

        private static float[] createSineWave(int sampleRate, int channels, float frequency, float amplitude)
        {
            const int frame_count = 48000;
            float[] buffer = new float[frame_count * channels];

            for (int frame = 0; frame < frame_count; frame++)
            {
                float sample = amplitude * MathF.Sin(2 * MathF.PI * frequency * frame / sampleRate);

                for (int channel = 0; channel < channels; channel++)
                    buffer[frame * channels + channel] = sample;
            }

            return buffer;
        }

        private static double rms(float[] buffer, int start, int count)
        {
            double sum = 0;

            for (int i = start; i < start + count; i++)
                sum += buffer[i] * buffer[i];

            return Math.Sqrt(sum / count);
        }
    }
}

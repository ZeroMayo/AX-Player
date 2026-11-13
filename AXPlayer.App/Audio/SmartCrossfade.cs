// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace AXPlayer.App.Audio
{
    /// <summary>
    /// Result of analysing the beginning and end of a track for smart crossfading.
    /// </summary>
    public readonly record struct SmartCrossfadeAnalysis(
        double LeadInSilenceSeconds,
        double TailSilenceSeconds,
        double HeadRms,
        double TailRms,
        bool HasLeadAnalysis,
        bool HasTailAnalysis)
    {
        public static SmartCrossfadeAnalysis Empty => new SmartCrossfadeAnalysis(0, 0, 0, 0, false, false);
    }

    /// <summary>
    /// The parameters used for a single smart crossfade.
    /// </summary>
    public sealed class SmartCrossfadePlan
    {
        /// <summary>
        /// The length of the crossfade. This is never longer than the configured maximum or the remaining audible part of the outgoing track.
        /// </summary>
        public double Duration { get; init; }

        /// <summary>
        /// The position in the incoming track where playback should start. Leading silence is skipped when enabled.
        /// </summary>
        public double IncomingStartOffset { get; init; }

        /// <summary>
        /// The amount of silence detected at the end of the outgoing track.
        /// </summary>
        public double OutgoingTailSilence { get; init; }

        /// <summary>
        /// Creates a plan from the analysis of both tracks.
        /// </summary>
        public static SmartCrossfadePlan Create(
            SmartCrossfadeAnalysis outgoing,
            SmartCrossfadeAnalysis incoming,
            double configuredDuration,
            double outgoingDuration,
            bool trimSilence)
        {
            configuredDuration = Math.Clamp(configuredDuration, 1, 12);
            outgoingDuration = Math.Max(0, outgoingDuration);

            double tailSilence = trimSilence && outgoing.HasTailAnalysis
                ? Math.Clamp(outgoing.TailSilenceSeconds, 0, Math.Max(0, outgoingDuration - 0.5))
                : 0;

            double leadInSilence = trimSilence && incoming.HasLeadAnalysis
                ? Math.Clamp(incoming.LeadInSilenceSeconds, 0, 30)
                : 0;

            double audibleDuration = Math.Max(0.5, outgoingDuration - tailSilence);
            double duration = Math.Min(configuredDuration, audibleDuration);

            // If the outgoing track is already very quiet at its end it is probably fading out naturally.
            // A shorter overlap avoids dragging the fade out over the quiet part.
            if (outgoing.HasTailAnalysis && outgoing.TailRms > 0 && toDecibels(outgoing.TailRms) < -38)
                duration = Math.Min(duration, 2);

            return new SmartCrossfadePlan
            {
                Duration = Math.Max(0.8, duration),
                // Leave a very small pre-roll so the first transient of the incoming track is not clipped.
                IncomingStartOffset = Math.Max(0, leadInSilence - 0.05),
                OutgoingTailSilence = tailSilence,
            };
        }

        private static double toDecibels(double amplitude) => 20 * Math.Log10(Math.Max(amplitude, 1e-9));
    }

    /// <summary>
    /// Analyses audio files with ffmpeg to find silence at their boundaries.
    /// </summary>
    public static class SmartCrossfadeAnalyzer
    {
        private const int sample_rate = 8000;
        private const int bytes_per_sample = sizeof(short);
        private const int windows_per_second = 50;
        private const double analysis_window_seconds = 20;

        /// <summary>
        /// Silence threshold of -45 dBFS.
        /// </summary>
        private const double silence_threshold = 0.005623413251903491;

        private const int max_cache_entries = 256;

        private static readonly ConcurrentDictionary<string, Task<SmartCrossfadeAnalysis>> cache = new ConcurrentDictionary<string, Task<SmartCrossfadeAnalysis>>();

        /// <summary>
        /// Analyses the leading and trailing parts of an audio file.
        /// Analysis is best-effort: if ffmpeg is unavailable the returned value simply contains no analysis.
        /// </summary>
        public static async Task<SmartCrossfadeAnalysis> AnalyseAsync(string audioFilePath, string ffmpegPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(audioFilePath) || string.IsNullOrEmpty(ffmpegPath) || !File.Exists(audioFilePath))
                return SmartCrossfadeAnalysis.Empty;

            string cacheKey;

            try
            {
                var fileInfo = new FileInfo(audioFilePath);
                cacheKey = $"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to inspect audio file for smart crossfade analysis.");
                return SmartCrossfadeAnalysis.Empty;
            }

            if (cache.Count > max_cache_entries)
                cache.Clear();

            Task<SmartCrossfadeAnalysis> analysisTask = cache.GetOrAdd(cacheKey, _ => analyseCoreAsync(audioFilePath, ffmpegPath));

            try
            {
                return await analysisTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return SmartCrossfadeAnalysis.Empty;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Smart crossfade analysis failed.");
                return SmartCrossfadeAnalysis.Empty;
            }
        }

        private static async Task<SmartCrossfadeAnalysis> analyseCoreAsync(string audioFilePath, string ffmpegPath)
        {
            try
            {
                Task<byte[]> headTask = decodePcmAsync(audioFilePath, ffmpegPath, null, analysis_window_seconds);
                Task<byte[]> tailTask = decodePcmAsync(audioFilePath, ffmpegPath, analysis_window_seconds, null);

                await Task.WhenAll(headTask, tailTask).ConfigureAwait(false);

                (double leadInSeconds, double headRms, bool headFound) = analyseHead(await headTask.ConfigureAwait(false));
                (double tailSeconds, double tailRms, bool tailFound) = analyseTail(await tailTask.ConfigureAwait(false));

                return new SmartCrossfadeAnalysis(leadInSeconds, tailSeconds, headRms, tailRms, headFound, tailFound);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to analyse audio for smart crossfade.");
                return SmartCrossfadeAnalysis.Empty;
            }
        }

        private static async Task<byte[]> decodePcmAsync(string audioFilePath, string ffmpegPath, double? fromEndSeconds, double? durationSeconds)
        {
            var startInfo = new ProcessStartInfo(ffmpegPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            startInfo.ArgumentList.Add("-hide_banner");
            startInfo.ArgumentList.Add("-loglevel");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-nostdin");

            if (fromEndSeconds.HasValue)
            {
                startInfo.ArgumentList.Add("-sseof");
                startInfo.ArgumentList.Add((-fromEndSeconds.Value).ToString(CultureInfo.InvariantCulture));
            }

            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(audioFilePath);

            if (durationSeconds.HasValue)
            {
                startInfo.ArgumentList.Add("-t");
                startInfo.ArgumentList.Add(durationSeconds.Value.ToString(CultureInfo.InvariantCulture));
            }

            startInfo.ArgumentList.Add("-map");
            startInfo.ArgumentList.Add("0:a:0");
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(sample_rate.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("s16le");
            startInfo.ArgumentList.Add("pipe:1");

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    return Array.Empty<byte>();

                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    try
                    {
                        Task<byte[]> outputTask = readAllBytesAsync(process.StandardOutput.BaseStream, timeout.Token);
                        Task<string> errorTask = process.StandardError.ReadToEndAsync();

                        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

                        byte[] output = await outputTask.ConfigureAwait(false);
                        await errorTask.ConfigureAwait(false);

                        return process.ExitCode == 0 ? output : Array.Empty<byte>();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "ffmpeg failed while decoding audio for smart crossfade.");

                        try
                        {
                            process.Kill(true);
                        }
                        catch
                        {
                            // ignored
                        }

                        return Array.Empty<byte>();
                    }
                }
            }
        }

        private static async Task<byte[]> readAllBytesAsync(Stream stream, CancellationToken cancellationToken)
        {
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream, 81920, cancellationToken).ConfigureAwait(false);
                return memoryStream.ToArray();
            }
        }

        private static (double leadInSeconds, double averageRms, bool foundAudio) analyseHead(byte[] pcm)
        {
            int windowCount = getWindowCount(pcm);

            if (windowCount == 0)
                return (0, 0, false);

            int firstAudibleWindow = -1;
            double[] windowRms = calculateWindowRms(pcm, windowCount);

            for (int i = 0; i < windowCount; i++)
            {
                if (windowRms[i] > silence_threshold)
                {
                    firstAudibleWindow = i;
                    break;
                }
            }

            if (firstAudibleWindow < 0)
                return (0, 0, false);

            double leadInSeconds = Math.Max(0, (firstAudibleWindow - 1) * getWindowDuration());

            int endWindow = Math.Min(windowCount, firstAudibleWindow + windows_per_second);
            double sum = 0;
            int count = 0;

            for (int i = firstAudibleWindow; i < endWindow; i++)
            {
                sum += windowRms[i];
                count++;
            }

            return (leadInSeconds, count > 0 ? sum / count : 0, true);
        }

        private static (double tailSeconds, double averageRms, bool foundAudio) analyseTail(byte[] pcm)
        {
            int windowCount = getWindowCount(pcm);

            if (windowCount == 0)
                return (0, 0, false);

            int lastAudibleWindow = -1;
            double[] windowRms = calculateWindowRms(pcm, windowCount);

            for (int i = 0; i < windowCount; i++)
            {
                if (windowRms[i] > silence_threshold)
                    lastAudibleWindow = i;
            }

            if (lastAudibleWindow < 0)
                return (0, 0, false);

            double totalDuration = windowCount * getWindowDuration();
            double audibleEnd = (lastAudibleWindow + 1) * getWindowDuration();
            double tailSeconds = Math.Max(0, totalDuration - audibleEnd);

            int startWindow = Math.Max(0, lastAudibleWindow - windows_per_second + 1);
            double sum = 0;
            int count = 0;

            for (int i = startWindow; i <= lastAudibleWindow; i++)
            {
                sum += windowRms[i];
                count++;
            }

            return (tailSeconds, count > 0 ? sum / count : 0, true);
        }

        private static int getWindowCount(byte[] pcm)
        {
            int windowBytes = getWindowSamples() * bytes_per_sample;
            return pcm.Length / windowBytes;
        }

        private static int getWindowSamples() => sample_rate / windows_per_second;

        private static double getWindowDuration() => 1d / windows_per_second;

        private static double[] calculateWindowRms(byte[] pcm, int windowCount)
        {
            int windowSamples = getWindowSamples();
            int windowBytes = windowSamples * bytes_per_sample;
            var result = new double[windowCount];

            for (int window = 0; window < windowCount; window++)
            {
                double sumSquares = 0;
                int offset = window * windowBytes;

                for (int sample = 0; sample < windowSamples; sample++)
                {
                    short value = BitConverter.ToInt16(pcm, offset + sample * bytes_per_sample);
                    double normalised = value / 32768d;
                    sumSquares += normalised * normalised;
                }

                result[window] = Math.Sqrt(sumSquares / windowSamples);
            }

            return result;
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NekoPlayer.App.Config;
using NekoPlayer.App.Extensions;
using NekoPlayer.App.Graphics.Containers;
using NekoPlayer.App.Input.Binding;
using NekoPlayer.App.Localisation;
using NekoPlayer.App.Overlays;
using NekoPlayer.App.Overlays.OSD;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NekoPlayer.App.Graphics
{
    public partial class ScreenshotManager : Component, IKeyBindingHandler<GlobalAction>, IHandleGlobalKeyboardInput
    {
        private readonly BindableBool cursorVisibility = new BindableBool(true);

        /// <summary>
        /// Changed when screenshots are being or have finished being taken, to control whether cursors should be visible.
        /// If cursors should not be visible, cursors have 3 frames to hide themselves.
        /// </summary>
        public IBindable<bool> CursorVisibility => cursorVisibility;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private NekoPlayerConfigManager config { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay onScreenDisplay { get; set; } = null!;

        [Resolved]
        private PushNotificationOverlay pushNotificationOverlay { get; set; } = null!;

        private Storage storage = null!;

        private Sample? shutter;

        [BackgroundDependencyLoader]
        private void load(Storage storage, AudioManager audio)
        {
            this.storage = storage.GetStorageForDirectory(@"screenshots");
            shutter = audio.Samples.Get("UI/shutter");
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.TakeScreenshot:
                    shutter?.Play();
                    TakeScreenshotAsync().FireAndForget();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private volatile int screenShotTasks;

        public Task TakeScreenshotAsync() => Task.Run(async () =>
        {
            Interlocked.Increment(ref screenShotTasks);

            ScreenshotFormat screenshotFormat = config.Get<ScreenshotFormat>(NekoPlayerSetting.ScreenshotFormat);
            bool captureMenuCursor = config.Get<bool>(NekoPlayerSetting.ScreenshotCaptureMenuCursor);

            try
            {
                if (!captureMenuCursor)
                {
                    cursorVisibility.Value = false;
                    Schedule(() => onScreenDisplay.Hide());

                    // We need to wait for at most 3 draw nodes to be drawn, following which we can be assured at least one DrawNode has been generated/drawn with the set value
                    const int frames_to_wait = 3;

                    int framesWaited = 0;

                    using (ManualResetEventSlim framesWaitedEvent = new ManualResetEventSlim(false))
                    {
                        ScheduledDelegate waitDelegate = host.DrawThread.Scheduler.AddDelayed(() =>
                        {
                            if (framesWaited++ >= frames_to_wait)
                                // ReSharper disable once AccessToDisposedClosure
                                framesWaitedEvent.Set();
                        }, 10, true);

                        if (!framesWaitedEvent.Wait(1000))
                            throw new TimeoutException("Screenshot data did not arrive in a timely fashion");

                        waitDelegate.Cancel();
                    }
                }

                using (Image<Rgba32>? image = await host.TakeScreenshotAsync().ConfigureAwait(false))
                {
                    if (config.Get<ScalingMode>(NekoPlayerSetting.Scaling) == ScalingMode.Everything)
                    {
                        float posX = config.Get<float>(NekoPlayerSetting.ScalingPositionX);
                        float posY = config.Get<float>(NekoPlayerSetting.ScalingPositionY);
                        float sizeX = config.Get<float>(NekoPlayerSetting.ScalingSizeX);
                        float sizeY = config.Get<float>(NekoPlayerSetting.ScalingSizeY);

                        image.Mutate(m =>
                        {
                            Rectangle rect = new Rectangle(Point.Empty, m.GetCurrentSize());

                            // Reduce size by user scale settings...
                            int sx = (rect.Width - (int)(rect.Width * sizeX)) / 2;
                            int sy = (rect.Height - (int)(rect.Height * sizeY)) / 2;
                            rect.Inflate(-sx, -sy);

                            // ...then adjust the region based on their positional offset.
                            rect.X = (int)(rect.X * posX) * 2;
                            rect.Y = (int)(rect.Y * posY) * 2;

                            m.Crop(rect);
                        });
                    }

                    clipboard.SetImage(image);

                    (string? filename, Stream? stream) = getWritableStream(screenshotFormat);

                    if (filename == null) return;

                    using (stream)
                    {
                        switch (screenshotFormat)
                        {
                            case ScreenshotFormat.Png:
                                await image.SaveAsPngAsync(stream).ConfigureAwait(false);
                                break;

                            case ScreenshotFormat.Jpg:
                                int jpeg_quality = config.Get<int>(NekoPlayerSetting.ScreenshotQuality); //92

                                await image.SaveAsJpegAsync(stream, new JpegEncoder { Quality = jpeg_quality }).ConfigureAwait(false);
                                break;

                            case ScreenshotFormat.Webp:
                                await image.SaveAsWebpAsync(stream).ConfigureAwait(false);
                                break;

                            default:
                                throw new InvalidOperationException($"Unknown enum member {nameof(ScreenshotFormat)} {screenshotFormat}.");
                        }
                    }

                    /*
                    notificationOverlay.Post(new SimpleNotification
                    {
                        Text = NotificationsStrings.ScreenshotSaved(filename),
                        Activated = () =>
                        {
                            storage.PresentFileExternally(filename);
                            return true;
                        }
                    });
                    */

                    Toast toast = new Toast(NekoPlayerStrings.ScreenshotSaved, filename);

                    onScreenDisplay.Display(toast);

                    storage.PresentFileExternally(filename);
                }
            }
            finally
            {
                if (Interlocked.Decrement(ref screenShotTasks) == 0)
                {
                    cursorVisibility.Value = true;
                    Schedule(() => onScreenDisplay.Show());
                }
            }
        });

        private static readonly object filename_reservation_lock = new object();

        private (string? filename, Stream? stream) getWritableStream(ScreenshotFormat format)
        {
            lock (filename_reservation_lock)
            {
                DateTime dt = DateTime.Now;
                string fileExt = format.ToString().ToLowerInvariant();

                string withoutIndex = $"NekoPlayer_{dt:yyyy-MM-dd_HH-mm-ss}.{fileExt}";
                if (!storage.Exists(withoutIndex))
                    return (withoutIndex, storage.GetStream(withoutIndex, FileAccess.Write, FileMode.Create));

                for (ulong i = 1; i < ulong.MaxValue; i++)
                {
                    string indexedName = $"NekoPlayer_{dt:yyyy-MM-dd_HH-mm-ss}-{i}.{fileExt}";
                    if (!storage.Exists(indexedName))
                        return (indexedName, storage.GetStream(indexedName, FileAccess.Write, FileMode.Create));
                }

                return (null, null);
            }
        }
    }
}

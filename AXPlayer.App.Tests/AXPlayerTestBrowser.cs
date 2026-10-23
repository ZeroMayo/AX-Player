// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Framework.Threading;
using AXPlayer.App.Audio.Effects;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics;
using AXPlayer.App.Localisation;
using AXPlayer.App.Overlays;
using static AXPlayer.App.AXPlayerApp;

namespace AXPlayer.App.Tests
{
    public partial class AXPlayerTestBrowser : AXPlayerAppBase
    {
        private OnScreenDisplay onScreenDisplay;

        private ScreenshotManager screenshotManager;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var languages = Enum.GetValues<Language>();

            var mappings = languages.Select(language =>
            {
                string cultureCode = language.ToCultureCode();

                try
                {
                    return new LocaleMapping(new ResourceManagerLocalisationStore(cultureCode));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Could not load localisations for language \"{cultureCode}\"");
                    return null;
                }
            }).Where(m => m != null);

            Localisation.AddLocaleMappings(mappings);

            onScreenDisplay = new OnScreenDisplay();
            screenshotManager = new ScreenshotManager();
            notificationOverlay = new PushNotificationOverlay();

            onScreenDisplay.BeginTracking(this, frameworkConfig);
            onScreenDisplay.BeginTracking(this, LocalConfig);

            loadComponentSingleFile(onScreenDisplay, Add, true);

            loadComponentSingleFile(notificationOverlay, Add, true);

            loadComponentSingleFile(screenshotManager, Add, true);

            Add(audioDuckFilter = new AudioFilter(Audio.TrackMixer));
            Audio.Tracks.AddAdjustment(AdjustableProperty.Volume, audioDuckVolume);
            sampleVolume = Audio.VolumeSample.GetBoundCopy();

            AddRange(new Drawable[]
            {
                new TestBrowser("AXPlayer"),
            });
        }

        private PushNotificationOverlay notificationOverlay;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; }

        private BindableNumber<double> sampleVolume = null!;
        private readonly List<DuckParameters> duckOperations = new List<DuckParameters>();
        private readonly BindableDouble audioDuckVolume = new BindableDouble(1);
        private AudioFilter audioDuckFilter = null!;

        private Task asyncLoadStream;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        /// <summary>
        /// Queues loading the provided component in sequential fashion.
        /// This operation is limited to a single thread to avoid saturating all cores.
        /// </summary>
        /// <param name="component">The component to load.</param>
        /// <param name="loadCompleteAction">An action to invoke on load completion (generally to add the component to the hierarchy).</param>
        /// <param name="cache">Whether to cache the component as type <typeparamref name="T"/> into the game dependencies before any scheduling.</param>
        private T loadComponentSingleFile<T>(T component, Action<Drawable> loadCompleteAction, bool cache = false)
            where T : class
        {
            if (cache)
                dependencies.CacheAs(component);

            var drawableComponent = component as Drawable ?? throw new ArgumentException($"Component must be a {nameof(Drawable)}", nameof(component));

            // schedule is here to ensure that all component loads are done after LoadComplete is run (and thus all dependencies are cached).
            // with some better organisation of LoadComplete to do construction and dependency caching in one step, followed by calls to loadComponentSingleFile,
            // we could avoid the need for scheduling altogether.
            Schedule(() =>
            {
                var previousLoadStream = asyncLoadStream;

                // chain with existing load stream
                asyncLoadStream = Task.Run(async () =>
                {
                    if (previousLoadStream != null)
                        await previousLoadStream.ConfigureAwait(false);

                    try
                    {
                        Logger.Log($"Loading {component}...");

                        // Since this is running in a separate thread, it is possible for OsuGame to be disposed after LoadComponentAsync has been called
                        // throwing an exception. To avoid this, the call is scheduled on the update thread, which does not run if IsDisposed = true
                        Task task = null;
                        var del = new ScheduledDelegate(() => task = LoadComponentAsync(drawableComponent, loadCompleteAction));
                        Scheduler.Add(del);

                        // The delegate won't complete if OsuGame has been disposed in the meantime
                        while (!IsDisposed && !del.Completed)
                            await Task.Delay(10).ConfigureAwait(false);

                        // Either we're disposed or the load process has started successfully
                        if (IsDisposed)
                            return;

                        Debug.Assert(task != null);

                        await task.ConfigureAwait(false);

                        Logger.Log($"Loaded {component}!");
                    }
                    catch (OperationCanceledException)
                    {
                    }
                });
            });

            return component;
        }

        private DependencyContainer dependencies;

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            host.Window.CursorState |= CursorState.Hidden;
        }
    }
}

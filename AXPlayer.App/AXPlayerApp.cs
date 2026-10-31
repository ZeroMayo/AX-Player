// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AXPlayer.App.Audio.Effects;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Input.Binding;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Overlays;
using AXPlayer.App.Screens;
using AXPlayer.App.Updater;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osuTK;

namespace AXPlayer.App
{
    [Cached(typeof(AXPlayerApp))]
    public partial class AXPlayerApp : AXPlayerAppBase
    {
        private ScreenStack screenStack;

        public static FontUsage DefaultFont = FontUsage.Default.With("GoogleSansFlex", 16, "Regular");

        public new partial class Fonts
        {
            public static FontUsage GoogleSansFlex = FontUsage.Default.With("GoogleSansFlex", 16, "Regular");
            public static FontUsage Rubik = FontUsage.Default.With("Rubik", 16, "Regular");
            public static FontUsage Pretendard = FontUsage.Default.With("PretendardVariable", 16, "Regular");
            public static FontUsage Hungeul = FontUsage.Default.With("Hungeul", 16);
            public static FontUsage Ownglyph_PDH = FontUsage.Default.With("Ownglyph_PDH", 16);
            public static FontUsage Dovemayo_Gothic = FontUsage.Default.With("Dovemayo_Gothic", 16);
            public static FontUsage Griun_Mongtori = FontUsage.Default.With("Griun_Mongtori", 16);
            public static FontUsage ONE_Mobile_POP = FontUsage.Default.With("ONE_Mobile_POP", 16);
            public static FontUsage HayuFont = FontUsage.Default.With("HayuFont", 16);
            public static FontUsage Cafe24Syongsyong = FontUsage.Default.With("Cafe24Syongsyong", 16);
            public static FontUsage DreamHeumulKR = FontUsage.Default.With("DreamHeumulKR", 16);
            public static FontUsage Hakgyoansim_ManitoR = FontUsage.Default.With("Hakgyoansim_ManitoR", 16);
            public static FontUsage Roboto = FontUsage.Default.With("Roboto", 16, "Regular");
            public static FontUsage OwnglyphDaisy = FontUsage.Default.With("OwnglyphDaisy", 16);
            public static FontUsage OwnglyphYuntaeng = FontUsage.Default.With("OwnglyphYuntaeng", 16);
            public static FontUsage GoogleSansFlexRounded = FontUsage.Default.With("GoogleSansFlexRounded", 16, "Regular");
            public static FontUsage Hippie = FontUsage.Default.With("Hippie", 16);
        }

        private BindableNumber<double> sampleVolume = null!;
        private FPSCounter fpsCounter;
        private Container topMostOverlayContent, overlayContainer, leftFloatingOverlayContent;

        public const float UI_CORNER_RADIUS = 18f;

        private OnScreenDisplay onScreenDisplay;
        private PushNotificationOverlay notificationOverlay;
        private ScreenshotManager screenshotManager;

        private Online.DiscordRPCClient discord_rpc;

        private VolumeOverlay volume;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; }

        private Bindable<float> uiScale;

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults()
        {
            return new Dictionary<FrameworkSetting, object>
            {
                { FrameworkSetting.AudioUseExperimentalWasapi, true },
            };
        }

        public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            const float adjustment_increment = 0.05f;

            switch (e.Action)
            {
                case PlatformAction.ZoomIn:
                    uiScale.Value += adjustment_increment;
                    return true;

                case PlatformAction.ZoomOut:
                    uiScale.Value -= adjustment_increment;
                    return true;

                case PlatformAction.ZoomDefault:
                    uiScale.SetDefault();
                    return true;
            }

            return base.OnPressed(e);
        }

        public UpdateManager UpdateManager;
        public MediaSession MediaSession;

        private RestartDialogOverlay restartDialogOverlay;
        private PopupDialogOverlay popupDialogOverlay;
        private NewToastOverlay newToastOverlay;
        private LoadingOverlay loadingOverlay;

        private Bindable<CloseButtonAction> closeButtonAction;

        private OnDemandBackbufferProvider onDemandBackbufferProvider;

        [BackgroundDependencyLoader]
        private void load()
        {
            replaceDefaultFont();
            var languages = Enum.GetValues<Language>();

            var mappings = languages.Select(language =>
            {
#if DEBUG
                if (language == Language.debug)
                    return new LocaleMapping("debug", new DebugLocalisationStore());
#endif

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

            uiScale = LocalConfig.GetBindable<float>(Config.AXPlayerSetting.UIScale);
            closeButtonAction = LocalConfig.GetBindable<CloseButtonAction>(AXPlayerSetting.CloseButtonAction);

            Localisation.AddLocaleMappings(mappings);

            loadComponentSingleFile(MediaSession = CreateMediaSession(), Add, true);
            // dependency on notification overlay, dependent by settings overlay
            loadComponentSingleFile(UpdateManager = CreateUpdateManager(), Add, true);

            if (UpdateManager is NoActionUpdateManager)
            {
                UpdateManagerVersionText.Value = AXPlayerStrings.ViewLatestVersions;
            }

            // Add your top-level game components here.
            // A screen stack and sample screen has been provided for convenience, but you can replace it if you don't want to use screens.
            AddRange(new Drawable[]
            {
                onDemandBackbufferProvider = new OnDemandBackbufferProvider
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = screenStack = new ScreenStack
                    {
                        RelativeSizeAxes = Axes.Both
                    },
                },
                overlayContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both
                },
                leftFloatingOverlayContent = new Container { RelativeSizeAxes = Axes.Both },
                topMostOverlayContent = new Container { RelativeSizeAxes = Axes.Both },
            });

            dependencies.CacheAs(onDemandBackbufferProvider);

            loadComponentSingleFile(volume = new VolumeOverlay(), leftFloatingOverlayContent.Add, true);

            onScreenDisplay = new OnScreenDisplay();
            loadingOverlay = new LoadingOverlay();
            screenshotManager = new ScreenshotManager();
            notificationOverlay = new PushNotificationOverlay();
            restartDialogOverlay = new RestartDialogOverlay();
            popupDialogOverlay = new PopupDialogOverlay();
            newToastOverlay = new NewToastOverlay();

            content.ScreenshotManager = screenshotManager;

            onScreenDisplay.BeginTracking(this, frameworkConfig);
            onScreenDisplay.BeginTracking(this, LocalConfig);
            onScreenDisplay.BeginTracking(this, AudioEffectsConfig);

            loadComponentSingleFile(loadingOverlay, overlayContainer.Add, true);
            loadComponentSingleFile(onScreenDisplay, topMostOverlayContent.Add, true);
            loadComponentSingleFile(notificationOverlay, overlayContainer.Add, true);
            loadComponentSingleFile(popupDialogOverlay, overlayContainer.Add, true);
            loadComponentSingleFile(restartDialogOverlay, topMostOverlayContent.Add, true);
            loadComponentSingleFile(newToastOverlay, topMostOverlayContent.Add, true);

            loadComponentSingleFile(screenshotManager, Add, true);

            if (RuntimeInfo.IsDesktop)
                loadComponentSingleFile(discord_rpc = new Online.DiscordRPCClient(), Add, true);

            loadComponentSingleFile(fpsCounter = new FPSCounter
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Margin = new MarginPadding(5),
            }, topMostOverlayContent.Add);

            applySafeAreaConsiderations = LocalConfig.GetBindable<bool>(AXPlayerSetting.SafeAreaConsiderations);
            applySafeAreaConsiderations.BindValueChanged(apply => SafeAreaContainer.SafeAreaOverrideEdges = apply.NewValue ? SafeAreaOverrideEdges : Edges.All, true);

            applyConfigMigrations();

            string lastVersion = LocalConfig.Get<string>(AXPlayerSetting.Version);
            string version = Version;

            // finally, update the version stored to the configuration.
            // this MUST happen after `applyConfigMigrations()` call, as it relies on comparing the previous version.
            // debug / local compilations will reset to a non-release string.
            LocalConfig.SetValue(AXPlayerSetting.Version, version);
        }

        /// <summary>
        /// Apply any migrations to configuration.
        /// </summary>
        private void applyConfigMigrations()
        {
            // arrives as 2026.1018.0
            string rawVersion = LocalConfig.Get<string>(AXPlayerSetting.Version);

            if (rawVersion.Length < 6)
                return;

            string[] pieces = rawVersion.Split('.');

            // on a fresh install or when coming from a non-release build, execution will end here.
            // we don't want to run migrations in such cases.
            if (!int.TryParse(pieces[0], out int year)) return;
            if (!int.TryParse(pieces[1], out int monthDay)) return;

            int combined = (year * 10000) + monthDay;

            if (combined < 20261018 && RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                Audio.UseExperimentalWasapi.Value = true;
            }
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.Notifications:
                    if (notificationOverlay.IsOpened.Value)
                        notificationOverlay.HideOverlay();
                    else
                        notificationOverlay.OpenOverlay();

                    return true;
            }

            return false;
        }

        private void replaceDefaultFont()
        {
            string fontName = string.Empty;

            switch (LocalConfig.Get<UIFont>(AXPlayerSetting.UIFont))
            {
                case UIFont.GoogleSansFlex:
                {
                    fontName = @"GoogleSansFlex";
                    break;
                }
                case UIFont.Rubik:
                {
                    fontName = @"Rubik";
                    break;
                }
                case UIFont.Pretendard:
                {
                    fontName = @"PretendardVariable";
                    break;
                }
                case UIFont.Roboto:
                {
                    fontName = @"Roboto";
                    break;
                }
                case UIFont.GoogleSansFlexRounded:
                {
                    fontName = @"GoogleSansFlexRounded";
                    break;
                }
            }

            DefaultFont = FontUsage.Default.With(fontName, 16, "Regular");
        }

        public List<YouTubeI18nLangItem> AvailableCaptionLanguages;
        public Bindable<YouTubeI18nLangItem> CurrentCaptionLanguage;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            discord_rpc?.Dispose();
        }

        private Bindable<bool> applySafeAreaConsiderations = null!;

        /// <summary>
        /// Adjust the globally applied <see cref="DrawSizePreservingFillContainer.TargetDrawSize"/> in every <see cref="ScalingContainerNew"/>.
        /// Useful for changing how the game handles different aspect ratios.
        /// </summary>
        public virtual Vector2 ScalingContainerTargetDrawSize { get; } = new Vector2(1024, 768);

        protected override Container CreateScalingContainer() => new ScalingContainerNew(ScalingMode.Everything);

        private Task asyncLoadStream;

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

        protected virtual UpdateManager CreateUpdateManager() => new UpdateManager();

        /// <summary>
        /// If supported by the platform, the media session creation will be handled by the app, allowing for features such as scrobbling and integration with external media controls.
        /// </summary>
        public virtual MediaSession CreateMediaSession() => new MediaSession();

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(audioDuckFilter = new AudioFilter(Audio.TrackMixer));
            Audio.Tracks.AddAdjustment(AdjustableProperty.Volume, audioDuckVolume);
            sampleVolume = Audio.VolumeSample.GetBoundCopy();

            screenStack.Push(new Loader());
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            GlobalCursorDisplay.ShowCursor = (UseSystemCursor.Value == false) ? ((screenStack.CurrentScreen as IAXPlayerScreen)?.CursorVisible ?? false) : false;
        }

        protected override bool OnExiting()
        {
            if (LoadFailed)
                return base.OnExiting();

            if (closeButtonAction.Value == CloseButtonAction.HideToTrayIcon && !QuitRequested)
            {
                RequestHideToTrayIcon();
                return true;
            }

            Logger.Log("Exiting...", LoggingTarget.Runtime, LogLevel.Debug);

            Schedule(() => AttemptExit());
            return !isExiting;
        }

        public virtual void RequestHideToTrayIcon()
        {
        }

        public bool QuitRequested;

        public override void AttemptExit(bool forceQuit = false)
        {
            if (forceQuit)
                Exit();

            ISample exitSound = Audio.Samples.Get(@"overlay-pop-out");
            DrawableSample drawableSample = new DrawableSample(exitSound);

            Content.Add(drawableSample);

            Bindable<double> fadeVolume = new Bindable<double>(1);

            drawableSample.Play();

            this.FadeOut(500, Easing.InQuart).OnComplete(_ => Exit());
            this.TransformBindableTo(fadeVolume, 0, 500, Easing.InQuart);
            Audio.Tracks.AddAdjustment(AdjustableProperty.Volume, fadeVolume);

            isExiting = true;
        }

        private bool isExiting = false;

        private readonly List<DuckParameters> duckOperations = new List<DuckParameters>();
        private readonly BindableDouble audioDuckVolume = new BindableDouble(1);
        private AudioFilter audioDuckFilter = null!;

#nullable enable
            /// <summary>
            /// Applies ducking, attenuating the volume and/or low-pass cutoff of the currently playing track to make headroom for effects (or just to apply an effect).
            /// </summary>
            /// <returns>A <see cref="IDisposable"/> which will restore the duck operation when disposed.</returns>
        public IDisposable Duck(DuckParameters? parameters = null)
        {
            // Don't duck if samples have no volume, it sounds weird.
            if (sampleVolume.Value == 0)
                return new InvokeOnDisposal(() => { });

            parameters ??= new DuckParameters();

            duckOperations.Add(parameters);

            DuckParameters volumeOperation = duckOperations.MinBy(p => p.DuckVolumeTo)!;
            DuckParameters lowPassOperation = duckOperations.MinBy(p => p.DuckCutoffTo)!;

            audioDuckFilter.CutoffTo(lowPassOperation.DuckCutoffTo, lowPassOperation.DuckDuration, lowPassOperation.DuckEasing);
            this.TransformBindableTo(audioDuckVolume, volumeOperation.DuckVolumeTo, volumeOperation.DuckDuration, volumeOperation.DuckEasing);

            return new InvokeOnDisposal(restoreDucking);

            void restoreDucking() => Schedule(() =>
            {
                if (!duckOperations.Remove(parameters))
                    return;

                DuckParameters? restoreVolumeOperation = duckOperations.MinBy(p => p.DuckVolumeTo);
                DuckParameters? restoreLowPassOperation = duckOperations.MinBy(p => p.DuckCutoffTo);

                // If another duck operation is in the list, restore ducking to its level, else reset back to defaults.
                audioDuckFilter.CutoffTo(restoreLowPassOperation?.DuckCutoffTo ?? AudioFilter.MAX_LOWPASS_CUTOFF, parameters.RestoreDuration, parameters.RestoreEasing);
                this.TransformBindableTo(audioDuckVolume, restoreVolumeOperation?.DuckVolumeTo ?? 1, parameters.RestoreDuration, parameters.RestoreEasing);
            });
        }
#nullable disable

        public class DuckParameters
        {
            /// <summary>
            /// The duration of the ducking transition in milliseconds.
            /// Defaults to 100 ms.
            /// </summary>
            public double DuckDuration = 100;

            /// <summary>
            /// The final volume which should be reached during ducking, when 0 is silent and 1 is original volume.
            /// Defaults to 25%.
            /// </summary>
            public double DuckVolumeTo = 0.25;

            /// <summary>
            /// The low-pass cutoff frequency which should be reached during ducking. If not required, set to <see cref="AudioFilter.MAX_LOWPASS_CUTOFF"/>.
            /// Defaults to 300 Hz.
            /// </summary>
            public int DuckCutoffTo = 300;

            /// <summary>
            /// The easing curve to be applied during ducking.
            /// Defaults to <see cref="Easing.Out"/>.
            /// </summary>
            public Easing DuckEasing = Easing.Out;

            /// <summary>
            /// The duration of the restoration transition in milliseconds.
            /// Defaults to 500 ms.
            /// </summary>
            public double RestoreDuration = 500;

            /// <summary>
            /// The easing curve to be applied during restoration.
            /// Defaults to <see cref="Easing.In"/>.
            /// </summary>
            public Easing RestoreEasing = Easing.In;
        }
    }
}

// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using ManagedBass.Fx;
using NekoPlayer.App.Audio;
using NekoPlayer.App.Config;
using NekoPlayer.App.Extensions;
using NekoPlayer.App.Graphics;
using NekoPlayer.App.Graphics.Cursor;
using NekoPlayer.App.Graphics.Sprites;
using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Input;
using NekoPlayer.App.Input.Binding;
using NekoPlayer.App.Localisation;
using NekoPlayer.App.Online;
using NekoPlayer.App.Resources;
using NekoPlayer.App.Utils;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK.Graphics;
using YoutubeExplode;

namespace NekoPlayer.App
{
    [Cached(typeof(NekoPlayerAppBase))]
    public partial class NekoPlayerAppBase : Game
    {
        // Anything in this class is shared between the test browser and the game implementation.
        // It allows for caching global dependencies that should be accessible to tests, or changing
        // the screen scaling for all components including the test browser and framework overlays.

        protected override Container<Drawable> Content => content;

        private Container content;

        protected bool LoadFailed { get; set; }

        public YoutubeClient YouTubeClient { get; set; }

        protected YouTubeAPI YouTubeService { get; set; }

        protected GoogleOAuth2 GoogleOAuth2 { get; set; }

        protected GoogleTranslate TranslateAPI { get; set; }

        /// <summary>
        /// The language in which the app is currently displayed in.
        /// </summary>
        public Bindable<Language> CurrentLanguage { get; } = new Bindable<Language>();

        private Bindable<string> frameworkLocale = null!;

        private IBindable<LocalisationParameters> localisationParameters = null!;

        protected NekoPlayerConfigManager LocalConfig { get; private set; }
        protected AudioEffectsConfigManager AudioEffectsConfig { get; private set; }

        protected GlobalCursorDisplay GlobalCursorDisplay { get; private set; }

        public Bindable<LocalisableString> UpdateManagerVersionText = new Bindable<LocalisableString>();
        public Bindable<bool> RestartRequired = new Bindable<bool>();
        public Bindable<double> CurrentTrackNormalizeVolume = new Bindable<double>(1);

        protected AudioNormalizationManager AudioNormalizationManager { get; private set; }

        protected NekoPlayerAppBase()
        {
            Name = "NekoPlayer";
        }

        protected Storage Storage { get; set; }

        private int allowableExceptions;

        public string GetFFmpegPath()
        {
            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                {
                    return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/FFmpeg/bin/win-x64/ffmpeg.exe";
                }
                case RuntimeInfo.Platform.Linux:
                {
                    return "ffmpeg";
                }
                default:
                {
                    throw new PlatformNotSupportedException();
                }
            }
        }

        public string GetYtDlpPath()
        {
            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                {
                    return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/yt-dlp.exe";
                }
                case RuntimeInfo.Platform.Linux:
                {
                    return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/yt-dlp_linux";
                }
                default:
                {
                    throw new PlatformNotSupportedException();
                }
            }
        }

        /// <summary>
        /// Allows a maximum of one unhandled exception, per second of execution.
        /// </summary>
        /// <returns>Whether to ignore the exception and continue running.</returns>
        private bool onExceptionThrown(Exception ex)
        {
            if (Interlocked.Decrement(ref allowableExceptions) < 0)
            {
                Logger.Log("oops nekoplayer crashed");
                Logger.Log("i lost my hope but i had an logs");
                //Logger.Log("Too many unhandled exceptions, crashing out.");
                return false;
            }

            Logger.Log($"Unhandled exception has been allowed with {allowableExceptions} more allowable exceptions.");
            // restore the stock of allowable exceptions after a short delay.
            Task.Delay(1000).ContinueWith(_ => Interlocked.Increment(ref allowableExceptions));

            return true;
        }

        public Bindable<bool> UseSystemCursor = null!;

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            // may be non-null for certain tests
            Storage ??= host.Storage;

            LocalConfig ??= new NekoPlayerConfigManager(Storage);

            UseSystemCursor = LocalConfig.GetBindable<bool>(NekoPlayerSetting.UseSystemCursor);

            UseSystemCursor.BindValueChanged(enabled =>
            {
                SetCursorVisibility(enabled.NewValue);
            }, true);

            foreach (var handler in host.AvailableInputHandlers)
            {
                if (handler is MouseHandler)
                {
                    (handler as MouseHandler).UseRelativeMode.Value = false; //we don't use raw mouse movement
                }
            }

            host.ExceptionThrown += onExceptionThrown;
        }

        public void SetCursorVisibility(bool visible)
        {
            if (Host.Window != null)
            {
                if (visible)
                {
                    Host.Window.CursorState = CursorState.Default;
                }
                else
                {
                    Host.Window.CursorState |= CursorState.Hidden;
                }
            }
        }

        public string ParseVideoQuality()
        {
            VideoQuality videoQuality = LocalConfig.Get<VideoQuality>(NekoPlayerSetting.VideoQuality);

            switch (videoQuality)
            {
                case VideoQuality.Quality_8K:
                    return "4320p";
                case VideoQuality.Quality_4K:
                    return "2160p";
                case VideoQuality.Quality_1440p:
                    return "1440p";
                case VideoQuality.Quality_1080p:
                    return "1080p";
                case VideoQuality.Quality_720p:
                    return "720p";
                case VideoQuality.Quality_480p:
                    return "480p";
                case VideoQuality.Quality_360p:
                    return "360p";
                case VideoQuality.Quality_240p:
                    return "240p";
                case VideoQuality.Quality_144p:
                    return "144p";
            }

            return string.Empty;
        }

        /// <summary>
        /// If supported by the platform, the game will automatically restart after the next exit.
        /// </summary>
        /// <returns>Whether a restart operation was queued.</returns>
        public virtual bool RestartAppWhenExited() => false;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            sentry.Dispose();

            AudioEffectsConfig?.Dispose();
            LocalConfig?.Dispose();

            if (Host != null)
                Host.ExceptionThrown -= onExceptionThrown;
        }

        public static void RecursiveDelete(DirectoryInfo baseDir)
        {
            if (!baseDir.Exists)
                return;

            foreach (var dir in baseDir.EnumerateDirectories())
            {
                RecursiveDelete(dir);
            }
            baseDir.Delete(true);
        }

        protected SessionStatics SessionStatics { get; private set; }

        public virtual Version AssemblyVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version();

        /// <summary>
        /// MD5 representation of the game executable.
        /// </summary>
        public string VersionHash { get; private set; }

        public bool IsDeployedBuild => AssemblyVersion.Major > 0;

        public virtual string Version
        {
            get
            {
                if (!IsDeployedBuild)
                    return @"local " + (DebugUtils.IsDebugBuild ? @"debug" : @"release");

                string informationalVersion = Assembly.GetEntryAssembly()?
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                if (!string.IsNullOrEmpty(informationalVersion))
                    return informationalVersion.Split('+').First();

                Version version = AssemblyVersion;
                return $@"{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public Action RestartAction;
        public Bindable<bool> UpdateButtonEnabled = new Bindable<bool>();

        private OverlayColourProvider overlayColourProvider;
        private ProjectYomiColour colours = null!;

        private IdleTracker idleTracker;

        /// <summary>
        /// Whether the user is currently in an idle state.
        /// </summary>
        public IBindable<bool> IsIdle => idleTracker.IsIdle;

        private OverlayColourScheme colourScheme;
        private SentryClient sentry { get; set; }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
            try
            {
                Logger.Log($"------------------------------------------------\nNekoPlayer by BoomboxRapsody\n------------------------------------------------\nApp version is: {Version}\nApp version hash is: {VersionHash}\nCultureInfo.CurrentCulture name is {CultureInfo.CurrentCulture.Name}\n------------------------------------------------\ngood luck ^^\n------------------------------------------------");
                RestartRequired.Value = false;
                UpdateManagerVersionText.Value = Version;
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                //Logger.Log(Host.CacheStorage.GetStorageForDirectory("videos").GetFullPath("videoId") + @"\video.mp4");
                Resources.AddStore(new DllResourceStore(typeof(NekoPlayerAppResources).Assembly));
                //Resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(NekoPlayerAppBase).Assembly), "BuiltInResources"));

                InitialiseFonts();

                // For some atlases, its recommended to use LargeTextureStore. e.g: mipmapping, incorrect positioning due to the atlas scale adjust, etc
                IResourceStore<TextureUpload> texUpload = Host.CreateTextureLoaderStore(Resources);
                LargeTextureStore largeTs = new(Host.Renderer, texUpload);
                largeTs.AddTextureSource(texUpload);
                dependencies.CacheAs(largeTs);

                frameworkLocale = frameworkConfig.GetBindable<string>(FrameworkSetting.Locale);
                frameworkLocale.BindValueChanged(_ => updateLanguage());

                localisationParameters = Localisation.CurrentParameters.GetBoundCopy();
                localisationParameters.BindValueChanged(_ => updateLanguage(), true);

                CurrentLanguage.BindValueChanged(val => frameworkLocale.Value = val.NewValue.ToCultureCode());

                RequestUpdateWindowTitle(string.Empty);

                dependencies.Cache(LocalConfig);

                dependencies.Cache(GoogleOAuth2 = new GoogleOAuth2(LocalConfig, !IsDeployedBuild));

                dependencies.Cache(AudioNormalizationManager = new AudioNormalizationManager(this, LocalConfig));

                dependencies.Cache(TranslateAPI = new GoogleTranslate(this, frameworkConfig));
                dependencies.Cache(YouTubeService = new YouTubeAPI(this, frameworkConfig, TranslateAPI, LocalConfig, GoogleOAuth2, !IsDeployedBuild, new HttpClient()));
                dependencies.Cache(YouTubeClient = new YoutubeClient());

                dependencies.Cache(sentry = new SentryClient(this, GoogleOAuth2, YouTubeService, Storage));

                dependencies.Cache(AudioEffectsConfig = new AudioEffectsConfigManager(Storage));
                dependencies.Cache(SessionStatics = new SessionStatics());

                colourScheme = LocalConfig.Get<OverlayColourScheme>(NekoPlayerSetting.ColourScheme);

                GlobalActionContainer globalBindings;

                ProjectYomiMenuSamples menuSamples;
                dependencies.Cache(menuSamples = new ProjectYomiMenuSamples());
                base.Content.Add(menuSamples);

                dependencies.CacheAs(idleTracker = new AppIdleTracker(6000));

                dependencies.CacheAs(colours = new ProjectYomiColour());

                dependencies.CacheAs(overlayColourProvider = new OverlayColourProvider(colourScheme));

                Logger.Log($"🎨 OverlayColourProvider loaded");

                /*
                // Ensure game and tests scale with window size and screen DPI.
                base.Content.Add(
                    new ScalingContainerNew(ScalingMode.Everything)
                    {
                        Child = globalBindings = new GlobalActionContainer(this)
                        {
                            Children = new Drawable[]
                            {
                                (GlobalCursorDisplay = new GlobalCursorDisplay
                                {
                                    RelativeSizeAxes = Axes.Both
                                }).WithChild(content = new AdaptiveTooltipContainer(GlobalCursorDisplay.MenuCursor)
                                {
                                    RelativeSizeAxes = Axes.Both
                                }),
                            }
                        }
                });
                */

                base.Content.Add(SafeAreaContainer = new SafeAreaContainer
                {
                    SafeAreaOverrideEdges = SafeAreaOverrideEdges,
                    RelativeSizeAxes = Axes.Both,
                    Child = CreateScalingContainer().WithChild(globalBindings = new GlobalActionContainer(this)
                    {
                        Children = new Drawable[]
                        {
                            (GlobalCursorDisplay = new GlobalCursorDisplay
                            {
                                RelativeSizeAxes = Axes.Both
                            }).WithChild(content = new AdaptiveTooltipContainer(GlobalCursorDisplay.MenuCursor)
                            {
                                RelativeSizeAxes = Axes.Both
                            }),
                        }
                    })
                });

                Logger.Log($"Scaling container loaded");

                trackAudioEffects();

                Audio.DeviceInitialisedAction = () =>
                {
                    trackAudioEffects();
                };
            }
            catch (Exception ex)
            {
                LoadFailed = true;
                Logger.Error(ex, "Failed to initialize app!");

                base.Content.Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Children = new Drawable[]
                    {
                        new ProjectYomiSpriteText
                        {
                            Text = "Failed to initialize app!",
                            Font = FontUsage.Default.With("Roboto", 32, "Regular"),
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre
                        },
                        new ProjectYomiSpriteText
                        {
                            Text = $"{ex.GetType().Name}: {ex.Message}",
                            Font = FontUsage.Default.With("Roboto", 16, "Regular"),
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Colour = Color4.Red,
                        }
                    }
                };
            }
        }

        public Bindable<double> MuteBindable = new Bindable<double>(0);

        #region Audio Effects
        private Bindable<bool> enableReverb = null!;
        private Bindable<bool> rotateEnabled = null!;
        private Bindable<bool> echoEnabled = null!;
        private Bindable<bool> distortionEnabled = null!;
        private Bindable<bool> chorusEnabled = null!;

        private Bindable<float> reverbWetMix = null!;
        private Bindable<float> reverbRoomSize = null!;
        private Bindable<float> reverbDamp = null!;
        private Bindable<float> reverbStereoWidth = null!;

        private Bindable<float> echoDryMix = null!;
        private Bindable<float> echoWetMix = null!;
        private Bindable<float> echoFeedback = null!;
        private Bindable<float> echoDelay = null!;

        private Bindable<float> rotateRate = null!;

        private Bindable<float> distortionVolume = null!;
        private Bindable<float> distortionDrive = null!;

        private Bindable<bool> karaokeModeEnabled = null!;

        private Bindable<float> chorusDryMix = null!;
        private Bindable<float> chorusWetMix = null!;
        private Bindable<float> chorusFeedback = null!;
        private Bindable<float> chorusMinSweep = null!;
        private Bindable<float> chorusMaxSweep = null!;
        private Bindable<float> chorusRate = null!;

        private Bindable<bool> eightBitEffectEnabled = null!;
        private Bindable<float> eightBitEffectBitDepth = null!;
        private Bindable<float> eightBitEffectDownsampleRatio = null!;

        private ReverbParameters reverbParameters = new ReverbParameters();
        private RotateParameters rotateParameters = new RotateParameters();
        private EchoParameters echoParameters = new EchoParameters();
        private DistortionParameters distortionParameters = new DistortionParameters();
        private ChorusParameters chorusParameters = new ChorusParameters();
        private DSPProcedure _karaokeDsp;
        private DSPProcedure _eightBitDsp;

        private void trackAudioEffects()
        {
            #region Init Step
            enableReverb = null;
            rotateEnabled = null;
            echoEnabled = null;
            distortionEnabled = null;
            chorusEnabled = null;
            reverbWetMix = null;
            reverbRoomSize = null;
            reverbDamp = null;
            reverbStereoWidth = null;
            echoDryMix = null;
            echoWetMix = null;
            echoFeedback = null;
            echoDelay = null;
            rotateRate = null;
            distortionVolume = null;
            distortionDrive = null;
            karaokeModeEnabled = null;
            chorusDryMix = null;
            chorusWetMix = null;
            chorusFeedback = null;
            chorusMinSweep = null;
            chorusMaxSweep = null;
            chorusRate = null;
            eightBitEffectEnabled = null;
            eightBitEffectBitDepth = null;
            eightBitEffectDownsampleRatio = null;
            reverbParameters = new ReverbParameters();
            rotateParameters = new RotateParameters();
            echoParameters = new EchoParameters();
            distortionParameters = new DistortionParameters();
            chorusParameters = new ChorusParameters();
            _karaokeDsp = null;
            _eightBitDsp = null;

            Logger.Log("Tracking audio effects");
            #endregion

            #region Reverb
            enableReverb = AudioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ReverbEnabled);

            if (enableReverb.Value)
                Audio.TrackMixer.AddEffect(reverbParameters);
            else
                Audio.TrackMixer.RemoveEffect(reverbParameters);

            enableReverb.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Audio.TrackMixer.AddEffect(reverbParameters);
                else
                    Audio.TrackMixer.RemoveEffect(reverbParameters);
            }, true);

            reverbWetMix = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbWetMix);

            reverbParameters.fWetMix = reverbWetMix.Value;

            if (enableReverb.Value)
                Audio.TrackMixer.UpdateEffect(reverbParameters);

            reverbWetMix.BindValueChanged(value =>
            {
                reverbParameters.fWetMix = value.NewValue;

                if (enableReverb.Value)
                    Audio.TrackMixer.UpdateEffect(reverbParameters);
            }, true);

            reverbRoomSize = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbRoomSize);

            reverbParameters.fRoomSize = reverbRoomSize.Value;

            if (enableReverb.Value)
                Audio.TrackMixer.UpdateEffect(reverbParameters);

            reverbRoomSize.BindValueChanged(value =>
            {
                reverbParameters.fRoomSize = value.NewValue;

                if (enableReverb.Value)
                    Audio.TrackMixer.UpdateEffect(reverbParameters);
            }, true);

            reverbDamp = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbDamp);

            reverbParameters.fDamp = reverbDamp.Value;

            if (enableReverb.Value)
                Audio.TrackMixer.UpdateEffect(reverbParameters);

            reverbDamp.BindValueChanged(value =>
            {
                reverbParameters.fDamp = value.NewValue;

                if (enableReverb.Value)
                    Audio.TrackMixer.UpdateEffect(reverbParameters);
            }, true);

            reverbStereoWidth = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ReverbStereoWidth);

            reverbParameters.fWidth = reverbStereoWidth.Value;

            if (enableReverb.Value)
                Audio.TrackMixer.UpdateEffect(reverbParameters);

            reverbStereoWidth.BindValueChanged(value =>
            {
                reverbParameters.fWidth = value.NewValue;

                if (enableReverb.Value)
                    Audio.TrackMixer.UpdateEffect(reverbParameters);
            }, true);
            #endregion

            #region Rotate
            rotateEnabled = AudioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.RotateEnabled);

            if (rotateEnabled.Value)
                Audio.TrackMixer.AddEffect(rotateParameters);
            else
                Audio.TrackMixer.RemoveEffect(rotateParameters);

            rotateEnabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Audio.TrackMixer.AddEffect(rotateParameters);
                else
                    Audio.TrackMixer.RemoveEffect(rotateParameters);
            }, true);

            rotateRate = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.RotateRate);

            rotateParameters.fRate = rotateRate.Value;

            if (rotateEnabled.Value)
                Audio.TrackMixer.UpdateEffect(rotateParameters);

            rotateRate.BindValueChanged(value =>
            {
                rotateParameters.fRate = value.NewValue;

                if (rotateEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(rotateParameters);
            }, true);
            #endregion

            #region Echo
            echoEnabled = AudioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EchoEnabled);

            if (echoEnabled.Value)
                Audio.TrackMixer.AddEffect(echoParameters);
            else
                Audio.TrackMixer.RemoveEffect(echoParameters);

            echoEnabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Audio.TrackMixer.AddEffect(echoParameters);
                else
                    Audio.TrackMixer.RemoveEffect(echoParameters);
            }, true);

            echoDryMix = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDryMix);

            echoParameters.fDryMix = echoDryMix.Value - 2;

            if (echoEnabled.Value)
                Audio.TrackMixer.UpdateEffect(echoParameters);

            echoDryMix.BindValueChanged(value =>
            {
                echoParameters.fDryMix = value.NewValue - 2;

                if (echoEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(echoParameters);
            }, true);

            echoWetMix = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoWetMix);

            echoParameters.fWetMix = echoWetMix.Value - 2;

            if (echoEnabled.Value)
                Audio.TrackMixer.UpdateEffect(echoParameters);

            echoWetMix.BindValueChanged(value =>
            {
                echoParameters.fWetMix = value.NewValue - 2;

                if (echoEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(echoParameters);
            }, true);

            echoFeedback = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoFeedback);

            echoParameters.fFeedback = echoFeedback.Value - 1;

            if (echoEnabled.Value)
                Audio.TrackMixer.UpdateEffect(echoParameters);

            echoFeedback.BindValueChanged(value =>
            {
                echoParameters.fFeedback = value.NewValue - 1;

                if (echoEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(echoParameters);
            }, true);

            echoDelay = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EchoDelay);

            echoParameters.fDelay = echoDelay.Value;

            if (echoEnabled.Value)
                Audio.TrackMixer.UpdateEffect(echoParameters);

            echoDelay.BindValueChanged(value =>
            {
                echoParameters.fDelay = value.NewValue;

                if (echoEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(echoParameters);
            }, true);
            #endregion

            #region Distortion
            distortionEnabled = AudioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.DistortionEnabled);

            if (distortionEnabled.Value)
                Audio.TrackMixer.AddEffect(distortionParameters);
            else
                Audio.TrackMixer.RemoveEffect(distortionParameters);

            distortionEnabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Audio.TrackMixer.AddEffect(distortionParameters);
                else
                    Audio.TrackMixer.RemoveEffect(distortionParameters);
            }, true);

            distortionVolume = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.DistortionVolume);

            distortionParameters.fVolume = distortionVolume.Value;

            if (distortionEnabled.Value)
                Audio.TrackMixer.UpdateEffect(distortionParameters);

            distortionVolume.BindValueChanged(value =>
            {
                distortionParameters.fVolume = value.NewValue;

                if (distortionEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(distortionParameters);
            }, true);

            distortionDrive = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.DistortionDrive);

            distortionParameters.fDrive = distortionDrive.Value;

            if (distortionEnabled.Value)
                Audio.TrackMixer.UpdateEffect(distortionParameters);

            distortionDrive.BindValueChanged(value =>
            {
                distortionParameters.fDrive = value.NewValue;

                if (distortionEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(distortionParameters);
            }, true);
            #endregion

            #region Karaoke
            KaraokeVocalVolume = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.KaraokeVocalVolume);

            _karaokeDsp = new DSPProcedure(KaraokeDsp);

            karaokeModeEnabled = AudioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.KaraokeEnabled);

            if (karaokeModeEnabled.Value)
                Audio.TrackMixer.AddDSP(_karaokeDsp, 1);
            else
                Audio.TrackMixer.RemoveDSP(_karaokeDsp);

            karaokeModeEnabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Audio.TrackMixer.AddDSP(_karaokeDsp, 1);
                else
                    Audio.TrackMixer.RemoveDSP(_karaokeDsp);
            }, true);
            #endregion

            #region Chorus
            chorusEnabled = AudioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.ChorusEnabled);

            if (chorusEnabled.Value)
                Audio.TrackMixer.AddEffect(chorusParameters);
            else
                Audio.TrackMixer.RemoveEffect(chorusParameters);

            chorusEnabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Audio.TrackMixer.AddEffect(chorusParameters);
                else
                    Audio.TrackMixer.RemoveEffect(chorusParameters);
            }, true);

            chorusDryMix = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusDryMix);

            chorusParameters.fDryMix = chorusDryMix.Value - 2;

            if (chorusEnabled.Value)
                Audio.TrackMixer.UpdateEffect(chorusParameters);

            chorusDryMix.BindValueChanged(value =>
            {
                chorusParameters.fDryMix = value.NewValue - 2;

                if (chorusEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(chorusParameters);
            }, true);

            chorusWetMix = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusWetMix);

            chorusParameters.fWetMix = chorusWetMix.Value - 2;

            if (chorusEnabled.Value)
                Audio.TrackMixer.UpdateEffect(chorusParameters);

            chorusWetMix.BindValueChanged(value =>
            {
                chorusParameters.fWetMix = value.NewValue - 2;

                if (chorusEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(chorusParameters);
            }, true);

            chorusFeedback = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusFeedback);

            chorusParameters.fFeedback = chorusFeedback.Value - 1;

            if (chorusEnabled.Value)
                Audio.TrackMixer.UpdateEffect(chorusParameters);

            chorusFeedback.BindValueChanged(value =>
            {
                chorusParameters.fFeedback = value.NewValue - 1;

                if (chorusEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(chorusParameters);
            }, true);

            chorusMinSweep = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusMinSweep);

            chorusParameters.fMinSweep = chorusMinSweep.Value;

            if (chorusEnabled.Value)
                Audio.TrackMixer.UpdateEffect(chorusParameters);

            chorusMinSweep.BindValueChanged(value =>
            {
                chorusParameters.fMinSweep = value.NewValue;

                if (chorusEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(chorusParameters);
            }, true);

            chorusMaxSweep = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusMaxSweep);

            chorusParameters.fMaxSweep = chorusMaxSweep.Value;

            if (chorusEnabled.Value)
                Audio.TrackMixer.UpdateEffect(chorusParameters);

            chorusMaxSweep.BindValueChanged(value =>
            {
                chorusParameters.fMaxSweep = value.NewValue;

                if (chorusEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(chorusParameters);
            }, true);

            chorusRate = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.ChorusRate);

            chorusParameters.fRate = chorusRate.Value;

            if (chorusEnabled.Value)
                Audio.TrackMixer.UpdateEffect(chorusParameters);

            chorusRate.BindValueChanged(value =>
            {
                chorusParameters.fRate = value.NewValue;

                if (chorusEnabled.Value)
                    Audio.TrackMixer.UpdateEffect(chorusParameters);
            }, true);
            #endregion

            #region 8-bit Effect
            eightBitEffectBitDepth = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EightBitEffectBitDepth);
            eightBitEffectDownsampleRatio = AudioEffectsConfig.GetBindable<float>(AudioEffectsSetting.EightBitEffectDownsampleRatio);

            _eightBitDsp = new DSPProcedure(EightBitDspCallback);

            eightBitEffectEnabled = AudioEffectsConfig.GetBindable<bool>(AudioEffectsSetting.EightBitEffectEnabled);

            if (eightBitEffectEnabled.Value)
                Audio.TrackMixer.AddDSP(_eightBitDsp, 1);
            else
                Audio.TrackMixer.RemoveDSP(_eightBitDsp);

            eightBitEffectEnabled.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                    Audio.TrackMixer.AddDSP(_eightBitDsp, 1);
                else
                    Audio.TrackMixer.RemoveDSP(_eightBitDsp);
            }, true);
            #endregion
        }
        #endregion

        public Bindable<float> KaraokeVocalVolume { get; set; }

        private unsafe void KaraokeDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            int sampleCount = length / sizeof(float);
            float[] samples = new float[sampleCount];

            Marshal.Copy(buffer, samples, 0, sampleCount);

            for (int i = 0; i < sampleCount; i += 2)
            {
                float left = samples[i];
                float right = samples[i + 1];

                // 보컬이 제거된 반주 소리 (L-R 연산)
                float karaokeSample = (left - right) * 0.5f;

                // VocalVolume이 1.0이면 원본 소리
                // VocalVolume이 0.0이면 보컬 제거 소리
                float vocalVolume = KaraokeVocalVolume.Value;
                float karaokeVolume = 1.0f - vocalVolume;

                samples[i] =
                    left * vocalVolume +
                    karaokeSample * karaokeVolume;

                samples[i + 1] =
                    right * vocalVolume +
                    karaokeSample * karaokeVolume;
            }

            Marshal.Copy(samples, 0, buffer, sampleCount);
        }

        float bitDepth = 8;
        float downsampleRatio = 0.25f; // 원본의 1/4 속도로 갱신 = 거친 chiptune 느낌

        float[] held = new float[2];
        float phase = 0f;

        public unsafe void EightBitDspCallback(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            // BASS DSP는 대체로 float PCM 버퍼를 전달합니다.
            int sampleCount = length / sizeof(float);
            var samples = new float[sampleCount];
            Marshal.Copy(buffer, samples, 0, sampleCount);

            const int channels = 2; // 실제 채널 수에 맞게 지정
            float levels = (1 << (int)eightBitEffectBitDepth.Value) - 1; // 255

            for (int i = 0; i < sampleCount; i += channels)
            {
                bool refresh = phase <= 0f;

                for (int ch = 0; ch < channels; ch++)
                {
                    if (refresh)
                    {
                        float x = Math.Clamp(samples[i + ch], -1f, 1f);

                        // 8-bit quantization: -1..1 → 256 discrete levels
                        held[ch] = MathF.Round(x * levels) / levels;
                    }

                    samples[i + ch] = held[ch];
                }

                phase += downsampleRatio;
                if (phase >= 1f)
                    phase -= 1f;
            }

            Marshal.Copy(samples, 0, buffer, sampleCount);
        }

        public virtual void AttemptExit(bool forceQuit = false)
        {
            if (!OnExiting())
                Exit();
            else
                Scheduler.AddDelayed(() => AttemptExit(forceQuit), 2000);
        }

        public virtual void RegisterMessage(INekoPlayerAppMessageHandler appMessageHandler)
        {
            AppMessageHandler = appMessageHandler;
        }

        public INekoPlayerAppMessageHandler AppMessageHandler;

        protected virtual Container CreateScalingContainer() => new DrawSizePreservingFillContainer();

        /// <summary>
        /// The <see cref="Edges"/> that the game should be drawn over at a top level.
        /// Defaults to <see cref="Edges.None"/>.
        /// </summary>
        protected virtual Edges SafeAreaOverrideEdges => Edges.None;

        protected SafeAreaContainer SafeAreaContainer { get; private set; }

        private DependencyContainer dependencies;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        public void RequestUpdateWindowTitle(string customTitle)
        {
            if (Host.Window == null)
                return;

            string newTitle = IsDeployedBuild ? "NekoPlayer" : "NekoPlayer (development)";

            if (!string.IsNullOrEmpty(customTitle))
            {
                newTitle = IsDeployedBuild ? $"NekoPlayer | {customTitle}" : $"NekoPlayer (development) | {customTitle}";
            }

            if (newTitle != Host.Window.Title)
                Host.Window.Title = newTitle;
        }

        private void updateLanguage() => CurrentLanguage.Value = LanguageExtensions.GetLanguageFor(frameworkLocale.Value, localisationParameters.Value);

        protected virtual void InitialiseFonts()
        {
            NekoPlayerFontLoader.LoadFonts(this, Resources);
        }

        public void EnableTrackNormlization()
        {
            Audio.Tracks.AddAdjustment(osu.Framework.Audio.AdjustableProperty.Volume, CurrentTrackNormalizeVolume);
        }

        public void DisableTrackNormalization()
        {
            Audio.Tracks.RemoveAdjustment(osu.Framework.Audio.AdjustableProperty.Volume, CurrentTrackNormalizeVolume);
        }
    }
}

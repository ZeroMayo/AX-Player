// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;

namespace AXPlayer.App.Input.Binding
{
    public partial class GlobalActionContainer : KeyBindingContainer<GlobalAction>, IHandleGlobalKeyboardInput, IKeyBindingHandler<GlobalAction>
    {
        protected override bool Prioritised => true;

        private readonly IKeyBindingHandler<GlobalAction>? handler;
        private readonly GlobalShortcutManager shortcutManager;
        private readonly IBindable<string> shortcutBindings;

        public GlobalActionContainer(AXPlayerAppBase? game, GlobalShortcutManager shortcutManager)
            : base(matchingMode: KeyCombinationMatchingMode.Modifiers)
        {
            this.shortcutManager = shortcutManager;
            shortcutBindings = shortcutManager.BindingsChanged.GetBoundCopy();

            if (game is IKeyBindingHandler<GlobalAction> h)
                handler = h;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            shortcutBindings.BindValueChanged(_ => Schedule(ReloadMappings));
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                shortcutBindings.UnbindEvents();

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// All default key bindings across all categories, ordered with highest priority first.
        /// </summary>
        /// <remarks>
        /// IMPORTANT: Take care when changing order of the items in the enumerable.
        /// It is used to decide the order of precedence, with the earlier items having higher precedence.
        /// </remarks>
        public override IEnumerable<IKeyBinding> DefaultKeyBindings => shortcutManager.GetBindings();

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e) => handler?.OnPressed(e) == true;

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e) => handler?.OnReleased(e);

        /// <summary>
        /// Custom playback speed preset shortcuts are assigned to consecutive actions, with <c>Ctrl+0</c> applying the tenth preset.
        /// </summary>
        public const int CUSTOM_PLAYBACK_SPEED_PRESET_SHORTCUT_COUNT = 10;

        public static bool TryGetCustomPlaybackSpeedPresetIndex(GlobalAction action, out int index)
        {
            index = action - GlobalAction.ApplyCustomPlaybackSpeedPreset1;
            return index >= 0 && index < CUSTOM_PLAYBACK_SPEED_PRESET_SHORTCUT_COUNT;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (shortcutManager.CapturingAction.Value != null)
                return shortcutManager.Capture(e);

            return base.OnKeyDown(e);
        }

        public static IEnumerable<KeyBinding> GlobalKeyBindings => new[]
        {
            new KeyBinding(InputKey.Enter, GlobalAction.Select),
            new KeyBinding(InputKey.KeypadEnter, GlobalAction.Select),

            new KeyBinding(InputKey.Escape, GlobalAction.Back),
            new KeyBinding(InputKey.ExtraMouseButton1, GlobalAction.Back),

            new KeyBinding(InputKey.Space, GlobalAction.PlayPause),
            new KeyBinding(InputKey.K, GlobalAction.PlayPause),
            //new KeyBinding(InputKey.PlayPause, GlobalAction.PlayPause),

            new KeyBinding(InputKey.Right, GlobalAction.FastForward_10sec),
            new KeyBinding(InputKey.Left, GlobalAction.FastRewind_10sec),
            new KeyBinding(InputKey.L, GlobalAction.FastForward_10sec),
            new KeyBinding(InputKey.J, GlobalAction.FastRewind_10sec),

            new KeyBinding(InputKey.A, GlobalAction.DecreasePlaybackSpeed),
            new KeyBinding(InputKey.D, GlobalAction.IncreasePlaybackSpeed),

            new KeyBinding(InputKey.Down, GlobalAction.DecreaseVideoVolume),
            new KeyBinding(InputKey.Up, GlobalAction.IncreaseVideoVolume),

            new KeyBinding(new[] { InputKey.Shift, InputKey.A }, GlobalAction.DecreasePlaybackSpeed2),
            new KeyBinding(new[] { InputKey.Shift, InputKey.D }, GlobalAction.IncreasePlaybackSpeed2),

            new KeyBinding(new[] { InputKey.Control, InputKey.Number1 }, GlobalAction.ApplyCustomPlaybackSpeedPreset1),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number2 }, GlobalAction.ApplyCustomPlaybackSpeedPreset2),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number3 }, GlobalAction.ApplyCustomPlaybackSpeedPreset3),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number4 }, GlobalAction.ApplyCustomPlaybackSpeedPreset4),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number5 }, GlobalAction.ApplyCustomPlaybackSpeedPreset5),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number6 }, GlobalAction.ApplyCustomPlaybackSpeedPreset6),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number7 }, GlobalAction.ApplyCustomPlaybackSpeedPreset7),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number8 }, GlobalAction.ApplyCustomPlaybackSpeedPreset8),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number9 }, GlobalAction.ApplyCustomPlaybackSpeedPreset9),
            new KeyBinding(new[] { InputKey.Control, InputKey.Number0 }, GlobalAction.ApplyCustomPlaybackSpeedPreset10),

            new KeyBinding(new[] { InputKey.Shift, InputKey.P }, GlobalAction.PrevVideo),
            new KeyBinding(new[] { InputKey.Shift, InputKey.N }, GlobalAction.NextVideo),
            new KeyBinding(new[] { InputKey.TrackPrevious }, GlobalAction.PrevVideo),
            new KeyBinding(new[] { InputKey.TrackNext }, GlobalAction.NextVideo),

            new KeyBinding(new[] { InputKey.Shift, InputKey.C }, GlobalAction.CycleCaptionLanguage),
            new KeyBinding(new[] { InputKey.Control, InputKey.F6 }, GlobalAction.CycleAspectRatio),
            new KeyBinding(new[] { InputKey.Control, InputKey.Shift, InputKey.F5 }, GlobalAction.CycleScalingMode),

            new KeyBinding(new[] { InputKey.Control, InputKey.O }, GlobalAction.OpenSettings),
            new KeyBinding(new[] { InputKey.Control, InputKey.D }, GlobalAction.OpenDescription),
            new KeyBinding(new[] { InputKey.Alt, InputKey.C }, GlobalAction.OpenComments),
            new KeyBinding(new[] { InputKey.Alt, InputKey.A }, GlobalAction.OpenAudioEffects),
            new KeyBinding(new[] { InputKey.P }, GlobalAction.OpenPlaylist),
            new KeyBinding(new[] { InputKey.R }, GlobalAction.ReportAbuse),

            new KeyBinding(new[] { InputKey.Alt, InputKey.P }, GlobalAction.ToggleAdjustPitchOnSpeedChange),

            new KeyBinding(new[] { InputKey.Control, InputKey.P }, GlobalAction.ToggleFPSDisplay),

            new KeyBinding(new[] { InputKey.F12 }, GlobalAction.TakeScreenshot),

            new KeyBinding(new[] { InputKey.Alt, InputKey.Up }, GlobalAction.IncreaseVolume),
            new KeyBinding(new[] { InputKey.Alt, InputKey.Down }, GlobalAction.DecreaseVolume),

            new KeyBinding(new[] { InputKey.Control, InputKey.F4 }, GlobalAction.ToggleMute),
            new KeyBinding(new[] { InputKey.Control, InputKey.Shift, InputKey.R }, GlobalAction.RestartApp),

            new KeyBinding(new[] { InputKey.Alt, InputKey.O }, GlobalAction.OpenLoadVideo),
            new KeyBinding(new[] { InputKey.Slash }, GlobalAction.OpenSearch),

            new KeyBinding(new[] { InputKey.Alt, InputKey.M }, GlobalAction.OpenMyPlaylists),
            new KeyBinding(new[] { InputKey.Control, InputKey.M }, GlobalAction.AddPlaylistKey),
            new KeyBinding(new[] { InputKey.Alt, InputKey.S }, GlobalAction.SaveVideoToPlaylist),
            new KeyBinding(new[] { InputKey.Alt, InputKey.R }, GlobalAction.ToggleRepeatVideo),
            new KeyBinding(new[] { InputKey.Tab }, GlobalAction.OpenMenu),
            new KeyBinding(InputKey.S, GlobalAction.ResetPlaybackSpeed),
            new KeyBinding(new[] { InputKey.Alt, InputKey.Shift, InputKey.F4 }, GlobalAction.QuitApp),

            new KeyBinding(new[] { InputKey.Shift, InputKey.F1 }, GlobalAction.ToggleReverbEffect),
            new KeyBinding(new[] { InputKey.Shift, InputKey.F2 }, GlobalAction.ToggleRotateEffect),
            new KeyBinding(new[] { InputKey.Shift, InputKey.F3 }, GlobalAction.ToggleEchoEffect),
            new KeyBinding(new[] { InputKey.Shift, InputKey.F4 }, GlobalAction.ToggleDistortionEffect),
            new KeyBinding(new[] { InputKey.Shift, InputKey.F5 }, GlobalAction.ToggleKaraokeEffect),
            new KeyBinding(new[] { InputKey.Shift, InputKey.F6 }, GlobalAction.ToggleChorusEffect),

            new KeyBinding(new[] { InputKey.Control, InputKey.I }, GlobalAction.ToggleControlsPinState),
            new KeyBinding(new[] { InputKey.Control, InputKey.N }, GlobalAction.Notifications),

            new KeyBinding(new[] { InputKey.Control, InputKey.F8 }, GlobalAction.CycleProfileShape),
            new KeyBinding(new[] { InputKey.Shift, InputKey.F7 }, GlobalAction.ToggleEightBitEffect),

            new KeyBinding(new[] { InputKey.Shift, InputKey.S }, GlobalAction.OpenPlaybackSpeedMenu),
            new KeyBinding(new[] { InputKey.Shift, InputKey.V }, GlobalAction.OpenVideoVolumeMenu),

            new KeyBinding(new[] { InputKey.Shift, InputKey.Minus }, GlobalAction.DecreaseVideoDim),
            new KeyBinding(new[] { InputKey.Shift, InputKey.Plus }, GlobalAction.IncreaseVideoDim),
            new KeyBinding(new[] { InputKey.Shift, InputKey.KeypadMinus }, GlobalAction.DecreaseVideoDim),
            new KeyBinding(new[] { InputKey.Shift, InputKey.KeypadPlus }, GlobalAction.IncreaseVideoDim),

            new KeyBinding(new[] { InputKey.Control, InputKey.Shift, InputKey.S }, GlobalAction.ToggleSmoothPlaybackSpeed),
        };
    }

    /// <remarks>
    /// IMPORTANT: New entries should always be added at the end of the enum, as key bindings are stored using the enum's numeric value and
    /// changes in order would cause key bindings to get associated with the wrong action.
    /// </remarks>
    public enum GlobalAction
    {
        Back,
        Select,
        PlayPause,
        FastForward_10sec,
        FastRewind_10sec,
        OpenSettings,
        ToggleAdjustPitchOnSpeedChange,
        ToggleFPSDisplay,
        CycleCaptionLanguage,
        CycleAspectRatio,
        CycleScalingMode,

        DecreasePlaybackSpeed,
        IncreasePlaybackSpeed,

        DecreasePlaybackSpeed2,
        IncreasePlaybackSpeed2,

        OpenDescription,
        OpenComments,

        TakeScreenshot,
        ReportAbuse,

        DecreaseVideoVolume,
        IncreaseVideoVolume,

        OpenPlaylist,

        PrevVideo,
        NextVideo,

        OpenAudioEffects,

        IncreaseVolume,
        DecreaseVolume,

        ToggleMute,
        RestartApp,

        OpenLoadVideo,
        OpenSearch,
        OpenMyPlaylists,
        AddPlaylistKey,
        SaveVideoToPlaylist,
        ToggleRepeatVideo,
        OpenMenu,
        ResetPlaybackSpeed,
        QuitApp,

        ToggleReverbEffect,
        ToggleRotateEffect,
        ToggleEchoEffect,
        ToggleDistortionEffect,
        ToggleKaraokeEffect,
        ToggleChorusEffect,

        ToggleControlsPinState,
        Notifications,
        CycleProfileShape,
        ToggleEightBitEffect,

        OpenPlaybackSpeedMenu,
        OpenVideoVolumeMenu,

        DecreaseVideoDim,
        IncreaseVideoDim,
        ToggleSmoothPlaybackSpeed,

        ApplyCustomPlaybackSpeedPreset1,
        ApplyCustomPlaybackSpeedPreset2,
        ApplyCustomPlaybackSpeedPreset3,
        ApplyCustomPlaybackSpeedPreset4,
        ApplyCustomPlaybackSpeedPreset5,
        ApplyCustomPlaybackSpeedPreset6,
        ApplyCustomPlaybackSpeedPreset7,
        ApplyCustomPlaybackSpeedPreset8,
        ApplyCustomPlaybackSpeedPreset9,
        ApplyCustomPlaybackSpeedPreset10,
    }
}

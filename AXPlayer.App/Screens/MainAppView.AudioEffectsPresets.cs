// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        private void refreshAudioEffectPresets()
        {
            if (audioEffectPresetList == null)
                return;

            audioEffectPresetList.Clear();

            IReadOnlyList<AudioEffectsPreset> presets = audioEffectsConfig.GetPresets();

            if (presets.Count == 0)
            {
                audioEffectPresetList.Add(new ProjectYomiSpriteText
                {
                    Text = AXPlayerStrings.NoAudioEffectPresets,
                    Font = AXPlayerApp.DefaultFont.With(size: 15),
                    Colour = overlayColourProvider1.Content2,
                    Margin = new MarginPadding { Horizontal = 4, Vertical = 4 },
                });

                return;
            }

            foreach (AudioEffectsPreset preset in presets)
            {
                string presetName = preset.Name;

                audioEffectPresetList.Add(new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 44,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new RoundedButton
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 44,
                                Margin = new MarginPadding { Right = 8 },
                                Text = presetName,
                                BackgroundColour = overlayColourProvider1.Background4,
                                Action = () => applyAudioEffectPreset(presetName),
                            },
                            new RoundedIconOnlyButton(FontAwesome.Solid.Times)
                            {
                                Width = 44,
                                Height = 44,
                                BackgroundColour = colours.Red3,
                                TooltipText = AXPlayerStrings.DeleteAudioEffectPreset,
                                Action = () => deleteAudioEffectPreset(presetName),
                            },
                        },
                    },
                });
            }
        }

        private void saveAudioEffectPreset()
        {
            if (audioEffectPresetNameBox == null)
                return;

            if (!audioEffectsConfig.SavePreset(audioEffectPresetNameBox.Current.Value))
                return;

            audioEffectPresetNameBox.Current.Value = string.Empty;
            newToastOverlay.ShowToast(AXPlayerStrings.PresetSaved);
            refreshAudioEffectPresets();
        }

        private void applyAudioEffectPreset(string name)
        {
            if (!audioEffectsConfig.ApplyPreset(name))
                return;

            newToastOverlay.ShowToast(AXPlayerStrings.AudioEffectPresetApplied(name));
        }

        private void deleteAudioEffectPreset(string name)
        {
            if (audioEffectsConfig.DeletePreset(name))
                refreshAudioEffectPresets();
        }
    }
}

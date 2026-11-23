// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK.Graphics;

namespace AXPlayer.App.Graphics.Caption
{
    public partial class ClosedCaptionPreview : Container
    {
        private ProjectYomiTextFlowContainer spriteText;
        private Bindable<CaptionFonts> captionFont;
        private Bindable<float> captionBGOpacity;
        private Bindable<Colour4> captionBGColor;
        private BackdropBlurContainer captionContainer;
        private Box bg;
        private Action<SpriteText> textCreationParameters;
        private Action<SpriteText> shadowOptions;
        private Bindable<int> captionBGRadius;
        private Bindable<bool> smoothCaptionAnimation;

        private Bindable<bool> closedCaptionsBackdropBlur;
        private CustomCaptionFonts? customCaptionFonts;

        /// <summary>
        /// The family of the font the user added for the captions, or <c>null</c> when one of the fonts
        /// shipped with the player is used.
        /// </summary>
        private string? customFontFamily;

        [BackgroundDependencyLoader(true)]
        private void load(AXPlayerConfigManager config, SessionStatics sessionStatics, TextureStore textureStore, CustomCaptionFonts? customCaptionFonts)
        {
            this.customCaptionFonts = customCaptionFonts;
            captionFont = config.GetBindable<CaptionFonts>(AXPlayerSetting.CaptionFont);
            captionBGOpacity = config.GetBindable<float>(AXPlayerSetting.CaptionBGOpacity);
            captionBGColor = config.GetBindable<Colour4>(AXPlayerSetting.CaptionBGColor);
            captionBGRadius = config.GetBindable<int>(AXPlayerSetting.CaptionCornerRadius);
            smoothCaptionAnimation = config.GetBindable<bool>(AXPlayerSetting.SmoothCaptionAnimation);
            closedCaptionsBackdropBlur = config.GetBindable<bool>(AXPlayerSetting.ClosedCaptionsBackdropBlur);

            captionContainer = new BackdropBlurContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeDuration = 350,
                AutoSizeEasing = Easing.OutQuart,
                Masking = true,
                BlurSigma = new osuTK.Vector2(10),
                Children = new Drawable[]
                {
                    bg = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.5f
                    },
                    spriteText = new ProjectYomiTextFlowContainer(t =>
                    {
                        t.Font = AXPlayerApp.Fonts.GoogleSansFlex.With(size: 24);
                        t.Shadow = false;
                    })
                    {
                        TextAnchor = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Margin = new MarginPadding(4),
                    }
                }
            };

            Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = AXPlayerApp.UI_CORNER_RADIUS,
                Children = new Drawable[]
                {
                    new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Origin = Anchor.Centre,
                        Anchor = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        Texture = textureStore.Get("AXPlayerBanner"),
                    },
                    captionContainer,
                }
            });

            captionBGRadius.BindValueChanged(corner =>
            {
                captionContainer.CornerRadius = new CornersInfo(corner.NewValue);
            }, true);

            closedCaptionsBackdropBlur.BindValueChanged(blur =>
            {
                captionContainer.BlurSigma = new osuTK.Vector2(blur.NewValue ? 10 : 0);
            }, true);

            captionBGOpacity.BindValueChanged(opacity =>
            {
                bg.Alpha = opacity.NewValue;

                if (opacity.NewValue < 0.5f)
                {
                    shadowOptions = spriteText => spriteText.Shadow = true;
                }
                else
                {
                    shadowOptions = spriteText => spriteText.Shadow = false;
                }
                RefreshFont();
            }, true);

            captionBGColor.BindValueChanged(colour =>
            {
                bg.Colour = colour.NewValue;
            }, true);

            smoothCaptionAnimation.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                {
                    captionContainer.AutoSizeDuration = 350;
                }
                else
                {
                    captionContainer.AutoSizeDuration = 0;
                }
            }, true);

            captionFont.BindValueChanged(_ => updateCaptionFont(), true);

            if (customCaptionFonts != null)
            {
                customCaptionFonts.Enabled.BindValueChanged(_ => updateCaptionFont());
                customCaptionFonts.SelectedFont.BindValueChanged(_ => updateCaptionFont());
                customCaptionFonts.AvailableFontsChanged += updateCaptionFont;
            }
        }

        /// <summary>
        /// Resolves the font the preview is drawn with, and redraws it with the new font.
        /// </summary>
        private void updateCaptionFont()
        {
            customFontFamily = customCaptionFonts?.GetSelectedFamily();

            textCreationParameters = t => t.Font = getSelectedFont(24);

            RefreshFont();
        }

        private FontUsage getSelectedFont(float size)
        {
            if (customFontFamily != null)
                return FontUsage.Default.With(customFontFamily, size);

            return captionFont.Value switch
            {
                CaptionFonts.Rubik => AXPlayerApp.Fonts.Rubik.With(size: size),
                CaptionFonts.Pretendard => AXPlayerApp.Fonts.Pretendard.With(size: size),
                CaptionFonts.Ownglyph_PDH => AXPlayerApp.Fonts.Ownglyph_PDH.With(size: size),
                CaptionFonts.Dovemayo_Gothic => AXPlayerApp.Fonts.Dovemayo_Gothic.With(size: size),
                CaptionFonts.Griun_Mongtori => AXPlayerApp.Fonts.Griun_Mongtori.With(size: size),
                CaptionFonts.Cafe24Syongsyong => AXPlayerApp.Fonts.Cafe24Syongsyong.With(size: size),
                CaptionFonts.Roboto => AXPlayerApp.Fonts.Roboto.With(size: size),
                CaptionFonts.DreamHeumulKR => AXPlayerApp.Fonts.DreamHeumulKR.With(size: size),
                CaptionFonts.Hakgyoansim_ManitoR => AXPlayerApp.Fonts.Hakgyoansim_ManitoR.With(size: size),
                CaptionFonts.OwnglyphDaisy => AXPlayerApp.Fonts.OwnglyphDaisy.With(size: size),
                CaptionFonts.OwnglyphYuntaeng => AXPlayerApp.Fonts.OwnglyphYuntaeng.With(size: size),
                CaptionFonts.GoogleSansFlexRounded => AXPlayerApp.Fonts.GoogleSansFlexRounded.With(size: size),
                CaptionFonts.Hippie => AXPlayerApp.Fonts.Hippie.With(size: size),
                CaptionFonts.GriunSobokhee => AXPlayerApp.Fonts.GriunSobokhee.With(size: size),
                CaptionFonts.GriunNulnul => AXPlayerApp.Fonts.Griun_Nulnul.With(size: size),
                _ => AXPlayerApp.Fonts.GoogleSansFlex.With(size: size),
            };
        }

        private void RefreshFont()
        {
            spriteText.Text = "";
            spriteText.AddText(AXPlayerStrings.ClosedCaptionPreview, text =>
            {
                textCreationParameters?.Invoke(text);
                shadowOptions?.Invoke(text);
            });
        }
    }
}

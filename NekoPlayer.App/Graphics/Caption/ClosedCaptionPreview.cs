// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NekoPlayer.App.Config;
using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Localisation;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK.Graphics;

namespace NekoPlayer.App.Graphics.Caption
{
    public partial class ClosedCaptionPreview : Container
    {
        private ProjectYomiTextFlowContainer spriteText;
        private Bindable<CaptionFonts> captionFont;
        private Bindable<float> captionBGOpacity;
        private Bindable<Colour4> captionBGColor;
        private Container captionContainer;
        private Box bg;
        private Action<SpriteText> textCreationParameters;
        private Action<SpriteText> shadowOptions;
        private Bindable<int> captionBGRadius;

        [BackgroundDependencyLoader]
        private void load(NekoPlayerConfigManager config, SessionStatics sessionStatics, TextureStore textureStore)
        {
            captionFont = config.GetBindable<CaptionFonts>(NekoPlayerSetting.CaptionFont);
            captionBGOpacity = config.GetBindable<float>(NekoPlayerSetting.CaptionBGOpacity);
            captionBGColor = config.GetBindable<Colour4>(NekoPlayerSetting.CaptionBGColor);
            captionBGRadius = config.GetBindable<int>(NekoPlayerSetting.CaptionCornerRadius);

            captionContainer = new Container
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeDuration = 350,
                AutoSizeEasing = Easing.OutQuart,
                Masking = true,
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
                        t.Font = NekoPlayerApp.Fonts.GoogleSansFlex.With(size: 24);
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
                CornerRadius = NekoPlayerApp.UI_CORNER_RADIUS,
                Children = new Drawable[]
                {
                    new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Origin = Anchor.Centre,
                        Anchor = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        Texture = textureStore.Get("ClosedCaptionPreviewBG"),
                    },
                    captionContainer,
                }
            });

            captionBGRadius.BindValueChanged(corner =>
            {
                captionContainer.CornerRadius = new CornersInfo(corner.NewValue);
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

            captionFont.BindValueChanged(v =>
            {
                switch (v.NewValue)
                {
                    case CaptionFonts.GoogleSansFlex:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.GoogleSansFlex.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Rubik:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Rubik.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Pretendard:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Pretendard.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Hungeul:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Hungeul.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Ownglyph_PDH:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Ownglyph_PDH.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Dovemayo_Gothic:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Dovemayo_Gothic.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Griun_Mongtori:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Griun_Mongtori.With(size: 24);
                        break;
                    }
                    case CaptionFonts.ONE_Mobile_POP:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.ONE_Mobile_POP.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Cafe24Syongsyong:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Cafe24Syongsyong.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Roboto:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Roboto.With(size: 24);
                        break;
                    }
                    case CaptionFonts.DreamHeumulKR:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.DreamHeumulKR.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Hakgyoansim_ManitoR:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Hakgyoansim_ManitoR.With(size: 24);
                        break;
                    }
                    case CaptionFonts.OwnglyphDaisy:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.OwnglyphDaisy.With(size: 24);
                        break;
                    }
                    case CaptionFonts.OwnglyphYuntaeng:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.OwnglyphYuntaeng.With(size: 24);
                        break;
                    }
                    case CaptionFonts.GoogleSansFlexRounded:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.GoogleSansFlexRounded.With(size: 24);
                        break;
                    }
                    case CaptionFonts.Hippie:
                    {
                        textCreationParameters = spriteText => spriteText.Font = NekoPlayerApp.Fonts.Hippie.With(size: 24);
                        break;
                    }
                }
                RefreshFont();
            }, true);
        }

        private void RefreshFont()
        {
            spriteText.Text = "";
            spriteText.AddText(NekoPlayerStrings.ClosedCaptionPreview, text =>
            {
                textCreationParameters?.Invoke(text);
                shadowOptions?.Invoke(text);
            });
        }
    }
}

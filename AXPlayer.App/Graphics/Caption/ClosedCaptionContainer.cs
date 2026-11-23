// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.
//
// SRV3 support:
// - Parses <timedtext format="3"> XML directly.
// - Supports <p> and <s> spans, pen definitions, word/line alignment,
//   caption positioning, per-span foreground colour/opacity, font size,
//   and the SRV3 bold/italic/underline flags.
// - The legacy YoutubeExplode ClosedCaptionTrack constructor remains supported.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osuTK.Graphics;
using YoutubeExplode.Videos.ClosedCaptions;
using AXPlayer.App.Config;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Graphics.Videos;
using AXPlayer.App.Graphics.UserInterface;
using osu.Framework.Extensions.Color4Extensions;

namespace AXPlayer.App.Graphics.Caption
{
    public partial class ClosedCaptionContainer : Container
    {
        public Bindable<bool> UIVisiblity = new Bindable<bool>();

        private ProjectYomiTextFlowContainer spriteText;
        private YouTubeVideoPlayer videoPlayer;

        // Legacy YouTubeExplode captions.
        private ClosedCaptionTrack captionTrack;

        // SRV3 captions. This is intentionally kept separate from YoutubeExplode,
        // because ClosedCaptionTrack does not expose SRV3's pen/span information.
        private Srv3CaptionTrack srv3Track;

        // SRV3 documents can contain several <p> elements that are active at the
        // same time, so each active cue is rendered by its own display instead of
        // sharing a single text flow.
        private Container srv3CaptionLayer;
        private readonly Dictionary<Srv3Caption, Srv3CaptionDisplay> srv3Displays = new Dictionary<Srv3Caption, Srv3CaptionDisplay>();
        private IReadOnlyList<Srv3Caption> lastSrv3Cues;

        private Bindable<bool> captionEnabled;
        private Bindable<CaptionFonts> captionFont;
        private BackdropBlurContainer captionContainer;
        private Bindable<float> bottomMargin = new Bindable<float>();

        public ClosedCaptionContainer(YouTubeVideoPlayer videoPlayer, ClosedCaptionTrack captionTrack)
        {
            this.videoPlayer = videoPlayer;
            UpdateCaptionTrack(captionTrack);
            initialiseContainer();
        }

        /// <summary>
        /// Creates a caption container from an SRV3/timedtext XML document.
        /// </summary>
        public ClosedCaptionContainer(YouTubeVideoPlayer videoPlayer, string srv3Xml)
        {
            this.videoPlayer = videoPlayer;
            UpdateSrv3CaptionTrack(srv3Xml);
            initialiseContainer();
        }

        public ClosedCaptionContainer(YouTubeVideoPlayer videoPlayer, ClosedCaptionTrack captionTrack, string srv3Xml, Bindable<bool> useNewCaptionFeature)
        {
            this.videoPlayer = videoPlayer;

            if (useNewCaptionFeature.Value)
                UpdateSrv3CaptionTrack(srv3Xml);
            else
                UpdateCaptionTrack(captionTrack);

            initialiseContainer();
        }

        private void initialiseContainer()
        {
            Padding = new MarginPadding(32);
            RelativeSizeAxes = Axes.Both;
            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            AlwaysPresent = true;
        }

        public void UpdateCaptionTrack(ClosedCaptionTrack captionTrack)
        {
            srv3Track = null;
            this.captionTrack = captionTrack;
            resetSrv3Captions();
        }

        /// <summary>
        /// Replaces the current caption track with an SRV3 document.
        /// </summary>
        public void UpdateSrv3CaptionTrack(string srv3Xml)
        {
            captionTrack = null;
            srv3Track = string.IsNullOrWhiteSpace(srv3Xml)
                ? null
                : Srv3CaptionTrack.Parse(srv3Xml);

            // Which document the captions come from decides whether the pen attributes of the document
            // are honoured at all, so the choice is reported.
            Logger.Log(srv3Track == null
                    ? @"The captions are rendered from the closed caption track (no SRV3 document was given)."
                    : $@"The captions are rendered from an SRV3 document with {srv3Track.DescribeBackgrounds()}.",
                LoggingTarget.Runtime);

            resetSrv3Captions();
        }

        private Bindable<bool> controlsVisibleState = null!, smoothCaptionAnimation = null!;
        private Action<SpriteText> textCreationParameters;
        private Action<SpriteText> shadowOptions;
        private Bindable<float> captionBGOpacity;
        private Bindable<Colour4> captionBGColor;
        private Bindable<int> captionBGRadius;
        private Box bg;
        private Bindable<bool> closedCaptionsBackdropBlur;
        private CustomCaptionFonts customCaptionFonts;

        /// <summary>
        /// The family of the font the user added for the captions, or <c>null</c> when one of the fonts
        /// shipped with the player is used.
        /// </summary>
        private string customFontFamily;

        [BackgroundDependencyLoader(true)]
        private void load(AXPlayerConfigManager config, SessionStatics sessionStatics, CustomCaptionFonts customCaptionFonts)
        {
            this.customCaptionFonts = customCaptionFonts;

            controlsVisibleState = sessionStatics.GetBindable<bool>(Static.IsControlVisible);
            captionEnabled = config.GetBindable<bool>(AXPlayerSetting.CaptionEnabled);
            captionFont = config.GetBindable<CaptionFonts>(AXPlayerSetting.CaptionFont);
            captionBGOpacity = config.GetBindable<float>(AXPlayerSetting.CaptionBGOpacity);
            captionBGColor = config.GetBindable<Colour4>(AXPlayerSetting.CaptionBGColor);
            captionBGRadius = config.GetBindable<int>(AXPlayerSetting.CaptionCornerRadius);
            smoothCaptionAnimation = config.GetBindable<bool>(AXPlayerSetting.SmoothCaptionAnimation);
            closedCaptionsBackdropBlur = config.GetBindable<bool>(AXPlayerSetting.ClosedCaptionsBackdropBlur);

            Add(srv3CaptionLayer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
            });

            Add(captionContainer = new BackdropBlurContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
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
                        Alpha = 0.5f,
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
            });

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

                // SRV3 displays bake the shadow option into their text, so they
                // need to be rebuilt when the background opacity changes.
                resetSrv3Captions();
            }, true);

            captionBGColor.BindValueChanged(colour =>
            {
                bg.Colour = colour.NewValue;
                applySrv3StyleToAll();
            }, true);

            captionBGRadius.BindValueChanged(corner =>
            {
                captionContainer.CornerRadius = new CornersInfo(corner.NewValue);
                applySrv3StyleToAll();
            }, true);

            closedCaptionsBackdropBlur.BindValueChanged(blur =>
            {
                captionContainer.BlurSigma = new osuTK.Vector2(blur.NewValue ? 10 : 0);
                applySrv3StyleToAll();
            }, true);

            smoothCaptionAnimation.BindValueChanged(enabled =>
            {
                if (enabled.NewValue)
                {
                    captionContainer.AutoSizeDuration = 350;
                } else
                {
                    captionContainer.AutoSizeDuration = 0;
                }

                applySrv3StyleToAll();
            }, true);

            captionFont.BindValueChanged(_ => updateCaptionFont(), true);

            if (customCaptionFonts != null)
            {
                // A font can only be used once it is in the caption-fonts folder, so a font that is added
                // later, or a selection that changes, has to rebuild the captions that are on screen.
                customCaptionFonts.Enabled.BindValueChanged(_ => updateCaptionFont());
                customCaptionFonts.SelectedFont.BindValueChanged(_ => updateCaptionFont());
                customCaptionFonts.AvailableFontsChanged += updateCaptionFont;
            }

            controlsVisibleState.BindValueChanged(v =>
            {
                UpdateControlsVisibleState(v.NewValue);
            }, true);

            bottomMargin.BindValueChanged(v =>
            {
                captionContainer.Margin = new MarginPadding
                {
                    Bottom = v.NewValue
                };

                updateSrv3DisplayPositions();
            }, true);
        }

        public void UpdateControlsVisibleState(bool state)
        {
            this.TransformBindableTo(bottomMargin, state ? 55 : 0, smoothCaptionAnimation.Value ? 500 : 0, state ? Easing.OutQuint : Easing.InOutCubic);
        }

        protected override void Update()
        {
            base.Update();

            bool hasTrack = captionTrack != null || srv3Track != null;

            if (!hasTrack)
            {
                Hide();
                return;
            }

            Show();

            try
            {
                double time = videoPlayer.VideoProgress.Value;

                if (srv3Track != null)
                {
                    // The legacy container is only used by YouTubeExplode tracks.
                    if (captionContainer.Alpha != 0)
                        captionContainer.Alpha = 0;

                    List<Srv3Caption> cues = srv3Track.GetByTime(TimeSpan.FromSeconds(time));

                    syncSrv3Captions(cues);
                    updateSrv3DisplayPositions();

                    if (cues.Count > 0)
                    {
                        if (smoothCaptionAnimation.Value)
                            srv3CaptionLayer.FadeIn(150, Easing.OutQuart);
                        else
                            srv3CaptionLayer.FadeIn();
                    }
                    else
                    {
                        if (smoothCaptionAnimation.Value)
                            srv3CaptionLayer.FadeOut(150, Easing.OutQuart);
                        else
                            srv3CaptionLayer.FadeOut();
                    }

                    return;
                }

                if (captionTrack != null)
                {
                    if (srv3CaptionLayer.Alpha != 0)
                        srv3CaptionLayer.Alpha = 0;


                    //fallback to bottom centere
                    captionContainer.Anchor = Anchor.BottomCentre;
                    captionContainer.Origin = Anchor.BottomCentre;
                    //also fallback text anchor to centere
                    spriteText.TextAnchor = Anchor.Centre;

                    bg.Colour = captionBGColor.Value;

                    var caption = captionTrack.TryGetByTime(TimeSpan.FromSeconds(time));

                    if (caption != null)
                    {
                        spriteText.Text = string.Empty;
                        spriteText.AddText(caption.Text, text =>
                        {
                            textCreationParameters?.Invoke(text);
                            shadowOptions?.Invoke(text);
                        });

                        if (smoothCaptionAnimation.Value)
                            captionContainer.FadeIn(150, Easing.OutQuart);
                        else
                            captionContainer.FadeIn();
                    }
                    else
                    {
                        if (smoothCaptionAnimation.Value)
                            captionContainer.FadeOut(150, Easing.OutQuart);
                        else
                            captionContainer.FadeOut();
                    }
                }
            }
            catch
            {
                Drawable target = srv3Track != null ? srv3CaptionLayer : captionContainer;

                if (target == null)
                    return;

                if (smoothCaptionAnimation.Value)
                    target.FadeOut(150, Easing.OutQuart);
                else
                    target.FadeOut();
            }
        }

        /// <summary>
        /// Keeps the rendered displays in sync with the cues that are active at
        /// the current playback position. Multiple cues may be active at once.
        /// </summary>
        private void syncSrv3Captions(IReadOnlyList<Srv3Caption> cues)
        {
            if (srv3CaptionLayer == null)
                return;

            if (sameSrv3Cues(cues))
                return;

            lastSrv3Cues = cues.ToArray();

            double fadeDuration = smoothCaptionAnimation.Value ? 150 : 0;

            // Retire displays whose cue is no longer active.
            foreach (Srv3Caption cue in srv3Displays.Keys.ToArray())
            {
                if (cues.Contains(cue))
                    continue;

                Srv3CaptionDisplay display = srv3Displays[cue];
                srv3Displays.Remove(cue);

                if (fadeDuration > 0)
                    display.Container.FadeOut(fadeDuration, Easing.OutQuart).Expire();
                else
                    display.Container.Expire();
            }

            // Create displays for newly active cues. Overlapping cues are kept
            // as independent displays so identical SRV3 positions overlap.
            foreach (Srv3Caption cue in cues)
            {
                if (srv3Displays.ContainsKey(cue))
                    continue;

                var display = new Srv3CaptionDisplay(cue, textCreationParameters);
                populateSrv3Display(display);
                applySrv3DisplayStyle(display);
                logSrv3CueWithoutBackground(cue, display);

                srv3Displays[cue] = display;
                srv3CaptionLayer.Add(display.Container);

                if (fadeDuration > 0)
                {
                    display.Container.Alpha = 0;
                    display.Container.FadeIn(fadeDuration, Easing.OutQuart);
                }
            }

            updateSrv3DisplayPositions();
        }

        private bool sameSrv3Cues(IReadOnlyList<Srv3Caption> cues)
        {
            if (lastSrv3Cues == null || lastSrv3Cues.Count != cues.Count)
                return false;

            for (int i = 0; i < cues.Count; i++)
            {
                if (!ReferenceEquals(lastSrv3Cues[i], cues[i]))
                    return false;
            }

            return true;
        }

        private void populateSrv3Display(Srv3CaptionDisplay display)
        {
            Srv3Caption cue = display.Cue;

            display.Text.Text = string.Empty;

            // ws controls paragraph alignment. p/ws values are zero-based.
            display.Text.TextAnchor = cue.TextAnchor;

            foreach (Srv3Span span in cue.Spans)
            {
                string text = span.Text;

                // SRV3 uses U+200B as an explicit zero-width separator between
                // styled spans in YouTube's generated timedtext.
                if (text.Length == 0)
                    continue;

                Action<SpriteText> configure = t =>
                {
                    textCreationParameters?.Invoke(t);
                    shadowOptions?.Invoke(t);

                    t.Colour = span.ForegroundColour;

                    // SRV3's bold flag can only be honoured by the caption fonts
                    // that ship a Bold instance. Other caption fonts ignore it.
                    bool bold = span.Bold && supportsSrv3Bold;

                    // SRV3's fs is a font-size family/index. sz is a percentage
                    // size multiplier. The test document uses 30/100/300.
                    if (span.SizeMultiplier.HasValue || bold)
                    {
                        float size = 24 * (span.SizeMultiplier ?? 1f);
                        t.Font = getSelectedFont(size, bold);
                    }

                    // osu!framework SpriteText exposes Shadow, which is the safest
                    // common rendering primitive for SRV3 edge information.
                    // We use the SRV3 edge colour as the shadow colour where an
                    // edge is requested.
                    if (span.EdgeColour.HasValue && span.EdgeType != 0)
                    {
                        t.Shadow = true;
                        t.ShadowColour = span.EdgeColour.Value;
                    }
                };

                // The pen background colours the caption's own background box.
                display.Text.AddText(text, configure);
            }
        }

        /// <summary>
        /// Reports a cue that is drawn without a background, so a caption whose pen asks for no
        /// background can be told apart from one that keeps the background of the player.
        /// </summary>
        private static void logSrv3CueWithoutBackground(Srv3Caption cue, Srv3CaptionDisplay display)
        {
            if (display.Background.Alpha > 0)
                return;

            string text = string.Concat(cue.Spans.Select(span => span.Text)).Trim();

            if (text.Length > 24)
                text = text[..24] + @"…";

            Logger.Log($@"The SRV3 cue at {cue.Start.TotalSeconds:0.###}s is drawn without a background "
                       + $@"('{text}', background {(cue.BackgroundColour is { } background ? background.ToHex() : @"none")}, "
                       + $@"box alpha {display.Background.Alpha:0.###}, blur {display.Container.BlurSigma.X:0}).",
                LoggingTarget.Runtime);
        }

        private void applySrv3DisplayStyle(Srv3CaptionDisplay display)
        {
            if (display.Cue.BackgroundColour is { } penBackground)
            {
                // A pen's bc/bo values define the caption background, including
                // its transparency, so they override the user's background opacity.
                display.Background.Colour = penBackground;
            }
            else
            {
                display.Background.Colour = captionBGColor.Value;
            }

            float alpha = backgroundAlpha(display.Cue.BackgroundColour, captionBGOpacity.Value);

            display.Background.Alpha = alpha;

            display.Container.CornerRadius = new CornersInfo(captionBGRadius.Value);

            // The container blurs the video behind the caption, which shows as a background of its own,
            // so a caption without a background must not blur it either.
            display.Container.BlurSigma = new osuTK.Vector2(closedCaptionsBackdropBlur.Value && alpha > 0 ? 10 : 0);
            display.Container.AutoSizeDuration = smoothCaptionAnimation.Value ? 350 : 0;
            display.Container.AutoSizeEasing = Easing.OutQuart;
        }

        /// <summary>
        /// The alpha the background box of a caption is drawn with.
        /// </summary>
        /// <remarks>
        /// A pen that states <c>bo="0"</c> asks for no background at all, so its box is hidden instead of
        /// being drawn with the transparency of its colour. A pen that states no background leaves the box
        /// to the background opacity of the player.
        /// </remarks>
        internal static float backgroundAlpha(Color4? penBackground, float configuredOpacity)
            => penBackground is { } background ? (background.A > 0 ? 1f : 0f) : configuredOpacity;

        /// <summary>
        /// The font the captions are drawn with, including the font the user added for them.
        /// </summary>
        internal FontUsage CaptionTextFont => getSelectedFont(24);

        /// <summary>
        /// The alpha of the background box of every rendered SRV3 caption, in rendering order.
        /// </summary>
        internal float[] Srv3BackgroundAlphas => srv3Displays.Values.Select(display => display.Background.Alpha).ToArray();

        /// <summary>
        /// The blur that the container of every rendered SRV3 caption applies to the video behind it, in
        /// rendering order. A caption without a background must not blur the video either.
        /// </summary>
        internal float[] Srv3BackgroundBlurs => srv3Displays.Values.Select(display => display.Container.BlurSigma.X).ToArray();

        private void applySrv3StyleToAll()
        {
            foreach (Srv3CaptionDisplay display in srv3Displays.Values)
                applySrv3DisplayStyle(display);
        }

        private void updateSrv3DisplayPositions()
        {
            if (srv3CaptionLayer == null)
                return;

            osuTK.Vector2 parentSize = srv3CaptionLayer.DrawSize;

            foreach (Srv3CaptionDisplay display in srv3Displays.Values)
            {
                Srv3CaptionPosition position = display.Cue.Position;
                osuTK.Vector2 size = display.Container.DrawSize;

                // ah/av address the anchor point selected by ap within the video
                // area. For example ap=4 (middle-centre) with ah=50 av=50 keeps
                // the caption's centre exactly in the middle of the video.
                float x = parentSize.X * position.HorizontalPercent / 100f - size.X * position.HorizontalFraction;
                float y = parentSize.Y * position.VerticalPercent / 100f - size.Y * position.VerticalFraction;

                if (position.MovesWithControls)
                    y -= bottomMargin.Value;

                x = Math.Clamp(x, 0, Math.Max(0, parentSize.X - size.X));
                y = Math.Clamp(y, 0, Math.Max(0, parentSize.Y - size.Y));

                display.Container.Position = new osuTK.Vector2(x, y);
            }
        }

        private void resetSrv3Captions()
        {
            lastSrv3Cues = null;

            foreach (Srv3CaptionDisplay display in srv3Displays.Values)
                display.Container.Expire();

            srv3Displays.Clear();
        }

        /// <summary>
        /// Whether the selected caption font provides a Bold instance that can be
        /// addressed through <see cref="FontUsage.Weight"/>.
        /// </summary>
        private bool supportsSrv3Bold => customFontFamily == null
                                         && captionFont.Value is CaptionFonts.Rubik
                                             or CaptionFonts.Pretendard
                                             or CaptionFonts.GoogleSansFlexRounded
                                             or CaptionFonts.GoogleSansFlex
                                             or CaptionFonts.Roboto;

        /// <summary>
        /// Resolves the font that was chosen for the captions, so the text of a caption can be created with it.
        /// </summary>
        /// <remarks>
        /// A font the user added is addressed by the name of its file, which is why the family is resolved only
        /// when the selection changes rather than for every caption that is drawn.
        /// </remarks>
        private void updateCaptionFont()
        {
            customFontFamily = customCaptionFonts?.GetSelectedFamily();

            textCreationParameters = t => t.Font = getSelectedFont(24);

            resetSrv3Captions();
        }

        private FontUsage getSelectedFont(float size, bool bold = false)
        {
            if (customFontFamily != null)
                return FontUsage.Default.With(customFontFamily, size);

            return getBuiltInFont(size, bold);
        }

        private FontUsage getBuiltInFont(float size, bool bold)
        {
            bool useBold = bold && supportsSrv3Bold;

            FontUsage withSizeAndWeight(FontUsage font)
                => useBold ? font.With(size: size, weight: "Bold") : font.With(size: size);

            switch (captionFont.Value)
            {
                case CaptionFonts.Rubik:
                    return withSizeAndWeight(AXPlayerApp.Fonts.Rubik);
                case CaptionFonts.Pretendard:
                    return withSizeAndWeight(AXPlayerApp.Fonts.Pretendard);
                case CaptionFonts.Ownglyph_PDH:
                    return AXPlayerApp.Fonts.Ownglyph_PDH.With(size: size);
                case CaptionFonts.Dovemayo_Gothic:
                    return AXPlayerApp.Fonts.Dovemayo_Gothic.With(size: size);
                case CaptionFonts.Griun_Mongtori:
                    return AXPlayerApp.Fonts.Griun_Mongtori.With(size: size);
                case CaptionFonts.Cafe24Syongsyong:
                    return AXPlayerApp.Fonts.Cafe24Syongsyong.With(size: size);
                case CaptionFonts.Roboto:
                    return withSizeAndWeight(AXPlayerApp.Fonts.Roboto);
                case CaptionFonts.DreamHeumulKR:
                    return AXPlayerApp.Fonts.DreamHeumulKR.With(size: size);
                case CaptionFonts.Hakgyoansim_ManitoR:
                    return AXPlayerApp.Fonts.Hakgyoansim_ManitoR.With(size: size);
                case CaptionFonts.OwnglyphDaisy:
                    return AXPlayerApp.Fonts.OwnglyphDaisy.With(size: size);
                case CaptionFonts.OwnglyphYuntaeng:
                    return AXPlayerApp.Fonts.OwnglyphYuntaeng.With(size: size);
                case CaptionFonts.GoogleSansFlexRounded:
                    return withSizeAndWeight(AXPlayerApp.Fonts.GoogleSansFlexRounded);
                case CaptionFonts.Hippie:
                    return AXPlayerApp.Fonts.Hippie.With(size: size);
                case CaptionFonts.GriunSobokhee:
                    return AXPlayerApp.Fonts.GriunSobokhee.With(size: size);
                case CaptionFonts.GriunNulnul:
                    return AXPlayerApp.Fonts.Griun_Nulnul.With(size: size);
                default:
                    return withSizeAndWeight(AXPlayerApp.Fonts.GoogleSansFlex);
            }
        }

        private sealed class Srv3CaptionDisplay
        {
            public Srv3Caption Cue { get; }

            public BackdropBlurContainer Container { get; }
            public Box Background { get; }
            public ProjectYomiTextFlowContainer Text { get; }

            public Srv3CaptionDisplay(Srv3Caption cue, Action<SpriteText> textCreationParameters)
            {
                Cue = cue;

                Container = new BackdropBlurContainer
                {
                    AutoSizeAxes = Axes.Both,
                    AutoSizeDuration = 350,
                    AutoSizeEasing = Easing.OutQuart,
                    Masking = true,
                    BlurSigma = new osuTK.Vector2(10),
                };

                Container.Add(Background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.5f,
                });

                Container.Add(Text = new ProjectYomiTextFlowContainer(textCreationParameters)
                {
                    TextAnchor = cue.TextAnchor,
                    AutoSizeAxes = Axes.Both,
                    Margin = new MarginPadding(4),
                });
            }
        }
    }

    /// <summary>
    /// Minimal SRV3 timedtext representation used by ClosedCaptionContainer.
    /// It intentionally follows the XML terminology used by YouTube's SRV3
    /// documents instead of converting the document to plain text.
    /// </summary>
    internal sealed class Srv3CaptionTrack
    {
        private readonly List<Srv3Caption> captions;

        /// <summary>
        /// The number of cues of the document.
        /// </summary>
        public int Count => captions.Count;

        /// <summary>
        /// Describes how the cues of the document state their background, which tells a cue without a
        /// background apart from one that only hides the background of the player.
        /// </summary>
        public string DescribeBackgrounds()
        {
            int transparent = captions.Count(cue => cue.BackgroundColour is { } background && background.A <= 0);
            int visible = captions.Count(cue => cue.BackgroundColour is { } background && background.A > 0);
            int player = captions.Count - transparent - visible;

            return $@"{captions.Count} cues ({transparent} without a background, {visible} with one, {player} with the background of the player)";
        }

        // Running maximum of caption end times. It allows GetByTime to stop
        // scanning backwards as soon as no earlier caption can still be active.
        private readonly TimeSpan[] latestEnds;

        private Srv3CaptionTrack(List<Srv3Caption> captions)
        {
            this.captions = captions;
            latestEnds = new TimeSpan[captions.Count];

            TimeSpan running = TimeSpan.MinValue;

            for (int i = 0; i < captions.Count; i++)
            {
                running = captions[i].End > running ? captions[i].End : running;
                latestEnds[i] = running;
            }
        }

        public static Srv3CaptionTrack Parse(string xml)
        {
            XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            XElement body = document.Root?.Element("body");

            if (body == null)
                return new Srv3CaptionTrack(new List<Srv3Caption>());

            Dictionary<int, Srv3Pen> pens = document.Root
                .Element("head")?
                .Elements("pen")
                .Select(Srv3Pen.Parse)
                .ToDictionary(p => p.Id)
                ?? new Dictionary<int, Srv3Pen>();

            Dictionary<int, Srv3Window> windows = document.Root
                .Element("head")?
                .Elements("ws")
                .Select(Srv3Window.Parse)
                .ToDictionary(w => w.Id)
                ?? new Dictionary<int, Srv3Window>();

            Dictionary<int, Srv3Position> positions = document.Root
                .Element("head")?
                .Elements("wp")
                .Select(Srv3Position.Parse)
                .ToDictionary(p => p.Id)
                ?? new Dictionary<int, Srv3Position>();

            var result = body.Elements("p")
                .Select(p => Srv3Caption.Parse(p, pens, windows, positions))
                .Where(c => c.Duration > TimeSpan.Zero)
                .OrderBy(c => c.Start)
                .ToList();

            return new Srv3CaptionTrack(result);
        }

        /// <summary>
        /// Returns every caption active at <paramref name="time"/>, ordered by
        /// start time. SRV3 documents can contain several paragraphs that share
        /// a start time (for example two speakers, or one caption per side of
        /// the video), so a single-cue lookup would drop valid captions.
        /// </summary>
        public List<Srv3Caption> GetByTime(TimeSpan time)
        {
            var result = new List<Srv3Caption>();

            if (captions.Count == 0)
                return result;

            // Find the last caption that starts at or before the requested time.
            int low = 0;
            int high = captions.Count - 1;
            int index = -1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);

                if (captions[middle].Start <= time)
                {
                    index = middle;
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            // Walk backwards while earlier captions could still be running.
            for (int i = index; i >= 0 && latestEnds[i] > time; i--)
            {
                Srv3Caption caption = captions[i];

                if (time >= caption.Start && time < caption.End)
                    result.Add(caption);
            }

            result.Sort((a, b) => a.Start.CompareTo(b.Start));
            return result;
        }
    }

    internal sealed class Srv3Caption
    {
        public TimeSpan Start { get; private set; }
        public TimeSpan Duration { get; private set; }
        public TimeSpan End => Start + Duration;
        public Srv3CaptionPosition Position { get; private set; } = Srv3CaptionPosition.Default;
        public Color4? BackgroundColour { get; private set; }
        public Anchor TextAnchor { get; private set; }
        public List<Srv3Span> Spans { get; } = new List<Srv3Span>();

        public static Srv3Caption Parse(
            XElement element,
            IReadOnlyDictionary<int, Srv3Pen> pens,
            IReadOnlyDictionary<int, Srv3Window> windows,
            IReadOnlyDictionary<int, Srv3Position> positions)
        {
            var result = new Srv3Caption
            {
                Start = milliseconds(element.Attribute("t")),
                Duration = milliseconds(element.Attribute("d")),
                Position = Srv3CaptionPosition.Default,
                TextAnchor = Anchor.Centre
            };

            int positionId = integer(element.Attribute("wp"), 0);

            if (positions.TryGetValue(positionId, out Srv3Position position))
                result.Position = position.Position;

            int windowId = integer(element.Attribute("ws"), 0);

            if (windows.TryGetValue(windowId, out Srv3Window window))
                result.TextAnchor = window.TextAnchor;

            int paragraphPen = integer(element.Attribute("p"), 0);
            Srv3Pen paragraphStyle = pens.TryGetValue(paragraphPen, out var pen)
                ? pen
                : Srv3Pen.Default;

            result.BackgroundColour = paragraphStyle.BackgroundColour;

            if (element.Elements("s").Any())
            {
                foreach (XElement span in element.Elements("s"))
                    addSpan(result, span, paragraphStyle, pens);
            }
            else
            {
                result.Spans.Add(new Srv3Span(element.Value, paragraphStyle));
            }

            // A single caption background box is used, so fall back to the first
            // visible span background when the paragraph pen has none.
            if (result.BackgroundColour is not { } paragraphBackground || paragraphBackground.A <= 0)
            {
                foreach (Srv3Span span in result.Spans)
                {
                    if (span.BackgroundColour is { } spanBackground && spanBackground.A > 0)
                    {
                        result.BackgroundColour = spanBackground;
                        break;
                    }
                }
            }

            return result;
        }

        private static void addSpan(
            Srv3Caption caption,
            XElement element,
            Srv3Pen paragraphStyle,
            IReadOnlyDictionary<int, Srv3Pen> pens)
        {
            int penId = integer(element.Attribute("p"), -1);

            Srv3Pen style = penId >= 0 && pens.TryGetValue(penId, out var spanPen)
                ? spanPen
                : paragraphStyle;

            string text = element.Value;

            // A span's t attribute is a timing offset within the cue. It is not
            // needed to display the current cue, but preserving the parsed value
            // here makes the representation ready for karaoke-style rendering.
            int offset = integer(element.Attribute("t"), 0);

            caption.Spans.Add(new Srv3Span(text, style, offset));
        }

        private static TimeSpan milliseconds(XAttribute attribute)
        {
            return TimeSpan.FromMilliseconds(integer(attribute, 0));
        }

        private static int integer(XAttribute attribute, int fallback)
        {
            if (attribute == null)
                return fallback;

            return int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }
    }

    internal sealed class Srv3Span
    {
        public string Text { get; }
        public Srv3Pen Pen { get; }
        public int OffsetMilliseconds { get; }

        public Color4 ForegroundColour => Pen.ForegroundColour;
        public Color4? BackgroundColour => Pen.BackgroundColour;
        public Color4? EdgeColour => Pen.EdgeColour;
        public int EdgeType => Pen.EdgeType;
        public float? SizeMultiplier => Pen.SizeMultiplier;
        public bool Bold => Pen.Bold;

        public Srv3Span(string text, Srv3Pen pen, int offsetMilliseconds = 0)
        {
            Text = text;
            Pen = pen;
            OffsetMilliseconds = offsetMilliseconds;
        }
    }

    internal sealed class Srv3Pen
    {
        public static Srv3Pen Default { get; } = new Srv3Pen();

        public int Id { get; private set; }
        public Color4 ForegroundColour { get; private set; } = Color4.White;

        /// <summary>
        /// The background of the pen, which SRV3 states as <c>bc</c> and <c>bo</c>. A pen that states
        /// neither has no background of its own, so its caption keeps the background of the player.
        /// </summary>
        public Color4? BackgroundColour { get; private set; }
        public Color4? EdgeColour { get; private set; }
        public int EdgeType { get; private set; }
        public float? SizeMultiplier { get; private set; }

        // SRV3 text decorations.
        public bool Bold { get; private set; }
        public bool Italic { get; private set; }
        public bool Underline { get; private set; }

        public static Srv3Pen Parse(XElement element)
        {
            var pen = new Srv3Pen
            {
                Id = integer(element.Attribute("id"), 0),
                ForegroundColour = parseColour(
                    element.Attribute("fc")?.Value,
                    opacity(element.Attribute("fo"), 255)),
                EdgeType = integer(element.Attribute("et"), 0),
                Bold = integer(element.Attribute("b"), 0) != 0,
                Italic = integer(element.Attribute("i"), 0) != 0,
                Underline = integer(element.Attribute("u"), 0) != 0
            };

            string background = element.Attribute("bc")?.Value;
            XAttribute backgroundOpacity = element.Attribute("bo");

            if (background != null)
            {
                pen.BackgroundColour = parseColour(background, opacity(backgroundOpacity, 255));
            }
            else if (backgroundOpacity != null)
            {
                // A pen that states only the opacity of its background keeps the default background
                // colour of SRV3, which is black, so bo="0" hides the background on its own.
                pen.BackgroundColour = parseColour("#000000", opacity(backgroundOpacity, 255));
            }

            string edge = element.Attribute("ec")?.Value;

            if (edge != null)
                pen.EdgeColour = parseColour(edge, 255);

            if (element.Attribute("sz") != null)
            {
                float percentage = floatValue(element.Attribute("sz"), 100);
                pen.SizeMultiplier = percentage / 100f;
            }

            return pen;
        }

        private static int opacity(XAttribute attribute, int fallback)
        {
            return Math.Clamp(integer(attribute, fallback), 0, 255);
        }

        private static Color4 parseColour(string value, int alpha)
        {
            float opacity = Math.Clamp(alpha, 0, 255) / 255f;

            if (string.IsNullOrWhiteSpace(value))
                return new Color4(1, 1, 1, opacity);

            string hex = value.Trim();

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                hex = hex.Substring(2);

            hex = hex.TrimStart('#');

            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                return new Color4(1, 1, 1, opacity);

            switch (hex.Length)
            {
                case 3:
                    // #RGB
                    return new Color4(
                        ((rgb >> 8) & 0xF) / 15f,
                        ((rgb >> 4) & 0xF) / 15f,
                        (rgb & 0xF) / 15f,
                        opacity);

                case 6:
                    // #RRGGBB, alpha comes from fo/bo.
                    return new Color4(
                        ((rgb >> 16) & 0xFF) / 255f,
                        ((rgb >> 8) & 0xFF) / 255f,
                        (rgb & 0xFF) / 255f,
                        opacity);

                case 8:
                    // #RRGGBBAA, combine the embedded alpha with fo/bo.
                    return new Color4(
                        ((rgb >> 24) & 0xFF) / 255f,
                        ((rgb >> 16) & 0xFF) / 255f,
                        ((rgb >> 8) & 0xFF) / 255f,
                        ((rgb & 0xFF) / 255f) * opacity);

                default:
                    return new Color4(1, 1, 1, opacity);
            }
        }

        private static int integer(XAttribute attribute, int fallback)
        {
            if (attribute == null)
                return fallback;

            return int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        private static float floatValue(XAttribute attribute, float fallback)
        {
            if (attribute == null)
                return fallback;

            return float.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                ? value
                : fallback;
        }
    }

    internal sealed class Srv3Window
    {
        public int Id { get; private set; }
        public Anchor TextAnchor { get; private set; } = Anchor.Centre;

        public static Srv3Window Parse(XElement element)
        {
            int justification = integer(element.Attribute("ju"), 0);

            return new Srv3Window
            {
                Id = integer(element.Attribute("id"), 0),
                TextAnchor = justification switch
                {
                    1 => Anchor.CentreLeft,
                    2 => Anchor.CentreRight,
                    _ => Anchor.Centre
                }
            };
        }

        private static int integer(XAttribute attribute, int fallback)
        {
            if (attribute == null)
                return fallback;

            return int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }
    }

    internal sealed class Srv3Position
    {
        public int Id { get; private set; }
        public Srv3CaptionPosition Position { get; private set; } = Srv3CaptionPosition.Default;

        public static Srv3Position Parse(XElement element)
        {
            // ap selects a point on a 3x3 anchor grid:
            // 0 = bottom-left, 1 = bottom-centre, 2 = bottom-right,
            // 3 = middle-left, 4 = centre, 5 = middle-right,
            // 6 = top-left, 7 = top-centre, 8 = top-right.
            int ap = integer(element.Attribute("ap"), 1);

            // ah/av are custom position percentages within the video area.
            // 0..100 (values outside the range are clamped).
            int ah = integer(element.Attribute("ah"), 50);
            int av = integer(element.Attribute("av"), 100);

            return new Srv3Position
            {
                Id = integer(element.Attribute("id"), 0),
                Position = new Srv3CaptionPosition(ah, av, ap)
            };
        }

        private static int integer(XAttribute attribute, int fallback)
        {
            if (attribute == null)
                return fallback;

            return int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }
    }

    /// <summary>
    /// A resolved SRV3 writing position. Unlike an <see cref="Anchor"/> it keeps
    /// the original percentages, so a caption can be placed at any custom
    /// coordinate rather than only on the nine built-in anchor points.
    /// </summary>
    internal readonly struct Srv3CaptionPosition : IEquatable<Srv3CaptionPosition>
    {
        public int HorizontalPercent { get; }
        public int VerticalPercent { get; }

        /// <summary>
        /// SRV3's ap value, a point on a 3x3 anchor grid:
        /// 0 = bottom-left, 1 = bottom-centre, 2 = bottom-right,
        /// 3 = middle-left, 4 = centre, 5 = middle-right,
        /// 6 = top-left, 7 = top-centre, 8 = top-right.
        /// </summary>
        public int AnchorPoint { get; }

        public static Srv3CaptionPosition Default { get; } = new Srv3CaptionPosition(50, 100, 1);

        /// <summary>
        /// 0 = left edge, 1 = centre, 2 = right edge.
        /// </summary>
        public int HorizontalAlignment => AnchorPoint % 3;

        public float HorizontalFraction => HorizontalAlignment * 0.5f;

        /// <summary>
        /// Fraction of the caption height to subtract from av: 1 = bottom edge,
        /// 0.5 = middle, 0 = top edge.
        /// </summary>
        public float VerticalFraction => (2 - (AnchorPoint / 3)) * 0.5f;

        /// <summary>
        /// Whether the controls overlay should push this caption upwards.
        /// </summary>
        public bool MovesWithControls => VerticalFraction >= 1f && VerticalPercent > 85;

        public Srv3CaptionPosition(int horizontalPercent, int verticalPercent, int anchorPoint)
        {
            HorizontalPercent = Math.Clamp(horizontalPercent, 0, 100);
            VerticalPercent = Math.Clamp(verticalPercent, 0, 100);
            AnchorPoint = Math.Clamp(anchorPoint, 0, 8);
        }

        public bool Equals(Srv3CaptionPosition other)
            => HorizontalPercent == other.HorizontalPercent
               && VerticalPercent == other.VerticalPercent
               && AnchorPoint == other.AnchorPoint;

        public override bool Equals(object obj)
            => obj is Srv3CaptionPosition other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(HorizontalPercent, VerticalPercent, AnchorPoint);
    }
}

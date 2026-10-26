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

        private Bindable<bool> captionEnabled;
        private Bindable<CaptionFonts> captionFont;
        private Container captionContainer;
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
        }

        private Bindable<bool> controlsVisibleState = null!, smoothCaptionAnimation = null!;
        private Action<SpriteText> textCreationParameters;
        private Action<SpriteText> shadowOptions;
        private Bindable<float> captionBGOpacity;
        private Bindable<Colour4> captionBGColor;
        private Bindable<int> captionBGRadius;
        private Box bg;

        [BackgroundDependencyLoader]
        private void load(AXPlayerConfigManager config, SessionStatics sessionStatics)
        {
            controlsVisibleState = sessionStatics.GetBindable<bool>(Static.IsControlVisible);
            captionEnabled = config.GetBindable<bool>(AXPlayerSetting.CaptionEnabled);
            captionFont = config.GetBindable<CaptionFonts>(AXPlayerSetting.CaptionFont);
            captionBGOpacity = config.GetBindable<float>(AXPlayerSetting.CaptionBGOpacity);
            captionBGColor = config.GetBindable<Colour4>(AXPlayerSetting.CaptionBGColor);
            captionBGRadius = config.GetBindable<int>(AXPlayerSetting.CaptionCornerRadius);
            smoothCaptionAnimation = config.GetBindable<bool>(AXPlayerSetting.SmoothCaptionAnimation);

            Add(captionContainer = new Container
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
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
            }, true);

            captionBGColor.BindValueChanged(colour =>
            {
                bg.Colour = colour.NewValue;
            }, true);

            captionBGRadius.BindValueChanged(corner =>
            {
                captionContainer.CornerRadius = new CornersInfo(corner.NewValue);
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
            }, true);

            captionFont.BindValueChanged(v =>
            {
                switch (v.NewValue)
                {
                    case CaptionFonts.GoogleSansFlex:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.GoogleSansFlex.With(size: 24);
                        break;
                    case CaptionFonts.Rubik:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Rubik.With(size: 24);
                        break;
                    case CaptionFonts.Pretendard:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Pretendard.With(size: 24);
                        break;
                    case CaptionFonts.Hungeul:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Hungeul.With(size: 24);
                        break;
                    case CaptionFonts.Ownglyph_PDH:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Ownglyph_PDH.With(size: 24);
                        break;
                    case CaptionFonts.Dovemayo_Gothic:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Dovemayo_Gothic.With(size: 24);
                        break;
                    case CaptionFonts.Griun_Mongtori:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Griun_Mongtori.With(size: 24);
                        break;
                    case CaptionFonts.ONE_Mobile_POP:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.ONE_Mobile_POP.With(size: 24);
                        break;
                    case CaptionFonts.Cafe24Syongsyong:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Cafe24Syongsyong.With(size: 24);
                        break;
                    case CaptionFonts.Roboto:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Roboto.With(size: 24);
                        break;
                    case CaptionFonts.DreamHeumulKR:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.DreamHeumulKR.With(size: 24);
                        break;
                    case CaptionFonts.Hakgyoansim_ManitoR:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Hakgyoansim_ManitoR.With(size: 24);
                        break;
                    case CaptionFonts.OwnglyphDaisy:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.OwnglyphDaisy.With(size: 24);
                        break;
                    case CaptionFonts.OwnglyphYuntaeng:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.OwnglyphYuntaeng.With(size: 24);
                        break;
                    case CaptionFonts.GoogleSansFlexRounded:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.GoogleSansFlexRounded.With(size: 24);
                        break;
                    case CaptionFonts.Hippie:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.Hippie.With(size: 24);
                        break;
                    default:
                        textCreationParameters = t => t.Font = AXPlayerApp.Fonts.GoogleSansFlex.With(size: 24);
                        break;
                }
            }, true);

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
                    Srv3Caption cue = srv3Track.TryGetByTime(TimeSpan.FromSeconds(time));

                    if (cue != null)
                    {
                        renderSrv3Cue(cue);

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

                    return;
                }

                if (captionTrack != null)
                {
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
                if (smoothCaptionAnimation.Value)
                    captionContainer.FadeOut(150, Easing.OutQuart);
                else
                    captionContainer.FadeOut();
            }
        }

        private void renderSrv3Cue(Srv3Caption cue)
        {
            spriteText.Text = string.Empty;

            // SRV3 wp uses a 3x3 anchor grid. wp 0 is the default position.
            captionContainer.Anchor = cue.Anchor;
            captionContainer.Origin = cue.Anchor;

            // ws controls paragraph alignment. p/ws values are zero-based.
            spriteText.TextAnchor = cue.TextAnchor;

            foreach (Srv3Span span in cue.Spans)
            {
                string text = span.Text;

                if (span.BackgroundColour.HasValue)
                    bg.Colour = span.BackgroundColour.Value;

                // SRV3 uses U+200B as an explicit zero-width separator between
                // styled spans in YouTube's generated timedtext.
                if (text.Length == 0)
                    continue;

                spriteText.AddText(text, t =>
                {
                    textCreationParameters?.Invoke(t);
                    shadowOptions?.Invoke(t);

                    t.Colour = span.ForegroundColour;

                    // SRV3's fs is a font-size family/index. sz is a percentage
                    // size multiplier. The test document uses 30/100/300.
                    if (span.SizeMultiplier.HasValue)
                    {
                        float size = 24 * span.SizeMultiplier.Value;
                        t.Font = getSelectedFont(size);
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
                });
            }
        }

        private FontUsage getSelectedFont(float size)
        {
            switch (captionFont.Value)
            {
                case CaptionFonts.Rubik:
                    return AXPlayerApp.Fonts.Rubik.With(size: size);
                case CaptionFonts.Pretendard:
                    return AXPlayerApp.Fonts.Pretendard.With(size: size);
                case CaptionFonts.Hungeul:
                    return AXPlayerApp.Fonts.Hungeul.With(size: size);
                case CaptionFonts.Ownglyph_PDH:
                    return AXPlayerApp.Fonts.Ownglyph_PDH.With(size: size);
                case CaptionFonts.Dovemayo_Gothic:
                    return AXPlayerApp.Fonts.Dovemayo_Gothic.With(size: size);
                case CaptionFonts.Griun_Mongtori:
                    return AXPlayerApp.Fonts.Griun_Mongtori.With(size: size);
                case CaptionFonts.ONE_Mobile_POP:
                    return AXPlayerApp.Fonts.ONE_Mobile_POP.With(size: size);
                case CaptionFonts.Cafe24Syongsyong:
                    return AXPlayerApp.Fonts.Cafe24Syongsyong.With(size: size);
                case CaptionFonts.Roboto:
                    return AXPlayerApp.Fonts.Roboto.With(size: size);
                case CaptionFonts.DreamHeumulKR:
                    return AXPlayerApp.Fonts.DreamHeumulKR.With(size: size);
                case CaptionFonts.Hakgyoansim_ManitoR:
                    return AXPlayerApp.Fonts.Hakgyoansim_ManitoR.With(size: size);
                case CaptionFonts.OwnglyphDaisy:
                    return AXPlayerApp.Fonts.OwnglyphDaisy.With(size: size);
                case CaptionFonts.OwnglyphYuntaeng:
                    return AXPlayerApp.Fonts.OwnglyphYuntaeng.With(size: size);
                case CaptionFonts.GoogleSansFlexRounded:
                    return AXPlayerApp.Fonts.GoogleSansFlexRounded.With(size: size);
                case CaptionFonts.Hippie:
                    return AXPlayerApp.Fonts.Hippie.With(size: size);
                default:
                    return AXPlayerApp.Fonts.GoogleSansFlex.With(size: size);
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

        private Srv3CaptionTrack(List<Srv3Caption> captions)
        {
            this.captions = captions;
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

        public Srv3Caption TryGetByTime(TimeSpan time)
        {
            // Binary search keeps per-frame caption lookup cheap.
            int low = 0;
            int high = captions.Count - 1;

            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                Srv3Caption caption = captions[middle];

                if (time < caption.Start)
                    high = middle - 1;
                else if (time >= caption.End)
                    low = middle + 1;
                else
                    return caption;
            }

            return null;
        }
    }

    internal sealed class Srv3Caption
    {
        public TimeSpan Start { get; private set; }
        public TimeSpan Duration { get; private set; }
        public TimeSpan End => Start + Duration;
        public Anchor Anchor { get; private set; }
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
                Anchor = Anchor.BottomCentre,
                TextAnchor = Anchor.Centre
            };

            int positionId = integer(element.Attribute("wp"), 0);

            if (positions.TryGetValue(positionId, out Srv3Position position))
                result.Anchor = position.Anchor;

            int windowId = integer(element.Attribute("ws"), 0);

            if (windows.TryGetValue(windowId, out Srv3Window window))
                result.TextAnchor = window.TextAnchor;

            int paragraphPen = integer(element.Attribute("p"), 0);
            Srv3Pen paragraphStyle = pens.TryGetValue(paragraphPen, out var pen)
                ? pen
                : Srv3Pen.Default;

            if (element.Elements("s").Any())
            {
                foreach (XElement span in element.Elements("s"))
                    addSpan(result, span, paragraphStyle, pens);
            }
            else
            {
                result.Spans.Add(new Srv3Span(element.Value, paragraphStyle));
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

            if (background != null)
                pen.BackgroundColour = parseColour(background, opacity(element.Attribute("bo"), 255));

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
            if (string.IsNullOrWhiteSpace(value))
                return new Color4(1, 1, 1, alpha / 255f);

            string hex = value.TrimStart('#');

            if (hex.Length != 6)
                return new Color4(1, 1, 1, alpha / 255f);

            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
                return new Color4(1, 1, 1, alpha / 255f);

            byte r = (byte)(rgb >> 16);
            byte g = (byte)(rgb >> 8);
            byte b = (byte)rgb;

            return new Color4(r / 255f, g / 255f, b / 255f, alpha / 255f);
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
        public Anchor Anchor { get; private set; } = Anchor.BottomCentre;

        public static Srv3Position Parse(XElement element)
        {
            int ap = integer(element.Attribute("ap"), 1);
            int ah = integer(element.Attribute("ah"), 50);
            int av = integer(element.Attribute("av"), 100);

            return new Srv3Position
            {
                Id = integer(element.Attribute("id"), 0),
                Anchor = toAnchor(ap, ah, av)
            };
        }

        private static Anchor toAnchor(int ap, int ah, int av)
        {
            // In SRV3, ah/av are the horizontal/vertical position percentages.
            // The supplied test document demonstrates, for example, that
            // ap=1 + av=40 is top-centre while ap=1 + av=70 is middle-centre.
            bool left = ah < 33;
            bool right = ah > 66;
            bool top = av < 55;
            bool bottom = av > 85;

            if (top)
                return left ? Anchor.TopLeft : right ? Anchor.TopRight : Anchor.TopCentre;

            if (bottom)
                return left ? Anchor.BottomLeft : right ? Anchor.BottomRight : Anchor.BottomCentre;

            return left ? Anchor.CentreLeft : right ? Anchor.CentreRight : Anchor.Centre;
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
}

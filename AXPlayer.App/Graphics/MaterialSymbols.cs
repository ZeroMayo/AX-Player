// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;

namespace AXPlayer.App.Graphics
{
    /// <summary>
    /// Icon catalogue backed by the bundled Material Symbols Rounded variable font.
    /// </summary>
    /// <remarks>
    /// Property names are kept identical to the FontAwesome icon names they replace, so call sites only
    /// need to swap the declaring type. The filled/outlined distinction of FontAwesome is expressed through
    /// the font's FILL axis, which is registered as two instances in <see cref="Config.AXPlayerFontLoader"/>.
    /// Glyphs are looked up by <see cref="IconUsage.FontName"/>, composed of <see cref="FONT_FAMILY"/> and the instance name.
    /// </remarks>
    public static class MaterialSymbols
    {
        /// <summary>
        /// The font family name, derived from the <c>Fonts/MaterialSymbolsRounded/MaterialSymbolsRounded.ttf</c> resource.
        /// </summary>
        public const string FONT_FAMILY = "MaterialSymbolsRounded";

        /// <summary>
        /// The font instance with the <c>FILL</c> axis set to 1, matching FontAwesome's solid icon set.
        /// </summary>
        public const string FILLED_WEIGHT = "Filled";

        /// <summary>
        /// The font instance with the <c>FILL</c> axis set to 0, matching FontAwesome's regular (outline) icon set.
        /// </summary>
        public const string OUTLINED_WEIGHT = "Outlined";

        /// <summary>
        /// Filled Material Symbols glyphs.
        /// </summary>
        public static class Filled
        {
            public static IconUsage ArrowDown => new IconUsage('\ue5db', FONT_FAMILY, FILLED_WEIGHT); // arrow_downward
            public static IconUsage Bars => new IconUsage('\ue5d2', FONT_FAMILY, FILLED_WEIGHT); // menu
            public static IconUsage Bell => new IconUsage('\ue7f4', FONT_FAMILY, FILLED_WEIGHT); // notifications
            public static IconUsage Bolt => new IconUsage('\uea0b', FONT_FAMILY, FILLED_WEIGHT); // bolt
            public static IconUsage Bookmark => new IconUsage('\ue866', FONT_FAMILY, FILLED_WEIGHT); // bookmark
            public static IconUsage Box => new IconUsage('\ue3c6', FONT_FAMILY, FILLED_WEIGHT); // crop_square
            public static IconUsage Bug => new IconUsage('\ue868', FONT_FAMILY, FILLED_WEIGHT); // bug_report
            public static IconUsage Camera => new IconUsage('\ue412', FONT_FAMILY, FILLED_WEIGHT); // photo_camera
            public static IconUsage CheckCircle => new IconUsage('\ue86c', FONT_FAMILY, FILLED_WEIGHT); // check_circle
            public static IconUsage ChevronDown => new IconUsage('\ue5cf', FONT_FAMILY, FILLED_WEIGHT); // expand_more
            public static IconUsage ChevronLeft => new IconUsage('\ue5cb', FONT_FAMILY, FILLED_WEIGHT); // chevron_left
            public static IconUsage ChevronRight => new IconUsage('\ue5cc', FONT_FAMILY, FILLED_WEIGHT); // chevron_right
            public static IconUsage ChevronUp => new IconUsage('\ue5ce', FONT_FAMILY, FILLED_WEIGHT); // expand_less
            public static IconUsage ClosedCaptioning => new IconUsage('\ue01c', FONT_FAMILY, FILLED_WEIGHT); // closed_caption
            public static IconUsage Cog => new IconUsage('\ue8b8', FONT_FAMILY, FILLED_WEIGHT); // settings
            public static IconUsage CommentAlt => new IconUsage('\ue0ca', FONT_FAMILY, FILLED_WEIGHT); // chat_bubble
            public static IconUsage EllipsisH => new IconUsage('\ue5d3', FONT_FAMILY, FILLED_WEIGHT); // more_horiz
            public static IconUsage ExchangeAlt => new IconUsage('\ue8d4', FONT_FAMILY, FILLED_WEIGHT); // swap_horiz
            public static IconUsage ExclamationTriangle => new IconUsage('\ue002', FONT_FAMILY, FILLED_WEIGHT); // warning
            public static IconUsage Eye => new IconUsage('\ue8f4', FONT_FAMILY, FILLED_WEIGHT); // visibility
            public static IconUsage FastBackward => new IconUsage('\ue020', FONT_FAMILY, FILLED_WEIGHT); // fast_rewind
            public static IconUsage FastForward => new IconUsage('\ue01f', FONT_FAMILY, FILLED_WEIGHT); // fast_forward
            public static IconUsage FileAudio => new IconUsage('\ueb82', FONT_FAMILY, FILLED_WEIGHT); // audio_file
            public static IconUsage Flag => new IconUsage('\ue153', FONT_FAMILY, FILLED_WEIGHT); // flag
            public static IconUsage Folder => new IconUsage('\ue2c7', FONT_FAMILY, FILLED_WEIGHT); // folder
            public static IconUsage Font => new IconUsage('\ue167', FONT_FAMILY, FILLED_WEIGHT); // font_download
            public static IconUsage Globe => new IconUsage('\ue80b', FONT_FAMILY, FILLED_WEIGHT); // public
            public static IconUsage History => new IconUsage('\ue889', FONT_FAMILY, FILLED_WEIGHT); // history
            public static IconUsage InfoCircle => new IconUsage('\ue88e', FONT_FAMILY, FILLED_WEIGHT); // info
            public static IconUsage Keyboard => new IconUsage('\ue312', FONT_FAMILY, FILLED_WEIGHT); // keyboard
            public static IconUsage Language => new IconUsage('\ue894', FONT_FAMILY, FILLED_WEIGHT); // language
            public static IconUsage List => new IconUsage('\ue896', FONT_FAMILY, FILLED_WEIGHT); // list
            public static IconUsage Pin => new IconUsage('\ue6aa', FONT_FAMILY, FILLED_WEIGHT); // keep
            public static IconUsage Microphone => new IconUsage('\ue029', FONT_FAMILY, FILLED_WEIGHT); // mic
            public static IconUsage MinusCircle => new IconUsage('\ue15c', FONT_FAMILY, FILLED_WEIGHT); // remove_circle
            public static IconUsage PaintBrush => new IconUsage('\ue3ae', FONT_FAMILY, FILLED_WEIGHT); // brush
            public static IconUsage Palette => new IconUsage('\ue40a', FONT_FAMILY, FILLED_WEIGHT); // palette
            public static IconUsage PaperPlane => new IconUsage('\ue163', FONT_FAMILY, FILLED_WEIGHT); // send
            public static IconUsage Pause => new IconUsage('\ue034', FONT_FAMILY, FILLED_WEIGHT); // pause
            public static IconUsage Play => new IconUsage('\ue037', FONT_FAMILY, FILLED_WEIGHT); // play_arrow
            public static IconUsage Plus => new IconUsage('\ue145', FONT_FAMILY, FILLED_WEIGHT); // add
            public static IconUsage QuestionCircle => new IconUsage('\ue887', FONT_FAMILY, FILLED_WEIGHT); // help
            public static IconUsage RulerHorizontal => new IconUsage('\ue41c', FONT_FAMILY, FILLED_WEIGHT); // straighten
            public static IconUsage RulerVertical => new IconUsage('\uea16', FONT_FAMILY, FILLED_WEIGHT); // height
            public static IconUsage Save => new IconUsage('\ue161', FONT_FAMILY, FILLED_WEIGHT); // save
            public static IconUsage Search => new IconUsage('\ue8b6', FONT_FAMILY, FILLED_WEIGHT); // search
            public static IconUsage Shapes => new IconUsage('\ue574', FONT_FAMILY, FILLED_WEIGHT); // category
            public static IconUsage SignInAlt => new IconUsage('\uea77', FONT_FAMILY, FILLED_WEIGHT); // login
            public static IconUsage SignOutAlt => new IconUsage('\ue9ba', FONT_FAMILY, FILLED_WEIGHT); // logout
            public static IconUsage SlidersH => new IconUsage('\ue429', FONT_FAMILY, FILLED_WEIGHT); // tune
            public static IconUsage Star => new IconUsage('\ue838', FONT_FAMILY, FILLED_WEIGHT); // star
            public static IconUsage Stopwatch => new IconUsage('\ue425', FONT_FAMILY, FILLED_WEIGHT); // timer
            public static IconUsage Sun => new IconUsage('\ue518', FONT_FAMILY, FILLED_WEIGHT); // light_mode
            public static IconUsage Sync => new IconUsage('\ue627', FONT_FAMILY, FILLED_WEIGHT); // sync
            public static IconUsage SyncAlt => new IconUsage('\ue8d4', FONT_FAMILY, FILLED_WEIGHT); // swap_horiz
            public static IconUsage TachometerAlt => new IconUsage('\ue9e4', FONT_FAMILY, FILLED_WEIGHT); // speed
            public static IconUsage ThumbsDown => new IconUsage('\ue8db', FONT_FAMILY, FILLED_WEIGHT); // thumb_down
            public static IconUsage ThumbsUp => new IconUsage('\ue8dc', FONT_FAMILY, FILLED_WEIGHT); // thumb_up
            public static IconUsage Times => new IconUsage('\ue5cd', FONT_FAMILY, FILLED_WEIGHT); // close
            public static IconUsage TimesCircle => new IconUsage('\ue5c9', FONT_FAMILY, FILLED_WEIGHT); // cancel
            public static IconUsage Undo => new IconUsage('\ue166', FONT_FAMILY, FILLED_WEIGHT); // undo
            public static IconUsage User => new IconUsage('\ue7fd', FONT_FAMILY, FILLED_WEIGHT); // person
            public static IconUsage Video => new IconUsage('\ue04b', FONT_FAMILY, FILLED_WEIGHT); // videocam
            public static IconUsage VolumeDown => new IconUsage('\ue04d', FONT_FAMILY, FILLED_WEIGHT); // volume_down
            public static IconUsage VolumeMute => new IconUsage('\ue04f', FONT_FAMILY, FILLED_WEIGHT); // volume_off
            public static IconUsage VolumeUp => new IconUsage('\ue050', FONT_FAMILY, FILLED_WEIGHT); // volume_up
            public static IconUsage Wind => new IconUsage('\uefd8', FONT_FAMILY, FILLED_WEIGHT); // air
            public static IconUsage WindowMaximize => new IconUsage('\ue30c', FONT_FAMILY, FILLED_WEIGHT); // desktop_windows
        }

        /// <summary>
        /// Outlined Material Symbols glyphs.
        /// </summary>
        public static class Outlined
        {
            public static IconUsage Bookmark => new IconUsage('\ue866', FONT_FAMILY, OUTLINED_WEIGHT); // bookmark
            public static IconUsage Clipboard => new IconUsage('\ue14f', FONT_FAMILY, OUTLINED_WEIGHT); // content_paste
            public static IconUsage ClosedCaptioning => new IconUsage('\ue01c', FONT_FAMILY, OUTLINED_WEIGHT); // closed_caption
            public static IconUsage Comment => new IconUsage('\ue0b9', FONT_FAMILY, OUTLINED_WEIGHT); // comment
            public static IconUsage CommentAlt => new IconUsage('\ue0ca', FONT_FAMILY, OUTLINED_WEIGHT); // chat_bubble
            public static IconUsage Edit => new IconUsage('\ue3c9', FONT_FAMILY, OUTLINED_WEIGHT); // edit
            public static IconUsage FolderOpen => new IconUsage('\ue2c8', FONT_FAMILY, OUTLINED_WEIGHT); // folder_open
            public static IconUsage Sun => new IconUsage('\ue518', FONT_FAMILY, OUTLINED_WEIGHT); // light_mode
            public static IconUsage WindowClose => new IconUsage('\ue5cd', FONT_FAMILY, OUTLINED_WEIGHT); // close
            public static IconUsage WindowMaximize => new IconUsage('\ue30c', FONT_FAMILY, OUTLINED_WEIGHT); // desktop_windows
            public static IconUsage WindowMinimize => new IconUsage('\ue931', FONT_FAMILY, OUTLINED_WEIGHT); // minimize
        }
    }
}

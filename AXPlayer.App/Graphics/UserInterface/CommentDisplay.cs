// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using Humanizer;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.Sprites;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osuTK;

namespace AXPlayer.App.Graphics.UserInterface
{
    public partial class CommentDisplay : CompositeDrawable
    {
        private ProfileImage profileImage = null!;
        private ProjectYomiTextFlowContainer channelName = null!;
        private LinkFlowContainer commentText = null!;
        public Action<VideoMetadataDisplay> ClickEvent = null!;
        private ProjectYomiSpriteText likeCount = null!, replyCount = null!, translateToText = null!;
        private RoundedButtonContainer translateButton = null!;

        public Action<double> TimestampClicked;

        private AXPlayerLoadingLayer loading;

        [Resolved]
        private YouTubeAPI api { get; set; } = null!;

        [Resolved]
        private AXPlayerAppBase app { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private AXPlayerConfigManager appConfig { get; set; } = null!;

        private Bindable<Localisation.Language> uiLanguage = null!;
        private Bindable<UsernameDisplayMode> usernameDisplayMode = null!;

        private Container contents;

        public CommentDisplay(CommentThread comment, Comment replyToComments = null)
        {
            commentData = comment;
            commentData2 = replyToComments;
            AutoSizeAxes = Axes.Y;
            Task.Run(async () =>
            {
                UpdateData();
            });
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider overlayColourProvider)
        {
            uiLanguage = app.CurrentLanguage.GetBoundCopy();
            usernameDisplayMode = appConfig.GetBindable<UsernameDisplayMode>(AXPlayerSetting.UsernameDisplayMode);

            InternalChildren = new Drawable[]
            {
                contents = new Container
                {
                    Padding = new MarginPadding() { Left = commentData2 != null ? 48 : 0 },
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            CornerRadius = AXPlayerApp.UI_CORNER_RADIUS,
                            Masking = true,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = overlayColourProvider.Background4,
                                    Alpha = 0.7f,
                                },
                                new Container {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding(7),
                                    Children = new Drawable[]
                                    {
                                        profileImage = new ProfileImage(35),
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Padding = new MarginPadding
                                            {
                                                Top = 5,
                                                Left = 42,
                                                Right = 5,
                                            },
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 2),
                                            Children = new Drawable[]
                                            {
                                                channelName = new ProjectYomiTextFlowContainer(f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Bold"))
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Colour = overlayColourProvider.Background1,
                                                },
                                                commentText = new LinkFlowContainer(font => font.Font = AXPlayerApp.DefaultFont.With(size: 17, weight: "Regular"))
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Colour = overlayColourProvider.Content2,
                                                },
                                                translateButton = new RoundedButtonContainer
                                                {
                                                    AutoSizeAxes = Axes.X,
                                                    Height = 27,
                                                    CornerRadius = 12,
                                                    Masking = true,
                                                    AlwaysPresent = true,
                                                    Position = new Vector2(0, 35),
                                                    ClickAction = f => translateComment(),
                                                    Children = new Drawable[]
                                                    {
                                                        new Container
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            CornerRadius = 12,
                                                            Child = new Box
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                Colour = overlayColourProvider.Background3,
                                                                Alpha = 0.7f,
                                                            },
                                                        },
                                                        new FillFlowContainer
                                                        {
                                                            AutoSizeAxes = Axes.X,
                                                            RelativeSizeAxes = Axes.Y,
                                                            Direction = FillDirection.Horizontal,
                                                            Spacing = new Vector2(4, 0),
                                                            Padding = new MarginPadding(8),
                                                            Children = new Drawable[]
                                                            {
                                                                translateToText = new ProjectYomiSpriteText
                                                                {
                                                                    Colour = overlayColourProvider.Content2,
                                                                    Font = AXPlayerApp.DefaultFont.With(size: 13.5f, weight: "Regular"),
                                                                },
                                                            }
                                                        }
                                                    }
                                                },
                                                new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(4, 0),
                                                    Children = new Drawable[]
                                                    {
                                                        new Container
                                                        {
                                                            AutoSizeAxes = Axes.X,
                                                            Height = 27,
                                                            CornerRadius = 27 / 2,
                                                            Masking = true,
                                                            AlwaysPresent = true,
                                                            Children = new Drawable[]
                                                            {
                                                                new Container
                                                                {
                                                                    RelativeSizeAxes = Axes.Both,
                                                                    CornerRadius = 27 / 2,
                                                                    Child = new Box
                                                                    {
                                                                        RelativeSizeAxes = Axes.Both,
                                                                        Colour = overlayColourProvider.Background3,
                                                                        Alpha = 0.7f,
                                                                    },
                                                                },
                                                                new FillFlowContainer
                                                                {
                                                                    AutoSizeAxes = Axes.X,
                                                                    RelativeSizeAxes = Axes.Y,
                                                                    Direction = FillDirection.Horizontal,
                                                                    Spacing = new Vector2(4, 0),
                                                                    Padding = new MarginPadding(8),
                                                                    Children = new Drawable[]
                                                                    {
                                                                        new SpriteIcon
                                                                        {
                                                                            Width = 12,
                                                                            Height = 12,
                                                                            Icon = FontAwesome.Solid.ThumbsUp,
                                                                            Colour = overlayColourProvider.Content2,
                                                                        },
                                                                        likeCount = new ProjectYomiSpriteText
                                                                        {
                                                                            Colour = overlayColourProvider.Content2,
                                                                            Font = AXPlayerApp.DefaultFont.With(size: 13.5f, weight: "SemiBold"),
                                                                        },
                                                                    }
                                                                }
                                                            }
                                                        },
                                                        replyCountBox = new Container
                                                        {
                                                            AutoSizeAxes = Axes.X,
                                                            Height = 27,
                                                            CornerRadius = 27 / 2,
                                                            Masking = true,
                                                            AlwaysPresent = true,
                                                            Children = new Drawable[]
                                                            {
                                                                new Container
                                                                {
                                                                    RelativeSizeAxes = Axes.Both,
                                                                    CornerRadius = 27 / 2,
                                                                    Child = new Box
                                                                    {
                                                                        RelativeSizeAxes = Axes.Both,
                                                                        Colour = overlayColourProvider.Background3,
                                                                        Alpha = 0.7f,
                                                                    },
                                                                },
                                                                new FillFlowContainer
                                                                {
                                                                    AutoSizeAxes = Axes.X,
                                                                    RelativeSizeAxes = Axes.Y,
                                                                    Direction = FillDirection.Horizontal,
                                                                    Spacing = new Vector2(4, 0),
                                                                    Padding = new MarginPadding(8),
                                                                    Children = new Drawable[]
                                                                    {
                                                                        new SpriteIcon
                                                                        {
                                                                            Width = 12,
                                                                            Height = 12,
                                                                            Icon = FontAwesome.Solid.CommentAlt,
                                                                            Colour = overlayColourProvider.Content2,
                                                                        },
                                                                        replyCount = new ProjectYomiSpriteText
                                                                        {
                                                                            Colour = overlayColourProvider.Content2,
                                                                            Font = AXPlayerApp.DefaultFont.With(size: 13.5f, weight: "SemiBold"),
                                                                        },
                                                                    }
                                                                }
                                                            }
                                                        },
                                                    }
                                                },
                                            }
                                        }
                                    }
                                },
                                loading = new AXPlayerLoadingLayer(true, false, true),
                            }
                        }
                    }
                },
            };
        }

        private bool translated;

        private CommentThread commentData;
        private Comment commentData2;

        private void translateComment()
        {
            if (translated == false)
            {
                Task.Run(async () => {
                    Schedule(async () =>
                    {
                        translateToText.Text = AXPlayerStrings.Translating;
                        setText(await translate.TranslateAsync(commentData2 != null ? commentData2.Snippet.TextOriginal : commentData.Snippet.TopLevelComment.Snippet.TextOriginal, GoogleTranslateLanguage.auto));
                        translateToText.Text = AXPlayerStrings.TranslateViewOriginal;
                    });
                });
                translated = true;
            }
            else
            {
                Schedule(() =>
                {
                    setText(commentData2 != null ? commentData2.Snippet.TextOriginal : commentData.Snippet.TopLevelComment.Snippet.TextOriginal);
                    translateToText.Text = AXPlayerStrings.TranslateTo(app.CurrentLanguage.Value.GetLocalisableDescription());
                });
                translated = false;
            }
        }

        [Resolved]
        private GoogleTranslate translate { get; set; } = null!;

        private Container replyCountBox;

        private void setText(string text)
        {
            commentText.Text = "";
            List<YouTubeDescriptionTextToken> list = AXPlayerDescriptionParser.Parse(text);

            foreach (YouTubeDescriptionTextToken item in list)
            {
                switch (item.Type)
                {
                    case YouTubeDescriptionTokenType.Text:
                        commentText.AddText(item.Value);
                        break;
                    case YouTubeDescriptionTokenType.Url:
                        commentText.AddArbitraryDrawable(new UrlRedirectDisplay(item.Value));
                        break;
                    case YouTubeDescriptionTokenType.Mention:
                        commentText.AddLink(item.Value, $"https://www.youtube.com/{item.Value}", AXPlayerStrings.YouTubeHandleViewProfile(item.Value), s => s.Font = s.Font.With(weight: "Bold"));
                        break;
                    case YouTubeDescriptionTokenType.Hashtag:
                        commentText.AddLink(item.Value, $"https://www.youtube.com/hashtag/{item.Value.Replace("#", string.Empty)}", AXPlayerStrings.Hashtag(item.Value), s => s.Font = s.Font.With(weight: "Bold"));
                        break;
                    case YouTubeDescriptionTokenType.Timestamp:
                        commentText.AddArbitraryDrawable(new TimestampButton(item.Value)
                        {
                            TimestampClicked = TimestampClicked,
                        });
                        break;
                }
            }
        }

        public void UpdateData()
        {
            Task.Run(async () =>
            {
                try
                {
                    Schedule(() => loading.Show());
                    DateTimeOffset? dateTime = commentData2 != null ? commentData2.Snippet.PublishedAtDateTimeOffset : commentData.Snippet.TopLevelComment.Snippet.PublishedAtDateTimeOffset;
                    DateTimeOffset now = DateTime.Now;

                    try
                    {
                        //Channel? channelData = api.TryGetChannel(commentData.Snippet.TopLevelComment.Snippet.AuthorChannelId.Value);

                        Schedule(() =>
                        {
                            //channelName.Text = channelData != null ? api.GetLocalizedChannelTitle(channelData) : commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                            if (commentData2 != null)
                            {
                                replyCountBox.Hide();
                                channelName.Text = AXPlayerStrings.CommentReply(commentData2.Snippet.AuthorDisplayName, commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName);
                            }
                            else
                            {
                                channelName.Text = commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                            }
                            channelName.AddText(" • ", f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
#pragma warning disable CS8629 // Nullable 값 형식이 null일 수 있습니다.
                            channelName.AddText(dateTime.Value.Humanize(dateToCompareAgainst: now), f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
#pragma warning restore CS8629 // Nullable 값 형식이 null일 수 있습니다.
                            setText(commentData2 != null ? commentData2.Snippet.TextOriginal : commentData.Snippet.TopLevelComment.Snippet.TextOriginal);
                            likeCount.Text = Convert.ToInt32(commentData2 != null ? commentData2.Snippet.LikeCount : commentData.Snippet.TopLevelComment.Snippet.LikeCount).ToStandardFormattedString(0);
                            translateToText.Text = AXPlayerStrings.TranslateTo(app.CurrentLanguage.Value.GetLocalisableDescription());
                            profileImage.UpdateProfileImage(commentData2 != null ? commentData2.Snippet.AuthorChannelId.Value : commentData.Snippet.TopLevelComment.Snippet.AuthorChannelId.Value);
                            replyCount.Text = Convert.ToInt32(commentData2 != null ? 0 : commentData.Snippet.TotalReplyCount).ToStandardFormattedString(0);

                            /*
                            usernameDisplayMode.BindValueChanged(locale =>
                            {
                                Schedule(() =>
                                {
                                    channelName.Text = string.Empty;
                                    channelName.Text = commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                                    //channelName.Text = channelData != null ? api.GetLocalizedChannelTitle(channelData) : commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                                    channelName.AddText(" • ", f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                    channelName.AddText(dateTime.Value.Humanize(dateToCompareAgainst: now), f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                });
                            }, true);
                            */

                            uiLanguage.BindValueChanged(locale =>
                            {
                                Schedule(() =>
                                {
                                    channelName.Text = string.Empty;
                                    if (commentData2 != null)
                                    {
                                        channelName.Text = AXPlayerStrings.CommentReply(commentData2.Snippet.AuthorDisplayName, commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName);
                                    }
                                    else
                                    {
                                        channelName.Text = commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                                    }
                                    //channelName.Text = channelData != null ? api.GetLocalizedChannelTitle(channelData) : commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                                    channelName.AddText(" • ", f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                    channelName.AddText(dateTime.Value.Humanize(dateToCompareAgainst: now), f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                    translateToText.Text = AXPlayerStrings.TranslateTo(app.CurrentLanguage.Value.GetLocalisableDescription());
                                });
                            });

                            Schedule(() => loading.Hide());
                        });
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, e.GetDescription());
                        Schedule(() =>
                        {
                            channelName.Text = commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                            channelName.AddText(" • ", f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
#pragma warning disable CS8629 // Nullable 값 형식이 null일 수 있습니다.
                            channelName.AddText(dateTime.Value.Humanize(dateToCompareAgainst: now), f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
#pragma warning restore CS8629 // Nullable 값 형식이 null일 수 있습니다.
                            setText(commentData.Snippet.TopLevelComment.Snippet.TextOriginal);
                            likeCount.Text = Convert.ToInt32(commentData.Snippet.TopLevelComment.Snippet.LikeCount).ToStandardFormattedString(0);
                            translateToText.Text = AXPlayerStrings.TranslateTo(app.CurrentLanguage.Value.GetLocalisableDescription());
                            profileImage.UpdateProfileImage(commentData.Snippet.TopLevelComment.Snippet.AuthorChannelId.Value);
                            replyCount.Text = Convert.ToInt32(commentData.Snippet.TotalReplyCount).ToStandardFormattedString(0);

                            usernameDisplayMode.BindValueChanged(locale =>
                            {
                                Schedule(() =>
                                {
                                    channelName.Text = string.Empty;
                                    channelName.Text = commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                                    channelName.AddText(" • ", f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                    channelName.AddText(dateTime.Value.Humanize(dateToCompareAgainst: now), f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                });
                            }, true);

                            uiLanguage.BindValueChanged(locale =>
                            {
                                Schedule(() =>
                                {
                                    channelName.Text = string.Empty;
                                    channelName.Text = commentData.Snippet.TopLevelComment.Snippet.AuthorDisplayName;
                                    channelName.AddText(" • ", f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                    channelName.AddText(dateTime.Value.Humanize(dateToCompareAgainst: now), f => f.Font = AXPlayerApp.DefaultFont.With(size: 13, weight: "Regular"));
                                    translateToText.Text = AXPlayerStrings.TranslateTo(app.CurrentLanguage.Value.GetLocalisableDescription());
                                });
                            });

                            Schedule(() => loading.Hide());
                        });
                    }
                }
                catch
                {
                    Hide();
                }
            });
        }
    }
}

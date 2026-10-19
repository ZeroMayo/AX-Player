// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Humanizer;
using AXPlayer.App.Config;
using AXPlayer.App.Extensions;
using AXPlayer.App.Graphics;
using AXPlayer.App.Graphics.Containers;
using AXPlayer.App.Graphics.UserInterface;
using AXPlayer.App.Graphics.UserInterfaceV2;
using AXPlayer.App.Localisation;
using AXPlayer.App.Online;
using AXPlayer.App.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osuTK.Graphics;
using PaletteNet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static Google.Apis.YouTube.v3.CommentThreadsResource.ListRequest;
using Container = osu.Framework.Graphics.Containers.Container;

namespace AXPlayer.App.Screens
{
    public partial class MainAppView
    {
        private RoundedIconButton viewMoreComments;
        private Container viewMoreCommentsContainer;

        private void updateComments(string videoId, string pageToken = "")
        {
            if (commentsDisabled)
            {
                Schedule(() =>
                {
                    quickCommentOpenButton.TooltipText = AXPlayerStrings.Comments(AXPlayerStrings.DisabledByUploader);
                    commentCount.Text = AXPlayerStrings.DisabledByUploader;
                    commentsContainerTitle.Text = AXPlayerStrings.Comments(AXPlayerStrings.DisabledByUploader);
                    commentsEmpty.Show();
                });
                return;
            }

            Schedule(() =>
            {
                foreach (var item in commentContainer.Children)
                {
                    if (string.IsNullOrEmpty(pageToken))
                        Schedule(() => item.Expire());
                }

                quickCommentOpenButton.TooltipText = AXPlayerStrings.Comments(videoData.Statistics.CommentCount != null ? Convert.ToInt32(videoData.Statistics.CommentCount).ToStandardFormattedString(0) : AXPlayerStrings.DisabledByUploader);
                commentCount.Text = videoData.Statistics.CommentCount != null ? Convert.ToDouble(videoData.Statistics.CommentCount).ToMetric(decimals: 2) : AXPlayerStrings.DisabledByUploader;
                commentsContainerTitle.Text = AXPlayerStrings.Comments(videoData.Statistics.CommentCount != null ? Convert.ToInt32(videoData.Statistics.CommentCount).ToStandardFormattedString(0) : AXPlayerStrings.DisabledByUploader);

                OrderEnum orderEnum = commentsSort.Value == CommentsSortCriteria.Top ? OrderEnum.Relevance : OrderEnum.Time;

                CommentThreadListResponse commentThreadListResponse;

                try
                {
                    // comments area
                    commentThreadListResponse = api.GetCommentThread(videoId, pageToken, orderEnum);

                    currentCommentsNextPageToken = commentThreadListResponse.NextPageToken;

                    if (!string.IsNullOrEmpty(commentThreadListResponse.NextPageToken))
                    {
                        viewMoreComments = new RoundedIconButton(FontAwesome.Solid.ArrowDown)
                        {
                            Width = 250,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = AXPlayerStrings.ViewMoreComments,
                            Action = () =>
                            {
                                viewMoreCommentsContainer.Expire();
                                updateComments(videoId, currentCommentsNextPageToken);
                            }
                        };

                        viewMoreCommentsContainer = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                viewMoreComments,
                            }
                        };
                    }

                    for (int i = 0; i < commentThreadListResponse.Items.Count; i++)
                    {
                        CommentThread item = commentThreadListResponse.Items[i];
#pragma warning disable CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.
                        Task.Run(async () =>
                        {
                            Schedule(() =>
                            {
                                commentContainer.Add(new CommentDisplay(item)
                                {
                                    RelativeSizeAxes = Axes.X,
                                    TimestampClicked = second =>
                                    {
                                        Logger.Log(second.ToString());
                                        hideOverlays();
                                        seekTo((second / 60) * 1000);
                                    }
                                });

                                if (item.Replies != null && item.Replies.Comments != null && item.Replies.Comments.Count > 0)
                                {
                                    for (int i2 = 0; i2 < item.Replies.Comments.Count; i2++)
                                    {
                                        Comment item2 = item.Replies.Comments[i2];
                                        commentContainer.Add(new CommentDisplay(item, item2)
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            TimestampClicked = second =>
                                            {
                                                Logger.Log(second.ToString());
                                                hideOverlays();
                                                seekTo((second / 60) * 1000);
                                            }
                                        });
                                    }
                                }
                            });
                        }).ContinueWith(_ =>
                        {
                            if (!string.IsNullOrEmpty(commentThreadListResponse.NextPageToken))
                            {
                                Schedule(() =>
                                {
                                    commentContainer.Remove(viewMoreCommentsContainer, false);
                                    commentContainer.Add(viewMoreCommentsContainer);
                                });
                            }
                        });
#pragma warning restore CS4014 // 이 호출을 대기하지 않으므로 호출이 완료되기 전에 현재 메서드가 계속 실행됩니다.
                    }

                    if (commentThreadListResponse.Items.Count > 0)
                        commentsEmpty.Hide();
                    else
                        commentsEmpty.Show();
                }
                catch (Exception e)
                {
                    Logger.Error(e, e.GetDescription());
                    commentsEmpty.Show();
                }
            });
        }

        private bool isVideoLoading;
        private VideosResource.RateRequest.RatingEnum currentVideoLikeOrDislike;

        private void updateRatingButtons(string videoId, bool ratingButtonsEnabled)
        {
            Task.Run(async () =>
            {
                VideosResource.RateRequest.RatingEnum things;
                if (googleOAuth2.SignedIn.Value)
                {
                    things = await api.GetVideoRating(videoId);
                }
                else
                {
                    things = VideosResource.RateRequest.RatingEnum.None;
                }
                currentVideoLikeOrDislike = things;

                switch (things)
                {
                    case VideosResource.RateRequest.RatingEnum.None:
                    {
                        Schedule(() =>
                        {
                            dislikeButtonBackgroundSelected.FadeOut(250, Easing.OutQuint);
                            likeButtonBackgroundSelected.FadeOut(250, Easing.OutQuint);
                            dislikeButton.TransformTo(nameof(CornerRadius), new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f, 16, 16), 250, Easing.OutQuint);
                            likeButton.TransformTo(nameof(CornerRadius), new CornersInfo(16, 16, AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f), 250, Easing.OutQuint);
                            likeButtonForeground.FadeColour(overlayColourProvider1.Content2, 250, Easing.OutQuint);
                            dislikeButtonForeground.FadeColour(overlayColourProvider1.Content2, 250, Easing.OutQuint);
                            quickLikeButton.SetEnabledValueLeftSide(false);
                            quickLikeButton.IconObject.FadeColour(accentColor, 250, Easing.OutQuint);
                            quickDislikeButton.SetEnabledValueRightSide(false);
                            quickDislikeButton.IconObject.FadeColour(accentColor, 250, Easing.OutQuint);

                            if (ratingButtonsEnabled)
                            {
                                likeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Like);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                dislikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Dislike);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                quickLikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Like);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                quickDislikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Dislike);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };
                            }
                            else
                            {
                                likeButton.ClickAction = async _ =>
                                {
                                };

                                dislikeButton.ClickAction = async _ =>
                                {
                                };

                                quickLikeButton.ClickAction = async _ =>
                                {
                                };

                                quickDislikeButton.ClickAction = async _ =>
                                {
                                };
                            }
                        });
                        break;
                    }
                    case VideosResource.RateRequest.RatingEnum.Like:
                    {
                        Schedule(() =>
                        {
                            dislikeButtonBackgroundSelected.FadeOut(250, Easing.OutQuint);
                            likeButtonBackgroundSelected.FadeIn(250, Easing.OutQuint);
                            dislikeButton.TransformTo(nameof(CornerRadius), new CornersInfo(AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f, 16, 16), 250, Easing.OutQuint);
                            likeButton.TransformTo(nameof(CornerRadius), new CornersInfo(16f), 250, Easing.OutQuint);
                            likeButtonForeground.FadeColour(overlayColourProvider1.Background4, 250, Easing.OutQuint);
                            dislikeButtonForeground.FadeColour(overlayColourProvider1.Content2, 250, Easing.OutQuint);
                            quickLikeButton.SetEnabledValueLeftSide(true);
                            quickLikeButton.IconObject.FadeColour(bgColor, 250, Easing.OutQuint);
                            quickDislikeButton.SetEnabledValueRightSide(false);
                            quickDislikeButton.IconObject.FadeColour(accentColor, 250, Easing.OutQuint);

                            if (ratingButtonsEnabled)
                            {
                                likeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.None);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                dislikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Dislike);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                quickLikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.None);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                quickDislikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Dislike);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };
                            }
                            else
                            {
                                likeButton.ClickAction = async _ =>
                                {
                                };

                                dislikeButton.ClickAction = async _ =>
                                {
                                };

                                quickLikeButton.ClickAction = async _ =>
                                {
                                };

                                quickDislikeButton.ClickAction = async _ =>
                                {
                                };
                            }
                        });
                        break;
                    }
                    case VideosResource.RateRequest.RatingEnum.Dislike:
                    {
                        Schedule(() =>
                        {
                            dislikeButtonBackgroundSelected.FadeIn(250, Easing.OutQuint);
                            likeButtonBackgroundSelected.FadeOut(250, Easing.OutQuint);
                            dislikeButton.TransformTo(nameof(CornerRadius), new CornersInfo(16f), 250, Easing.OutQuint);
                            likeButton.TransformTo(nameof(CornerRadius), new CornersInfo(16, 16, AXPlayerApp.UI_CORNER_RADIUS / 3f, AXPlayerApp.UI_CORNER_RADIUS / 3f), 250, Easing.OutQuint);
                            likeButtonForeground.FadeColour(overlayColourProvider1.Content2, 250, Easing.OutQuint);
                            dislikeButtonForeground.FadeColour(overlayColourProvider1.Background4, 250, Easing.OutQuint);
                            quickLikeButton.SetEnabledValueLeftSide(false);
                            quickLikeButton.IconObject.FadeColour(accentColor, 250, Easing.OutQuint);
                            quickDislikeButton.SetEnabledValueRightSide(true);
                            quickDislikeButton.IconObject.FadeColour(bgColor, 250, Easing.OutQuint);

                            if (ratingButtonsEnabled)
                            {
                                likeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Like);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                dislikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.None);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                quickLikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.Like);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };

                                quickDislikeButton.ClickAction = async _ =>
                                {
                                    if (!googleOAuth2.SignedIn.Value)
                                        return;

                                    await api.RateVideo(videoId, VideosResource.RateRequest.RatingEnum.None);
                                    Schedule(() =>
                                    {
                                        refreshLikeDislikeCount(videoId);
                                        updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                                    });
                                };
                            }
                            else
                            {
                                likeButton.ClickAction = async _ =>
                                {
                                };

                                dislikeButton.ClickAction = async _ =>
                                {
                                };

                                quickLikeButton.ClickAction = async _ =>
                                {
                                };

                                quickDislikeButton.ClickAction = async _ =>
                                {
                                };
                            }
                        });
                        break;
                    }
                }
            });
        }

        [Resolved]
        private OverlayColourProvider overlayColourProvider1 { get; set; }

        private void refreshLikeDislikeCount(string videoId)
        {
            Task.Run(() =>
            {
                try
                {
                    dislikeCount.Text = ReturnYouTubeDislike.GetDislikes(videoId).Dislikes > 0 ? Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoId).Dislikes).ToMetric(decimals: 2) : Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoId).RawDislikes).ToMetric(decimals: 2);
                    dislikeButton.TooltipText = AXPlayerStrings.DislikeCountTooltip(ReturnYouTubeDislike.GetDislikes(videoId).Dislikes.ToStandardFormattedString(0), ReturnYouTubeDislike.GetDislikes(videoId).RawDislikes.ToStandardFormattedString(0));
                }
                catch
                {
                    dislikeCount.Text = "0";
                }

                likeCount.Text = videoData.Statistics.LikeCount != null ? Convert.ToDouble(videoData.Statistics.LikeCount).ToMetric(decimals: 2) : Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoId).RawLikes).ToMetric(decimals: 2);
            });
        }

        public void SetVideoMetadataDisplayAlignment(VideoMetadataDisplayAlignment alignment)
        {
            videoMetadataDisplay.SetVideoMetadataDisplayAlignment(alignment);
            switch (alignment)
            {
                case VideoMetadataDisplayAlignment.Left:
                {
                    videoMetadataDisplay.Anchor = Anchor.TopLeft;
                    videoMetadataDisplay.Origin = Anchor.TopLeft;
                    videoMetadataDisplayBase.Padding = new MarginPadding(0);
                    break;
                }
                case VideoMetadataDisplayAlignment.Center:
                {
                    videoMetadataDisplay.Anchor = Anchor.TopCentre;
                    videoMetadataDisplay.Origin = Anchor.TopCentre;
                    videoMetadataDisplayBase.Padding = new MarginPadding(0);
                    break;
                }
                case VideoMetadataDisplayAlignment.Right:
                {
                    videoMetadataDisplay.Anchor = Anchor.TopRight;
                    videoMetadataDisplay.Origin = Anchor.TopRight;
                    videoMetadataDisplayBase.Padding = new MarginPadding
                    {
                        Right = 44,
                    };
                    break;
                }
            }
        }

        private Color4 bgColor2;

        public void GetProfileImagePalette(Google.Apis.YouTube.v3.Data.Channel channel)
        {
            Task.Run(async () =>
            {
                var cachePath = app.Host.CacheStorage.GetStorageForDirectory("profile_cache_GetProfileImagePalette").GetFullPath($"{channel.Id}.png");

                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(channel.Snippet.Thumbnails.High.Url);
                    await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes);
                }

                using Image<Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<Rgba32>(app.Host.CacheStorage.GetStorageForDirectory("profile_cache").GetFullPath($"{channel.Id}.png"));

                IBitmapHelper bitmapHelper = new BitmapHelper(bitmap);
                PaletteBuilder paletteBuilder = new PaletteBuilder();
                Palette palette = paletteBuilder.Generate(bitmapHelper);
                int? rgbColor = palette.MutedSwatch.Rgb;
                int? rgbTextColor = palette.MutedSwatch.TitleTextColor;

                if (rgbColor != null && rgbTextColor != null)
                {
                    Color4 bgColor = System.Drawing.Color.FromArgb((int)rgbColor);
                    Color4 textColor = System.Drawing.Color.FromArgb((int)rgbTextColor);
                    Schedule(() =>
                    {
                        viewChannelButton.BackgroundColour = bgColor;
                        viewChannelButton.ForegroundColour = textColor;
                    });
                }
            });
        }

        public void UpdateChannelSubscribeState(string channelId)
        {
            Task.Run(async () =>
            {
                if (signedIn.Value == true)
                {
                    bool response = await api.IsChannelSubscribed(channelId);

                    if (response == true)
                        Schedule(() => subscribeButton.Text = AXPlayerStrings.Unsubscribe);
                    else
                        Schedule(() => subscribeButton.Text = AXPlayerStrings.Subscribe);
                }
                else
                {
                    Schedule(() => subscribeButton.Text = AXPlayerStrings.Subscribe);
                }
            });
        }

        public void GetPalette(Google.Apis.YouTube.v3.Data.Video video)
        {
            Task.Run(async () =>
            {
                try
                {
                    var cachePath = app.Host.CacheStorage.GetStorageForDirectory("videoThumbnailCache_").GetFullPath($"{video.Id}.png");

                    using (var httpClient = new System.Net.Http.HttpClient())
                    {
                        var imageBytes = await httpClient.GetByteArrayAsync(video.Snippet.Thumbnails.High.Url);
                        await System.IO.File.WriteAllBytesAsync(cachePath, imageBytes);
                    }

                    using SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> bitmap = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(app.Host.CacheStorage.GetStorageForDirectory("videoThumbnailCache_").GetFullPath($"{video.Id}.png"));

                    IBitmapHelper bitmapHelper = new BitmapHelper(bitmap);
                    PaletteBuilder paletteBuilder = new PaletteBuilder();
                    Palette palette = paletteBuilder.Generate(bitmapHelper);
                    try
                    {
                        int? rgbColor = palette.LightMutedSwatch?.Rgb;
                        int? rgbColor2 = palette.DarkMutedSwatch?.Rgb;

                        if (rgbColor != null && rgbColor2 != null)
                        {
                            accentColor = System.Drawing.Color.FromArgb((int)rgbColor);
                            bgColor = System.Drawing.Color.FromArgb((int)rgbColor2);
                            bgColor2 = System.Drawing.Color.FromArgb((int)rgbColor2);
                        }
                        else
                        {
                            accentColor = overlayColourProvider1.Content2;
                            bgColor = overlayColourProvider1.Background3;
                            bgColor2 = overlayColourProvider1.Content2.Darken(1);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, e.GetDescription());
                    }
                }
                catch
                {
                    accentColor = overlayColourProvider1.Content2;
                    bgColor = overlayColourProvider1.Background3;
                    bgColor2 = overlayColourProvider1.Content2.Darken(1);
                }

                #region video controls color area

                Schedule(() =>
                {
                    videoMetadataDisplay.ApplyColours(new VideoMetadataDisplayWithoutProfile.VideoMetadataDisplayWithoutProfileColours
                    {
                        BGColor = bgColor,
                        FGColor = accentColor,
                        FGColor2 = accentColor,
                    });

                    seekbar.AccentColour = accentColor;
                    seekbar.BackgroundColour = bgColor2;

                    //spinner.AccentColor = accentColor;
                    //spinner.BackgroundColour = bgColor;

                    videoLoadingProgressSpinner.AccentColor = accentColor;
                    videoLoadingProgressSpinner.BackgroundColour = bgColor;
                    videoLoadingProgressBaseBG.Colour = bgColor;

                    prevVideoButton.AccentColor = accentColor;
                    prevVideoButton.BackgroundColour = bgColor;
                    prevVideoButton.IconObject.FadeColour(accentColor);

                    nextVideoButton.AccentColor = accentColor;
                    nextVideoButton.BackgroundColour = bgColor;
                    nextVideoButton.IconObject.FadeColour(accentColor);

                    playPause.AccentColor = accentColor;
                    playPause.BackgroundColour = bgColor;

                    if (currentVideoSource != null)
                        playPause.IconObject.FadeColour(currentVideoSource.IsPlaying() ? bgColor : accentColor);
                    else
                        playPause.IconObject.FadeColour(accentColor);

                    speedBarBG.Colour = bgColor;
                    speedBarIcon.Colour = accentColor;

                    repeatButton.IconObject.FadeColour(repeat.Value ? bgColor : accentColor);
                    repeatButton.AccentColor = accentColor;
                    repeatButton.BackgroundColour = bgColor;

                    quickLikeButton.IconObject.FadeColour(currentVideoLikeOrDislike == VideosResource.RateRequest.RatingEnum.Like ? bgColor : accentColor, 250, Easing.OutQuint);
                    quickLikeButton.AccentColor = accentColor;
                    quickLikeButton.BackgroundColour = bgColor;
                    quickDislikeButton.IconObject.FadeColour(currentVideoLikeOrDislike == VideosResource.RateRequest.RatingEnum.Dislike ? bgColor : accentColor, 250, Easing.OutQuint);
                    quickDislikeButton.AccentColor = accentColor;
                    quickDislikeButton.BackgroundColour = bgColor;

                    quickCommentOpenButton.IconObject.FadeColour(accentColor);
                    quickCommentOpenButton.AccentColor = accentColor;
                    quickCommentOpenButton.BackgroundColour = bgColor;

                    videoSettingsButton.IconObject.FadeColour(accentColor);
                    videoSettingsButton.AccentColor = accentColor;
                    videoSettingsButton.BackgroundColour = bgColor;

                    playlistButton.IconObject.FadeColour(accentColor);
                    playlistButton.AccentColor = accentColor;
                    playlistButton.BackgroundColour = bgColor;

                    captionButton.IconObject.FadeColour(captionEnabled.Value ? bgColor : accentColor);
                    captionButton.AccentColor = accentColor;
                    captionButton.BackgroundColour = bgColor;

                    pinButton.IconObject.FadeColour(alwaysShowControl.Value ? bgColor : accentColor);
                    pinButton.AccentColor = accentColor;
                    pinButton.BackgroundColour = bgColor;

                    speedText.Colour = accentColor;

                    volumeBarBG.Colour = bgColor;
                    volumeIcon.Colour = accentColor;

                    volumeBarSlider.AccentColour = accentColor;
                    volumeBarSlider.BackgroundColour = bgColor.Lighten(0.5f);

                    volumeText.Colour = accentColor;

                    timeBG.Colour = bgColor;
                    timeText.Colour = accentColor;

                    menuOverlayShow.BackgroundColour = bgColor;
                    menuOverlayShow.IconColour = accentColor;
                });

                #endregion
            });
        }

        private Color4 accentColor;
        private Color4 bgColor;

        private string currentCommentsNextPageToken;

        private void updateVideoMetadata(string videoId)
        {
            videoMetadataDisplay.UpdateVideo(videoId);
            videoMetadataDisplayDetails.UpdateVideo(videoId);
            Task.Run(async () =>
            {
                // metadata area
                videoData = api.GetVideo(videoId);
                channelData = api.GetChannel(videoData.Snippet.ChannelId);
                profileImage.UpdateProfileImage(videoData.Snippet.ChannelId);
                updateRatingButtons(videoId, videoData.Statistics.LikeCount != null);
                UpdateChannelSubscribeState(videoData.Snippet.ChannelId);

                Schedule(() => GetPalette(videoData));
                Schedule(() => commentOpenButton.Enabled.Value = videoData.Statistics.CommentCount != null);

                if (googleOAuth2.SignedIn.Value)
                {
                    Schedule(() => reportOpenButton.Enabled.Value = true);
                    Schedule(() => saveVideoOpenButton.Enabled.Value = true);
                    Schedule(() => quickLikeButton.Enabled.Value = true);
                    Schedule(() => quickDislikeButton.Enabled.Value = true);
                }
                //Schedule(() => seekbar.GetPalette(videoData));

                commentsDisabled = videoData.Statistics.CommentCount == null;

                if (videoData.Statistics.CommentCount != null)
                {
                    Schedule(() => quickCommentOpenButton.Enabled.Value = true);
                }
                else
                {
                    Schedule(() => quickCommentOpenButton.Enabled.Value = false);
                }

                updateWindowTitle();
                //game.RequestUpdateWindowTitle($"{api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId))} - {api.GetLocalizedVideoTitle(videoData)}");

                DateTimeOffset? dateTime = videoData.Snippet.PublishedAtDateTimeOffset;
                DateTime now = DateTime.Now;
                if (!string.IsNullOrEmpty(api.GetLocalizedVideoDescription(videoData)))
                {
                    //Schedule(() => videoDescription.Text = api.GetLocalizedVideoDescription(videoData));
                    Schedule(() => GetLocalizedVideoDescriptionRemake(videoData));
                }
                else
                {
                    videoDescription.Text = string.Empty;
                    Schedule(() => videoDescription.AddText(AXPlayerStrings.NoDescription, text =>
                    {
                        text.Font = AXPlayerApp.DefaultFont.With(weight: "SemiBold");
                        text.Colour = overlayColourProvider1.Background1;
                    }));
                }
                sessionStatics.GetBindable<string>(Static.CurrentThumbnailUrl).Value = videoData.Snippet.Thumbnails.High.Url;
                //commentCount.Text = videoData.Statistics.CommentCount != null ? Convert.ToInt32(videoData.Statistics.CommentCount).ToStandardFormattedString(0) : NekoPlayerStrings.DisabledByUploader;
                /*
                try
                {
                    dislikeCount.Text = ReturnYouTubeDislike.GetDislikes(videoId).Dislikes > 0 ? Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoId).Dislikes).ToMetric(decimals: 2) : Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoId).RawDislikes).ToMetric(decimals: 2);
                    dislikeButton.TooltipText = NekoPlayerStrings.DislikeCountTooltip(ReturnYouTubeDislike.GetDislikes(videoId).Dislikes.ToStandardFormattedString(0), ReturnYouTubeDislike.GetDislikes(videoId).RawDislikes.ToStandardFormattedString(0));
                }
                catch
                {
                    dislikeCount.Text = "0";
                }
                */

                string uploadDateRaw = videoData.Snippet.PublishedAtRaw;

                DateTime.TryParseExact(uploadDateRaw, @"yyyy-MM-dd\THH:mm:ss\Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var uploadDate);

                //likeCount.Text = videoData.Statistics.LikeCount != null ? Convert.ToDouble(videoData.Statistics.LikeCount).ToMetric(decimals: 2) : Convert.ToDouble(ReturnYouTubeDislike.GetDislikes(videoId).RawLikes).ToMetric(decimals: 2);
                //commentsContainerTitle.Text = NekoPlayerStrings.Comments(videoData.Statistics.CommentCount != null ? Convert.ToInt32(videoData.Statistics.CommentCount).ToStandardFormattedString(0) : NekoPlayerStrings.Disabled);
                videoInfoDetails.Text = AXPlayerStrings.VideoMetadataDescWithoutChannelName(Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), uploadDate.ToString());

                updateComments(videoId);

                refreshLikeDislikeCount(videoId);

                Schedule(() =>
                {
                    updatePresence(discordRichPresence.Value);

                    subscribeButton.Action = () =>
                    {
                        Task.Run(async () =>
                        {
                            if (!googleOAuth2.SignedIn.Value)
                                return; //log in to more actions

                            bool result = await api.IsChannelSubscribed(videoData.Snippet.ChannelId);
                            string subscriptionId = await api.GetSubscriptionId(videoData.Snippet.ChannelId);

                            Logger.Log("SubscribeClickAction clicked");

                            if (result)
                            {
                                Schedule(() => youtubeChannelMetadataDisplay.UpdateUser(api.GetChannel(videoData.Snippet.ChannelId)));

                                declineButton.Action = async () =>
                                {
                                    hideOverlayContainer(unsubscribeDialog);
                                };
                                acceptButton.Action = async () =>
                                {
                                    hideOverlayContainer(unsubscribeDialog);
                                    await api.UnsubscribeChannel(subscriptionId);

                                    Logger.Log("UnsubscribeChannel()");

                                    /*
                                    ToastBase toast = new ToastWithIcon(NekoPlayerStrings.General, NekoPlayerStrings.SubscriptionRemoved, FontAwesome.Solid.SignOutAlt);

                                    Schedule(() => onScreenDisplay.Display(toast));
                                    */
                                    Schedule(() =>
                                    {
                                        notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.SignOutAlt, Color4.Red, AXPlayerStrings.SubscriptionRemoved, api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId))));
                                    });
                                    Schedule(() => UpdateChannelSubscribeState(videoData.Snippet.ChannelId));
                                };

                                Schedule(() =>
                                {
                                    hideOverlays();
                                    showOverlayContainer(unsubscribeDialog);
                                });
                            }
                            else
                            {
                                await api.SubscribeChannel(videoData.Snippet.ChannelId);

                                Logger.Log("SubscribeChannel()");

                                /*
                                ToastBase toast = new ToastWithIcon(NekoPlayerStrings.General, NekoPlayerStrings.SubscriptionAdded, FontAwesome.Solid.SignInAlt);

                                Schedule(() => onScreenDisplay.Display(toast));
                                */
                                Schedule(() =>
                                {
                                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.SignInAlt, Color4.Green, AXPlayerStrings.SubscriptionAdded, api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId))));
                                });
                                Schedule(() => UpdateChannelSubscribeState(videoData.Snippet.ChannelId));
                            }
                        });
                    };

                    reportButton.Action = () =>
                    {
                        if (!googleOAuth2.SignedIn.Value)
                            return;

                        Schedule(() =>
                        {
                            notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.CheckCircle, Color4.Green, AXPlayerStrings.ReportSuccess, AXPlayerStrings.Report));
                        });
                        //ToastBase toast = new ToastWithIcon(NekoPlayerStrings.Report, NekoPlayerStrings.ReportSuccess, FontAwesome.Solid.CheckCircle);
                        api.ReportAbuse(videoId, reportReason.Current.Value.Id, (reportReason.Current.Value.ContainsSecondaryReasons ? reportSubReason.Current.Value.Id : null), (!string.IsNullOrEmpty(reportComment.Current.Value) ? reportComment.Current.Value : null));
                        //Schedule(() => onScreenDisplay.Display(toast));
                        reportComment.Current.Value = string.Empty;
                        reportReason.Current.Value = reportReason.Items.ToArray()[0];
                        reportSubReason.Current.Value = reportSubReason.Items.ToArray()[0];
                        hideOverlayContainer(reportAbuseOverlay);
                    };

                    commentSendButton.ClickAction = _ =>
                    {
                        if (videoData == null)
                            return;

                        if (!googleOAuth2.SignedIn.Value)
                            return;

                        //ToastBase toast = new ToastWithIcon(NekoPlayerStrings.General, NekoPlayerStrings.CommentAdded, FontAwesome.Regular.Comment);
                        api.SendComment(videoId, commentTextBox.Text);

                        Scheduler.AddDelayed(() => updateComments(videoId), 2000);

                        //Schedule(() => onScreenDisplay.Display(toast));
                        Schedule(() =>
                        {
                            notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Regular.Comment, Color4.White, AXPlayerStrings.CommentAdded, AXPlayerStrings.General));
                        });

                        commentTextBox.Text = string.Empty;
                    };
                });

                commentsSort.BindValueChanged(sort =>
                {
                    updateComments(videoId);
                });

                usernameDisplayMode.BindValueChanged(locale =>
                {
                    Schedule(() =>
                    {
                        updateWindowTitle();
                        //game.RequestUpdateWindowTitle($"{api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId))} - {api.GetLocalizedVideoTitle(videoData)}");
                        if (api.TryToGetMineChannel() != null)
                            commentTextBox.PlaceholderText = AXPlayerStrings.CommentWith;
                    });
                }, true);

                showVideoMetadataOnWindowTitle.BindValueChanged(_ =>
                {
                    Schedule(() =>
                    {
                        updateWindowTitle();
                    });
                }, true);

                localeBindable.BindValueChanged(locale =>
                {
                    Task.Run(async () =>
                    {
                        Schedule(() =>
                        {
                            updateWindowTitle();
                            //game.RequestUpdateWindowTitle($"{api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId))} - {api.GetLocalizedVideoTitle(videoData)}");
                            if (!string.IsNullOrEmpty(api.GetLocalizedVideoDescription(videoData)))
                            {
                                //Schedule(() => videoDescription.Text = api.GetLocalizedVideoDescription(videoData));
                                Schedule(() => GetLocalizedVideoDescriptionRemake(videoData));
                            }
                            else
                            {
                                videoDescription.Text = string.Empty;
                                Schedule(() => videoDescription.AddText(AXPlayerStrings.NoDescription, text =>
                                {
                                    text.Font = AXPlayerApp.DefaultFont.With(weight: "SemiBold");
                                    text.Colour = overlayColourProvider1.Background1;
                                }));
                            }
                            videoInfoDetails.Text = AXPlayerStrings.VideoMetadataDescWithoutChannelName(Convert.ToInt32(videoData.Statistics.ViewCount).ToStandardFormattedString(0), uploadDate.ToString());
                        });
                    });
                });

                if (googleOAuth2.SignedIn.Value)
                {
                    try
                    {
                        foreach (var item in myPlaylistsDropdown.Items)
                        {
                            bool result = await api.IsVideoExistsOnPlaylist(item.Id, videoId);
                            Schedule(() => saveVideoOpenButton.Icon = result ? FontAwesome.Solid.Bookmark : FontAwesome.Regular.Bookmark);
                        }
                    }
                    catch
                    {
                    }
                }

                TimeSpan duration = XmlConvert.ToTimeSpan(videoData.ContentDetails.Duration);
                videoDuration = duration;
                if (duration.Hours > 0)
                {
                    totalTimeText = $"{duration.Hours.ToString("0")}:{duration.Minutes.ToString("00")}:{duration.Seconds.ToString("00")}";
                }
                else
                {
                    totalTimeText = $"{duration.Minutes.ToString("0")}:{duration.Seconds.ToString("00")}";
                }
            });
        }

        private string totalTimeText;

        private TimeSpan videoDuration;

        private Bindable<UsernameDisplayMode> usernameDisplayMode;

        private Bindable<bool> showVideoMetadataOnWindowTitle;

        private void updateWindowTitle()
        {
            if ((showVideoMetadataOnWindowTitle.Value) && (videoData != null))
            {
                game.RequestUpdateWindowTitle($"{TruncateWithEllipsis(api.GetLocalizedChannelTitle(api.GetChannel(videoData.Snippet.ChannelId)), 40)} - {TruncateWithEllipsis(api.GetLocalizedVideoTitle(videoData), 50)}");
            }
            else
            {
                game.RequestUpdateWindowTitle(string.Empty);
            }
        }

        private int parseTimestampFromURL(string url)
        {
            Match match = Regex.Match(url, @"[?&]t=(\d+)s?");

            if (match.Success)
            {
                int calculated = int.Parse(match.Groups[1].Value) * 1000;

                return calculated;
            }

            return 0;
        }

        private bool timestampInURL(string url)
        {
            Match match = Regex.Match(url, @"[?&]t=(\d+)s?");

            if (match.Success)
            {
                return true;
            }

            return false;
        }
    }
}

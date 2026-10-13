// Copyright (c) 2026 ZeroMayo <boomboxrapsody@gmail.com>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.YouTube.v3.Data;
using NekoPlayer.App.Extensions;
using NekoPlayer.App.Graphics.Containers;
using NekoPlayer.App.Graphics.UserInterface;
using NekoPlayer.App.Localisation;
using NekoPlayer.App.Online;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace NekoPlayer.App.Screens
{
    public partial class MainAppView
    {
        private void fetchMyPlaylists()
        {
            foreach (var item in myPlaylistItemsView.Children)
            {
                Schedule(() => item.Expire());
            }

            if (!googleOAuth2.SignedIn.Value)
                return;

            Task.Run(async () =>
            {
                IList<Playlist> playlists = await api.GetMyPlaylistItemsAsync();

                Schedule(() =>
                {
                    myPlaylistsDropdown.Items = playlists;

                    if (playlists.Count > 0)
                        myPlaylistsDropdown.Current.Value = playlists[0];
                });

                foreach (Playlist playlist in playlists)
                {
                    MyPlaylistView playlistItemView = new MyPlaylistView()
                    {
                        RelativeSizeAxes = Axes.X,
                        Enabled = { Value = true },
                        ClickAction = async v =>
                        {
                            Schedule(async () =>
                            {
                                SetPlaylist(playlist.Id).FireAndForget();
                            });
                        },
                        OptionsClickEvent = async data =>
                        {
                            Schedule(async () =>
                            {
                                hideOverlays();

                                editPlaylistTitleBox.Current.Value = data.Snippet.Title;
                                switch (data.Status.PrivacyStatus)
                                {
                                    case "public":
                                        editPlaylistPrivacyStatusDropdown.Current.Value = PrivacyStatus.Public;
                                        break;
                                    case "unlisted":
                                        editPlaylistPrivacyStatusDropdown.Current.Value = PrivacyStatus.Unlisted;
                                        break;
                                    case "private":
                                        editPlaylistPrivacyStatusDropdown.Current.Value = PrivacyStatus.Private;
                                        break;
                                }

                                showOverlayContainer(editPlaylistOverlay);

                                updatePlaylistButton.Action = async () =>
                                {
                                    await Task.Run(async () =>
                                    {
                                        Schedule(async () =>
                                        {
                                            hideOverlays();
                                        });

                                        await api.UpdatePlaylistInfo(data.Id, editPlaylistTitleBox.Current.Value, editPlaylistPrivacyStatusDropdown.Current.Value);

                                        await Task.Delay(1000);

                                        Schedule(async () =>
                                        {
                                            fetchMyPlaylists();
                                        });
                                    });
                                };
                            });
                        },
                    };

                    Schedule(() =>
                    {
                        playlistItemView.Data = playlist;
                        myPlaylistItemsView.Add(playlistItemView);
                        playlistItemView.UpdateData();
                    });
                }
            });
        }

        private void saveVideoToPlaylist(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
                return;

            Task.Run(async () =>
            {
                await api.SaveVideoToPlaylist(myPlaylistsDropdown.Current.Value.Id, videoId);

                saveVideoOpenButton.Icon = FontAwesome.Solid.Bookmark;

                /*
                ToastBase toast = new ToastWithIcon(NekoPlayerStrings.Playlists, NekoPlayerStrings.VideoSavedToPlaylist(myPlaylistsDropdown.Current.Value.Snippet.Title), FontAwesome.Solid.List);

                Schedule(() => onScreenDisplay.Display(toast));
                */
                Schedule(() =>
                {
                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.List, Color4.Green, NekoPlayerStrings.VideoSavedToPlaylist(myPlaylistsDropdown.Current.Value.Snippet.Title), NekoPlayerStrings.Playlists));
                });

                await Task.Delay(1000);

                Schedule(async () =>
                {
                    fetchMyPlaylists();
                });
            });
        }

        private void removeVideoFromPlaylist(string videoId)
        {
            if (string.IsNullOrEmpty(videoId))
                return;

            Task.Run(async () =>
            {
                await api.RemoveVideoFromPlaylist(myPlaylistsDropdown.Current.Value.Id, videoId);

                saveVideoOpenButton.Icon = FontAwesome.Regular.Bookmark;

                /*
                ToastBase toast = new ToastWithIcon(NekoPlayerStrings.Playlists, NekoPlayerStrings.VideoRemovedFromPlaylist(myPlaylistsDropdown.Current.Value.Snippet.Title), FontAwesome.Solid.List);

                Schedule(() => onScreenDisplay.Display(toast));
                */
                Schedule(() =>
                {
                    notificationOverlay.Push(new PushNotificationContainer(FontAwesome.Solid.List, Color4.Red, NekoPlayerStrings.VideoRemovedFromPlaylist(myPlaylistsDropdown.Current.Value.Snippet.Title), NekoPlayerStrings.Playlists));
                });

                await Task.Delay(1000);

                Schedule(async () =>
                {
                    fetchMyPlaylists();
                });
            });
        }

        public void GetReportReasons()
        {
            if (googleOAuth2.SignedIn.Value == false)
                return;

            IList<VideoAbuseReportReasonItem> wth2 = api.GetVideoAbuseReportReasons();

            Schedule(() =>
            {
                reportReason.Items = wth2;
                reportReason.Current.Value = wth2[0];
            });
        }
    }
}

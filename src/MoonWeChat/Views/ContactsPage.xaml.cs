using System;
using System.Collections.ObjectModel;
using System.Linq;
using MoonWeChat.Models;
using MoonWeChat.Services;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class ContactsPage : Page
    {
        public ObservableCollection<Contact> Contacts { get; } = new ObservableCollection<Contact>();

        public ContactsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            TabBar.SetSelectedIndex(1);
            ReloadLocal();
        }

        private void ReloadLocal()
        {
            Contacts.Clear();
            foreach (var c in AppServices.Data.GetContacts()
                .OrderBy(x => x.PinyinIndex)
                .ThenBy(x => x.DisplayName))
            {
                Contacts.Add(c);
            }
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            var keyword = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                ReloadLocal();
                return;
            }

            // 本地先过滤
            var local = AppServices.Data.GetContacts()
                .Where(c =>
                    (!string.IsNullOrEmpty(c.DisplayName) && c.DisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.WxId) && c.WxId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.Nickname) && c.Nickname.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            Contacts.Clear();
            foreach (var c in local)
            {
                Contacts.Add(c);
            }

            // 远程搜索（有 Token 时）
            if (!AppSettings.UseSampleData && AppSettings.IsRemoteConfigured)
            {
                try
                {
                    AppServices.Api.Configure(AppSettings.BaseUrl, AppSettings.Token);
                    var remote = await AppServices.Api.SearchFriendAsync(keyword);
                    if (remote.Ok && remote.Contacts != null)
                    {
                        foreach (var dto in remote.Contacts)
                        {
                            if (string.IsNullOrEmpty(dto.UserName))
                            {
                                continue;
                            }

                            if (Contacts.Any(x => string.Equals(x.WxId, dto.UserName, StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            Contacts.Add(new Contact
                            {
                                WxId = dto.UserName,
                                Nickname = dto.NickName ?? dto.UserName,
                                Remark = dto.Remark,
                                AvatarAccentColor = "#576B95",
                                AvatarUrl = dto.BigHeadImgUrl ?? dto.SmallHeadImgUrl,
                                Signature = "搜索结果",
                                PinyinIndex = "#"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    var dlg = new MessageDialog("远程搜索失败：" + ex.Message);
                    await dlg.ShowAsync();
                }
            }
        }

        private void OnTabSelected(object sender, int index)
        {
            switch (index)
            {
                case 0:
                    Frame.Navigate(typeof(ChatListPage));
                    break;
                case 2:
                    Frame.Navigate(typeof(MomentsPage));
                    break;
                case 3:
                    Frame.Navigate(typeof(SettingsPage));
                    break;
            }
        }
    }
}

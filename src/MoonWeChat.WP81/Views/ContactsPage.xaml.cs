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
    /// <summary>通讯录二级页（WP 全景体系，无底部 Tab）。</summary>
    public sealed partial class ContactsPage : Page
    {
        public ObservableCollection<Contact> Contacts { get; } = new ObservableCollection<Contact>();

        public ContactsPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ReloadLocal();
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(MainHubPage));
            }
        }

        private void ReloadLocal()
        {
            var desired = AppServices.Data.GetContacts()
                .OrderBy(x => x.PinyinIndex)
                .ThenBy(x => x.DisplayName).ToList();
            for (var i = Contacts.Count - 1; i >= desired.Count; i--) Contacts.RemoveAt(i);
            for (var i = 0; i < desired.Count; i++)
            {
                var c = desired[i];
                if (i < Contacts.Count && string.Equals(Contacts[i].WxId, c.WxId, StringComparison.OrdinalIgnoreCase)) continue;
                var old = Contacts.FirstOrDefault(x => string.Equals(x.WxId, c.WxId, StringComparison.OrdinalIgnoreCase));
                if (old != null) Contacts.Remove(old);
                if (i <= Contacts.Count) Contacts.Insert(i, c); else Contacts.Add(c);
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = SearchBox.Text == null ? string.Empty : SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword)) { ReloadLocal(); return; }
            var desired = AppServices.Data.GetContacts().Where(c =>
                (c.DisplayName ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (c.WxId ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (c.Nickname ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.PinyinIndex).ThenBy(x => x.DisplayName).ToList();
            for (var i = Contacts.Count - 1; i >= desired.Count; i--) Contacts.RemoveAt(i);
            for (var i = 0; i < desired.Count; i++)
            {
                if (i < Contacts.Count && string.Equals(Contacts[i].WxId, desired[i].WxId, StringComparison.OrdinalIgnoreCase)) continue;
                if (i < Contacts.Count) Contacts[i] = desired[i]; else Contacts.Add(desired[i]);
            }
        }

        private async void OnSearchClick(object sender, RoutedEventArgs e)
        {
            try
            {
            var keyword = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                ReloadLocal();
                return;
            }

            var local = AppServices.Data.GetContacts()
                .Where(c =>
                    (!string.IsNullOrEmpty(c.DisplayName) && c.DisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.WxId) && c.WxId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.Nickname) && c.Nickname.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            for (var i = Contacts.Count - 1; i >= local.Count; i--) Contacts.RemoveAt(i);
            for (var i = 0; i < local.Count; i++)
            {
                if (i < Contacts.Count) Contacts[i] = local[i]; else Contacts.Add(local[i]);
            }

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
                                AvatarAccentColor = null,
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
            catch (Exception ex)
            {
                await new MessageDialog("搜索失败：" + ex.Message).ShowAsync();
            }
        }
    }
}

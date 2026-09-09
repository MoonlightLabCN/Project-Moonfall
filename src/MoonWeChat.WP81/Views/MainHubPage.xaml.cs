using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using MoonWeChat.Models;
using MoonWeChat.Services;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;

namespace MoonWeChat.Views
{
    /// <summary>
    /// WP 8.1 全景主页（Hub）。会话 / 通讯录 / 发现 / 我 横向滑动，不再用底部 Tab。
    /// </summary>
    public sealed partial class MainHubPage : Page
    {
        public ChatListViewModel ChatList { get; } = new ChatListViewModel();
        public MomentsViewModel Moments { get; } = new MomentsViewModel();
        public ObservableCollection<ChatSession> FilteredSessions { get; } = new ObservableCollection<ChatSession>();
        public ObservableCollection<Contact> Contacts { get; } = new ObservableCollection<Contact>();
        private string _contactQuery;
        public string ContactQuery
        {
            get { return _contactQuery; }
            set
            {
                if (string.Equals(_contactQuery, value, StringComparison.Ordinal)) return;
                _contactQuery = value;
                ReloadFilteredSessions();
                ReloadContacts(_contactQuery);
            }
        }

        private bool _firstShow = true;
        private bool _momentsLoaded;

        public MainHubPage()
        {
            InitializeComponent();
            DataContext = this;
            NavigationCacheMode = NavigationCacheMode.Enabled;
            ChatList.Sessions.CollectionChanged += OnSessionsCollectionChanged;
            // HubSection 内 DataTemplate 有时不继承页 DataContext，显式挂上
            Loaded += (s, e) =>
            {
                if (MainHub == null)
                {
                    return;
                }

                foreach (var section in MainHub.Sections)
                {
                    section.DataContext = this;
                }
            };
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 首次进入做全量；从聊天返回时页面已缓存，只做轻量刷新，避免整页重绑
            if (_firstShow)
            {
                _firstShow = false;
                ChatList.Reload();
                ReloadFilteredSessions();
                ReloadContacts();
            }

            if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
            {
                // 带冷却的自动刷新；顺带把通讯录也重排，否则自动拉回来的
                // 新联系人不会出现在“通讯录”那一段里。
                var ignored = AutoRefreshAsync();
            }

            // 朋友圈只在首次进入时拉一次。以前每次 OnNavigatedTo 都拉，
            // 从聊天页返回也算一次，等于每退出一个会话就多打一轮朋友圈网络请求。
            // 需要最新内容时用底部命令栏的「刷新」。
            if (!_momentsLoaded)
            {
                _momentsLoaded = true;
                // WP8.1 不接受 CoreDispatcherPriority.Idle；用 Normal 投递，仍不阻塞主页首帧。
                var ignoredMoments = Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        try
                        {
                            await Moments.LoadAsync().ConfigureAwait(true);
                        }
                        catch
                        {
                        }
                    });
            }
        }

        private async System.Threading.Tasks.Task AutoRefreshAsync()
        {
            try
            {
                await ChatList.AutoRefreshRemoteAsync().ConfigureAwait(true);
            }
            catch
            {
                // 刷新失败不影响已经画出来的主页
            }

            ReloadContacts();
            ReloadFilteredSessions();
        }

        private void OnSessionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ReloadFilteredSessions();
        }

        private void ReloadFilteredSessions()
        {
            var keyword = (_contactQuery ?? string.Empty).Trim();
            var source = ChatList.Sessions.AsEnumerable();
            if (!string.IsNullOrEmpty(keyword))
            {
                source = source.Where(s =>
                    (!string.IsNullOrEmpty(s.DisplayName) && s.DisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(s.Id) && s.Id.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(s.PreviewText) && s.PreviewText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var desired = source.ToList();
            for (var i = FilteredSessions.Count - 1; i >= desired.Count; i--) FilteredSessions.RemoveAt(i);
            for (var i = 0; i < desired.Count; i++)
            {
                var session = desired[i];
                if (i < FilteredSessions.Count && ReferenceEquals(FilteredSessions[i], session)) continue;
                var existing = FilteredSessions.IndexOf(session);
                if (existing >= 0) FilteredSessions.Move(existing, i);
                else if (i <= FilteredSessions.Count) FilteredSessions.Insert(i, session);
                else FilteredSessions.Add(session);
            }
        }

        private void ReloadContacts(string keyword = null)
        {
            var q = AppServices.Data.GetContacts().AsEnumerable();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                q = q.Where(c =>
                    (!string.IsNullOrEmpty(c.DisplayName) && c.DisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.WxId) && c.WxId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(c.Nickname) && c.Nickname.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var desired = q.OrderBy(x => x.PinyinIndex).ThenBy(x => x.DisplayName).ToList();
            for (var i = Contacts.Count - 1; i >= desired.Count; i--) Contacts.RemoveAt(i);
            for (var i = 0; i < desired.Count; i++)
            {
                var c = desired[i];
                if (i < Contacts.Count && ReferenceEquals(Contacts[i], c))
                    continue;
                var existing = Contacts.FirstOrDefault(x => string.Equals(x.WxId, c.WxId, StringComparison.OrdinalIgnoreCase));
                if (existing != null) Contacts.Remove(existing);
                if (i < Contacts.Count) Contacts[i] = c;
                else if (i <= Contacts.Count) Contacts.Insert(i, c);
                else Contacts.Add(c);
            }
        }

        private void OnSessionClicked(object sender, ItemClickEventArgs e)
        {
            var session = e.ClickedItem as ChatSession;
            if (session != null)
            {
                Frame.Navigate(typeof(ChatPage), session.Id);
            }
        }

        private void OnContactClicked(object sender, ItemClickEventArgs e)
        {
            var contact = e.ClickedItem as Contact;
            if (contact != null)
            {
                Frame.Navigate(typeof(ChatPage), contact.WxId);
            }
        }

        private void OnBannerTapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await ChatList.RefreshRemoteAsync();
                ReloadContacts(ContactQuery);
                await Moments.LoadAsync(true);
            }
            catch (Exception ex)
            {
                await new MessageDialog("刷新失败：" + ex.Message).ShowAsync();
            }
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            // Hub 第二段：通讯录搜索框
            if (MainHub != null && MainHub.Sections.Count > 1)
            {
                MainHub.ScrollToSection(MainHub.Sections[1]);
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ConnectPage));
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private void OnWelcomeClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(WelcomePage));
        }

        private void OnOpenMomentsTapped(object sender, TappedRoutedEventArgs e)
        {
            Frame.Navigate(typeof(MomentsPage));
        }

        private void OnContactSearchClick(object sender, RoutedEventArgs e)
        {
            ReloadContacts(ContactQuery);
        }

        private void OnSectionHeaderClick(object sender, HubSectionHeaderClickEventArgs e)
        {
            // 预留：段标题点击
        }
    }
}

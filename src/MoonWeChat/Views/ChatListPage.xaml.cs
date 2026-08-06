using MoonWeChat.Models;
using MoonWeChat.Services;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    /// <summary>“微信”主页：会话列表 + 底部四个 Tab。</summary>
    public sealed partial class ChatListPage : Page
    {
        public ChatListViewModel ViewModel { get; } = new ChatListViewModel();

        public ChatListPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.Reload();
            TabBar.SetSelectedIndex(0);
            // 进主页再探一次在线，更新横幅
            if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
            {
                _ = ViewModel.RefreshRemoteAsync();
            }
        }

        private void OnBannerTapped(object sender, TappedRoutedEventArgs e)
        {
            // 掉线横幅 → 去重扫（Token 已保存在本机）
            Frame.Navigate(typeof(LoginPage));
        }

        private void OnSessionClicked(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is ChatSession session)
            {
                Frame.Navigate(typeof(ChatPage), session.Id);
            }
        }

        private void OnTabSelected(object sender, int index)
        {
            switch (index)
            {
                case 1:
                    Frame.Navigate(typeof(ContactsPage));
                    break;
                case 2:
                    Frame.Navigate(typeof(MomentsPage));
                    break;
                case 3:
                    // “我” → 连接设置（后端配置 / 登录入口）
                    Frame.Navigate(typeof(SettingsPage));
                    break;
                default:
                    break;
            }
        }

        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            // 后续：本地会话 + 联系人搜索
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ConnectPage));
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshRemoteAsync();
        }

        private void OnMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item)
            {
                switch (item.Text)
                {
                    case "服务器设置":
                        Frame.Navigate(typeof(SettingsPage));
                        break;
                    case "扫码登录":
                        Frame.Navigate(typeof(LoginPage));
                        break;
                    default:
                        break;
                }
            }
        }
    }
}

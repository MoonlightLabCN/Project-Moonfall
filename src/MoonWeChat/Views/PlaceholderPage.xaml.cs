using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    /// <summary>“发现”占位；“我”提供设置/登录入口。</summary>
    public sealed partial class PlaceholderPage : Page
    {
        private string _title = string.Empty;

        public PlaceholderPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _title = e.Parameter as string ?? string.Empty;
            TitleText.Text = _title;

            if (_title == "我")
            {
                HintText.Text = "账号与后端连接";
                MeActions.Visibility = Visibility.Visible;
                TabBar.SetSelectedIndex(3);
            }
            else
            {
                HintText.Text = "“" + _title + "”功能开发中（朋友圈等后续接 WeChatPadPro 接口）";
                MeActions.Visibility = Visibility.Collapsed;
                TabBar.SetSelectedIndex(2);
            }
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private void OnTabSelected(object sender, int index)
        {
            switch (index)
            {
                case 0:
                    Frame.Navigate(typeof(ChatListPage));
                    break;
                case 1:
                    Frame.Navigate(typeof(ContactsPage));
                    break;
                case 2:
                    Frame.Navigate(typeof(MomentsPage));
                    break;
                case 3:
                    Frame.Navigate(typeof(PlaceholderPage), "我");
                    break;
            }
        }
    }
}

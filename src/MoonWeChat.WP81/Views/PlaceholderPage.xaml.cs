using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    /// <summary>功能占位二级页（无底部 Tab）。</summary>
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
                HintText.Text = "账号与后端连接（也可在全景「我」段操作）";
                MeActions.Visibility = Visibility.Visible;
            }
            else
            {
                HintText.Text = "“" + _title + "”功能开发中";
                MeActions.Visibility = Visibility.Collapsed;
            }
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

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SettingsPage));
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }
    }
}

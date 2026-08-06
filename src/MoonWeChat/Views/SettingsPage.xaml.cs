using MoonWeChat.Services;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; } = new SettingsViewModel();

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.ReloadFromStore();
            // PasswordBox / CheckBox(bool?) 在这套 SDK 上不适合直接 x:Bind 字面双向，手动同步
            AdminKeyBox.Password = ViewModel.AdminKey ?? string.Empty;
            AdminKeyBox.PasswordChanged -= OnAdminKeyChanged;
            AdminKeyBox.PasswordChanged += OnAdminKeyChanged;
            UseSampleCheck.IsChecked = ViewModel.UseSampleData;
        }

        private void OnAdminKeyChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.AdminKey = AdminKeyBox.Password;
        }

        private void OnUseSampleClick(object sender, RoutedEventArgs e)
        {
            ViewModel.UseSampleData = UseSampleCheck.IsChecked == true;
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(ChatListPage));
            }
        }

        private void OnGoLoginClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginPage));
        }

        private void OnGoConnectClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ConnectPage));
        }

        private void OnWelcomeClick(object sender, RoutedEventArgs e)
        {
            AppSettings.HasCompletedOnboarding = false;
            Frame.Navigate(typeof(WelcomePage));
        }

        // TextBox 在部分 SDK 上 TwoWay x:Bind 要失焦才回写，跟聊天输入一样手动补一层。
        private void OnBaseUrlChanged(object sender, TextChangedEventArgs e) => ViewModel.BaseUrl = BaseUrlBox.Text;
        private void OnTokenChanged(object sender, TextChangedEventArgs e) => ViewModel.Token = TokenBox.Text;
        private void OnWxIdChanged(object sender, TextChangedEventArgs e) => ViewModel.WxId = WxIdBox.Text;
        private void OnProxyChanged(object sender, TextChangedEventArgs e) => ViewModel.Proxy = ProxyBox.Text;
    }
}

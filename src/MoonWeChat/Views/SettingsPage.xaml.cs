using MoonWeChat.Services;
using MoonWeChat.Services.WeChatPad;
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
            AdminKeyBox.Password = ViewModel.AdminKey ?? string.Empty;
            AdminKeyBox.PasswordChanged -= OnAdminKeyChanged;
            AdminKeyBox.PasswordChanged += OnAdminKeyChanged;
            UseSampleCheck.IsChecked = ViewModel.UseSampleData;
            SyncThemeRadios();
            SyncBackendRadios();
        }

        private void SyncBackendRadios()
        {
            var kind = AppSettings.BackendKind;
            BackendPyRadio.IsChecked = kind == BackendKind.PyWeixin;
            BackendPadRadio.IsChecked = kind == BackendKind.WeChatPadPro;
        }

        private void OnBackendClick(object sender, RoutedEventArgs e)
        {
            var kind = BackendPadRadio.IsChecked == true
                ? BackendKind.WeChatPadPro
                : BackendKind.PyWeixin;
            if (kind == ViewModel.BackendKind)
            {
                return;
            }

            ViewModel.BackendKind = kind;
            AppSettings.BackendKind = kind;
            // 每个后端记住自己的地址，切换时回读该后端的 BaseUrl。
            ViewModel.BaseUrl = AppSettings.BaseUrl;
        }

        private void SyncThemeRadios()
        {
            var theme = AppSettings.VisualTheme;
            ThemeWin10Radio.IsChecked = theme == AppVisualTheme.Win10;
            ThemeWpRadio.IsChecked = theme == AppVisualTheme.WpClassic;
        }

        private void OnThemeClick(object sender, RoutedEventArgs e)
        {
            var theme = ThemeWpRadio.IsChecked == true
                ? AppVisualTheme.WpClassic
                : AppVisualTheme.Win10;
            if (theme == ThemeService.Current)
            {
                return;
            }

            ThemeService.Apply(theme, persist: true);
            var root = Window.Current.Content as Frame;
            if (root != null) { root.Navigate(typeof(ShellPage)); root.BackStack.Clear(); }
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
                AppNavigation.NavigateToMain(Frame);
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

        private void OnBaseUrlChanged(object sender, TextChangedEventArgs e) => ViewModel.BaseUrl = BaseUrlBox.Text;
        private void OnTokenChanged(object sender, TextChangedEventArgs e) => ViewModel.Token = TokenBox.Text;
        private void OnWxIdChanged(object sender, TextChangedEventArgs e) => ViewModel.WxId = WxIdBox.Text;
        private void OnProxyChanged(object sender, TextChangedEventArgs e) => ViewModel.Proxy = ProxyBox.Text;
    }
}

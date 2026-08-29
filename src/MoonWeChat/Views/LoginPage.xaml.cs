using System;
using System.ComponentModel;
using MoonWeChat.Services;
using MoonWeChat.Services.WeChatPad;
using MoonWeChat.ViewModels;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; } = new LoginViewModel();
        private bool _enteringMain;

        public LoginPage()
        {
            InitializeComponent();
            ViewModel.PropertyChanged += OnVmPropertyChanged;
            UpdateQrPlaceholder();
            UpdateEnterButtons();
            UpdateStartButtonLabel();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.BackendKind = AppSettings.BackendKind;
            SyncBackendRadios();

            if (!string.IsNullOrEmpty(AppSettings.BaseUrl))
            {
                ViewModel.ServerUrl = AppSettings.BaseUrl;
            }

            if (!string.IsNullOrEmpty(AppSettings.Token))
            {
                ViewModel.Token = AppSettings.Token;
            }

            if (!string.IsNullOrEmpty(AppSettings.AdminKey))
            {
                ViewModel.AdminKey = AppSettings.AdminKey;
                AdminKeyBox.Password = AppSettings.AdminKey;
            }

            if (!string.IsNullOrEmpty(AppSettings.Proxy))
            {
                ViewModel.Proxy = AppSettings.Proxy;
            }

            // 若本机已登录：显示「直接进入」
            UpdateEnterButtons();
            UpdateStartButtonLabel();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.CancelPolling();
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginViewModel.QrImage) ||
                e.PropertyName == nameof(LoginViewModel.HasQr) ||
                e.PropertyName == nameof(LoginViewModel.StartButtonLabel) ||
                e.PropertyName == nameof(LoginViewModel.IsBusy))
            {
                UpdateQrPlaceholder();
                UpdateStartButtonLabel();
            }

            if (e.PropertyName == nameof(LoginViewModel.IsLoggedIn))
            {
                UpdateEnterButtons();
                if (ViewModel.IsLoggedIn)
                {
                    AppNavigation.NavigateToMain(Frame);
                }
            }

            if (e.PropertyName == nameof(LoginViewModel.ShowAdvanced))
            {
                AdvancedPanel.Visibility = ViewModel.ShowAdvanced
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UpdateQrPlaceholder()
        {
            QrPlaceholder.Visibility = ViewModel.QrImage == null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateEnterButtons()
        {
            var show = ViewModel.IsLoggedIn || AppSettings.IsLoggedIn;
            EnterAppButton.Visibility = ViewModel.IsLoggedIn ? Visibility.Visible : Visibility.Collapsed;
            SkipIfLoggedButton.Visibility = (!ViewModel.IsLoggedIn && AppSettings.IsLoggedIn)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateStartButtonLabel()
        {
            StartButton.Content = ViewModel.StartButtonLabel;
        }

        private void SyncBackendRadios()
        {
            var kind = ViewModel.BackendKind;
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
            ViewModel.ServerUrl = AppSettings.BaseUrl;
        }

        private void OnServerTextChanged(object sender, TextChangedEventArgs e) =>
            ViewModel.ServerUrl = ServerBox.Text ?? string.Empty;

        private void OnTokenTextChanged(object sender, TextChangedEventArgs e) =>
            ViewModel.Token = TokenBox.Text ?? string.Empty;

        private void OnProxyTextChanged(object sender, TextChangedEventArgs e) =>
            ViewModel.Proxy = ProxyBox.Text ?? string.Empty;

        private void OnAdminKeyChanged(object sender, RoutedEventArgs e) =>
            ViewModel.AdminKey = AdminKeyBox.Password ?? string.Empty;

        private void OnToggleAdvancedClick(object sender, RoutedEventArgs e) =>
            ViewModel.ShowAdvanced = !ViewModel.ShowAdvanced;

        private async void OnStartLoginClick(object sender, RoutedEventArgs e)
        {
            try { await ViewModel.StartLoginAsync(); }
            catch (Exception ex) { await ShowErrorAsync("登录失败：" + ex.Message); }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else if (AppSettings.IsLoggedIn)
            {
                AppNavigation.NavigateToMain(Frame);
            }
            else if (!AppSettings.HasCompletedOnboarding)
            {
                Frame.Navigate(typeof(WelcomePage));
            }
            else
            {
                AppNavigation.NavigateToMain(Frame);
            }
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e) =>
            Frame.Navigate(typeof(SettingsPage));

        private void OnEnterAppClick(object sender, RoutedEventArgs e)
        {
            EnterMainPage();
        }

        private static async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            await new ContentDialog { Title = "提示", Content = message, CloseButtonText = "确定" }.ShowAsync();
        }

        private void EnterMainPage()
        {
            if (_enteringMain)
            {
                return;
            }

            _enteringMain = true;
            var frame = Frame;
            try
            {
                AppSettings.UseSampleData = false;
                AppSettings.HasCompletedOnboarding = true;

                // 先投递导航，再重建服务，避免触摸回调里的同步工作吞掉页面切换。
                var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        AppNavigation.NavigateToMain(frame);
                    }
                    catch
                    {
                        _enteringMain = false;
                        return;
                    }

                    try
                    {
                        AppServices.Rebuild();
                        if (AppSettings.IsLoggedIn)
                        {
                            AppServices.Live.StartPolling();
                        }
                    }
                    catch
                    {
                        // 主页可先显示；后续刷新会再次尝试连接服务。
                    }
                });
            }
            catch
            {
                _enteringMain = false;
            }
        }
    }
}

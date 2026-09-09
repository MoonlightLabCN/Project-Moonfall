using System.ComponentModel;
using MoonWeChat.Services;
using MoonWeChat.Services.WeChatPad;
using MoonWeChat.ViewModels;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using System;
using Windows.UI.Popups;

namespace MoonWeChat.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; } = new LoginViewModel();
        private bool _enteringMain;

        public LoginPage()
        {
            InitializeComponent();
            DataContext = this;
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
                    Frame.Navigate(typeof(MainHubPage));
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
            try { await ViewModel.StartLoginAsync(); } catch (Exception ex) { await new MessageDialog("登录失败：" + ex.Message).ShowAsync(); }
        }

        // WP8.1 旧版 XAML 在 ScrollViewer 内偶尔只派发 Tapped，不派发 Command。
        private async void OnStartLoginTapped(object sender, TappedRoutedEventArgs e)
        {
            try { await ViewModel.StartLoginAsync(); } catch (Exception ex) { await new MessageDialog("登录失败：" + ex.Message).ShowAsync(); }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else if (AppSettings.IsLoggedIn)
            {
                Frame.Navigate(typeof(MainHubPage));
            }
            else if (!AppSettings.HasCompletedOnboarding)
            {
                Frame.Navigate(typeof(WelcomePage));
            }
            else
            {
                Frame.Navigate(typeof(MainHubPage));
            }
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e) =>
            Frame.Navigate(typeof(SettingsPage));

        private void OnEnterAppClick(object sender, RoutedEventArgs e)
        {
            EnterMainPage();
        }

        // WP8.1 在 ScrollViewer 内偶尔漏掉 Button.Click；Tapped 作为同一入口的触摸兜底。
        private void OnEnterAppTapped(object sender, TappedRoutedEventArgs e)
        {
            EnterMainPage();
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

                // 先把页面切换投递到下一帧，避免旧版 WP8.1 在触摸回调里重建服务后吞掉导航。
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

                    // 导航成功后再启动真实数据层；即使服务重建失败，主页也能先显示并给出刷新反馈。
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
                        // MainHubPage 会在后台刷新时再次尝试，不能阻断进入主页。
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

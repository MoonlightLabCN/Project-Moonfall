using System.ComponentModel;
using MoonWeChat.Services;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; } = new LoginViewModel();

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
                    Frame.Navigate(typeof(ChatListPage));
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

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else if (AppSettings.IsLoggedIn)
            {
                Frame.Navigate(typeof(ChatListPage));
            }
            else if (!AppSettings.HasCompletedOnboarding)
            {
                Frame.Navigate(typeof(WelcomePage));
            }
            else
            {
                Frame.Navigate(typeof(ChatListPage));
            }
        }

        private void OnOpenSettingsClick(object sender, RoutedEventArgs e) =>
            Frame.Navigate(typeof(SettingsPage));

        private void OnEnterAppClick(object sender, RoutedEventArgs e)
        {
            AppSettings.UseSampleData = false;
            AppServices.Rebuild();
            if (AppSettings.IsLoggedIn)
            {
                AppServices.Live.StartPolling();
            }

            Frame.Navigate(typeof(ChatListPage));
        }
    }
}

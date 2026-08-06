using System;
using System.Threading.Tasks;
using MoonWeChat.Services;
using MoonWeChat.Services.WeChatPad;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class ConnectPage : Page
    {
        private bool _busy;

        public ConnectPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // 回填已有配置
            if (!string.IsNullOrEmpty(AppSettings.BaseUrl))
            {
                ServerBox.Text = AppSettings.BaseUrl;
            }

            TokenBox.Text = AppSettings.Token ?? string.Empty;
            ProxyBox.Text = AppSettings.Proxy ?? string.Empty;
            AdminKeyBox.Password = AppSettings.AdminKey ?? string.Empty;
            UpdateNormalizedHint();
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
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

        private void OnServerChanged(object sender, TextChangedEventArgs e) => UpdateNormalizedHint();
        private void OnTokenChanged(object sender, TextChangedEventArgs e) { }
        private void OnProxyChanged(object sender, TextChangedEventArgs e) { }

        private void UpdateNormalizedHint()
        {
            var normalized = AppSettings.NormalizeBaseUrl(ServerBox.Text);
            NormalizedUrlText.Text = string.IsNullOrEmpty(normalized)
                ? "规范化后：—"
                : "规范化后：" + normalized;
        }

        private async void OnGenTokenClick(object sender, RoutedEventArgs e)
        {
            if (_busy)
            {
                return;
            }

            var baseUrl = AppSettings.NormalizeBaseUrl(ServerBox.Text);
            var adminKey = AdminKeyBox.Password?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(baseUrl))
            {
                SetStatus("请先填写服务器地址。");
                return;
            }

            if (string.IsNullOrEmpty(adminKey))
            {
                SetStatus("生成 Token 需要 AdminKey（服务启动日志里）。");
                return;
            }

            _busy = true;
            SetStatus("正在调用 POST /Admin/GenAuthKey …");
            try
            {
                AppSettings.BaseUrl = baseUrl;
                AppSettings.AdminKey = adminKey;
                AppServices.Rebuild();

                var result = await AppServices.Api.GenAuthKeyAsync(adminKey, 1, 365).ConfigureAwait(true);
                if (!result.Ok)
                {
                    SetStatus("生成失败：" + (result.Message ?? "unknown"));
                    return;
                }

                var token = WeChatPadApiClient.ExtractTokenFromAuthResult(result);
                if (string.IsNullOrEmpty(token))
                {
                    SetStatus("接口返回成功，但未能解析 Token。原始：" + Truncate(result.RawJson, 160));
                    return;
                }

                TokenBox.Text = token;
                AppSettings.Token = token;
                SetStatus("Token 已生成并填入，请点「测试连接」或「保存并去扫码登录」。");
            }
            catch (Exception ex)
            {
                SetStatus("异常：" + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private async void OnTestClick(object sender, RoutedEventArgs e)
        {
            if (_busy)
            {
                return;
            }

            if (!TryPushFields(requireToken: false, out var error))
            {
                SetStatus(error);
                return;
            }

            _busy = true;
            SetStatus("正在探测 " + AppSettings.BaseUrl + " …");
            try
            {
                AppServices.Rebuild();
                var result = await AppServices.Api.TestReachableAsync().ConfigureAwait(true);
                if (result.Ok)
                {
                    SetStatus("连接成功。服务器可达（HTTP " + result.Code + "）。可以保存并去扫码。");
                }
                else
                {
                    SetStatus("连不上：" + result.Message + "\n请确认：① 电脑/云上 WeChatPadPro 已启动 ② 手机与服务器网络互通 ③ 地址端口正确 ④ 防火墙/穿透已放行。");
                }
            }
            catch (Exception ex)
            {
                SetStatus("异常：" + ex.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private async void OnSaveAndLoginClick(object sender, RoutedEventArgs e)
        {
            if (!SaveCore(requireToken: true, out var error))
            {
                SetStatus(error);
                return;
            }

            SetStatus("已保存。正在打开扫码登录…");
            // 给 UI 一帧反馈
            await Task.Delay(100).ConfigureAwait(true);
            Frame.Navigate(typeof(LoginPage));
        }

        private void OnSaveOnlyClick(object sender, RoutedEventArgs e)
        {
            // 允许只存地址、暂无 Token，方便以后填；但会提醒
            if (!SaveCore(requireToken: false, out var error))
            {
                SetStatus(error);
                return;
            }

            // 用户明确想先看 UI：临时进示例，但保留远程配置
            AppSettings.UseSampleData = true;
            AppServices.Rebuild();
            SetStatus("远程配置已保存。当前以示例数据进入，之后可在设置里关掉示例并登录。");
            AppNavigation.NavigateToMain(Frame);
        }

        private bool SaveCore(bool requireToken, out string error)
        {
            if (!TryPushFields(requireToken, out error))
            {
                return false;
            }

            AppSettings.UseSampleData = false;
            AppSettings.HasCompletedOnboarding = true;
            AppServices.Rebuild();
            return true;
        }

        private bool TryPushFields(bool requireToken, out string error)
        {
            error = null;
            var baseUrl = AppSettings.NormalizeBaseUrl(ServerBox.Text);
            if (string.IsNullOrEmpty(baseUrl))
            {
                error = "请填写服务器地址。";
                return false;
            }

            var token = TokenBox.Text?.Trim() ?? string.Empty;
            if (requireToken && string.IsNullOrEmpty(token))
            {
                error = "扫码登录需要 Token。请粘贴或用 AdminKey 生成。";
                return false;
            }

            AppSettings.BaseUrl = baseUrl;
            AppSettings.Token = token;
            AppSettings.Proxy = ProxyBox.Text?.Trim() ?? string.Empty;
            AppSettings.AdminKey = AdminKeyBox.Password?.Trim() ?? string.Empty;
            UpdateNormalizedHint();
            return true;
        }

        private void SetStatus(string text)
        {
            StatusText.Text = text ?? string.Empty;
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= n)
            {
                return s ?? string.Empty;
            }

            return s.Substring(0, n) + "…";
        }
    }
}

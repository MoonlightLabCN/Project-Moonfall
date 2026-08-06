using System;
using System.Threading.Tasks;
using MoonWeChat.Common;
using MoonWeChat.Services;
using MoonWeChat.Services.WeChatPad;

namespace MoonWeChat.ViewModels
{
    public sealed class SettingsViewModel : BindableBase
    {
        private string _baseUrl;
        private string _adminKey;
        private string _token;
        private string _wxId;
        private string _proxy;
        private bool _useSampleData;
        private string _statusText = string.Empty;
        private bool _busy;

        public SettingsViewModel()
        {
            ReloadFromStore();
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => !IsBusy);
            GenTokenCommand = new RelayCommand(async _ => await GenTokenAsync(), _ => !IsBusy);
            TestCommand = new RelayCommand(async _ => await TestAsync(), _ => !IsBusy);
            ClearSessionCommand = new RelayCommand(_ => ClearSession(), _ => !IsBusy);
            OnlineStatusCommand = new RelayCommand(async _ => await RefreshOnlineAsync(), _ => !IsBusy);
        }

        public string BaseUrl
        {
            get => _baseUrl;
            set => SetProperty(ref _baseUrl, value);
        }

        public string AdminKey
        {
            get => _adminKey;
            set => SetProperty(ref _adminKey, value);
        }

        public string Token
        {
            get => _token;
            set => SetProperty(ref _token, value);
        }

        public string WxId
        {
            get => _wxId;
            set => SetProperty(ref _wxId, value);
        }

        public string Proxy
        {
            get => _proxy;
            set => SetProperty(ref _proxy, value);
        }

        public bool UseSampleData
        {
            get => _useSampleData;
            set => SetProperty(ref _useSampleData, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public bool IsBusy
        {
            get => _busy;
            private set
            {
                if (SetProperty(ref _busy, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                    GenTokenCommand.RaiseCanExecuteChanged();
                    TestCommand.RaiseCanExecuteChanged();
                    ClearSessionCommand.RaiseCanExecuteChanged();
                    OnlineStatusCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand GenTokenCommand { get; }
        public RelayCommand TestCommand { get; }
        public RelayCommand ClearSessionCommand { get; }
        public RelayCommand OnlineStatusCommand { get; }

        public void ReloadFromStore()
        {
            BaseUrl = AppSettings.BaseUrl;
            AdminKey = AppSettings.AdminKey;
            Token = AppSettings.Token;
            WxId = AppSettings.WxId;
            Proxy = AppSettings.Proxy;
            UseSampleData = AppSettings.UseSampleData;
        }

        public async Task SaveAsync()
        {
            IsBusy = true;
            try
            {
                AppSettings.BaseUrl = BaseUrl;
                AppSettings.AdminKey = AdminKey;
                AppSettings.Token = Token;
                AppSettings.WxId = WxId;
                AppSettings.Proxy = Proxy;
                AppSettings.UseSampleData = UseSampleData;

                // 回读规范化后的值
                ReloadFromStore();
                AppServices.Rebuild();

                if (!UseSampleData && AppSettings.IsLoggedIn)
                {
                    try
                    {
                        await AppServices.Live.RefreshAsync().ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        StatusText = "已保存，但刷新通讯录失败：" + ex.Message;
                        return;
                    }
                }

                StatusText = UseSampleData
                    ? "已保存：当前使用示例数据"
                    : (AppSettings.IsLoggedIn ? "已保存：真实后端 + 已登录" : "已保存：真实后端，尚未登录（去扫码）");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task GenTokenAsync()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(AdminKey))
            {
                StatusText = "生成 Token 需要 BaseUrl 和 AdminKey";
                return;
            }

            IsBusy = true;
            StatusText = "正在用 AdminKey 生成授权码…";
            try
            {
                AppSettings.BaseUrl = BaseUrl;
                AppSettings.AdminKey = AdminKey;
                AppServices.Rebuild();

                var api = AppServices.Api;
                var result = await api.GenAuthKeyAsync(AdminKey, 1, 365).ConfigureAwait(true);
                if (!result.Ok)
                {
                    StatusText = "生成失败：" + (result.Message ?? result.RawJson);
                    return;
                }

                var token = WeChatPadApiClient.ExtractTokenFromAuthResult(result);
                if (string.IsNullOrEmpty(token))
                {
                    StatusText = "接口成功但未能解析 token，请看服务端返回：" + Truncate(result.RawJson, 200);
                    return;
                }

                Token = token;
                AppSettings.Token = token;
                StatusText = "Token 已生成并填入（请再点保存）";
            }
            catch (Exception ex)
            {
                StatusText = "异常：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task TestAsync()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                StatusText = "请先填写服务器地址";
                return;
            }

            IsBusy = true;
            StatusText = "测试连通性…";
            try
            {
                AppSettings.BaseUrl = BaseUrl;
                AppSettings.Token = Token;
                AppServices.Rebuild();
                var result = await AppServices.Api.TestReachableAsync().ConfigureAwait(true);
                StatusText = result.Ok
                    ? "服务器可达（HTTP " + result.Code + "）"
                    : "连不上：" + result.Message;
            }
            catch (Exception ex)
            {
                StatusText = "异常：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ClearSession()
        {
            AppSettings.ClearSession();
            WxId = string.Empty;
            StatusText = "已清除本机 wxid / 昵称，需要重新扫码登录";
            AppServices.Rebuild();
        }

        public async Task RefreshOnlineAsync()
        {
            if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(Token))
            {
                StatusText = "请先配置 BaseUrl 和 Token";
                return;
            }

            IsBusy = true;
            StatusText = "查询在线状态…";
            try
            {
                AppSettings.BaseUrl = BaseUrl;
                AppSettings.Token = Token;
                AppServices.Rebuild();
                var text = await AppServices.Api.GetOnlineInfoTextAsync().ConfigureAwait(true);
                StatusText = text;
            }
            catch (Exception ex)
            {
                StatusText = "查询失败：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
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

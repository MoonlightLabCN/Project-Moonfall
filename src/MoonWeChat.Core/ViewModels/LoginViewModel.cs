using System;
using System.Threading.Tasks;
using MoonWeChat.Common;
using MoonWeChat.Services;
using MoonWeChat.Services.WeChatPad;
using Windows.UI.Xaml.Media.Imaging;

namespace MoonWeChat.ViewModels
{
    /// <summary>
    /// 登录：服务器地址 + Token → 扫码一次。
    /// 成功后本机记住凭证；服务器长期在线时下次打开 App 自动进会话，无需再扫。
    /// </summary>
    public sealed class LoginViewModel : BindableBase
    {
        private string _serverUrl = AppSettings.BaseUrl ?? string.Empty;
        private string _token = AppSettings.Token ?? string.Empty;
        private string _adminKey = AppSettings.AdminKey ?? string.Empty;
        private string _proxy = AppSettings.Proxy ?? string.Empty;
        private BackendKind _backendKind = AppSettings.BackendKind;
        private string _statusText =
            "填写服务器地址和 Token，点「扫码登录」。登录成功后下次打开客户端会自动进入，不用再管。";
        private string _uuid;
        private bool _busy;
        private bool _loggedIn;
        private bool _showAdvanced;
        private bool _hasQr;
        private BitmapImage _qrImage;
        private bool _pollRunning;
        private int _pollGeneration;

        public string ServerUrl
        {
            get => _serverUrl;
            set
            {
                if (SetProperty(ref _serverUrl, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(NormalizedUrlHint));
                    StartLoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string Token
        {
            get => _token;
            set
            {
                if (SetProperty(ref _token, value ?? string.Empty))
                {
                    StartLoginCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string AdminKey
        {
            get => _adminKey;
            set => SetProperty(ref _adminKey, value ?? string.Empty);
        }

        public string Proxy
        {
            get => _proxy;
            set => SetProperty(ref _proxy, value ?? string.Empty);
        }

        public BackendKind BackendKind
        {
            get => _backendKind;
            set
            {
                if (SetProperty(ref _backendKind, value))
                {
                    OnPropertyChanged(nameof(NormalizedUrlHint));
                    OnPropertyChanged(nameof(BackendLabel));
                }
            }
        }

        public string BackendLabel => BackendKind == BackendKind.WeChatPadPro ? "WeChatPadPro" : "pyweixin";

        public string NormalizedUrlHint
        {
            get
            {
                var n = AppSettings.NormalizeBaseUrl(ServerUrl, BackendKind);
                return string.IsNullOrEmpty(n) ? "规范化后：—" : "规范化后：" + n;
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public BitmapImage QrImage
        {
            get => _qrImage;
            private set
            {
                if (SetProperty(ref _qrImage, value))
                {
                    HasQr = value != null;
                    OnPropertyChanged(nameof(StartButtonLabel));
                }
            }
        }

        public bool HasQr
        {
            get => _hasQr;
            private set => SetProperty(ref _hasQr, value);
        }

        public bool ShowAdvanced
        {
            get => _showAdvanced;
            set => SetProperty(ref _showAdvanced, value);
        }

        public bool IsBusy
        {
            get => _busy;
            private set
            {
                if (SetProperty(ref _busy, value))
                {
                    StartLoginCommand.RaiseCanExecuteChanged();
                    GenTokenCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoggedIn
        {
            get => _loggedIn;
            private set => SetProperty(ref _loggedIn, value);
        }

        public string StartButtonLabel =>
            HasQr && !IsLoggedIn ? "重新获取二维码" : "扫码登录";

        public string PersistHint =>
            AppSettings.IsLoggedIn
                ? "本机已记住登录：" + AppSettings.SelfNickname + " / " + AppSettings.WxId +
                  "。若服务器仍在线，直接返回会话列表即可，无需重扫。"
                : "登录成功后，本机会记住服务器地址、Token 和微信账号。只要服务器长期在线，下次打开 App 自动进入会话。";

        public RelayCommand StartLoginCommand { get; }
        public RelayCommand GenTokenCommand { get; }
        public RelayCommand EnterMainCommand { get; }

        public LoginViewModel()
        {
            if (string.IsNullOrWhiteSpace(_adminKey) &&
                (_serverUrl.IndexOf("127.0.0.1", StringComparison.Ordinal) >= 0 ||
                 _serverUrl.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                _adminKey = "moonwechat_local_2026";
            }

            StartLoginCommand = new RelayCommand(
                async _ => await StartLoginAsync(),
                _ => !IsBusy &&
                     !string.IsNullOrWhiteSpace(ServerUrl) &&
                     !string.IsNullOrWhiteSpace(Token));

            GenTokenCommand = new RelayCommand(
                async _ => await GenTokenAsync(),
                _ => !IsBusy);

            EnterMainCommand = new RelayCommand(_ =>
            {
                // 由页面导航
            });
        }

        public void CancelPolling()
        {
            _pollGeneration++;
            _uuid = null;
            _pollRunning = false;
        }

        public async Task GenTokenAsync()
        {
            if (IsBusy)
            {
                return;
            }

            var baseUrl = AppSettings.NormalizeBaseUrl(ServerUrl, BackendKind);
            if (string.IsNullOrEmpty(baseUrl))
            {
                StatusText = "请先填写服务器地址。";
                return;
            }

            if (string.IsNullOrWhiteSpace(AdminKey))
            {
                StatusText = "生成 Token 需要 AdminKey（服务端 .env 的 ADMIN_KEY）。";
                ShowAdvanced = true;
                return;
            }

            IsBusy = true;
            try
            {
                AppSettings.BackendKind = BackendKind;
                AppSettings.BaseUrl = baseUrl;
                AppSettings.AdminKey = AdminKey.Trim();
                AppServices.Rebuild();
                StatusText = "正在生成 Token…";

                var result = await AppServices.Api.GenAuthKeyAsync(AppSettings.AdminKey, 1, 365)
                    .ConfigureAwait(true);
                if (!result.Ok)
                {
                    StatusText = "生成失败：" + (result.Message ?? "unknown");
                    return;
                }

                var token = WeChatPadApiClient.ExtractTokenFromAuthResult(result);
                if (string.IsNullOrEmpty(token))
                {
                    StatusText = "接口成功但未能解析 Token。";
                    return;
                }

                Token = token;
                AppSettings.Token = token;
                AppServices.Rebuild();
                StatusText = "Token 已生成并填入。点「扫码登录」即可。";
            }
            catch (Exception ex)
            {
                StatusText = "生成异常：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>保存服务器+Token → 取码 → 轮询；成功后本机持久化，下次免扫。</summary>
        public async Task StartLoginAsync()
        {
            if (IsBusy)
            {
                return;
            }

            var baseUrl = AppSettings.NormalizeBaseUrl(ServerUrl, BackendKind);
            var token = (Token ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(baseUrl))
            {
                StatusText = "请填写服务器地址。";
                return;
            }

            if (string.IsNullOrEmpty(token))
            {
                StatusText = "请填写 Token。可在高级选项用 AdminKey 生成。";
                ShowAdvanced = true;
                return;
            }

            IsBusy = true;
            IsLoggedIn = false;
            CancelPolling();
            QrImage = null;

            try
            {
                // 持久化连接信息（扫码前就记住，方便下次）
                AppSettings.BackendKind = BackendKind;
                AppSettings.BaseUrl = baseUrl;
                AppSettings.Token = token;
                AppSettings.AdminKey = (AdminKey ?? string.Empty).Trim();
                AppSettings.Proxy = (Proxy ?? string.Empty).Trim();
                AppSettings.UseSampleData = false;
                AppSettings.HasCompletedOnboarding = true;
                ServerUrl = AppSettings.BaseUrl;
                Token = AppSettings.Token;
                AppServices.Rebuild();

                StatusText = "正在连接 " + AppSettings.BaseUrl + " (" + AppSettings.BackendLabel + ") …";
                var ping = await AppServices.Api.TestReachableAsync().ConfigureAwait(true);
                if (!ping.Ok)
                {
                    StatusText = "连不上服务器：" + (ping.Message ?? "") +
                                 "。请确认 " + AppSettings.BackendLabel + " 服务在跑，且地址可访问。";
                    return;
                }

                // 若本 Token 已在线，直接免扫进入
                StatusText = "检查该 Token 是否已在线…";
                var probe = await AppServices.Api.ProbeDeviceSessionAsync().ConfigureAwait(true);
                if (probe.Online)
                {
                    SessionBootstrap.MarkLoggedIn(
                        string.IsNullOrEmpty(probe.WxId) ? AppSettings.WxId : probe.WxId,
                        string.IsNullOrEmpty(probe.Nickname) ? AppSettings.SelfNickname : probe.Nickname);

                    if (string.IsNullOrWhiteSpace(AppSettings.WxId))
                    {
                        AppSettings.WxId = "online";
                    }

                    IsLoggedIn = true;
                    StatusText = "该 Token 已在服务器在线，无需扫码，直接进入。";
                    OnPropertyChanged(nameof(PersistHint));
                    return;
                }

                StatusText = "正在获取登录二维码…";
                var qr = await AppServices.Api.GetLoginQrAsync(AppSettings.Proxy).ConfigureAwait(true);
                if (!qr.Ok)
                {
                    StatusText = "获取二维码失败：" + (qr.Message ?? "未知") +
                                 "。请检查 Token 是否有效。";
                    return;
                }

                _uuid = qr.Uuid;
                await LoadQrImageAsync(qr).ConfigureAwait(true);
                OnPropertyChanged(nameof(StartButtonLabel));

                if (QrImage == null)
                {
                    StatusText = "已建立登录会话，但二维码图解析失败。可重试。";
                }
                else
                {
                    StatusText = "请用另一台手机微信扫本机二维码，并点确认登录。成功后下次打开 App 将自动进入。";
                }

                if (!string.IsNullOrEmpty(_uuid))
                {
                    var gen = _pollGeneration;
                    _ = PollLoopAsync(_uuid, gen);
                }
            }
            catch (Exception ex)
            {
                StatusText = "异常：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(StartButtonLabel));
            }
        }

        private async Task LoadQrImageAsync(QrLoginResult qr)
        {
            try
            {
                if (!string.IsNullOrEmpty(qr.QrBase64))
                {
                    var stream = await WeChatPadApiClient.Base64ToStreamAsync(qr.QrBase64).ConfigureAwait(true);
                    if (stream != null)
                    {
                        var bmp = new BitmapImage();
                        await bmp.SetSourceAsync(stream);
                        QrImage = bmp;
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(qr.QrUrl))
                {
                    QrImage = new BitmapImage(new Uri(qr.QrUrl));
                    return;
                }

                if (!string.IsNullOrEmpty(qr.QrContent) &&
                    qr.QrContent.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var url = "https://api.pwmqr.com/qrcode/create/?url=" +
                              Uri.EscapeDataString(qr.QrContent);
                    QrImage = new BitmapImage(new Uri(url));
                }
            }
            catch (Exception ex)
            {
                StatusText = "二维码图片加载失败：" + ex.Message;
            }
        }

        private async Task PollLoopAsync(string uuid, int generation)
        {
            if (_pollRunning)
            {
                return;
            }

            _pollRunning = true;
            try
            {
                var api = AppServices.Api;
                for (var i = 0; i < 90; i++)
                {
                    await Task.Delay(2000).ConfigureAwait(true);
                    if (generation != _pollGeneration || _uuid != uuid)
                    {
                        return;
                    }

                    LoginStatusResult status;
                    try
                    {
                        status = await api.CheckLoginStatusAsync(uuid).ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        StatusText = "轮询异常：" + ex.Message;
                        continue;
                    }

                    switch (status.State)
                    {
                        case QrScanState.Waiting:
                            StatusText = "等待扫码…";
                            break;
                        case QrScanState.Scanned:
                            StatusText = "已扫码，请在手机上点确认登录";
                            break;
                        case QrScanState.Expired:
                            StatusText = "二维码已过期，请再点「扫码登录」";
                            QrImage = null;
                            return;
                        case QrScanState.Confirmed:
                            var wxid = status.WxId;
                            var nick = status.Nickname;
                            if (string.IsNullOrWhiteSpace(wxid) && !string.IsNullOrWhiteSpace(nick))
                            {
                                wxid = "session:" + nick;
                            }

                            if (string.IsNullOrWhiteSpace(wxid))
                            {
                                wxid = "logged_in";
                            }

                            // 关键：本机持久化，下次免登
                            SessionBootstrap.MarkLoggedIn(wxid, nick);

                            try
                            {
                                StatusText = "正在初始化会话…";
                                await api.NewInitAsync().ConfigureAwait(true);
                            }
                            catch
                            {
                            }

                            try
                            {
                                await AppServices.Live.RefreshAsync().ConfigureAwait(true);
                            }
                            catch
                            {
                            }

                            IsLoggedIn = true;
                            StatusText = "登录成功：" + AppSettings.SelfNickname + " (" + AppSettings.WxId +
                                         ")。已记住，下次打开直接进。";
                            OnPropertyChanged(nameof(PersistHint));
                            return;
                        case QrScanState.Failed:
                            StatusText = string.IsNullOrEmpty(status.Message)
                                ? "登录失败，请重试"
                                : status.Message;
                            break;
                        default:
                            if (!string.IsNullOrEmpty(status.Message))
                            {
                                StatusText = status.Message;
                            }

                            break;
                    }
                }

                StatusText = "等待超时，请重新获取二维码";
            }
            finally
            {
                _pollRunning = false;
            }
        }
    }
}

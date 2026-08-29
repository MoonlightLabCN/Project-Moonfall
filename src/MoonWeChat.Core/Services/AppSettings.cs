using System;
using MoonWeChat.Services.WeChatPad;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 本地设置。WP 端永远是“瘦客户端”，本机只存远程地址 + Token + 登录态。
    /// 支持双后端：pyweixin 网关与 WeChatPadPro。
    /// </summary>
    public static class AppSettings
    {
        private const string Prefix = "moon.";

        private static IPropertySet Values
        {
            get
            {
                try
                {
                    return ApplicationData.Current.LocalSettings.Values;
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 是否走过欢迎引导（点过「连接远程」或「示例数据」）。
        /// false 时启动落在 WelcomePage。
        /// </summary>
        public static bool HasCompletedOnboarding
        {
            get => GetBool("hasOnboarded", false);
            set => Set("hasOnboarded", value);
        }

        /// <summary>
        /// true = 示例数据；false = 连所选后端。
        /// 默认 true，方便无服务器时预览 UI。
        /// </summary>
        public static bool UseSampleData
        {
            get => GetBool("useSampleData", true);
            set => Set("useSampleData", value);
        }

        /// <summary>当前后端协议。默认 pyweixin，保留旧版本行为。</summary>
        public static BackendKind BackendKind
        {
            get
            {
                var raw = GetString("backendKind", "PyWeixin");
                if (string.Equals(raw, "WeChatPadPro", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase))
                {
                    return WeChatPad.BackendKind.WeChatPadPro;
                }

                return WeChatPad.BackendKind.PyWeixin;
            }
            set => Set("backendKind", value == WeChatPad.BackendKind.WeChatPadPro
                ? "WeChatPadPro"
                : "PyWeixin");
        }

        /// <summary>当前后端的显示名。</summary>
        public static string BackendLabel =>
            BackendKind == WeChatPad.BackendKind.WeChatPadPro ? "WeChatPadPro" : "pyweixin";

        /// <summary>当前后端的默认端口。</summary>
        public static int DefaultBackendPort =>
            BackendKind == WeChatPad.BackendKind.WeChatPadPro ? 8062 : 18765;

        /// <summary>
        /// 远程后端根地址（不含 /api，provider 会按协议自动补）。
        /// 例：pyweixin → http://192.168.1.10:18765；WeChatPadPro → http://192.168.1.10:8062
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                var key = BaseUrlKey(BackendKind);
                var fallback = BackendKind == WeChatPad.BackendKind.WeChatPadPro
                    ? "http://127.0.0.1:8062"
                    : "http://127.0.0.1:18765";
                return GetString(key, fallback);
            }
            set => Set(BaseUrlKey(BackendKind), NormalizeBaseUrl(value));
        }

        /// <summary>服务启动日志里的 adminKey / 管理后台凭证，仅用于生成 Token。</summary>
        public static string AdminKey
        {
            get => GetString("adminKey", string.Empty);
            set => Set("adminKey", (value ?? string.Empty).Trim());
        }

        /// <summary>业务鉴权码（pyweixin 网关 token / WeChatPadPro Access Token）。</summary>
        public static string Token
        {
            get => GetString("token", string.Empty);
            set => Set("token", (value ?? string.Empty).Trim());
        }

        /// <summary>扫码登录后的 wxid。</summary>
        public static string WxId
        {
            get => GetString("wxId", string.Empty);
            set => Set("wxId", (value ?? string.Empty).Trim());
        }

        public static string SelfNickname
        {
            get => GetString("selfNickname", "我");
            set => Set("selfNickname", string.IsNullOrWhiteSpace(value) ? "我" : value.Trim());
        }

        /// <summary>可选 HTTP 代理（云服务器登录时可能需要）。</summary>
        public static string Proxy
        {
            get => GetString("proxy", string.Empty);
            set => Set("proxy", (value ?? string.Empty).Trim());
        }

        /// <summary>
        /// 界面配色：Win10（微信绿）或 WpClassic（黑底青绿）。
        /// 默认随编译平台：WP 壳默认经典，UWP 壳默认 Win10。
        /// </summary>
        public static AppVisualTheme VisualTheme
        {
            get
            {
#if WINDOWS_PHONE_APP
                var defaultTheme = "WpClassic";
#else
                var defaultTheme = "Win10";
#endif
                var raw = GetString("visualTheme", defaultTheme);
                if (string.Equals(raw, "WpClassic", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase))
                {
                    return AppVisualTheme.WpClassic;
                }

                return AppVisualTheme.Win10;
            }
            set => Set("visualTheme", value == AppVisualTheme.WpClassic ? "WpClassic" : "Win10");
        }

        /// <summary>至少填了远程地址 + Token。</summary>
        public static bool IsRemoteConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token);

        /// <summary>已配置远程且有 wxid。</summary>
        public static bool IsLoggedIn =>
            IsRemoteConfigured && !string.IsNullOrWhiteSpace(WxId);

        /// <summary>
        /// 把用户输入规范成 BaseUrl：
        /// - 已是 http(s)://… → 去尾斜杠（WeChatPadPro 若带 /api 也会去掉，provider 会自动补）
        /// - host:port → 默认补 http://
        /// - 纯 host → 按当前后端补默认端口（pyweixin 18765 / WeChatPadPro 8062）
        /// </summary>
        public static string NormalizeBaseUrl(string raw, BackendKind? kind = null)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var backend = kind ?? BackendKind;
            var s = raw.Trim().TrimEnd('/');
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (backend == WeChatPad.BackendKind.WeChatPadPro &&
                    s.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - 4).TrimEnd('/');
                }

                return s;
            }

            // 避免用户手滑写成 //host
            s = s.TrimStart('/');

            if (s.Contains(":"))
            {
                return "http://" + s;
            }

            return "http://" + s + ":" + (backend == WeChatPad.BackendKind.WeChatPadPro ? 8062 : 18765);
        }

        /// <summary>从 BaseUrl 拆出用于 UI 显示的 host 部分。</summary>
        public static string DisplayHost
        {
            get
            {
                var url = BaseUrl;
                if (string.IsNullOrEmpty(url))
                {
                    return string.Empty;
                }

                try
                {
                    var u = new Uri(url);
                    return u.IsDefaultPort ? u.Host : (u.Host + ":" + u.Port);
                }
                catch
                {
                    return url;
                }
            }
        }

        private static string BaseUrlKey(BackendKind kind) =>
            kind == BackendKind.WeChatPadPro ? "baseUrl.wechatpadpro" : "baseUrl.pyweixin";

        /// <summary>
        /// 明确把地址写进指定后端的槽位。
        /// 设置页在「同时改后端和地址」时必须用这个：BaseUrl 的 setter 依赖当前 BackendKind，
        /// 先后顺序不同会把地址存错槽，用户输入的新地址就丢了。
        /// </summary>
        public static void SetBaseUrlFor(BackendKind kind, string value)
        {
            Set(BaseUrlKey(kind), NormalizeBaseUrl(value, kind));
        }

        /// <summary>读取指定后端槽位里的地址（不受当前 BackendKind 影响）。</summary>
        public static string GetBaseUrlFor(BackendKind kind)
        {
            var fallback = kind == WeChatPad.BackendKind.WeChatPadPro
                ? "http://127.0.0.1:8062"
                : "http://127.0.0.1:18765";
            return GetString(BaseUrlKey(kind), fallback);
        }


        /// <summary>
        /// 仅清本机「已扫码」标记。保留 BaseUrl + Token，
        /// 方便服务器掉线后同一 Token 重新扫码，无需重配。
        /// </summary>
        public static void ClearSession()
        {
            WxId = string.Empty;
            SelfNickname = "我";
        }

        /// <summary>彻底忘掉远程（含 Token）。一般不用。</summary>
        public static void ClearRemoteCredentials()
        {
            ClearSession();
            Token = string.Empty;
            // 保留 BaseUrl / AdminKey，方便重连同一台服务器
        }

        private static string GetString(string key, string defaultValue)
        {
            var bag = Values;
            if (bag == null)
            {
                return defaultValue;
            }

            return bag[Prefix + key] as string ?? defaultValue;
        }

        private static bool GetBool(string key, bool defaultValue)
        {
            var bag = Values;
            if (bag == null)
            {
                return defaultValue;
            }

            var raw = bag[Prefix + key];
            if (raw is bool b)
            {
                return b;
            }

            if (raw is string s && bool.TryParse(s, out var parsed))
            {
                return parsed;
            }

            return defaultValue;
        }

        private static void Set(string key, object value)
        {
            var bag = Values;
            if (bag == null)
            {
                return;
            }

            bag[Prefix + key] = value;
        }
    }
}

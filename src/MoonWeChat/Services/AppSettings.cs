using System;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 本地设置。WP 端永远是“瘦客户端”，WeChatPadPro 跑在电脑/云上，
    /// 本机只存远程地址 + Token + 登录态。
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
        /// true = 示例数据；false = 连远程 WeChatPadPro。
        /// 默认 true，方便无服务器时预览 UI。
        /// </summary>
        public static bool UseSampleData
        {
            get => GetBool("useSampleData", true);
            set => Set("useSampleData", value);
        }

        /// <summary>
        /// 远程 WeChatPadPro 根地址。
        /// 例：http://192.168.1.10:1239 或 https://wx.example.com
        /// 默认空——WP 上不要默认 localhost（手机访问不到本机）。
        /// </summary>
        public static string BaseUrl
        {
            // 本机 Docker 默认端口 1238（见 _wechatpadpro/WeChatPadPro/deploy）
            get => GetString("baseUrl", "http://127.0.0.1:1238");
            set => Set("baseUrl", NormalizeBaseUrl(value));
        }

        /// <summary>服务启动日志里的 adminKey，仅用于生成 Token。</summary>
        public static string AdminKey
        {
            get => GetString("adminKey", string.Empty);
            set => Set("adminKey", (value ?? string.Empty).Trim());
        }

        /// <summary>业务鉴权码（knowhub 文档里的 TokenKey / Device Token）。</summary>
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

        /// <summary>至少填了远程地址 + Token。</summary>
        public static bool IsRemoteConfigured =>
            !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token);

        /// <summary>已配置远程且有 wxid。</summary>
        public static bool IsLoggedIn =>
            IsRemoteConfigured && !string.IsNullOrWhiteSpace(WxId);

        /// <summary>
        /// 把用户输入规范成 BaseUrl：
        /// - 已是 http(s)://… → 去尾斜杠
        /// - host:port → 默认补 http://
        /// - 纯 host → 默认 http://host:1239
        /// </summary>
        public static string NormalizeBaseUrl(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var s = raw.Trim().TrimEnd('/');
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }

            // 避免用户手滑写成 //host
            s = s.TrimStart('/');

            if (s.Contains(":"))
            {
                return "http://" + s;
            }

            // 本机 Docker compose 默认 1238；纯 host 时补该端口
            return "http://" + s + ":1238";
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

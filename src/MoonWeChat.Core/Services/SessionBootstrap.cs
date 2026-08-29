using System;
using System.Threading.Tasks;
using MoonWeChat.Services.WeChatPad;

namespace MoonWeChat.Services
{
    public enum SessionOnlineState
    {
        /// <summary>未配置远程或示例模式，无需探测。</summary>
        NotApplicable = 0,
        /// <summary>本机已记住登录，且服务器上该 Token 仍在线。</summary>
        Online = 1,
        /// <summary>本机有凭证，但服务器会话已掉（需重新扫码，Token 可保留）。</summary>
        NeedRescan = 2,
        /// <summary>连不上服务器。</summary>
        Unreachable = 3,
        /// <summary>本机尚未完成扫码（无 wxid）。</summary>
        NotLoggedInLocally = 4
    }

    public sealed class SessionProbeResult
    {
        public SessionOnlineState State { get; set; }
        public string Message { get; set; }
        public string WxId { get; set; }
        public string Nickname { get; set; }
    }

    /// <summary>
    /// 启动/恢复时的会话引导：本机记住 BaseUrl+Token+WxId 后免二次扫码；
    /// 服务器长期在线则直接进会话；仅当服务端掉线才提示重扫。
    /// </summary>
    public static class SessionBootstrap
    {
        public static SessionOnlineState LastState { get; private set; } = SessionOnlineState.NotApplicable;
        public static string LastMessage { get; private set; } = string.Empty;

        /// <summary>会话列表顶部横幅：仅 NeedRescan / Unreachable 时有文案。</summary>
        public static string BannerText
        {
            get
            {
                switch (LastState)
                {
                    case SessionOnlineState.NeedRescan:
                        return string.IsNullOrEmpty(LastMessage)
                            ? "服务器上的微信会话已掉线，点此重新扫码（Token 不用改）"
                            : LastMessage;
                    case SessionOnlineState.Unreachable:
                        return string.IsNullOrEmpty(LastMessage)
                            ? "连不上服务器，请确认电脑上的 pyweixin 网关仍在运行"
                            : LastMessage;
                    default:
                        return string.Empty;
                }
            }
        }

        public static bool ShowBanner =>
            LastState == SessionOnlineState.NeedRescan ||
            LastState == SessionOnlineState.Unreachable;

        /// <summary>
        /// 探测并（在线时）拉起轮询/刷新。可在启动后后台调用。
        /// </summary>
        public static async Task<SessionProbeResult> EnsureSessionAsync(bool startPollingIfOnline = true)
        {
            var result = new SessionProbeResult { State = SessionOnlineState.NotApplicable };

            if (AppSettings.UseSampleData)
            {
                LastState = SessionOnlineState.NotApplicable;
                LastMessage = string.Empty;
                result.State = SessionOnlineState.NotApplicable;
                result.Message = "示例模式";
                return result;
            }

            if (!AppSettings.IsRemoteConfigured)
            {
                LastState = SessionOnlineState.NotLoggedInLocally;
                LastMessage = "尚未配置服务器与 Token";
                result.State = SessionOnlineState.NotLoggedInLocally;
                result.Message = LastMessage;
                return result;
            }

            if (!AppSettings.IsLoggedIn)
            {
                LastState = SessionOnlineState.NotLoggedInLocally;
                LastMessage = "本机尚未扫码登录";
                result.State = SessionOnlineState.NotLoggedInLocally;
                result.Message = LastMessage;
                return result;
            }

            try
            {
                AppServices.Rebuild();
                var api = AppServices.Api;

                // 1) 先探测 HTTP 可达
                var ping = await api.TestReachableAsync().ConfigureAwait(true);
                if (!ping.Ok)
                {
                    result.State = SessionOnlineState.Unreachable;
                    result.Message = "连不上 " + AppSettings.BaseUrl + "：" + (ping.Message ?? "");
                    LastState = result.State;
                    LastMessage = result.Message;
                    return result;
                }

                // 2) 问服务器：这个 Token 的微信是否还在线
                var probe = await api.ProbeDeviceSessionAsync().ConfigureAwait(true);
                result.WxId = probe.WxId;
                result.Nickname = probe.Nickname;

                if (probe.Online)
                {
                    if (!string.IsNullOrEmpty(probe.WxId))
                    {
                        AppSettings.WxId = probe.WxId;
                    }

                    if (!string.IsNullOrEmpty(probe.Nickname))
                    {
                        AppSettings.SelfNickname = probe.Nickname;
                    }

                    AppSettings.UseSampleData = false;
                    AppServices.Rebuild();

                    if (startPollingIfOnline)
                    {
                        AppServices.Live.StartPolling();
                        try
                        {
                            await AppServices.Live.RefreshAsync().ConfigureAwait(true);
                        }
                        catch
                        {
                            // 刷新失败不阻断“在线”
                        }
                    }

                    result.State = SessionOnlineState.Online;
                    result.Message = "已自动恢复会话 · " +
                                     (AppSettings.SelfNickname ?? "") + " · " +
                                     (AppSettings.WxId ?? "");
                    LastState = result.State;
                    LastMessage = result.Message;
                    return result;
                }

                // 3) 明确离线：尝试唤醒一次（部分部署支持）
                try
                {
                    await api.AwakenAsync().ConfigureAwait(true);
                    await Task.Delay(800).ConfigureAwait(true);
                    probe = await api.ProbeDeviceSessionAsync().ConfigureAwait(true);
                    if (probe.Online)
                    {
                        if (!string.IsNullOrEmpty(probe.WxId))
                        {
                            AppSettings.WxId = probe.WxId;
                        }

                        if (!string.IsNullOrEmpty(probe.Nickname))
                        {
                            AppSettings.SelfNickname = probe.Nickname;
                        }

                        AppServices.Rebuild();
                        if (startPollingIfOnline)
                        {
                            AppServices.Live.StartPolling();
                            try
                            {
                                await AppServices.Live.RefreshAsync().ConfigureAwait(true);
                            }
                            catch
                            {
                            }
                        }

                        result.State = SessionOnlineState.Online;
                        result.Message = "唤醒成功 · " + (AppSettings.WxId ?? "");
                        LastState = result.State;
                        LastMessage = result.Message;
                        return result;
                    }
                }
                catch
                {
                    // ignore awaken errors
                }

                result.State = SessionOnlineState.NeedRescan;
                result.Message = string.IsNullOrEmpty(probe.Message)
                    ? "服务器上微信已掉线，请重新扫码（服务器地址和 Token 不用改）"
                    : probe.Message;
                LastState = result.State;
                LastMessage = result.Message;
                return result;
            }
            catch (Exception ex)
            {
                result.State = SessionOnlineState.Unreachable;
                result.Message = "探测异常：" + ex.Message;
                LastState = result.State;
                LastMessage = result.Message;
                return result;
            }
        }

        /// <summary>登录成功后调用：标记本机已登录并拉起轮询。</summary>
        public static void MarkLoggedIn(string wxId, string nickname)
        {
            if (!string.IsNullOrWhiteSpace(wxId))
            {
                AppSettings.WxId = wxId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(nickname))
            {
                AppSettings.SelfNickname = nickname.Trim();
            }

            AppSettings.UseSampleData = false;
            AppSettings.HasCompletedOnboarding = true;
            AppServices.Rebuild();
            if (AppSettings.IsLoggedIn)
            {
                AppServices.Live.StartPolling();
            }

            LastState = SessionOnlineState.Online;
            LastMessage = "登录成功 · " + (AppSettings.SelfNickname ?? "") + " · " + (AppSettings.WxId ?? "");
        }
    }
}

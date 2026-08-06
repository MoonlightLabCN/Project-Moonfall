using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MoonWeChat.Common;
using MoonWeChat.Models;
using MoonWeChat.Services;

namespace MoonWeChat.ViewModels
{
    /// <summary>
    /// “微信”主页（会话列表）。数据来自 AppServices.Data（示例或实机）。
    /// </summary>
    public class ChatListViewModel : BindableBase
    {
        public ObservableCollection<ChatSession> Sessions { get; } = new ObservableCollection<ChatSession>();

        private string _subtitle;
        public string Subtitle
        {
            get => _subtitle;
            set => SetProperty(ref _subtitle, value);
        }

        private string _bannerText = string.Empty;
        public string BannerText
        {
            get => _bannerText;
            set
            {
                if (SetProperty(ref _bannerText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(ShowBanner));
                }
            }
        }

        public bool ShowBanner => !string.IsNullOrEmpty(BannerText);

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public ChatListViewModel()
        {
            AppServices.Data.SessionsChanged += OnSessionsChanged;
            Reload();
        }

        public void Reload()
        {
            AppServices.Data.SessionsChanged -= OnSessionsChanged;
            AppServices.Data.SessionsChanged += OnSessionsChanged;

            Sessions.Clear();
            var ordered = AppServices.Data.GetSessions()
                .OrderByDescending(s => s.IsPinned)
                .ThenByDescending(LastActivity);

            foreach (var session in ordered)
            {
                Sessions.Add(session);
            }

            if (AppServices.Data.IsSample)
            {
                Subtitle = "示例数据";
            }
            else if (AppSettings.IsLoggedIn)
            {
                Subtitle = (AppSettings.SelfNickname ?? "") + " · " + AppSettings.WxId;
            }
            else
            {
                Subtitle = "未登录 · 点右上角配置";
            }

            // 同步启动探测留下的横幅
            BannerText = SessionBootstrap.ShowBanner ? SessionBootstrap.BannerText : string.Empty;
        }

        public async Task RefreshRemoteAsync()
        {
            if (AppServices.Data.IsSample)
            {
                Reload();
                return;
            }

            IsRefreshing = true;
            try
            {
                var probe = await SessionBootstrap.EnsureSessionAsync(startPollingIfOnline: true)
                    .ConfigureAwait(true);

                if (probe.State == SessionOnlineState.Online)
                {
                    BannerText = string.Empty;
                    Subtitle = (AppSettings.SelfNickname ?? "") + " · " + AppSettings.WxId;
                }
                else if (probe.State == SessionOnlineState.NeedRescan ||
                         probe.State == SessionOnlineState.Unreachable)
                {
                    BannerText = probe.Message ?? SessionBootstrap.BannerText;
                }
                else
                {
                    await AppServices.Data.RefreshAsync().ConfigureAwait(true);
                }

                Reload();
            }
            catch (Exception ex)
            {
                Subtitle = "刷新失败：" + ex.Message;
                BannerText = "刷新失败：" + ex.Message;
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void OnSessionsChanged(object sender, EventArgs e)
        {
            Reload();
        }

        private static DateTimeOffset LastActivity(ChatSession session)
        {
            return session.Messages.Count > 0
                ? session.Messages[session.Messages.Count - 1].Timestamp
                : DateTimeOffset.MinValue;
        }
    }
}

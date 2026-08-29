using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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

        private int _reloadEpoch;

        private IChatDataService _subscribed;

        public ChatListViewModel()
        {
            Reload();
        }

        public void Reload()
        {
            // 数据源可能已经被 AppServices.Rebuild() 换掉（示例↔真实、换后端）。
            // 必须从「上一次订阅的那个实例」上退订，而不是从当前实例上退订，
            // 否则旧数据源会一直挂着这个 handler。
            var data = AppServices.Data;
            if (!ReferenceEquals(_subscribed, data))
            {
                if (_subscribed != null)
                {
                    _subscribed.SessionsChanged -= OnSessionsChanged;
                }

                data.SessionsChanged += OnSessionsChanged;
                _subscribed = data;
            }

            var ordered = AppServices.Data.GetSessions()
                .OrderByDescending(s => s.IsPinned)
                .ThenByDescending(LastActivity)
                .ToList();

            // 原地增量对齐，不做 Clear()+Add()。
            // ChatSession 现在实现了 INotifyPropertyChanged，行内的未读角标/预览/时间
            // 会自己刷新，这里只需要处理「顺序变了」和「多了/少了会话」。
            // 整表重建会销毁全部容器并把滚动位置打回顶部——轮询每几秒触发一次时非常明显。
            for (int i = 0; i < ordered.Count; i++)
            {
                var session = ordered[i];
                if (i < Sessions.Count && ReferenceEquals(Sessions[i], session))
                {
                    continue;
                }

                var existing = IndexOfSession(session, i);
                if (existing >= 0)
                {
                    Sessions.Move(existing, i);
                }
                else
                {
                    Sessions.Insert(i, session);
                }
            }

            while (Sessions.Count > ordered.Count)
            {
                Sessions.RemoveAt(Sessions.Count - 1);
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

        /// <summary>
        /// 自动刷新（进主页 / 从聊天返回）的最小间隔。
        /// EnsureSessionAsync 一轮要打十几个 HTTP 并重启轮询定时器，
        /// 每次退出会话都跑一遍会把 Lumia 拖到明显卡顿。用户手点刷新不受这个限制。
        /// </summary>
        private static readonly TimeSpan AutoRefreshCooldown = TimeSpan.FromSeconds(60);

        private static DateTimeOffset _lastAutoRefreshAt = DateTimeOffset.MinValue;

        /// <summary>页面 OnNavigatedTo 里调这个：太频繁就只做本地重排，不打网络。</summary>
        public async Task AutoRefreshRemoteAsync()
        {
            if (AppServices.Data.IsSample)
            {
                Reload();
                return;
            }

            if (DateTimeOffset.Now - _lastAutoRefreshAt < AutoRefreshCooldown)
            {
                Reload();
                return;
            }

            await RefreshRemoteAsync().ConfigureAwait(true);
        }

        public async Task RefreshRemoteAsync()
        {
            if (AppServices.Data.IsSample)
            {
                Reload();
                return;
            }

            _lastAutoRefreshAt = DateTimeOffset.Now;
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

        private async void OnSessionsChanged(object sender, EventArgs e)
        {
            // 合并短时间连发的会话变更（轮询/补全资料），避免主页疯狂 Rebuild
            var epoch = Interlocked.Increment(ref _reloadEpoch);
            try
            {
                await Task.Delay(120).ConfigureAwait(true);
            }
            catch
            {
                return;
            }

            if (epoch == _reloadEpoch)
            {
                Reload();
            }
        }

        private int IndexOfSession(ChatSession session, int startAt)
        {
            for (int i = startAt; i < Sessions.Count; i++)
            {
                if (ReferenceEquals(Sessions[i], session))
                {
                    return i;
                }
            }

            return -1;
        }

        private static DateTimeOffset LastActivity(ChatSession session)
        {
            return session.Messages.Count > 0
                ? session.Messages[session.Messages.Count - 1].Timestamp
                : DateTimeOffset.MinValue;
        }
    }
}

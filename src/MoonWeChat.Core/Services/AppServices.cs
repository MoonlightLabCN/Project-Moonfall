using System;
using MoonWeChat.Services.WeChatPad;
using Windows.UI.Core;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 应用级服务组合。页面 / ViewModel 只拿这里的 <see cref="Data"/>，
    /// 不要直接 new Sample / Live。
    /// </summary>
    public static class AppServices
    {
        private static readonly object Gate = new object();
        private static IChatDataService _data;
        private static SampleChatDataService _sample;
        private static LiveChatDataService _live;
        private static BackendKind _liveBackendKind;
        private static CoreDispatcher _dispatcher;
        private static int _generation;

        /// <summary>
        /// 每次 <see cref="Rebuild"/> 真正换掉 <see cref="Data"/> 指向时 +1。
        /// 带页面缓存（NavigationCacheMode.Enabled）的页面必须记住进入时的代号，
        /// 代号变了就重新 Load —— 否则会拿着示例会话对象往真实网关发消息。
        /// </summary>
        public static int DataGeneration
        {
            get
            {
                Ensure();
                return _generation;
            }
        }

        public static IChatDataService Data
        {
            get
            {
                Ensure();
                return _data;
            }
        }

        public static LiveChatDataService Live
        {
            get
            {
                Ensure();
                return _live;
            }
        }

        public static IBackendProvider Api
        {
            get
            {
                Ensure();
                return _live.Api;
            }
        }

        public static void Initialize(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            Ensure();
            _live.AttachDispatcher(dispatcher);
            Rebuild();
        }

        /// <summary>
        /// 设置页切换“示例 / 真实 / 后端协议”或保存凭证后调用，重建当前 Data 指向。
        /// </summary>
        public static void Rebuild()
        {
            lock (Gate)
            {
                if (_sample == null)
                {
                    _sample = new SampleChatDataService();
                }

                EnsureLiveLocked();

                var previous = _data;

                // 启动阶段不自动 StartPolling（DispatcherTimer 在首帧前可能踩坑）。
                // 登录后的轮询由 SessionBootstrap / 页面 Refresh 触发。
                if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
                {
                    _live.StopPolling();
                    _data = _sample;
                }
                else
                {
                    _data = _live;
                    if (!AppSettings.IsLoggedIn)
                    {
                        _live.StopPolling();
                    }
                }

                if (!ReferenceEquals(previous, _data))
                {
                    // 离开示例模式时把演示会话恢复原状，避免下次再进示例时
                    // 还留着上一轮点出来的假气泡。
                    if (ReferenceEquals(previous, _sample))
                    {
                        SampleDataService.Reset();
                    }

                    _generation++;
                }
            }
        }

        private static void EnsureLiveLocked()
        {
            if (_live != null && _liveBackendKind == AppSettings.BackendKind)
            {
                if (_dispatcher != null)
                {
                    _live.AttachDispatcher(_dispatcher);
                }

                _live.ApplySettings();
                return;
            }

            var old = _live;
            _live = new LiveChatDataService();
            _liveBackendKind = AppSettings.BackendKind;
            if (_dispatcher != null)
            {
                _live.AttachDispatcher(_dispatcher);
            }

            _live.ApplySettings();
            old?.Dispose();
        }

        private static void Ensure()
        {
            if (_data != null)
            {
                return;
            }

            lock (Gate)
            {
                if (_data != null)
                {
                    return;
                }

                _sample = new SampleChatDataService();
                EnsureLiveLocked();
                _data = (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured) ? (IChatDataService)_sample : _live;
                _generation++;
            }
        }
    }
}

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
        private static CoreDispatcher _dispatcher;

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

        public static WeChatPadApiClient Api
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
        /// 设置页切换“示例 / 真实”或保存凭证后调用，重建当前 Data 指向。
        /// </summary>
        public static void Rebuild()
        {
            lock (Gate)
            {
                if (_sample == null)
                {
                    _sample = new SampleChatDataService();
                }

                if (_live == null)
                {
                    _live = new LiveChatDataService();
                }

                if (_dispatcher != null)
                {
                    _live.AttachDispatcher(_dispatcher);
                }

                _live.ApplySettings();

                if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
                {
                    _live.StopPolling();
                    _data = _sample;
                }
                else
                {
                    _data = _live;
                    if (AppSettings.IsLoggedIn)
                    {
                        _live.StartPolling();
                    }
                    else
                    {
                        _live.StopPolling();
                    }
                }
            }
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
                _live = new LiveChatDataService();
                if (_dispatcher != null)
                {
                    _live.AttachDispatcher(_dispatcher);
                }

                _data = (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured) ? (IChatDataService)_sample : _live;
            }
        }
    }
}

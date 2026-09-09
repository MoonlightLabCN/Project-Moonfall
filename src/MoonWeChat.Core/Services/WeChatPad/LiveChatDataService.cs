using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoonWeChat.Models;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace MoonWeChat.Services.WeChatPad
{
    /// <summary>
    /// 真实后端数据层：内存会话/联系人 + 统一后端协议 HTTP。
    /// 根据 AppSettings.BackendKind 自动选择 pyweixin 或 WeChatPadPro provider。
    /// 收消息：定时 Sync 轮询（手机端不方便挂 Webhook 公网回调）。
    /// </summary>
    public sealed class LiveChatDataService : IChatDataService, IDisposable
    {
        private readonly IBackendProvider _api;
        private readonly object _gate = new object();
        private readonly Dictionary<string, ChatSession> _sessions = new Dictionary<string, ChatSession>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Contact> _contacts = new Dictionary<string, Contact>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _seenMsgIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private const int SeenMsgIdCap = 4000;

        private DispatcherTimer _pollTimer;
        private DispatcherTimer _heartTimer;
        private CoreDispatcher _dispatcher;
        private int _refreshBusy;
        private int _pollBusy;
        private int _heartBusy;

        public bool IsSample => false;

        // 跟随当前配色：WP 经典皮肤是青绿（#00ABA9），不是微信绿。
        public string MyAccent => ThemeService.Current == AppVisualTheme.WpClassic ? "#00ABA9" : "#07C160";

        private string _lastRefreshError = string.Empty;

        /// <summary>上一次 RefreshAsync 的失败原因，成功时为空。</summary>
        public string LastRefreshError
        {
            get
            {
                lock (_gate)
                {
                    return _lastRefreshError ?? string.Empty;
                }
            }
        }

        private void SetRefreshError(string message)
        {
            lock (_gate)
            {
                _lastRefreshError = message ?? string.Empty;
            }
        }
        public string MyDisplayName => AppSettings.SelfNickname;

        public event EventHandler SessionsChanged;
        public event EventHandler ContactsChanged;
        public event EventHandler<ChatMessageEventArgs> MessageReceived;

        public LiveChatDataService()
        {
            _api = BackendProviderFactory.Create(AppSettings.BackendKind);
            ApplySettings();
        }

        public void ApplySettings()
        {
            _api.Configure(AppSettings.BaseUrl, AppSettings.Token);
        }

        public void AttachDispatcher(CoreDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void StartPolling(int intervalSeconds = 4)
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
            }

            if (intervalSeconds < 2)
            {
                intervalSeconds = 2;
            }

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(intervalSeconds) };
            _pollTimer.Tick += async (s, e) => await PollOnceAsync().ConfigureAwait(true);
            _pollTimer.Start();

            // 心跳保活（knowhub: /Login/HeartBeat），约 45 秒一次
            if (_heartTimer != null)
            {
                _heartTimer.Stop();
            }

            _heartTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            _heartTimer.Tick += async (s, e) => await HeartBeatOnceAsync().ConfigureAwait(true);
            _heartTimer.Start();
        }

        public void StopPolling()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer = null;
            }

            if (_heartTimer != null)
            {
                _heartTimer.Stop();
                _heartTimer = null;
            }
        }

        public IReadOnlyList<ChatSession> GetSessions()
        {
            lock (_gate)
            {
                return _sessions.Values
                    .OrderByDescending(s => s.IsPinned)
                    .ThenByDescending(LastActivity)
                    .ToList();
            }
        }

        public ChatSession GetSessionById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            lock (_gate)
            {
                ChatSession s;
                return _sessions.TryGetValue(id, out s) ? s : null;
            }
        }

        public IReadOnlyList<Contact> GetContacts()
        {
            lock (_gate)
            {
                return _contacts.Values
                    .OrderBy(c => c.PinyinIndex)
                    .ThenBy(c => c.DisplayName)
                    .ToList();
            }
        }

        public async Task RefreshAsync()
        {
            if (Interlocked.Exchange(ref _refreshBusy, 1) == 1)
            {
                return;
            }

            try
            {
                ApplySettings();
                if (!AppSettings.IsRemoteConfigured)
                {
                    return;
                }

                // 自己的资料（昵称）
                try
                {
                    var profile = await _api.GetSelfProfileAsync().ConfigureAwait(true);
                    if (profile.Ok)
                    {
                        ApplySelfProfile(profile);
                    }
                }
                catch
                {
                    // ignore
                }

                var friends = await _api.GetFriendListAsync().ConfigureAwait(true);
                if (!friends.Ok)
                {
                    // 网关（UI_UNAVAILABLE=503）会带上具体诊断，例如
                    // 「通讯录同步失败：微信主窗口在「点击搜索框」后失去前台焦点」。
                    // 以前这里没有 else 分支，这条消息被直接丢掉，
                    // 手机上表现为「刷新了但联系人没变」，和成功无法区分。
                    SetRefreshError(string.IsNullOrWhiteSpace(friends.Message)
                        ? "通讯录同步失败：网关未返回原因。"
                        : friends.Message);
                    Raise(SessionsChanged);
                    return;
                }

                SetRefreshError(string.Empty);
                if (friends.Ok)
                {
                    lock (_gate)
                    {
                        foreach (var dto in friends.Contacts)
                        {
                            UpsertContact_NoLock(dto);
                            var display = string.IsNullOrEmpty(dto.Remark) ? dto.NickName : dto.Remark;
                            var avatarUrl = FirstUrl(dto.BigHeadImgUrl, dto.SmallHeadImgUrl);
                            if (!_sessions.ContainsKey(dto.UserName))
                            {
                                _sessions[dto.UserName] = new ChatSession
                                {
                                    Id = dto.UserName,
                                    DisplayName = display,
                                    AvatarAccentColor = AccentFor(dto.UserName),
                                    AvatarUrl = avatarUrl,
                                    IsGroup = dto.IsChatroom,
                                    MemberCount = 0,
                                    LastMessagePreview = string.Empty,
                                    LastMessageTimeText = string.Empty,
                                    Messages = new ObservableCollection<ChatMessage>()
                                };
                            }
                            else
                            {
                                var session = _sessions[dto.UserName];
                                session.DisplayName = display;
                                session.IsGroup = dto.IsChatroom;
                                if (!string.IsNullOrEmpty(avatarUrl))
                                {
                                    session.AvatarUrl = avatarUrl;
                                }
                            }
                        }
                    }

                    Raise(ContactsChanged);
                    Raise(SessionsChanged);
                }

                // 群列表并入会话
                try
                {
                    var groups = await _api.GetGroupListAsync().ConfigureAwait(true);
                    if (groups.Ok && groups.Contacts != null)
                    {
                        lock (_gate)
                        {
                            foreach (var dto in groups.Contacts)
                            {
                                if (string.IsNullOrEmpty(dto.UserName))
                                {
                                    continue;
                                }

                                dto.IsChatroom = true;
                                UpsertContact_NoLock(dto);
                                var display = string.IsNullOrEmpty(dto.Remark) ? dto.NickName : dto.Remark;
                                if (!_sessions.ContainsKey(dto.UserName))
                                {
                                    _sessions[dto.UserName] = new ChatSession
                                    {
                                        Id = dto.UserName,
                                        DisplayName = display ?? dto.UserName,
                                        AvatarAccentColor = AccentFor(dto.UserName),
                                        AvatarUrl = FirstUrl(dto.BigHeadImgUrl, dto.SmallHeadImgUrl),
                                        IsGroup = true,
                                        Messages = new ObservableCollection<ChatMessage>(),
                                        LastMessagePreview = string.Empty,
                                        LastMessageTimeText = string.Empty
                                    };
                                }
                                else
                                {
                                    _sessions[dto.UserName].IsGroup = true;
                                    if (!string.IsNullOrEmpty(display))
                                    {
                                        _sessions[dto.UserName].DisplayName = display;
                                    }
                                }
                            }
                        }

                        Raise(SessionsChanged);
                        Raise(ContactsChanged);
                    }
                }
                catch
                {
                    // ignore
                }

                // 开启服务端自动同步（若支持）+ 唤醒 + 本地拉一轮
                try
                {
                    await _api.StartAutoSyncAsync().ConfigureAwait(true);
                    await _api.AwakenAsync().ConfigureAwait(true);
                }
                catch
                {
                    // ignore
                }

                await PollOnceAsync().ConfigureAwait(true);
            }
            finally
            {
                Interlocked.Exchange(ref _refreshBusy, 0);
            }
        }

        public async Task<ChatMessage> SendTextAsync(string sessionId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                return RejectUnknownTarget(sessionId);
            }

            var now = DateTimeOffset.Now;
            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = MessageType.Text,
                IsMine = true,
                SenderId = AppSettings.WxId ?? "me",
                SenderName = MyDisplayName,
                SenderAvatar = MyAccent,
                Timestamp = now,
                TimeText = now.ToString("HH:mm"),
                Content = text.Trim(),
                Status = MessageStatus.Sending,
                ShowSenderName = false
            };

            await RunOnUiAsync(() =>
            {
                session.Messages.Add(message);
                TouchPreview(session, message, isMine: true);
            }).ConfigureAwait(true);

            ApplySettings();
            ApiCallResult result;
            try
            {
                // Keep the client-generated id all the way to the gateway so a
                // failed/retried bubble cannot be confused with another send.
                result = await _api.SendTextMessageAsync(sessionId, message.Content, message.Id).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                result = new ApiCallResult { Ok = false, Message = ex.Message };
            }

            await ApplyResultAsync(message, result).ConfigureAwait(true);

            Raise(SessionsChanged);
            return message;
        }

        /// <summary>
        /// 把一次后端调用的结果落到本地气泡上。所有发送路径都必须走这里，
        /// 否则失败气泡会没有 ErrorText，UI 只能显示无信息量的兜底文案。
        /// </summary>
        private Task ApplyResultAsync(ChatMessage message, ApiCallResult result)
        {
            return RunOnUiAsync(() =>
            {
                var ok = result != null && result.Ok;
                message.Status = ok ? MessageStatus.Sent : MessageStatus.Failed;
                if (ok)
                {
                    // 服务端消息号只有这一次机会拿到。不存下来，
                    // 之后的撤回 / 引用回复就只剩本地 GUID 可用，服务端不认。
                    StampServerIds(message, result);

                    // 网关跑在 --mock 下时不接触电脑微信，必须让用户看见，
                    // 不能让一次 HTTP 契约测试被当成实机发送成功。
                    message.ErrorText = IsMockTransport(result)
                        ? "注意：网关处于 mock 模式，这条并没有发到电脑微信。"
                        : string.Empty;
                }
                else
                {
                    message.ErrorText = "发送失败：" +
                        (result == null || string.IsNullOrWhiteSpace(result.Message)
                            ? "网关未返回成功"
                            : result.Message);
                }
            });
        }

        /// <summary>
        /// 从发送应答的 Data 里取出服务端消息号，写回本地气泡。
        /// MAX v8 的 /Msg/SendTxt 等接口在 Data 里回 NewMsgId / ClientMsgId / CreateTime；
        /// pyweixin 网关只回 ClientMsgId（我们自己传过去的那个）。
        /// </summary>
        private static void StampServerIds(ChatMessage message, ApiCallResult result)
        {
            if (message == null || result == null || string.IsNullOrEmpty(result.DataJson))
            {
                return;
            }

            try
            {
                var data = Windows.Data.Json.JsonObject.Parse(result.DataJson);

                var newMsgId = ReadIdLike(data, "NewMsgId", "newMsgId", "new_msg_id", "SvrId", "svr_id", "MsgId", "msgId");
                if (!string.IsNullOrEmpty(newMsgId))
                {
                    message.ServerMsgId = newMsgId;
                }

                var clientMsgId = ReadIdLike(data, "ClientMsgId", "clientMsgId", "client_msg_id");
                if (!string.IsNullOrEmpty(clientMsgId))
                {
                    message.ServerClientMsgId = clientMsgId;
                }

                var createTime = ReadIdLike(data, "CreateTime", "createTime", "create_time");
                long parsed;
                if (!string.IsNullOrEmpty(createTime) && long.TryParse(createTime, out parsed) && parsed > 0)
                {
                    message.ServerCreateTime = parsed;
                }
            }
            catch
            {
                // 应答不是对象或字段缺失：撤回时会因为拿不到消息号而如实报错。
            }
        }

        /// <summary>
        /// 读一个「可能是数字也可能是字符串」的 id 字段。
        /// 64 位消息号在 JSON 里两种形态都出现过，手册也明说要用字符串传。
        /// </summary>
        private static string ReadIdLike(Windows.Data.Json.JsonObject data, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!data.ContainsKey(key))
                {
                    continue;
                }

                var value = data[key];
                if (value.ValueType == Windows.Data.Json.JsonValueType.String)
                {
                    var text = value.GetString();
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
                else if (value.ValueType == Windows.Data.Json.JsonValueType.Number)
                {
                    return ((long)value.GetNumber()).ToString();
                }
            }

            return null;
        }

        /// <summary>网关在 Data.Transport 里回 "mock" 表示这次调用没有碰真实微信。</summary>
        private static bool IsMockTransport(ApiCallResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.DataJson))
            {
                return false;
            }

            try
            {
                var data = Windows.Data.Json.JsonObject.Parse(result.DataJson);
                foreach (var key in new[] { "Transport", "transport" })
                {
                    if (data.ContainsKey(key) && data[key].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        return string.Equals(data[key].GetString(), "mock", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            catch
            {
                // 不是对象或解析失败：当成非 mock，交给其它校验。
            }

            return false;
        }

        public async Task<ChatMessage> SendPlaceholderAsync(string sessionId, MessageType type, string label)
        {
            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                return RejectUnknownTarget(sessionId);
            }

            // 图片应由 SendImageAsync 走选图；这里点「相册」占位时提示
            if (type == MessageType.Image)
            {
                var hint = CreateOutgoing(sessionId, MessageType.Text, "[请使用相册选图发送，或等待系统相册回调]");
                hint.Status = MessageStatus.Failed;
                await AppendLocalAsync(session, hint).ConfigureAwait(true);
                return hint;
            }

            var now = DateTimeOffset.Now;
            var message = CreateOutgoing(sessionId, type, null);
            message.Status = MessageStatus.Sending;

            switch (type)
            {
                case MessageType.Location:
                    message.LocationName = "当前位置";
                    message.LocationAddress = "由客户端发起（演示坐标）";
                    break;
                case MessageType.ContactCard:
                    // 默认把「文件传输助手」当名片目标演示；真机可后续改成联系人选择器
                    message.CardName = "文件传输助手";
                    message.CardAvatar = "#07C160";
                    break;
                case MessageType.Link:
                    message.LinkTitle = label ?? "链接";
                    message.LinkSource = "moonwechat";
                    message.Content = "https://wx.knowhub.cloud/";
                    break;
                case MessageType.Voice:
                    message.DurationSeconds = 1;
                    message.Status = MessageStatus.Failed;
                    message.Content = "[语音] 需录音权限与 /Msg/SendVoice，暂未接采集";
                    break;
                case MessageType.File:
                    message.FileName = "demo.txt";
                    message.FileExtension = "TXT";
                    message.FileSizeText = "-";
                    message.Status = MessageStatus.Failed;
                    message.Content = "[文件] 请后续接 /Msg/SendCDNFile";
                    break;
                default:
                    message.Type = MessageType.Text;
                    message.Content = label ?? string.Empty;
                    break;
            }

            await AppendLocalAsync(session, message).ConfigureAwait(true);

            if (message.Status == MessageStatus.Failed)
            {
                Raise(SessionsChanged);
                return message;
            }

            ApplySettings();
            ApiCallResult result;
            switch (type)
            {
                case MessageType.Location:
                    result = await _api.SendLocationAsync(sessionId, message.LocationName, message.LocationAddress).ConfigureAwait(true);
                    break;
                case MessageType.ContactCard:
                    result = await _api.SendCardAsync(sessionId, "filehelper", message.CardName).ConfigureAwait(true);
                    break;
                case MessageType.Link:
                    result = await _api.SendLinkAsync(sessionId, message.LinkTitle, message.Content ?? "https://wx.knowhub.cloud/").ConfigureAwait(true);
                    break;
                case MessageType.Text:
                    result = await _api.SendTextMessageAsync(sessionId, message.Content).ConfigureAwait(true);
                    break;
                default:
                    result = new ApiCallResult { Ok = false, Message = "未映射类型" };
                    break;
            }

            await ApplyResultAsync(message, result).ConfigureAwait(true);

            Raise(SessionsChanged);
            return message;
        }

        public async Task<ChatMessage> SendImageAsync(string sessionId, string imageBase64, string fileName = "image.jpg")
        {
            if (string.IsNullOrWhiteSpace(imageBase64))
            {
                return null;
            }

            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                return RejectUnknownTarget(sessionId);
            }

            var message = CreateOutgoing(sessionId, MessageType.Image, null);
            message.Status = MessageStatus.Sending;
            message.MediaAccentColor = "#576B95";
            message.MediaAspectRatio = 0.75;

            await AppendLocalAsync(session, message).ConfigureAwait(true);

            ApplySettings();
            var result = await _api.SendImageAsync(sessionId, imageBase64, fileName).ConfigureAwait(true);

            await ApplyResultAsync(message, result).ConfigureAwait(true);

            Raise(SessionsChanged);
            return message;
        }

        public async Task<bool> RevokeLastMineAsync(string sessionId)
        {
            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                return false;
            }

            ChatMessage target = null;
            for (int i = session.Messages.Count - 1; i >= 0; i--)
            {
                var m = session.Messages[i];
                if (m.IsMine && m.Status == MessageStatus.Sent && m.Type != MessageType.SystemNotice && m.Type != MessageType.DateDivider)
                {
                    target = m;
                    break;
                }
            }

            if (target == null)
            {
                return false;
            }

            ApplySettings();
            var create = (long)(target.Timestamp.ToUniversalTime() - new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).TotalSeconds;
            // ClientMsgId 用服务端回的那个（不是本地 GUID），NewMsgId 用 ServerMsgId。
            var createTime = target.ServerCreateTime > 0 ? target.ServerCreateTime : create;
            var result = await _api.RevokeMessageAsync(
                sessionId,
                string.IsNullOrEmpty(target.ServerClientMsgId) ? target.Id : target.ServerClientMsgId,
                createTime,
                target.ServerMsgId).ConfigureAwait(true);
            if (!result.Ok)
            {
                return false;
            }

            await RunOnUiAsync(() =>
            {
                var notice = CreateOutgoing(sessionId, MessageType.Recall, "你撤回了一条消息");
                notice.Status = MessageStatus.Sent;
                notice.IsMine = false;
                notice.ShowSenderName = false;
                session.Messages.Add(notice);
                TouchPreview(session, notice, isMine: true);
            }).ConfigureAwait(true);

            Raise(SessionsChanged);
            return true;
        }

        public async Task<ChatMessage> SendFileAsync(string sessionId, string fileBase64, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileBase64))
            {
                return null;
            }

            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                return RejectUnknownTarget(sessionId);
            }

            var message = CreateOutgoing(sessionId, MessageType.File, null);
            message.Status = MessageStatus.Sending;
            message.FileName = fileName ?? "file.bin";
            var ext = System.IO.Path.GetExtension(fileName ?? "").TrimStart('.').ToUpperInvariant();
            message.FileExtension = string.IsNullOrEmpty(ext) ? "FILE" : ext;
            message.FileSizeText = "~";

            await AppendLocalAsync(session, message).ConfigureAwait(true);
            ApplySettings();
            var result = await _api.SendFileAsync(sessionId, fileBase64, fileName).ConfigureAwait(true);
            await ApplyResultAsync(message, result).ConfigureAwait(true);
            Raise(SessionsChanged);
            return message;
        }

        public async Task<bool> SendPatAsync(string sessionId, string targetWxId = null)
        {
            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                return false;
            }

            ApplySettings();
            var target = string.IsNullOrEmpty(targetWxId) ? sessionId : targetWxId;
            var result = await _api.SendPatAsync(sessionId, target).ConfigureAwait(true);
            if (!result.Ok)
            {
                return false;
            }

            var notice = CreateOutgoing(sessionId, MessageType.Pat, "你拍了拍对方");
            notice.Status = MessageStatus.Sent;
            notice.IsMine = false;
            await AppendLocalAsync(session, notice).ConfigureAwait(true);
            Raise(SessionsChanged);
            return true;
        }

        public async Task<ChatMessage> SendQuoteAsync(string sessionId, string text, ChatMessage quoteOf)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                return RejectUnknownTarget(sessionId);
            }

            var message = CreateOutgoing(sessionId, MessageType.Text, text.Trim());
            message.Status = MessageStatus.Sending;
            if (quoteOf != null)
            {
                message.QuotedSenderName = quoteOf.IsMine ? "我" : quoteOf.SenderName;
                message.QuotedContent = quoteOf.Content ?? PreviewFor(quoteOf);
            }

            await AppendLocalAsync(session, message).ConfigureAwait(true);
            ApplySettings();
            // 引用必须用服务端消息号。收到的消息 ServerMsgId 由 MessageMapper 填，
            // 自己发的由 StampServerIds 回填；都没有时退回 Id（老数据）。
            var quotedServerId = quoteOf == null
                ? null
                : (string.IsNullOrEmpty(quoteOf.ServerMsgId) ? quoteOf.Id : quoteOf.ServerMsgId);

            MsgQuoteContext quoteContext = null;
            if (quoteOf != null)
            {
                long quotedMsgId;
                long.TryParse(quoteOf.ServerClientMsgId ?? string.Empty, out quotedMsgId);
                quoteContext = new MsgQuoteContext
                {
                    MsgType = quoteOf.ServerMsgType > 0 ? quoteOf.ServerMsgType : 1,
                    MsgId = quotedMsgId,
                    // 群聊里被引用的是某个群成员，会话是群本身。
                    FromUserId = session.IsGroup
                        ? (quoteOf.IsMine ? (AppSettings.WxId ?? string.Empty) : (quoteOf.SenderId ?? string.Empty))
                        : sessionId,
                    ChatUserId = sessionId,
                };
            }

            var result = await _api.SendQuoteAsync(
                sessionId,
                message.Content,
                quotedServerId,
                message.QuotedContent,
                quoteContext).ConfigureAwait(true);
            await ApplyResultAsync(message, result).ConfigureAwait(true);
            Raise(SessionsChanged);
            return message;
        }

        public async Task EnrichSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || !AppSettings.IsLoggedIn)
            {
                return;
            }

            ApplySettings();
            var session = EnsureSession(sessionId);
            if (session == null)
            {
                return;
            }

            try
            {
                // 注意：这里刻意不 Raise(SessionsChanged)。
                // 进会话时若全量刷会话列表，会连带刷新仍在返回栈里的主页，造成明显卡顿。
                if (session.IsGroup || sessionId.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
                {
                    var count = await _api.GetChatRoomMemberCountAsync(sessionId).ConfigureAwait(false);
                    if (count > 0)
                    {
                        await RunOnUiAsync(() =>
                        {
                            session.IsGroup = true;
                            session.MemberCount = count;
                        }).ConfigureAwait(true);
                    }
                }
                else
                {
                    var detail = await _api.GetContactDetailAsync(sessionId).ConfigureAwait(false);
                    if (detail.Ok)
                    {
                        var friends = ParseContactsFromDetail(detail);
                        if (friends.Count > 0)
                        {
                            await RunOnUiAsync(() =>
                            {
                                lock (_gate)
                                {
                                    foreach (var dto in friends)
                                    {
                                        UpsertContact_NoLock(dto);
                                        if (_sessions.ContainsKey(dto.UserName))
                                        {
                                            var s = _sessions[dto.UserName];
                                            s.DisplayName = string.IsNullOrEmpty(dto.Remark) ? dto.NickName : dto.Remark;
                                            s.AvatarUrl = FirstUrl(dto.BigHeadImgUrl, dto.SmallHeadImgUrl) ?? s.AvatarUrl;
                                        }
                                    }
                                }
                            }).ConfigureAwait(true);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private List<PadContactDto> ParseContactsFromDetail(ApiCallResult detail)
        {
            var list = new List<PadContactDto>();
            if (detail == null || string.IsNullOrEmpty(detail.RawJson))
            {
                return list;
            }

            try
            {
                var root = Windows.Data.Json.JsonObject.Parse(detail.RawJson);
                Windows.Data.Json.JsonObject data = null;
                if (root.ContainsKey("Data") && root["Data"].ValueType == Windows.Data.Json.JsonValueType.Object)
                {
                    data = root["Data"].GetObject();
                }
                else if (root.ContainsKey("data") && root["data"].ValueType == Windows.Data.Json.JsonValueType.Object)
                {
                    data = root["data"].GetObject();
                }
                else
                {
                    data = root;
                }

                // single contact object
                string userName = null, nick = null, remark = null, big = null, small = null;
                foreach (var k in new[] { "UserName", "userName", "Username", "wxid" })
                {
                    if (data.ContainsKey(k) && data[k].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        userName = data[k].GetString();
                        break;
                    }
                }

                foreach (var k in new[] { "NickName", "nickName", "Nickname" })
                {
                    if (data.ContainsKey(k) && data[k].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        nick = data[k].GetString();
                        break;
                    }
                }

                foreach (var k in new[] { "Remark", "remark", "RemarkName" })
                {
                    if (data.ContainsKey(k) && data[k].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        remark = data[k].GetString();
                        break;
                    }
                }

                foreach (var k in new[] { "BigHeadImgUrl", "bigHeadImgUrl" })
                {
                    if (data.ContainsKey(k) && data[k].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        big = data[k].GetString();
                        break;
                    }
                }

                foreach (var k in new[] { "SmallHeadImgUrl", "smallHeadImgUrl", "HeadImgUrl" })
                {
                    if (data.ContainsKey(k) && data[k].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        small = data[k].GetString();
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(userName))
                {
                    list.Add(new PadContactDto
                    {
                        UserName = userName,
                        NickName = nick ?? userName,
                        Remark = remark,
                        BigHeadImgUrl = big,
                        SmallHeadImgUrl = small,
                    });
                }
            }
            catch
            {
                // ignore
            }

            return list;
        }

        public async Task<bool> RetrySendAsync(string sessionId, ChatMessage message)
        {
            if (message == null || !message.IsMine)
            {
                return false;
            }

            var session = ResolveSendTarget(sessionId);
            if (session == null)
            {
                await RunOnUiAsync(() =>
                {
                    message.Status = MessageStatus.Failed;
                    message.ErrorText = "重发已拦截：会话 “" + (sessionId ?? "?") + "” 不在当前后端的会话列表里。";
                }).ConfigureAwait(true);
                return false;
            }

            await RunOnUiAsync(() =>
            {
                message.Status = MessageStatus.Sending;
                message.ErrorText = string.Empty;
            }).ConfigureAwait(true);

            ApplySettings();
            ApiCallResult result;
            switch (message.Type)
            {
                case MessageType.Text:
                    // 重发必须带上同一个本地消息 id，否则网关侧就无法把
                    // “一条手机气泡” 和 “一次 HTTP 发送” 对上账。
                    result = await _api.SendTextMessageAsync(sessionId, message.Content, message.Id).ConfigureAwait(true);
                    break;
                case MessageType.Location:
                    result = await _api.SendLocationAsync(sessionId, message.LocationName, message.LocationAddress).ConfigureAwait(true);
                    break;
                case MessageType.ContactCard:
                    result = await _api.SendCardAsync(sessionId, "filehelper", message.CardName).ConfigureAwait(true);
                    break;
                case MessageType.Link:
                    result = await _api.SendLinkAsync(sessionId, message.LinkTitle, message.Content ?? string.Empty).ConfigureAwait(true);
                    break;
                case MessageType.File:
                    result = new ApiCallResult { Ok = false, Message = "文件请重新从「+」选择发送" };
                    break;
                default:
                    result = new ApiCallResult { Ok = false, Message = "该类型暂不支持重发" };
                    break;
            }

            await ApplyResultAsync(message, result).ConfigureAwait(true);

            return result.Ok;
        }

        public IBackendProvider Api
        {
            get
            {
                ApplySettings();
                return _api;
            }
        }

        public void Dispose()
        {
            StopPolling();
            _api.Dispose();
        }

        // ------------------------------------------------------------------

        private async Task PollOnceAsync()
        {
            if (!AppSettings.IsLoggedIn)
            {
                return;
            }

            if (Interlocked.Exchange(ref _pollBusy, 1) == 1)
            {
                return;
            }

            try
            {
                ApplySettings();
                SyncMsgResult sync;
                try
                {
                    sync = await _api.GetSyncMsgAsync().ConfigureAwait(true);
                }
                catch
                {
                    // Tick 是 async void：这里漏出去的异常没有人能接，会直接崩进程。
                    return;
                }

                if (!sync.Ok || sync.Messages == null || sync.Messages.Count == 0)
                {
                    return;
                }

                var self = AppSettings.WxId ?? string.Empty;
                foreach (var dto in sync.Messages)
                {
                    var msgKey = !string.IsNullOrEmpty(dto.NewMsgId) ? dto.NewMsgId : dto.MsgId;
                    if (string.IsNullOrEmpty(msgKey))
                    {
                        msgKey = Guid.NewGuid().ToString("N");
                    }

                    lock (_gate)
                    {
                        if (_seenMsgIds.Contains(msgKey))
                        {
                            continue;
                        }

                        // 长时间挂着轮询时不能无限增长；手机内存很紧。
                        if (_seenMsgIds.Count > SeenMsgIdCap)
                        {
                            _seenMsgIds.Clear();
                        }

                        _seenMsgIds.Add(msgKey);
                    }

                    var isMine = !string.IsNullOrEmpty(self) &&
                                 string.Equals(dto.FromUserName, self, StringComparison.OrdinalIgnoreCase);

                    // 会话 id：群用群 id；单聊用对方 id
                    string sessionId;
                    if (dto.IsGroup)
                    {
                        sessionId = (dto.FromUserName != null && dto.FromUserName.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
                            ? dto.FromUserName
                            : dto.ToUserName;
                    }
                    else
                    {
                        sessionId = isMine ? dto.ToUserName : dto.FromUserName;
                    }

                    if (string.IsNullOrEmpty(sessionId))
                    {
                        continue;
                    }

                    var session = EnsureSession(sessionId);
                    var mapped = MapMessage(dto, isMine, session.IsGroup);

                    await RunOnUiAsync(() =>
                    {
                        // 去重：同 Id 不重复加
                        if (session.Messages.Any(m => m.Id == mapped.Id))
                        {
                            return;
                        }

                        session.Messages.Add(mapped);
                        if (!isMine)
                        {
                            session.UnreadCount += 1;
                        }

                        TouchPreview(session, mapped, isMine);
                    }).ConfigureAwait(true);

                    MessageReceived?.Invoke(this, new ChatMessageEventArgs(sessionId, mapped));
                }

                Raise(SessionsChanged);
            }
            finally
            {
                Interlocked.Exchange(ref _pollBusy, 0);
            }
        }

        /// <summary>
        /// 发送目标解析：只认这个数据源自己已知的会话（来自好友/群列表或已收到过消息）。
        /// 绝不能像 <see cref="EnsureSession"/> 那样凭空造一个 —— 页面被缓存时可能还攥着
        /// 示例数据源的会话对象，那个 id 一旦被当成 ToWxid 发到网关，
        /// 网关会拿它去微信搜索框里搜，可能命中并发给完全不相干的人。
        /// </summary>
        private ChatSession ResolveSendTarget(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return null;
            }

            lock (_gate)
            {
                ChatSession s;
                return _sessions.TryGetValue(sessionId, out s) ? s : null;
            }
        }

        private static ChatMessage RejectUnknownTarget(string sessionId)
        {
            var now = DateTimeOffset.Now;
            return new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = MessageType.Text,
                IsMine = true,
                SenderId = AppSettings.WxId ?? "me",
                SenderName = AppSettings.SelfNickname,
                Timestamp = now,
                TimeText = now.ToString("HH:mm"),
                Status = MessageStatus.Failed,
                ErrorText = "发送已拦截：会话 “" + (sessionId ?? "?") +
                            "” 不在当前后端的会话列表里（可能来自示例数据）。请退出会话、刷新列表后重进。",
                ShowSenderName = false
            };
        }

        private ChatSession EnsureSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return null;
            }

            lock (_gate)
            {
                ChatSession s;
                if (_sessions.TryGetValue(sessionId, out s))
                {
                    return s;
                }

                Contact c;
                _contacts.TryGetValue(sessionId, out c);

                s = new ChatSession
                {
                    Id = sessionId,
                    DisplayName = c != null ? c.DisplayName : sessionId,
                    AvatarAccentColor = AccentFor(sessionId),
                    AvatarUrl = c != null ? c.AvatarUrl : null,
                    IsGroup = sessionId.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase),
                    Messages = new ObservableCollection<ChatMessage>(),
                    LastMessagePreview = string.Empty,
                    LastMessageTimeText = string.Empty
                };
                _sessions[sessionId] = s;
                return s;
            }
        }

        private void UpsertContact_NoLock(PadContactDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.UserName))
            {
                return;
            }

            // 过滤文件传输助手以外的常见系统账号可按需扩展
            var region = string.Join(" ", new[] { dto.Province, dto.City }.Where(x => !string.IsNullOrEmpty(x)));

            // 备注和昵称都可能为空（部分后端返回空字符串），这里必须兜底到 UserName，
            // 否则下面取首字母会抛异常，一个无名联系人就能让整轮通讯录同步中断。
            var display = dto.Remark;
            if (string.IsNullOrEmpty(display))
            {
                display = dto.NickName;
            }

            if (string.IsNullOrEmpty(display))
            {
                display = dto.UserName;
            }

            var pinyin = char.IsLetter(display, 0) ? display.Substring(0, 1).ToUpperInvariant() : "#";

            _contacts[dto.UserName] = new Contact
            {
                WxId = dto.UserName,
                Nickname = dto.NickName ?? dto.UserName,
                Remark = dto.Remark ?? string.Empty,
                AvatarAccentColor = AccentFor(dto.UserName),
                AvatarUrl = FirstUrl(dto.BigHeadImgUrl, dto.SmallHeadImgUrl),
                Region = region,
                Signature = dto.Signature ?? string.Empty,
                PinyinIndex = pinyin
            };
        }

        private ChatMessage CreateOutgoing(string sessionId, MessageType type, string content)
        {
            var now = DateTimeOffset.Now;
            return new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = type,
                IsMine = true,
                SenderId = AppSettings.WxId ?? "me",
                SenderName = MyDisplayName,
                SenderAvatar = MyAccent,
                Timestamp = now,
                TimeText = now.ToString("HH:mm"),
                Content = content,
                Status = MessageStatus.Sending,
                ShowSenderName = false
            };
        }

        private async Task AppendLocalAsync(ChatSession session, ChatMessage message)
        {
            await RunOnUiAsync(() =>
            {
                session.Messages.Add(message);
                TouchPreview(session, message, isMine: true);
            }).ConfigureAwait(true);
        }

        private async Task HeartBeatOnceAsync()
        {
            if (!AppSettings.IsLoggedIn)
            {
                return;
            }

            if (Interlocked.Exchange(ref _heartBusy, 1) == 1)
            {
                return;
            }

            try
            {
                ApplySettings();
                await _api.HeartBeatAsync().ConfigureAwait(true);
            }
            catch
            {
                // 心跳失败静默
            }
            finally
            {
                Interlocked.Exchange(ref _heartBusy, 0);
            }
        }

        private void ApplySelfProfile(ApiCallResult profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.RawJson))
            {
                return;
            }

            try
            {
                var root = Windows.Data.Json.JsonObject.Parse(profile.RawJson);
                Windows.Data.Json.JsonObject data = null;
                if (root.ContainsKey("Data") && root["Data"].ValueType == Windows.Data.Json.JsonValueType.Object)
                {
                    data = root["Data"].GetObject();
                }
                else if (root.ContainsKey("data") && root["data"].ValueType == Windows.Data.Json.JsonValueType.Object)
                {
                    data = root["data"].GetObject();
                }
                else
                {
                    data = root;
                }

                string nick = null;
                string wxid = null;
                foreach (var key in new[] { "NickName", "nickName", "Nickname", "nickname" })
                {
                    if (data.ContainsKey(key) && data[key].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        nick = data[key].GetString();
                        break;
                    }
                }

                foreach (var key in new[] { "UserName", "userName", "wxid", "Wxid", "WxId" })
                {
                    if (data.ContainsKey(key) && data[key].ValueType == Windows.Data.Json.JsonValueType.String)
                    {
                        wxid = data[key].GetString();
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(nick))
                {
                    AppSettings.SelfNickname = nick;
                }

                if (!string.IsNullOrEmpty(wxid) && string.IsNullOrEmpty(AppSettings.WxId))
                {
                    AppSettings.WxId = wxid;
                }
            }
            catch
            {
                // ignore parse errors
            }
        }

        private static string FirstUrl(params string[] urls)
        {
            if (urls == null)
            {
                return null;
            }

            foreach (var u in urls)
            {
                if (!string.IsNullOrWhiteSpace(u) &&
                    (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    return u;
                }
            }

            return null;
        }

        private static ChatMessage MapMessage(PadMessageDto dto, bool isMine, bool isGroup)
        {
            var ts = dto.CreateTime > 0
                ? new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(dto.CreateTime).ToLocalTime()
                : DateTimeOffset.Now;

            // 群消息 Content 常为 "wxid:\n正文"
            var senderId = dto.FromUserName;
            var content = dto.Content ?? string.Empty;
            string senderName = null;

            if (isGroup && !isMine && !string.IsNullOrEmpty(content))
            {
                var idx = content.IndexOf(":\n", StringComparison.Ordinal);
                if (idx > 0 && idx < 64)
                {
                    senderId = content.Substring(0, idx);
                    content = content.Substring(idx + 2);
                    senderName = senderId;
                }
            }

            var type = MapMsgType(dto.MsgType, content);
            var msg = new ChatMessage
            {
                Id = !string.IsNullOrEmpty(dto.NewMsgId) ? dto.NewMsgId : (dto.MsgId ?? Guid.NewGuid().ToString("N")),
                SenderId = senderId,
                SenderName = isMine ? AppSettings.SelfNickname : (senderName ?? senderId),
                SenderAvatar = AccentFor(senderId ?? "?"),
                IsMine = isMine,
                Type = type,
                Timestamp = ts,
                TimeText = ts.ToString("HH:mm"),
                Status = MessageStatus.Sent,
                ShowSenderName = isGroup && !isMine,
                Content = content
            };

            if (type == MessageType.Image)
            {
                msg.MediaAccentColor = "#576B95";
                msg.MediaAspectRatio = 1.0;
                msg.Content = null;
            }
            else if (type == MessageType.SystemNotice || type == MessageType.Recall || type == MessageType.Pat)
            {
                // 系统类保留 Content
            }

            return msg;
        }

        private static MessageType MapMsgType(int msgType, string content)
        {
            // 微信协议常见：1 文本 3 图片 34 语音 43 视频 47 表情 49 appmsg 10000 系统 ...
            switch (msgType)
            {
                case 1: return MessageType.Text;
                case 3: return MessageType.Image;
                case 34: return MessageType.Voice;
                case 42: return MessageType.ContactCard;
                case 43: return MessageType.Video;
                case 47: return MessageType.Emoji;
                case 48: return MessageType.Location;
                case 10000:
                case 10002:
                    if (!string.IsNullOrEmpty(content) && content.Contains("拍了拍"))
                    {
                        return MessageType.Pat;
                    }

                    if (!string.IsNullOrEmpty(content) && content.Contains("撤回"))
                    {
                        return MessageType.Recall;
                    }

                    return MessageType.SystemNotice;
                default:
                    return MessageType.Text;
            }
        }

        private static void TouchPreview(ChatSession session, ChatMessage message, bool isMine)
        {
            session.LastMessagePreview = PreviewFor(message);
            session.LastMessageSenderPrefix = (!isMine && session.IsGroup) ? message.SenderName : null;
            session.LastMessageTimeText = message.TimeText;
        }

        private static string PreviewFor(ChatMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Text: return message.Content ?? string.Empty;
                case MessageType.Image: return "[图片]";
                case MessageType.Voice: return "[语音]";
                case MessageType.Video: return "[视频]";
                case MessageType.File: return "[文件]";
                case MessageType.Location: return "[位置]";
                case MessageType.ContactCard: return "[名片]";
                case MessageType.Link: return "[链接]";
                case MessageType.MiniProgram: return "[小程序]";
                case MessageType.Emoji: return "[动画表情]";
                case MessageType.SystemNotice:
                case MessageType.Recall:
                case MessageType.Pat:
                    return message.Content ?? "[系统消息]";
                default: return message.Content ?? string.Empty;
            }
        }

        private static string AccentFor(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "#07C160";
            }

            // 稳定哈希 → 固定色板
            string[] palette =
            {
                "#F1592A", "#576B95", "#9C6ADE", "#10AEFF", "#FA9D3B",
                "#07C160", "#E64340", "#6467F0", "#3D7EFF", "#00B578"
            };
            unchecked
            {
                int h = 0;
                foreach (var ch in key)
                {
                    h = (h * 31) + ch;
                }

                if (h < 0)
                {
                    h = -h;
                }

                return palette[h % palette.Length];
            }
        }

        private static DateTimeOffset LastActivity(ChatSession session)
        {
            if (session?.Messages != null && session.Messages.Count > 0)
            {
                return session.Messages[session.Messages.Count - 1].Timestamp;
            }

            return DateTimeOffset.MinValue;
        }

        private void Raise(EventHandler handler)
        {
            if (handler == null)
            {
                return;
            }

            var d = _dispatcher;
            if (d == null)
            {
                handler(this, EventArgs.Empty);
                return;
            }

            var ignore = d.RunAsync(CoreDispatcherPriority.Normal, () => handler(this, EventArgs.Empty));
        }

        private Task RunOnUiAsync(Action action)
        {
            var d = _dispatcher;
            if (d == null || d.HasThreadAccess)
            {
                action();
                return Task.FromResult(0);
            }

            var tcs = new TaskCompletionSource<int>();
            var ignore = d.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    action();
                    tcs.SetResult(0);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }
    }
}

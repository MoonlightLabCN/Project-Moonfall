using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MoonWeChat.Common;
using MoonWeChat.Models;
using MoonWeChat.Services;

namespace MoonWeChat.ViewModels
{
    public class ChatViewModel : BindableBase
    {
        public ChatSession Session { get; private set; }

        public ObservableCollection<ChatMessage> Messages => Session?.Messages;

        public bool IsGroup => Session?.IsGroup ?? false;

        public string HeaderTitle => Session == null
            ? string.Empty
            : (Session.IsGroup
                ? (Session.MemberCount > 0
                    ? $"{Session.DisplayName} ({Session.MemberCount})"
                    : Session.DisplayName)
                : Session.DisplayName);

        private string _draftText = string.Empty;
        private bool _sendInProgress;
        private string _sendStatusText = string.Empty;
        public string DraftText
        {
            get => _draftText;
            set
            {
                if (SetProperty(ref _draftText, value))
                {
                    OnPropertyChanged(nameof(CanSend));
                    OnPropertyChanged(nameof(ShowSendButton));
                    OnPropertyChanged(nameof(ShowMoreButton));
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanSend => !_sendInProgress && !string.IsNullOrWhiteSpace(DraftText);

        /// <summary>
        /// 发送中 CanSend 会变 false，但按钮不能在这一刻退回成“+”——草稿还在输入框里，
        /// 退回去会让人以为消息已经发走。所以显示与可用分开：有草稿或正在发送就显示发送按钮，
        /// 只有 CanSend 才允许点。
        /// </summary>
        public bool ShowSendButton => _sendInProgress || !string.IsNullOrWhiteSpace(DraftText);

        public bool ShowMoreButton => !ShowSendButton;

        public bool IsSendInProgress
        {
            get => _sendInProgress;
            private set
            {
                if (SetProperty(ref _sendInProgress, value))
                {
                    OnPropertyChanged(nameof(CanSend));
                    OnPropertyChanged(nameof(ShowSendButton));
                    OnPropertyChanged(nameof(ShowMoreButton));
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SendStatusText
        {
            get => _sendStatusText;
            private set
            {
                if (SetProperty(ref _sendStatusText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasSendStatus));
                }
            }
        }

        public bool HasSendStatus => !string.IsNullOrWhiteSpace(SendStatusText);

        private ChatMessage _quoteTarget;
        public ChatMessage QuoteTarget
        {
            get => _quoteTarget;
            set
            {
                if (SetProperty(ref _quoteTarget, value))
                {
                    OnPropertyChanged(nameof(HasQuoteTarget));
                    OnPropertyChanged(nameof(QuotePreview));
                }
            }
        }

        public bool HasQuoteTarget => QuoteTarget != null;
        public string QuotePreview => QuoteTarget == null
            ? string.Empty
            : ("引用 " + (QuoteTarget.IsMine ? "我" : QuoteTarget.SenderName) + "：" + (QuoteTarget.Content ?? "[消息]"));

        public RelayCommand SendCommand { get; }

        public event EventHandler<ChatMessage> MessageAppended;

        /// <summary>Load 时的 <see cref="AppServices.DataGeneration"/>，用于识别数据源已被切换。</summary>
        public int LoadedGeneration { get; private set; } = -1;

        /// <summary>
        /// 数据源在本页缓存期间被切换过（示例 ↔ 真实、换后端）。
        /// 此时 <see cref="Session"/> 是旧数据源的对象，其 Id 在新数据源里可能根本不存在，
        /// 继续发送就会把示例会话 id 当成 ToWxid 打到网关。
        /// </summary>
        public bool IsStale => LoadedGeneration != AppServices.DataGeneration;

        public ChatViewModel()
        {
            SendCommand = new RelayCommand(async _ => await SendAsync(), _ => CanSend);
        }

        /// <summary>
        /// 所有发送入口的统一前置检查。返回 true 表示已经拦下并写好了提示，调用方必须直接返回。
        /// </summary>
        private bool BlockSend()
        {
            if (Session == null)
            {
                SendStatusText = "会话未就绪。";
                return true;
            }

            if (IsStale)
            {
                SendStatusText = "数据源已切换，请退出会话重新进入后再发送。";
                return true;
            }

            if (AppServices.Data.IsSample)
            {
                SendStatusText = "当前是示例数据，不会发送到电脑微信。请到“服务器设置”取消示例数据后再发。";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 先同步绑定会话与消息（首帧立刻可画），网络补全放到后台，避免进聊天卡顿。
        /// </summary>
        public void Load(string sessionId)
        {
            LoadedGeneration = AppServices.DataGeneration;
            Session = AppServices.Data.GetSessionById(sessionId);
            if (Session != null)
            {
                Session.UnreadCount = 0;
            }

            QuoteTarget = null;
            DraftText = string.Empty;
            SendStatusText = string.Empty;

            OnPropertyChanged(nameof(Session));
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(IsGroup));
            OnPropertyChanged(nameof(HeaderTitle));

            if (Session != null && !string.IsNullOrEmpty(sessionId))
            {
                var id = sessionId;
                var ignored = EnrichInBackgroundAsync(id);
            }
        }

        private async Task EnrichInBackgroundAsync(string sessionId)
        {
            try
            {
                // 网络在线程池完成；回 UI 只刷标题（群人数等）
                await AppServices.Data.EnrichSessionAsync(sessionId).ConfigureAwait(true);
                if (Session != null && string.Equals(Session.Id, sessionId, StringComparison.Ordinal))
                {
                    OnPropertyChanged(nameof(HeaderTitle));
                    OnPropertyChanged(nameof(IsGroup));
                }
            }
            catch
            {
                // ignore
            }
        }

        public void Send()
        {
            _ = SendAsync();
        }

        public async Task SendAsync()
        {
            if (!CanSend)
            {
                return;
            }

            if (BlockSend())
            {
                return;
            }

            var text = DraftText.Trim();
            var quote = QuoteTarget;
            IsSendInProgress = true;
            SendStatusText = "正在发送…";
            try
            {
                ChatMessage message;
                if (quote != null)
                {
                    message = await AppServices.Data.SendQuoteAsync(Session.Id, text, quote).ConfigureAwait(true);
                }
                else
                {
                    message = await AppServices.Data.SendTextAsync(Session.Id, text).ConfigureAwait(true);
                }

                if (message == null)
                {
                    // 数据源没有产出本地消息，等于什么都没发生，不能当成功。
                    DraftText = text;
                    SendStatusText = "发送未生效，请检查网关和电脑微信。";
                    return;
                }

                MessageAppended?.Invoke(this, message);
                if (message.IsFailed)
                {
                    // Keep the draft available for a deliberate retry.
                    DraftText = text;
                    SendStatusText = string.IsNullOrWhiteSpace(message.ErrorText)
                        ? "发送失败，请检查网关和电脑微信。"
                        : message.ErrorText;
                }
                else
                {
                    DraftText = string.Empty;
                    SendStatusText = string.Empty;
                    if (quote != null && ReferenceEquals(QuoteTarget, quote))
                    {
                        QuoteTarget = null;
                    }
                }
            }
            catch (Exception ex)
            {
                DraftText = text;
                SendStatusText = "发送失败：" + ex.Message;
            }
            finally
            {
                IsSendInProgress = false;
            }
        }

        public void SendPlaceholder(MessageType type, string label)
        {
            _ = SendPlaceholderAsync(type, label);
        }

        public async Task SendPlaceholderAsync(MessageType type, string label)
        {
            if (BlockSend())
            {
                return;
            }

            var message = await AppServices.Data.SendPlaceholderAsync(Session.Id, type, label).ConfigureAwait(true);
            ReportOutcome(message);
        }

        public async Task SendImageBase64Async(string base64, string fileName)
        {
            if (string.IsNullOrEmpty(base64) || BlockSend())
            {
                return;
            }

            SendStatusText = "正在发送图片…";
            var message = await AppServices.Data.SendImageAsync(Session.Id, base64, fileName).ConfigureAwait(true);
            ReportOutcome(message);
        }

        public async Task SendFileBase64Async(string base64, string fileName)
        {
            if (string.IsNullOrEmpty(base64) || BlockSend())
            {
                return;
            }

            SendStatusText = "正在发送文件…";
            var message = await AppServices.Data.SendFileAsync(Session.Id, base64, fileName).ConfigureAwait(true);
            ReportOutcome(message);
        }

        public async Task RetryAsync(ChatMessage message)
        {
            if (message == null || !message.IsFailed || BlockSend())
            {
                return;
            }

            SendStatusText = "正在重发…";
            await AppServices.Data.RetrySendAsync(Session.Id, message).ConfigureAwait(true);
            MessageAppended?.Invoke(this, message);
            SendStatusText = message.IsFailed
                ? (string.IsNullOrWhiteSpace(message.ErrorText) ? "重发失败。" : message.ErrorText)
                : string.Empty;
        }

        public async Task RevokeLastAsync()
        {
            if (BlockSend())
            {
                return;
            }

            var ok = await AppServices.Data.RevokeLastMineAsync(Session.Id).ConfigureAwait(true);
            if (ok)
            {
                SendStatusText = string.Empty;
                MessageAppended?.Invoke(this, Session.Messages.LastOrDefault());
            }
            else
            {
                SendStatusText = "撤回未成功（当前后端可能不支持撤回）。";
            }
        }

        public async Task PatAsync()
        {
            if (BlockSend())
            {
                return;
            }

            var ok = await AppServices.Data.SendPatAsync(Session.Id, Session.IsGroup ? null : Session.Id).ConfigureAwait(true);
            if (ok)
            {
                SendStatusText = string.Empty;
                MessageAppended?.Invoke(this, Session.Messages.LastOrDefault());
            }
            else
            {
                SendStatusText = "拍一拍未成功（当前后端可能不支持）。";
            }
        }

        /// <summary>
        /// 非文本发送的统一回显。数据源返回 null 或 Failed 都必须让用户看见，
        /// 不能只把气泡插进列表就当成功。
        /// </summary>
        private void ReportOutcome(ChatMessage message)
        {
            if (message == null)
            {
                SendStatusText = "发送未生效，请检查网关和电脑微信。";
                return;
            }

            MessageAppended?.Invoke(this, message);
            SendStatusText = message.IsFailed
                ? (string.IsNullOrWhiteSpace(message.ErrorText) ? "发送失败，请检查网关和电脑微信。" : message.ErrorText)
                : string.Empty;
        }

        public void SetQuote(ChatMessage message)
        {
            if (message == null || message.Type == MessageType.DateDivider || message.Type == MessageType.SystemNotice)
            {
                return;
            }

            QuoteTarget = message;
        }

        public void ClearQuote()
        {
            QuoteTarget = null;
        }
    }
}

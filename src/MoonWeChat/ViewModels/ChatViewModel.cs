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
        public string DraftText
        {
            get => _draftText;
            set
            {
                if (SetProperty(ref _draftText, value))
                {
                    OnPropertyChanged(nameof(CanSend));
                    SendCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool CanSend => !string.IsNullOrWhiteSpace(DraftText);

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

        public ChatViewModel()
        {
            SendCommand = new RelayCommand(async _ => await SendAsync(), _ => CanSend);
        }

        public async void Load(string sessionId)
        {
            Session = AppServices.Data.GetSessionById(sessionId);
            if (Session != null)
            {
                Session.UnreadCount = 0;
            }

            OnPropertyChanged(nameof(Session));
            OnPropertyChanged(nameof(Messages));
            OnPropertyChanged(nameof(IsGroup));
            OnPropertyChanged(nameof(HeaderTitle));

            if (Session != null)
            {
                try
                {
                    await AppServices.Data.EnrichSessionAsync(sessionId).ConfigureAwait(true);
                    OnPropertyChanged(nameof(HeaderTitle));
                    OnPropertyChanged(nameof(IsGroup));
                }
                catch
                {
                    // ignore
                }
            }
        }

        public void Send()
        {
            _ = SendAsync();
        }

        public async Task SendAsync()
        {
            if (!CanSend || Session == null)
            {
                return;
            }

            var text = DraftText.Trim();
            DraftText = string.Empty;
            ChatMessage message;
            if (QuoteTarget != null)
            {
                var q = QuoteTarget;
                QuoteTarget = null;
                message = await AppServices.Data.SendQuoteAsync(Session.Id, text, q).ConfigureAwait(true);
            }
            else
            {
                message = await AppServices.Data.SendTextAsync(Session.Id, text).ConfigureAwait(true);
            }

            if (message != null)
            {
                MessageAppended?.Invoke(this, message);
            }
        }

        public void SendPlaceholder(MessageType type, string label)
        {
            _ = SendPlaceholderAsync(type, label);
        }

        public async Task SendPlaceholderAsync(MessageType type, string label)
        {
            if (Session == null)
            {
                return;
            }

            var message = await AppServices.Data.SendPlaceholderAsync(Session.Id, type, label).ConfigureAwait(true);
            if (message != null)
            {
                MessageAppended?.Invoke(this, message);
            }
        }

        public async Task SendImageBase64Async(string base64, string fileName)
        {
            if (Session == null || string.IsNullOrEmpty(base64))
            {
                return;
            }

            var message = await AppServices.Data.SendImageAsync(Session.Id, base64, fileName).ConfigureAwait(true);
            if (message != null)
            {
                MessageAppended?.Invoke(this, message);
            }
        }

        public async Task SendFileBase64Async(string base64, string fileName)
        {
            if (Session == null || string.IsNullOrEmpty(base64))
            {
                return;
            }

            var message = await AppServices.Data.SendFileAsync(Session.Id, base64, fileName).ConfigureAwait(true);
            if (message != null)
            {
                MessageAppended?.Invoke(this, message);
            }
        }

        public async Task RetryAsync(ChatMessage message)
        {
            if (Session == null || message == null || !message.IsFailed)
            {
                return;
            }

            await AppServices.Data.RetrySendAsync(Session.Id, message).ConfigureAwait(true);
            MessageAppended?.Invoke(this, message);
        }

        public async Task RevokeLastAsync()
        {
            if (Session == null)
            {
                return;
            }

            var ok = await AppServices.Data.RevokeLastMineAsync(Session.Id).ConfigureAwait(true);
            if (ok)
            {
                MessageAppended?.Invoke(this, Session.Messages.LastOrDefault());
            }
        }

        public async Task PatAsync()
        {
            if (Session == null)
            {
                return;
            }

            await AppServices.Data.SendPatAsync(Session.Id, Session.IsGroup ? null : Session.Id).ConfigureAwait(true);
            MessageAppended?.Invoke(this, Session.Messages.LastOrDefault());
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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoonWeChat.Models;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 示例数据适配：把静态 SampleDataService 包成 IChatDataService，
    /// 发送逻辑仍是纯本地插入（和原先 ChatViewModel 行为一致）。
    /// </summary>
    public sealed class SampleChatDataService : IChatDataService
    {
        public bool IsSample => true;
        public string MyAccent => SampleDataService.MyAccent;

        /// <summary>示例数据不走网络，永远没有刷新错误。</summary>
        public string LastRefreshError => string.Empty;
        public string MyDisplayName => "我";

#pragma warning disable CS0067 // 示例模式不主动推送，事件由接口约定保留
        public event EventHandler SessionsChanged;
        public event EventHandler ContactsChanged;
        public event EventHandler<ChatMessageEventArgs> MessageReceived;
#pragma warning restore CS0067

        public IReadOnlyList<ChatSession> GetSessions() => SampleDataService.GetSessions();

        public ChatSession GetSessionById(string id) => SampleDataService.GetSessionById(id);

        public IReadOnlyList<Contact> GetContacts() => SampleDataService.GetContacts();

        public Task RefreshAsync() => Task.FromResult(0);

        public Task<ChatMessage> SendTextAsync(string sessionId, string text)
        {
            var session = GetSessionById(sessionId);
            if (session == null || string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult<ChatMessage>(null);
            }

            var now = DateTimeOffset.Now;
            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = MessageType.Text,
                IsMine = true,
                SenderId = "me",
                SenderName = MyDisplayName,
                SenderAvatar = MyAccent,
                Timestamp = now,
                TimeText = now.ToString("HH:mm"),
                Content = text.Trim(),
                Status = MessageStatus.Sent,
            };

            session.Messages.Add(message);
            TouchSessionPreview(session, message);
            return Task.FromResult(message);
        }

        public Task<ChatMessage> SendPlaceholderAsync(string sessionId, MessageType type, string label)
        {
            var session = GetSessionById(sessionId);
            if (session == null)
            {
                return Task.FromResult<ChatMessage>(null);
            }

            var now = DateTimeOffset.Now;
            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = type,
                IsMine = true,
                SenderId = "me",
                SenderName = MyDisplayName,
                SenderAvatar = MyAccent,
                Timestamp = now,
                TimeText = now.ToString("HH:mm"),
                Status = MessageStatus.Sent,
            };

            switch (type)
            {
                case MessageType.Image:
                    message.MediaAccentColor = "#576B95";
                    message.MediaAspectRatio = 0.75;
                    break;
                case MessageType.Voice:
                    message.DurationSeconds = 3;
                    break;
                case MessageType.Location:
                    message.LocationName = "我的位置";
                    message.LocationAddress = "演示占位地址 · 真实接口会带真实定位";
                    break;
                case MessageType.ContactCard:
                    message.CardName = "苏晚";
                    message.CardAvatar = "#F1592A";
                    break;
                case MessageType.File:
                    message.FileName = "演示文件.txt";
                    message.FileExtension = "TXT";
                    message.FileSizeText = "12 KB";
                    break;
                default:
                    message.Type = MessageType.Text;
                    message.Content = $"[演示占位] 这里会调用 WeChatPadPro 的{label}发送接口";
                    break;
            }

            session.Messages.Add(message);
            TouchSessionPreview(session, message);
            return Task.FromResult(message);
        }

        public Task<ChatMessage> SendImageAsync(string sessionId, string imageBase64, string fileName = "image.jpg")
        {
            // 示例模式：不解码图片，只插一条图片气泡
            return SendPlaceholderAsync(sessionId, MessageType.Image, "图片");
        }

        public Task<bool> RetrySendAsync(string sessionId, ChatMessage message)
        {
            if (message == null)
            {
                return Task.FromResult(false);
            }

            message.Status = MessageStatus.Sent;
            return Task.FromResult(true);
        }

        public Task<bool> RevokeLastMineAsync(string sessionId)
        {
            var session = GetSessionById(sessionId);
            if (session == null)
            {
                return Task.FromResult(false);
            }

            for (int i = session.Messages.Count - 1; i >= 0; i--)
            {
                if (session.Messages[i].IsMine)
                {
                    session.Messages.RemoveAt(i);
                    session.Messages.Add(new ChatMessage
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Type = MessageType.Recall,
                        Content = "你撤回了一条消息",
                        Timestamp = DateTimeOffset.Now,
                        TimeText = DateTimeOffset.Now.ToString("HH:mm"),
                        Status = MessageStatus.Sent
                    });
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }

        public Task<ChatMessage> SendFileAsync(string sessionId, string fileBase64, string fileName)
        {
            return SendPlaceholderAsync(sessionId, MessageType.File, fileName ?? "文件");
        }

        public Task<bool> SendPatAsync(string sessionId, string targetWxId = null)
        {
            var session = GetSessionById(sessionId);
            if (session == null)
            {
                return Task.FromResult(false);
            }

            session.Messages.Add(new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = MessageType.Pat,
                Content = "你拍了拍对方",
                Timestamp = DateTimeOffset.Now,
                TimeText = DateTimeOffset.Now.ToString("HH:mm"),
                Status = MessageStatus.Sent
            });
            return Task.FromResult(true);
        }

        public Task<ChatMessage> SendQuoteAsync(string sessionId, string text, ChatMessage quoteOf)
        {
            var session = GetSessionById(sessionId);
            if (session == null || string.IsNullOrWhiteSpace(text))
            {
                return Task.FromResult<ChatMessage>(null);
            }

            var message = new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = MessageType.Text,
                IsMine = true,
                SenderName = MyDisplayName,
                SenderAvatar = MyAccent,
                Content = text.Trim(),
                QuotedSenderName = quoteOf == null ? null : (quoteOf.IsMine ? "我" : quoteOf.SenderName),
                QuotedContent = quoteOf?.Content,
                Timestamp = DateTimeOffset.Now,
                TimeText = DateTimeOffset.Now.ToString("HH:mm"),
                Status = MessageStatus.Sent
            };
            session.Messages.Add(message);
            return Task.FromResult(message);
        }

        public Task EnrichSessionAsync(string sessionId) => Task.FromResult(0);

        private static void TouchSessionPreview(ChatSession session, ChatMessage message)
        {
            session.LastMessagePreview = PreviewFor(message);
            session.LastMessageSenderPrefix = null;
            session.LastMessageTimeText = message.TimeText;
        }

        private static string PreviewFor(ChatMessage message)
        {
            switch (message.Type)
            {
                case MessageType.Text:
                    return message.Content ?? string.Empty;
                case MessageType.Image:
                    return "[图片]";
                case MessageType.Voice:
                    return "[语音]";
                case MessageType.Video:
                    return "[视频]";
                case MessageType.File:
                    return "[文件]";
                case MessageType.Location:
                    return "[位置]";
                case MessageType.ContactCard:
                    return "[名片]";
                case MessageType.Link:
                    return "[链接]";
                case MessageType.MiniProgram:
                    return "[小程序]";
                case MessageType.Emoji:
                    return "[动画表情]";
                default:
                    return message.Content ?? string.Empty;
            }
        }

    }
}

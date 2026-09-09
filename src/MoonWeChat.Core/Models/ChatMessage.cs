using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MoonWeChat.Models
{
    /// <summary>
    /// 一条聊天消息。字段设计尽量贴近 WeChatPadPro Webhook 推送 / 消息发送接口
    /// 会给到的数据（发送者、类型、内容、各类型专属附加字段），具体见
    /// docs/WeChatPadPro-API-notes.md。
    /// </summary>
    public class ChatMessage : INotifyPropertyChanged
    {
        /// <summary>消息唯一 ID（对应后端的 clientMsgId / msgId）。</summary>
        public string Id { get; set; }

        /// <summary>
        /// 服务端消息 id（MAX v8 里的 svr_id / new_msg_id，64 位整数的字符串形式）。
        /// 收到的消息在 <see cref="MessageMapper"/> 里填；自己发出的消息在发送应答里回填。
        /// 撤回和引用都必须用它 —— <see cref="Id"/> 对自己发的消息是本地 GUID，
        /// 服务端不认。
        /// </summary>
        public string ServerMsgId { get; set; }

        /// <summary>服务端回的 ClientMsgId（int64），撤回时要一起带上。</summary>
        public string ServerClientMsgId { get; set; }

        /// <summary>服务端消息时间（Unix 秒），撤回参数 CreateTime。</summary>
        public long ServerCreateTime { get; set; }

        /// <summary>服务端消息类型（协议值，1=文本 3=图片 34=语音 …），引用回复要回填。</summary>
        public int ServerMsgType { get; set; }

        /// <summary>发送者 wxid。群聊里用它区分不同成员。</summary>
        public string SenderId { get; set; }

        /// <summary>发送者昵称（群聊内可能是群昵称，与好友昵称不同）。</summary>
        public string SenderName { get; set; }

        /// <summary>发送者头像（图片路径 / Uri）。</summary>
        public string SenderAvatar { get; set; }

        /// <summary>是否是“我”发出的消息，决定气泡靠左/靠右、颜色。</summary>
        public bool IsMine { get; set; }

        /// <summary>消息类型。</summary>
        public MessageType Type { get; set; }

        /// <summary>时间戳。</summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>格式化好的时间文本（如 "14:32"），示例数据里直接算好，避免页面里再放转换逻辑。</summary>
        public string TimeText { get; set; }

        private MessageStatus _status = MessageStatus.Sent;

        /// <summary>发送状态，仅 IsMine=true 时有意义。Sending→Sent/Failed 会通知 UI。</summary>
        public MessageStatus Status
        {
            get => _status;
            set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsSending));
            }
        }

        public bool IsFailed => IsMine && Status == MessageStatus.Failed;

        public bool IsSending => IsMine && Status == MessageStatus.Sending;

        private string _errorText = string.Empty;

        /// <summary>发送失败时的可诊断原因，避免 UI 只显示一个没有上下文的失败图标。</summary>
        public string ErrorText
        {
            get => _errorText;
            set
            {
                var next = value ?? string.Empty;
                if (_errorText == next)
                {
                    return;
                }

                _errorText = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasErrorText));
            }
        }

        public bool HasErrorText => !string.IsNullOrWhiteSpace(ErrorText);

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 群聊里，是否需要在气泡上方显示发送者昵称
        /// （单聊不显示；群聊里“我”发的也不显示）。示例数据里直接算好。
        /// </summary>
        public bool ShowSenderName { get; set; }

        // ------- 文本 -------
        /// <summary>文本内容；也用作各类卡片的主标题/祝福语/文件名等主要文案的兜底字段。</summary>
        public string Content { get; set; }

        /// <summary>被引用/回复的原消息发送者（有值即代表这是一条带引用的消息）。</summary>
        public string QuotedSenderName { get; set; }

        /// <summary>被引用/回复的原消息内容摘要。</summary>
        public string QuotedContent { get; set; }

        /// <summary>是否带引用块，XAML 里直接绑这个，不用再写转换器判空。</summary>
        public bool HasQuote => !string.IsNullOrEmpty(QuotedContent);

        // ------- 图片 / 视频 / 表情 -------
        /// <summary>图片/视频缩略图的强调色（示例数据没有真实图床，用色块模拟图片占位）。</summary>
        public string MediaAccentColor { get; set; }

        /// <summary>缩略图宽高比（宽/高），控制图片气泡的形状。</summary>
        public double MediaAspectRatio { get; set; } = 1.0;

        /// <summary>图片/视频占位卡片的显示宽度，固定给一个贴近微信实机的尺寸。</summary>
        public double MediaDisplayWidth { get; set; } = 160;

        /// <summary>按宽高比反推出的显示高度。</summary>
        public double MediaDisplayHeight => MediaAspectRatio > 0 ? System.Math.Round(MediaDisplayWidth / MediaAspectRatio) : MediaDisplayWidth;

        /// <summary>视频/语音时长（秒）。</summary>
        public int DurationSeconds { get; set; }

        // ------- 语音 -------
        /// <summary>语音消息是否未读（未读语音在会话列表和气泡上会有绿点）。</summary>
        public bool IsVoiceUnread { get; set; }

        /// <summary>语音气泡里显示的时长文案，如 "3″"。</summary>
        public string VoiceDurationText => DurationSeconds + "″";

        /// <summary>语音气泡宽度：微信语音条会按时长变长，这里按秒数分几档、封顶。</summary>
        public double VoiceBubbleWidth => 88 + System.Math.Min(DurationSeconds, 40) * 3.2;

        /// <summary>视频/时长类气泡右下角显示的 mm:ss 文案。</summary>
        public string DurationText => System.TimeSpan.FromSeconds(DurationSeconds).ToString(DurationSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss");

        // ------- 文件 -------
        public string FileName { get; set; }
        public string FileSizeText { get; set; }
        public string FileExtension { get; set; }

        // ------- 位置 -------
        public string LocationName { get; set; }
        public string LocationAddress { get; set; }

        // ------- 名片 -------
        public string CardName { get; set; }
        public string CardAvatar { get; set; }

        // ------- 链接分享 -------
        public string LinkTitle { get; set; }
        public string LinkSource { get; set; }

        // ------- 小程序 -------
        public string MiniProgramTitle { get; set; }
        public string MiniProgramAppName { get; set; }

        }
}

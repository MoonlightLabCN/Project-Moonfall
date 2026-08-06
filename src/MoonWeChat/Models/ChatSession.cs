using System;
using System.Collections.ObjectModel;

namespace MoonWeChat.Models
{
    /// <summary>
    /// 会话列表（“微信”主页）里的一行。对应真实微信的“最近聊天”，
    /// 与通讯录里的 Contact 是两个概念：一个联系人可能从未开始过会话。
    /// </summary>
    public class ChatSession
    {
        /// <summary>会话 ID：单聊用对方 wxid，群聊用群 chatroomId。</summary>
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string AvatarAccentColor { get; set; }

        /// <summary>远程头像 URL（好友/群头像）。</summary>
        public string AvatarUrl { get; set; }

        public bool IsGroup { get; set; }

        /// <summary>群聊人数（仅 IsGroup=true 时展示）。</summary>
        public int MemberCount { get; set; }

        /// <summary>最后一条消息的预览文案（非文本类型显示形如“[图片]”的占位）。</summary>
        public string LastMessagePreview { get; set; }

        /// <summary>群聊里最后一条消息展示为 “昵称: 内容”，单聊不需要发送者前缀。</summary>
        public string LastMessageSenderPrefix { get; set; }

        /// <summary>
        /// 列表行里真正显示的那一串预览文字：群聊是 "昵称: 内容"，单聊就是纯内容。
        /// 这里直接拼好，XAML 里绑一个 TextBlock 就行，不用在标记里再判断 IsGroup。
        /// </summary>
        public string PreviewText => string.IsNullOrEmpty(LastMessageSenderPrefix)
            ? LastMessagePreview
            : $"{LastMessageSenderPrefix}: {LastMessagePreview}";

        public string LastMessageTimeText { get; set; }

        public int UnreadCount { get; set; }

        /// <summary>未读角标是否要显示（0 条不显示）。</summary>
        public bool HasUnread => UnreadCount > 0;

        /// <summary>角标文案，超过 99 显示 "99+"，微信自己就是这个规则。</summary>
        public string UnreadBadgeText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

        public bool IsMuted { get; set; }

        /// <summary>未开免打扰 + 有未读 -&gt; 显示数字角标。</summary>
        public bool ShowUnreadNumber => HasUnread && !IsMuted;

        /// <summary>开了免打扰但仍有未读 -&gt; 只显示一个小灰点，不显示数字（微信自己就是这样处理的）。</summary>
        public bool ShowMutedDot => HasUnread && IsMuted;

        public bool IsPinned { get; set; }

        /// <summary>该会话的完整消息记录，聊天页从这里读取。</summary>
        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();
    }
}

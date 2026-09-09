using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MoonWeChat.Models
{
    /// <summary>
    /// 会话列表（“微信”主页）里的一行。对应真实微信的“最近聊天”，
    /// 与通讯录里的 Contact 是两个概念：一个联系人可能从未开始过会话。
    ///
    /// 必须实现 INotifyPropertyChanged：会话列表的未读角标 / 最后一条预览 / 时间
    /// 都是收到消息后原地改这个对象的属性。没有通知的话，两个 Shell 的列表都只能
    /// 靠整表 Clear+Add 才刷新，滚动位置会被打回顶部。
    /// </summary>
    public class ChatSession : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _displayName;
        private string _avatarAccentColor;
        private string _avatarUrl;
        private bool _isGroup;
        private int _memberCount;
        private string _lastMessagePreview;
        private string _lastMessageSenderPrefix;
        private string _lastMessageTimeText;
        private int _unreadCount;
        private bool _isMuted;
        private bool _isPinned;

        /// <summary>会话 ID：单聊用对方 wxid，群聊用群 chatroomId。</summary>
        public string Id { get; set; }

        public string DisplayName
        {
            get { return _displayName; }
            set { SetProperty(ref _displayName, value); }
        }

        public string AvatarAccentColor
        {
            get { return _avatarAccentColor; }
            set { SetProperty(ref _avatarAccentColor, value); }
        }

        /// <summary>远程头像 URL（好友/群头像）。</summary>
        public string AvatarUrl
        {
            get { return _avatarUrl; }
            set { SetProperty(ref _avatarUrl, value); }
        }

        public bool IsGroup
        {
            get { return _isGroup; }
            set { SetProperty(ref _isGroup, value); }
        }

        /// <summary>群聊人数（仅 IsGroup=true 时展示）。</summary>
        public int MemberCount
        {
            get { return _memberCount; }
            set { SetProperty(ref _memberCount, value); }
        }

        /// <summary>最后一条消息的预览文案（非文本类型显示形如“[图片]”的占位）。</summary>
        public string LastMessagePreview
        {
            get { return _lastMessagePreview; }
            set
            {
                if (SetProperty(ref _lastMessagePreview, value))
                {
                    OnPropertyChanged(nameof(PreviewText));
                }
            }
        }

        /// <summary>群聊里最后一条消息展示为 “昵称: 内容”，单聊不需要发送者前缀。</summary>
        public string LastMessageSenderPrefix
        {
            get { return _lastMessageSenderPrefix; }
            set
            {
                if (SetProperty(ref _lastMessageSenderPrefix, value))
                {
                    OnPropertyChanged(nameof(PreviewText));
                }
            }
        }

        /// <summary>
        /// 列表行里真正显示的那一串预览文字：群聊是 "昵称: 内容"，单聊就是纯内容。
        /// 这里直接拼好，XAML 里绑一个 TextBlock 就行，不用在标记里再判断 IsGroup。
        /// </summary>
        public string PreviewText => string.IsNullOrEmpty(LastMessageSenderPrefix)
            ? LastMessagePreview
            : $"{LastMessageSenderPrefix}: {LastMessagePreview}";

        public string LastMessageTimeText
        {
            get { return _lastMessageTimeText; }
            set { SetProperty(ref _lastMessageTimeText, value); }
        }

        public int UnreadCount
        {
            get { return _unreadCount; }
            set
            {
                if (SetProperty(ref _unreadCount, value))
                {
                    OnPropertyChanged(nameof(HasUnread));
                    OnPropertyChanged(nameof(UnreadBadgeText));
                    OnPropertyChanged(nameof(ShowUnreadNumber));
                    OnPropertyChanged(nameof(ShowMutedDot));
                }
            }
        }

        /// <summary>未读角标是否要显示（0 条不显示）。</summary>
        public bool HasUnread => UnreadCount > 0;

        /// <summary>角标文案，超过 99 显示 "99+"，微信自己就是这个规则。</summary>
        public string UnreadBadgeText => UnreadCount > 99 ? "99+" : UnreadCount.ToString();

        public bool IsMuted
        {
            get { return _isMuted; }
            set
            {
                if (SetProperty(ref _isMuted, value))
                {
                    OnPropertyChanged(nameof(ShowUnreadNumber));
                    OnPropertyChanged(nameof(ShowMutedDot));
                }
            }
        }

        /// <summary>未开免打扰 + 有未读 -&gt; 显示数字角标。</summary>
        public bool ShowUnreadNumber => HasUnread && !IsMuted;

        /// <summary>开了免打扰但仍有未读 -&gt; 只显示一个小灰点，不显示数字（微信自己就是这样处理的）。</summary>
        public bool ShowMutedDot => HasUnread && IsMuted;

        public bool IsPinned
        {
            get { return _isPinned; }
            set { SetProperty(ref _isPinned, value); }
        }

        /// <summary>该会话的完整消息记录，聊天页从这里读取。</summary>
        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

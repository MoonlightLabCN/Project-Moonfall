using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoonWeChat.Models;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 会话 / 联系人 / 发消息的统一数据面。
    /// 示例模式和 WeChatPadPro 实机模式都实现这个接口，ViewModel 不关心底层。
    /// </summary>
    public interface IChatDataService
    {
        bool IsSample { get; }
        string MyAccent { get; }

        /// <summary>
        /// 上一次 <see cref="RefreshAsync"/> 的失败原因，成功时为空。
        /// 网关现在会如实上报「通讯录同步失败：微信窗口被挡住」这类诊断，
        /// 客户端必须把它显示出来 —— 否则界面上和「同步成功但一个联系人都没有」
        /// 没有区别，用户无从判断。
        /// </summary>
        string LastRefreshError { get; }
        string MyDisplayName { get; }

        event EventHandler SessionsChanged;
        event EventHandler ContactsChanged;
        event EventHandler<ChatMessageEventArgs> MessageReceived;

        IReadOnlyList<ChatSession> GetSessions();
        ChatSession GetSessionById(string id);
        IReadOnlyList<Contact> GetContacts();

        Task RefreshAsync();

        Task<ChatMessage> SendTextAsync(string sessionId, string text);

        /// <summary>
        /// 非文本快捷发送（位置/名片等）。实机按类型调 WeChatPadPro。
        /// </summary>
        Task<ChatMessage> SendPlaceholderAsync(string sessionId, MessageType type, string label);

        /// <summary>
        /// 发送图片（base64，不含 data: 前缀亦可）。
        /// </summary>
        Task<ChatMessage> SendImageAsync(string sessionId, string imageBase64, string fileName = "image.jpg");

        /// <summary>
        /// 对 Failed 状态的我方消息重发（目前支持文本；其它类型按 type 再调）。
        /// </summary>
        Task<bool> RetrySendAsync(string sessionId, ChatMessage message);

        /// <summary>撤回我方最近一条可撤回消息（实机 /Msg/Revoke）。</summary>
        Task<bool> RevokeLastMineAsync(string sessionId);

        /// <summary>发送文件 base64。</summary>
        Task<ChatMessage> SendFileAsync(string sessionId, string fileBase64, string fileName);

        /// <summary>拍一拍（群内对方 wxid，单聊可传对方 id）。</summary>
        Task<bool> SendPatAsync(string sessionId, string targetWxId = null);

        /// <summary>引用回复文本。</summary>
        Task<ChatMessage> SendQuoteAsync(string sessionId, string text, ChatMessage quoteOf);

        /// <summary>打开会话时刷新对方资料/群人数。</summary>
        Task EnrichSessionAsync(string sessionId);
    }

    public sealed class ChatMessageEventArgs : EventArgs
    {
        public ChatMessageEventArgs(string sessionId, ChatMessage message)
        {
            SessionId = sessionId;
            Message = message;
        }

        public string SessionId { get; }
        public ChatMessage Message { get; }
    }
}

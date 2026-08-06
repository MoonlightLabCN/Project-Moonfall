namespace MoonWeChat.Models
{
    /// <summary>
    /// 消息类型。取值范围对齐 docs/WeChatPadPro-API-notes.md 第3节列出的、
    /// WeChatPadPro 后端真实支持收发的消息种类，UI 不设计后端不具备的类型。
    /// </summary>
    public enum MessageType
    {
        /// <summary>纯文本（含被引用/回复的情况，引用信息单独放在 QuotedContent 里）。</summary>
        Text,

        /// <summary>图片消息。</summary>
        Image,

        /// <summary>动图/表情包（协议上与图片是独立类型）。</summary>
        Emoji,

        /// <summary>语音消息，携带时长。</summary>
        Voice,

        /// <summary>视频消息，携带时长与封面。</summary>
        Video,

        /// <summary>文件/文档消息。</summary>
        File,

        /// <summary>位置消息。</summary>
        Location,

        /// <summary>名片（分享联系人）。</summary>
        ContactCard,

        /// <summary>图文链接分享。</summary>
        Link,

        /// <summary>小程序卡片分享。</summary>
        MiniProgram,

        // 红包 / 转账已刻意不做：资金链路容易触发平台风控。

        /// <summary>撤回提示（系统消息变体）。</summary>
        Recall,

        /// <summary>拍一拍提示（系统消息变体）。</summary>
        Pat,

        /// <summary>入群/群公告等系统提示。</summary>
        SystemNotice,

        /// <summary>时间分隔线，纯 UI 辅助类型，不对应后端协议字段。</summary>
        DateDivider
    }
}

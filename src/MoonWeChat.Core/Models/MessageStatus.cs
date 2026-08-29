namespace MoonWeChat.Models
{
    /// <summary>
    /// 仅用于“我方发出”的消息，表示这条消息目前的发送状态。
    /// 对应 WeChatPadPro 里发消息接口的同步返回 + Webhook 状态回调两种信号来源。
    /// </summary>
    public enum MessageStatus
    {
        /// <summary>已成功发出（默认状态，多数示例数据用这个）。</summary>
        Sent,

        /// <summary>正在发送中（等待后端返回 clientMsgId / 发送回执）。</summary>
        Sending,

        /// <summary>发送失败（网络失败或被风控拦截），前端要给出可点击重发的入口。</summary>
        Failed
    }
}

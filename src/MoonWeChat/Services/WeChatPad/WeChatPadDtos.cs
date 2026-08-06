using System;
using System.Collections.Generic;

namespace MoonWeChat.Services.WeChatPad
{
    public sealed class ApiCallResult
    {
        public bool Ok { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public string RawJson { get; set; }
        public string DataJson { get; set; }
    }

    public sealed class QrLoginResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        /// <summary>轮询 CheckLoginStatus 用的 uuid / key。</summary>
        public string Uuid { get; set; }
        /// <summary>二维码内容字符串（微信登录 URL）。</summary>
        public string QrContent { get; set; }
        /// <summary>可直接绑定 Image 的 base64（不含 data: 前缀）或完整 data URI。</summary>
        public string QrBase64 { get; set; }
        /// <summary>若服务端直接给图片 URL。</summary>
        public string QrUrl { get; set; }
    }

    public enum QrScanState
    {
        Unknown = 0,
        Waiting = 1,
        Scanned = 2,
        Confirmed = 3,
        Expired = 4,
        Failed = 5
    }

    public sealed class LoginStatusResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public QrScanState State { get; set; }
        public string WxId { get; set; }
        public string Nickname { get; set; }
        public string HeadImgUrl { get; set; }
        public string RawJson { get; set; }
    }

    public sealed class PadContactDto
    {
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Remark { get; set; }
        public string Alias { get; set; }
        public string BigHeadImgUrl { get; set; }
        public string SmallHeadImgUrl { get; set; }
        public string Province { get; set; }
        public string City { get; set; }
        public string Signature { get; set; }
        public bool IsChatroom { get; set; }
    }

    public sealed class PadMessageDto
    {
        public string MsgId { get; set; }
        public string NewMsgId { get; set; }
        public string FromUserName { get; set; }
        public string ToUserName { get; set; }
        public int MsgType { get; set; }
        public string Content { get; set; }
        public long CreateTime { get; set; }
        public string PushContent { get; set; }
        public string ImgBufBase64 { get; set; }
        public bool IsGroup
        {
            get
            {
                return (FromUserName != null && FromUserName.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
                    || (ToUserName != null && ToUserName.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public sealed class FriendListResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public List<PadContactDto> Contacts { get; set; } = new List<PadContactDto>();
    }

    public sealed class SyncMsgResult
    {
        public bool Ok { get; set; }
        public string Message { get; set; }
        public List<PadMessageDto> Messages { get; set; } = new List<PadMessageDto>();
    }

    /// <summary>当前 Token 在 WeChatPadPro 上的微信会话是否仍在线。</summary>
    public sealed class DeviceSessionProbe
    {
        public bool Online { get; set; }
        public string Message { get; set; }
        public string WxId { get; set; }
        public string Nickname { get; set; }
    }
}

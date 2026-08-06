namespace MoonWeChat.Models
{
    /// <summary>
    /// 通讯录联系人。对应 WeChatPadPro 的好友详情接口（昵称/备注/头像/地区/标签/wxid）。
    /// </summary>
    public class Contact
    {
        public string WxId { get; set; }
        public string Nickname { get; set; }
        public string Remark { get; set; }
        public string AvatarAccentColor { get; set; }
        /// <summary>远程头像 URL；空则 AvatarView 回落文字头像。</summary>
        public string AvatarUrl { get; set; }
        public string Region { get; set; }
        public string Signature { get; set; }
        public string PinyinIndex { get; set; }

        public string DisplayName => string.IsNullOrEmpty(Remark) ? Nickname : Remark;
    }
}


using System;
using System.Linq;
using MoonWeChat.Models;

namespace MoonWeChat.Services.WeChatPad
{
    /// <summary>
    /// WeChatPadPro 协议消息 → 前端 ChatMessage。
    /// MsgType 取值因版本略有差异，这里按微信经典类型做宽松映射。
    /// </summary>
    public static class MessageMapper
    {
        // 经典 MsgType：1 文本, 3 图片, 34 语音, 43 视频, 47 表情, 48 位置, 49 app/链接/文件, 10000 系统, 10002 撤回
        public static ChatMessage ToChatMessage(PadMessageDto dto, string selfWxId, string selfName, string myAccent, string peerAccent)
        {
            if (dto == null)
            {
                return null;
            }

            var from = dto.FromUserName ?? string.Empty;
            var to = dto.ToUserName ?? string.Empty;
            var isMine = !string.IsNullOrEmpty(selfWxId) &&
                         string.Equals(from, selfWxId, StringComparison.OrdinalIgnoreCase);

            // 群聊内容常见 "wxid:\n正文"
            var senderId = from;
            var content = dto.Content ?? string.Empty;
            string groupMemberName = null;
            if (dto.IsGroup && !isMine && content.Contains(":\n"))
            {
                var idx = content.IndexOf(":\n", StringComparison.Ordinal);
                if (idx > 0)
                {
                    senderId = content.Substring(0, idx);
                    content = content.Substring(idx + 2);
                }
            }

            var ts = dto.CreateTime > 0
                ? new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(dto.CreateTime).ToLocalTime()
                : DateTimeOffset.Now;

            var message = new ChatMessage
            {
                Id = FirstNonEmpty(dto.NewMsgId, dto.MsgId, Guid.NewGuid().ToString("N")),

                // 服务端原始标识：撤回 / 引用回复必须用这些，不能用本地 Id。
                ServerMsgId = FirstNonEmpty(dto.NewMsgId, dto.MsgId, string.Empty),
                ServerClientMsgId = dto.MsgId ?? string.Empty,
                ServerCreateTime = dto.CreateTime,
                ServerMsgType = dto.MsgType,

                SenderId = isMine ? "me" : senderId,
                SenderName = isMine ? (selfName ?? "我") : (groupMemberName ?? senderId),
                SenderAvatar = isMine ? myAccent : peerAccent,
                IsMine = isMine,
                Timestamp = ts,
                TimeText = ts.ToString("HH:mm"),
                Status = MessageStatus.Sent,
                ShowSenderName = dto.IsGroup && !isMine,
                Content = content,
            };

            ApplyType(message, dto.MsgType, content);
            return message;
        }

        public static string SessionIdFor(PadMessageDto dto, string selfWxId)
        {
            if (dto == null)
            {
                return null;
            }

            var from = dto.FromUserName ?? string.Empty;
            var to = dto.ToUserName ?? string.Empty;

            if (from.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
            {
                return from;
            }

            if (to.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
            {
                return to;
            }

            // 单聊：会话 id 是对方 wxid
            if (!string.IsNullOrEmpty(selfWxId))
            {
                if (string.Equals(from, selfWxId, StringComparison.OrdinalIgnoreCase))
                {
                    return to;
                }

                if (string.Equals(to, selfWxId, StringComparison.OrdinalIgnoreCase))
                {
                    return from;
                }
            }

            return string.IsNullOrEmpty(from) ? to : from;
        }

        public static string PreviewFor(ChatMessage message)
        {
            if (message == null)
            {
                return string.Empty;
            }

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
                    return "[文件] " + (message.FileName ?? string.Empty);
                case MessageType.Location:
                    return "[位置] " + (message.LocationName ?? string.Empty);
                case MessageType.ContactCard:
                    return "[名片] " + (message.CardName ?? string.Empty);
                case MessageType.Link:
                    return "[链接] " + (message.LinkTitle ?? string.Empty);
                case MessageType.MiniProgram:
                    return "[小程序] " + (message.MiniProgramTitle ?? string.Empty);
                case MessageType.Emoji:
                    return "[动画表情]";
                case MessageType.Recall:
                    return message.Content ?? "撤回了一条消息";
                case MessageType.Pat:
                    return message.Content ?? "拍了拍";
                case MessageType.SystemNotice:
                    return message.Content ?? string.Empty;
                default:
                    return message.Content ?? string.Empty;
            }
        }

        public static string AccentFor(string seed)
        {
            // 稳定色：同一 wxid 永远同色
            if (string.IsNullOrEmpty(seed))
            {
                return "#576B95";
            }

            var palette = new[]
            {
                "#F1592A", "#576B95", "#9C6ADE", "#10AEFF", "#FA9D3B",
                "#07C160", "#FA5151", "#1485EE", "#EE3F4D", "#2C9678",
            };

            unchecked
            {
                int hash = 17;
                foreach (var ch in seed)
                {
                    hash = hash * 31 + ch;
                }

                if (hash < 0)
                {
                    hash = -hash;
                }

                return palette[hash % palette.Length];
            }
        }

        public static string PinyinIndexFor(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "#";
            }

            var c = name.Trim()[0];
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
            {
                return char.ToUpperInvariant(c).ToString();
            }

            // 中文等：归到 #，真要拼音分组可后续接库
            return "#";
        }

        private static void ApplyType(ChatMessage message, int msgType, string content)
        {
            switch (msgType)
            {
                case 3:
                    message.Type = MessageType.Image;
                    message.MediaAccentColor = "#3A3F58";
                    message.MediaAspectRatio = 0.75;
                    break;
                case 34:
                    message.Type = MessageType.Voice;
                    message.DurationSeconds = 1;
                    break;
                case 43:
                    message.Type = MessageType.Video;
                    message.MediaAccentColor = "#1F1F1F";
                    message.MediaAspectRatio = 0.75;
                    message.DurationSeconds = 1;
                    break;
                case 47:
                    message.Type = MessageType.Emoji;
                    message.MediaAccentColor = "#FA9D3B";
                    break;
                case 48:
                    message.Type = MessageType.Location;
                    message.LocationName = "位置";
                    message.LocationAddress = content;
                    break;
                case 42:
                    message.Type = MessageType.ContactCard;
                    message.CardName = content;
                    message.CardAvatar = "#576B95";
                    break;
                case 49:
                    // appmsg：链接 / 文件 / 小程序。资金类（红包/转账）降级为系统提示，不做专属气泡。
                    if (Contains(content, "微信红包", "红包", "微信转账", "转账"))
                    {
                        message.Type = MessageType.SystemNotice;
                        message.Content = "[已忽略资金类消息]";
                    }
                    else if (Contains(content, "<type>6</type>", "appattach"))
                    {
                        message.Type = MessageType.File;
                        message.FileName = "文件";
                        message.FileExtension = "FILE";
                        message.FileSizeText = "";
                    }
                    else if (Contains(content, "miniprogram", "小程序"))
                    {
                        message.Type = MessageType.MiniProgram;
                        message.MiniProgramTitle = "小程序";
                        message.MiniProgramAppName = "";
                    }
                    else
                    {
                        message.Type = MessageType.Link;
                        message.LinkTitle = FirstNonEmpty(ExtractXmlTag(content, "title"), "链接");
                        message.LinkSource = ExtractXmlTag(content, "sourcedisplayname") ?? "";
                    }
                    break;
                case 10000:
                case 10002:
                    if (Contains(content, "撤回", "recalled"))
                    {
                        message.Type = MessageType.Recall;
                        message.Content = content;
                    }
                    else if (Contains(content, "拍了拍"))
                    {
                        message.Type = MessageType.Pat;
                        message.Content = content;
                    }
                    else
                    {
                        message.Type = MessageType.SystemNotice;
                        message.Content = content;
                    }
                    break;
                default:
                    message.Type = MessageType.Text;
                    message.Content = content;
                    break;
            }
        }

        private static bool Contains(string text, params string[] needles)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (var n in needles)
            {
                if (text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ExtractXmlTag(string xml, string tag)
        {
            if (string.IsNullOrEmpty(xml) || string.IsNullOrEmpty(tag))
            {
                return null;
            }

            var open = "<" + tag + ">";
            var close = "</" + tag + ">";
            var i = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
            {
                return null;
            }

            i += open.Length;
            var j = xml.IndexOf(close, i, StringComparison.OrdinalIgnoreCase);
            if (j < 0)
            {
                return null;
            }

            return xml.Substring(i, j - i).Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }
    }
}

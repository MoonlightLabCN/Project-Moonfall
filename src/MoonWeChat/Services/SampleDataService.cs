using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MoonWeChat.Models;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 示例数据源。所有字段设计都对齐 docs/WeChatPadPro-API-notes.md 里确认过的、
    /// 后端真实能给到的数据形状；没有真实图床/地图服务，用色块 + 文字头像代替。
    /// 真正接后端时，这个类整体替换成“调用 WeChatPadPro Webhook 收到消息后写入
    /// ObservableCollection”的数据层就行，上层 ViewModel/View 不用变。
    /// </summary>
    public static class SampleDataService
    {
        // 用固定的一天做时间基准，方便时间文案对得上、不随运行时间漂移。
        private static readonly DateTimeOffset Today = new DateTimeOffset(2026, 8, 5, 0, 0, 0, TimeSpan.FromHours(8));
        private static readonly DateTimeOffset Yesterday = Today.AddDays(-1);

        public const string MyAccent = "#07C160";

        private static List<ChatSession> _sessions;

        public static List<ChatSession> GetSessions()
        {
            if (_sessions != null)
            {
                return _sessions;
            }

            _sessions = new List<ChatSession>
            {
                BuildSuWanSession(),
                BuildDevGroupSession(),
                BuildFileHelperSession(),
                BuildMomSession(),
                BuildProductGroupSession(),
                BuildOldWangSession(),
                BuildCourierSession(),
                BuildDigitalGroupSession(),
                BuildWeChatTeamSession(),
            };

            return _sessions;
        }

        public static ChatSession GetSessionById(string id) => GetSessions().FirstOrDefault(s => s.Id == id);

        public static List<Contact> GetContacts()
        {
            return new List<Contact>
            {
                new Contact { WxId = "su_wan_92", Nickname = "苏晚", Remark = "苏晚", AvatarAccentColor = "#F1592A", Region = "上海", Signature = "在人间凑数的日子", PinyinIndex = "S" },
                new Contact { WxId = "chen_lao", Nickname = "陈工", Remark = "老陈", AvatarAccentColor = "#576B95", Region = "杭州", Signature = "代码即诗", PinyinIndex = "C" },
                new Contact { WxId = "ken_a", Nickname = "阿KEN", Remark = "", AvatarAccentColor = "#9C6ADE", Region = "深圳", Signature = "美术这块我来出", PinyinIndex = "A" },
                new Contact { WxId = "bai_ze", Nickname = "白泽", Remark = "白泽策划", AvatarAccentColor = "#10AEFF", Region = "北京", Signature = "", PinyinIndex = "B" },
                new Contact { WxId = "mom_001", Nickname = "妈妈", Remark = "妈", AvatarAccentColor = "#FA9D3B", Region = "老家", Signature = "", PinyinIndex = "M" },
                new Contact { WxId = "wang_lao", Nickname = "王建国", Remark = "老王", AvatarAccentColor = "#576B95", Region = "北京", Signature = "退休生活家", PinyinIndex = "W" },
                new Contact { WxId = "filehelper", Nickname = "文件传输助手", Remark = "", AvatarAccentColor = "#07C160", Region = "", Signature = "", PinyinIndex = "W" },
            };
        }

        // ---------------------------------------------------------------
        // 会话 1：单聊——覆盖后端支持的几乎全部消息类型，作为主展示样本
        // ---------------------------------------------------------------
        private static ChatSession BuildSuWanSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "sw_" + (++seq);

            messages.Add(DateDivider(Id(), Today.AddHours(9)));

            messages.Add(Text(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(9).AddMinutes(2),
                "在嘛，新的开屏图我发你了，看看色调"));

            messages.Add(Image(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(9).AddMinutes(2).AddSeconds(20),
                "#3A3F58", 0.72));

            messages.Add(Text(Id(), true, Today.AddHours(9).AddMinutes(5),
                "月亮那个渐变有点太亮了，能再压暗一点吗，感觉要盖过前景的建筑剪影"));

            messages.Add(Text(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(9).AddMinutes(6),
                "懂了，我调调"));

            messages.Add(Emoji(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(9).AddMinutes(6).AddSeconds(10), "👌"));

            messages.Add(Voice(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(9).AddMinutes(30), 14, isUnread: true));

            messages.Add(TextWithQuote(Id(), true, Today.AddHours(9).AddMinutes(33),
                "苏晚", "在嘛，新的开屏图我发你了，看看色调",
                "语音听了，行，就按你说的先出一版，明天早上给我看看"));

            messages.Add(Pat(Id(), Today.AddHours(9).AddMinutes(40), "苏晚 拍了拍 我"));

            messages.Add(DateDivider(Id(), Today.AddHours(13)));

            messages.Add(Video(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(13).AddMinutes(2),
                "#22303A", 0.56, 47));

            messages.Add(Text(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(13).AddMinutes(2).AddSeconds(5),
                "顺便录了个星空摄影素材，看能不能用在结局 CG 里"));

            messages.Add(File(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(13).AddMinutes(10),
                "开屏动效_v3.aep", "AE 工程", "86.4 MB"));

            messages.Add(Text(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(13).AddMinutes(20),
                "辛苦啦，请你喝奶茶 ☕"));

            messages.Add(Text(Id(), true, Today.AddHours(13).AddMinutes(45),
                "收到，回头请你吃饭"));

            messages.Add(Location(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(15).AddMinutes(2),
                "野百合咖啡·外滩店", "上海市黄浦区中山东一路 12 号"));

            messages.Add(Text(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(15).AddMinutes(2).AddSeconds(8),
                "在这边改稿，路过叫我"));

            messages.Add(ContactCard(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(16),
                "阿KEN", "#9C6ADE", "把美术那边的联系方式也甩你，后面找他对接原画"));

            messages.Add(Link(Id(), true, Today.AddHours(16).AddMinutes(30),
                "Windows 10 Mobile 上线倒计时：我们复盘了三个失败案例", "科技乱炖 · 公众号", "#576B95"));

            messages.Add(MiniProgram(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(17),
                "大月墜落狂想 - 内测反馈收集表", "问卷星", "#10AEFF"));

            messages.Add(SystemNotice(Id(), Today.AddHours(17).AddMinutes(1), "苏晚 撤回了一条消息"));

            messages.Add(Text(Id(), false, "su_wan_92", "苏晚", "#F1592A", Today.AddHours(17).AddMinutes(2),
                "打错字了，重发一下：内测表填完记得艾特我"));

            messages.Add(Text(Id(), true, Today.AddHours(18), "收到，晚点弄", status: MessageStatus.Sending));

            messages.Add(Text(Id(), true, Today.AddHours(18).AddMinutes(1),
                "对了图层文件能重新导一份吗，我这边打不开", status: MessageStatus.Failed));

            return new ChatSession
            {
                Id = "su_wan_92",
                DisplayName = "苏晚",
                AvatarAccentColor = "#F1592A",
                IsGroup = false,
                LastMessagePreview = "对了图层文件能重新导一份吗，我这边打不开",
                LastMessageTimeText = "18:01",
                UnreadCount = 2,
                IsMuted = false,
                IsPinned = true,
                Messages = messages,
            };
        }

        // ---------------------------------------------------------------
        // 会话 2：群聊——同一套消息类型在群里的呈现（带发送者昵称/头像）
        // ---------------------------------------------------------------
        private static ChatSession BuildDevGroupSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "grp_" + (++seq);

            messages.Add(DateDivider(Id(), Today.AddHours(10)));

            messages.Add(SystemNotice(Id(), Today.AddHours(10), "\"老陈\" 邀请 \"白泽策划\" 加入了群聊"));

            messages.Add(GroupText(Id(), false, "chen_lao", "老陈", "#576B95", Today.AddHours(10).AddMinutes(1),
                "群里已经拉了，白泽你先看看《大月墜落狂想》的策划案，Windows 10 Mobile 那版 UI 稿也在群文件"));

            messages.Add(GroupFile(Id(), false, "chen_lao", "老陈", "#576B95", Today.AddHours(10).AddMinutes(2),
                "大月墜落狂想_策划案_v6.pdf", "PDF 文档", "4.1 MB"));

            messages.Add(GroupText(Id(), false, "bai_ze", "白泽策划", "#10AEFF", Today.AddHours(10).AddMinutes(20),
                "看完了，剧情节奏很好。第三章要不要加一个朋友圈打卡节点？"));

            messages.Add(GroupTextWithQuote(Id(), true, Today.AddHours(10).AddMinutes(22),
                "白泽策划", "第三章要不要加一个朋友圈打卡节点？",
                "可以，朋友圈时间线这周接 /FriendCircle/GetList，红包转账我们不做"));

            messages.Add(GroupEmoji(Id(), false, "ken_a", "阿KEN", "#9C6ADE", Today.AddHours(10).AddMinutes(23), "🎉"));

            messages.Add(GroupVoice(Id(), false, "ken_a", "阿KEN", "#9C6ADE", Today.AddHours(11), 8, isUnread: false));

            messages.Add(GroupImage(Id(), false, "ken_a", "阿KEN", "#9C6ADE", Today.AddHours(11).AddMinutes(1),
                "#4A3B5C", 0.8));

            messages.Add(GroupText(Id(), false, "ken_a", "阿KEN", "#9C6ADE", Today.AddHours(11).AddMinutes(1).AddSeconds(15),
                "角色立绘第一版，月见和阿墜，先看构图"));

            messages.Add(Pat(Id(), Today.AddHours(11).AddMinutes(30), "老陈 拍了拍 阿KEN"));

            messages.Add(GroupText(Id(), false, "chen_lao", "老陈", "#576B95", Today.AddHours(11).AddMinutes(31),
                "立绘先这样，赶紧把手上的活干完"));

            messages.Add(DateDivider(Id(), Today.AddHours(19)));

            messages.Add(GroupLocation(Id(), false, "bai_ze", "白泽策划", "#10AEFF", Today.AddHours(19).AddMinutes(5),
                "深夜食堂·望京店", "北京市朝阳区望京东路 6 号"));

            messages.Add(GroupText(Id(), false, "bai_ze", "白泽策划", "#10AEFF", Today.AddHours(19).AddMinutes(5).AddSeconds(10),
                "庆祝内测过审，今晚我请客"));

            messages.Add(GroupMiniProgram(Id(), false, "chen_lao", "老陈", "#576B95", Today.AddHours(19).AddMinutes(6),
                "美团 - 深夜食堂(望京店)", "美团", "#FFC300"));

            messages.Add(GroupText(Id(), true, null, null, null, Today.AddHours(19).AddMinutes(40), "群费我记一下，线下结"));

            messages.Add(GroupText(Id(), true, null, null, null, Today.AddHours(19).AddMinutes(41), "先这样，不够我再补"));

            messages.Add(SystemNotice(Id(), Today.AddHours(19).AddMinutes(42), "以下为新消息"));

            messages.Add(GroupText(Id(), false, "ken_a", "阿KEN", "#9C6ADE", Today.AddHours(19).AddMinutes(50),
                "到了到了，靠窗那桌"));

            return new ChatSession
            {
                Id = "grp_moonfall_dev",
                DisplayName = "大月墜落狂想 开发群",
                AvatarAccentColor = "#576B95",
                IsGroup = true,
                MemberCount = 6,
                LastMessageSenderPrefix = "阿KEN",
                LastMessagePreview = "到了到了，靠窗那桌",
                LastMessageTimeText = "19:50",
                UnreadCount = 5,
                IsMuted = false,
                IsPinned = false,
                Messages = messages,
            };
        }

        // ---------------------------------------------------------------
        // 其余会话：轻量填充，保证点进去不是空的，同时覆盖列表页要展示的各种状态
        // ---------------------------------------------------------------
        private static ChatSession BuildFileHelperSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "fh_" + (++seq);

            messages.Add(DateDivider(Id(), Today.AddHours(8)));
            messages.Add(Text(Id(), true, Today.AddHours(8).AddMinutes(1), "A103时间#5"));
            messages.Add(Text(Id(), false, "filehelper", "文件传输助手", "#07C160", Today.AddHours(8).AddMinutes(1).AddSeconds(2),
                "设置成功：同步间隔已设为 4 秒"));
            messages.Add(Text(Id(), true, Today.AddHours(8).AddMinutes(2), "10086"));
            messages.Add(Text(Id(), false, "filehelper", "文件传输助手", "#07C160", Today.AddHours(8).AddMinutes(2).AddSeconds(1),
                "WeChatPadPro 已连接 · 设备在线 · Token 有效期还剩 341 天"));

            return new ChatSession
            {
                Id = "filehelper",
                DisplayName = "文件传输助手",
                AvatarAccentColor = "#07C160",
                IsGroup = false,
                LastMessagePreview = "WeChatPadPro 已连接 · 设备在线 · Token 有效期还剩 341 天",
                LastMessageTimeText = "08:02",
                UnreadCount = 0,
                IsMuted = false,
                IsPinned = true,
                Messages = messages,
            };
        }

        private static ChatSession BuildMomSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "mom_" + (++seq);

            messages.Add(DateDivider(Id(), Yesterday.AddHours(20)));
            messages.Add(Text(Id(), false, "mom_001", "妈妈", "#FA9D3B", Yesterday.AddHours(20).AddMinutes(5), "吃饭了没"));
            messages.Add(Text(Id(), true, Yesterday.AddHours(20).AddMinutes(10), "吃了吃了，在加班"));
            messages.Add(Voice(Id(), false, "mom_001", "妈妈", "#FA9D3B", Yesterday.AddHours(20).AddMinutes(12), 22, isUnread: false));

            return new ChatSession
            {
                Id = "mom_001",
                DisplayName = "妈",
                AvatarAccentColor = "#FA9D3B",
                IsGroup = false,
                LastMessagePreview = "[语音] 22″",
                LastMessageTimeText = "昨天",
                UnreadCount = 0,
                IsMuted = false,
                IsPinned = false,
                Messages = messages,
            };
        }

        private static ChatSession BuildProductGroupSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "pg_" + (++seq);

            messages.Add(DateDivider(Id(), Yesterday.AddHours(9)));
            messages.Add(GroupText(Id(), false, "chen_lao", "老陈", "#576B95", Yesterday.AddHours(9).AddMinutes(2),
                "新需求评审挪到周五下午"));
            messages.Add(GroupText(Id(), false, "bai_ze", "白泽策划", "#10AEFF", Yesterday.AddHours(9).AddMinutes(5),
                "收到"));

            return new ChatSession
            {
                Id = "grp_product",
                DisplayName = "产品需求群",
                AvatarAccentColor = "#9C6ADE",
                IsGroup = true,
                MemberCount = 18,
                LastMessageSenderPrefix = "白泽策划",
                LastMessagePreview = "收到",
                LastMessageTimeText = "昨天",
                UnreadCount = 12,
                IsMuted = true,
                IsPinned = false,
                Messages = messages,
            };
        }

        private static ChatSession BuildOldWangSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "ow_" + (++seq);

            messages.Add(DateDivider(Id(), Yesterday.AddHours(15)));
            messages.Add(Text(Id(), false, "wang_lao", "王建国", "#576B95", Yesterday.AddHours(15).AddMinutes(2),
                "小伙子，你那个 Windows Phone 的应用做得怎么样了"));
            messages.Add(Text(Id(), true, Yesterday.AddHours(15).AddMinutes(10), "在做在做，快了王叔"));

            return new ChatSession
            {
                Id = "wang_lao",
                DisplayName = "王建国",
                AvatarAccentColor = "#576B95",
                IsGroup = false,
                LastMessagePreview = "在做在做，快了王叔",
                LastMessageTimeText = "昨天",
                UnreadCount = 0,
                IsMuted = false,
                IsPinned = false,
                Messages = messages,
            };
        }

        private static ChatSession BuildCourierSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "cr_" + (++seq);

            messages.Add(DateDivider(Id(), Today.AddHours(11)));
            messages.Add(Text(Id(), false, "courier_007", "順丰快递-王师傅", "#F1592A", Today.AddHours(11).AddMinutes(2),
                "包裹到了，方便的话下楼取一下"));
            messages.Add(Location(Id(), false, "courier_007", "順丰快递-王师傅", "#F1592A", Today.AddHours(11).AddMinutes(3),
                "1 号楼快递柜", "已放入 1 号楼快递柜 · 取件码 8846"));

            return new ChatSession
            {
                Id = "courier_007",
                DisplayName = "順丰快递-王师傅",
                AvatarAccentColor = "#F1592A",
                IsGroup = false,
                LastMessagePreview = "[位置] 1 号楼快递柜",
                LastMessageTimeText = "11:03",
                UnreadCount = 1,
                IsMuted = false,
                IsPinned = false,
                Messages = messages,
            };
        }

        private static ChatSession BuildDigitalGroupSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "dg_" + (++seq);

            messages.Add(DateDivider(Id(), Yesterday.AddHours(22)));
            messages.Add(GroupLink(Id(), false, "digital_01", "数码阿飞", "#10AEFF", Yesterday.AddHours(22),
                "iPhone 16 Pro 深度评测：这次影像升级值不值得换", "极客湾 · 公众号", "#576B95"));

            return new ChatSession
            {
                Id = "grp_digital",
                DisplayName = "数码好物拼单",
                AvatarAccentColor = "#10AEFF",
                IsGroup = true,
                MemberCount = 132,
                LastMessageSenderPrefix = "数码阿飞",
                LastMessagePreview = "[链接] iPhone 16 Pro 深度评测：这次影像升级值不值得换",
                LastMessageTimeText = "昨天",
                UnreadCount = 99,
                IsMuted = true,
                IsPinned = false,
                Messages = messages,
            };
        }

        private static ChatSession BuildWeChatTeamSession()
        {
            var messages = new ObservableCollection<ChatMessage>();
            int seq = 0;
            string Id() => "wt_" + (++seq);

            messages.Add(DateDivider(Id(), Yesterday.AddHours(3)));
            messages.Add(Text(Id(), false, "weixin", "微信团队", "#07C160", Yesterday.AddHours(3).AddMinutes(2),
                "你的账号于 03:02 在新设备上登录，如非本人操作请及时修改密码"));

            return new ChatSession
            {
                Id = "weixin_team",
                DisplayName = "微信团队",
                AvatarAccentColor = "#07C160",
                IsGroup = false,
                LastMessagePreview = "你的账号于 03:02 在新设备上登录，如非本人操作请及时修改密码",
                LastMessageTimeText = "昨天",
                UnreadCount = 0,
                IsMuted = false,
                IsPinned = false,
                Messages = messages,
            };
        }

        // ---------------------------------------------------------------
        // 下面是各消息类型的构造小工具，单聊/群聊共用同一个 ChatMessage 模型，
        // 区别只是群聊消息会带上 SenderName/SenderAvatar 并把 ShowSenderName 置 true。
        // ---------------------------------------------------------------

        private static string TimeText(DateTimeOffset t) => t.ToString("HH:mm");

        private static ChatMessage DateDivider(string id, DateTimeOffset t) => new ChatMessage
        {
            Id = id,
            Type = MessageType.DateDivider,
            Timestamp = t,
            Content = FormatDateDivider(t),
        };

        private static string FormatDateDivider(DateTimeOffset t)
        {
            var d = t.Date;
            if (d == Today.Date) return t.ToString("HH:mm");
            if (d == Yesterday.Date) return "昨天 " + t.ToString("HH:mm");
            return t.ToString("M月d日 HH:mm");
        }

        private static ChatMessage SystemNotice(string id, DateTimeOffset t, string text) => new ChatMessage
        {
            Id = id,
            Type = MessageType.SystemNotice,
            Timestamp = t,
            Content = text,
        };

        private static ChatMessage Pat(string id, DateTimeOffset t, string text) => new ChatMessage
        {
            Id = id,
            Type = MessageType.Pat,
            Timestamp = t,
            Content = text,
        };

        private static ChatMessage Text(string id, bool mine, DateTimeOffset t, string content, MessageStatus status = MessageStatus.Sent)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Text,
                IsMine = mine,
                SenderId = mine ? "me" : null,
                Timestamp = t,
                TimeText = TimeText(t),
                Content = content,
                Status = status,
            };

        private static ChatMessage Text(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string content)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Text,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                Content = content,
            };

        private static ChatMessage GroupText(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string content)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Text,
                IsMine = mine,
                SenderId = mine ? "me" : senderId,
                SenderName = mine ? "我" : senderName,
                SenderAvatar = mine ? MyAccent : senderAvatar,
                ShowSenderName = !mine,
                Timestamp = t,
                TimeText = TimeText(t),
                Content = content,
            };

        private static ChatMessage TextWithQuote(string id, bool mine, DateTimeOffset t, string quotedSender, string quotedContent, string content)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Text,
                IsMine = mine,
                Timestamp = t,
                TimeText = TimeText(t),
                Content = content,
                QuotedSenderName = quotedSender,
                QuotedContent = quotedContent,
            };

        private static ChatMessage GroupTextWithQuote(string id, bool mine, DateTimeOffset t, string quotedSender, string quotedContent, string content)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Text,
                IsMine = mine,
                SenderName = mine ? "我" : quotedSender,
                SenderAvatar = mine ? MyAccent : "#10AEFF",
                ShowSenderName = !mine,
                Timestamp = t,
                TimeText = TimeText(t),
                Content = content,
                QuotedSenderName = quotedSender,
                QuotedContent = quotedContent,
            };

        private static ChatMessage Image(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string accent, double ratio)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Image,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                MediaAccentColor = accent,
                MediaAspectRatio = ratio,
            };

        private static ChatMessage GroupImage(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string accent, double ratio)
        {
            var m = Image(id, mine, senderId, senderName, senderAvatar, t, accent, ratio);
            m.ShowSenderName = !mine;
            return m;
        }

        private static ChatMessage Emoji(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string emoji)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Emoji,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                Content = emoji,
            };

        private static ChatMessage GroupEmoji(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string emoji)
        {
            var m = Emoji(id, mine, senderId, senderName, senderAvatar, t, emoji);
            m.ShowSenderName = !mine;
            return m;
        }

        private static ChatMessage Voice(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, int seconds, bool isUnread)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Voice,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                DurationSeconds = seconds,
                IsVoiceUnread = isUnread,
            };

        private static ChatMessage GroupVoice(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, int seconds, bool isUnread)
        {
            var m = Voice(id, mine, senderId, senderName, senderAvatar, t, seconds, isUnread);
            m.ShowSenderName = !mine;
            return m;
        }

        private static ChatMessage Video(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string accent, double ratio, int seconds)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Video,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                MediaAccentColor = accent,
                MediaAspectRatio = ratio,
                DurationSeconds = seconds,
            };

        private static ChatMessage File(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string fileName, string ext, string size)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.File,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                FileName = fileName,
                FileExtension = ext,
                FileSizeText = size,
            };

        private static ChatMessage GroupFile(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string fileName, string ext, string size)
        {
            var m = File(id, mine, senderId, senderName, senderAvatar, t, fileName, ext, size);
            m.ShowSenderName = !mine;
            return m;
        }

        private static ChatMessage Location(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string name, string address)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Location,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                LocationName = name,
                LocationAddress = address,
            };

        private static ChatMessage GroupLocation(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string name, string address)
        {
            var m = Location(id, mine, senderId, senderName, senderAvatar, t, name, address);
            m.ShowSenderName = !mine;
            return m;
        }

        private static ChatMessage ContactCard(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string cardName, string cardAvatar, string content)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.ContactCard,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                CardName = cardName,
                CardAvatar = cardAvatar,
                Content = content,
            };

        private static ChatMessage Link(string id, bool mine, DateTimeOffset t, string title, string source, string accent)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Link,
                IsMine = mine,
                Timestamp = t,
                TimeText = TimeText(t),
                LinkTitle = title,
                LinkSource = source,
                MediaAccentColor = accent,
            };

        private static ChatMessage GroupLink(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string title, string source, string accent)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.Link,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                ShowSenderName = !mine,
                Timestamp = t,
                TimeText = TimeText(t),
                LinkTitle = title,
                LinkSource = source,
                MediaAccentColor = accent,
            };

        private static ChatMessage MiniProgram(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string title, string appName, string accent)
            => new ChatMessage
            {
                Id = id,
                Type = MessageType.MiniProgram,
                IsMine = mine,
                SenderId = senderId,
                SenderName = senderName,
                SenderAvatar = senderAvatar,
                Timestamp = t,
                TimeText = TimeText(t),
                MiniProgramTitle = title,
                MiniProgramAppName = appName,
                MediaAccentColor = accent,
            };

        private static ChatMessage GroupMiniProgram(string id, bool mine, string senderId, string senderName, string senderAvatar, DateTimeOffset t, string title, string appName, string accent)
        {
            var m = MiniProgram(id, mine, senderId, senderName, senderAvatar, t, title, appName, accent);
            m.ShowSenderName = !mine;
            return m;
        }

        }
}

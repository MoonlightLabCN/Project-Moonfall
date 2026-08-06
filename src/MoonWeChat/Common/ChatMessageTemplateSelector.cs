using MoonWeChat.Models;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MoonWeChat.Common
{
    /// <summary>
    /// 选气泡“外壳”用的模板：时间分隔线、居中系统提示（撤回/拍一拍/入群公告）、
    /// 普通左右分布的聊天气泡行，这三种布局完全不同，所以在最外层先分流一次。
    /// 具体消息内容（文本/图片/语音……）交给 <see cref="BubbleContentTemplateSelector"/> 再选一次。
    /// </summary>
    public class MessageRowTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DateDividerRowTemplate { get; set; }
        public DataTemplate SystemNoticeRowTemplate { get; set; }
        public DataTemplate BubbleRowTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (!(item is ChatMessage message))
            {
                return BubbleRowTemplate;
            }

            switch (message.Type)
            {
                case MessageType.DateDivider:
                    return DateDividerRowTemplate;
                case MessageType.SystemNotice:
                case MessageType.Recall:
                case MessageType.Pat:
                    return SystemNoticeRowTemplate;
                default:
                    return BubbleRowTemplate;
            }
        }
    }

    /// <summary>
    /// 气泡“内容”模板：同一个 BubbleRowTemplate 里，用它按 <see cref="MessageType"/>
    /// 换不同的内容呈现（文本 / 图片 / 语音 / 视频 / 文件 / 位置 / 名片 / 链接 / 小程序）。
    /// 不做红包/转账气泡。
    /// </summary>
    public class BubbleContentTemplateSelector : DataTemplateSelector
    {
        public DataTemplate TextTemplate { get; set; }
        public DataTemplate ImageTemplate { get; set; }
        public DataTemplate EmojiTemplate { get; set; }
        public DataTemplate VoiceTemplate { get; set; }
        public DataTemplate VideoTemplate { get; set; }
        public DataTemplate FileTemplate { get; set; }
        public DataTemplate LocationTemplate { get; set; }
        public DataTemplate ContactCardTemplate { get; set; }
        public DataTemplate LinkTemplate { get; set; }
        public DataTemplate MiniProgramTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
            => SelectTemplateCore(item);

        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (!(item is ChatMessage message))
            {
                return TextTemplate;
            }

            switch (message.Type)
            {
                case MessageType.Image:
                    return ImageTemplate;
                case MessageType.Emoji:
                    return EmojiTemplate;
                case MessageType.Voice:
                    return VoiceTemplate;
                case MessageType.Video:
                    return VideoTemplate;
                case MessageType.File:
                    return FileTemplate;
                case MessageType.Location:
                    return LocationTemplate;
                case MessageType.ContactCard:
                    return ContactCardTemplate;
                case MessageType.Link:
                    return LinkTemplate;
                case MessageType.MiniProgram:
                    return MiniProgramTemplate;
                case MessageType.Text:
                default:
                    return TextTemplate;
            }
        }
    }
}

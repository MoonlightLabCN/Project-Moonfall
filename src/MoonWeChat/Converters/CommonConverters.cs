using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace MoonWeChat.Converters
{
    /// <summary>bool -&gt; Visibility，true 显示。传参 "Invert" 可以反过来用。</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool flag = value is bool b && b;
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                flag = !flag;
            }

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>字符串非空 -&gt; Visible，用来控制“引用块”“备注不为空”这类场景。</summary>
    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>对象非 null -&gt; Visible；传 "Invert" 反过来（有图时藏占位字）。</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isNull = value == null;
            if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                isNull = !isNull;
            }

            // 默认：null → Visible（占位提示）；非 null → Collapsed
            // Invert：null → Collapsed；非 null → Visible
            return isNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 消息气泡的“我方/对方”配色。文本、语音这两类气泡有颜色区分，
    /// 其余卡片类型（文件/链接/位置/名片/小程序）在真实微信里不分我方对方，颜色固定，
    /// 所以只有这两类模板会用到这个转换器。
    /// </summary>
    public class MineToBubbleBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush MineBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x95, 0xEC, 0x69));
        private static readonly SolidColorBrush TheirsLightBrush = new SolidColorBrush(Colors.White);
        private static readonly SolidColorBrush TheirsDarkBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isMine = value is bool b && b;
            if (isMine)
            {
                return MineBrush;
            }

            return Application.Current.RequestedTheme == ApplicationTheme.Dark ? TheirsDarkBrush : TheirsLightBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 气泡内文字颜色：我方气泡背景固定是浅绿色，两套主题下都用深色字；
    /// 对方气泡背景跟随主题（浅色白底/深色灰底），字色也要跟着主题走。
    /// </summary>
    public class MineToTextBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush OnMineBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
        private static readonly SolidColorBrush LightPrimaryBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1A));
        private static readonly SolidColorBrush DarkPrimaryBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xF2, 0xF2, 0xF2));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isMine = value is bool b && b;
            if (isMine)
            {
                return OnMineBrush;
            }

            return Application.Current.RequestedTheme == ApplicationTheme.Dark ? DarkPrimaryBrush : LightPrimaryBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>bool IsMine -&gt; HorizontalAlignment，控制气泡靠左/靠右。</summary>
    public class MineToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => (value is bool b && b) ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>int 数量 &gt; 0 -&gt; Visible，用于未读角标、群人数等。</summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => (value is int i && i > 0) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// 用一个字符串（比如联系人名字/会话名）稳定地映射出一个头像底色，
    /// 示例数据没有真实头像图片，用“文字头像”代替，颜色要保证同一个人每次都一样。
    /// </summary>
    public class AccentHexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string hex = value as string;
            if (string.IsNullOrEmpty(hex))
            {
                hex = "#07C160";
            }

            try
            {
                hex = hex.TrimStart('#');
                byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
                byte bch = System.Convert.ToByte(hex.Substring(4, 2), 16);
                return new SolidColorBrush(Color.FromArgb(0xFF, r, g, bch));
            }
            catch
            {
                return new SolidColorBrush(Color.FromArgb(0xFF, 0x07, 0xC1, 0x60));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}

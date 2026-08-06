using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace MoonWeChat.Controls
{
    /// <summary>
    /// 方形头像：有 ImageUrl 时加载远程图，失败回落文字头像。
    /// </summary>
    public sealed partial class AvatarView : UserControl
    {
        public AvatarView()
        {
            InitializeComponent();
            Loaded += (s, e) => Refresh();
        }

        public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(
            nameof(DisplayName), typeof(string), typeof(AvatarView),
            new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

        public string DisplayName
        {
            get => (string)GetValue(DisplayNameProperty);
            set => SetValue(DisplayNameProperty, value);
        }

        public static readonly DependencyProperty AccentColorHexProperty = DependencyProperty.Register(
            nameof(AccentColorHex), typeof(string), typeof(AvatarView),
            new PropertyMetadata("#07C160", OnVisualPropertyChanged));

        public string AccentColorHex
        {
            get => (string)GetValue(AccentColorHexProperty);
            set => SetValue(AccentColorHexProperty, value);
        }

        public static readonly DependencyProperty ImageUrlProperty = DependencyProperty.Register(
            nameof(ImageUrl), typeof(string), typeof(AvatarView),
            new PropertyMetadata(null, OnVisualPropertyChanged));

        /// <summary>http(s) 头像地址；空则只用文字头像。</summary>
        public string ImageUrl
        {
            get => (string)GetValue(ImageUrlProperty);
            set => SetValue(ImageUrlProperty, value);
        }

        public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
            nameof(Size), typeof(double), typeof(AvatarView),
            new PropertyMetadata(40d, OnVisualPropertyChanged));

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly DependencyProperty CornerRadiusValueProperty = DependencyProperty.Register(
            nameof(CornerRadiusValue), typeof(CornerRadius), typeof(AvatarView),
            new PropertyMetadata(new CornerRadius(0), OnVisualPropertyChanged));

        public CornerRadius CornerRadiusValue
        {
            get => (CornerRadius)GetValue(CornerRadiusValueProperty);
            set => SetValue(CornerRadiusValueProperty, value);
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as AvatarView;
            view?.Refresh();
        }

        private void Refresh()
        {
            if (RootBorder == null || InitialText == null || RootGrid == null)
            {
                return;
            }

            var size = Size > 0 ? Size : 40d;
            RootGrid.Width = size;
            RootGrid.Height = size;
            RootBorder.Width = size;
            RootBorder.Height = size;
            RootBorder.CornerRadius = CornerRadiusValue;
            RootBorder.Background = GetAccentBrush(AccentColorHex);
            InitialText.Text = GetInitial(DisplayName);
            InitialText.FontSize = Math.Max(12d, size * 0.4);

            if (AvatarImage != null)
            {
                AvatarImage.Width = size;
                AvatarImage.Height = size;
            }

            TryLoadRemoteImage();
        }

        private void TryLoadRemoteImage()
        {
            if (AvatarImage == null)
            {
                return;
            }

            var url = ImageUrl;
            // 兼容：有人把色值塞进 SenderAvatar 字段
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("#", StringComparison.Ordinal) ||
                (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                 !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                AvatarImage.Visibility = Visibility.Collapsed;
                AvatarImage.Source = null;
                return;
            }

            try
            {
                var bmp = new BitmapImage();
                bmp.ImageFailed += (s, e) =>
                {
                    AvatarImage.Visibility = Visibility.Collapsed;
                };
                bmp.ImageOpened += (s, e) =>
                {
                    AvatarImage.Visibility = Visibility.Visible;
                };
                bmp.UriSource = new Uri(url);
                AvatarImage.Source = bmp;
            }
            catch
            {
                AvatarImage.Visibility = Visibility.Collapsed;
                AvatarImage.Source = null;
            }
        }

        private static string GetInitial(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            string trimmed = displayName.Trim();
            char first = trimmed[0];
            if (first < 128 && char.IsLetter(first))
            {
                return char.ToUpperInvariant(first).ToString();
            }

            return trimmed[trimmed.Length - 1].ToString();
        }

        private static Brush GetAccentBrush(string hex)
        {
            try
            {
                string h = (hex ?? "#07C160").TrimStart('#');
                if (h.Length < 6)
                {
                    return new SolidColorBrush(Color.FromArgb(0xFF, 0x07, 0xC1, 0x60));
                }

                byte r = Convert.ToByte(h.Substring(0, 2), 16);
                byte g = Convert.ToByte(h.Substring(2, 2), 16);
                byte b = Convert.ToByte(h.Substring(4, 2), 16);
                return new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
            }
            catch
            {
                return new SolidColorBrush(Color.FromArgb(0xFF, 0x07, 0xC1, 0x60));
            }
        }
    }
}

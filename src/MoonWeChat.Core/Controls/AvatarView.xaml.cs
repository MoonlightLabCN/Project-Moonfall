using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace MoonWeChat.Controls
{
    /// <summary>
    /// 方形头像：有 ImageUrl 时加载远程图，失败回落文字头像。
    /// 刷色 / 解码尺寸做了缓存，减少列表滚动时的分配。
    /// </summary>
    public sealed partial class AvatarView : UserControl
    {
        private static readonly Dictionary<string, SolidColorBrush> BrushCache =
            new Dictionary<string, SolidColorBrush>(StringComparer.OrdinalIgnoreCase);

        private string _loadedImageUrl;
        private bool _imageHooked;

        public AvatarView()
        {
            InitializeComponent();
            Loaded += (s, e) => Refresh(forceImage: false);
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
            new PropertyMetadata(null, OnVisualPropertyChanged));

        public string AccentColorHex
        {
            get => (string)GetValue(AccentColorHexProperty);
            set => SetValue(AccentColorHexProperty, value);
        }

        public static readonly DependencyProperty ImageUrlProperty = DependencyProperty.Register(
            nameof(ImageUrl), typeof(string), typeof(AvatarView),
            new PropertyMetadata(null, OnImageUrlChanged));

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
            view?.Refresh(forceImage: false);
        }

        private static void OnImageUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as AvatarView;
            view?.Refresh(forceImage: true);
        }

        private void Refresh(bool forceImage)
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

            TryLoadRemoteImage(forceImage, size);
        }

        private void TryLoadRemoteImage(bool force, double size)
        {
            if (AvatarImage == null)
            {
                return;
            }

            var url = ImageUrl;
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("#", StringComparison.Ordinal) ||
                (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                 !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                _loadedImageUrl = null;
                AvatarImage.Visibility = Visibility.Collapsed;
                AvatarImage.Source = null;
                return;
            }

            if (!force && string.Equals(_loadedImageUrl, url, StringComparison.Ordinal))
            {
                return;
            }

            _loadedImageUrl = url;

            try
            {
                var bmp = new BitmapImage();
                // 按控件尺寸解码，列表里省内存、解码更快
                var px = (int)Math.Max(40, Math.Ceiling(size * 2));
                bmp.DecodePixelType = DecodePixelType.Logical;
                bmp.DecodePixelWidth = px;
                bmp.DecodePixelHeight = px;

                if (!_imageHooked)
                {
                    _imageHooked = true;
                    AvatarImage.ImageFailed += (s, e) =>
                    {
                        AvatarImage.Visibility = Visibility.Collapsed;
                    };
                    AvatarImage.ImageOpened += (s, e) =>
                    {
                        AvatarImage.Visibility = Visibility.Visible;
                    };
                }

                bmp.UriSource = new Uri(url);
                AvatarImage.Source = bmp;
            }
            catch
            {
                AvatarImage.Visibility = Visibility.Collapsed;
                AvatarImage.Source = null;
                _loadedImageUrl = null;
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
                if (string.IsNullOrWhiteSpace(hex))
                {
                    var themed = Application.Current == null ? null : Application.Current.Resources["AccentBrush"] as Brush;
                    return themed ?? new SolidColorBrush(Colors.Transparent);
                }

                string h = hex.TrimStart('#');
                if (h.Length < 6)
                {
                    h = "07C160";
                }

                SolidColorBrush cached;
                if (BrushCache.TryGetValue(h, out cached))
                {
                    return cached;
                }

                byte r = Convert.ToByte(h.Substring(0, 2), 16);
                byte g = Convert.ToByte(h.Substring(2, 2), 16);
                byte b = Convert.ToByte(h.Substring(4, 2), 16);
                cached = new SolidColorBrush(Color.FromArgb(0xFF, r, g, b));
                BrushCache[h] = cached;
                return cached;
            }
            catch
            {
                var themed = Application.Current == null ? null : Application.Current.Resources["AccentBrush"] as Brush;
                return themed ?? new SolidColorBrush(Colors.Transparent);
            }
        }
    }
}

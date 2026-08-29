using System;
using Windows.UI.Xaml;

namespace MoonWeChat.Services
{
    /// <summary>界面皮肤：Win10 浅色微信绿 / WP 经典黑底青强调。</summary>
    public enum AppVisualTheme
    {
        Win10 = 0,
        WpClassic = 1
    }

    /// <summary>
    /// 运行时切换颜色资源字典。页面上的 {ThemeResource …} 会跟着新字典走；
    /// 已打开页面可能要再导航一次才完全刷新（气泡转换器也会读最新资源）。
    /// </summary>
    public static class ThemeService
    {
        private const string Win10Path = "ms-appx:///Styles/Themes/Win10Theme.xaml";
        private const string WpClassicPath = "ms-appx:///Styles/Themes/WpClassicTheme.xaml";

        public static AppVisualTheme Current { get; private set; } = AppVisualTheme.Win10;

        public static event EventHandler ThemeChanged;

        /// <summary>
        /// 用「App.xaml 里实际合并进去的那本字典」校准 Current。
        /// 两个 Shell 静态合并的默认主题不同（UWP=Win10，WP81=WpClassic），
        /// 不校准的话 Current 恒为 Win10，设置页的「已经是这个主题就不动」判断会误判，
        /// WP8.1 上第一次点「Windows 10」会没反应。
        /// </summary>
        public static void SyncCurrentFromMergedDictionaries()
        {
            var app = Application.Current;
            if (app == null || app.Resources == null || app.Resources.MergedDictionaries == null)
            {
                return;
            }

            foreach (var dict in app.Resources.MergedDictionaries)
            {
                var src = dict.Source;
                var s = src == null ? string.Empty : (src.OriginalString ?? string.Empty);
                if (s.IndexOf("WpClassicTheme.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Current = AppVisualTheme.WpClassic;
                    return;
                }

                if (s.IndexOf("Win10Theme.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Current = AppVisualTheme.Win10;
                    return;
                }
            }
        }

        /// <summary>
        /// 启动后（首帧已经画出来、Window 已 Activate）调用一次，把用户存的配色应用上。
        /// 注意：绝不能在 App 构造函数里调 —— 那时候改 MergedDictionaries 会 0xc000027b 闪退。
        /// </summary>
        public static void ApplyFromSettings()
        {
            SyncCurrentFromMergedDictionaries();
            Apply(AppSettings.VisualTheme, persist: false);
        }

        public static void Apply(AppVisualTheme theme, bool persist = true)
        {
            if (persist)
            {
                AppSettings.VisualTheme = theme;
            }

            var app = Application.Current;
            if (app == null || app.Resources == null || app.Resources.MergedDictionaries == null)
            {
                Current = theme;
                return;
            }

            // 已是目标主题则不动字典，避免无意义的 Remove/Insert 触发 XAML 崩溃
            string want = theme == AppVisualTheme.WpClassic ? "WpClassicTheme.xaml" : "Win10Theme.xaml";
            var merged = app.Resources.MergedDictionaries;
            for (int i = 0; i < merged.Count; i++)
            {
                var src = merged[i].Source;
                if (src != null && (src.OriginalString ?? string.Empty).IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Current = theme;
                    return;
                }
            }

            try
            {
                for (int i = merged.Count - 1; i >= 0; i--)
                {
                    var src = merged[i].Source;
                    if (src == null)
                    {
                        continue;
                    }

                    var s = src.OriginalString ?? string.Empty;
                    if (s.IndexOf("Win10Theme.xaml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("WpClassicTheme.xaml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("ColorsAndBrushes.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        merged.RemoveAt(i);
                    }
                }

                var path = theme == AppVisualTheme.WpClassic ? WpClassicPath : Win10Path;
                merged.Insert(0, new ResourceDictionary { Source = new Uri(path) });
                Current = theme;
                ThemeChanged?.Invoke(null, EventArgs.Empty);
            }
            catch
            {
                // 换肤失败不崩应用；保留已有资源
                Current = theme;
            }
        }

        public static string DisplayName(AppVisualTheme theme)
        {
            return theme == AppVisualTheme.WpClassic ? "WP 经典" : "Windows 10";
        }
    }
}

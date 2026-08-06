using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace MoonWeChat.Controls
{
    /// <summary>底部四个 Tab 按钮的选中事件，宿主页面订阅后自己决定怎么导航。</summary>
    public sealed partial class BottomTabBar : UserControl
    {
        private bool _suppressEvent;

        public event EventHandler<int> TabSelected;

        public BottomTabBar()
        {
            InitializeComponent();
            ChatTab.IsChecked = true; // 默认选中“微信”这一栏；原因见 XAML 里 ChatTab 上的注释。
        }

        /// <summary>宿主页面在 OnNavigatedTo 里调用，把当前应该高亮的 Tab 设上去，不会触发 TabSelected。</summary>
        public void SetSelectedIndex(int index)
        {
            _suppressEvent = true;
            ChatTab.IsChecked = index == 0;
            ContactsTab.IsChecked = index == 1;
            DiscoverTab.IsChecked = index == 2;
            MeTab.IsChecked = index == 3;
            _suppressEvent = false;
        }

        private void OnTabChecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (!(sender is ToggleButton tapped))
            {
                return;
            }

            // 四个 ToggleButton 之间手动模拟“单选”，因为 ToggleButton 本身不像 RadioButton 那样自带分组。
            foreach (var btn in new[] { ChatTab, ContactsTab, DiscoverTab, MeTab })
            {
                if (btn != tapped)
                {
                    btn.IsChecked = false;
                }
            }

            if (_suppressEvent)
            {
                return;
            }

            int index = int.Parse((string)tapped.Tag);
            TabSelected?.Invoke(this, index);
        }
    }
}

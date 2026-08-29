using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

namespace MoonWeChat.Views
{
    public sealed partial class ShellPage : Page
    {
        private bool _wide;
        private ApplicationView _applicationView;
        private InputPane _inputPane;

        public ShellPage()
        {
            InitializeComponent();
            _applicationView = ApplicationView.GetForCurrentView();
            _inputPane = InputPane.GetForCurrentView();
            SizeChanged += OnSizeChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _applicationView.VisibleBoundsChanged += OnVisibleBoundsChanged;
            _inputPane.Showing += OnInputShowing;
            _inputPane.Hiding += OnInputHiding;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_applicationView != null) _applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            if (_inputPane != null)
            {
                _inputPane.Showing -= OnInputShowing;
                _inputPane.Hiding -= OnInputHiding;
            }
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            SizeChanged -= OnSizeChanged;
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args) { UpdateVisibleBounds(sender); }
        private void UpdateVisibleBounds(ApplicationView view)
        {
            if (view == null || Window.Current == null || RootGrid == null) return;
            var window = Window.Current.Bounds;
            var visible = view.VisibleBounds;
            RootGrid.Margin = new Thickness(Math.Max(0, visible.Left - window.Left), Math.Max(0, visible.Top - window.Top), Math.Max(0, window.Right - visible.Right), Math.Max(0, window.Bottom - visible.Bottom));
        }
        private void OnInputShowing(InputPane sender, InputPaneVisibilityEventArgs args) { BottomTabs.Visibility = Visibility.Collapsed; }
        private void OnInputHiding(InputPane sender, InputPaneVisibilityEventArgs args) { if (!_wide) BottomTabs.Visibility = Visibility.Visible; }
        private void OnLoaded(object sender, RoutedEventArgs e) { UpdateVisibleBounds(_applicationView); NavigateIfNeeded(typeof(ChatListPage)); }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var wide = e.NewSize.Width >= 800;
            if (wide == _wide) return;
            _wide = wide;
            VisualStateManager.GoToState(this, wide ? "Wide" : "Narrow", true);
            if (wide) { NarrowHost.Visibility = Visibility.Collapsed; WideSplit.Visibility = Visibility.Visible; if (ContentFrame.Content == null) NavigateIfNeeded(typeof(ChatListPage)); }
            else { WideSplit.Visibility = Visibility.Collapsed; NarrowHost.Visibility = Visibility.Visible; if (NarrowFrame.Content == null) NavigateIfNeeded(typeof(ChatListPage)); }
        }
        private Frame ActiveFrame { get { return _wide ? ContentFrame : NarrowFrame; } }
        private void NavigateIfNeeded(Type page, object parameter = null) { var frame = ActiveFrame; if (frame.Content == null || frame.CurrentSourcePageType != page) frame.Navigate(page, parameter); }
        private void OnNavClick(object sender, RoutedEventArgs e) { var b = sender as Button; if (b != null) NavigateIndex(Convert.ToInt32(b.Tag)); }
        private void OnTabSelected(object sender, int index) { NavigateIndex(index); }
        private void NavigateIndex(int index) { switch (index) { case 0: NavigateIfNeeded(typeof(ChatListPage)); break; case 1: NavigateIfNeeded(typeof(ContactsPage)); break; case 2: NavigateIfNeeded(typeof(MomentsPage)); break; case 3: NavigateIfNeeded(typeof(SettingsPage)); break; } }
        private void OnContentNavigated(object sender, NavigationEventArgs e) { if (BottomTabs != null) BottomTabs.SetSelectedIndex(e.SourcePageType == typeof(ContactsPage) ? 1 : e.SourcePageType == typeof(MomentsPage) ? 2 : e.SourcePageType == typeof(SettingsPage) ? 3 : 0); UpdateBackButton(); }
        private void UpdateBackButton() { var root = Window.Current.Content as Frame; SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = (ActiveFrame != null && ActiveFrame.CanGoBack) || (root != null && root.CanGoBack) ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed; }
        public void NavigateHome() { NavigateIfNeeded(typeof(ChatListPage)); }
        public bool TryGoBack() { var frame = ActiveFrame; if (frame != null && frame.CanGoBack) { frame.GoBack(); return true; } return false; }
    }
}

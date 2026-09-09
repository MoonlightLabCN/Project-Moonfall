using System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class ShellPage : Page
    {
        private const double WideBreakpoint = 800;
        private bool _wide;
        private double _keyboardHeight;
        private readonly ApplicationView _applicationView;
        private readonly InputPane _inputPane;

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
            if (_applicationView != null)
            {
                _applicationView.VisibleBoundsChanged -= OnVisibleBoundsChanged;
            }

            if (_inputPane != null)
            {
                _inputPane.Showing -= OnInputShowing;
                _inputPane.Hiding -= OnInputHiding;
            }

            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            SizeChanged -= OnSizeChanged;
        }

        private void OnVisibleBoundsChanged(ApplicationView sender, object args)
        {
            UpdateVisibleBounds(sender);
        }

        private void UpdateVisibleBounds(ApplicationView view)
        {
            if (view == null || Window.Current == null || RootGrid == null)
            {
                return;
            }

            var window = Window.Current.Bounds;
            var visible = view.VisibleBounds;
            RootGrid.Margin = new Thickness(
                Math.Max(0, visible.Left - window.Left),
                Math.Max(0, visible.Top - window.Top),
                Math.Max(0, window.Right - visible.Right),
                Math.Max(0, window.Bottom - visible.Bottom));
        }

        private void OnInputShowing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            _keyboardHeight = args.OccludedRect.Height;
            UpdateChrome();
        }

        private void OnInputHiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            _keyboardHeight = 0;
            UpdateChrome();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateVisibleBounds(_applicationView);
            ApplyLayout(ActualWidth);
            if (ContentFrame.Content == null)
            {
                NavigateTab(0);
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyLayout(e.NewSize.Width);
        }

        private void ApplyLayout(double width)
        {
            _wide = width >= WideBreakpoint;
            VisualStateManager.GoToState(this, _wide ? "Wide" : "Narrow", false);
            UpdateChrome();
        }

        private static bool IsOverlayPage(Type page)
        {
            return page == typeof(ChatPage)
                || page == typeof(LoginPage)
                || page == typeof(ConnectPage)
                || page == typeof(WelcomePage);
        }

        private void UpdateChrome()
        {
            var overlay = ContentFrame != null && IsOverlayPage(ContentFrame.CurrentSourcePageType);
            if (BottomTabs != null)
            {
                BottomTabs.Visibility = (!_wide && !overlay) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (RootSplit != null && _wide)
            {
                RootSplit.IsPaneOpen = true;
            }

            if (KeyboardSpacer != null)
            {
                KeyboardSpacer.Height = _keyboardHeight > 0 ? _keyboardHeight : 0;
            }
        }

        private void OnNavClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                NavigateTab(Convert.ToInt32(button.Tag));
            }
        }

        private void OnTabSelected(object sender, int index)
        {
            NavigateTab(index);
        }

        private void NavigateTab(int index)
        {
            Type page;
            switch (index)
            {
                case 1:
                    page = typeof(ContactsPage);
                    break;
                case 2:
                    page = typeof(MomentsPage);
                    break;
                case 3:
                    page = typeof(SettingsPage);
                    break;
                default:
                    page = typeof(ChatListPage);
                    break;
            }

            if (ContentFrame.CurrentSourcePageType != page)
            {
                ContentFrame.Navigate(page);
            }

            ContentFrame.BackStack.Clear();
            HighlightNav(index);
        }

        private void HighlightNav(int index)
        {
            SetNavWeight(NavChat, index == 0);
            SetNavWeight(NavContacts, index == 1);
            SetNavWeight(NavDiscover, index == 2);
            SetNavWeight(NavMe, index == 3);
        }

        private static void SetNavWeight(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.FontWeight = selected
                ? Windows.UI.Text.FontWeights.SemiBold
                : Windows.UI.Text.FontWeights.Normal;
        }

        private void OnContentNavigated(object sender, NavigationEventArgs e)
        {
            var type = e.SourcePageType;
            var index = type == typeof(ContactsPage) ? 1
                : type == typeof(MomentsPage) ? 2
                : type == typeof(SettingsPage) ? 3
                : 0;
            if (BottomTabs != null && !IsOverlayPage(type))
            {
                BottomTabs.SetSelectedIndex(index);
            }

            HighlightNav(index);
            UpdateChrome();
            UpdateBackButton();
        }

        private void UpdateBackButton()
        {
            var root = Window.Current.Content as Frame;
            var canGoBack = (ContentFrame != null && ContentFrame.CanGoBack)
                || (root != null && root.CanGoBack);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                canGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        public void NavigateHome()
        {
            NavigateTab(0);
        }

        public bool TryGoBack()
        {
            if (ContentFrame != null && ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
                return true;
            }

            return false;
        }
    }
}

using MoonWeChat.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MoonWeChat.Views
{
    public sealed partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            AppNavigation.EnterRemoteSetup(Frame);
        }

        private void OnSampleClick(object sender, RoutedEventArgs e)
        {
            AppNavigation.EnterSampleMode(Frame);
        }
    }
}

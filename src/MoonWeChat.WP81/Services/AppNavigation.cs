using System;
using MoonWeChat.Views;
using Windows.UI.Xaml.Controls;

namespace MoonWeChat.Services
{
    public static class AppNavigation
    {
        public static Type ResolveLaunchPage()
        {
            if (!AppSettings.HasCompletedOnboarding) return typeof(WelcomePage);
            if (AppSettings.UseSampleData || AppSettings.IsLoggedIn) return typeof(MainHubPage);
            return AppSettings.IsRemoteConfigured ? typeof(LoginPage) : typeof(ConnectPage);
        }

        public static void NavigateToMain(Frame frame)
        {
            NavigateRoot(frame, typeof(MainHubPage));
        }

        public static void EnterSampleMode(Frame frame)
        {
            AppSettings.UseSampleData = true;
            AppSettings.HasCompletedOnboarding = true;
            AppServices.Rebuild();
            NavigateToMain(frame);
        }

        public static void EnterRemoteSetup(Frame frame)
        {
            AppSettings.UseSampleData = false;
            AppSettings.HasCompletedOnboarding = true;
            AppServices.Rebuild();
            NavigateRoot(frame, AppSettings.IsLoggedIn ? typeof(MainHubPage) : typeof(ConnectPage));
        }

        private static void NavigateRoot(Frame frame, Type page)
        {
            if (frame == null) return;
            if (frame.CurrentSourcePageType != page) frame.Navigate(page);
            frame.BackStack.Clear();
        }
    }
}

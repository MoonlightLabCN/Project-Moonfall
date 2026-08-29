using System;
using MoonWeChat.Views;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace MoonWeChat.Services
{
    /// <summary>
    /// UWP / Win10 Mobile Shell 导航。
    /// </summary>
    public static class AppNavigation
    {
        public static Type ResolveLaunchPage()
        {
            if (!AppSettings.HasCompletedOnboarding)
            {
                return typeof(WelcomePage);
            }

            if (AppSettings.UseSampleData || AppSettings.IsLoggedIn)
            {
                return typeof(ShellPage);
            }

            return typeof(LoginPage);
        }

        public static void NavigateToMain(Frame frame)
        {
            var root = Window.Current.Content as Frame;
            var shell = root == null ? null : root.Content as ShellPage;
            if (shell != null)
            {
                shell.NavigateHome();
                return;
            }

            (root ?? frame)?.Navigate(typeof(ShellPage));
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
            if (AppSettings.IsLoggedIn)
            {
                NavigateToMain(frame);
            }
            else
            {
                frame?.Navigate(typeof(LoginPage));
            }
        }
    }
}

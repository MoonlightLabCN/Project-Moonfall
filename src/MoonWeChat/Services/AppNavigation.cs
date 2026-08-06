using System;
using MoonWeChat.Views;
using Windows.UI.Xaml.Controls;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 启动路由：
    /// - 已记住登录（BaseUrl+Token+WxId）→ 直接会话列表（服务器长期在线则免扫）
    /// - 仅有服务器/Token 未扫码 → 登录页
    /// - 首次 → 欢迎页
    /// </summary>
    public static class AppNavigation
    {
        public static Type ResolveLaunchPage()
        {
            if (!AppSettings.HasCompletedOnboarding)
            {
                return typeof(WelcomePage);
            }

            if (AppSettings.UseSampleData)
            {
                return typeof(ChatListPage);
            }

            // 核心：本机已登录 → 直接进主页，不再弹登录
            if (AppSettings.IsLoggedIn)
            {
                return typeof(ChatListPage);
            }

            // 配了服务器但还没扫过码
            return typeof(LoginPage);
        }

        public static void NavigateToMain(Frame frame)
        {
            frame?.Navigate(typeof(ChatListPage));
        }

        public static void EnterSampleMode(Frame frame)
        {
            AppSettings.UseSampleData = true;
            AppSettings.HasCompletedOnboarding = true;
            AppServices.Rebuild();
            NavigateToMain(frame);
        }

        /// <summary>欢迎页「连接并登录」→ 一体化登录（地址+Token+扫码）。</summary>
        public static void EnterRemoteSetup(Frame frame)
        {
            AppSettings.UseSampleData = false;
            AppSettings.HasCompletedOnboarding = true;
            AppServices.Rebuild();
            // 若已登录过，直接主页
            if (AppSettings.IsLoggedIn)
            {
                frame?.Navigate(typeof(ChatListPage));
            }
            else
            {
                frame?.Navigate(typeof(LoginPage));
            }
        }
    }
}

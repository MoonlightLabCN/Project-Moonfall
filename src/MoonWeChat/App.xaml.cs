using System;
using MoonWeChat.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat
{
    /// <summary>
    /// 启动：若本机已记住登录，直接进会话；后台探测服务器是否仍在线。
    /// 服务器长期在线 + 本机有 Token/WxId → 用户不用再管登录。
    /// </summary>
    sealed partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += OnResuming;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                AppServices.Initialize(Window.Current.Dispatcher);
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(AppNavigation.ResolveLaunchPage(), e.Arguments);
                }

                Window.Current.Activate();

                // 已登录：后台恢复会话（探测在线、心跳、拉消息），不挡 UI
                if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
                {
                    _ = ResumeSessionInBackgroundAsync();
                }
            }
        }

        private static async System.Threading.Tasks.Task ResumeSessionInBackgroundAsync()
        {
            try
            {
                await SessionBootstrap.EnsureSessionAsync(startPollingIfOnline: true)
                    .ConfigureAwait(true);
            }
            catch
            {
                // 启动探测失败不崩；会话列表会显示横幅
            }
        }

        private void OnResuming(object sender, object e)
        {
            if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
            {
                _ = ResumeSessionInBackgroundAsync();
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("加载页面失败: " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            // 不清除登录态。微信会话由服务器保活；本机只存 Token/WxId。
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}

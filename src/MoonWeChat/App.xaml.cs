using System;
using System.Diagnostics;
using MoonWeChat.Services;
using MoonWeChat.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace MoonWeChat
{
    /// <summary>UWP / Win10 Mobile 入口。启动路径尽量不做会炸 XAML 的资源热切换。</summary>
    sealed partial class App : Application
    {
        public App()
        {
            // 先挂崩溃日志，再 InitializeComponent
            UnhandledException += OnUnhandledException;

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("InitializeComponent failed: " + ex);
                throw;
            }

            // 注意：不要在这里 ThemeService.ApplyFromSettings()。
            // 启动瞬间改 Application.Resources.MergedDictionaries 会在 Windows.UI.Xaml 里 0xc000027b 闪退。
            // 配色以 App.xaml 里静态合并的 Win10Theme 为准；设置页再换主题。

            Suspending += OnSuspending;
            Resuming += OnResuming;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                Frame rootFrame = Window.Current.Content as Frame;

                if (rootFrame == null)
                {
                    rootFrame = new Frame();
                    rootFrame.NavigationFailed += OnNavigationFailed;
                    Window.Current.Content = rootFrame;

                    SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
                    rootFrame.Navigated += OnRootNavigated;

                    try
                    {
                        AppServices.Initialize(Window.Current.Dispatcher);
                    }
                    catch (Exception ex)
                    {
                        LogCrash("AppServices.Initialize", ex);
                    }
                }

                if (e.PrelaunchActivated == false)
                {
                    if (rootFrame.Content == null)
                    {
                        Type page;
                        try
                        {
                            page = AppNavigation.ResolveLaunchPage();
                        }
                        catch (Exception ex)
                        {
                            LogCrash("ResolveLaunchPage", ex);
                            page = typeof(WelcomePage);
                        }

                        try
                        {
                            rootFrame.Navigate(page, e.Arguments);
                        }
                        catch (Exception ex)
                        {
                            LogCrash("Navigate " + (page != null ? page.FullName : "?"), ex);
                            // 最后兜底：欢迎页
                            rootFrame.Navigate(typeof(WelcomePage), e.Arguments);
                        }
                    }

                    Window.Current.Activate();

                    // 首帧已经画出来，现在换配色字典是安全的。
                    // 放在构造函数里会 0xc000027b 闪退（见上面的注释）；
                    // 但完全不调用的话，用户在设置页选的配色每次重启都会丢。
                    try
                    {
                        ThemeService.ApplyFromSettings();
                    }
                    catch (Exception ex)
                    {
                        LogCrash("ApplyTheme", ex);
                    }

                    // 轮询放到 Activate 之后，避免启动时 DispatcherTimer / 网络拖垮首帧
                    if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
                    {
                        var ignored = ResumeSessionInBackgroundAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogCrash("OnLaunched", ex);
                throw;
            }
        }

        private void OnRootNavigated(object sender, NavigationEventArgs e)
        {
            var frame = sender as Frame;
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                frame != null && frame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        private void OnBackRequested(object sender, BackRequestedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            var shell = frame == null ? null : frame.Content as ShellPage;
            if (shell != null && shell.TryGoBack())
            {
                e.Handled = true;
            }
            else if (frame != null && frame.CanGoBack)
            {
                e.Handled = true;
                frame.GoBack();
            }
        }

        private static async System.Threading.Tasks.Task ResumeSessionInBackgroundAsync()
        {
            try
            {
                await SessionBootstrap.EnsureSessionAsync(startPollingIfOnline: true)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogCrash("ResumeSession", ex);
            }
        }

        private void OnResuming(object sender, object e)
        {
            if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
            {
                var ignored = ResumeSessionInBackgroundAsync();
            }
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            LogCrash("NavigationFailed " + e.SourcePageType, e.Exception);
            // 不要 throw，否则整应用崩；尽量回欢迎页
            var frame = sender as Frame;
            if (frame != null)
            {
                frame.Navigate(typeof(WelcomePage));
            }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogCrash("UnhandledException", e.Exception);
#if DEBUG
            // 调试构建里让它真的崩：无条件吞异常会掩盖启动/XAML 解析失败，
            // 而那恰好是这个项目一直在追的一类问题。
            e.Handled = false;
#else
            // 发布构建：尽量不死进程（仍可能被系统杀），崩溃细节已写进 crash.log。
            e.Handled = true;
#endif
        }

        private static void LogCrash(string where, Exception ex)
        {
            try
            {
                Debug.WriteLine("[CRASH] " + where + ": " + ex);
                var path = System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.log");
                System.IO.File.AppendAllText(path, DateTimeOffset.Now.ToString("o") + " [" + where + "] " + ex + Environment.NewLine);
            }
            catch
            {
                // ignore logging failures
            }
        }
    }
}

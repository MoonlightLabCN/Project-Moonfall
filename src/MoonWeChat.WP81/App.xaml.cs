using System;
using System.Diagnostics;
using System.IO;
using MoonWeChat.Services;
using MoonWeChat.Views;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Phone.UI.Input;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat
{
    public sealed partial class App : Application
    {
        private TransitionCollection _transitions;

        public App()
        {
            UnhandledException += OnUnhandledException;
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += OnResuming;
            HardwareButtons.BackPressed += OnHardwareBackPressed;
            ThemeService.ThemeChanged += OnThemeChanged;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                var rootFrame = EnsureRootFrame();
                if (rootFrame.Content == null)
                {
                    SuspendTransitionsUntilFirstNavigation(rootFrame);
                    var page = AppNavigation.ResolveLaunchPage();
                    if (!rootFrame.Navigate(page, e.Arguments))
                    {
                        throw new InvalidOperationException("Cannot navigate to " + page.Name);
                    }
                }

                Window.Current.Activate();
                ThemeService.ApplyFromSettings();
                ApplySystemChrome();

                if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
                {
                    var ignored = ResumeSessionAsync();
                }
            }
            catch (Exception ex)
            {
                LogCrash("OnLaunched", ex);
                throw;
            }
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            EnsureRootFrame();
            Window.Current.Activate();
            ApplySystemChrome();

            var fileArgs = args as FileOpenPickerContinuationEventArgs;
            if (fileArgs != null)
            {
                FilePickerContinuation.Continue(fileArgs);
            }
        }

        private Frame EnsureRootFrame()
        {
            var rootFrame = Window.Current.Content as Frame;
            if (rootFrame != null)
            {
                return rootFrame;
            }

            rootFrame = new Frame { CacheSize = 2 };
            try
            {
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Frame language: " + ex.Message);
            }

            rootFrame.NavigationFailed += OnNavigationFailed;
            AppServices.Initialize(Window.Current.Dispatcher);
            Window.Current.Content = rootFrame;
            return rootFrame;
        }

        private void SuspendTransitionsUntilFirstNavigation(Frame frame)
        {
            if (frame.ContentTransitions != null)
            {
                _transitions = new TransitionCollection();
                foreach (var transition in frame.ContentTransitions)
                {
                    _transitions.Add(transition);
                }
            }

            frame.ContentTransitions = null;
            frame.Navigated += OnFirstNavigated;
        }

        private void OnFirstNavigated(object sender, NavigationEventArgs e)
        {
            var frame = sender as Frame;
            if (frame == null) return;
            frame.ContentTransitions = _transitions ?? new TransitionCollection { new NavigationThemeTransition() };
            frame.Navigated -= OnFirstNavigated;
        }

        private static async System.Threading.Tasks.Task ResumeSessionAsync()
        {
            try
            {
                await SessionBootstrap.EnsureSessionAsync(true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogCrash("ResumeSession", ex);
            }
        }

        private void OnResuming(object sender, object e)
        {
            ApplySystemChrome();
            if (!AppSettings.UseSampleData && AppSettings.IsLoggedIn)
            {
                var ignored = ResumeSessionAsync();
            }
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            ApplySystemChrome();
        }

        private static void ApplySystemChrome()
        {
            try
            {
                var status = StatusBar.GetForCurrentView();
                var chrome = Application.Current.Resources["ChromeBackgroundBrush"] as SolidColorBrush;
                var foreground = Application.Current.Resources["PrimaryTextBrush"] as SolidColorBrush;
                status.BackgroundColor = chrome == null ? Colors.Black : chrome.Color;
                status.BackgroundOpacity = 1;
                status.ForegroundColor = foreground == null ? Colors.White : foreground.Color;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("StatusBar: " + ex.Message);
            }
        }

        private void OnHardwareBackPressed(object sender, BackPressedEventArgs e)
        {
            var frame = Window.Current.Content as Frame;
            if (frame == null || !frame.CanGoBack) return;
            e.Handled = true;
            frame.GoBack();
        }

        private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            LogCrash("NavigationFailed " + e.SourcePageType, e.Exception);
            var frame = sender as Frame;
            if (frame != null && frame.CurrentSourcePageType != typeof(WelcomePage))
            {
                frame.Navigate(typeof(WelcomePage));
            }
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            e.SuspendingOperation.GetDeferral().Complete();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogCrash("UnhandledException", e.Exception);
#if DEBUG
            e.Handled = false;
#else
            e.Handled = true;
#endif
        }

        private static void LogCrash(string where, Exception ex)
        {
            try
            {
                var line = DateTimeOffset.Now.ToString("o") + " [" + where + "] " + ex + Environment.NewLine;
                Debug.WriteLine(line);
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "crash.log");
                StorageFile file;
                try
                {
                    file = StorageFile.GetFileFromPathAsync(path).AsTask().GetAwaiter().GetResult();
                }
                catch
                {
                    file = ApplicationData.Current.LocalFolder
                        .CreateFileAsync("crash.log", CreationCollisionOption.OpenIfExists)
                        .AsTask().GetAwaiter().GetResult();
                }
                FileIO.AppendTextAsync(file, line).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception logError)
            {
                Debug.WriteLine("Crash log failed: " + logError.Message);
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using MoonWeChat.Models;
using MoonWeChat.Services;
using MoonWeChat.ViewModels;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using Windows.UI.Popups;

namespace MoonWeChat.Views
{
    public sealed partial class ChatPage : Page
    {
        public ChatViewModel ViewModel { get; } = new ChatViewModel();

        private bool _isVoiceInputMode;

        private string _boundSessionId;
        private bool _handlersHooked;
        private InputPane _inputPane;

        public ChatPage()
        {
            InitializeComponent();
            DataContext = this;
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (!_handlersHooked)
            {
                ViewModel.MessageAppended += OnMessageAppended;
                ViewModel.PropertyChanged += OnVmPropertyChanged;
                _handlersHooked = true;
            }

            FilePickerContinuation.SetHandler(OnPickerContinued);
            _inputPane = InputPane.GetForCurrentView();
            _inputPane.Showing += OnInputPaneShowing;
            _inputPane.Hiding += OnInputPaneHiding;

            var sessionId = e.Parameter as string;
            // 同会话二次进入：不重复 Load，只滚到底（缓存页）。
            // 但「数据源被换掉」时必须重新 Load —— 否则会攥着示例数据源的会话对象，
            // 把示例 id 当成 ToWxid 发到真实网关。
            if (sessionId != null &&
                (!string.Equals(sessionId, _boundSessionId, StringComparison.Ordinal) || ViewModel.IsStale))
            {
                _boundSessionId = sessionId;
                ViewModel.Load(sessionId);
                HeaderTitleText.Text = ViewModel.HeaderTitle;
                UpdateMuteMenuText();
                UpdateQuoteBar();
            }
            else if (ViewModel.IsStale && _boundSessionId != null)
            {
                ViewModel.Load(_boundSessionId);
            }
            else
            {
                HeaderTitleText.Text = ViewModel.HeaderTitle;
                UpdateMuteMenuText();
                UpdateQuoteBar();
            }

            // 等布局一轮后再滚，避免同步 ScrollIntoView 卡首帧
            ScheduleScrollToBottom();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            FilePickerContinuation.ClearHandler();
            if (_inputPane != null)
            {
                _inputPane.Showing -= OnInputPaneShowing;
                _inputPane.Hiding -= OnInputPaneHiding;
                _inputPane = null;
            }
            // 保持 cache 时不卸载事件；彻底离开导航栈时由页面回收
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatViewModel.HeaderTitle))
            {
                HeaderTitleText.Text = ViewModel.HeaderTitle;
            }

            if (e.PropertyName == nameof(ChatViewModel.HasQuoteTarget) ||
                e.PropertyName == nameof(ChatViewModel.QuotePreview))
            {
                UpdateQuoteBar();
            }
        }

        private void UpdateQuoteBar()
        {
            if (ViewModel.HasQuoteTarget)
            {
                QuoteBar.Visibility = Visibility.Visible;
                QuotePreviewText.Text = ViewModel.QuotePreview;
            }
            else
            {
                QuoteBar.Visibility = Visibility.Collapsed;
            }
        }

        private void OnMessageAppended(object sender, ChatMessage message)
        {
            HeaderTitleText.Text = ViewModel.HeaderTitle;
            ScrollToBottom();
        }

        private void ScheduleScrollToBottom()
        {
            // WP8.1 不接受 CoreDispatcherPriority.Idle；Normal 足够让当前导航回调先返回。
            var ignored = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, ScrollToBottomCore);
        }

        private void ScrollToBottom()
        {
            ScheduleScrollToBottom();
        }

        private void ScrollToBottomCore()
        {
            if (ViewModel.Messages == null || ViewModel.Messages.Count == 0 || MessageListView == null)
            {
                return;
            }

            try
            {
                MessageListView.ScrollIntoView(ViewModel.Messages[ViewModel.Messages.Count - 1]);
            }
            catch
            {
                // 列表尚未就绪时忽略
            }
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            // 必须 await：fire-and-forget 会吞掉发送失败，按钮看起来“已经发出去了”。
            try { await ViewModel.SendAsync().ConfigureAwait(true); } catch (Exception ex) { await new MessageDialog("发送失败：" + ex.Message).ShowAsync(); }
        }

        private void OnInputPaneShowing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            args.EnsuredFocusedElementInView = true;
            InputBottomPadding.Height = args.OccludedRect.Height;
        }

        private void OnInputPaneHiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            InputBottomPadding.Height = 0;
        }

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.DraftText = InputTextBox.Text;
        }

        private void OnMoreClick(object sender, RoutedEventArgs e)
        {
            MorePanel.Visibility = MorePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnEmojiPicked(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var emoji = button != null ? button.Content as string : null;
            if (emoji != null)
            {
                ViewModel.DraftText += emoji;
            }

            EmojiFlyout.Hide();
        }

        private void OnMorePanelItemClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var tag = button != null ? button.Tag as string : null;
            if (tag == null)
            {
                return;
            }

            MorePanel.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "album":
                case "camera":
                    // WP8.1：必须 AndContinue，不能 await PickSingleFileAsync
                    FilePickerContinuation.PickImage();
                    break;
                case "video":
                    break;
                case "location":
                    ViewModel.SendPlaceholder(MessageType.Location, "位置");
                    break;
                case "card":
                    ViewModel.SendPlaceholder(MessageType.ContactCard, "名片");
                    break;
                case "file":
                    FilePickerContinuation.PickFile();
                    break;
            }
        }

        private async void OnPickerContinued(StorageFile file, string operation)
        {
            try
            {
                if (file == null)
                {
                    await new MessageDialog("已取消选择文件。", "附件").ShowAsync();
                    return;
                }

                if (operation == FilePickerContinuation.OperationImage)
                {
                    var base64 = await ReadFileBase64Async(file, 8 * 1024 * 1024).ConfigureAwait(true);
                    if (base64 == null) { await new MessageDialog("图片为空或超过 8 MB。", "附件").ShowAsync(); return; }
                    await ViewModel.SendImageBase64Async(base64, file.Name).ConfigureAwait(true);
                }
                else if (operation == FilePickerContinuation.OperationFile)
                {
                    var base64 = await ReadFileBase64Async(file, 12 * 1024 * 1024).ConfigureAwait(true);
                    if (base64 == null) { await new MessageDialog("文件为空或超过 12 MB。", "附件").ShowAsync(); return; }
                    await ViewModel.SendFileBase64Async(base64, file.Name).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                await new MessageDialog("附件发送失败：" + ex.Message).ShowAsync();
            }
        }

        private static async Task<string> ReadFileBase64Async(StorageFile file, uint maxBytes)
        {
            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {
                var size = (uint)stream.Size;
                if (size == 0 || size > maxBytes)
                {
                    return null;
                }

                var reader = new DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync(size);
                var bytes = new byte[size];
                reader.ReadBytes(bytes);
                return Convert.ToBase64String(bytes);
            }
        }

        private async void OnMessageClick(object sender, ItemClickEventArgs e)
        {
            try
            {
                var message = e.ClickedItem as ChatMessage;
                if (message == null)
                {
                    return;
                }

                if (message.IsFailed)
                {
                    await ViewModel.RetryAsync(message).ConfigureAwait(true);
                    return;
                }

                // 点一条消息设为引用目标
                if (message.Type != MessageType.DateDivider &&
                    message.Type != MessageType.SystemNotice &&
                    message.Type != MessageType.Recall &&
                    message.Type != MessageType.Pat)
                {
                    ViewModel.SetQuote(message);
                }
            }
            catch (Exception ex)
            {
                await new MessageDialog("消息操作失败：" + ex.Message).ShowAsync();
            }
        }

        private void OnClearQuoteClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearQuote();
        }

        private async void OnRevokeClick(object sender, RoutedEventArgs e)
        {
            try { await ViewModel.RevokeLastAsync().ConfigureAwait(true); } catch (Exception ex) { await new MessageDialog("撤回失败：" + ex.Message).ShowAsync(); }
        }

        private async void OnPatClick(object sender, RoutedEventArgs e)
        {
            try { await ViewModel.PatAsync().ConfigureAwait(true); } catch (Exception ex) { await new MessageDialog("操作失败：" + ex.Message).ShowAsync(); }
        }

        private void OnVoiceModeToggleClick(object sender, RoutedEventArgs e)
        {
            _isVoiceInputMode = !_isVoiceInputMode;

            if (_isVoiceInputMode)
            {
                InputTextBox.Visibility = Visibility.Collapsed;
                VoiceHoldButton.Visibility = Visibility.Visible;
                VoiceModeIcon.Symbol = Windows.UI.Xaml.Controls.Symbol.Edit;
            }
            else
            {
                InputTextBox.Visibility = Visibility.Visible;
                VoiceHoldButton.Visibility = Visibility.Collapsed;
                VoiceModeIcon.Symbol = Windows.UI.Xaml.Controls.Symbol.Microphone;
            }

            MorePanel.Visibility = Visibility.Collapsed;
        }

        private void OnVoiceHoldPressed(object sender, PointerRoutedEventArgs e)
        {
            VoiceHoldText.Text = "松开 发送";
        }

        private void OnVoiceHoldReleased(object sender, PointerRoutedEventArgs e)
        {
            if (VoiceHoldText.Text == "松开 发送")
            {
                ViewModel.SendPlaceholder(MessageType.Voice, "语音");
            }

            VoiceHoldText.Text = "按住 说话";
        }

        private void OnToggleMuteClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Session == null)
            {
                return;
            }

            ViewModel.Session.IsMuted = !ViewModel.Session.IsMuted;
            UpdateMuteMenuText();
        }

        private void UpdateMuteMenuText()
        {
            if (ViewModel.Session != null)
            {
                MuteMenuItem.Text = ViewModel.Session.IsMuted ? "取消消息免打扰" : "设置消息免打扰";
            }
        }
    }
}

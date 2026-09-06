using System;
using System.Threading.Tasks;
using MoonWeChat.Models;
using MoonWeChat.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class ChatPage : Page
    {
        public ChatViewModel ViewModel { get; } = new ChatViewModel();

        private bool _isVoiceInputMode;

        private string _boundSessionId;
        private bool _handlersHooked;

        public ChatPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Disabled;
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

            // 页面可能由导航框架复用。除了「换了会话」要重新 Load，
            // 「数据源被换掉」（示例↔真实、换后端）也必须重新 Load ——
            // 否则会攥着旧数据源的会话对象，把旧 id 当成 ToWxid 发到真实网关。
            var sessionId = e.Parameter as string;
            if (sessionId != null &&
                (!string.Equals(sessionId, _boundSessionId, StringComparison.Ordinal) || ViewModel.IsStale))
            {
                _boundSessionId = sessionId;
                ViewModel.Load(sessionId);
            }
            else if (ViewModel.IsStale && _boundSessionId != null)
            {
                ViewModel.Load(_boundSessionId);
            }

            HeaderTitleText.Text = ViewModel.HeaderTitle;
            UpdateMuteMenuText();
            UpdateQuoteBar();
            ScrollToBottom();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
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

        private void ScrollToBottom()
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Idle, () =>
            {
                if (ViewModel.Messages != null && ViewModel.Messages.Count > 0 && MessageListView != null)
                {
                    try
                    {
                        MessageListView.ScrollIntoView(ViewModel.Messages[ViewModel.Messages.Count - 1]);
                    }
                    catch
                    {
                    }
                }
            });
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
            try { await ViewModel.SendAsync().ConfigureAwait(true); } catch (Exception ex) { await ShowErrorAsync("发送失败：" + ex.Message); }
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
            if (sender is Button button && button.Content is string emoji)
            {
                ViewModel.DraftText += emoji;
                InputTextBox.Text = ViewModel.DraftText;
            }
        }

        private async void OnMorePanelItemClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button; var tag = button == null ? null : button.Tag as string; if (tag == null) return; MorePanel.Visibility = Visibility.Collapsed;
                switch (tag) { case "album": case "camera": await PickAndSendImageAsync().ConfigureAwait(true); break; case "video": await ViewModel.SendPlaceholderAsync(MessageType.Video, "视频").ConfigureAwait(true); break; case "location": await ViewModel.SendPlaceholderAsync(MessageType.Location, "位置").ConfigureAwait(true); break; case "card": await ViewModel.SendPlaceholderAsync(MessageType.ContactCard, "名片").ConfigureAwait(true); break; case "file": await PickAndSendFileAsync().ConfigureAwait(true); break; }
            }
            catch (Exception ex) { await ShowErrorAsync("操作失败：" + ex.Message); }
        }

        private async Task PickAndSendImageAsync()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".gif");
                picker.FileTypeFilter.Add(".bmp");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file == null)
                {
                    return;
                }

                var base64 = await ReadFileBase64Async(file, 8 * 1024 * 1024, "图片").ConfigureAwait(true);

                await ViewModel.SendImageBase64Async(base64, file.Name).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("图片发送失败：" + ex.Message);
            }
        }

        private async Task PickAndSendFileAsync()
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.ViewMode = PickerViewMode.List;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file == null)
                {
                    return;
                }

                var base64 = await ReadFileBase64Async(file, 12 * 1024 * 1024, "文件").ConfigureAwait(true);

                await ViewModel.SendFileBase64Async(base64, file.Name).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("文件发送失败：" + ex.Message);
            }
        }

        private static async Task<string> ReadFileBase64Async(StorageFile file, uint maxBytes, string kind)
        {
            var properties = await file.GetBasicPropertiesAsync();
            if (properties.Size == 0) throw new InvalidOperationException(kind + "为空，无法发送。");
            if (properties.Size > maxBytes) throw new InvalidOperationException(kind + "超过 " + (maxBytes / 1024 / 1024) + " MB 上限，未读取文件内容。");
            using (IRandomAccessStream stream = await file.OpenReadAsync())
            {
                var size = (uint)stream.Size;
                using (var reader = new DataReader(stream.GetInputStreamAt(0)))
                {
                    await reader.LoadAsync(size);
                    var bytes = new byte[size];
                    reader.ReadBytes(bytes);
                    return Convert.ToBase64String(bytes);
                }
            }
        }

        private async void OnMessageClick(object sender, ItemClickEventArgs e)
        {
            if (!(e.ClickedItem is ChatMessage message))
            {
                return;
            }

            if (message.IsFailed)
            {
                try { await ViewModel.RetryAsync(message).ConfigureAwait(true); } catch (Exception ex) { await ShowErrorAsync("重发失败：" + ex.Message); }
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

        private void OnClearQuoteClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearQuote();
        }

        private async void OnRevokeClick(object sender, RoutedEventArgs e)
        {
            try { await ViewModel.RevokeLastAsync().ConfigureAwait(true); } catch (Exception ex) { await ShowErrorAsync(ex.Message); }
        }

        private async void OnPatClick(object sender, RoutedEventArgs e)
        {
            try { await ViewModel.PatAsync().ConfigureAwait(true); } catch (Exception ex) { await ShowErrorAsync(ex.Message); }
        }

        private void OnClearLocalClick(object sender, RoutedEventArgs e)
        {
            _ = ShowErrorAsync("为避免误删服务器同步记录，此版本不提供清空本机消息。");
        }

        private void OnVoiceModeToggleClick(object sender, RoutedEventArgs e)
        {
            _isVoiceInputMode = !_isVoiceInputMode;

            if (_isVoiceInputMode)
            {
                InputTextBox.Visibility = Visibility.Collapsed;
                VoiceHoldButton.Visibility = Visibility.Visible;
                VoiceModeIcon.Glyph = "\uE765";
            }
            else
            {
                InputTextBox.Visibility = Visibility.Visible;
                VoiceHoldButton.Visibility = Visibility.Collapsed;
                VoiceModeIcon.Glyph = "\uE720";
            }

            MorePanel.Visibility = Visibility.Collapsed;
        }

        private void OnVoiceHoldPressed(object sender, PointerRoutedEventArgs e)
        {
            VoiceHoldText.Text = "松开 发送";
        }

        private async void OnVoiceHoldReleased(object sender, PointerRoutedEventArgs e)
        {
            try { if (VoiceHoldText.Text == "松开 发送") await ViewModel.SendPlaceholderAsync(MessageType.Voice, "语音"); }
            catch (Exception ex) { await ShowErrorAsync("语音发送失败：" + ex.Message); }
            finally { VoiceHoldText.Text = "按住 说话"; }
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

        private async System.Threading.Tasks.Task ShowErrorAsync(string text)
        {
            try { await new ContentDialog { Title = "提示", Content = text, CloseButtonText = "确定" }.ShowAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("ContentDialog failed: " + ex); }
        }
    }
}

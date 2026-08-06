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

        public ChatPage()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            ViewModel.MessageAppended += OnMessageAppended;
            ViewModel.PropertyChanged += OnVmPropertyChanged;

            if (e.Parameter is string sessionId)
            {
                ViewModel.Load(sessionId);
            }

            HeaderTitleText.Text = ViewModel.HeaderTitle;
            UpdateMuteMenuText();
            UpdateQuoteBar();
            ScrollToBottom();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.MessageAppended -= OnMessageAppended;
            ViewModel.PropertyChanged -= OnVmPropertyChanged;
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
            if (ViewModel.Messages == null || ViewModel.Messages.Count == 0)
            {
                return;
            }

            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                if (ViewModel.Messages != null && ViewModel.Messages.Count > 0)
                {
                    MessageListView.ScrollIntoView(ViewModel.Messages[ViewModel.Messages.Count - 1]);
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

        private void OnSendClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Send();
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
            }

            EmojiFlyout.Hide();
        }

        private async void OnMorePanelItemClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string tag))
            {
                return;
            }

            MorePanel.Visibility = Visibility.Collapsed;

            switch (tag)
            {
                case "album":
                case "camera":
                    await PickAndSendImageAsync().ConfigureAwait(true);
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
                    await PickAndSendFileAsync().ConfigureAwait(true);
                    break;
            }
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

                var base64 = await ReadFileBase64Async(file, 8 * 1024 * 1024).ConfigureAwait(true);
                if (base64 == null)
                {
                    return;
                }

                await ViewModel.SendImageBase64Async(base64, file.Name).ConfigureAwait(true);
            }
            catch
            {
                // ignore
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

                var base64 = await ReadFileBase64Async(file, 12 * 1024 * 1024).ConfigureAwait(true);
                if (base64 == null)
                {
                    return;
                }

                await ViewModel.SendFileBase64Async(base64, file.Name).ConfigureAwait(true);
            }
            catch
            {
                // ignore
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
            if (!(e.ClickedItem is ChatMessage message))
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

        private void OnClearQuoteClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearQuote();
        }

        private async void OnRevokeClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.RevokeLastAsync().ConfigureAwait(true);
        }

        private async void OnPatClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.PatAsync().ConfigureAwait(true);
        }

        private void OnClearLocalClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Messages != null)
            {
                ViewModel.Messages.Clear();
            }
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

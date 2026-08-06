using System;
using MoonWeChat.Models;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MoonWeChat.Views
{
    public sealed partial class MomentsPage : Page
    {
        public MomentsViewModel ViewModel { get; } = new MomentsViewModel();

        public MomentsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            TabBar.SetSelectedIndex(2);
            await ViewModel.LoadAsync();
        }

        private void OnTabSelected(object sender, int index)
        {
            switch (index)
            {
                case 0:
                    Frame.Navigate(typeof(ChatListPage));
                    break;
                case 1:
                    Frame.Navigate(typeof(ContactsPage));
                    break;
                case 3:
                    Frame.Navigate(typeof(SettingsPage));
                    break;
                default:
                    break;
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnPublishClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Draft = DraftBox.Text;
            await ViewModel.PublishAsync();
            DraftBox.Text = ViewModel.Draft ?? string.Empty;
        }

        private void OnDraftChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Draft = DraftBox.Text;
        }

        private async void OnLikeClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MomentPost post)
            {
                await ViewModel.LikeAsync(post);
            }
        }

        private async void OnCommentClick(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is MomentPost post))
            {
                return;
            }

            var box = new TextBox { PlaceholderText = "写评论…", AcceptsReturn = false };
            var dialog = new ContentDialog
            {
                Title = "评论",
                Content = box,
                PrimaryButtonText = "发送",
                SecondaryButtonText = "取消"
            };

            // ContentDialog 在 10240 上 API 可用
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(box.Text))
            {
                await ViewModel.CommentAsync(post, box.Text);
            }
        }
    }
}

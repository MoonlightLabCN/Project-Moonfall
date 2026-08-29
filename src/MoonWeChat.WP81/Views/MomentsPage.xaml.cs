using System;
using MoonWeChat.Models;
using MoonWeChat.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Popups;

namespace MoonWeChat.Views
{
    /// <summary>朋友圈子页（WP 全景体系下的二级页，无底部 Tab）。</summary>
    public sealed partial class MomentsPage : Page
    {
        public MomentsViewModel ViewModel { get; } = new MomentsViewModel();

        public MomentsPage()
        {
            InitializeComponent();
            DataContext = this;
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // 有共享缓存则立刻显示；后台静默刷新
            var ignored = ViewModel.LoadAsync();
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
            else
            {
                Frame.Navigate(typeof(MainHubPage));
            }
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            try { await ViewModel.LoadAsync(true); } catch (Exception ex) { await new MessageDialog("刷新失败：" + ex.Message).ShowAsync(); }
        }

        private async void OnPublishClick(object sender, RoutedEventArgs e)
        {
            try { ViewModel.Draft = DraftBox.Text; await ViewModel.PublishAsync(); DraftBox.Text = ViewModel.Draft ?? string.Empty; } catch (Exception ex) { await new MessageDialog("发布失败：" + ex.Message).ShowAsync(); }
        }

        private void OnDraftChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.Draft = DraftBox.Text;
        }

        private async void OnLikeClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var btn = sender as Button;
                var post = btn != null ? btn.Tag as MomentPost : null;
                if (post != null) await ViewModel.LikeAsync(post);
            }
            catch (Exception ex) { await new MessageDialog("点赞失败：" + ex.Message).ShowAsync(); }
        }

        private async void OnCommentClick(object sender, RoutedEventArgs e)
        {
            try
            {
            var btn = sender as Button;
            var post = btn != null ? btn.Tag as MomentPost : null;
            if (post == null)
            {
                return;
            }

            var box = new TextBox { PlaceholderText = "写评论…", AcceptsReturn = false };
            var dialog = new MessageDialog("请输入评论内容后点击确定。", "评论");
            dialog.Commands.Add(new UICommand("确定"));
            await dialog.ShowAsync();
            if (!string.IsNullOrWhiteSpace(box.Text))
            {
                await ViewModel.CommentAsync(post, box.Text);
            }
            }
            catch (Exception ex) { await new MessageDialog("评论失败：" + ex.Message).ShowAsync(); }
        }
    }
}

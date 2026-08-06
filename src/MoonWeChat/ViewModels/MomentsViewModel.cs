using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using MoonWeChat.Common;
using MoonWeChat.Models;
using MoonWeChat.Services;

namespace MoonWeChat.ViewModels
{
    public sealed class MomentsViewModel : BindableBase
    {
        public ObservableCollection<MomentPost> Posts { get; } = new ObservableCollection<MomentPost>();

        private string _statusText = string.Empty;
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        private string _draft = string.Empty;
        public string Draft
        {
            get => _draft;
            set => SetProperty(ref _draft, value);
        }

        private bool _busy;
        public bool IsBusy
        {
            get => _busy;
            private set => SetProperty(ref _busy, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand PublishCommand { get; }

        public MomentsViewModel()
        {
            RefreshCommand = new RelayCommand(async _ => await LoadAsync(), _ => !IsBusy);
            PublishCommand = new RelayCommand(async _ => await PublishAsync(), _ => !IsBusy);
        }

        public async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            StatusText = "加载中…";
            try
            {
                var list = await MomentsService.LoadAsync().ConfigureAwait(true);
                Posts.Clear();
                foreach (var p in list)
                {
                    Posts.Add(p);
                }

                StatusText = AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured
                    ? "示例朋友圈（未连远程时展示）"
                    : ("已加载 " + Posts.Count + " 条");
            }
            catch (Exception ex)
            {
                StatusText = "加载失败：" + ex.Message;
            }
            finally
            {
                IsBusy = false;
                RefreshCommand.RaiseCanExecuteChanged();
                PublishCommand.RaiseCanExecuteChanged();
            }
        }

        public async Task PublishAsync()
        {
            if (string.IsNullOrWhiteSpace(Draft))
            {
                StatusText = "写点什么再发";
                return;
            }

            IsBusy = true;
            try
            {
                var text = Draft.Trim();
                var ok = await MomentsService.PublishTextAsync(text).ConfigureAwait(true);
                if (ok)
                {
                    Draft = string.Empty;
                    // 本地插一条到顶部
                    Posts.Insert(0, new MomentPost
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        AuthorName = AppSettings.SelfNickname ?? "我",
                        AuthorAccent = "#07C160",
                        Content = text,
                        CreateTime = DateTimeOffset.Now,
                        TimeText = "刚刚",
                        LikeNames = new System.Collections.Generic.List<string>(),
                        Comments = new System.Collections.Generic.List<MomentComment>()
                    });
                    StatusText = "已发布";
                }
                else
                {
                    StatusText = "发布失败（检查远程 /FriendCircle 接口）";
                }
            }
            finally
            {
                IsBusy = false;
                PublishCommand.RaiseCanExecuteChanged();
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }

        public async Task LikeAsync(MomentPost post)
        {
            if (post == null)
            {
                return;
            }

            await MomentsService.ToggleLikeAsync(post).ConfigureAwait(true);
            RefreshPost(post);
        }

        public async Task CommentAsync(MomentPost post, string text)
        {
            if (post == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var ok = await MomentsService.CommentAsync(post, text).ConfigureAwait(true);
            StatusText = ok ? "评论已发送" : "评论失败";
            if (ok)
            {
                RefreshPost(post);
            }
        }

        private void RefreshPost(MomentPost post)
        {
            var idx = Posts.IndexOf(post);
            if (idx >= 0)
            {
                Posts.RemoveAt(idx);
                Posts.Insert(idx, post);
            }
        }
    }
}

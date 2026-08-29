using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using MoonWeChat.Common;
using MoonWeChat.Models;
using MoonWeChat.Services;

namespace MoonWeChat.ViewModels
{
    public sealed class MomentsViewModel : BindableBase
    {
        // 进程内短缓存：Hub 与 MomentsPage 共用，避免点两次各拉一遍
        private static IReadOnlyList<MomentPost> _sharedCache;
        private static DateTimeOffset _sharedCacheAt;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);

        private ObservableCollection<MomentPost> _posts = new ObservableCollection<MomentPost>();
        public ObservableCollection<MomentPost> Posts
        {
            get => _posts;
            private set => SetProperty(ref _posts, value);
        }

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
            RefreshCommand = new RelayCommand(async _ => await LoadAsync(force: true), _ => !IsBusy);
            PublishCommand = new RelayCommand(async _ => await PublishAsync(), _ => !IsBusy);

            // 有缓存时构造即绑定，页面打开零等待
            if (_sharedCache != null)
            {
                Posts = new ObservableCollection<MomentPost>(_sharedCache);
                StatusText = StatusForCount(_sharedCache.Count);
            }
        }

        public Task LoadAsync() => LoadAsync(force: false);

        public async Task LoadAsync(bool force)
        {
            if (IsBusy)
            {
                return;
            }

            if (!force &&
                _sharedCache != null &&
                (DateTimeOffset.Now - _sharedCacheAt) < CacheTtl)
            {
                ApplyList(_sharedCache, fromCache: true);
                return;
            }

            IsBusy = true;
            if (Posts.Count == 0)
            {
                StatusText = "加载中…";
            }

            try
            {
                // 示例数据同步返回，不走假 async
                IReadOnlyList<MomentPost> list;
                if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
                {
                    list = MomentsService.GetSampleCached();
                }
                else
                {
                    list = await MomentsService.LoadAsync().ConfigureAwait(true);
                }

                _sharedCache = list;
                _sharedCacheAt = DateTimeOffset.Now;
                ApplyList(list, fromCache: false);
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

        private void ApplyList(IReadOnlyList<MomentPost> list, bool fromCache)
        {
            // 整表替换，只触发一次集合变更通知（比 Clear+N 次 Add 轻很多）
            Posts = new ObservableCollection<MomentPost>(list ?? new MomentPost[0]);
            StatusText = fromCache
                ? StatusForCount(Posts.Count) + " · 缓存"
                : StatusForCount(Posts.Count);
        }

        private static string StatusForCount(int count)
        {
            if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
            {
                return "示例朋友圈（未连远程时展示）";
            }

            if (!string.IsNullOrEmpty(MomentsService.LastLoadError))
            {
                return "加载失败：" + MomentsService.LastLoadError;
            }

            return count == 0 ? "没有拉到朋友圈内容" : ("已加载 " + count + " 条");
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
                    var post = new MomentPost
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        AuthorName = AppSettings.SelfNickname ?? "我",
                        AuthorAccent = "#07C160",
                        Content = text,
                        CreateTime = DateTimeOffset.Now,
                        TimeText = "刚刚",
                        LikeNames = new List<string>(),
                        Comments = new List<MomentComment>()
                    };
                    Posts.Insert(0, post);
                    InvalidateSharedCacheWithCurrent();
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
            InvalidateSharedCacheWithCurrent();
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
                InvalidateSharedCacheWithCurrent();
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

        private void InvalidateSharedCacheWithCurrent()
        {
            var copy = new List<MomentPost>(Posts);
            _sharedCache = copy;
            _sharedCacheAt = DateTimeOffset.Now;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MoonWeChat.Models;
using MoonWeChat.Services.WeChatPad;
using Windows.Data.Json;

namespace MoonWeChat.Services
{
    /// <summary>
    /// 朋友圈数据：示例本地造数；实机调 /FriendCircle/GetList + Operation(点赞)。
    /// </summary>
    public static class MomentsService
    {
        private static List<MomentPost> _sampleCache;

        /// <summary>同步取示例数据（已缓存），供 UI 首帧立刻绑定。</summary>
        public static IReadOnlyList<MomentPost> GetSampleCached()
        {
            if (_sampleCache == null)
            {
                _sampleCache = BuildSample();
            }

            return _sampleCache;
        }

        /// <summary>
        /// 真实模式下这一轮加载是否失败。UI 必须据此显示错误，
        /// 绝不能把示例朋友圈当成真实内容展示（那等于对用户撒谎）。
        /// </summary>
        public static string LastLoadError { get; private set; } = string.Empty;

        public static async Task<IReadOnlyList<MomentPost>> LoadAsync()
        {
            LastLoadError = string.Empty;

            if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
            {
                return GetSampleCached();
            }

            try
            {
                AppServices.Api.Configure(AppSettings.BaseUrl, AppSettings.Token);
                var list = await AppServices.Api.GetMomentsListAsync().ConfigureAwait(false);
                // 空列表是合法结果（今天没人发），如实返回空，不拿演示数据充数。
                return list ?? new List<MomentPost>();
            }
            catch (Exception ex)
            {
                // 真实模式失败就是失败。以前这里回落到 GetSampleCached()，
                // 而 UI 又把它标成「已加载 N 条」，用户看到的是假动态却以为是真的。
                LastLoadError = ex.Message;
                return new List<MomentPost>();
            }
        }

        public static async Task<bool> ToggleLikeAsync(MomentPost post)
        {
            if (post == null)
            {
                return false;
            }

            if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
            {
                if (post.LikedByMe)
                {
                    post.LikedByMe = false;
                    post.LikeNames.Remove("我");
                }
                else
                {
                    post.LikedByMe = true;
                    if (!post.LikeNames.Contains("我"))
                    {
                        post.LikeNames.Insert(0, "我");
                    }
                }

                return true;
            }

            try
            {
                AppServices.Api.Configure(AppSettings.BaseUrl, AppSettings.Token);
                var ok = await AppServices.Api.MomentLikeAsync(post.Id, !post.LikedByMe).ConfigureAwait(true);
                if (ok)
                {
                    post.LikedByMe = !post.LikedByMe;
                    if (post.LikedByMe)
                    {
                        if (!post.LikeNames.Contains("我"))
                        {
                            post.LikeNames.Insert(0, "我");
                        }
                    }
                    else
                    {
                        post.LikeNames.Remove("我");
                    }
                }

                return ok;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> PublishTextAsync(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
            {
                return true;
            }

            try
            {
                AppServices.Api.Configure(AppSettings.BaseUrl, AppSettings.Token);
                var r = await AppServices.Api.PublishMomentTextAsync(content.Trim()).ConfigureAwait(true);
                return r != null && r.Ok;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> CommentAsync(MomentPost post, string content)
        {
            if (post == null || string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            content = content.Trim();
            if (AppSettings.UseSampleData || !AppSettings.IsRemoteConfigured)
            {
                post.Comments.Add(new MomentComment { AuthorName = "我", Content = content });
                return true;
            }

            try
            {
                AppServices.Api.Configure(AppSettings.BaseUrl, AppSettings.Token);
                var r = await AppServices.Api.MomentCommentAsync(post.Id, content).ConfigureAwait(true);
                if (r.Ok)
                {
                    post.Comments.Add(new MomentComment { AuthorName = "我", Content = content });
                }

                return r.Ok;
            }
            catch
            {
                return false;
            }
        }

        private static List<MomentPost> BuildSample()
        {
            var now = DateTimeOffset.Now;
            return new List<MomentPost>
            {
                new MomentPost
                {
                    Id = "m1",
                    AuthorWxId = "su_wan_92",
                    AuthorName = "苏晚",
                    AuthorAccent = "#F1592A",
                    Content = "外滩改稿到半夜，开屏色定这个方向了。Windows Phone 上的浅色灰真的很 Metro。",
                    CreateTime = now.AddHours(-2),
                    TimeText = "2 小时前",
                    ImageUrls = new List<string>(),
                    LikeNames = new List<string> { "老陈", "阿KEN", "白泽策划" },
                    Comments = new List<MomentComment>
                    {
                        new MomentComment { AuthorName = "老陈", Content = "色温再冷一点" },
                        new MomentComment { AuthorName = "阿KEN", Content = "立绘我今晚交" },
                    },
                    Location = "上海 · 外滩"
                },
                new MomentPost
                {
                    Id = "m2",
                    AuthorWxId = "ken_a",
                    AuthorName = "阿KEN",
                    AuthorAccent = "#9C6ADE",
                    Content = "月见 / 阿墜 立绘线稿，构图先过一下。",
                    CreateTime = now.AddHours(-5),
                    TimeText = "5 小时前",
                    ImageUrls = new List<string>(),
                    LikeNames = new List<string> { "苏晚", "我" },
                    LikedByMe = true,
                    Comments = new List<MomentComment>()
                },
                new MomentPost
                {
                    Id = "m3",
                    AuthorWxId = "chen_lao",
                    AuthorName = "老陈",
                    AuthorAccent = "#576B95",
                    Content = "内测包已经打好，朋友圈这条当打卡。红包转账我们不做，资金链路太招风。",
                    CreateTime = now.AddDays(-1),
                    TimeText = "昨天",
                    ImageUrls = new List<string>(),
                    LikeNames = new List<string> { "白泽策划" },
                    Comments = new List<MomentComment>
                    {
                        new MomentComment { AuthorName = "白泽策划", Content = "稳" }
                    }
                },
                new MomentPost
                {
                    Id = "m4",
                    AuthorWxId = "me",
                    AuthorName = "我",
                    AuthorAccent = "#07C160",
                    Content = "大月微信：WP 瘦客户端 + 远程 WeChatPadPro。今天把发现页接成朋友圈了。",
                    CreateTime = now.AddDays(-2),
                    TimeText = "2 天前",
                    LikeNames = new List<string> { "苏晚", "老陈", "阿KEN", "白泽策划" },
                    Comments = new List<MomentComment>()
                }
            };
        }
    }
}

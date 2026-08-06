using System;
using System.Collections.Generic;

namespace MoonWeChat.Models
{
    /// <summary>
    /// 一条朋友圈。字段对齐 /FriendCircle/GetList 常见返回（宽松解析）。
    /// </summary>
    public class MomentPost
    {
        public string Id { get; set; }
        public string AuthorWxId { get; set; }
        public string AuthorName { get; set; }
        public string AuthorAvatarUrl { get; set; }
        public string AuthorAccent { get; set; }
        public string Content { get; set; }
        public DateTimeOffset CreateTime { get; set; }
        public string TimeText { get; set; }
        public List<string> ImageUrls { get; set; } = new List<string>();
        public string Location { get; set; }
        public List<string> LikeNames { get; set; } = new List<string>();
        public List<MomentComment> Comments { get; set; } = new List<MomentComment>();
        public bool LikedByMe { get; set; }

        public bool HasImages => ImageUrls != null && ImageUrls.Count > 0;
        public bool HasLikes => LikeNames != null && LikeNames.Count > 0;
        public bool HasComments => Comments != null && Comments.Count > 0;
        public bool HasLocation => !string.IsNullOrEmpty(Location);
        public string LikesText => HasLikes ? string.Join("、", LikeNames) : string.Empty;
        public string FirstImageUrl => HasImages ? ImageUrls[0] : null;
        public bool HasSecondImage => ImageUrls != null && ImageUrls.Count > 1;
        public string SecondImageUrl => HasSecondImage ? ImageUrls[1] : null;
        public bool HasThirdImage => ImageUrls != null && ImageUrls.Count > 2;
        public string ThirdImageUrl => HasThirdImage ? ImageUrls[2] : null;
        public bool HasMoreImages => ImageUrls != null && ImageUrls.Count > 3;
        public string MoreImagesText => HasMoreImages ? ("+" + (ImageUrls.Count - 3)) : string.Empty;
    }

    public class MomentComment
    {
        public string AuthorName { get; set; }
        public string Content { get; set; }
        public string DisplayText => string.IsNullOrEmpty(AuthorName) ? Content : (AuthorName + "：" + Content);
    }
}

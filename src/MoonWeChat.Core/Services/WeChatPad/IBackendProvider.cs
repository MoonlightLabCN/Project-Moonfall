using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoonWeChat.Models;

namespace MoonWeChat.Services.WeChatPad
{
    /// <summary>
    /// 双后端统一协议。PyWeixin（电脑 RPA 网关）与 WeChatPadPro（iPad 协议服务）
    /// 都实现这个接口，页面 / ViewModel / 数据层只依赖 <see cref="IBackendProvider"/>。
    /// </summary>
    public interface IBackendProvider : IDisposable
    {
        BackendKind Backend { get; }
        string ProtocolName { get; }

        void Configure(string baseUrl, string token);

        Task<ApiCallResult> TestReachableAsync();

        Task<ApiCallResult> GenAuthKeyAsync(string adminKey, int count = 1, int days = 365);

        Task<QrLoginResult> GetLoginQrAsync(string proxy = null);

        Task<LoginStatusResult> CheckLoginStatusAsync(string uuid);

        Task<ApiCallResult> NewInitAsync();

        Task<ApiCallResult> GetOnlineStatusAsync();

        Task<DeviceSessionProbe> ProbeDeviceSessionAsync();

        Task<FriendListResult> GetFriendListAsync();

        Task<ApiCallResult> SendTextMessageAsync(string toUserName, string content, string clientMsgId = null);

        Task<ApiCallResult> SendLocationAsync(string toUserName, string name, string address, double lat = 31.23, double lng = 121.47);

        Task<ApiCallResult> SendCardAsync(string toUserName, string cardWxId, string cardNickName);

        Task<ApiCallResult> SendLinkAsync(string toUserName, string title, string url, string desc = null);

        Task<ApiCallResult> SendImageAsync(string toUserName, string base64, string fileName = "image.jpg");

        Task<ApiCallResult> ForwardImageXmlAsync(string toUserName, string messageXml);

        Task<ApiCallResult> RevokeMessageAsync(string toUserName, string clientMsgId, long createTime = 0, string serverMsgId = null);

        Task<ApiCallResult> HeartBeatAsync();

        Task<ApiCallResult> GetSelfProfileAsync();

        Task<ApiCallResult> GetContactDetailAsync(string userName);

        Task<FriendListResult> SearchFriendAsync(string keyword);

        Task<ApiCallResult> SetFriendRemarkAsync(string userName, string remark);

        Task<FriendListResult> GetGroupListAsync();

        Task<int> GetChatRoomMemberCountAsync(string chatroomId);

        Task<ApiCallResult> SendPatAsync(string chatroomOrUser, string targetWxId);

        Task<ApiCallResult> SetChatRoomAnnouncementAsync(string chatroomId, string content);

        Task<ApiCallResult> StartAutoSyncAsync();

        Task<ApiCallResult> AwakenAsync();

        Task<string> GetOnlineInfoTextAsync();

        Task<ApiCallResult> SendFileAsync(string toUserName, string base64, string fileName);

        Task<ApiCallResult> SendEmojiAsync(string toUserName, string emojiMd5OrText);

        Task<ApiCallResult> SendQuoteAsync(string toUserName, string content, string quoteMsgId, string quoteContent, MsgQuoteContext context = null);

        Task<bool> MomentLikeAsync(string momentId, bool like);

        Task<ApiCallResult> PublishMomentTextAsync(string content);

        Task<ApiCallResult> MomentCommentAsync(string momentId, string content);

        Task<ApiCallResult> GetMomentDetailAsync(string momentId);

        Task<List<MomentPost>> GetMomentsListAsync();

        Task<SyncMsgResult> GetSyncMsgAsync();
    }

    /// <summary>
    /// 根据当前设置创建后端 provider。
    /// </summary>
    public static class BackendProviderFactory
    {
        public static IBackendProvider Create(BackendKind kind)
        {
            return kind == BackendKind.WeChatPadPro
                ? (IBackendProvider)new WeChatPadProProvider()
                : new PyWeixinProvider();
        }
    }


    /// <summary>
    /// 后端种类。旧设置值若不存在则默认 PyWeixin（保留 2026-08 交接时的默认行为）。
    /// </summary>
    public enum BackendKind
    {
        PyWeixin = 0,
        WeChatPadPro = 1
    }

    /// <summary>
    /// pyweixin 网关 provider：电脑上跑 server/pyweixin_gateway，路径不带 /api 前缀，
    /// AdminKey 生成 Token 走 /Admin/GenAuthKey?key=...。
    /// </summary>
    public sealed class PyWeixinProvider : WeChatPadApiClient
    {
        public PyWeixinProvider() : base(BackendKind.PyWeixin) { }
    }

    /// <summary>
    /// WeChatPadProMAX provider：自托管 / 托管版协议服务，路径统一带 /api 前缀，
    /// 管理接口用 X-Admin-Token 请求头。
    /// </summary>
    public sealed class WeChatPadProProvider : WeChatPadApiClient
    {
        public WeChatPadProProvider() : base(BackendKind.WeChatPadPro) { }
    }
}

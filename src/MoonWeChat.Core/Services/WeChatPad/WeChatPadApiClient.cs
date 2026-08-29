using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Web.Http.Headers;

// AppSettings 在父命名空间 MoonWeChat.Services
// ProbeDeviceSession 需要读本机已存 wxid 做心跳兜底

namespace MoonWeChat.Services.WeChatPad
{
    /// <summary>
    /// 远程微信后端 HTTP 客户端。两个 provider 共用这个实现：
    /// - PyWeixin：路径不带 /api 前缀，兼容 server/pyweixin_gateway。
    /// - WeChatPadPro：路径统一带 /api 前缀（WeChatPadProMAX v8 swagger 基路径）。
    /// 字段解析尽量兼容 Code/code、Data/data 等写法；路径不对时改候选列表即可。
    /// </summary>
    public class WeChatPadApiClient : IBackendProvider
    {
        private readonly HttpClient _http;
        private string _baseUrl;
        private string _token;

        public WeChatPadApiClient() : this(BackendKind.PyWeixin)
        {
        }

        public WeChatPadApiClient(BackendKind backend)
        {
            Backend = backend;

            // 允许自签证书（局域网/内网部署常见）。
            var filter = new HttpBaseProtocolFilter();
            try
            {
                filter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Untrusted);
                filter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.InvalidName);
                filter.IgnorableServerCertificateErrors.Add(Windows.Security.Cryptography.Certificates.ChainValidationResult.Expired);
            }
            catch
            {
                // 老 SDK 上个别枚举可能不存在，忽略。
            }

            _http = new HttpClient(filter);
            _http.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("application/json"));
        }

        public BackendKind Backend { get; }

        public string ProtocolName =>
            Backend == BackendKind.WeChatPadPro ? "WeChatPadPro" : "pyweixin";

        /// <summary>
        /// 是否允许把 Token 放进 query string。本地 pyweixin 网关只认请求头，
        /// 而且会把请求行打进控制台日志，所以对它必须关闭。
        /// </summary>
        private bool AllowTokenInQuery => Backend == BackendKind.WeChatPadPro;

        /// <summary>当前后端是 WeChatPadProMAX（v8 swagger 契约）。</summary>
        private bool IsMax => Backend == BackendKind.WeChatPadPro;

        public void Configure(string baseUrl, string token)
        {
            _baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            _token = (token ?? string.Empty).Trim();
        }

        /// <summary>
        /// WeChatPadProMAX swagger 规定的认证方式：
        /// 业务接口用 X-Access-Token 请求头，管理接口用 X-Admin-Token 请求头。
        /// 旧版部署也兼容 ?key=TOKEN 查询参数，这里两种都带，确保最大兼容。
        /// </summary>
        private void AttachAuth(HttpRequestMessage req, bool attachToken, bool isAdmin = false)
        {
            if (!attachToken || string.IsNullOrEmpty(_token))
            {
                return;
            }

            // 优先用请求头（swagger 标准）
            var headerName = isAdmin ? "X-Admin-Token" : "X-Access-Token";
            try
            {
                req.Headers.Remove(headerName);
            }
            catch
            {
            }

            req.Headers.TryAppendWithoutValidation(headerName, _token);
        }

        private void AttachAdminKey(HttpRequestMessage req, string adminKey)
        {
            if (string.IsNullOrWhiteSpace(adminKey))
            {
                return;
            }

            try
            {
                req.Headers.Remove("X-Admin-Token");
            }
            catch
            {
            }

            req.Headers.TryAppendWithoutValidation("X-Admin-Token", adminKey.Trim());
        }

        public void Dispose() => _http?.Dispose();

        // ------------------------------------------------------------------
        // 管理：生成授权码
        // ------------------------------------------------------------------

        public async Task<ApiCallResult> GenAuthKeyAsync(string adminKey, int count = 1, int days = 365)
        {
            if (string.IsNullOrWhiteSpace(adminKey))
            {
                return Fail("adminKey 为空");
            }

            // 同时兼容 v8 swagger 小写字段与本地 Docker 部署的大写字段。
            var body = new JsonObject
            {
                ["count"] = JsonValue.CreateNumberValue(count),
                ["Count"] = JsonValue.CreateNumberValue(count),
                ["days"] = JsonValue.CreateNumberValue(days),
                ["Days"] = JsonValue.CreateNumberValue(days),
                ["remark"] = JsonValue.CreateStringValue("moonwechat"),
                ["Remark"] = JsonValue.CreateStringValue("moonwechat"),
            };

            // adminKey 只走 X-Admin-Token 请求头（AttachAdminKey），不拼 query：
            // 它比业务 Token 权限更高，更不能出现在服务端日志的请求行里。
            string[] paths =
            {
                "/Admin/GenAuthKey",
                "/admin/GenAuthKey",
                "/Admin/GenAuthKey1",
                "/admin/GenAuthKey1"
            };

            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await PostJsonAdminAsync(path, body, adminKey).ConfigureAwait(false);
                if (last.Ok)
                {
                    return last;
                }
            }

            return last ?? Fail("GenAuthKey 失败");
        }

        private async Task<ApiCallResult> PostJsonAdminAsync(string pathAndQuery, JsonObject body, string adminKey)
        {
            ApiCallResult last = null;
            foreach (var candidate in BuildPathCandidates(pathAndQuery))
            {
                var url = BuildUrl(candidate, attachToken: false);
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Post, new Uri(url)))
                    {
                        AttachAdminKey(req, adminKey);
                        req.Content = new HttpStringContent(body?.Stringify() ?? "{}", Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                        using (var resp = await _http.SendRequestAsync(req).AsTask().ConfigureAwait(false))
                        {
                            var text = await resp.Content.ReadAsStringAsync().AsTask().ConfigureAwait(false);
                            var result = Interpret(resp, text);
                            if (result.Ok)
                            {
                                return result;
                            }

                            last = result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    last = Fail(ex.Message);
                }
            }

            return last ?? Fail("请求失败");
        }

        public static string ExtractTokenFromAuthResult(ApiCallResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.RawJson))
            {
                return null;
            }

            try
            {
                var root = JsonObject.Parse(result.RawJson);
                // 常见：Data 是字符串 token，或 Data.authKeys[0]，或 Data 数组
                string token = TryGetString(root, "Data") ?? TryGetString(root, "data") ?? TryGetString(root, "token") ?? TryGetString(root, "Token");
                if (!string.IsNullOrEmpty(token) && token.Length > 8 && !token.StartsWith("{") && !token.StartsWith("["))
                {
                    return token;
                }

                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data");
                if (data != null)
                {
                    token = TryGetString(data, "token") ?? TryGetString(data, "Token") ?? TryGetString(data, "authKey") ?? TryGetString(data, "AuthKey");
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }

                    var arr = TryGetArray(data, "authKeys") ?? TryGetArray(data, "AuthKeys") ?? TryGetArray(data, "tokens");
                    if (arr != null && arr.Count > 0)
                    {
                        var first = arr[0];
                        if (first.ValueType == JsonValueType.String)
                        {
                            return first.GetString();
                        }

                        if (first.ValueType == JsonValueType.Object)
                        {
                            var o = first.GetObject();
                            return TryGetString(o, "token") ?? TryGetString(o, "Token") ?? TryGetString(o, "authKey");
                        }
                    }
                }

                var dataArr = TryGetArray(root, "Data") ?? TryGetArray(root, "data");
                if (dataArr != null && dataArr.Count > 0)
                {
                    var first = dataArr[0];
                    if (first.ValueType == JsonValueType.String)
                    {
                        return first.GetString();
                    }

                    if (first.ValueType == JsonValueType.Object)
                    {
                        var o = first.GetObject();
                        return TryGetString(o, "token") ?? TryGetString(o, "Token") ?? TryGetString(o, "authKey") ?? TryGetString(o, "Key");
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }

            return null;
        }

        // ------------------------------------------------------------------
        // 登录
        // ------------------------------------------------------------------

        public async Task<QrLoginResult> GetLoginQrAsync(string proxy = null)
        {
            // swagger: POST /Login/GetQRWinUwp — 专为 Windows UWP 设计（绕过验证）
            // body: {DeviceName, Proxy: {ProxyIp, ProxyUser, ProxyPassword}, oversea}
            var body = new JsonObject
            {
                ["DeviceName"] = JsonValue.CreateStringValue("MoonWeChat"),
                ["oversea"] = JsonValue.CreateBooleanValue(false),
            };
            if (!string.IsNullOrWhiteSpace(proxy))
            {
                var proxyObj = new JsonObject
                {
                    ["ProxyIp"] = JsonValue.CreateStringValue(proxy),
                    ["ProxyUser"] = JsonValue.CreateStringValue(""),
                    ["ProxyPassword"] = JsonValue.CreateStringValue(""),
                };
                body["Proxy"] = proxyObj;
            }

            // swagger PascalCase 路径优先；GetQRWinUwp 是 UWP 专用，GetQRMac 是 Mac 通用。
            // 本地 Docker 部署（v18.6）使用小写 /login/GetLoginQrCode* 路径，也一并兼容。
            string[] paths =
            {
                "/Login/GetQRWinUwp",
                "/Login/GetQRMac",
                "/Login/GetQRPad",
                "/Login/GetQRPadx",
                "/Login/GetQRWin",
                "/Login/GetQRWinUnified",
                "/Login/GetQRx",
                "/Login/GetQR",
                "/login/GetLoginQrCodeMac",
                "/login/GetLoginQrCodeWin",
                "/login/GetLoginQrCodeNew",
                "/login/GetLoginQrCodeNewX",
                "/login/GetLoginQrCodePad",
                "/login/GetLoginQrCodePadX",
            };

            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await PostJsonAsync(path, body, attachToken: true).ConfigureAwait(false);
                if (!last.Ok)
                {
                    continue;
                }

                var qr = ParseQr(last);
                if (qr.Ok && (!string.IsNullOrEmpty(qr.Uuid) || !string.IsNullOrEmpty(qr.QrUrl) || !string.IsNullOrEmpty(qr.QrBase64) || !string.IsNullOrEmpty(qr.QrContent)))
                {
                    return qr;
                }
            }

            return new QrLoginResult
            {
                Ok = false,
                Message = last?.Message ?? "获取登录二维码失败"
            };
        }

        public async Task<LoginStatusResult> CheckLoginStatusAsync(string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                return new LoginStatusResult { Ok = false, Message = "uuid 为空", State = QrScanState.Failed };
            }

            // swagger v8: /Login/CheckQR?uuid=...；本地 Docker v18.6: /login/CheckLoginStatus（token 走 key query）。
            string[] uuidPaths =
            {
                $"/Login/CheckQR?uuid={Uri.EscapeDataString(uuid)}",
                $"/Login/CheckMacQR?uuid={Uri.EscapeDataString(uuid)}",
            };
            string[] localPaths =
            {
                "/login/CheckLoginStatus",
            };

            ApiCallResult last = null;
            foreach (var path in uuidPaths)
            {
                last = await PostJsonAsync(path, new JsonObject(), attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    var parsed = ParseLoginStatus(last);
                    if (parsed.State != QrScanState.Unknown || !string.IsNullOrEmpty(parsed.WxId))
                    {
                        return parsed;
                    }
                }
            }

            // 回退：GET 方式（部分部署支持）；本地 Docker 使用 GET /login/CheckLoginStatus
            foreach (var path in uuidPaths.Concat(localPaths))
            {
                last = await GetAsync(path, attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    var parsed = ParseLoginStatus(last);
                    if (parsed.State != QrScanState.Unknown || !string.IsNullOrEmpty(parsed.WxId))
                    {
                        return parsed;
                    }
                }
            }

            foreach (var path in localPaths)
            {
                last = await PostJsonAsync(path, new JsonObject(), attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    var parsed = ParseLoginStatus(last);
                    if (parsed.State != QrScanState.Unknown || !string.IsNullOrEmpty(parsed.WxId))
                    {
                        return parsed;
                    }
                }
            }

            return new LoginStatusResult
            {
                Ok = false,
                Message = last?.Message ?? "查询登录状态失败",
                State = QrScanState.Unknown,
                RawJson = last?.RawJson
            };
        }

        /// <summary>knowhub 流程第 4 步：扫码成功后初始化会话 /Login/Newinit</summary>
        public async Task<ApiCallResult> NewInitAsync()
        {
            string[] paths =
            {
                "/Login/Newinit",
                "/Login/TwiceAutoAuth",
                "/login/Newinit",
                "/login/LoginNew",
                "/login/GetInItStatus",
            };

            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await PostJsonAsync(path, new JsonObject(), attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    return last;
                }

                last = await GetAsync(path, attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    return last;
                }
            }

            return last ?? Fail("Newinit 失败");
        }

        public async Task<ApiCallResult> GetOnlineStatusAsync()
        {
            string[] paths =
            {
                "/Login/GetCacheInfo",
                "/api/login/GetLoginStatus",
                "/login/GetLoginStatus",
                "/Login/GetLoginStatus"
            };

            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await GetAsync(path, attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    return last;
                }

                last = await PostJsonAsync(path, new JsonObject(), attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    return last;
                }
            }

            return last ?? Fail("无法获取在线状态");
        }

        /// <summary>
        /// 探测当前 Token 在服务器上是否仍有微信在线会话。
        /// 用于 App 二次启动免扫码：在线则直接进；离线才提示重扫。
        /// </summary>
        public async Task<DeviceSessionProbe> ProbeDeviceSessionAsync()
        {
            var probe = new DeviceSessionProbe { Online = false };

            // 优先 GetLoginStatus / CacheInfo / 资料
            string[] paths =
            {
                "/login/GetLoginStatus",
                "/Login/GetLoginStatus",
                "/Login/GetCacheInfo",
                "/api/login/GetLoginStatus",
                "/User/GetContractProfile",
                "/user/GetContractProfile",
                "/User/GetOnlineInfo"
            };

            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await GetAsync(path, attachToken: true).ConfigureAwait(false);
                if (!last.Ok)
                {
                    last = await PostJsonAsync(path, new JsonObject(), attachToken: true).ConfigureAwait(false);
                }

                if (last == null)
                {
                    continue;
                }

                var text = (last.Message ?? string.Empty) + " " + (last.RawJson ?? string.Empty);
                if (LooksLikeOffline(text))
                {
                    probe.Online = false;
                    probe.Message = last.Message ?? "该账号需要重新登录";
                    // 继续试别的路径，有的接口更准
                    continue;
                }

                if (!last.Ok)
                {
                    continue;
                }

                // 解析 wxid / nick
                try
                {
                    if (!string.IsNullOrEmpty(last.RawJson))
                    {
                        var root = JsonObject.Parse(last.RawJson);
                        var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data") ?? root;
                        var wxid = TryGetString(data, "wxid")
                                   ?? TryGetString(data, "Wxid")
                                   ?? TryGetString(data, "WxId")
                                   ?? TryGetString(data, "UserName")
                                   ?? TryGetString(data, "userName")
                                   ?? TryGetString(data, "username");
                        var nick = TryGetString(data, "nick_name")
                                   ?? TryGetString(data, "NickName")
                                   ?? TryGetString(data, "nickName")
                                   ?? TryGetString(data, "Nickname");
                        var loginState = TryGetString(data, "loginState")
                                         ?? TryGetString(data, "LoginState")
                                         ?? TryGetString(data, "state")
                                         ?? TryGetString(data, "State");

                        if (!string.IsNullOrEmpty(wxid))
                        {
                            probe.WxId = wxid;
                        }

                        if (!string.IsNullOrEmpty(nick))
                        {
                            probe.Nickname = nick;
                        }

                        if (!string.IsNullOrEmpty(loginState) &&
                            loginState.IndexOf("NoLogin", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            probe.Online = false;
                            probe.Message = "loginState == MMLoginStateNoLogin";
                            continue;
                        }

                        // 有 wxid 或明确成功码 → 视为在线
                        if (!string.IsNullOrEmpty(probe.WxId) || last.Code == 200 || last.Ok)
                        {
                            // 若仅 Ok 但文案像掉线，不算在线
                            if (!LooksLikeOffline(text))
                            {
                                // 有资料字段才更可信；纯 200 空 data 可能仍是假在线
                                if (!string.IsNullOrEmpty(probe.WxId) || !string.IsNullOrEmpty(probe.Nickname))
                                {
                                    probe.Online = true;
                                    probe.Message = "online";
                                    return probe;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // parse fail → try next
                }
            }

            // 兜底：心跳成功也算“链路还在”
            try
            {
                var hb = await HeartBeatAsync().ConfigureAwait(false);
                if (hb.Ok && !LooksLikeOffline((hb.Message ?? "") + " " + (hb.RawJson ?? "")))
                {
                    // 心跳 OK 但没有 wxid：若本机已有 wxid，也算在线
                    var localWx = MoonWeChat.Services.AppSettings.WxId;
                    if (!string.IsNullOrWhiteSpace(localWx))
                    {
                        probe.Online = true;
                        probe.WxId = localWx;
                        probe.Nickname = MoonWeChat.Services.AppSettings.SelfNickname;
                        probe.Message = "heartbeat ok";
                        return probe;
                    }
                }
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(probe.Message))
            {
                probe.Message = last?.Message ?? "未能确认在线状态，请重新扫码";
            }

            probe.Online = false;
            return probe;
        }

        private static bool LooksLikeOffline(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // 中文/英文常见掉线文案
            string[] keys =
            {
                "不存在",
                "重新登录",
                "NoLogin",
                "未登录",
                "掉线",
                "离线",
                "not login",
                "not online",
                "expired",
                "过期",
                "版本过低"
            };

            foreach (var k in keys)
            {
                if (text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------------
        // 通讯录
        // ------------------------------------------------------------------

        public async Task<FriendListResult> GetFriendListAsync()
        {
            // swagger: POST /Friend/GetContractList
            // body: {currentWxcontactSeq, currentChatRoomContactSeq} — camelCase
            string[] paths =
            {
                "/Friend/GetContractList",
                "/friend/GetFriendList",
                "/friend/GetContactList",
            };

            var seqBody = new JsonObject
            {
                ["currentWxcontactSeq"] = JsonValue.CreateNumberValue(0),
                ["currentChatRoomContactSeq"] = JsonValue.CreateNumberValue(0),
            };

            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await PostJsonAsync(path, seqBody, attachToken: true).ConfigureAwait(false);
                if (!last.Ok)
                {
                    continue;
                }

                var friends = ParseFriendList(last);
                if (friends.Ok)
                {
                    return friends;
                }
            }

            return new FriendListResult
            {
                Ok = false,
                Message = last?.Message ?? "拉取好友列表失败"
            };
        }

        // ------------------------------------------------------------------
        // 消息
        // ------------------------------------------------------------------

        public async Task<ApiCallResult> SendTextMessageAsync(string toUserName, string content, string clientMsgId = null)
        {
            if (string.IsNullOrWhiteSpace(toUserName) || string.IsNullOrWhiteSpace(content))
            {
                return Fail("收件人或内容为空");
            }

            // swagger: POST /Msg/SendTxt
            // body: {ToWxid, Content, At, Type} — 注意是 ToWxid 不是 ToUserName
            var body = new JsonObject
            {
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName),
                ["Content"] = JsonValue.CreateStringValue(content),
                ["At"] = JsonValue.CreateStringValue(""),
                // Msg.SendNewMsgParamDoc 的字段说明是「Type请填写1」（1 = 文本），
                // 而手册 §1.3 的 curl 示例写的是 0 —— 文档自身不一致。
                // 以字段级模型说明为准，MAX 用 1；pyweixin 网关忽略这个字段。
                ["Type"] = JsonValue.CreateNumberValue(IsMax ? 1 : 0),
            };

            // The local pyweixin gateway uses this id to correlate the
            // optimistic phone bubble with the single HTTP send operation.
            // Do not add the extension field to WeChatPadPro requests.
            if (Backend == BackendKind.PyWeixin && !string.IsNullOrWhiteSpace(clientMsgId))
            {
                body["ClientMsgId"] = JsonValue.CreateStringValue(clientMsgId.Trim());
            }

            // 发送类接口绝不做「第一条路径失败就换下一条」的回退：
            // 失败原因可能是超时，而这一侧的超时并不代表电脑微信那边没发出去，
            // 再打一次就会重复发送。pyweixin 只有 /Msg/SendTxt，WeChatPadPro 走 /api 前缀候选。
            return await PostJsonAsync("/Msg/SendTxt", body, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
        }

        /// <summary>knowhub: POST /Msg/ShareLocation</summary>
        public async Task<ApiCallResult> SendLocationAsync(string toUserName, string name, string address, double lat = 31.23, double lng = 121.47)
        {
            if (string.IsNullOrWhiteSpace(toUserName))
            {
                return Fail("toUserName 为空");
            }

            // swagger: POST /Msg/ShareLocation
            // body: {ToWxid, X, Y, Poiname, Label, Scale, Infourl}
            var body = new JsonObject
            {
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName),
                ["X"] = JsonValue.CreateNumberValue(lng),
                ["Y"] = JsonValue.CreateNumberValue(lat),
                ["Poiname"] = JsonValue.CreateStringValue(name ?? "位置"),
                ["Label"] = JsonValue.CreateStringValue(address ?? string.Empty),
                ["Scale"] = JsonValue.CreateNumberValue(15),
                ["Infourl"] = JsonValue.CreateStringValue(""),
            };

            return await PostFirstOkAsync(body, "/Msg/ShareLocation").ConfigureAwait(false);
        }

        /// <summary>knowhub: POST /Msg/ShareCard</summary>
        public async Task<ApiCallResult> SendCardAsync(string toUserName, string cardWxId, string cardNickName)
        {
            if (string.IsNullOrWhiteSpace(toUserName) || string.IsNullOrWhiteSpace(cardWxId))
            {
                return Fail("toUserName / cardWxId 为空");
            }

            // swagger: POST /Msg/ShareCard, body: {ToWxid, CardWxId, CardNickName, CardAlias}
            var body = new JsonObject
            {
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName),
                ["CardWxId"] = JsonValue.CreateStringValue(cardWxId),
                ["CardNickName"] = JsonValue.CreateStringValue(cardNickName ?? cardWxId),
                ["CardAlias"] = JsonValue.CreateStringValue(cardNickName ?? string.Empty),
            };

            return await PostFirstOkAsync(body, "/Msg/ShareCard").ConfigureAwait(false);
        }

        /// <summary>knowhub: POST /Msg/ShareLink</summary>
        public async Task<ApiCallResult> SendLinkAsync(string toUserName, string title, string url, string desc = null)
        {
            if (string.IsNullOrWhiteSpace(toUserName) || string.IsNullOrWhiteSpace(url))
            {
                return Fail("toUserName / url 为空");
            }

            // swagger: POST /Msg/ShareLink, body: {ToWxid, Type, Xml}
            // Xml 是微信链接卡片 XML 格式
            var xml = "<appmsg appid=\"\" sdkver=\"0\"><title>" + XmlEscape(title ?? "链接") +
                      "</title><des>" + XmlEscape(desc ?? "") +
                      "</des><url>" + XmlEscape(url) +
                      "</url><type>5</type></appmsg>";
            var body = new JsonObject
            {
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName),
                ["Type"] = JsonValue.CreateNumberValue(5),
                ["Xml"] = JsonValue.CreateStringValue(xml),
            };

            return await PostFirstOkAsync(body, "/Msg/ShareLink").ConfigureAwait(false);
        }

        /// <summary>
        /// 图片：POST /Msg/UploadImg。
        /// MAX v8 手册里这个接口的摘要就是「发送图片」—— 它自己完成上传并投递，
        /// 不需要第二步。`/Msg/SendCDNImg` 的摘要是「发送Cdn图片(转发图片)」、
        /// 参数是 `Content == 消息xml`，那是**转发一条已存在的图片消息**，
        /// 不是上传的收尾。原来的两步写法会拿 UploadImg 的 Data JSON 当 xml 去转发，
        /// 必然失败，于是一张实际已经发出去的图片在手机上显示成红色感叹号。
        /// </summary>
        public async Task<ApiCallResult> SendImageAsync(string toUserName, string base64, string fileName = "image.jpg")
        {
            if (string.IsNullOrWhiteSpace(toUserName) || string.IsNullOrWhiteSpace(base64))
            {
                return Fail("toUserName / 图片数据为空");
            }

            var comma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                base64 = base64.Substring(comma + 1);
            }

            // Msg.SendImageMsgParamDoc: {Base64, ToWxid}
            var uploadBody = new JsonObject
            {
                ["Base64"] = JsonValue.CreateStringValue(base64),
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName),
            };

            // 单路径、超时不重试：没拿到应答不代表没发出去。
            return await PostJsonAsync("/Msg/UploadImg", uploadBody, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
        }

        /// <summary>
        /// 转发一条已收到的图片消息：POST /Msg/SendCDNImg，Content 必须是原始消息 xml。
        /// 目前 UI 没有转发入口，保留接口以免以后又被误当成上传收尾来用。
        /// </summary>
        public async Task<ApiCallResult> ForwardImageXmlAsync(string toUserName, string messageXml)
        {
            if (string.IsNullOrWhiteSpace(toUserName) || string.IsNullOrWhiteSpace(messageXml))
            {
                return Fail("toUserName / 消息 xml 为空");
            }

            var body = new JsonObject
            {
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName),
                ["Content"] = JsonValue.CreateStringValue(messageXml),
            };
            return await PostJsonAsync("/Msg/SendCDNImg", body, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
        }

        /// <summary>
        /// 撤回：POST /Msg/Revoke。
        /// Msg.RevokeMsgParamDoc 把 ClientMsgId / CreateTime / NewMsgId 都声明成
        /// integer(int64)，ToUserName 是字符串。
        /// 原来的写法把三个 id 都当字符串发，而且 ClientMsgId 和 NewMsgId 传同一个值 ——
        /// 对自己发出的消息那是本地 GUID（32 位 hex），服务端不可能认，
        /// 所以撤回从来没有真正成功过。
        /// </summary>
        public async Task<ApiCallResult> RevokeMessageAsync(string toUserName, string clientMsgId, long createTime = 0, string serverMsgId = null)
        {
            var newMsgId = ParseInt64OrZero(serverMsgId);
            var localMsgId = ParseInt64OrZero(clientMsgId);
            if (IsMax && newMsgId == 0 && localMsgId == 0)
            {
                return Fail("这条消息没有服务端消息号（发送应答里没带回来），无法撤回。");
            }

            var body = new JsonObject
            {
                ["ToUserName"] = JsonValue.CreateStringValue(toUserName ?? string.Empty),
                ["ClientMsgId"] = JsonValue.CreateNumberValue(localMsgId),
                ["NewMsgId"] = JsonValue.CreateNumberValue(newMsgId != 0 ? newMsgId : localMsgId),
                ["CreateTime"] = JsonValue.CreateNumberValue(createTime > 0 ? createTime : UnixNow()),
            };

            // 单路径：撤回不是幂等操作，换路径重试没有意义。
            return await PostJsonAsync("/Msg/Revoke", body, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
        }

        /// <summary>把 64 位消息号的字符串形式转成数字；本地 GUID 之类转不了就返回 0。</summary>
        private static long ParseInt64OrZero(string value)
        {
            long parsed;
            return long.TryParse((value ?? string.Empty).Trim(), out parsed) ? parsed : 0L;
        }

        /// <summary>knowhub: POST /Login/HeartBeat — 保活，防掉线</summary>
        public async Task<ApiCallResult> HeartBeatAsync()
        {
            var body = new JsonObject();
            var r = await PostFirstOkAsync(body, "/Login/HeartBeat", "/Login/HeartBeatLong", "/Login/AutoHeartBeat").ConfigureAwait(false);
            if (r.Ok)
            {
                return r;
            }

            return await GetAsync("/Login/HeartBeat", attachToken: true).ConfigureAwait(false);
        }

        /// <summary>knowhub: POST /User/GetContractProfile — 自己的资料</summary>
        public async Task<ApiCallResult> GetSelfProfileAsync()
        {
            var body = new JsonObject();
            return await PostFirstOkAsync(body, "/User/GetContractProfile", "/user/GetContractProfile", "/User/GetOnlineInfo", "/user/GetProfile").ConfigureAwait(false);
        }

        /// <summary>knowhub: POST /Friend/GetContractDetail</summary>
        public async Task<ApiCallResult> GetContactDetailAsync(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Fail("userName 为空");
            }

            var body = new JsonObject
            {
                ["UserName"] = JsonValue.CreateStringValue(userName),
                ["userName"] = JsonValue.CreateStringValue(userName),
                ["Wxid"] = JsonValue.CreateStringValue(userName),
            };
            var arr = new JsonArray { JsonValue.CreateStringValue(userName) };
            body["UserNames"] = arr;
            body["ToUserName"] = JsonValue.CreateStringValue(userName);

            return await PostFirstOkAsync(body, "/Friend/GetContractDetail", "/friend/GetContractDetail", "/friend/GetContactDetailsList").ConfigureAwait(false);
        }

        /// <summary>POST /Friend/Search</summary>
        public async Task<FriendListResult> SearchFriendAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return new FriendListResult { Ok = false, Message = "关键词为空" };
            }

            var body = new JsonObject
            {
                ["UserName"] = JsonValue.CreateStringValue(keyword.Trim()),
                ["userName"] = JsonValue.CreateStringValue(keyword.Trim()),
                ["SearchScene"] = JsonValue.CreateNumberValue(1),
                ["OpCode"] = JsonValue.CreateNumberValue(0),
            };
            var result = await PostFirstOkAsync(body, "/Friend/Search", "/friend/Search", "/friend/SearchContact").ConfigureAwait(false);
            if (!result.Ok)
            {
                return new FriendListResult { Ok = false, Message = result.Message };
            }

            return ParseFriendList(result);
        }

        /// <summary>POST /Friend/SetRemarks</summary>
        public async Task<ApiCallResult> SetFriendRemarkAsync(string userName, string remark)
        {
            var body = new JsonObject
            {
                ["UserName"] = JsonValue.CreateStringValue(userName ?? string.Empty),
                ["Remark"] = JsonValue.CreateStringValue(remark ?? string.Empty),
                ["remark"] = JsonValue.CreateStringValue(remark ?? string.Empty),
            };
            return await PostFirstOkAsync(body, "/Friend/SetRemarks", "/friend/SetRemarks", "/user/ModifyRemark").ConfigureAwait(false);
        }

        // ------------------------------------------------------------------
        // 群 Group
        // ------------------------------------------------------------------

        public async Task<FriendListResult> GetGroupListAsync()
        {
            var body = new JsonObject();
            var result = await PostFirstOkAsync(body, "/Group/GroupList", "/Group/GetChatRoomInfo", "/friend/GroupList", "/group/GroupList", "/group/GetAllGroupList").ConfigureAwait(false);
            if (!result.Ok)
            {
                return new FriendListResult { Ok = false, Message = result.Message };
            }

            var parsed = ParseFriendList(result);
            // 标记为群
            foreach (var c in parsed.Contacts)
            {
                if (c.UserName != null && !c.UserName.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase))
                {
                    // 有些返回没有 @chatroom 后缀，仍标 IsChatroom
                    c.IsChatroom = true;
                }
                else
                {
                    c.IsChatroom = true;
                }
            }

            parsed.Ok = true;
            return parsed;
        }

        public async Task<int> GetChatRoomMemberCountAsync(string chatroomId)
        {
            if (string.IsNullOrWhiteSpace(chatroomId))
            {
                return 0;
            }

            var body = new JsonObject
            {
                ["ChatRoomName"] = JsonValue.CreateStringValue(chatroomId),
                ["chatRoomName"] = JsonValue.CreateStringValue(chatroomId),
                ["UserName"] = JsonValue.CreateStringValue(chatroomId),
            };
            var result = await PostFirstOkAsync(body,
                "/Group/GetChatRoomMemberDetail",
                "/Group/GetChatRoomInfoDetail",
                "/Group/GetChatRoomInfo",
                "/group/GetChatroomMemberDetail",
                "/group/GetChatRoomInfo").ConfigureAwait(false);
            if (!result.Ok || string.IsNullOrEmpty(result.RawJson))
            {
                return 0;
            }

            try
            {
                var root = JsonObject.Parse(result.RawJson);
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data") ?? root;
                var members = TryGetArray(data, "NewChatroomData")
                              ?? TryGetArray(data, "ChatRoomMember")
                              ?? TryGetArray(data, "MemberList")
                              ?? TryGetArray(data, "memberList")
                              ?? TryGetArray(data, "ChatRoomMembers");
                if (members != null)
                {
                    return members.Count;
                }

                // MemberCount 字段
                foreach (var k in new[] { "MemberCount", "memberCount", "ChatRoomMemberCount" })
                {
                    if (data.ContainsKey(k) && data[k].ValueType == JsonValueType.Number)
                    {
                        return (int)data[k].GetNumber();
                    }
                }
            }
            catch
            {
                // ignore
            }

            return 0;
        }

        /// <summary>POST /Group/SendPat 拍一拍</summary>
        public async Task<ApiCallResult> SendPatAsync(string chatroomOrUser, string targetWxId)
        {
            // Group.SendPatParamDoc: {QID, Scene, ToUserName}
            // QID = 会话（群）id，ToUserName = 被拍的人。
            var body = new JsonObject
            {
                ["QID"] = JsonValue.CreateStringValue(chatroomOrUser ?? string.Empty),
                ["ToUserName"] = JsonValue.CreateStringValue(targetWxId ?? chatroomOrUser ?? string.Empty),
                ["Scene"] = JsonValue.CreateNumberValue(0),
            };

            // 原来这里的回退链是 "/Group/SendPat" → "/Msg/SendApp" → "/group/SendPat"。
            // `/Msg/SendApp` 在 MAX v8 里是**群发消息**（Content + ToIds 数组），
            // 拿拍一拍的 body 打过去语义完全不对，属于不该存在的回退。
            return await PostJsonAsync("/Group/SendPat", body, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
        }

        /// <summary>POST /Group/SetChatRoomAnnouncement</summary>
        public async Task<ApiCallResult> SetChatRoomAnnouncementAsync(string chatroomId, string content)
        {
            var body = new JsonObject
            {
                ["ChatRoomName"] = JsonValue.CreateStringValue(chatroomId ?? string.Empty),
                ["Content"] = JsonValue.CreateStringValue(content ?? string.Empty),
            };
            return await PostFirstOkAsync(body, "/Group/SetChatRoomAnnouncement", "/group/SetChatroomAnnouncement").ConfigureAwait(false);
        }

        // ------------------------------------------------------------------
        // 消息扩展：文件 / 表情 / 引用 / 自动同步
        // ------------------------------------------------------------------

        public async Task<ApiCallResult> StartAutoSyncAsync()
        {
            // Msg.SyncParam2Doc 只有一个字段 TargetURL（兼容旧版，可忽略）。
            // 原来发的 SyncKey 不在契约里；回退到 /Msg/Sync 语义也不同
            // —— 那是「触发一次增量同步」，不是「启用自动同步」。
            var body = new JsonObject
            {
                ["TargetURL"] = JsonValue.CreateStringValue(string.Empty),
            };
            return await PostJsonAsync("/Msg/StartAutoSync", body, attachToken: true).ConfigureAwait(false);
        }

        public async Task<ApiCallResult> AwakenAsync()
        {
            var body = new JsonObject();
            return await PostFirstOkAsync(body, "/Login/Awaken", "/Login/TwiceAutoAuth", "/login/WakeUpLogin").ConfigureAwait(false);
        }

        public async Task<string> GetOnlineInfoTextAsync()
        {
            var r = await PostFirstOkAsync(new JsonObject(), "/User/GetOnlineInfo", "/User/GetAllOnline", "/User/GetContractProfile", "/user/GetProfile").ConfigureAwait(false);
            if (!r.Ok)
            {
                return r.Message ?? "离线/未知";
            }

            try
            {
                var root = JsonObject.Parse(r.RawJson ?? "{}");
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data") ?? root;
                var nick = TryGetString(data, "NickName") ?? TryGetString(data, "nickName");
                var wxid = TryGetString(data, "UserName") ?? TryGetString(data, "wxid");
                var state = TryGetString(data, "State") ?? TryGetString(data, "status") ?? TryGetString(data, "Status");
                if (!string.IsNullOrEmpty(nick) || !string.IsNullOrEmpty(wxid))
                {
                    return "在线 · " + (nick ?? "") + " " + (wxid ?? "") + (string.IsNullOrEmpty(state) ? "" : (" · " + state));
                }

                return "服务器已响应 · " + Truncate(r.RawJson, 80);
            }
            catch
            {
                return "服务器已响应";
            }
        }

        public async Task<ApiCallResult> SendFileAsync(string toUserName, string base64, string fileName)
        {
            if (string.IsNullOrWhiteSpace(toUserName) || string.IsNullOrWhiteSpace(base64))
            {
                return Fail("toUserName / 文件为空");
            }

            var comma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                base64 = base64.Substring(comma + 1);
            }

            if (IsMax)
            {
                // MAX v8 的 Msg 模块共 18 个接口，其中**没有任何一个是「上传文件」**：
                // /Msg/SendCDNFile 的摘要是「发送文件(转发,并非上传)」，
                // 参数说明是 `Content == 收到文件消息xml`。
                // 把 base64 塞进 Content 永远不会成功，只会得到一条服务端错误，
                // 所以这里如实拒绝，而不是发一条注定失败的请求再把原因转述成乱码。
                return Fail("WeChatPadProMAX 不支持从客户端上传文件（/Msg/SendCDNFile 只能转发已收到的文件消息）。请在电脑微信里发送，或改发图片。");
            }

            // pyweixin 网关自己会把 base64 落盘再走 RPA 发送。
            var sendBody = new JsonObject
            {
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName),
                ["Content"] = JsonValue.CreateStringValue(base64),
            };

            return await PostJsonAsync("/Msg/SendCDNFile", sendBody, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
        }

        public async Task<ApiCallResult> SendEmojiAsync(string toUserName, string emojiMd5OrText)
        {
            // swagger: POST /Msg/SendEmoji, body: {Md5, ToWxid, TotalLen}
            var body = new JsonObject
            {
                ["ToWxid"] = JsonValue.CreateStringValue(toUserName ?? string.Empty),
                ["Md5"] = JsonValue.CreateStringValue(emojiMd5OrText ?? string.Empty),
                ["TotalLen"] = JsonValue.CreateNumberValue(0),
            };
            var r = await PostJsonAsync("/Msg/SendEmoji", body, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
            if (r.Ok || r.TransportError)
            {
                return r;
            }

            return await SendTextMessageAsync(toUserName, emojiMd5OrText).ConfigureAwait(false);
        }

        public async Task<ApiCallResult> SendQuoteAsync(string toUserName, string content, string quoteMsgId, string quoteContent, MsgQuoteContext context = null)
        {
            // Msg.QuoteDoc。手册推荐的形状是
            //   {"content":"回复内容", "reply_context": 收到消息.reply_context}
            // 并注明「64位 svr_id/new_msg_id 必须使用字符串」。
            // 群聊时 reply_context 要同时带群成员 from_user_id 与群会话 chat_user_id。
            var svrId = quoteMsgId ?? string.Empty;
            var msgType = (context != null && context.MsgType > 0) ? context.MsgType : 1;

            var replyContext = new JsonObject
            {
                ["svr_id"] = JsonValue.CreateStringValue(svrId),
                ["new_msg_id"] = JsonValue.CreateStringValue(svrId),
                ["msg_type"] = JsonValue.CreateNumberValue(msgType),
                ["to_wxid"] = JsonValue.CreateStringValue(toUserName ?? string.Empty),
                ["conversation_id"] = JsonValue.CreateStringValue(toUserName ?? string.Empty),
                ["quote_content"] = JsonValue.CreateStringValue(quoteContent ?? string.Empty),
            };

            if (context != null)
            {
                if (context.MsgId != 0)
                {
                    replyContext["msg_id"] = JsonValue.CreateNumberValue(context.MsgId);
                }

                if (context.Sequence != 0)
                {
                    replyContext["sequence"] = JsonValue.CreateStringValue(context.Sequence.ToString());
                }

                // 群聊：被引用的人是群成员，会话是群本身；单聊两者相同。
                replyContext["from_user_id"] = JsonValue.CreateStringValue(
                    string.IsNullOrEmpty(context.FromUserId) ? (toUserName ?? string.Empty) : context.FromUserId);
                replyContext["chat_user_id"] = JsonValue.CreateStringValue(
                    string.IsNullOrEmpty(context.ChatUserId) ? (toUserName ?? string.Empty) : context.ChatUserId);
            }

            // 顶层同时保留扁平字段：Msg.QuoteDoc 两种形状都声明了。
            var body = new JsonObject
            {
                ["to_wxid"] = JsonValue.CreateStringValue(toUserName ?? string.Empty),
                ["content"] = JsonValue.CreateStringValue(content ?? string.Empty),
                ["quote_content"] = JsonValue.CreateStringValue(quoteContent ?? string.Empty),
                ["svr_id"] = JsonValue.CreateStringValue(svrId),
                ["new_msg_id"] = JsonValue.CreateStringValue(svrId),
                ["msg_type"] = JsonValue.CreateNumberValue(msgType),
                ["reply_context"] = replyContext,
            };
            var r = await PostJsonAsync("/Msg/Quote", body, attachToken: true, stopOnTransportError: true).ConfigureAwait(false);
            if (r.Ok || r.TransportError)
            {
                // TransportError：服务端可能已经发出去了，绝不能再拼一条纯文本重发。
                return r;
            }

            // 回退：拼引用格式文本（仅在服务端明确答复「这条路不通」时）
            var fallback = string.IsNullOrEmpty(quoteContent)
                ? content
                : ("「" + quoteContent + "」\n- - - - - - - - - -\n" + content);
            return await SendTextMessageAsync(toUserName, fallback).ConfigureAwait(false);
        }

        // ------------------------------------------------------------------
        // 朋友圈 FriendCircle
        // ------------------------------------------------------------------

        public async Task<List<Models.MomentPost>> GetMomentsListAsync()
        {
            // FriendCircle.GetListParamDoc: {fristpagemd5, maxid} —— 全小写，
            // 而且服务端把 first 拼成了 frist，照抄，别顺手改对。
            var body = new JsonObject
            {
                ["fristpagemd5"] = JsonValue.CreateStringValue(string.Empty),
                ["maxid"] = JsonValue.CreateNumberValue(0),
            };

            string[] paths =
            {
                "/FriendCircle/GetList",
                "/FriendCircle/MmSnsSync",
                "/FriendCircle/Messages",
                "/sns/GetSnsSync",
                "/sns/SendSnsTimeLine",
                "/sns/SendFriendCircle",
            };

            foreach (var path in paths)
            {
                var result = await PostJsonAsync(path, body, attachToken: true).ConfigureAwait(false);
                if (!result.Ok)
                {
                    continue;
                }

                var list = ParseMoments(result);
                if (list.Count > 0)
                {
                    return list;
                }
            }

            return new List<Models.MomentPost>();
        }

        public async Task<bool> MomentLikeAsync(string momentId, bool like)
        {
            if (string.IsNullOrEmpty(momentId))
            {
                return false;
            }

            // Operation 常见：Type 1=赞 2=取消赞（以部署为准，双试）
            var body = new JsonObject
            {
                ["Id"] = JsonValue.CreateStringValue(momentId),
                ["id"] = JsonValue.CreateStringValue(momentId),
                ["SnsObjId"] = JsonValue.CreateStringValue(momentId),
                ["Type"] = JsonValue.CreateNumberValue(like ? 1 : 2),
                ["type"] = JsonValue.CreateNumberValue(like ? 1 : 2),
            };

            var r = await PostFirstOkAsync(body, "/FriendCircle/Operation", "/FriendCircle/Comment").ConfigureAwait(false);
            return r.Ok;
        }

        public async Task<ApiCallResult> PublishMomentTextAsync(string content)
        {
            var body = new JsonObject
            {
                ["Content"] = JsonValue.CreateStringValue(content ?? string.Empty),
                ["content"] = JsonValue.CreateStringValue(content ?? string.Empty),
                ["Privacy"] = JsonValue.CreateNumberValue(0),
                ["Text"] = JsonValue.CreateStringValue(content ?? string.Empty),
            };
            return await PostFirstOkAsync(body, "/FriendCircle/Messages", "/FriendCircle/Upload", "/FriendCircle/PushCommnet").ConfigureAwait(false);
        }

        /// <summary>POST /FriendCircle/Comment 或 PushCommnet</summary>
        public async Task<ApiCallResult> MomentCommentAsync(string momentId, string content)
        {
            if (string.IsNullOrWhiteSpace(momentId) || string.IsNullOrWhiteSpace(content))
            {
                return Fail("momentId / content 为空");
            }

            var body = new JsonObject
            {
                ["Id"] = JsonValue.CreateStringValue(momentId),
                ["SnsObjId"] = JsonValue.CreateStringValue(momentId),
                ["Content"] = JsonValue.CreateStringValue(content.Trim()),
                ["content"] = JsonValue.CreateStringValue(content.Trim()),
                ["ReplyCommnetId"] = JsonValue.CreateNumberValue(0),
                ["Type"] = JsonValue.CreateNumberValue(2),
            };
            return await PostFirstOkAsync(body, "/FriendCircle/Comment", "/FriendCircle/PushCommnet", "/FriendCircle/GetCommnet").ConfigureAwait(false);
        }

        public async Task<ApiCallResult> GetMomentDetailAsync(string momentId)
        {
            var body = new JsonObject
            {
                ["Id"] = JsonValue.CreateStringValue(momentId ?? string.Empty),
                ["SnsObjId"] = JsonValue.CreateStringValue(momentId ?? string.Empty),
            };
            return await PostFirstOkAsync(body, "/FriendCircle/GetDetail", "/FriendCircle/GetIdDetail", "/FriendCircle/GetCommnet").ConfigureAwait(false);
        }

        private static List<Models.MomentPost> ParseMoments(ApiCallResult result)
        {
            var list = new List<Models.MomentPost>();
            if (result == null || string.IsNullOrEmpty(result.RawJson))
            {
                return list;
            }

            try
            {
                var root = JsonObject.Parse(result.RawJson);
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data") ?? root;
                var arr = TryGetArray(data, "ObjectList")
                          ?? TryGetArray(data, "objectList")
                          ?? TryGetArray(data, "List")
                          ?? TryGetArray(data, "list")
                          ?? TryGetArray(data, "SnsList")
                          ?? TryGetArray(data, "snsList")
                          ?? TryGetArray(root, "Data")
                          ?? TryGetArray(root, "data");

                if (arr == null)
                {
                    return list;
                }

                foreach (var item in arr)
                {
                    if (item.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }

                    var o = item.GetObject();
                    var id = TryGetString(o, "Id") ?? TryGetString(o, "id") ?? TryGetString(o, "SnsObjId")
                             ?? TryGetString(o, "ObjectId") ?? Guid.NewGuid().ToString("N");
                    var content = TryGetString(o, "Content") ?? TryGetString(o, "content")
                                  ?? TryGetString(o, "ObjectDesc") ?? TryGetString(o, "Desc")
                                  ?? TryGetString(o, "description") ?? string.Empty;

                    // 嵌套 UserInfo / Contact
                    var user = TryGetObject(o, "UserInfo") ?? TryGetObject(o, "userInfo")
                               ?? TryGetObject(o, "Contact") ?? TryGetObject(o, "Author") ?? o;
                    var authorName = TryGetString(user, "NickName") ?? TryGetString(user, "nickName")
                                     ?? TryGetString(user, "Username") ?? TryGetString(o, "Username")
                                     ?? TryGetString(o, "UserName") ?? "好友";
                    var authorWx = TryGetString(user, "Username") ?? TryGetString(user, "UserName")
                                   ?? TryGetString(user, "wxid") ?? authorName;
                    var avatar = TryGetString(user, "BigHeadImgUrl") ?? TryGetString(user, "SmallHeadImgUrl")
                                 ?? TryGetString(user, "HeadImgUrl") ?? TryGetString(user, "headImgUrl");

                    long create = 0;
                    foreach (var k in new[] { "CreateTime", "createTime", "CreateTimeStamp" })
                    {
                        if (o.ContainsKey(k) && o[k].ValueType == JsonValueType.Number)
                        {
                            create = (long)o[k].GetNumber();
                            break;
                        }
                    }

                    var ts = create > 0
                        ? new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddSeconds(create).ToLocalTime()
                        : DateTimeOffset.Now;

                    var post = new Models.MomentPost
                    {
                        Id = id,
                        AuthorWxId = authorWx,
                        AuthorName = authorName,
                        AuthorAvatarUrl = avatar,
                        AuthorAccent = StableAccent(authorWx ?? authorName),
                        Content = StripXmlNoise(content),
                        CreateTime = ts,
                        TimeText = FormatRelative(ts),
                        Location = TryGetString(o, "Location") ?? TryGetString(o, "location") ?? string.Empty,
                    };

                    // 图片列表
                    var media = TryGetArray(o, "MediaList") ?? TryGetArray(o, "mediaList")
                                ?? TryGetArray(o, "ImageList") ?? TryGetArray(o, "imageList");
                    if (media != null)
                    {
                        foreach (var m in media)
                        {
                            if (m.ValueType == JsonValueType.String)
                            {
                                var u = m.GetString();
                                if (!string.IsNullOrEmpty(u))
                                {
                                    post.ImageUrls.Add(u);
                                }
                            }
                            else if (m.ValueType == JsonValueType.Object)
                            {
                                var mo = m.GetObject();
                                var u = TryGetString(mo, "Url") ?? TryGetString(mo, "url")
                                        ?? TryGetString(mo, "Thumb") ?? TryGetString(mo, "URL");
                                if (!string.IsNullOrEmpty(u))
                                {
                                    post.ImageUrls.Add(u);
                                }
                            }
                        }
                    }

                    // 点赞
                    var likes = TryGetArray(o, "LikeList") ?? TryGetArray(o, "likeList")
                                ?? TryGetArray(o, "LikeUserList");
                    if (likes != null)
                    {
                        foreach (var l in likes)
                        {
                            if (l.ValueType == JsonValueType.String)
                            {
                                post.LikeNames.Add(l.GetString());
                            }
                            else if (l.ValueType == JsonValueType.Object)
                            {
                                var lo = l.GetObject();
                                var n = TryGetString(lo, "NickName") ?? TryGetString(lo, "Username")
                                        ?? TryGetString(lo, "UserName");
                                if (!string.IsNullOrEmpty(n))
                                {
                                    post.LikeNames.Add(n);
                                }
                            }
                        }
                    }

                    // 评论
                    var comments = TryGetArray(o, "CommentList") ?? TryGetArray(o, "commentList")
                                   ?? TryGetArray(o, "Comments");
                    if (comments != null)
                    {
                        foreach (var c in comments)
                        {
                            if (c.ValueType != JsonValueType.Object)
                            {
                                continue;
                            }

                            var co = c.GetObject();
                            post.Comments.Add(new Models.MomentComment
                            {
                                AuthorName = TryGetString(co, "NickName") ?? TryGetString(co, "Username") ?? "",
                                Content = TryGetString(co, "Content") ?? TryGetString(co, "content") ?? ""
                            });
                        }
                    }

                    list.Add(post);
                }
            }
            catch
            {
                // ignore parse errors
            }

            return list;
        }

        private static string StripXmlNoise(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            // 粗暴去掉尖括号块，朋友圈 ObjectDesc 有时是 XML
            if (s.IndexOf('<') >= 0)
            {
                var sb = new System.Text.StringBuilder();
                bool inTag = false;
                foreach (var ch in s)
                {
                    if (ch == '<')
                    {
                        inTag = true;
                        continue;
                    }

                    if (ch == '>')
                    {
                        inTag = false;
                        continue;
                    }

                    if (!inTag)
                    {
                        sb.Append(ch);
                    }
                }

                return sb.ToString().Trim();
            }

            return s.Trim();
        }

        private static string FormatRelative(DateTimeOffset ts)
        {
            var span = DateTimeOffset.Now - ts;
            if (span.TotalMinutes < 1)
            {
                return "刚刚";
            }

            if (span.TotalHours < 1)
            {
                return ((int)span.TotalMinutes) + " 分钟前";
            }

            if (span.TotalHours < 24)
            {
                return ((int)span.TotalHours) + " 小时前";
            }

            if (span.TotalDays < 7)
            {
                return ((int)span.TotalDays) + " 天前";
            }

            return ts.ToString("M月d日");
        }

        public async Task<SyncMsgResult> GetSyncMsgAsync()
        {
            // swagger: POST /Msg/Sync, body: {Scene, Synckey}
            string[] paths =
            {
                "/Msg/Sync",
                "/Msg/StartAutoSync",
                "/message/HttpSyncMsg",
            };

            var body = new JsonObject
            {
                ["Scene"] = JsonValue.CreateNumberValue(0),
                ["Synckey"] = JsonValue.CreateStringValue(string.Empty),
            };

            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await PostJsonAsync(path, body, attachToken: true).ConfigureAwait(false);
                if (!last.Ok)
                {
                    last = await GetAsync(path, attachToken: true).ConfigureAwait(false);
                }

                if (!last.Ok)
                {
                    continue;
                }

                var sync = ParseSyncMsg(last);
                if (sync.Ok)
                {
                    return sync;
                }
            }

            return new SyncMsgResult
            {
                Ok = false,
                Message = last?.Message ?? "同步消息失败"
            };
        }

        public async Task<ApiCallResult> TestReachableAsync()
        {
            // 轻量探测：不依赖登录态的 swagger 或根路径 / 状态接口
            try
            {
                var uri = new Uri(_baseUrl + "/");
                using (var req = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    using (var resp = await _http.SendRequestAsync(req).AsTask().ConfigureAwait(false))
                    {
                        return new ApiCallResult
                        {
                            Ok = ((int)resp.StatusCode) < 500,
                            Code = (int)resp.StatusCode,
                            Message = resp.ReasonPhrase,
                            RawJson = string.Empty
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        // ------------------------------------------------------------------
        // HTTP 原语
        // ------------------------------------------------------------------

        private async Task<ApiCallResult> PostFirstOkAsync(JsonObject body, params string[] paths)
        {
            ApiCallResult last = null;
            foreach (var path in paths)
            {
                last = await PostJsonAsync(path, body, attachToken: true).ConfigureAwait(false);
                if (last.Ok)
                {
                    return last;
                }
            }

            return last ?? Fail("请求失败");
        }

        private sealed class CdnFields
        {
            public string FileId;
            public string AesKey;
            public string Url;
        }

        private static CdnFields ExtractCdnFields(ApiCallResult result)
        {
            var f = new CdnFields();
            if (result == null || string.IsNullOrEmpty(result.RawJson))
            {
                return f;
            }

            try
            {
                var root = JsonObject.Parse(result.RawJson);
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data") ?? root;
                f.FileId = TryGetString(data, "FileId") ?? TryGetString(data, "FileID") ?? TryGetString(data, "MediaId")
                           ?? TryGetString(data, "fileId") ?? TryGetString(data, "mediaId");
                f.AesKey = TryGetString(data, "AESKey") ?? TryGetString(data, "AesKey") ?? TryGetString(data, "aesKey");
                f.Url = TryGetString(data, "CDNURL") ?? TryGetString(data, "Url") ?? TryGetString(data, "url")
                        ?? TryGetString(data, "ImageUrl") ?? TryGetString(data, "BigUrl");
            }
            catch
            {
                // ignore
            }

            return f;
        }

        private async Task<ApiCallResult> GetAsync(string pathAndQuery, bool attachToken, bool isAdmin = false)
        {
            ApiCallResult last = null;
            foreach (var candidate in BuildPathCandidates(pathAndQuery))
            {
                var url = BuildUrl(candidate, attachToken);
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, new Uri(url)))
                    {
                        AttachAuth(req, attachToken, isAdmin);
                        using (var resp = await _http.SendRequestAsync(req).AsTask().ConfigureAwait(false))
                        {
                            var text = await resp.Content.ReadAsStringAsync().AsTask().ConfigureAwait(false);
                            var result = Interpret(resp, text);
                            if (result.Ok)
                            {
                                return result;
                            }

                            last = result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    last = Fail(ex.Message);
                }
            }

            return last ?? Fail("请求失败");
        }

        private async Task<ApiCallResult> PostJsonAsync(string pathAndQuery, JsonObject body, bool attachToken, bool isAdmin = false, bool stopOnTransportError = false)
        {
            ApiCallResult last = null;
            foreach (var candidate in BuildPathCandidates(pathAndQuery))
            {
                var url = BuildUrl(candidate, attachToken);
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Post, new Uri(url)))
                    {
                        AttachAuth(req, attachToken, isAdmin);
                        req.Content = new HttpStringContent(body?.Stringify() ?? "{}", Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                        using (var resp = await _http.SendRequestAsync(req).AsTask().ConfigureAwait(false))
                        {
                            var text = await resp.Content.ReadAsStringAsync().AsTask().ConfigureAwait(false);
                            var result = Interpret(resp, text);
                            if (result.Ok)
                            {
                                return result;
                            }

                            last = result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    last = TransportFail(ex.Message);
                    if (stopOnTransportError)
                    {
                        // 发送类调用：没拿到应答不代表没发出去，不能再换路径试。
                        return last;
                    }
                }
            }

            return last ?? Fail("请求失败");
        }

        /// <summary>
        /// WeChatPadProMAX v8 的基路径固定是 /api（swagger: http://host:8062/api）。
        /// 这里**不再**回退到根路径：回退对发送类接口是重复发送风险
        /// （第一次可能已经发出去了，只是应答被判成失败），对其余接口只是白白多一轮 404。
        /// pyweixin 网关走根路径。
        /// </summary>
        private IEnumerable<string> BuildPathCandidates(string pathAndQuery)
        {
            if (string.IsNullOrEmpty(pathAndQuery))
            {
                pathAndQuery = "/";
            }

            if (!pathAndQuery.StartsWith("/"))
            {
                pathAndQuery = "/" + pathAndQuery;
            }

            if (Backend != BackendKind.WeChatPadPro)
            {
                yield return pathAndQuery;
                yield break;
            }

            if (pathAndQuery.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                pathAndQuery.Equals("/api", StringComparison.OrdinalIgnoreCase))
            {
                yield return pathAndQuery;
                yield break;
            }

            yield return "/api" + pathAndQuery;
        }

        private string BuildUrl(string pathAndQuery, bool attachToken)
        {
            if (string.IsNullOrWhiteSpace(_baseUrl))
            {
                throw new InvalidOperationException("BaseUrl 未配置");
            }

            if (string.IsNullOrEmpty(pathAndQuery))
            {
                pathAndQuery = "/";
            }

            if (!pathAndQuery.StartsWith("/"))
            {
                pathAndQuery = "/" + pathAndQuery;
            }

            var url = _baseUrl + pathAndQuery;

            // Token 一律只走 X-Access-Token 请求头，绝不拼进 query string：
            // query 会出现在服务端的请求行日志里（pyweixin 网关就是直接 print 请求行），
            // 也会被任何反向代理/抓包工具原样记录，等于每发一条消息就泄一次 Token。
            // 只有 WeChatPadPro 的老部署确实需要 ?key=，由 AllowTokenInQuery 单独开。
            if (attachToken && AllowTokenInQuery && !string.IsNullOrEmpty(_token))
            {
                url += (url.IndexOf('?') >= 0 ? "&" : "?") + "key=" + Uri.EscapeDataString(_token);
            }

            return url;
        }

        private static ApiCallResult Interpret(HttpResponseMessage resp, string text)
        {
            var result = new ApiCallResult
            {
                Code = (int)resp.StatusCode,
                RawJson = text ?? string.Empty,
                Message = resp.ReasonPhrase
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                result.Ok = resp.IsSuccessStatusCode;
                return result;
            }

            try
            {
                var root = JsonObject.Parse(text);
                var code = TryGetInt(root, "Code") ?? TryGetInt(root, "code") ?? TryGetInt(root, "ret") ?? TryGetInt(root, "Ret");
                var msg = TryGetString(root, "Text") ?? TryGetString(root, "Message") ?? TryGetString(root, "msg") ?? TryGetString(root, "message");

                // swagger ResponseResult 有 Success 布尔字段，优先用它
                var success = TryGetBool(root, "Success") ?? TryGetBool(root, "success");

                if (success.HasValue)
                {
                    result.Ok = success.Value;
                }
                else if (code.HasValue)
                {
                    // 实测：200=成功，-2=token 不存在，300=需重新登录
                    result.Ok = code.Value == 200 || code.Value == 0 || code.Value == 1;
                    result.Code = code.Value;
                }
                else
                {
                    result.Ok = resp.IsSuccessStatusCode;
                }

                if (code.HasValue)
                {
                    result.Code = code.Value;
                }

                if (!string.IsNullOrEmpty(msg))
                {
                    result.Message = msg;
                }

                // Data 原样保留
                if (root.ContainsKey("Data"))
                {
                    result.DataJson = root["Data"].Stringify();
                }
                else if (root.ContainsKey("data"))
                {
                    result.DataJson = root["data"].Stringify();
                }

                // 若业务 code 表示失败，但 HTTP 200
                if (code.HasValue && !(code.Value == 200 || code.Value == 0 || code.Value == 1))
                {
                    result.Ok = false;
                }
            }
            catch
            {
                result.Ok = resp.IsSuccessStatusCode;
            }

            return result;
        }

        private static long UnixNow()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
            {
                return s ?? string.Empty;
            }

            return s.Substring(0, max) + "…";
        }

        /// <summary>XML 转义（WP8.1 WinRT 没有 System.Security.SecurityElement）</summary>
        private static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&apos;"); break;
                    default: sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        private static string StableAccent(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return "#576B95";
            }

            string[] palette =
            {
                "#F1592A", "#576B95", "#9C6ADE", "#10AEFF", "#FA9D3B",
                "#07C160", "#E64340", "#6467F0", "#3D7EFF", "#00B578"
            };
            unchecked
            {
                int h = 0;
                foreach (var ch in key)
                {
                    h = (h * 31) + ch;
                }

                if (h < 0)
                {
                    h = -h;
                }

                return palette[h % palette.Length];
            }
        }

        private static ApiCallResult Fail(string message) => new ApiCallResult
        {
            Ok = false,
            Code = -1,
            Message = message ?? "error"
        };

        /// <summary>没有拿到任何服务端应答的失败（连不上 / 超时）。</summary>
        private static ApiCallResult TransportFail(string message) => new ApiCallResult
        {
            Ok = false,
            Code = -1,
            Message = message ?? "error",
            TransportError = true
        };

        // ------------------------------------------------------------------
        // 解析
        // ------------------------------------------------------------------

        private static QrLoginResult ParseQr(ApiCallResult call)
        {
            var qr = new QrLoginResult { Ok = call.Ok, Message = call.Message };
            if (string.IsNullOrEmpty(call.RawJson))
            {
                return qr;
            }

            try
            {
                var root = JsonObject.Parse(call.RawJson);
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data") ?? root;

                // 本机 Docker 返回 uuid + qrCodeBase64 + QrCodeUrl + QrLink
                qr.Uuid = FirstString(data, "uuid", "Uuid", "QRCodeKey", "qrCodeKey", "Id", "id");
                if (string.IsNullOrEmpty(qr.Uuid))
                {
                    qr.Uuid = FirstString(data, "Key", "key"); // 部分部署 Key 即会话
                }

                qr.QrContent = FirstString(data, "QrLink", "qrLink", "QrCode", "qrCode", "Content", "content");
                qr.QrUrl = FirstString(data, "QrCodeUrl", "qrCodeUrl", "QrUrl", "qrUrl", "QrImgUrl", "ImgUrl", "ImageUrl");
                qr.QrBase64 = FirstString(data, "qrCodeBase64", "QrCodeBase64", "QrBase64", "qrBase64", "Base64", "base64");

                // 有的把整段 base64 放在 Data 字符串
                if (string.IsNullOrEmpty(qr.QrBase64))
                {
                    var dataStr = TryGetString(root, "Data") ?? TryGetString(root, "data");
                    if (!string.IsNullOrEmpty(dataStr) && dataStr.Length > 100)
                    {
                        qr.QrBase64 = dataStr;
                    }
                }

                // 外部二维码服务拼 URL（README 里出现过 api.pwmqr.com）
                if (string.IsNullOrEmpty(qr.QrUrl) && !string.IsNullOrEmpty(qr.QrContent) && qr.QrContent.StartsWith("http", StringComparison.OrdinalIgnoreCase) == false)
                {
                    // QrContent 可能是 weixin:// 或 http://weixin.qq.com/x/...
                    qr.QrUrl = "https://api.pwmqr.com/qrcode/create/?url=" + Uri.EscapeDataString(qr.QrContent);
                }
                else if (string.IsNullOrEmpty(qr.QrUrl) && !string.IsNullOrEmpty(qr.QrContent) && qr.QrContent.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // 有的 QrContent 本身就是可打开的二维码图片/中间页
                    if (qr.QrContent.IndexOf("qrcode", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        qr.QrContent.IndexOf("pwmqr", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        qr.QrUrl = qr.QrContent;
                    }
                    else
                    {
                        qr.QrUrl = "https://api.pwmqr.com/qrcode/create/?url=" + Uri.EscapeDataString(qr.QrContent);
                    }
                }

                qr.Ok = call.Ok || !string.IsNullOrEmpty(qr.Uuid) || !string.IsNullOrEmpty(qr.QrBase64) || !string.IsNullOrEmpty(qr.QrUrl);
            }
            catch (Exception ex)
            {
                qr.Ok = false;
                qr.Message = ex.Message;
            }

            return qr;
        }

        private static LoginStatusResult ParseLoginStatus(ApiCallResult call)
        {
            var r = new LoginStatusResult
            {
                Ok = call.Ok,
                Message = call.Message,
                RawJson = call.RawJson,
                State = QrScanState.Unknown
            };

            if (string.IsNullOrEmpty(call.RawJson))
            {
                return r;
            }

            try
            {
                var root = JsonObject.Parse(call.RawJson);
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data") ?? root;

                r.WxId = FirstString(data, "wxid", "Wxid", "WxId", "userName", "UserName", "username");
                r.Nickname = FirstString(data, "nick_name", "NickName", "nickName", "Nickname", "nickname", "Name");
                r.HeadImgUrl = FirstString(data, "head_img_url", "HeadImgUrl", "headImgUrl", "SmallHeadImgUrl", "BigHeadImgUrl");

                var stateStr = FirstString(data, "msg", "Msg", "State", "state", "Status", "status", "LoginState", "loginState", "Text");
                var stateNum = TryGetInt(data, "state") ?? TryGetInt(data, "State") ?? TryGetInt(data, "Status") ?? TryGetInt(data, "status") ?? TryGetInt(data, "loginState") ?? TryGetInt(data, "ret");

                // 本机实测：state==2 且带 wxid = 扫码确认成功
                if (!string.IsNullOrEmpty(r.WxId) || (stateNum.HasValue && stateNum.Value == 2 && !string.IsNullOrEmpty(r.Nickname)))
                {
                    r.State = QrScanState.Confirmed;
                    r.Ok = true;
                }
                else if (stateNum.HasValue)
                {
                    // 常见：0 等待 / 1 已扫 / 2 确认 / -1 过期 等，不完全统一，下面再靠字符串兜底
                    switch (stateNum.Value)
                    {
                        case 0: r.State = QrScanState.Waiting; break;
                        case 1: r.State = QrScanState.Scanned; break;
                        case 2: r.State = QrScanState.Confirmed; break;
                        case 3: r.State = QrScanState.Confirmed; break;
                        case -1: r.State = QrScanState.Expired; break;
                        case 4: r.State = QrScanState.Expired; break;
                        default: r.State = QrScanState.Unknown; break;
                    }
                }

                if (!string.IsNullOrEmpty(stateStr))
                {
                    var s = stateStr.ToLowerInvariant();
                    if (s.Contains("expire") || s.Contains("过期") || s.Contains("超时"))
                    {
                        r.State = QrScanState.Expired;
                    }
                    else if (s.Contains("confirm") || s.Contains("success") || s.Contains("成功") || s.Contains("已登录") || s.Contains("login_success"))
                    {
                        r.State = QrScanState.Confirmed;
                    }
                    else if (s.Contains("scan") || s.Contains("已扫") || s.Contains("扫码"))
                    {
                        r.State = QrScanState.Scanned;
                    }
                    else if (s.Contains("wait") || s.Contains("等待") || s.Contains("未扫"))
                    {
                        r.State = QrScanState.Waiting;
                    }
                }

                if (r.State == QrScanState.Confirmed)
                {
                    r.Ok = true;
                }
            }
            catch (Exception ex)
            {
                r.Ok = false;
                r.Message = ex.Message;
            }

            return r;
        }

        private static FriendListResult ParseFriendList(ApiCallResult call)
        {
            var result = new FriendListResult { Ok = call.Ok, Message = call.Message };
            if (string.IsNullOrEmpty(call.RawJson))
            {
                return result;
            }

            try
            {
                var root = JsonObject.Parse(call.RawJson);
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data");

                JsonArray arr = null;
                if (data != null)
                {
                    arr = TryGetArray(data, "FriendList")
                        ?? TryGetArray(data, "friendList")
                        ?? TryGetArray(data, "ContactList")
                        ?? TryGetArray(data, "contactList")
                        ?? TryGetArray(data, "List")
                        ?? TryGetArray(data, "list")
                        ?? TryGetArray(data, "friends");
                }

                arr = arr ?? TryGetArray(root, "Data") ?? TryGetArray(root, "data");

                if (arr == null)
                {
                    // 有时 Data 直接是对象包一层
                    result.Ok = call.Ok;
                    return result;
                }

                foreach (var item in arr)
                {
                    if (item.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }

                    var o = item.GetObject();
                    var userName = FirstString(o, "UserName", "userName", "Username", "username", "Wxid", "wxid");
                    if (string.IsNullOrEmpty(userName))
                    {
                        continue;
                    }

                    // 过滤系统号噪声可在上层做
                    var dto = new PadContactDto
                    {
                        UserName = userName,
                        NickName = FirstString(o, "NickName", "nickName", "Nickname", "nickname") ?? userName,
                        Remark = FirstString(o, "Remark", "remark", "RemarkName", "remarkName"),
                        Alias = FirstString(o, "Alias", "alias"),
                        BigHeadImgUrl = FirstString(o, "BigHeadImgUrl", "bigHeadImgUrl", "HeadImgUrl"),
                        SmallHeadImgUrl = FirstString(o, "SmallHeadImgUrl", "smallHeadImgUrl"),
                        Province = FirstString(o, "Province", "province"),
                        City = FirstString(o, "City", "city"),
                        Signature = FirstString(o, "Signature", "signature"),
                        IsChatroom = userName.EndsWith("@chatroom", StringComparison.OrdinalIgnoreCase)
                    };
                    result.Contacts.Add(dto);
                }

                result.Ok = true;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Message = ex.Message;
            }

            return result;
        }

        private static SyncMsgResult ParseSyncMsg(ApiCallResult call)
        {
            var result = new SyncMsgResult { Ok = call.Ok, Message = call.Message };
            if (string.IsNullOrEmpty(call.RawJson))
            {
                return result;
            }

            try
            {
                var root = JsonObject.Parse(call.RawJson);
                var data = TryGetObject(root, "Data") ?? TryGetObject(root, "data");
                JsonArray arr = null;
                if (data != null)
                {
                    arr = TryGetArray(data, "AddMsgs")
                        ?? TryGetArray(data, "addMsgs")
                        ?? TryGetArray(data, "List")
                        ?? TryGetArray(data, "list")
                        ?? TryGetArray(data, "MsgList")
                        ?? TryGetArray(data, "messages")
                        ?? TryGetArray(data, "Messages");
                }

                arr = arr ?? TryGetArray(root, "Data") ?? TryGetArray(root, "data");
                if (arr == null)
                {
                    result.Ok = call.Ok;
                    return result;
                }

                foreach (var item in arr)
                {
                    if (item.ValueType != JsonValueType.Object)
                    {
                        continue;
                    }

                    var o = item.GetObject();
                    // 嵌套 FromUserName.String 风格（protobuf JSON）
                    var from = NestedString(o, "FromUserName") ?? FirstString(o, "FromUserName", "fromUserName", "from");
                    var to = NestedString(o, "ToUserName") ?? FirstString(o, "ToUserName", "toUserName", "to");
                    var content = NestedString(o, "Content") ?? FirstString(o, "Content", "content", "Text", "text");
                    var msgId = FirstString(o, "MsgId", "msgId", "NewMsgId", "newMsgId", "id") ?? Guid.NewGuid().ToString("N");
                    var msgType = TryGetInt(o, "MsgType") ?? TryGetInt(o, "msgType") ?? 1;
                    var createTime = TryGetLong(o, "CreateTime") ?? TryGetLong(o, "createTime") ?? 0L;

                    result.Messages.Add(new PadMessageDto
                    {
                        MsgId = msgId,
                        NewMsgId = FirstString(o, "NewMsgId", "newMsgId"),
                        FromUserName = from,
                        ToUserName = to,
                        Content = content,
                        MsgType = msgType,
                        CreateTime = createTime,
                        PushContent = FirstString(o, "PushContent", "pushContent")
                    });
                }

                result.Ok = true;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Message = ex.Message;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // JSON helpers
        // ------------------------------------------------------------------

        private static string NestedString(JsonObject o, string key)
        {
            if (o == null || !o.ContainsKey(key))
            {
                return null;
            }

            var v = o[key];
            if (v.ValueType == JsonValueType.String)
            {
                return v.GetString();
            }

            if (v.ValueType == JsonValueType.Object)
            {
                var inner = v.GetObject();
                return TryGetString(inner, "String") ?? TryGetString(inner, "string") ?? TryGetString(inner, "str");
            }

            return null;
        }

        private static string FirstString(JsonObject o, params string[] keys)
        {
            if (o == null)
            {
                return null;
            }

            foreach (var k in keys)
            {
                var s = TryGetString(o, k);
                if (!string.IsNullOrEmpty(s))
                {
                    return s;
                }
            }

            return null;
        }

        private static string TryGetString(JsonObject o, string key)
        {
            if (o == null || string.IsNullOrEmpty(key) || !o.ContainsKey(key))
            {
                return null;
            }

            var v = o[key];
            if (v.ValueType == JsonValueType.String)
            {
                return v.GetString();
            }

            if (v.ValueType == JsonValueType.Number)
            {
                return v.GetNumber().ToString();
            }

            if (v.ValueType == JsonValueType.Boolean)
            {
                return v.GetBoolean() ? "true" : "false";
            }

            return null;
        }

        private static int? TryGetInt(JsonObject o, string key)
        {
            if (o == null || !o.ContainsKey(key))
            {
                return null;
            }

            var v = o[key];
            if (v.ValueType == JsonValueType.Number)
            {
                return (int)v.GetNumber();
            }

            if (v.ValueType == JsonValueType.String)
            {
                int n;
                if (int.TryParse(v.GetString(), out n))
                {
                    return n;
                }
            }

            return null;
        }

        private static long? TryGetLong(JsonObject o, string key)
        {
            if (o == null || !o.ContainsKey(key))
            {
                return null;
            }

            var v = o[key];
            if (v.ValueType == JsonValueType.Number)
            {
                return (long)v.GetNumber();
            }

            if (v.ValueType == JsonValueType.String)
            {
                long n;
                if (long.TryParse(v.GetString(), out n))
                {
                    return n;
                }
            }

            return null;
        }

        private static bool? TryGetBool(JsonObject o, string key)
        {
            if (o == null || !o.ContainsKey(key))
            {
                return null;
            }

            var v = o[key];
            if (v.ValueType == JsonValueType.Boolean)
            {
                return v.GetBoolean();
            }

            if (v.ValueType == JsonValueType.String)
            {
                var s = v.GetString();
                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return null;
        }

        private static JsonObject TryGetObject(JsonObject o, string key)
        {
            if (o == null || !o.ContainsKey(key))
            {
                return null;
            }

            var v = o[key];
            return v.ValueType == JsonValueType.Object ? v.GetObject() : null;
        }

        private static JsonArray TryGetArray(JsonObject o, string key)
        {
            if (o == null || !o.ContainsKey(key))
            {
                return null;
            }

            var v = o[key];
            return v.ValueType == JsonValueType.Array ? v.GetArray() : null;
        }

        /// <summary>把 base64 图片写到临时流，供 Image.Source 使用。</summary>
        public static async Task<IRandomAccessStream> Base64ToStreamAsync(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64))
            {
                return null;
            }

            var raw = base64.Trim();
            var comma = raw.IndexOf(',');
            if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            {
                raw = raw.Substring(comma + 1);
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(raw);
            }
            catch
            {
                return null;
            }

            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);
            return stream;
        }
    }
}

# WeChatPadProMAX v8 API 接口手册

> 由 `swagger/swagger.json`（构建 `v8_m4.1.12.29_p8.0.75.53 (build 20260809_030557)`）生成，生成日期 2026-08-16。
> 服务默认监听 `0.0.0.0:8062`，接口基路径 `/api`；本文所有路径均相对 `http://<host>:8062/api`。

## 目录

- [1. 快速开始](#1-快速开始)
- [2. 通用约定](#2-通用约定)
  - [2.1 请求头](#21-请求头)
  - [2.2 响应包装](#22-响应包装)
  - [2.3 错误码](#23-错误码)
- [3. 实时通道](#3-实时通道)
- [4. 接口速查表](#4-接口速查表)
- [5. 分模块接口详情](#5-分模块接口详情)
- [附录 A：数据模型索引](#附录-a数据模型索引)

---

## 1. 快速开始

### 1.1 服务配置

```ini
# conf/app.conf 关键项
httpaddr = "0.0.0.0"
httpport = 8062
user_token_key = ""          # 个人中心生成的 TokenKey，必填
redislink = 127.0.0.1:6379  # Redis 数据存储
websocketport = 8089        # WebSocket 推送端口
```

### 1.2 鉴权三件套

| 凭证 | 获取 | 传递方式 | 用途 |
|---|---|---|---|
| TokenKey | 个人中心生成，填到 `conf/app.conf` | 服务端配置 | 服务启动/云端校验凭证 |
| Access Token | `POST /Admin/GenAuthKey` 生成（X-Admin-Token 调） | 请求头 `X-Access-Token` | 业务接口鉴权，每个登录账号一个 |
| Admin Token | 管理员后台生成 | 请求头 `X-Admin-Token` | 仅 `/Admin/*`、`/User/GetAllOnline` 等管理接口 |

### 1.3 最小登录流程

```bash
# 1) 生成业务鉴权码（管理员接口）
curl -X POST 'http://127.0.0.1:8062/api/Admin/GenAuthKey' \
     -H 'X-Admin-Token: <admin_token>' \
     -H 'Content-Type: application/json' \
     -d '{"count":1,"days":365,"remark":"my-account"}'

# 2) 获取 Mac 登录二维码，响应 Data 里拿 uuid/deviceID
curl -X POST 'http://127.0.0.1:8062/api/Login/GetQRMac' \
     -H 'X-Access-Token: <access_token>' \
     -H 'Content-Type: application/json' \
     -d '{"DeviceName":"my-pad","oversea":false}'

# 3) 轮询扫码结果
curl 'http://127.0.0.1:8062/api/Login/CheckMacQR?uuid=<uuid>&deviceID=<device_id>' \
     -H 'X-Access-Token: <access_token>'

# 4) 初始化会话（可选 MaxSynckey/CurrentSynckey 二次同步）
curl -X POST 'http://127.0.0.1:8062/api/Login/Newinit' \
     -H 'X-Access-Token: <access_token>'

# 5) 发送文本
curl -X POST 'http://127.0.0.1:8062/api/Msg/SendTxt' \
     -H 'X-Access-Token: <access_token>' \
     -H 'Content-Type: application/json' \
     -d '{"ToWxid":"wxid_xxx","Content":"hello","Type":0,"At":""}'
```

其他登录形态：`/Login/GetQR`（iPad）、`GetQRx`（iPad 绕过验证码）、`GetQRPadx`（Pad）、`GetQRWin`/`GetQRWinUwp`（Windows）、`GetQRMac`（Mac）、`GetQRWatch`（Car）、`HarmonyLoginApi`（鸿蒙）、`62data`/`A16Data`（账密登录）、`ExtDeviceLoginConfirmGet`（新设备确认）。

---

## 2. 通用约定

### 2.1 请求头

| 请求头 | 说明 |
|---|---|
| `X-Access-Token` | 账号令牌；**除 Admin 外所有业务接口**都校验，可在 Swagger 页顶部统一设置 |
| `X-Admin-Token` | 管理员凭证；仅管理接口使用（`/Admin/*`、`/User/GetAllOnline`） |
| `X-QR-Check-Token` | 二维码检测令牌；仅 `/Login/CheckQR` 推荐使用（旧客户端用 query `check_token` 兼容） |
| `Content-Type` | 请求体一律 `application/json` |
| `X-Request-ID` | 响应头，请求链路标识，排障用 |

### 2.2 响应包装

绝大多数接口返回 `models.ResponseResult`：

```json
{
  "Code": 0,
  "CodeValue": "",
  "Data": { ... },       // 业务数据，结构随接口而定
  "Data62": "",
  "Debug": "",
  "ID": 0,
  "Message": "",
  "Success": true,
  "request_id": "..."
}
```

判断成功以 `Success == true`（或 `Code == 0`）为准；`Data` 具体结构见各接口说明。登录类接口另有 `models.ResponseResult2`（多一个 `DeviceId` 字段）。

### 2.3 错误码

| HTTP | 含义 |
|---|---|
| 400 | 请求参数错误 |
| 401 | 凭证无效或过期 |
| 403 | 凭证已禁用或权限不足 |
| 409 | 账号、会话存在状态冲突 |
| 429 | 请求频率超限 |
| 500 | 服务内部异常 |
| 502 | 上游协议或授权服务异常 |

错误响应体也是 `models.ResponseResult`，`Message` 携带具体原因。

---

## 3. 实时通道

消息/事件回推有三种通道，均由服务端 `dispatcher` 事件总线分发：

| 通道 | 地址/配置 | 说明 |
|---|---|---|
| WebSocket | `ws://<host>:8089/ws/sync`、`/ws/status`、`/ws/health` | 长连接实时消息/状态；`/ws/sync` 为消息同步主通道 |
| Webhook | `POST /api/Webhook/Set` 配置（url/secret/messageTypes/重试） | 服务端主动 POST 事件到你的回调 URL，HMAC 签名防伪造，失败重试 |
| RabbitMQ | `conf/app.conf` 开启 `push_rabbitmq` | 高吞吐/多进程消费场景 |

Webhook 配置模型 `webhook.WebhookConfig`：`url`（回调地址）、`secret`（签名密钥）、`messageTypes`（订阅的消息类型）、`includeSelfMessage`（是否包含自己发的）、`retryCount`（重试次数）、`timeout`、`enabled`。

---

## 4. 接口速查表

| 模块 | 接口数 |
|---|---|
| Login | 38 |
| Group | 23 |
| Search | 23 |
| Wxapp | 20 |
| Msg | 18 |
| User | 18 |
| Finder | 15 |
| Tools | 15 |
| Friend | 12 |
| OfficialAccounts | 12 |
| TenPay | 12 |
| FriendCircle | 11 |
| Webhook | 6 |
| Label | 5 |
| Favor | 4 |
| Admin | 3 |
| QWContact | 3 |
| Voice | 3 |
| SayHello | 2 |
| Translate | 2 |
| Customized | 1 |

### 全部接口一览

| 方法 | 路径 | 摘要 |
|---|---|---|
| POST | `/Admin/DelayAuthKey` | 延期授权码a |
| POST | `/Admin/DeleteAuthKey` | 删除授权码 |
| POST | `/Admin/GenAuthKey` | 生成授权码 |
| POST | `/Customized/WXCTDUniftyAuthBatch` | 批量开小程序 |
| POST | `/Favor/Del` | 删除收藏 |
| POST | `/Favor/GetFavInfo` | 获取搜藏信息 |
| POST | `/Favor/GetFavItem` | 读取收藏内容 |
| POST | `/Favor/Sync` | 同步收藏 |
| POST | `/Finder/Comment` | 评论 |
| POST | `/Finder/Decrypt` | 评论 |
| POST | `/Finder/FinderGetMsgSessionId` | 获取Finder私信会话ID |
| POST | `/Finder/FinderLiveDetail` | 直播详情 |
| POST | `/Finder/FinderSearchList` | 搜索列表 |
| POST | `/Finder/FinderSendText` | 发送私信文字 |
| POST | `/Finder/Findergettopiclist` | 主题列表 |
| POST | `/Finder/Follow` | 关注 |
| POST | `/Finder/GetCommentDetail` | 查看指定内容 |
| POST | `/Finder/GetCommentList` | 评论列表/详情（支持RootCommentId翻页） |
| POST | `/Finder/GetRecommend` | 推荐 |
| POST | `/Finder/Like` | 点赞 |
| POST | `/Finder/Search` | 用户搜索 |
| POST | `/Finder/TargetUserPage` | 查看指定人首页 |
| POST | `/Finder/UserPrepare` | 用户中心 |
| POST | `/Friend/Blacklist` | 添加/移除黑名单 |
| POST | `/Friend/Delete` | 删除好友 |
| POST | `/Friend/GetContractDetail` | 获取通讯录好友详情 |
| POST | `/Friend/GetContractList` | 获取通讯录好友 |
| POST | `/Friend/GetFriendstate` | 查询好友状态 |
| POST | `/Friend/GetMFriend` | 获取手机通讯录 |
| POST | `/Friend/LbsFind` | 附近人 |
| POST | `/Friend/PassVerify` | 通过好友请求 |
| POST | `/Friend/Search` | 搜索联系人 |
| POST | `/Friend/SendRequest` | 添加联系人(发送好友请求) |
| POST | `/Friend/SetRemarks` | 设置好友备注 |
| POST | `/Friend/Upload` | 上传通讯录 |
| POST | `/FriendCircle/Comment` | 朋友圈点赞/评论 |
| POST | `/FriendCircle/GetCommnet` | 获取评论内容 |
| POST | `/FriendCircle/GetDetail` | 获取特定人朋友圈 |
| POST | `/FriendCircle/GetIdDetail` | 获取特定ID详情内容 |
| POST | `/FriendCircle/GetList` | 朋友圈首页列表 |
| POST | `/FriendCircle/Messages` | 发布朋友圈 |
| POST | `/FriendCircle/MmSnsSync` | 查询正在 评论转发的ID |
| POST | `/FriendCircle/Operation` | 朋友圈操作 |
| POST | `/FriendCircle/PrivacySettings` | 朋友圈权限设置 |
| POST | `/FriendCircle/PushCommnet` | 启动评论检查任务并转发评论 |
| POST | `/FriendCircle/Upload` | 朋友圈下载CDN视频 |
| POST | `/Group/AddChatRoomMember` | 增加群成员(40人以内) |
| POST | `/Group/ConsentToJoin` | 同意进入群聊 |
| POST | `/Group/CreateChatRoom` | 创建群聊 |
| POST | `/Group/DelChatRoomMember` | 删除群成员 |
| POST | `/Group/FacingCreateChatRoom` | 创建群聊 |
| POST | `/Group/GetChatRoomInfo` | 获取群详情(不带公告内容) |
| POST | `/Group/GetChatRoomInfoDetail` | 获取群信息(带公告内容) |
| POST | `/Group/GetChatRoomMemberDetail` | 获取群成员详情 |
| POST | `/Group/GetQRCode` | 获取群二维码 |
| GET | `/Group/GroupList` | 获取群列表（兼容路由） |
| POST | `/Group/InviteChatRoomMember` | 邀请群成员(40人以上) |
| GET | `/Group/List` | 获取群列表（业务路由） |
| POST | `/Group/MoveContractList` | 保存到通讯录 |
| POST | `/Group/OperateChatRoomAdmin` | 群管理操作(添加、删除、转让) |
| POST | `/Group/Quit` | 退出群聊 |
| POST | `/Group/ScanIntoGroup` | 扫码进群 |
| POST | `/Group/ScanIntoGroupEnterprise` | 扫码进群(企业) |
| POST | `/Group/SendPat` | 群拍一拍功能 |
| POST | `/Group/SendTransferGroupOwner` | 转让群 |
| POST | `/Group/SetChatRoomAnnouncement` | 设置群公告 |
| POST | `/Group/SetChatRoomName` | 设置群名称 |
| POST | `/Group/SetChatRoomRemarks` | 设置群备注(仅自己可见) |
| POST | `/Group/SetChatroomAccessVerify` | 设置群聊邀请开关 |
| POST | `/Label/Add` | 添加标签 |
| POST | `/Label/Delete` | 删除标签 |
| POST | `/Label/GetList` | 获取标签列表 |
| POST | `/Label/UpdateList` | 更新标签列表 |
| POST | `/Label/UpdateName` | 修改标签 |
| POST | `/Login/62data` | 62登陆(账号或密码) |
| POST | `/Login/62dataQRCodeApply` | 62登陆(账号或密码), 并申请使用二维码验证 |
| POST | `/Login/62dataSMSAgain` | 62登陆(账号或密码), 重发验证码 |
| POST | `/Login/62dataSMSApply` | 62登陆(账号或密码), 并申请使用SMS验证 |
| POST | `/Login/62dataSMSVerify` | 62登陆(账号或密码), 二维码验证校验 |
| POST | `/Login/A16Data` | A16登陆(账号或密码) - android == 8.0.50 |
| POST | `/Login/A16Data848` | A16登陆(账号或密码) - android == 新版云函数 |
| POST | `/Login/AutoHeartBeat` | 开启自动心跳, 自动二次登录 |
| POST | `/Login/Awaken` | 唤醒登陆(只限扫码登录) |
| POST | `/Login/CheckMacQR` | 检测Mac二维码 |
| POST | `/Login/CheckQR` | 检测二维码 |
| POST | `/Login/ExtDeviceLoginConfirmGet` | 新设备扫码登录 |
| POST | `/Login/ExtDeviceLoginConfirmOk` | 新设备扫码确认登录 |
| POST | `/Login/Get62Data` | 获取62数据 |
| POST | `/Login/GetA16Data` | 获取A16数据 |
| POST | `/Login/GetCacheInfo` | 获取登陆缓存信息 |
| POST | `/Login/GetLoginQRCode862` | 获取二维码(iPad 8.0.62 专用) |
| POST | `/Login/GetQR` | 获取二维码(iPad) |
| POST | `/Login/GetQRMac` | 获取二维码(Mac) |
| POST | `/Login/GetQRMac_oversea` | 获取二维码(Mac，海外) |
| POST | `/Login/GetQRPad` | 获取二维码(安卓Pad-ppmt专用) |
| POST | `/Login/GetQRPadx` | 获取二维码(安卓Pad-绕过验证码) |
| POST | `/Login/GetQRWatch` | 获取二维码(Car) |
| POST | `/Login/GetQRWin` | 获取二维码(Windows) |
| POST | `/Login/GetQRWinUnified` | 获取二维码(WinUnified-统一PC版) |
| POST | `/Login/GetQRWinUwp` | 获取二维码(WindowsUwp-绕过验证码) |
| POST | `/Login/GetQR_oversea` | 获取二维码(iPad，海外) |
| POST | `/Login/GetQRx` | 获取二维码(iPad-绕过验证码) |
| POST | `/Login/GetQRx_oversea` | 获取二维码(iPad-绕过验证码，海外) |
| POST | `/Login/HarmonyLoginApi` | 获取二维码(鸿蒙平板) |
| POST | `/Login/HeartBeat` | 心跳包 |
| GET | `/Login/HeartBeatLogs` | 获取心跳日志 |
| POST | `/Login/HeartBeatLong` | 长连接心跳包跳包 |
| POST | `/Login/LogOut` | 退出登录 |
| GET | `/Login/LongLinkStatus` | 查看当前账号长连接运行状态 |
| POST | `/Login/Newinit` | 初始化 |
| POST | `/Login/TwiceAutoAuth` | 二次登陆 |
| POST | `/Login/YPayVerificationcode` | 提交登录验证码 |
| POST | `/Msg/Quote` | 发送引用回复消息 |
| POST | `/Msg/Revoke` | 撤回消息 |
| POST | `/Msg/SendApp` | 群发消息 |
| POST | `/Msg/SendCDNFile` | 发送文件(转发,并非上传) |
| POST | `/Msg/SendCDNImg` | 发送Cdn图片(转发图片) |
| POST | `/Msg/SendCDNVideo` | 发送Cdn视频(转发视频) |
| POST | `/Msg/SendEmoji` | 发送Emoji |
| POST | `/Msg/SendTxt` | 发送文本消息 |
| POST | `/Msg/SendVideo` | 发送视频 |
| POST | `/Msg/SendVoice` | 发送语音 |
| POST | `/Msg/SendXCX` | 发送小程序消息 |
| POST | `/Msg/ShareCard` | 分享名片 |
| POST | `/Msg/ShareLink` | 发送分享链接消息 |
| POST | `/Msg/ShareLocation` | 分享位置 |
| POST | `/Msg/ShareVideo` | 发送分享视频消息 |
| POST | `/Msg/StartAutoSync` | 启动自动同步 |
| POST | `/Msg/Sync` | 同步消息 |
| POST | `/Msg/UploadImg` | 发送图片 |
| POST | `/OfficialAccounts/AuthMpLogin` | 授权公众号登录 |
| POST | `/OfficialAccounts/Follow` | 关注 |
| POST | `/OfficialAccounts/GetAppMsgExt` | 阅读文章,返回 分享、看一看、阅读数据 |
| POST | `/OfficialAccounts/GetAppMsgExtLike` | 点赞文章,返回 分享、看一看、阅读数据 |
| POST | `/OfficialAccounts/GetMpHistory` | 获取公众号历史消息 |
| POST | `/OfficialAccounts/GetMpHistoryMessage` | 获取公众号历史消息HTML |
| POST | `/OfficialAccounts/JSAPIPreVerify` | JSAPIPreVerify |
| POST | `/OfficialAccounts/MpGetA8Key` | MpGetA8Key(获取文章key和uin) |
| POST | `/OfficialAccounts/OauthAuthorize` | OauthAuthorize |
| POST | `/OfficialAccounts/QRConnectAuthorize` | 二维码授权请求 |
| POST | `/OfficialAccounts/QRConnectAuthorizeConfirm` | 二维码授权确认 |
| POST | `/OfficialAccounts/Quit` | 取消关注 |
| POST | `/QWContact/QWApplyAddContact` | QWApplyAddContact |
| POST | `/QWContact/QWContact/QWAddContact` | QWAddContact |
| POST | `/QWContact/SearchQWContact` | SearchQWContact |
| POST | `/SayHello/Modelv1` | 模式1-扫码 |
| POST | `/SayHello/Modelv2` | 模式3-v3\v4打招呼 |
| POST | `/Search/AI` | AI 搜索 |
| POST | `/Search/All` | 全部综合搜索 |
| POST | `/Search/Articles` | 公众号文章搜索 |
| POST | `/Search/Baike` | 百科搜索 |
| POST | `/Search/Books` | 读书搜索 |
| GET | `/Search/Capabilities` | 查看通用搜索支持的分类 |
| POST | `/Search/Channels` | 视频号内容搜索 |
| POST | `/Search/Emoji` | 表情搜索 |
| POST | `/Search/Gateway` | 兼容旧版搜一搜网页网关 |
| POST | `/Search/Images` | 图片搜索 |
| POST | `/Search/Listen` | 听一听搜索 |
| POST | `/Search/Live` | 直播搜索 |
| POST | `/Search/MiniGames` | 小游戏搜索 |
| POST | `/Search/MiniPrograms` | 小程序搜索 |
| POST | `/Search/Moments` | 朋友圈搜索 |
| POST | `/Search/News` | 新闻搜索 |
| POST | `/Search/OfficialAccounts` | 公众号与账号搜索 |
| POST | `/Search/Query` | 通用分类搜索 |
| POST | `/Search/Service/{name}` | 高级搜索能力调用入口 |
| GET | `/Search/Services` | 查看高级搜索能力目录 |
| POST | `/Search/Stickers` | 贴图搜索 |
| POST | `/Search/Underlines` | 划线搜索 |
| POST | `/Search/WeChatIndex` | 微信指数搜索 |
| POST | `/TenPay/Collectmoney` | 确认收款 |
| POST | `/TenPay/ConfirmPreTransferApi` | 确认支付 |
| POST | `/TenPay/GeMaSkdPayQCode` | 自定义经营个人收款单 |
| POST | `/TenPay/GeneratePayQCode` | 生成自定义收款二维码 |
| POST | `/TenPay/GetEncryptInfo` | 获取加密信息 |
| POST | `/TenPay/GetRedPacketListApi` | 查看红包领取列表入口 |
| POST | `/TenPay/OpenHongBao` | 抢红包(带参数) |
| POST | `/TenPay/Openwxhb` | 拆开红包 |
| POST | `/TenPay/Qrydetailwxhb` | 查看红包 |
| POST | `/TenPay/Receivewxhb` | 打开红包不用key |
| POST | `/TenPay/SjSkdPayQCode` | 自定义商家收款单 |
| POST | `/TenPay/WXCreateRedPacketApi` | 创建红包 |
| POST | `/Tools/CdnDownloadImage` | 通过CDN下载微信图片 |
| POST | `/Tools/DownloadFile` | 下载微信文件分片 |
| POST | `/Tools/DownloadImg` | 下载微信图片分片 |
| POST | `/Tools/DownloadVideo` | 下载微信视频分片 |
| POST | `/Tools/DownloadVoice` | 语音下载 |
| GET | `/Tools/GeneratePayQCode` | 生成支付二维码 |
| POST | `/Tools/GetA8Key` | GetA8Key |
| POST | `/Tools/GetBandCardList` | 获取余额以及银行卡信息 |
| POST | `/Tools/GetBoundHardDevices` | GetBoundHardDevices |
| POST | `/Tools/GetCdnDns` | 获取CDN服务器dns信息 |
| POST | `/Tools/HelperVerification` | OauthSdkApp |
| POST | `/Tools/OauthSdkApp` | OauthSdkApp |
| POST | `/Tools/ThirdAppGrant` | 第三方APP授权 |
| POST | `/Tools/UploadFile` | 文件上传 |
| POST | `/Tools/setproxy` | 修改微信步数 |
| POST | `/Translate/Send` | 翻译并发送文字 |
| POST | `/Translate/Text` | 文字翻译 |
| POST | `/User/BindQQ` | 绑定QQ |
| POST | `/User/BindingEmail` | 绑定邮箱 |
| POST | `/User/BindingMobile` | 换绑手机号 |
| GET | `/User/CheckCanSetAlias` | 检测微信登录环境 |
| POST | `/User/DelSafetyInfo` | 删除登录设备 |
| GET | `/User/GetAllOnline` | 获取所有在线wxid（需管理员 key） |
| POST | `/User/GetContractProfile` | 取个人信息 |
| GET | `/User/GetOnlineInfo` | 获取在线信息 |
| POST | `/User/GetQRCode` | 取个人二维码 |
| POST | `/User/GetSafetyInfo` | 登录设备管理 |
| POST | `/User/PrivacySettings` | 隐私设置 |
| POST | `/User/ReportMotion` | ReportMotion |
| POST | `/User/SendVerifyMobile` | 发送手机验证码 |
| POST | `/User/SetAlisa` | 设置微信号 |
| POST | `/User/SetPasswd` | 修改密码 |
| POST | `/User/UpdateProfile` | 修改个人信息 |
| POST | `/User/UploadHeadImage` | 修改头像 |
| POST | `/User/VerifyPasswd` | 验证密码 |
| POST | `/Voice/MessageTranscribe` | 接收到的语音消息转文字 |
| POST | `/Voice/Result` | 查询异步语音转写结果 |
| POST | `/Voice/Transcribe` | 上传语音并转成文字 |
| GET | `/Webhook/Business/Get` | 获取业务回调URL（按授权码） |
| POST | `/Webhook/Business/Set` | 设置业务回调URL（按授权码） |
| GET | `/Webhook/Get` | 获取 Webhook 配置（按授权码） |
| POST | `/Webhook/Remove` | 删除 Webhook 配置（按授权码） |
| POST | `/Webhook/Set` | 设置 Webhook 配置（按授权码） |
| POST | `/Webhook/Test` | 测试发送 Webhook 消息（按授权码） |
| POST | `/Wxapp/AddAvatar` | AddAvatar |
| POST | `/Wxapp/AddMobile` | 小程序绑定增加手机号 |
| POST | `/Wxapp/CloudCallFunction` | 小程序云函数 |
| POST | `/Wxapp/DelMobile` | 小程序删除手机号 |
| POST | `/Wxapp/DellAvatar` | DellAvatar |
| POST | `/Wxapp/GETCreditScoreParam` | 查询游戏信用积分 |
| POST | `/Wxapp/GetAllMobile` | GetAllMobile |
| POST | `/Wxapp/GetRandomAvatar` | GetRandomAvatar |
| POST | `/Wxapp/GetUnionPay` | 微信云闪付支付 |
| POST | `/Wxapp/GetUserOpenId` | GetUserOpenId |
| POST | `/Wxapp/GetWxAppRecord` | 获取小程序记录 |
| POST | `/Wxapp/JSGetSessionid` | 小程序获取小程序支付sessionid |
| POST | `/Wxapp/JSLogin` | 授权小程序(定制) |
| POST | `/Wxapp/JSOperateWxData` | 小程序操作 |
| POST | `/Wxapp/UploadAvatarImg` | UploadAvatarImg |
| POST | `/Wxapp/Verifyplugin` | 小程序获取HostSign |
| POST | `/Wxapp/Wxapp/AddWxAppRecord` | 新增小程序记录 |
| POST | `/Wxapp/Wxapp/GetpullPay` | 推送小程序支付 |
| POST | `/Wxapp/Wxapp/JSGetSessionidQRcode` | 获取付小程序款二维码 |
| POST | `/Wxapp/Wxapp/QrcodeAuthLogin` | 扫码授权登录app或网页 |

---

## 5. 分模块接口详情

> 请求体字段表由 swagger 模型解析生成；"示例"为 swagger 自带示例或按字段类型补全的占位。

### Admin（3 个接口）

#### `POST /Admin/DelayAuthKey`

延期授权码a 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| header | `X-Admin-Token` | string | 否 | 管理员凭证；仅在开启管理员接口后使用 |

**请求体**（模型 `Admin.DelayAuthKeyModel`：authcode/days）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `authcode` | string |  |  |  |
| `days` | integer(int64) |  |  |  |

请求示例：

```json
{
 "authcode": "",
 "days": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Admin/DeleteAuthKey`

删除授权码 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| header | `X-Admin-Token` | string | 否 | 管理员凭证；仅在开启管理员接口后使用 |

**请求体**（模型 `Admin.DeleteAuthKeyModel`：authcode）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `authcode` | string |  |  |  |

请求示例：

```json
{
 "authcode": ""
}
```

**响应**：`models.ResponseResult`

#### `POST /Admin/GenAuthKey`

生成授权码 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| header | `X-Admin-Token` | string | 否 | 管理员凭证；仅在开启管理员接口后使用 |

**请求体**（模型 `Admin.GenAuthKeyModel`：remark）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `count` | integer(int64) |  |  |  |
| `days` | integer(int64) |  |  |  |
| `remark` | string |  |  |  |

请求示例：

```json
{
 "count": 0,
 "days": 0,
 "remark": "示例值"
}
```

**响应**：`models.ResponseResult`

---

### Customized（1 个接口）

#### `POST /Customized/WXCTDUniftyAuthBatch`

批量开小程序 

**请求体**（模型 `Customized.WXCTDUniftyAuthParmDoc`：Wxid 列表或标识）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Username` | string |  |  |  |

请求示例：

```json
{
 "Username": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

---

### Favor（4 个接口）

#### `POST /Favor/Del`

删除收藏 

**请求体**（模型 `Favor.DelParamDoc`：FavId在同步收藏中获取）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FavId` | integer(int32) |  |  |  |

请求示例：

```json
{
 "FavId": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Favor/GetFavInfo`

获取搜藏信息 

**响应**：`models.ResponseResult`

#### `POST /Favor/GetFavItem`

读取收藏内容 

**请求体**（模型 `Favor.GetFavItemParamDoc`：FavId在同步收藏中获取）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FavId` | integer(int32) |  |  |  |

请求示例：

```json
{
 "FavId": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Favor/Sync`

同步收藏 

**请求体**（模型 `Favor.SyncParamDoc`：keybuf:第二次请求需要带上第一次返回的）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Keybuf` | string |  |  |  |

请求示例：

```json
{
 "Keybuf": "your_keybuf"
}
```

**响应**：`models.ResponseResult`

---

### Finder（15 个接口）

#### `POST /Finder/Comment`

评论 

**请求体**（模型 `Finder.CommentParamDoc`：评论）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `CommentId` | integer(int64) |  |  |  |
| `Content` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `ObjectNonceId` | string |  |  |  |
| `OpType` | integer(int32) |  |  |  |
| `ReplyCommentId` | integer(int64) |  |  |  |
| `ReplyUsername` | string |  |  |  |
| `RootCommentId` | integer(int64) |  |  |  |
| `Scene` | integer(int32) |  |  |  |
| `SessionBuffer` | string |  |  |  |
| `Username` | string |  |  |  |

请求示例：

```json
{
 "CommentId": 0,
 "Content": "示例值",
 "Id": 0,
 "ObjectNonceId": "objectnonceid_from_previous_response",
 "OpType": 0,
 "ReplyCommentId": 0,
 "ReplyUsername": "示例值",
 "RootCommentId": 0,
 "Scene": 0,
 "SessionBuffer": "示例值",
 "Username": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/Decrypt`

评论 

**请求体**（模型 `Finder.DecryptParamDoc`：评论）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/FinderGetMsgSessionId`

获取Finder私信会话ID 

**请求体**（模型 `Finder.FinderGetMsgSessionIdParamDoc`：获取会话ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |

请求示例：

```json
{
 "FinderUsername": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/FinderLiveDetail`

直播详情 

**请求体**（模型 `Finder.FinderLiveDetailParamDoc`：直播详情）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderNonceID` | string |  |  |  |
| `FinderObjectID` | integer(int64) |  |  |  |

请求示例：

```json
{
 "FinderNonceID": "findernonceid_from_previous_response",
 "FinderObjectID": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/FinderSearchList`

搜索列表 

**请求体**（模型 `models.EmptyObject`：搜索列表（无参数，传 {}））

_（无字段说明）_

请求示例：

```json
{}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/FinderSendText`

发送私信文字 

**请求体**（模型 `Finder.FinderSendTextParamDoc`：直播详情）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Text` | string |  |  |  |

请求示例：

```json
{
 "FinderUsername": "示例值",
 "Text": "你好"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/Findergettopiclist`

主题列表 

**请求体**（模型 `Finder.FinderGetTopicListParamDoc`：主题列表）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LastBuffer` | string |  |  |  |
| `TopTitle` | string |  |  |  |

请求示例：

```json
{
 "LastBuffer": "示例值",
 "TopTitle": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/Follow`

关注 

**请求体**（模型 `Finder.DefaultParamDoc`：关注）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Value` | string |  |  |  |

请求示例：

```json
{
 "FinderUsername": "示例值",
 "Value": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/GetCommentDetail`

查看指定内容 

**请求体**（模型 `Finder.GetCommentDetailParamDoc`：查看指定内容）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `LastBuffer` | string |  |  |  |
| `ObjectNonceId` | string |  |  |  |
| `RootCommentId` | integer(int64) |  |  |  |

请求示例：

```json
{
 "FinderUsername": "示例值",
 "Id": 0,
 "LastBuffer": "示例值",
 "ObjectNonceId": "objectnonceid_from_previous_response",
 "RootCommentId": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/GetCommentList`

评论列表/详情（支持RootCommentId翻页） 

**请求体**（模型 `Finder.GetCommentDetailParamDoc`：评论列表/详情）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `LastBuffer` | string |  |  |  |
| `ObjectNonceId` | string |  |  |  |
| `RootCommentId` | integer(int64) |  |  |  |

请求示例：

```json
{
 "FinderUsername": "示例值",
 "Id": 0,
 "LastBuffer": "示例值",
 "ObjectNonceId": "objectnonceid_from_previous_response",
 "RootCommentId": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/GetRecommend`

推荐 

**请求体**（模型 `models.EmptyObject`：推荐首页（无参数，传 {}））

_（无字段说明）_

请求示例：

```json
{}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/Like`

点赞 

**请求体**（模型 `Finder.LikeParamDoc`：点赞）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Id` | integer(int64) |  |  |  |
| `ObjectNonceId` | string |  |  |  |
| `SessionBuffer` | string |  |  |  |

请求示例：

```json
{
 "FinderUsername": "示例值",
 "Id": 0,
 "ObjectNonceId": "objectnonceid_from_previous_response",
 "SessionBuffer": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/Search`

用户搜索 

**请求体**（模型 `Finder.DefaultParamDoc`：用户搜索）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `FinderUsername` | string |  |  |  |
| `Value` | string |  |  |  |

请求示例：

```json
{
 "FinderUsername": "示例值",
 "Value": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/TargetUserPage`

查看指定人首页 

**请求体**（模型 `Finder.TargetUserPageParamDoc`：查看指定人首页）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LastBuffer` | string |  |  |  |
| `Target` | string |  |  |  |

请求示例：

```json
{
 "LastBuffer": "示例值",
 "Target": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Finder/UserPrepare`

用户中心 

**响应**：`models.ResponseResult`

---

### Friend（12 个接口）

#### `POST /Friend/Blacklist`

添加/移除黑名单 

**请求体**（模型 `Friend.BlacklistParamDoc`：Val == 15添加  7移除）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `toWxid` | string |  |  |  |
| `val` | integer(int32) |  |  |  |

请求示例：

```json
{
 "toWxid": "towxid_from_previous_response",
 "val": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/Delete`

删除好友 

**请求体**（模型 `Friend.DefaultParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `toWxid` | string |  |  |  |

请求示例：

```json
{
 "toWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/GetContractDetail`

获取通讯录好友详情 

body 支持 userName \| toWxids \| Towxids；多个微信用英文逗号分隔；chatRoom 可选

**请求体**（模型 `Friend.GetContractDetailparameterDoc`：多个微信请用,隔开(最多20个),ChatRoom请留空；也支持 userName/toWxids/Towxids）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `userName` | string |  |  |  |

请求示例：

```json
{
 "userName": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/GetContractList`

获取通讯录好友 

**请求体**（模型 `Friend.GetContractListparameterDoc`：CurrentWxcontactSeq和CurrentChatRoomContactSeq没有的情况下请填写0）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `currentChatRoomContactSeq` | integer(int32) |  |  |  |
| `currentWxcontactSeq` | integer(int32) |  |  |  |

请求示例：

```json
{
 "currentChatRoomContactSeq": 0,
 "currentWxcontactSeq": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/GetFriendstate`

查询好友状态 

**请求体**（模型 `Friend.FriendRelationParamDoc`：OpCode == 1）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `opCode` | integer(int32) |  |  |  |
| `toWxid` | string |  |  |  |

请求示例：

```json
{
 "opCode": 0,
 "toWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/GetMFriend`

获取手机通讯录 

**响应**：`models.ResponseResult`

#### `POST /Friend/LbsFind`

附近人 

**请求体**（模型 `Friend.LbsFindParamDoc`：OpCode == 1）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `latitude` | number(float) |  |  |  |
| `longitude` | number(float) |  |  |  |
| `opCode` | integer(int32) |  |  |  |

请求示例：

```json
{
 "latitude": 0,
 "longitude": 0,
 "opCode": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/PassVerify`

通过好友请求 

**请求体**（模型 `Friend.PassVerifyParamDoc`：Scene：代表来源,请在消息中的xml中获取）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `opcode` | integer(int32) |  |  |  |
| `scene` | integer(int32) |  |  |  |
| `v1` | string |  |  |  |
| `v2` | string |  |  |  |

请求示例：

```json
{
 "opcode": 0,
 "scene": 0,
 "v1": "示例值",
 "v2": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/Search`

搜索联系人 

示例：{"Wxid":"可留空由authcode注入","Keyword":"wxid_xxx","FromScene":0,"SearchScene":1}

**请求体**（模型 `Friend.SearchParamDoc`：爆粉情况下特殊通道请自行填写,默认时FromScene=0,SearchScene=1）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `fromScene` | integer(int32) |  |  |  |
| `keyword` | string |  |  |  |
| `searchScene` | integer(int32) |  |  |  |

请求示例：

```json
{
 "fromScene": 0,
 "keyword": "your_keyword",
 "searchScene": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/SendRequest`

添加联系人(发送好友请求) 

示例：{"Wxid":"可留空由authcode注入","V1":"xxx","V2":"yyy","Content":"你好","Scene":17}

**请求体**（模型 `Friend.SendRequestParamDoc`：V1 V2是必填项）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `v1` | string |  |  |  |
| `v2` | string |  |  |  |

请求示例：

```json
{
 "v1": "示例值",
 "v2": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/SetRemarks`

设置好友备注 

**请求体**（模型 `Friend.SetRemarksParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `remarks` | string |  |  |  |
| `toWxid` | string |  |  |  |

请求示例：

```json
{
 "remarks": "示例值",
 "toWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Friend/Upload`

上传通讯录 

**请求体**（模型 `Friend.UploadParamDoc`：PhoneNo多个手机号请用,隔开   CurrentPhoneNo自己的手机号  Opcode == 1上传 2删除）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `currentPhoneNo` | string |  |  |  |
| `opcode` | integer(int32) |  |  |  |
| `phoneNo` | string |  |  |  |

请求示例：

```json
{
 "currentPhoneNo": "示例值",
 "opcode": 0,
 "phoneNo": "示例值"
}
```

**响应**：`models.ResponseResult`

---

### FriendCircle（11 个接口）

#### `POST /FriendCircle/Comment`

朋友圈点赞/评论 

**请求体**（模型 `FriendCircle.CommentParamDoc`：type：1点赞 2：文本 3:消息 4：with 5陌生人点赞 replyCommnetId：回复评论Id）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `content` | string |  |  |  |
| `id` | string |  |  |  |
| `replyCommnetId` | integer(int32) |  |  |  |
| `type` | integer(int32) |  |  |  |

请求示例：

```json
{
 "content": "示例值",
 "id": "id_from_previous_response",
 "replyCommnetId": 0,
 "type": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/GetCommnet`

获取评论内容 

**请求体**（模型 `FriendCircle.GetCommnetParamDoc`：包含id和username的XML数据）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `xmlData` | string |  |  |  |

请求示例：

```json
{
 "xmlData": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/GetDetail`

获取特定人朋友圈 

**请求体**（模型 `FriendCircle.GetDetailparameterDoc`：打开首页时：Fristpagemd5留空,Maxid填写0）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `fristpagemd5` | string |  |  |  |
| `maxid` | integer(int64) |  |  |  |
| `towxid` | string |  |  |  |

请求示例：

```json
{
 "fristpagemd5": "示例值",
 "maxid": 0,
 "towxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/GetIdDetail`

获取特定ID详情内容 

**请求体**（模型 `FriendCircle.GetIdDetailParamDoc`：Id为当前朋友圈内容的id）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `id` | integer(int64) |  |  |  |
| `towxid` | string |  |  |  |

请求示例：

```json
{
 "id": 0,
 "towxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/GetList`

朋友圈首页列表 

**请求体**（模型 `FriendCircle.GetListParamDoc`：打开首页时：Fristpagemd5留空,Maxid填写0）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `fristpagemd5` | string |  |  |  |
| `maxid` | integer(int64) |  |  |  |

请求示例：

```json
{
 "fristpagemd5": "示例值",
 "maxid": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/Messages`

发布朋友圈 

**请求体**（模型 `FriendCircle.SnsPostItemDoc`：请自行构造xml内容）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `blackList` | string |  |  |  |
| `private` | integer(int64) |  |  |  |
| `thumbmd5` | string |  |  |  |
| `thumburl` | string |  |  |  |
| `title` | string |  |  |  |
| `totalSize` | string |  |  |  |
| `videomd5` | string |  |  |  |
| `videourl` | string |  |  |  |
| `withUserList` | string |  |  |  |

请求示例：

```json
{
 "blackList": "示例值",
 "private": 0,
 "thumbmd5": "示例值",
 "thumburl": "https://example.com",
 "title": "示例值",
 "totalSize": "示例值",
 "videomd5": "示例值",
 "videourl": "https://example.com",
 "withUserList": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/MmSnsSync`

查询正在 评论转发的ID 

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/Operation`

朋友圈操作 

**请求体**（模型 `FriendCircle.OperationParamDoc`：type：1删除朋友圈2设为隐私3设为公开4删除评论5取消点赞）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `commnetId` | integer(int32) |  |  |  |
| `id` | string |  |  |  |
| `type` | integer(int32) |  |  |  |

请求示例：

```json
{
 "commnetId": 0,
 "id": "id_from_previous_response",
 "type": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/PrivacySettings`

朋友圈权限设置 

**请求体**（模型 `FriendCircle.PrivacySettingsParamDoc`：核心参数请联系客服获取代码列表）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `function` | integer(int32) |  |  |  |
| `value` | integer(int32) |  |  |  |

请求示例：

```json
{
 "function": 0,
 "value": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/PushCommnet`

启动评论检查任务并转发评论 

**请求体**（模型 `FriendCircle.RequestParamsDoc`：评论转发的地址与sns id）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `forwardAddr` | string |  |  |  |
| `id` | string |  |  |  |

请求示例：

```json
{
 "forwardAddr": "示例值",
 "id": "id_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /FriendCircle/Upload`

朋友圈下载CDN视频 

**请求体**（模型 `FriendCircle.DownloadMediaModelDoc`：下载参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `key` | string |  |  |  |
| `url` | string |  |  |  |

请求示例：

```json
{
 "key": "your_key",
 "url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

---

### Group（23 个接口）

#### `POST /Group/AddChatRoomMember`

增加群成员(40人以内) 

**请求体**（模型 `Group.AddChatRoomParamDoc`：ToWxids 多个微信ID用,隔开 ChatRoomName 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ChatRoomName` | string |  |  |  |
| `ToWxids` | string |  |  |  |

请求示例：

```json
{
 "ChatRoomName": "示例值",
 "ToWxids": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/ConsentToJoin`

同意进入群聊 

**请求体**（模型 `Group.ConsentToJoinParamDoc`：Url请在消息内容xml中查找）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

请求示例：

```json
{
 "Url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/CreateChatRoom`

创建群聊 

**请求体**（模型 `Group.CreateChatRoomParamDoc`：ToWxids 多个微信ID用,隔开 至少三个好友微信ID以上）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ToWxids` | string |  |  |  |

请求示例：

```json
{
 "ToWxids": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/DelChatRoomMember`

删除群成员 

**请求体**（模型 `Group.AddChatRoomParamDoc`：ToWxids 多个微信ID用,隔开 ChatRoomName 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ChatRoomName` | string |  |  |  |
| `ToWxids` | string |  |  |  |

请求示例：

```json
{
 "ChatRoomName": "示例值",
 "ToWxids": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/FacingCreateChatRoom`

创建群聊 

**请求体**（模型 `Group.FacingCreateChatRoomParamDoc`：ToWxids 多个微信ID用,隔开 至少三个好友微信ID以上）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Latitude` | number(float) |  |  |  |
| `Longitude` | number(float) |  |  |  |
| `OpCode` | integer(int32) |  |  |  |
| `Password` | string |  |  |  |

请求示例：

```json
{
 "Latitude": 0,
 "Longitude": 0,
 "OpCode": 0,
 "Password": "your_password"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/GetChatRoomInfo`

获取群详情(不带公告内容) 

**请求体**（模型 `Group.GetChatRoomParamDoc`：UserNameList == 群ID,多个查询请用,隔开）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/GetChatRoomInfoDetail`

获取群信息(带公告内容) 

**请求体**（模型 `Group.GetChatRoomParamDoc`：QID == 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/GetChatRoomMemberDetail`

获取群成员详情 

**请求体**（模型 `Group.GetChatRoomParamDoc`：QID == 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/GetQRCode`

获取群二维码 

**请求体**（模型 `Group.GetChatRoomParamDoc`：QID == 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `GET /Group/GroupList`

获取群列表（兼容路由） 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| query | `force` | string | 否 | 可选：1/true 表示强制全量刷新，不读缓存 |

**响应**：`models.ResponseResult`

#### `POST /Group/InviteChatRoomMember`

邀请群成员(40人以上) 

**请求体**（模型 `Group.AddChatRoomParamDoc`：ToWxids 多个微信ID用,隔开 ChatRoomName 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ChatRoomName` | string |  |  |  |
| `ToWxids` | string |  |  |  |

请求示例：

```json
{
 "ChatRoomName": "示例值",
 "ToWxids": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `GET /Group/List`

获取群列表（业务路由） 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| query | `force` | string | 否 | 可选：1/true 表示强制全量刷新 |

**响应**：`models.ResponseResult`

#### `POST /Group/MoveContractList`

保存到通讯录 

**请求体**（模型 `Group.MoveContractListParamDoc`：Val == 3添加 2移除）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |
| `Val` | integer(int32) |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response",
 "Val": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/OperateChatRoomAdmin`

群管理操作(添加、删除、转让) 

**请求体**（模型 `Group.OperateChatRoomAdminParamDoc`：ToWxids == 多个wxid用,隔开(仅限于添加/删除管理员) Val == 1添加 2删除 3转让）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |
| `ToWxids` | string |  |  |  |
| `Val` | integer(int32) |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response",
 "ToWxids": "示例值",
 "Val": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/Quit`

退出群聊 

**请求体**（模型 `Group.QuitGroupParamDoc`：QID == 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/ScanIntoGroup`

扫码进群 

**请求体**（模型 `Group.ScanIntoGroupParamDoc`：只支持url）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

请求示例：

```json
{
 "Url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/ScanIntoGroupEnterprise`

扫码进群(企业) 

**请求体**（模型 `Group.ScanIntoGroupParamDoc`：只支持url）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

请求示例：

```json
{
 "Url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/SendPat`

群拍一拍功能 

**请求体**（模型 `Group.SendPatParamDoc`：QID/ToUserName/Scene）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `QID` | string |  |  |  |
| `Scene` | integer(int64) |  |  |  |
| `ToUserName` | string |  |  |  |

请求示例：

```json
{
 "QID": "qid_from_previous_response",
 "Scene": 0,
 "ToUserName": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/SendTransferGroupOwner`

转让群 

**请求体**（模型 `Group.TransferGroupOwnerParamDoc`：QID/NewOwnerUserName）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `NewOwnerUserName` | string |  |  |  |
| `QID` | string |  |  |  |

请求示例：

```json
{
 "NewOwnerUserName": "示例值",
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/SetChatRoomAnnouncement`

设置群公告 

**请求体**（模型 `Group.OperateChatRoomInfoParamDoc`：Content == 公告内容）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `QID` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/SetChatRoomName`

设置群名称 

**请求体**（模型 `Group.OperateChatRoomInfoParamDoc`：Content == 名称）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `QID` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/SetChatRoomRemarks`

设置群备注(仅自己可见) 

**请求体**（模型 `Group.OperateChatRoomInfoParamDoc`：QID == 群ID）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `QID` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Group/SetChatroomAccessVerify`

设置群聊邀请开关 

**请求体**（模型 `Group.SetChatroomAccessVerifyParamDoc`：QID/Enable）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Enable` | boolean |  |  |  |
| `QID` | string |  |  |  |

请求示例：

```json
{
 "Enable": false,
 "QID": "qid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

---

### Label（5 个接口）

#### `POST /Label/Add`

添加标签 

**请求体**（模型 `Label.AddParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelName` | string |  |  |  |

请求示例：

```json
{
 "LabelName": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Label/Delete`

删除标签 

**请求体**（模型 `Label.DeleteParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelID` | string |  |  |  |

请求示例：

```json
{
 "LabelID": "labelid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Label/GetList`

获取标签列表 

**响应**：`models.ResponseResult`

#### `POST /Label/UpdateList`

更新标签列表 

**请求体**（模型 `Label.UpdateListParamDoc`：ToWxid:多个请用,隔开）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelID` | string |  |  |  |
| `ToWxids` | string |  |  |  |

请求示例：

```json
{
 "LabelID": "labelid_from_previous_response",
 "ToWxids": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Label/UpdateName`

修改标签 

**请求体**（模型 `Label.UpdateNameParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `LabelID` | integer(int32) |  |  |  |
| `NewName` | string |  |  |  |

请求示例：

```json
{
 "LabelID": 0,
 "NewName": "示例值"
}
```

**响应**：`models.ResponseResult`

---

### Login（38 个接口）

#### `POST /Login/62data`

62登陆(账号或密码) 

**请求体**（模型 `Login.Data62LoginReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Data62` | string |  |  |  |
| `DeviceName` | string |  |  |  |
| `Password` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `UserName` | string |  |  |  |

请求示例：

```json
{
 "Data62": "示例值",
 "DeviceName": "示例值",
 "Password": "your_password",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "UserName": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/62dataQRCodeApply`

62登陆(账号或密码), 并申请使用二维码验证 

**请求体**（模型 `Login.Data62LoginReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Data62` | string |  |  |  |
| `DeviceName` | string |  |  |  |
| `Password` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `UserName` | string |  |  |  |

请求示例：

```json
{
 "Data62": "示例值",
 "DeviceName": "示例值",
 "Password": "your_password",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "UserName": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/62dataSMSAgain`

62登陆(账号或密码), 重发验证码 

**请求体**（模型 `Login.Data62SMSAgainReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Cookie` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `Url` | string |  |  |  |

请求示例：

```json
{
 "Cookie": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "Url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/62dataSMSApply`

62登陆(账号或密码), 并申请使用SMS验证 

**请求体**（模型 `Login.Data62LoginReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Data62` | string |  |  |  |
| `DeviceName` | string |  |  |  |
| `Password` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `UserName` | string |  |  |  |

请求示例：

```json
{
 "Data62": "示例值",
 "DeviceName": "示例值",
 "Password": "your_password",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "UserName": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/62dataSMSVerify`

62登陆(账号或密码), 二维码验证校验 

**请求体**（模型 `Login.Data62SMSVerifyReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Cookie` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `Sms` | string |  |  |  |
| `Url` | string |  |  |  |

请求示例：

```json
{
 "Cookie": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "Sms": "示例值",
 "Url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/A16Data`

A16登陆(账号或密码) - android == 8.0.50 

**请求体**（模型 `Login.A16LoginParam`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `A16` | string |  |  |  |
| `DeviceName` | string |  |  |  |
| `Extend` | `Algorithm.AndroidDeviceInfo` |  |  |  |
| `Password` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `UserName` | string |  |  |  |

请求示例：

```json
{
 "A16": "示例值",
 "DeviceName": "示例值",
 "Extend": {
  "AndriodBssId": "andriodbssid_from_previous_response",
  "AndriodFsId": "andriodfsid_from_previous_response",
  "AndriodId": "andriodid_from_previous_response",
  "AndriodSsId": "andriodssid_from_previous_response",
  "Androidversion": "示例值",
  "Arch": "示例值",
  "BuildBoard": "示例值",
  "BuildFP": "示例值",
  "BuildID": "buildid_from_previous_response",
  "Features": "示例值",
  "Hardware": "示例值",
  "Imei": "示例值",
  "KernelReleaseNumber": "示例值",
  "Manufacturer": "示例值",
  "PackageSign": "示例值",
  "PhoneModel": "示例值",
  "PhoneSerial": "示例值",
  "RadioVersion": "示例值",
  "SbMD5": "示例值",
  "SfArm64MD5": "示例值",
  "SfArmMD5": "示例值",
  "SfMD5": "示例值",
  "WLanAddress": "示例值",
  "WidevineDeviceID": "widevinedeviceid_from_previous_response",
  "WidevineProvisionID": "widevineprovisionid_from_previous_response",
  "WifiFullName": "示例值",
  "WifiName": "示例值"
 },
 "Password": "your_password",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "UserName": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/A16Data848`

A16登陆(账号或密码) - android == 新版云函数 

**请求体**（模型 `Login.A16LoginParam`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `A16` | string |  |  |  |
| `DeviceName` | string |  |  |  |
| `Extend` | `Algorithm.AndroidDeviceInfo` |  |  |  |
| `Password` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `UserName` | string |  |  |  |

请求示例：

```json
{
 "A16": "示例值",
 "DeviceName": "示例值",
 "Extend": {
  "AndriodBssId": "andriodbssid_from_previous_response",
  "AndriodFsId": "andriodfsid_from_previous_response",
  "AndriodId": "andriodid_from_previous_response",
  "AndriodSsId": "andriodssid_from_previous_response",
  "Androidversion": "示例值",
  "Arch": "示例值",
  "BuildBoard": "示例值",
  "BuildFP": "示例值",
  "BuildID": "buildid_from_previous_response",
  "Features": "示例值",
  "Hardware": "示例值",
  "Imei": "示例值",
  "KernelReleaseNumber": "示例值",
  "Manufacturer": "示例值",
  "PackageSign": "示例值",
  "PhoneModel": "示例值",
  "PhoneSerial": "示例值",
  "RadioVersion": "示例值",
  "SbMD5": "示例值",
  "SfArm64MD5": "示例值",
  "SfArmMD5": "示例值",
  "SfMD5": "示例值",
  "WLanAddress": "示例值",
  "WidevineDeviceID": "widevinedeviceid_from_previous_response",
  "WidevineProvisionID": "widevineprovisionid_from_previous_response",
  "WifiFullName": "示例值",
  "WifiName": "示例值"
 },
 "Password": "your_password",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "UserName": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/AutoHeartBeat`

开启自动心跳, 自动二次登录 

**响应**：`models.ResponseResult`

#### `POST /Login/Awaken`

唤醒登陆(只限扫码登录) 

**响应**：`models.ResponseResult`

#### `POST /Login/CheckMacQR`

检测Mac二维码 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| query | `uuid` | string | 是 | 请输入取码时返回的UUID |
| query | `deviceID` | string | 否 | GetMacQR 返回的设备 ID；留空时服务尝试根据 uuid 恢复 |

**请求体**（模型 `Login.MaccodeParam`：JSON 兼容调用；也可只使用 uuid/deviceID query 参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `authcode` | string |  | 旧客户端兼容授权码；推荐使用 X-Access-Token 请求头 |  |
| `deviceID` | string |  | GetMacQR 返回的设备 ID；留空时服务尝试根据 uuid 恢复 | device_id_from_login |
| `uuid` | string |  | GetMacQR 返回的 UUID | uuid_from_qr_response |

请求示例：

```json
{
 "authcode": "",
 "deviceID": "device_id_from_login",
 "uuid": "uuid_from_qr_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/CheckQR`

检测二维码 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| header | `X-QR-Check-Token` | string | 否 | GetQR 返回的二维码专属检测令牌（推荐） |
| query | `check_token` | string | 否 | 二维码检测令牌的旧客户端 query 兼容形式；优先使用 X-QR-Check-Token |
| query | `uuid` | string | 是 | 请输入取码时返回的UUID |

**响应**：`models.ResponseResult`

#### `POST /Login/ExtDeviceLoginConfirmGet`

新设备扫码登录 

**请求体**（模型 `Login.ExtDeviceLoginConfirmParam`：URL == MAC iPad Windows 的微信二维码解析出来的url）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

请求示例：

```json
{
 "Url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/ExtDeviceLoginConfirmOk`

新设备扫码确认登录 

**请求体**（模型 `Login.ExtDeviceLoginConfirmParam`：URL == MAC iPad Windows 的微信二维码解析出来的url）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |

请求示例：

```json
{
 "Url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/Get62Data`

获取62数据 

**响应**：`models.ResponseResult`

#### `POST /Login/GetA16Data`

获取A16数据 

**响应**：`models.ResponseResult`

#### `POST /Login/GetCacheInfo`

获取登陆缓存信息 

**响应**：`models.ResponseResult`

#### `POST /Login/GetLoginQRCode862`

获取二维码(iPad 8.0.62 专用) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult2`

#### `POST /Login/GetQR`

获取二维码(iPad) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRMac`

获取二维码(Mac) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRMac_oversea`

获取二维码(Mac，海外) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRPad`

获取二维码(安卓Pad-ppmt专用) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRPadx`

获取二维码(安卓Pad-绕过验证码) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRWatch`

获取二维码(Car) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRWin`

获取二维码(Windows) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRWinUnified`

获取二维码(WinUnified-统一PC版) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRWinUwp`

获取二维码(WindowsUwp-绕过验证码) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQR_oversea`

获取二维码(iPad，海外) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRx`

获取二维码(iPad-绕过验证码) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/GetQRx_oversea`

获取二维码(iPad-绕过验证码，海外) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/HarmonyLoginApi`

获取二维码(鸿蒙平板) 

**请求体**（模型 `Login.GetQRReq`：不使用代理请留空；oversea=true 启用海外域名(wechat.com)）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceName` | string |  |  |  |
| `Proxy` | `models.ProxyInfo` |  |  |  |
| `oversea` | boolean |  |  |  |

请求示例：

```json
{
 "DeviceName": "示例值",
 "Proxy": {
  "ProxyIp": "",
  "ProxyPassword": "",
  "ProxyUser": ""
 },
 "oversea": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Login/HeartBeat`

心跳包 

**响应**：`models.ResponseResult`

#### `GET /Login/HeartBeatLogs`

获取心跳日志 

**响应**：array<string>；返回日志列表

#### `POST /Login/HeartBeatLong`

长连接心跳包跳包 

**响应**：`models.ResponseResult`

#### `POST /Login/LogOut`

退出登录 

**响应**：`models.ResponseResult`

#### `GET /Login/LongLinkStatus`

查看当前账号长连接运行状态 

返回 F104 握手模式、收发时间、重连次数和 Ticket 生命周期，不包含任何密钥材料

**响应**：`models.ResponseResult`

#### `POST /Login/Newinit`

初始化 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| query | `MaxSynckey` | string | 否 | 二次同步需要带入 |
| query | `CurrentSynckey` | string | 否 | 二次同步需要带入 |

**响应**：`models.ResponseResult`

#### `POST /Login/TwiceAutoAuth`

二次登陆 

**响应**：`models.ResponseResult`

#### `POST /Login/YPayVerificationcode`

提交登录验证码 

**请求体**（模型 `Login.VerificationcodeParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Code` | string |  |  |  |
| `Data62` | string |  |  |  |
| `Ticket` | string |  |  |  |
| `Uuid` | string |  |  |  |

请求示例：

```json
{
 "Code": "示例值",
 "Data62": "示例值",
 "Ticket": "示例值",
 "Uuid": "uuid_from_qr_response"
}
```

**响应**：`models.ResponseResult`

---

### Msg（18 个接口）

#### `POST /Msg/Quote`

发送引用回复消息 

支持文本、图片、语音、视频、应用消息及群聊；id/new_msg_id/svr_id 是同一个服务器消息ID。推荐发送 {"content":"回复内容","reply_context":收到消息.reply_context}，群聊上下文会同时携带群成员 from_user_id 与群会话 chat_user_id。

**请求体**（模型 `Msg.QuoteDoc`：回复内容及被引用消息上下文；64位 svr_id/new_msg_id 必须使用字符串）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `chat_user_id` | string |  |  |  |
| `content` | string |  |  |  |
| `display_name` | string |  |  |  |
| `from_user_id` | string |  |  |  |
| `msg_type` | integer(int32) |  |  |  |
| `new_msg_id` | string |  |  |  |
| `quote_content` | string |  |  |  |
| `reply_context` | `Msg.QuoteContextDoc` |  |  |  |
| `sequence` | string |  |  |  |
| `svr_id` | string |  |  |  |
| `to_wxid` | string |  |  |  |

请求示例：

```json
{
 "content": "这是调用方对该消息的引用回复",
 "reply_context": {
  "svr_id": "4588852482559559123",
  "new_msg_id": "4588852482559559123",
  "msg_id": 1513020125,
  "msg_type": 34,
  "sequence": 46191,
  "to_wxid": "123456789@chatroom",
  "conversation_id": "123456789@chatroom",
  "from_user_id": "wxid_group_member",
  "chat_user_id": "123456789@chatroom",
  "quote_content": "你好，你可以做什么？"
 }
}
```

**响应**：`Msg.QuoteResponseDoc`

#### `POST /Msg/Revoke`

撤回消息 

**请求体**（模型 `Msg.RevokeMsgParamDoc`：请注意参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ClientMsgId` | integer(int64) |  |  |  |
| `CreateTime` | integer(int64) |  |  |  |
| `NewMsgId` | integer(int64) |  |  |  |
| `ToUserName` | string |  |  |  |

请求示例：

```json
{
 "ClientMsgId": 0,
 "CreateTime": 0,
 "NewMsgId": 0,
 "ToUserName": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendApp`

群发消息 

**请求体**（模型 `Msg.SendGroupMassMsgTextParamDoc`：Type请根据场景设置,xml请自行构造）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `ToIds` | array<string> |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "ToIds": [
  "示例值"
 ]
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendCDNFile`

发送文件(转发,并非上传) 

**请求体**（模型 `Msg.DefaultParamDoc`：Content==收到文件消息xml）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `ToWxid` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "ToWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendCDNImg`

发送Cdn图片(转发图片) 

**请求体**（模型 `Msg.DefaultParamDoc`：Content==消息xml）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `ToWxid` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "ToWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendCDNVideo`

发送Cdn视频(转发视频) 

**请求体**（模型 `Msg.DefaultParamDoc`：Content==消息xml）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `ToWxid` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "ToWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendEmoji`

发送Emoji 

**请求体**（模型 `Msg.SendEmojiParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Md5` | string |  |  |  |
| `ToWxid` | string |  |  |  |
| `TotalLen` | integer(int32) |  |  |  |

请求示例：

```json
{
 "Md5": "示例值",
 "ToWxid": "towxid_from_previous_response",
 "TotalLen": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendTxt`

发送文本消息 

**请求体**（模型 `Msg.SendNewMsgParamDoc`：Type请填写1 At == 群@,多个wxid请用,隔开）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `At` | string |  |  |  |
| `Content` | string |  |  |  |
| `ToWxid` | string |  |  |  |
| `Type` | integer(int64) |  |  |  |

请求示例：

```json
{
 "At": "示例值",
 "Content": "示例值",
 "ToWxid": "towxid_from_previous_response",
 "Type": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendVideo`

发送视频 

**请求体**（模型 `Msg.SendVideoMsgParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `ImageBase64` | string |  |  |  |
| `PlayLength` | integer(int32) |  |  |  |
| `ToWxid` | string |  |  |  |

请求示例：

```json
{
 "Base64": "示例值",
 "ImageBase64": "示例值",
 "PlayLength": 0,
 "ToWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendVoice`

发送语音 

**请求体**（模型 `Msg.SendVoiceMessageParamDoc`：Type： AMR = 0, MP3 = 2, SILK = 4, SPEEX = 1, WAVE = 3 VoiceTime ：音频长度 1000为一秒）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `ToWxid` | string |  |  |  |
| `Type` | integer(int32) |  |  |  |
| `VoiceTime` | integer(int32) |  |  |  |

请求示例：

```json
{
 "Base64": "示例值",
 "ToWxid": "towxid_from_previous_response",
 "Type": 0,
 "VoiceTime": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/SendXCX`

发送小程序消息 

**请求体**（模型 `Msg.DefaultParamDoc`：Content==小程序xml）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Content` | string |  |  |  |
| `ToWxid` | string |  |  |  |

请求示例：

```json
{
 "Content": "示例值",
 "ToWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/ShareCard`

分享名片 

**请求体**（模型 `Msg.ShareCardParamDoc`：ToWxid==接收的微信ID CardWxId==名片wxid CardNickName==名片昵称 CardAlias==名片别名）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `CardAlias` | string |  |  |  |
| `CardNickName` | string |  |  |  |
| `CardWxId` | string |  |  |  |
| `ToWxid` | string |  |  |  |

请求示例：

```json
{
 "CardAlias": "示例值",
 "CardNickName": "示例值",
 "CardWxId": "cardwxid_from_previous_response",
 "ToWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/ShareLink`

发送分享链接消息 

**请求体**（模型 `Msg.SendAppMsgParamDoc`：Type==类型 Desc==描述 Xml==发送xml内容 ToWxid==接受者）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ToWxid` | string |  |  |  |
| `Type` | integer(int32) |  |  |  |
| `Xml` | string |  |  |  |

请求示例：

```json
{
 "ToWxid": "towxid_from_previous_response",
 "Type": 0,
 "Xml": "<msg></msg>"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/ShareLocation`

分享位置 

**请求体**（模型 `Msg.ShareLocationParamDoc`）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Infourl` | string |  |  |  |
| `Label` | string |  |  |  |
| `Poiname` | string |  |  |  |
| `Scale` | number(double) |  |  |  |
| `ToWxid` | string |  |  |  |
| `X` | number(double) |  |  |  |
| `Y` | number(double) |  |  |  |

请求示例：

```json
{
 "Infourl": "https://example.com",
 "Label": "示例值",
 "Poiname": "示例值",
 "Scale": 0,
 "ToWxid": "towxid_from_previous_response",
 "X": 0,
 "Y": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/ShareVideo`

发送分享视频消息 

**请求体**（模型 `Msg.ShareVideoMsgParamDoc`：xml：微信返回的视频xml）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `ToWxid` | string |  |  |  |
| `Xml` | string |  |  |  |

请求示例：

```json
{
 "ToWxid": "towxid_from_previous_response",
 "Xml": "<msg></msg>"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/StartAutoSync`

启动自动同步 

启用该账号的统一消息同步；后续 WS/Webhook 的 sync_message 事件会自动带 reply_context，以及 image/video/file/voice 所需的结构化业务参数。

**请求体**（模型 `Msg.SyncParam2Doc`：兼容旧版：TargetURL 可忽略）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `TargetURL` | string |  |  |  |

请求示例：

```json
{
 "TargetURL": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/Sync`

同步消息 

触发一次微信增量同步；聊天消息会同时按 wechatpad.message.v2 投递到 WS/Webhook。图片、视频、文件分别在 image/video/file 中携带可直接调用下载接口的 download_context，每条消息还携带 reply_context。

**请求体**（模型 `Msg.SyncParamDoc`：Scene填写0,Synckey留空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Scene` | integer(int32) |  |  |  |
| `Synckey` | string |  |  |  |

请求示例：

```json
{
 "Scene": 0,
 "Synckey": "your_synckey"
}
```

**响应**：`models.ResponseResult`

#### `POST /Msg/UploadImg`

发送图片 

**请求体**（模型 `Msg.SendImageMsgParamDoc`：请注意base64格式）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `ToWxid` | string |  |  |  |

请求示例：

```json
{
 "Base64": "示例值",
 "ToWxid": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

---

### OfficialAccounts（12 个接口）

#### `POST /OfficialAccounts/AuthMpLogin`

授权公众号登录 

**请求体**（模型 `OfficialAccounts.AuthMpLoginParam`：url/scene 必填）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `scene` | integer(int32) |  |  |  |
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "scene": 0,
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/Follow`

关注 

**请求体**（模型 `OfficialAccounts.DefaultParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/GetAppMsgExt`

阅读文章,返回 分享、看一看、阅读数据 

**请求体**（模型 `OfficialAccounts.ReadParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/GetAppMsgExtLike`

点赞文章,返回 分享、看一看、阅读数据 

**请求体**（模型 `OfficialAccounts.ReadParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/GetMpHistory`

获取公众号历史消息 

**请求体**（模型 `OfficialAccounts.GetMpHistoryMsgParam`：url 必填）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/GetMpHistoryMessage`

获取公众号历史消息HTML 

**请求体**（模型 `OfficialAccounts.GetMpHistoryMsgParam`：url必填）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/JSAPIPreVerify`

JSAPIPreVerify 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| query | `url` | string | 是 | 需要 JSAPI 权限校验的完整页面 URL |
| query | `appid` | string | 是 | 公众号 AppID |

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/MpGetA8Key`

MpGetA8Key(获取文章key和uin) 

**请求体**（模型 `OfficialAccounts.ReadParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/OauthAuthorize`

OauthAuthorize 

**请求体**（模型 `OfficialAccounts.GetkeyParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  | 公众号 AppID | wx1234567890abcdef |
| `url` | string |  | 需要 JSAPI 权限校验的完整页面 URL | https://example.com/article |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "url": "https://example.com/article",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/QRConnectAuthorize`

二维码授权请求 

**请求体**（模型 `OfficialAccounts.QRConnectParam`：url 必填）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/QRConnectAuthorizeConfirm`

二维码授权确认 

**请求体**（模型 `OfficialAccounts.QRConnectParam`：url 必填）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `url` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "url": "https://example.com",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /OfficialAccounts/Quit`

取消关注 

**请求体**（模型 `OfficialAccounts.DefaultParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `wxid` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

---

### QWContact（3 个接口）

#### `POST /QWContact/QWApplyAddContact`

QWApplyAddContact 

**请求体**（模型 `QWContact.QWApplyAddContactParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Context` | string |  |  |  |
| `Username` | string |  |  |  |
| `V1` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Context": "示例值",
 "Username": "wxid_recipient",
 "V1": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /QWContact/QWContact/QWAddContact`

QWAddContact 

**请求体**（模型 `QWContact.QWAddContactParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Username` | string |  |  |  |
| `V1` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Username": "wxid_recipient",
 "V1": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /QWContact/SearchQWContact`

SearchQWContact 

**请求体**（模型 `QWContact.AddWxAppRecordParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Username` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Username": "wxid_recipient",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

---

### SayHello（2 个接口）

#### `POST /SayHello/Modelv1`

模式1-扫码 

**请求体**（模型 `SayHello.Model1Param`：注意,请先执行1再执行2）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Url` | string |  |  |  |
| `VerifyContent` | string |  |  |  |

请求示例：

```json
{
 "Url": "https://example.com",
 "VerifyContent": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /SayHello/Modelv2`

模式3-v3\v4打招呼 

**请求体**（模型 `SayHello.SendRequestParam1`：Scene 招呼通道 v3v4通道，v4可空）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Scene` | integer(int64) |  |  |  |
| `V3` | string |  |  |  |
| `V4` | string |  |  |  |
| `VerifyContent` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Scene": 0,
 "V3": "示例值",
 "V4": "示例值",
 "VerifyContent": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

---

### Search（23 个接口）

#### `POST /Search/AI`

AI 搜索 

使用当前登录设备的 AI 搜索独立页面协议（scene 4818），返回账号区域可用性及会话标识

**请求体**（模型 `Search.AIFirstPageRequest`：可直接执行的首轮示例；续问追加上一次响应的 session_id，并递增 turn）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `model` | string |  | 搜索模型；留空使用服务默认模型 | hy3-preview-proxy |
| `query` | string |  | AI 搜索问题，至少 2 个字符 | 深圳有哪些值得关注的科技公司 |
| `turn` | integer(int64) |  | 首轮固定为 0 | 0 |

请求示例：

```json
{
 "query": "深圳有哪些值得关注的科技公司",
 "model": "hy3-preview-proxy",
 "turn": 0,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/All`

全部综合搜索 

固定使用 all 分类，返回混合类型结果并支持 search_id、cursor 和 next_offset 分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "人工智能",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Articles`

公众号文章搜索 

固定使用 article 分类，支持 search_id/cursor 分页

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "Go 语言",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Baike`

百科搜索 

固定使用 baike 分类搜索百科内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "大模型",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Books`

读书搜索 

固定使用 read 分类搜索微信读书相关内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "架构设计",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `GET /Search/Capabilities`

查看通用搜索支持的分类 

返回 Query 接口可填写的 category、对应 business_type 以及当前已建模能力；用于客户端动态生成分类列表，普通固定分类搜索流程可跳过该接口。

**响应**：`models.ResponseResult`

#### `POST /Search/Channels`

视频号内容搜索 

搜索视频号内容，返回视频地址、封面、时长、互动指标及 operation_params 运行参数，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "科技",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Emoji`

表情搜索 

固定使用 emoji 分类搜索表情内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "开心",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Gateway`

兼容旧版搜一搜网页网关 

保留给已有 Gateway 调用方：通过项目协议登录态取得网页搜索授权并访问搜索页面。新业务查询优先使用 Articles、OfficialAccounts、Channels、MiniPrograms、Moments、AI 等独立接口；该入口返回网页网关结果，不等同于分类协议搜索。

**请求体**（模型 `Search.Request`：搜索参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `a8_scene` | integer(int32) |  | GetA8Key 场景；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `category` | string |  | 搜索分类；Query 接口必填，独立分类接口由路由自动设置 | all |
| `code_type` | integer(int32) |  | GetA8Key 代码类型；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `code_version` | integer(int32) |  | GetA8Key 代码版本；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `cursor` | string |  | 续页游标；首页留空，续页原样传回上一页的 cursor |  |
| `include_raw` | boolean |  | 是否在响应中附带微信原始数据；调试时才建议开启 | False |
| `limit` | integer(int64) |  | 每页数量，建议 10，最大值由微信服务决定 | 10 |
| `offset` | integer(int64) |  | 结果偏移量；首页传 0，续页传上一页返回的 next_offset | 0 |
| `opcode` | integer(int32) |  | GetA8Key 操作码；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `path` | string |  | 网页网关路径；仅 Gateway 使用，留空采用默认搜索路径 | page/search/mobile_jump |
| `protocol_scene` | integer(int64) |  | 协议场景值；普通搜索使用 0 让服务自动选择 | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |
| `scene` | integer(int64) |  | 网页网关场景；仅 Gateway 使用，默认 4812 | 4812 |
| `search_id` | string |  | 续页标识；首页留空，续页原样传回上一页的 search_id |  |
| `type` | integer(int64) |  | 网页网关搜索类型；仅 Gateway 使用，默认 53 | 53 |

请求示例：

```json
{
 "query": "深圳科技",
 "path": "page/search/mobile_jump",
 "scene": 4812,
 "type": 53,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Images`

图片搜索 

固定使用 image 分类搜索图片内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "深圳夜景",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Listen`

听一听搜索 

固定使用 listen 分类搜索音频内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "科技播客",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Live`

直播搜索 

固定使用 live 分类搜索直播内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "科技直播",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/MiniGames`

小游戏搜索 

固定使用 mini_game 分类搜索小游戏，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "休闲游戏",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/MiniPrograms`

小程序搜索 

固定使用 mini_program 分类，返回 appid、username、名称、简介和图标，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "乘车码",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Moments`

朋友圈搜索 

搜索当前协议账号可检索的朋友圈内容，返回正文、位置、时间、图片或视频 media 列表，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "周末徒步",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/News`

新闻搜索 

固定使用 news 分类搜索新闻内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "科技新闻",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/OfficialAccounts`

公众号与账号搜索 

搜索公众号、相关小程序及视频号账号；结果通过 result_type 区分，并返回 account_id、account_name、wechat_id、认证信息和菜单。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "腾讯科技",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Query`

通用分类搜索 

给需要动态指定 category 的调用方使用，一套接口支持 all、article、official_account、channels、mini_program、moments 等分类。已有明确业务类型时优先调用对应独立接口。首页 offset=0；续页原样传回上一页的 search_id、cursor，并使用 next_offset。

**请求体**（模型 `Search.Request`：首页：query/category/offset/limit；翻页：再传 search_id/cursor）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `a8_scene` | integer(int32) |  | GetA8Key 场景；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `category` | string |  | 搜索分类；Query 接口必填，独立分类接口由路由自动设置 | all |
| `code_type` | integer(int32) |  | GetA8Key 代码类型；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `code_version` | integer(int32) |  | GetA8Key 代码版本；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `cursor` | string |  | 续页游标；首页留空，续页原样传回上一页的 cursor |  |
| `include_raw` | boolean |  | 是否在响应中附带微信原始数据；调试时才建议开启 | False |
| `limit` | integer(int64) |  | 每页数量，建议 10，最大值由微信服务决定 | 10 |
| `offset` | integer(int64) |  | 结果偏移量；首页传 0，续页传上一页返回的 next_offset | 0 |
| `opcode` | integer(int32) |  | GetA8Key 操作码；仅 Gateway 高级调试使用，0 表示默认值 | 0 |
| `path` | string |  | 网页网关路径；仅 Gateway 使用，留空采用默认搜索路径 | page/search/mobile_jump |
| `protocol_scene` | integer(int64) |  | 协议场景值；普通搜索使用 0 让服务自动选择 | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |
| `scene` | integer(int64) |  | 网页网关场景；仅 Gateway 使用，默认 4812 | 4812 |
| `search_id` | string |  | 续页标识；首页留空，续页原样传回上一页的 search_id |  |
| `type` | integer(int64) |  | 网页网关搜索类型；仅 Gateway 使用，默认 53 | 53 |

请求示例：

```json
{
 "query": "人工智能",
 "category": "all",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Service/{name}`

高级搜索能力调用入口 

根据 Services 返回的 name 调用对应搜索业务能力，主要用于协议调试和扩展能力接入。常规分类搜索使用 Query 或对应独立业务接口；该入口请求体由具体能力决定。

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| path | `name` | string | 是 | 服务名称 |

**请求体**（模型 `Search.CGICallRequest`：协议服务参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否在响应中附带原始协议数据 | False |
| `method` | string |  | 仅 HTTP 类服务需要；留空使用 Services 返回的默认方法 | POST |
| `payload` | object |  | 服务要求的 JSON 请求对象；字段由 Services 返回的具体能力决定 |  |
| `payload_base64` | string |  | 原始二进制请求的 Base64；与 payload、payload_hex 三选一 |  |
| `payload_hex` | string |  | 原始二进制请求的十六进制；与 payload、payload_base64 三选一 |  |

请求示例：

```json
{
 "payload": {
  "query": "深圳科技"
 },
 "include_raw": false,
 "method": "POST"
}
```

**响应**：`models.ResponseResult`

#### `GET /Search/Services`

查看高级搜索能力目录 

返回 Service/{name} 高级调用入口支持的业务能力名称、请求方式和输入类型。面向协议调试及尚未封装成独立路由的能力，日常文章、公众号、视频号、小程序和朋友圈搜索使用独立接口。

**响应**：`models.ResponseResult`

#### `POST /Search/Stickers`

贴图搜索 

固定使用 sticker 分类搜索贴图内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "谢谢",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/Underlines`

划线搜索 

固定使用 underline 分类搜索划线内容，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "架构",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

#### `POST /Search/WeChatIndex`

微信指数搜索 

固定使用 wechat_index 分类查询微信指数相关结果，支持协议分页。

**请求体**（模型 `Search.VerticalFirstPageRequest`：可直接执行的首页示例；续页追加上一页返回的 search_id、cursor，并把 offset 改为 next_offset）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `include_raw` | boolean |  | 是否附带微信原始数据；普通调用保持 false | False |
| `limit` | integer(int64) |  | 每页数量，范围 1-100 | 10 |
| `offset` | integer(int64) |  | 首页固定传 0；续页改传上一页返回的 next_offset | 0 |
| `query` | string |  | 搜索关键词，至少 2 个字符 | 深圳科技 |

请求示例：

```json
{
 "query": "人工智能",
 "offset": 0,
 "limit": 10,
 "include_raw": false
}
```

**响应**：`models.ResponseResult`

---

### TenPay（12 个接口）

#### `POST /TenPay/Collectmoney`

确认收款 

**请求体**（模型 `TenPay.CollectmoneyModel`：转账与交易标识）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `invalidTime` | string |  | 收款请求失效时间 | 0 |
| `toUserName` | string |  | 付款方微信标识 | wxid_payer |
| `transFerId` | string |  | 转账标识 | transfer_id_from_message |
| `transactionId` | string |  | 交易标识 | transaction_id_from_message |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

请求示例：

```json
{
 "invalidTime": "0",
 "toUserName": "wxid_payer",
 "transFerId": "transfer_id_from_message",
 "transactionId": "transaction_id_from_message",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/ConfirmPreTransferApi`

确认支付 

**请求体**（模型 `TenPay.ConfirmPreTransfer`：预支付返回参数与支付密码）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `bankSerial` | string |  | 预支付响应中的银行卡序列号 | bank_serial_from_pre_transfer |
| `bankType` | string |  | 预支付响应中的银行类型 | CFT |
| `payPassword` | string |  | 支付密码；只通过 HTTPS 提交，不记录日志 | your_pay_password |
| `reqKey` | string |  | 预支付响应中的请求密钥 | req_key_from_pre_transfer |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

请求示例：

```json
{
 "bankSerial": "bank_serial_from_pre_transfer",
 "bankType": "CFT",
 "payPassword": "your_pay_password",
 "reqKey": "req_key_from_pre_transfer",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/GeMaSkdPayQCode`

自定义经营个人收款单 

**请求体**（模型 `TenPay.GeMaSkdPayQCodeParam`：注意参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Money` | string |  |  |  |
| `Name` | string |  |  |  |
| `Remark` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Money": "示例值",
 "Name": "示例值",
 "Remark": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/GeneratePayQCode`

生成自定义收款二维码 

**请求体**（模型 `TenPay.GeneratePayQCodeModel`：收款名称与金额）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `money` | string |  | 收款金额，单位元，最多两位小数 | 1.00 |
| `name` | string |  | 收款项目名称 | 商品款 |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

请求示例：

```json
{
 "money": "1.00",
 "name": "商品款",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/GetEncryptInfo`

获取加密信息 

**响应**：`models.ResponseResult`

#### `POST /TenPay/GetRedPacketListApi`

查看红包领取列表入口 

**请求体**（模型 `TenPay.HongBaoDetail`：红包消息 XML、分页 offset 和 size）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `offset` | integer(int64) |  | 领取记录分页偏移 | 0 |
| `size` | integer(int64) |  | 领取记录分页数量 | 20 |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |
| `xml` | string |  | 红包消息中的原始 XML | <msg></msg> |

请求示例：

```json
{
 "offset": 0,
 "size": 20,
 "wxid": "wxid_bound_by_access_token",
 "xml": "<msg></msg>"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/OpenHongBao`

抢红包(带参数) 

**请求体**（模型 `TenPay.HongBaoTailParam`：注意参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `SendId` | string |  |  |  |
| `SendUserName` | string |  |  |  |
| `TimingIdentifier` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

请求示例：

```json
{
 "SendId": "sendid_from_previous_response",
 "SendUserName": "示例值",
 "TimingIdentifier": "示例值",
 "Wxid": "wxid_bound_by_access_token",
 "Xml": "<msg></msg>"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/Openwxhb`

拆开红包 

**请求体**（模型 `TenPay.OpenwxhbParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Encrypt_key` | string |  |  |  |
| `Encrypt_userinfo` | string |  |  |  |
| `SendUserName` | string |  |  |  |
| `TimingIdentifier` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

请求示例：

```json
{
 "Encrypt_key": "your_encrypt_key",
 "Encrypt_userinfo": "示例值",
 "SendUserName": "示例值",
 "TimingIdentifier": "示例值",
 "Wxid": "wxid_bound_by_access_token",
 "Xml": "<msg></msg>"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/Qrydetailwxhb`

查看红包 

**请求体**（模型 `TenPay.QrydetailwxhbParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Encrypt_key` | string |  |  |  |
| `Encrypt_userinfo` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

请求示例：

```json
{
 "Encrypt_key": "your_encrypt_key",
 "Encrypt_userinfo": "示例值",
 "Wxid": "wxid_bound_by_access_token",
 "Xml": "<msg></msg>"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/Receivewxhb`

打开红包不用key 

**请求体**（模型 `TenPay.ReceivewxhbParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Encrypt_key` | string |  |  |  |
| `Encrypt_userinfo` | string |  |  |  |
| `InWay` | string |  |  |  |
| `Wxid` | string |  |  |  |
| `Xml` | string |  |  |  |

请求示例：

```json
{
 "Encrypt_key": "your_encrypt_key",
 "Encrypt_userinfo": "示例值",
 "InWay": "示例值",
 "Wxid": "wxid_bound_by_access_token",
 "Xml": "<msg></msg>"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/SjSkdPayQCode`

自定义商家收款单 

**请求体**（模型 `TenPay.SjSkdPayQCodeParam`：注意参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Money` | string |  |  |  |
| `Name` | string |  |  |  |
| `Remark` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Money": "示例值",
 "Name": "示例值",
 "Remark": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /TenPay/WXCreateRedPacketApi`

创建红包 

**请求体**（模型 `TenPay.RedPacket`：红包类型、接收人、数量、金额和祝福语）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `amount` | integer(int32) |  | 红包总金额，单位分 | 100 |
| `content` | string |  | 红包祝福语 | 恭喜发财 |
| `count` | integer(int32) |  | 红包个数 | 1 |
| `from` | integer(int32) |  | 红包来源场景 | 0 |
| `redType` | integer(int32) |  | 红包类型 | 0 |
| `username` | string |  | 接收人微信标识；群红包填写群 ID | wxid_recipient |
| `wxid` | string |  | 账号由 Access Token 绑定关系解析，普通调用无需提交 |  |

请求示例：

```json
{
 "amount": 100,
 "content": "恭喜发财",
 "count": 1,
 "from": 0,
 "redType": 0,
 "username": "wxid_recipient",
 "wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

---

### Tools（15 个接口）

#### `POST /Tools/CdnDownloadImage`

通过CDN下载微信图片 

从 image.cdn_download_contexts 选择 original、standard 或 thumbnail 对象并原样提交；必填 file_no 与 file_aes_key，响应 Data.Image 为解密后的图片 Base64。

**请求体**（模型 `Tools.CdnDownloadImageParamDoc`：直接提交 image.cdn_download_contexts 中所需清晰度的对象）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `file_aes_key` | string |  |  |  |
| `file_no` | string |  |  |  |

请求示例：

```json
{
 "variant": "original",
 "file_no": "cdn_file_no_from_image_message",
 "file_aes_key": "aes_key_from_image_message"
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/DownloadFile`

下载微信文件分片 

将收到消息的 file.download_context 原样作为请求体；必填 attach_id、user_name、data_len、section.start_pos、section.data_len，app_id 为空时保持空字符串。群聊 user_name 使用 xxx@chatroom。

**请求体**（模型 `Tools.DownloadAppAttachParamDoc`：直接提交 file.download_context）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `app_id` | string |  |  |  |
| `attach_id` | string |  |  |  |
| `data_len` | integer(int64) |  |  |  |
| `section` | `Tools.DownloadSectionDoc` |  |  |  |
| `user_name` | string |  |  |  |

请求示例：

```json
{
 "app_id": "file_app_id",
 "attach_id": "media_attach_id",
 "user_name": "123456789@chatroom",
 "data_len": 2500000,
 "section": {
  "start_pos": 0,
  "data_len": 1048576
 }
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/DownloadImg`

下载微信图片分片 

将收到消息的 image.download_context 原样作为请求体；必填 to_wxid、msg_id、data_len、section.start_pos、section.data_len。群聊 to_wxid 使用 xxx@chatroom。每次按实际返回字节数推进 start_pos，直到达到 data_len。

**请求体**（模型 `Tools.DownloadParamDoc`：直接提交 image.download_context）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `compress_type` | integer(int64) |  |  |  |
| `data_len` | integer(int64) |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `section` | `Tools.DownloadSectionDoc` |  |  |  |
| `to_wxid` | string |  |  |  |

请求示例：

```json
{
 "to_wxid": "123456789@chatroom",
 "msg_id": 123456789,
 "data_len": 2400000,
 "section": {
  "start_pos": 0,
  "data_len": 1048576
 },
 "compress_type": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/DownloadVideo`

下载微信视频分片 

将收到消息的 video.download_context 原样作为请求体；必填 msg_id、data_len、section.start_pos、section.data_len。每次按实际返回字节数推进 start_pos，直到达到 data_len。

**请求体**（模型 `Tools.DownloadParamDoc`：直接提交 video.download_context）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `compress_type` | integer(int64) |  |  |  |
| `data_len` | integer(int64) |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `section` | `Tools.DownloadSectionDoc` |  |  |  |
| `to_wxid` | string |  |  |  |

请求示例：

```json
{
 "to_wxid": "wxid_sender",
 "msg_id": 123456789,
 "data_len": 3145728,
 "section": {
  "start_pos": 0,
  "data_len": 1048576
 },
 "compress_type": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/DownloadVoice`

语音下载 

**请求体**（模型 `Tools.DownloadVoiceParamDoc`：注意参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `bufid` | string |  |  |  |
| `fromUserName` | string |  |  |  |
| `length` | integer(int64) |  |  |  |
| `msgId` | integer(int32) |  |  |  |

请求示例：

```json
{
 "bufid": "bufid_from_previous_response",
 "fromUserName": "示例值",
 "length": 0,
 "msgId": 0
}
```

**响应**：`models.ResponseResult`

#### `GET /Tools/GeneratePayQCode`

生成支付二维码 

**响应**：`models.ResponseResult`

#### `POST /Tools/GetA8Key`

GetA8Key 

**请求体**（模型 `Tools.GetA8KeyParamDoc`：OpCode == 2 Scene == 4 CodeType == 19 CodeVersion == 5 以上是默认参数,如有需求自行修改）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `codeType` | integer(int32) |  |  |  |
| `codeVersion` | integer(int32) |  |  |  |
| `cookieBase64` | string |  |  |  |
| `flag` | integer(int32) |  |  |  |
| `netType` | string |  |  |  |
| `opCode` | integer(int32) |  |  |  |
| `reqUrl` | string |  |  |  |
| `scene` | integer(int32) |  |  |  |

请求示例：

```json
{
 "codeType": 0,
 "codeVersion": 0,
 "cookieBase64": "示例值",
 "flag": 0,
 "netType": "示例值",
 "opCode": 0,
 "reqUrl": "https://example.com",
 "scene": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/GetBandCardList`

获取余额以及银行卡信息 

**响应**：`models.ResponseResult`

#### `POST /Tools/GetBoundHardDevices`

GetBoundHardDevices 

**响应**：`models.ResponseResult`

#### `POST /Tools/GetCdnDns`

获取CDN服务器dns信息 

**响应**：`models.ResponseResult`

#### `POST /Tools/HelperVerification`

OauthSdkApp 

**请求体**（模型 `Tools.HelperVerificationParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `gcc` | string |  |  |  |
| `mobile` | string |  |  |  |

请求示例：

```json
{
 "gcc": "示例值",
 "mobile": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/OauthSdkApp`

OauthSdkApp 

**请求体**（模型 `Tools.OauthSdkAppParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `avatarId` | integer(int32) |  |  |  |
| `opt` | integer(int32) |  |  |  |
| `packageName` | string |  |  |  |
| `state` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "avatarId": 0,
 "opt": 0,
 "packageName": "示例值",
 "state": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/ThirdAppGrant`

第三方APP授权 

**请求体**（模型 `Tools.ThirdAppGrantParamDoc`：注意参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `url` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/UploadFile`

文件上传 

**请求体**（模型 `Tools.UploadParamDoc`：文件上传）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `base64` | string |  |  |  |

请求示例：

```json
{
 "base64": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Tools/setproxy`

修改微信步数 

**请求体**（模型 `Tools.SetStepParamDoc`：步数，最高支持98000）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `step` | integer(int64) |  |  |  |

请求示例：

```json
{
 "step": 0
}
```

**响应**：`models.ResponseResult`

---

### Translate（2 个接口）

#### `POST /Translate/Send`

翻译并发送文字 

先翻译文字，再使用 authcode 绑定账号的项目协议登录态向指定联系人或群发送译文。

**请求体**（模型 `Translate.SendRequest`：文字、目标语言和接收方）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `at` | string |  |  |  |
| `source_lang` | string |  |  |  |
| `target_lang` | string |  |  |  |
| `text` | string |  |  |  |
| `to_wxid` | string |  |  |  |

请求示例：

```json
{
 "at": "示例值",
 "source_lang": "示例值",
 "target_lang": "示例值",
 "text": "你好",
 "to_wxid": "to_wxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Translate/Text`

文字翻译 

翻译待发送或已接收的文字，source_lang 留空时自动识别原语言。

**请求体**（模型 `Translate.TextRequest`：文字和目标语言）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `source_lang` | string |  |  |  |
| `target_lang` | string |  |  |  |
| `text` | string |  |  |  |

请求示例：

```json
{
 "source_lang": "示例值",
 "target_lang": "示例值",
 "text": "你好"
}
```

**响应**：`models.ResponseResult`

---

### User（18 个接口）

#### `POST /User/BindQQ`

绑定QQ 

**请求体**（模型 `User.BindQQParam`：account:QQ账号, password:QQ密码）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Account` | integer(int32) |  |  |  |
| `Password` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Account": 0,
 "Password": "your_password",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/BindingEmail`

绑定邮箱 

**请求体**（模型 `User.EmailParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Email` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Email": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/BindingMobile`

换绑手机号 

**请求体**（模型 `User.BindMobileParam`：Mobile == 格式：+8617399999999 Verifycode == 验证码请先通过(发送手机验证码)获取）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Mobile` | string |  |  |  |
| `Verifycode` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Mobile": "示例值",
 "Verifycode": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `GET /User/CheckCanSetAlias`

检测微信登录环境 

**响应**：`models.ResponseResult`

#### `POST /User/DelSafetyInfo`

删除登录设备 

**请求体**（模型 `User.DelSafetyInfoParam`：UUID请在登录设备管理中获取）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Uuid` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Uuid": "uuid_from_qr_response",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `GET /User/GetAllOnline`

获取所有在线wxid（需管理员 key） 

**参数**

| 位置 | 参数 | 类型 | 必填 | 说明 |
|---|---|---|---|---|
| header | `X-Admin-Token` | string | 否 | 管理员凭证；仅在开启管理员接口后使用 |

**响应**：`models.ResponseResult`

#### `POST /User/GetContractProfile`

取个人信息 

**响应**：`models.ResponseResult`

#### `GET /User/GetOnlineInfo`

获取在线信息 

**响应**：`models.ResponseResult`

#### `POST /User/GetQRCode`

取个人二维码 

**请求体**（模型 `User.GetQRCodeParam`：Style == 二维码样式(请自行探索) 8默认）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Style` | integer(int32) |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Style": 0,
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/GetSafetyInfo`

登录设备管理 

**响应**：`models.ResponseResult`

#### `POST /User/PrivacySettings`

隐私设置 

**请求体**（模型 `User.PrivacySettingsParam`：核心参数请联系客服获取代码列表）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Function` | integer(int32) |  |  |  |
| `Value` | integer(int32) |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Function": 0,
 "Value": 0,
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/ReportMotion`

ReportMotion 

**请求体**（模型 `User.ReportMotionParam`：具体用法请联系客服）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `DeviceId` | string |  |  |  |
| `DeviceType` | string |  |  |  |
| `StepCount` | integer(int64) |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "DeviceId": "device_id_from_login",
 "DeviceType": "示例值",
 "StepCount": 0,
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/SendVerifyMobile`

发送手机验证码 

**请求体**（模型 `User.SendVerifyMobileParam`：Opcode == 场景(18代表绑手机号) Mobile == 格式：+8617399999999）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Mobile` | string |  |  |  |
| `Opcode` | integer(int32) |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Mobile": "示例值",
 "Opcode": 0,
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/SetAlisa`

设置微信号 

**请求体**（模型 `User.SetAlisaParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Alisa` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Alisa": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/SetPasswd`

修改密码 

**请求体**（模型 `User.NewSetPasswdParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `NewPassword` | string |  |  |  |
| `Ticket` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "NewPassword": "示例值",
 "Ticket": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/UpdateProfile`

修改个人信息 

**请求体**（模型 `User.UpdateProfileParam`：NickName ==名称  Sex == 性别（1:男 2：女） Country == 国家,例如：CH Province == 省份 例如:WuHan Signature == 个性签名）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `City` | string |  |  |  |
| `Country` | string |  |  |  |
| `NickName` | string |  |  |  |
| `Province` | string |  |  |  |
| `Sex` | integer(int32) |  |  |  |
| `Signature` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "City": "示例值",
 "Country": "示例值",
 "NickName": "示例值",
 "Province": "示例值",
 "Sex": 0,
 "Signature": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/UploadHeadImage`

修改头像 

**请求体**（模型 `User.UploadHeadImageParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Base64` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Base64": "示例值",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /User/VerifyPasswd`

验证密码 

**请求体**（模型 `User.NewVerifyPasswdParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Password` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Password": "your_password",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

---

### Voice（3 个接口）

#### `POST /Voice/MessageTranscribe`

接收到的语音消息转文字 

根据消息同步得到的消息标识，通过 authcode 绑定账号的项目 Mac 协议登录态分片下载语音，再上传转写并轮询文字结果。优先传 new_msg_id；旧消息可传 msg_id；群语音同时传 chat_room_name；同步消息含 master_buf_id 时一并传入。

**请求体**（模型 `Voice.MessageRequest`：接收语音的消息标识与转写参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `bits_per_sample` | integer(int32) |  |  |  |
| `chat_room_name` | string |  |  |  |
| `client_msg_id` | string |  |  |  |
| `encode_type` | integer(int32) |  |  |  |
| `file_type` | integer(int32) |  |  |  |
| `from_user_name` | string |  |  |  |
| `length` | integer(int64) |  |  |  |
| `master_buf_id` | `Voice.DecimalInt64` |  |  |  |
| `msg_id` | integer(int32) |  |  |  |
| `new_msg_id` | `Voice.DecimalInt64` |  |  |  |
| `poll_interval_ms` | integer(int64) |  |  |  |
| `sample_rate` | integer(int32) |  |  |  |
| `scene` | integer(int32) |  |  |  |
| `to_user_name` | string |  |  |  |
| `voice_id` | string |  |  |  |
| `wait_seconds` | integer(int64) |  |  |  |

请求示例：

```json
{
 "bits_per_sample": 0,
 "chat_room_name": "示例值",
 "client_msg_id": "client_msg_id_from_previous_response",
 "encode_type": 0,
 "file_type": 0,
 "from_user_name": "示例值",
 "length": 0,
 "master_buf_id": "master_buf_id_from_previous_response",
 "msg_id": 0,
 "new_msg_id": "new_msg_id_from_previous_response",
 "poll_interval_ms": 0,
 "sample_rate": 0,
 "scene": 0,
 "to_user_name": "示例值",
 "voice_id": "voice_id_from_previous_response",
 "wait_seconds": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Voice/Result`

查询异步语音转写结果 

使用 Transcribe 返回的 voice_id 查询转写进度；complete=true 表示结果结束，text 为当前识别文字，retry_after_ms 为建议查询间隔。

**请求体**（模型 `Voice.ResultRequest`：voice_id）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `voice_id` | string |  |  |  |

请求示例：

```json
{
 "voice_id": "voice_id_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Voice/Transcribe`

上传语音并转成文字 

使用 authcode 绑定账号的项目 Mac 协议登录态完成语音分片上传和结果轮询。传入 audio_base64 提交新任务；wait_seconds 大于 0 时同步等待，等于 0 时返回 voice_id 供 Result 查询。音频属性需与实际编码保持一致。

**请求体**（模型 `Voice.Request`：音频与转写参数）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `audio_base64` | string |  |  |  |
| `bits_per_sample` | integer(int32) |  |  |  |
| `chunk_size` | integer(int64) |  |  |  |
| `encode_type` | integer(int32) |  |  |  |
| `file_type` | integer(int32) |  |  |  |
| `from_user_name` | string |  |  |  |
| `poll_interval_ms` | integer(int64) |  |  |  |
| `sample_rate` | integer(int32) |  |  |  |
| `scene` | integer(int32) |  |  |  |
| `to_user_name` | string |  |  |  |
| `voice_id` | string |  |  |  |
| `wait_seconds` | integer(int64) |  |  |  |

请求示例：

```json
{
 "audio_base64": "示例值",
 "bits_per_sample": 0,
 "chunk_size": 0,
 "encode_type": 0,
 "file_type": 0,
 "from_user_name": "示例值",
 "poll_interval_ms": 0,
 "sample_rate": 0,
 "scene": 0,
 "to_user_name": "示例值",
 "voice_id": "voice_id_from_previous_response",
 "wait_seconds": 0
}
```

**响应**：`models.ResponseResult`

---

### Webhook（6 个接口）

#### `GET /Webhook/Business/Get`

获取业务回调URL（按授权码） 

curl 示例：curl "http://0.0.0.1:8057/api/Webhook/Business/Get?authcode=ac123"

**响应**：`models.ResponseResult`

#### `POST /Webhook/Business/Set`

设置业务回调URL（按授权码） 

curl -X POST "http://0.0.0.0:8057/api/Webhook/Business/Set?authcode=ac123" -H "Content-Type: application/json" -d '{"syncMessageUrl":"http://127.0.0.1:6999/wic/wechat/{authcode}/SyncMessage","logoutUrl":"http://127.0.0.1:6999/wic/wechat/{authcode}/logoutSys"}'

**请求体**（模型 `businesscfg.BusinessConfig`：回调配置）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `eventUrl` | string |  |  |  |
| `logoutUrl` | string |  |  |  |
| `syncMessageUrl` | string |  |  |  |

请求示例：

```json
{
 "eventUrl": "https://example.com",
 "logoutUrl": "https://example.com",
 "syncMessageUrl": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `GET /Webhook/Get`

获取 Webhook 配置（按授权码） 

**响应**：`models.ResponseResult`

#### `POST /Webhook/Remove`

删除 Webhook 配置（按授权码） 

**响应**：`models.ResponseResult`

#### `POST /Webhook/Set`

设置 Webhook 配置（按授权码） 

const crypto = require('crypto');\nconst ok = (body, secret) => {\n  const bases = `${body.Wxid}:${body.MessageType}:${body.Timestamp}`;\n  const expect = crypto.createHmac('sha256', secret).update(bases).digest('hex');\n  return expect === body.Signature;\n}

**请求体**（模型 `webhook.WebhookConfig`：配置：url/secret/filters等）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `enabled` | boolean |  |  |  |
| `enabledSet` | boolean |  |  |  |
| `includeSelfMessage` | boolean |  |  |  |
| `messageTypes` | array<string> |  |  |  |
| `retryCount` | integer(int64) |  |  |  |
| `retryCountSet` | boolean |  |  |  |
| `secret` | string |  |  |  |
| `timeout` | integer(int64) |  |  |  |
| `url` | string |  |  |  |

请求示例：

```json
{
 "enabled": false,
 "enabledSet": false,
 "includeSelfMessage": false,
 "messageTypes": [
  "示例值"
 ],
 "retryCount": 0,
 "retryCountSet": false,
 "secret": "your_secret",
 "timeout": 0,
 "url": "https://example.com"
}
```

**响应**：`models.ResponseResult`

#### `POST /Webhook/Test`

测试发送 Webhook 消息（按授权码） 

响应：发送成功返回 OK；失败返回错误信息。

**请求体**（模型 `models.WebhookTestRequest`：测试请求体）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `MessageType` | string |  |  |  |
| `TestData` | object |  | 业务响应数据；结构由具体接口决定 |  |

请求示例：

```json
{
 "MessageType": "示例值",
 "TestData": {}
}
```

**响应**：`models.ResponseResult`

---

### Wxapp（20 个接口）

#### `POST /Wxapp/AddAvatar`

AddAvatar 

**请求体**（模型 `Wxapp.AddAvatarParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `aFilekey` | string |  |  |  |
| `appid` | string |  |  |  |
| `nickName` | string |  |  |  |

请求示例：

```json
{
 "aFilekey": "your_afilekey",
 "appid": "wx1234567890abcdef",
 "nickName": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/AddMobile`

小程序绑定增加手机号 

**请求体**（模型 `Wxapp.CheckVerifyCodeDataDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `mobile` | string |  |  |  |
| `verifyCode` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "mobile": "示例值",
 "verifyCode": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/CloudCallFunction`

小程序云函数 

**请求体**（模型 `Wxapp.CloudCallParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `data` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "data": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/DelMobile`

小程序删除手机号 

**请求体**（模型 `Wxapp.DelMobileDataDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `mobile` | string |  |  |  |
| `opcode` | integer(int64) |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "mobile": "示例值",
 "opcode": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/DellAvatar`

DellAvatar 

**请求体**（模型 `Wxapp.DellAvatarParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `avatarId` | integer(int32) |  |  |  |

请求示例：

```json
{
 "avatarId": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/GETCreditScoreParam`

查询游戏信用积分 

**请求体**（模型 `Wxapp.GETCreditScoreParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/GetAllMobile`

GetAllMobile 

**请求体**（模型 `Wxapp.JSOperateWxParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `data` | string |  |  |  |
| `opt` | integer(int64) |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "data": "示例值",
 "opt": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/GetRandomAvatar`

GetRandomAvatar 

**请求体**（模型 `Wxapp.DefaultParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/GetUnionPay`

微信云闪付支付 

示例：{"appid":"wx123...","sessionid":"xxx","timeStamp":"1700000000","nonceStr":"abc","package":"prepay_id=...","paySign":"xxx"}

**请求体**（模型 `Wxapp.UnionpayDataDoc`：支付请求数据）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `nonceStr` | string |  |  |  |
| `package` | string |  |  |  |
| `paySign` | string |  |  |  |
| `sessionid` | string |  |  |  |
| `timeStamp` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "nonceStr": "示例值",
 "package": "示例值",
 "paySign": "示例值",
 "sessionid": "sessionid_from_previous_response",
 "timeStamp": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/GetUserOpenId`

GetUserOpenId 

示例：{"toWxId":"wxid_xxx","appid":"wx1234567890abcdef"}

**请求体**（模型 `Wxapp.GetUserOpenIdParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `toWxId` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "toWxId": "towxid_from_previous_response"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/GetWxAppRecord`

获取小程序记录 

**请求体**（模型 `Wxapp.GetWxAppRecordParamDoc`：获取小程序记录）

_（无字段说明）_

请求示例：

```json
{}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/JSGetSessionid`

小程序获取小程序支付sessionid 

**请求体**（模型 `Wxapp.DefaultParam`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `Appid` | string |  |  |  |
| `Wxid` | string |  |  |  |

请求示例：

```json
{
 "Appid": "wx1234567890abcdef",
 "Wxid": "wxid_bound_by_access_token"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/JSLogin`

授权小程序(定制) 

示例：{"appid":"wx1234567890abcdef"}

**请求体**（模型 `Wxapp.DefaultParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/JSOperateWxData`

小程序操作 

**请求体**（模型 `Wxapp.JSOperateWxParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `data` | string |  |  |  |
| `opt` | integer(int64) |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "data": "示例值",
 "opt": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/UploadAvatarImg`

UploadAvatarImg 

**请求体**（模型 `Wxapp.AddAvatarImgParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `jpgLink` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "jpgLink": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/Verifyplugin`

小程序获取HostSign 

**请求体**（模型 `Wxapp.JSOperateWxParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `data` | string |  |  |  |
| `opt` | integer(int64) |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "data": "示例值",
 "opt": 0
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/Wxapp/AddWxAppRecord`

新增小程序记录 

**请求体**（模型 `Wxapp.AddWxAppRecordParamDoc`）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `username` | string |  |  |  |

请求示例：

```json
{
 "username": "wxid_recipient"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/Wxapp/GetpullPay`

推送小程序支付 

**请求体**（模型 `Wxapp.GetpullPayParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `nonceStr` | string |  |  |  |
| `package` | string |  |  |  |
| `paySign` | string |  |  |  |
| `sessionid` | string |  |  |  |
| `timeStamp` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "nonceStr": "示例值",
 "package": "示例值",
 "paySign": "示例值",
 "sessionid": "sessionid_from_previous_response",
 "timeStamp": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/Wxapp/JSGetSessionidQRcode`

获取付小程序款二维码 

**请求体**（模型 `Wxapp.SessionidQRParamDoc`：true）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `appid` | string |  |  |  |
| `nonceStr` | string |  |  |  |
| `package` | string |  |  |  |
| `paySign` | string |  |  |  |
| `sessionid` | string |  |  |  |
| `timeStamp` | string |  |  |  |

请求示例：

```json
{
 "appid": "wx1234567890abcdef",
 "nonceStr": "示例值",
 "package": "示例值",
 "paySign": "示例值",
 "sessionid": "sessionid_from_previous_response",
 "timeStamp": "示例值"
}
```

**响应**：`models.ResponseResult`

#### `POST /Wxapp/Wxapp/QrcodeAuthLogin`

扫码授权登录app或网页 

**请求体**（模型 `Wxapp.QrcodeAuthLoginParamDoc`）

| 字段 | 类型 | 必填 | 说明 | 示例 |
|---|---|---|---|---|
| `uuid` | string |  |  |  |

请求示例：

```json
{
 "uuid": "uuid_from_qr_response"
}
```

**响应**：`models.ResponseResult`

---

## 附录 A：数据模型索引

全部 190 个模型字段定义见 `docs/WeChatPadPro-API-models.md`。

| 模型 | 说明 |
|---|---|
| `Admin.DelayAuthKeyModel` | DelayAuthKeyModel |
| `Admin.DeleteAuthKeyModel` | DeleteAuthKeyModel |
| `Admin.GenAuthKeyModel` | GenAuthKeyModel |
| `Algorithm.AndroidDeviceInfo` | AndroidDeviceInfo |
| `Customized.WXCTDUniftyAuthParmDoc` | WXCTDUniftyAuthParmDoc |
| `Favor.DelParamDoc` | DelParamDoc |
| `Favor.GetFavItemParamDoc` | GetFavItemParamDoc |
| `Favor.SyncParamDoc` | SyncParamDoc |
| `Finder.CommentParamDoc` | CommentParamDoc |
| `Finder.DecryptParamDoc` | DecryptParamDoc |
| `Finder.DefaultParamDoc` | DefaultParamDoc |
| `Finder.FinderGetMsgSessionIdParamDoc` | FinderGetMsgSessionIdParamDoc |
| `Finder.FinderGetTopicListParamDoc` | FinderGetTopicListParamDoc |
| `Finder.FinderJoinLiveParamDoc` | FinderJoinLiveParamDoc |
| `Finder.FinderLiveDetailParamDoc` | FinderLiveDetailParamDoc |
| `Finder.FinderSendTextParamDoc` | FinderSendTextParamDoc |
| `Finder.GetCommentDetailParamDoc` | GetCommentDetailParamDoc |
| `Finder.LikeParamDoc` | LikeParamDoc |
| `Finder.TargetUserPageParamDoc` | TargetUserPageParamDoc |
| `Friend.BlacklistParamDoc` | BlacklistParamDoc |
| `Friend.DefaultParamDoc` | DefaultParamDoc |
| `Friend.FriendRelationParamDoc` | FriendRelationParamDoc |
| `Friend.GetContractDetailparameterDoc` | GetContractDetailparameterDoc |
| `Friend.GetContractListparameterDoc` | GetContractListparameterDoc |
| `Friend.LbsFindParamDoc` | LbsFindParamDoc |
| `Friend.PassVerifyParamDoc` | PassVerifyParamDoc |
| `Friend.SearchParamDoc` | SearchParamDoc |
| `Friend.SendRequestParamDoc` | SendRequestParamDoc |
| `Friend.SetRemarksParamDoc` | SetRemarksParamDoc |
| `Friend.UploadParamDoc` | UploadParamDoc |
| `FriendCircle.CdnSnsImageUploadParamDoc` | CdnSnsImageUploadParamDoc |
| `FriendCircle.CommentParamDoc` | CommentParamDoc |
| `FriendCircle.DownloadMediaModelDoc` | DownloadMediaModelDoc |
| `FriendCircle.GetCommnetParamDoc` | GetCommnetParamDoc |
| `FriendCircle.GetDetailparameterDoc` | GetDetailparameterDoc |
| `FriendCircle.GetIdDetailParamDoc` | GetIdDetailParamDoc |
| `FriendCircle.GetListParamDoc` | GetListParamDoc |
| `FriendCircle.MessagearameterDoc` | MessagearameterDoc |
| `FriendCircle.MmSnsSyncParamDoc` | MmSnsSyncParamDoc |
| `FriendCircle.OperationParamDoc` | OperationParamDoc |
| `FriendCircle.PrivacySettingsParamDoc` | PrivacySettingsParamDoc |
| `FriendCircle.RequestParamsDoc` | RequestParamsDoc |
| `FriendCircle.SnsPostItemDoc` | SnsPostItemDoc |
| `FriendCircle.SnsUploadParamDoc` | SnsUploadParamDoc |
| `FriendCircle.SnsUploadVideoParamDoc` | SnsUploadVideoParamDoc |
| `Group.AddChatRoomParamDoc` | AddChatRoomParamDoc |
| `Group.ConsentToJoinParamDoc` | ConsentToJoinParamDoc |
| `Group.CreateChatRoomParamDoc` | CreateChatRoomParamDoc |
| `Group.FacingCreateChatRoomParamDoc` | FacingCreateChatRoomParamDoc |
| `Group.GetChatRoomParamDoc` | GetChatRoomParamDoc |
| `Group.MoveContractListParamDoc` | MoveContractListParamDoc |
| `Group.OperateChatRoomAdminParamDoc` | OperateChatRoomAdminParamDoc |
| `Group.OperateChatRoomInfoParamDoc` | OperateChatRoomInfoParamDoc |
| `Group.QuitGroupParamDoc` | QuitGroupParamDoc |
| `Group.ScanIntoGroupParamDoc` | ScanIntoGroupParamDoc |
| `Group.SendPatParamDoc` | SendPatParamDoc |
| `Group.SetChatroomAccessVerifyParamDoc` | SetChatroomAccessVerifyParamDoc |
| `Group.TransferGroupOwnerParamDoc` | TransferGroupOwnerParamDoc |
| `Label.AddParamDoc` | AddParamDoc |
| `Label.DeleteParamDoc` | DeleteParamDoc |
| `Label.UpdateListParamDoc` | UpdateListParamDoc |
| `Label.UpdateNameParamDoc` | UpdateNameParamDoc |
| `Login.A16LoginParam` | A16LoginParam |
| `Login.Data62LoginReq` | Data62LoginReq |
| `Login.Data62SMSAgainReq` | Data62SMSAgainReq |
| `Login.Data62SMSVerifyReq` | Data62SMSVerifyReq |
| `Login.ExtDeviceLoginConfirmParam` | ExtDeviceLoginConfirmParam |
| `Login.GetQRReq` | GetQRReq |
| `Login.MaccodeParam` | MaccodeParam — 旧客户端兼容授权码；推荐使用 X-Access-Token 请求头 |
| `Login.VerificationcodeParam` | VerificationcodeParam |
| `Msg.DefaultParamDoc` | DefaultParamDoc |
| `Msg.QuoteContextDoc` | QuoteContextDoc |
| `Msg.QuoteDoc` | QuoteDoc |
| `Msg.QuoteResponseDoc` | QuoteResponseDoc |
| `Msg.QuoteSendReplyContext` | QuoteSendReplyContext |
| `Msg.QuoteSendResult` | QuoteSendResult |
| `Msg.RevokeMsgParamDoc` | RevokeMsgParamDoc |
| `Msg.SendAppMsgParamDoc` | SendAppMsgParamDoc |
| `Msg.SendEmojiParamDoc` | SendEmojiParamDoc |
| `Msg.SendGroupMassMsgTextParamDoc` | SendGroupMassMsgTextParamDoc |
| `Msg.SendImageMsgParamDoc` | SendImageMsgParamDoc |
| `Msg.SendNewMsgParamDoc` | SendNewMsgParamDoc |
| `Msg.SendVideoMsgParamDoc` | SendVideoMsgParamDoc |
| `Msg.SendVoiceMessageParamDoc` | SendVoiceMessageParamDoc |
| `Msg.ShareCardParamDoc` | ShareCardParamDoc |
| `Msg.ShareLocationParamDoc` | ShareLocationParamDoc |
| `Msg.ShareVideoMsgParamDoc` | ShareVideoMsgParamDoc |
| `Msg.SyncParam2Doc` | SyncParam2Doc |
| `Msg.SyncParamDoc` | SyncParamDoc |
| `OfficialAccounts.AuthMpLoginParam` | AuthMpLoginParam |
| `OfficialAccounts.DefaultParam` | DefaultParam |
| `OfficialAccounts.GetMpHistoryMsgParam` | GetMpHistoryMsgParam |
| `OfficialAccounts.GetkeyParam` | GetkeyParam — 公众号 AppID |
| `OfficialAccounts.QRConnectParam` | QRConnectParam |
| `OfficialAccounts.ReadParam` | ReadParam |
| `QWContact.AddWxAppRecordParam` | AddWxAppRecordParam |
| `QWContact.QWAddContactParam` | QWAddContactParam |
| `QWContact.QWApplyAddContactParam` | QWApplyAddContactParam |
| `SayHello.Model1Param` | Model1Param |
| `SayHello.Model2Param` | Model2Param |
| `SayHello.SendRequestParam1` | SendRequestParam1 |
| `Search.AIFirstPageRequest` | AIFirstPageRequest — 是否附带微信原始数据；普通调用保持 false |
| `Search.CGICallRequest` | CGICallRequest — 是否在响应中附带原始协议数据 |
| `Search.Request` | Request — GetA8Key 场景；仅 Gateway 高级调试使用，0 表示默认值 |
| `Search.VerticalFirstPageRequest` | VerticalFirstPageRequest — 是否附带微信原始数据；普通调用保持 false |
| `TenPay.CollectmoneyModel` | CollectmoneyModel — 收款请求失效时间 |
| `TenPay.ConfirmPreTransfer` | ConfirmPreTransfer — 预支付响应中的银行卡序列号 |
| `TenPay.GeMaSkdPayQCodeParam` | GeMaSkdPayQCodeParam |
| `TenPay.GeneratePayQCodeModel` | GeneratePayQCodeModel — 收款金额，单位元，最多两位小数 |
| `TenPay.HongBaoDetail` | HongBaoDetail — 领取记录分页偏移 |
| `TenPay.HongBaoParam` | HongBaoParam |
| `TenPay.HongBaoTailParam` | HongBaoTailParam |
| `TenPay.OpenwxhbParam` | OpenwxhbParam |
| `TenPay.QrydetailwxhbParam` | QrydetailwxhbParam |
| `TenPay.ReceivewxhbParam` | ReceivewxhbParam |
| `TenPay.RedPacket` | RedPacket — 红包总金额，单位分 |
| `TenPay.SjSkdPayQCodeParam` | SjSkdPayQCodeParam |
| `Tools.CdnDownloadImageParamDoc` | CdnDownloadImageParamDoc |
| `Tools.DownloadAppAttachParamDoc` | DownloadAppAttachParamDoc |
| `Tools.DownloadParamDoc` | DownloadParamDoc |
| `Tools.DownloadSectionDoc` | DownloadSectionDoc |
| `Tools.DownloadVoiceParamDoc` | DownloadVoiceParamDoc |
| `Tools.GetA8KeyParamDoc` | GetA8KeyParamDoc |
| `Tools.HelperVerificationParamDoc` | HelperVerificationParamDoc |
| `Tools.OauthSdkAppParamDoc` | OauthSdkAppParamDoc |
| `Tools.SetProxyParamDoc` | SetProxyParamDoc |
| `Tools.SetStepParamDoc` | SetStepParamDoc |
| `Tools.ThirdAppGrantParamDoc` | ThirdAppGrantParamDoc |
| `Tools.UploadParamDoc` | UploadParamDoc |
| `Translate.SendRequest` | SendRequest |
| `Translate.TextRequest` | TextRequest |
| `User.BindMobileParam` | BindMobileParam |
| `User.BindQQParam` | BindQQParam |
| `User.DelSafetyInfoParam` | DelSafetyInfoParam |
| `User.EmailParam` | EmailParam |
| `User.GetQRCodeParam` | GetQRCodeParam |
| `User.NewSetPasswdParam` | NewSetPasswdParam |
| `User.NewVerifyPasswdParam` | NewVerifyPasswdParam |
| `User.PrivacySettingsParam` | PrivacySettingsParam |
| `User.ReportMotionParam` | ReportMotionParam |
| `User.SendVerifyMobileParam` | SendVerifyMobileParam |
| `User.SetAlisaParam` | SetAlisaParam |
| `User.UpdateProfileParam` | UpdateProfileParam |
| `User.UploadHeadImageParam` | UploadHeadImageParam |
| `Voice.DecimalInt64` | DecimalInt64 |
| `Voice.MessageRequest` | MessageRequest |
| `Voice.Request` | Request |
| `Voice.ResultRequest` | ResultRequest |
| `Wxapp.AddAvatarImgParamDoc` | AddAvatarImgParamDoc |
| `Wxapp.AddAvatarParamDoc` | AddAvatarParamDoc |
| `Wxapp.AddWxAppRecordParamDoc` | AddWxAppRecordParamDoc |
| `Wxapp.CheckVerifyCodeDataDoc` | CheckVerifyCodeDataDoc |
| `Wxapp.CloudCallParamDoc` | CloudCallParamDoc |
| `Wxapp.DefaultParam` | DefaultParam |
| `Wxapp.DefaultParamDoc` | DefaultParamDoc |
| `Wxapp.DelMobileDataDoc` | DelMobileDataDoc |
| `Wxapp.DellAvatarParamDoc` | DellAvatarParamDoc |
| `Wxapp.GETCreditScoreParam` | GETCreditScoreParam |
| `Wxapp.GetUserOpenIdParamDoc` | GetUserOpenIdParamDoc |
| `Wxapp.GetWxAppRecordParamDoc` | GetWxAppRecordParamDoc |
| `Wxapp.GetpullPayParamDoc` | GetpullPayParamDoc |
| `Wxapp.JSOperateWxParamDoc` | JSOperateWxParamDoc |
| `Wxapp.OauthListParamDoc` | OauthListParamDoc |
| `Wxapp.QrcodeAuthLoginParamDoc` | QrcodeAuthLoginParamDoc |
| `Wxapp.SessionidQRParamDoc` | SessionidQRParamDoc |
| `Wxapp.UnionpayDataDoc` | UnionpayDataDoc |
| `XiaoWei.BuluHistoryItemRequest` | BuluHistoryItemRequest |
| `XiaoWei.BuluUserHistoryRequest` | BuluUserHistoryRequest |
| `XiaoWei.CardScreenshotMediaRequest` | CardScreenshotMediaRequest |
| `XiaoWei.CardScreenshotSecurityCheckRequest` | CardScreenshotSecurityCheckRequest |
| `XiaoWei.CardWrapRequest` | CardWrapRequest |
| `XiaoWei.ChatBubbleExtraInfoRequest` | ChatBubbleExtraInfoRequest |
| `XiaoWei.DeleteHistoryItemListRequest` | DeleteHistoryItemListRequest |
| `XiaoWei.DeleteHistoryItemRequest` | DeleteHistoryItemRequest |
| `XiaoWei.DeleteXiaoweiChatHistoryRequest` | DeleteXiaoweiChatHistoryRequest |
| `XiaoWei.GetA2AChatListRequest` | GetA2AChatListRequest |
| `XiaoWei.GetChatHistoryListRequest` | GetChatHistoryListRequest |
| `XiaoWei.GetHalfScreenSuggestionsRequest` | GetHalfScreenSuggestionsRequest |
| `XiaoWei.GetRedDotRequest` | GetRedDotRequest |
| `XiaoWei.GetUserCardListRequest` | GetUserCardListRequest |
| `XiaoWei.InviteUsersRequest` | InviteUsersRequest |
| `XiaoWei.MarkRedDotReadValidRequest` | MarkRedDotReadValidRequest |
| `XiaoWei.PageContextRequest` | PageContextRequest |
| `businesscfg.BusinessConfig` | BusinessConfig |
| `models.EmptyObject` | EmptyObject |
| `models.ProxyInfo` | ProxyInfo |
| `models.ResponseResult` | ResponseResult — 业务响应数据；结构由具体接口决定 |
| `models.ResponseResult2` | ResponseResult2 — 业务响应数据；结构由具体接口决定 |
| `models.WebhookTestRequest` | WebhookTestRequest — 业务响应数据；结构由具体接口决定 |
| `webhook.WebhookConfig` | WebhookConfig |

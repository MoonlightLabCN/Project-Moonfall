# 交接文档

## 架构（2026-08-07）

**一个 Core + 两个 Shell**，同时适配 **WP 8.1** 与 **Win10 Mobile / UWP**。

```
MoonWeChat.sln
├── MoonWeChat.Core     Shared Project — 业务层（无页面）
├── MoonWeChat.UWP      src/MoonWeChat/ — Win10 / W10M UI
└── MoonWeChat.WP81     src/MoonWeChat.WP81/ — Windows Phone 8.1 UI
```

| 层 | 职责 |
|----|------|
| **Core** | Models / Services / ViewModels / WeChatPad HTTP / 设置持久化 |
| **UWP Shell** | App 生命周期、Views、Styles、Controls（底部 Tab 微信风） |
| **WP81 Shell** | 同上结构，适配 8.1 API（硬件返回、选图 AndContinue 等） |

导航类 `AppNavigation` **各 Shell 各一份**（会引用各自的 Page 类型）。

产品定位不变：**手机是瘦客户端**，电脑登录微信并运行 `server/pyweixin_gateway`。

## 编译

### UWP（Win10M / 桌面）

```powershell
$env:TEMP = "H:\项目\UWP\Project-大月墜落狂想\_tmp"
$env:TMP  = $env:TEMP
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild src\MoonWeChat\MoonWeChat.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
```

### WP 8.1

```powershell
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
& $msbuild src\MoonWeChat.WP81\MoonWeChat.WP81.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x86
# 真机: Platform=ARM

# 自动化冒烟（编译产物 / 清单 / 资源 / 启动路径 / 二进制符号 / MakeAppx）
powershell -ExecutionPolicy Bypass -File tools\Wp81SmokeVerify.ps1
```

**启动修复（与 UWP 同因）**：`App.xaml.cs` 禁止在构造函数里 `ThemeService.ApplyFromSettings()`（会 XAML 闪退）。

**模拟器部署**：`AppDeployCmd /installlaunch … /targetdevice:7` 在本机多次失败（CoreCon 连接超时 -2146233088），尽管 `/EnumerateDevices` 能列出 Emulator 8.1。冒烟脚本覆盖已构建包的完整性；真机/可连通模拟器上再用 AppDeploy 安装。

跨盘 TEMP 会导致 PRI210，临时把 TEMP/TMP 指到项目盘。

## 后端

见 `server/pyweixin_gateway/README.md`、欢迎页 / ConnectPage 向导。旧协议笔记仍在 `docs/WeChatPadPro-API-notes.md`。

## 明确边界

- 非官方协议，有封号风险
- 不要在同一 Shell 里再塞「第二套 WP UI」；WP 用 **WP81 工程** 部署

---

## 全量审计与修复（2026-08-28 第二轮）

### 当前状态

上一轮的交接要求「暂停开发、交由他人审计」。本轮已经完成**全量审计**并**逐条修复**，
覆盖网关、UI 自动化、共享 Core、假数据、两个 Shell 的排版布局与安全。

**两个工程都能编译，WP8.1 冒烟脚本 34 项全过，网关在 --mock 下跑通了端到端断言。
但仍然没有做实机发送验收** —— 没有重新部署到 Nokia 920T / Lumia 950 XL，
也没有在改动后往电脑微信发过任何一条新消息。不要把「编译通过 + mock 通过」当作实机可用。

### 本轮修掉的问题（按严重度）

#### 安全

- **网关默认 adminKey 是硬编码常量**（`moonwechat_local_2026`）。局域网内任何知道这个
  字面量的人都能调 `/Admin/GenAuthKey` 重签 token 接管网关，进而通过 UI 自动化操作你的微信。
  现在首启随机生成；**载入旧 config 时检测到默认值会自动轮换并打印警告**。
  → **更正（本轮核对磁盘后）**：上一版交接文档写的「adminKey 已经在本轮测试中被轮换过」
  与磁盘状态不符。`config.json` 里当时仍然是字面量 `moonwechat_local_2026`——
  因为轮换逻辑是**载入时**触发的，而 `0.0.0.0:18765` 上跑着的那个进程（PID 17348，
  启动于 8/27 23:53）是用轮换代码落盘（8/28 09:31）**之前**的旧代码起的，它从没执行过这段逻辑。
  该进程已在本轮停掉。**网关下次启动时才会真正轮换**，届时请重新从 `config.json` 读新值。
- **Token 每次请求都被拼进 URL query，并被网关打进控制台日志**。
  客户端改为只走 `X-Access-Token` 请求头（`AllowTokenInQuery` 仅对 WeChatPadPro 老部署开）；
  网关新增 `scrub_secrets()` 把请求行里的 `key=/token=/adminKey=` 打码；
  启动横幅默认只打掩码，要完整值加 `--show-secrets`。

#### 可见 UI 自动化（最大的实机风险）

- **`operationCompleted` 原来是常量 `True`**，只代表「没抛异常」。现在 `send_text` 在按
  Alt+S **之前**做一次真实校验：先往剪贴板写哨兵值 → `Ctrl+A` `Ctrl+C` 读回编辑框内容 →
  和待发文本逐字比对，不一致或读不回来就**中止发送**。响应里新增 `Verified` 字段
  （`editor-readback`）说明这次「完成」是靠什么证明的。
  它仍然**不等于微信服务器已落库**，只证明「编辑框里确实是这段文字且按了发送键」。
- **定比例坐标点击可能把消息发给别人**。新增 `ALLOWED_TARGETS` 白名单，默认只允许
  「文件传输助手」；其它目标（真实好友、群）直接拒绝并说明原因。
- **`is_logged_in()` 在 pywinauto 抛异常时返回 True**（因为 `"" != "mmui::LoginWindow"`），
  把探测失败变成「假在线」然后去盲点坐标。现在探测失败一律返回 False。
- **窗口类名硬编码 `Qt51514QWindowIcon`**（Qt 5.15.14），微信一升级就失效。
  改为匹配 `Qt*QWindowIcon` 前后缀。
- **剪贴板被吞**：每发一条就把用户剪贴板内容覆盖掉且不恢复。现在发送前后保存/还原。
- 每一步之后校验微信窗口仍在前台，中途失焦立即中止。

#### 收消息

- **`sync_messages` 每轮给每条消息生成新的 `uuid4()`**，导致 `seen_msg_ids` 永远命中不了，
  手机端每 4 秒把同一批消息重复插一遍。改为按
  `sha1(会话+发送人+内容+时间+序号)` 生成**内容寻址的稳定 ID**。
- 网关 `inbox` / `seen_msg_ids` 和客户端 `_seenMsgIds` 都无上限增长，现在都有上限裁剪。
- `PollOnceAsync` 里 `GetSyncMsgAsync` 抛异常会从 `async void` 的 Tick 漏出去直接崩进程，
  现在单独兜住。

#### 仍在伪装成功的分支

- `/msg/sendcdnimg` 在 `Content` 不是真实路径时**什么都不做却返回 `sent`**（非 mock 下现在如实报失败）。
- `send_file` 在 Qt 画布版微信上没有可见 UI 兜底路径，现在**明确报错**而不是走一条注定失败的 pyweixin 调用。
- `send_quote` 原来在异常时直接再发一条纯文本兜底 —— UI 可能已经被驱动过一半了。
  现在只在「明确知道没动过 UI」时才降级。

#### Core 发送链路

- **示例会话 id 会被原样当成 `ToWxid` 发到网关**（最严重的一条）。
  路径是：示例模式打开会话 → 关掉示例 → 返回栈退回被缓存的 ChatPage →
  `Session.Id` 还是示例的 → 网关拿它去微信搜索框搜 → 可能命中并发给不相干的人。
  三层修复：
  1. `AppServices.DataGeneration` 计数器，数据源一换就 +1；
  2. `ChatViewModel.IsStale` 据此判断，两个 Shell 的 ChatPage 在 `OnNavigatedTo` 里重新 Load；
  3. `LiveChatDataService` 新增 `ResolveSendTarget()`，**只认自己会话表里已有的 id**，
     未知 id 直接返回一条带说明的 Failed 气泡，绝不再 `EnsureSession` 凭空造。
- **发送类接口不再做跨路径回退**（`PostFirstOkAsync` → 单路径 + `stopOnTransportError`）。
  新增 `ApiCallResult.TransportError` 区分「没拿到应答」和「服务端明确说不行」：
  超时不重试，因为服务端可能已经发出去了。引用/表情的文本降级同理。
- **`ErrorText` 只有文本路径设置**，引用/图片/文件/重试失败时 UI 拿不到原因。
  新增 `ApplyResultAsync()`，所有发送路径统一走它。
- **重试丢了 `ClientMsgId`**，审计对账断掉，现在带上。
- **空昵称联系人会抛异常中断整轮通讯录同步**（`char.IsLetter(display, 0)` 没有空值保护）。
- 设置页同时改后端和地址时，新地址会被写进旧后端的槽里而丢失
  （`BaseUrl` setter 依赖当前 `BackendKind`）。新增 `SetBaseUrlFor(kind, value)` 显式指定槽位。

#### 假数据

- **朋友圈在真实模式下失败会静默回落到示例数据**，而 UI 显示「已加载 3 条」——
  用户看到假动态却以为是真的。现在真实模式失败就是失败，
  `MomentsService.LastLoadError` 传给 UI 如实显示。
- **示例模式的「不会真发」提示只覆盖了文本发送**。位置/名片/语音/图片/文件/重试/撤回/拍一拍
  全都没有守卫，照样插入 `Status = Sent` 的绿色气泡。现在 `ChatViewModel.BlockSend()`
  统一守住**所有**发送入口。
- 网关 `--mock` 时响应里的 `Transport: "mock"` 客户端原来完全不看。
  现在 `IsMockTransport()` 检测到就在气泡上标注「网关处于 mock 模式，这条并没有发到电脑微信」。
- 示例数据是进程级静态的，切走再切回来还留着上一轮点出来的假气泡。
  `SampleDataService.Reset()` 在离开示例模式时清掉。

#### 两个 Shell 的排版布局

- **`ChatSession` 没有实现 INotifyPropertyChanged**（`ChatMessage` 反而实现了）。
  后果：两个 Shell 的会话列表里未读角标/最后一条预览/时间**永远不更新**，
  UWP 那边 `x:Bind` 还全是默认的 OneTime。列表之所以看起来会刷新，
  完全是因为 `Reload()` 在做 `Clear()` + 逐个 `Add()` —— 那是在绕过缺失的 INPC，
  代价是每次轮询都销毁全部容器、滚动位置打回顶部。
  现在 `ChatSession` 实现了 INPC，UWP 的绑定改成 OneWay，`Reload()` 改成原地增量对齐。
- **掉线横幅颜色写死** `#FFF3CD`/`#856404`，在 WpClassic 纯黑主题下是唯一一块亮黄色。
  改为 `BannerBackgroundBrush` / `BannerBorderBrush` / `BannerTextBrush` 三个主题资源
  （四本主题字典都加了，深色下用暗琥珀）。
- **气泡 `MaxWidth="260"` 写死**，桌面/平板全屏时聊天变成一条窄带。改成 420。
  （更正上一轮报告里的一个说法：小屏并不会因此被裁切，列宽本身就会兜住。）
- **UWP 主页标题和右侧 4 个按钮叠在同一个 Grid 单元格里**，副标题
  （`昵称 · wxid_xxxxxxxx`）一长就钻到按钮下面。改成两列 + `TextTrimming`。
- **每次从聊天页返回都跑一整轮 `EnsureSessionAsync`**（十几个 HTTP + 重启轮询定时器）。
  新增 `AutoRefreshRemoteAsync()` 带 60 秒冷却；手点刷新不受限制。
  WP81 还额外每次都拉一遍朋友圈，改成只在首次进入时拉。
- **WP81 通讯录段在自动刷新后不更新**（`ReloadContacts` 只在首次进入和手动刷新时调）。
- **主题设置重启就丢**：`ThemeService.ApplyFromSettings()` 全项目零调用，
  `AppSettings.VisualTheme` 写了从来不读。而且 `ThemeService.Current` 恒初始化为 `Win10`，
  WP81 实际合并的是 WpClassic，导致设置页「已经是这个主题就不动」的判断误判，
  WP8.1 上第一次点「Windows 10」没反应。
  现在启动时（`Window.Current.Activate()` **之后**）调一次，并新增
  `SyncCurrentFromMergedDictionaries()` 用实际合并的字典校准 `Current`。
- 我方头像的昵称和主色原来写死（`"我"` / `#07C160`），现在跟着消息走、跟着主题走。
- `OnUnhandledException` 原来无条件 `e.Handled = true`，会掩盖启动/XAML 解析失败 ——
  正是这个项目一直在追的那类问题。改成 DEBUG 下不吞、RELEASE 下仍兜住。

### 冒烟脚本的一处调整（需要知情）

`tools/Wp81SmokeVerify.ps1` 原来有一条门禁：**只要 `App.xaml.cs` 里出现
`ThemeService.ApplyFromSettings()` 就 FAIL**。它的注释写的是「startup must NOT hot-swap
themes (real crash cause)」，而真正的崩溃原因是**在构造函数里**改
`Application.Resources.MergedDictionaries`（0xc000027b）。

因为「启动后应用用户存的配色」是必须修的（否则设置每次重启都丢），
这条门禁改成**按位置判断**：调用点在 `Window.Current.Activate()` **之前**才 FAIL。
已经验证：把调用塞回构造函数，脚本仍然会 FAIL。

## 后端复查与修复（2026-08-29 第三轮）

只动了 `server/pyweixin_gateway/`（`gateway.py` / `weixin_ui_fallback.py`）和本文档。
**没有碰前端**——UI 与 Shell 正在被重写。所有结论都是通读代码 + 跑断言得到的，
不是局部推断；具体验证方式见本节末尾。

### 发送链路（weixin_ui_fallback.py）

- **剪贴板还原会销毁非文本内容**。`_read_clipboard_text()` 在剪贴板里是图片/文件时
  返回 `""` 而不是抛异常，于是 `saved_clipboard` 拿到空串，`finally` 里照样
  `EmptyClipboard()` + 写空文本，用户原本复制的图片就没了。
  现在用 `saved_clipboard_text = None` 区分「读到空文本」和「根本不是文本」，
  后者跳过还原（宁可留下我们的 payload，也不清空用户剪贴板）。
- **`_search` 点空会把联系人名粘进真实会话**。`_require_still_foreground` 只检查窗口
  还在前台，不检查焦点落在哪个控件；一旦没点中搜索框，`Ctrl+A` + `Ctrl+V` 就打进了
  当前打开会话的输入框，留下一条草稿（最终发送有 readback 门禁挡着，不会误发）。
  现在搜索框也做一次哨兵 + readback 校验，读回内容不等于目标联系人就中止。
- `open_chat` 的非白名单分支确实是死代码（`_normalize_target` 已经全拒了），
  按建议保留并加注释说明它只在改了 `ALLOWED_TARGETS` 后才生效，不再让人误以为支持任意联系人。

### 收消息（gateway.py）

- **`_stable_msg_id` 把 batch 内 index 算进哈希**，列表平移后同一条消息哈希就变了，
  `seen_msg_ids` 命中不了，于是重复插进手机。
  现在 `index` 改成「该条 `(sender, content, stamp)` 在**本会话本批次**内的出现次序」：
  列表平移不影响哈希，同一批里连发两条相同文本仍可区分。
  顺带确认了另一半问题的根因——`stamp` 其实**恒为空**：`pull_messages`
  （`WeChatAuto.py:3899`）只返回 `消息发送人/消息内容/消息类型`，从来没有 `消息时间` 字段。
  所以去重键实际是 `(session, sender, content, 出现次序)`，取舍问题不存在了。
- **`sync_messages` 异常被吞成 `return []`**，「窗口被挡住 / RPA 超时」和「真的没有新消息」
  在手机端完全一样。现在失败抛 `WeixinSyncError`，`/Msg/Sync` 回
  `Code=503` + `Data.uiError`，让手机能如实显示。

### 不再把失败伪装成成功

- **`refresh_contacts` 失败时伪造联系人**：原来会凭空造一个「文件传输助手」并以成功返回，
  手机显示「通讯录：1 个联系人」，用户以为同步完了。现在如实上报失败。
- `last_ui_error` 原来**只有两处赋值、零处读取**。现在 `/Friend/GetContractList`、
  `/Group/GroupList`、`/Msg/Sync`、`/FriendCircle/GetList` 失败时都带上它。
- **朋友圈时间戳全是「现在」**：`CreateTime` 写死 `now_unix()`，`发布时间` 被丢掉，
  于是每条都显示「刚刚」、排序也失效。现在解析 pyweixin 的相对时间
  （`3分钟前` / `2小时前` / `昨天` / `5天前`，含英文变体）成真实 Unix 时间戳。
- 朋友圈读取失败同样不再返回「空但成功」。

### 安全

- **CORS 头删除**。原来对所有响应无条件加 `ACAO: *`、预检回 `Allow-Headers: *`。
  手机客户端不是浏览器，根本不需要 CORS；留着等于让你浏览器里打开的任意网站都能跨源
  打这个局域网服务。同时补了 **Host 头校验**（只接受 IP 字面量 / localhost）挡 DNS rebinding。
- **token / adminKey 比较改成 `secrets.compare_digest`**。
- **`capture_qr` 的「已登录」不再用 `Code 200`**。改成 `409`（`ALREADY_ONLINE`）——
  注意不能用 `fail()` 的默认 `300`，客户端把 300 解释成「需重新登录」，语义正好相反。
- **`save_config` 收紧权限**：`chmod 0o600`，首次创建时再用 `icacls` 去掉继承 ACE。
- 另外自查发现两个原审计清单外的问题，一并修了：
  - **`/Msg/SendCDNImg` 可读任意路径**：原来只判断 `os.path.exists()`，
    客户端传 `C:/Windows/...` 就会被当作待发文件。现在限定在 `data/` 目录内。
  - **`read_json_body` 无上限、且在鉴权之前执行**：未鉴权的客户端声明
    `Content-Length: 8GB` 就能让网关去分配。现在上限 32 MiB，
    畸形 `Content-Length` 也不再在 `try` 之外抛异常导致连接无响应中断。
- **RPA 超时文案去掉了「未登录」三个字**。客户端 `LooksLikeOffline()` 拿这类关键词做
  掉线判断，而「窗口被挡住」不是「账号掉线」——照原样会让手机丢掉一个好端端的会话、
  退回扫码页。前端重写时请注意别再用「消息文本里找关键词」判断在线状态。

### 「如实上报失败」的三个连带后果（都已处理）

把失败如实上报，会让客户端的**路径回退列表**从「第一条就停」变成「一路试到底」，
于是暴露出三个原本被掩盖的问题。这三处是本轮改动引入的，不是原审计清单里的：

1. **RPA 放大**。`GetSyncMsgAsync` 依次试 3 个路径、每个再 POST/GET 重试一次；
   原来「空但成功」在第一条就停了，现在 `Ok=false` 会让它全试一遍，
   即一次轮询可能触发 4 次 90 秒的 RPA，全部串在同一个队列上。
   加了 `READ_FAIL_COOLDOWN = 20s`：冷却期内直接重放缓存的错误，不再驱动微信。
   已验证 6 次客户端尝试只驱动 RPA **1** 次；`contacts` / `sync` / `moments` 各自独立冷却；
   冷却过期后会重试（微信恢复了不会被永久拉黑），成功后清除缓存。

2. **404 覆盖真实诊断**。客户端把**最后一次**尝试的 Message 交给用户，
   而它候选列表的尾部（`/message/HttpSyncMsg`、`/friend/GetContactList`、
   `/group/GetAllGroupList`、`/friend/GetContactDetailsList`、`/friend/SearchContact`、
   `/user/ModifyRemark`、`/sns/*`）网关**一个都没实现**。
   结果手机上显示的是「未实现的路径」，而不是「窗口被挡住」——
   我刚加的诊断被自己盖掉了。现在这些别名都落地到对应实现上，已逐条验证不再返回 404。
   > 前端重写时注意：客户端这套「多路径顺序重试 + 取最后一条错误」的策略，
   > 会让最没用的那条错误浮到用户面前。如果你重写这部分，建议改成
   > **保留第一条语义明确的失败**（或按 Code 分级），而不是无脑取 `last`。

3. **`status()` 的 45 秒探测被放大**。它被 4 条路由用到（heartbeat / profile /
   capture_qr / check_qr），每条客户端又有多个别名，一个轮询周期能把同一个探测排 5 次。
   原来声明了 `self.last_probe` 却**从未读取**，正好是给它准备的位置：
   现在加了 `PROBE_CACHE_TTL = 8s` 的短缓存，已验证 6 个请求只探测 1 次。
   **`check_qr` 例外**，它显式传 `fresh=True`——那条路径的存在意义就是捕捉
   「未登录 → 已登录」的跳变，读缓存会把登录检测推迟一个 TTL。

### 验证方式与边界

`gateway.py` / `weixin_ui_fallback.py` 都能编译。断言在临时目录里起真实
`ThreadingHTTPServer` 跑，覆盖：消息 ID 平移稳定性与批内重复可区分、朋友圈四种时间格式、
路径穿越（`..\..\Startup\evil.bat` 等）被收进 `data/`、Host 校验（IPv4 / 括号 IPv6 /
裸 IPv6 / 主机名 403）、CORS 头缺失、鉴权三态、四条读路径失败回 503 且不伪造数据、
畸形与超大 `Content-Length`、冷却行为、失败文案不含掉线关键词。全部通过。

**边界**：全程没有真实微信参与，`config.json` 也没被动过（断言跑在临时 `CONFIG_PATH` 上）。
剪贴板还原、搜索框 readback 这两处只能在实机上验收——它们依赖真实的 Qt 画布窗口。
`0.0.0.0:18765` 上那个真实网关也从未用新代码起过。

### WeChatPadPro 侧（只读排查，未改动）

`_wechatpadpro/` 下只有第三方**预编译产物**（`bin_releases/*.exe`、`*.zip`），没有源码，
所以代码层面无从修复；能看的是部署配置。凭证文件（`*_CREDS.txt`、`.env`、`conf_app.conf`、
`token*.txt`、`config.json`）都已被 `.gitignore` 正确排除，**没有泄漏进 git**（已逐个核对）。
当前 `1238` / `1239` 上没有进程在跑。

部署配置里有几处值得在重新启用前处理，都只需改配置：

- `conf_app.conf`：`httpaddr = "0.0.0.0"`（暴露到局域网）、`EnableDocs = true` +
  `serve_swagger = true`（`runmode = prod` 下仍然开着 API 文档）、`mcp_enabled = true`
  且 `mcp_allowed_hosts` / `mcp_allowed_origins` **都是空字符串**（未限制来源）。
  `mcp_write_enabled = false` 算是唯一的好消息。
- `WeChatPadPro/.env`：`DEBUG=true`、`HOST=0.0.0.0`。
- `.env` 比 `.env.example` 多出 `MYSQL_DATABASE / MYSQL_USER / MYSQL_PASSWORD /
  MYSQL_ROOT_PASSWORD` 四个键，`.env.example` 需要补齐，否则照样例部署会缺库配置。

没有读取或输出任何凭证值——上面全部基于键名与非敏感设置项。

## 主机侧其余部分（2026-08-29 第四轮）

范围：`tools/` 的 7 个脚本、`server/pyweixin_gateway/` 的非代码文件、
`_wechatpadpro/poll_login.py`、`_tmp/` 里的历史脚本。**两个 shell 未触碰**（正在重写）。

### P0 编码：7 个 `.ps1` 在系统默认的 PowerShell 下根本解析不过

这些脚本是 **UTF-8 无 BOM**，而本机 Windows PowerShell 5.1 的 ANSI 代码页是 **gb2312**。
5.1 对无 BOM 的文件按 ANSI 解码，于是 UTF-8 的中文字节被按 GB2312 拆开，
**把字符串的收尾引号吃掉** → 真语法错误，不是显示问题。

实测（同一批文件、两个宿主）：

| 宿主 | ANSI | 解析失败 |
|---|---|---|
| Windows PowerShell 5.1 | gb2312 | **3 个**（`start.ps1` / `LocalDeployUwp.ps1` / `PackWp81Appx.ps1`） |
| PowerShell 7 | utf-8 | 0 个 |

最要紧的是 **`start.ps1` 就是 README 里写的网关启动方式**，而它在 `powershell.exe`
（系统默认、双击 / 快捷方式走的那个）下解析就失败。之前一直没暴露，
大概是因为都用 `pwsh` 或直接 `python gateway.py` 起的。

已给 7 个含非 ASCII 的脚本加上 UTF-8 BOM——微软自己生成的
`Add-AppDevPackage.ps1` / `Install.ps1` 都带 BOM，这本来就是 Windows PowerShell 的约定。
修完两个宿主都是 0 失败，并实跑 `start.ps1` 确认中文输出字节正确。

> **写新 `.ps1` 时注意**：只要含中文就必须存成 UTF-8 **带 BOM**，否则在中文 Windows 的
> 5.1 下必崩。这次我自己写探测脚本时也踩了同一个坑，可见有多容易复发。

### P1 `$pwd` 被覆盖（`PackWp10Appx.ps1:77`）

`$pwd = 'moon-wp81'` —— `$pwd` 是 PowerShell 的**自动变量**（当前目录，`PathInfo` 类型）。
覆盖后同一 session 里任何依赖 `$pwd` 的 `Join-Path` / `Resolve-Path` 都会静默拿到
`moon-wp81\...`，而故障点离这里很远。已实测确认该行为，改名 `$pfxPassword`。

### P1 凭证被打进 stdout（三处）

打包 / 冒烟输出经常被重定向进日志文件，和上一轮给网关横幅做掩码是同一类问题：

- `PackWp81Appx.ps1:90` 把 pfx 口令打出来 → 改为只提示「口令写在脚本里」。
- `TestWeChatPadPro.ps1` 把 `/Login/GetCacheInfo` 与 `/Admin/GenAuthKey` 的**响应体原文**
  打出来（含 token / wxid / 昵称）→ 改为只打状态码 + 一句判定。
- `_wechatpadpro/poll_login.py:11` `print("token", token)` → 改为掩码
  （完整值本来就在 `token.txt` 里，没必要回显）。

已实测：传入 `TTT-SECRET-TOKEN` / `AAA-SECRET-ADMIN` 跑一遍，
输出里两个明文都不出现（`-CheckAdmin` 也一样）。

### P1 `/Admin/GenAuthKey` 会签发真 token，却被当成只读探测

`TestWeChatPadPro.ps1` 原来每次运行都调它——这不是只读检查，**每跑一次就多签一个 token**。
改成 `-CheckAdmin` 显式开启，默认跳过并说明原因。

### P2 依赖声明与实际 import 不符

`requirements.txt` 原来只有一行 `pywechat127`（无版本），但代码**直接** import 了
`psutil` / `pyautogui` / `win32*` / `pywinauto`。它们目前只是 `pywechat127` 的传递依赖——
上游哪天不再依赖，网关会在 `import` 时才炸，而不是装依赖时就报错。
已补齐直接依赖并钉版本（与本机实装一致）。

顺带记一个容易「顺手改错」的点：**分发包名是 `pywechat127`，导入名却是 `pyweixin`**
（`gateway.py:257`）。这是上游的设定，不是笔误。

### P2 `Wp81SmokeVerify.ps1` 硬编码 MSBuild 路径且不检查存在性

`$msb = 'C:\...\2017\Community\...\MSBuild.exe'` 直接就 `& $msb`，路径不在时报的是
「找不到命令」这种和真实原因无关的错。已改成候选列表 + `Test-Path`，
找不到就打印试过哪些路径并 `exit 1`（其余打包脚本本来就是这个写法，只有这个漏了）。

### 查过但没问题 / 已失效的

- **`DeployToDevicePortal.ps1` 里覆写 `$args` 再 `@args` 展开**：我一开始怀疑这会导致
  每次 curl 拿到零参数。**实测证明是我判断错了**——带 `param()` 的函数里
  `$args` 只是普通局部变量，`@args` 展开正常。该脚本这一处没问题。
  （无 `param()` 的函数里 `$args` 才是自动变量，但那种写法这里没有。）
- **`TestWeChatPadPro.ps1` 的 `$ErrorActionPreference = 'SilentlyContinue'`**：
  怀疑它会吞掉 `Invoke-WebRequest` 的失败让 `catch` 不触发。实测 `catch` 正常进入
  （连接类错误是终止性的），行为与 `Stop` 一致。没问题。
- **`Parse-CredsFile` 用 `$script:` 改脚本级 `param()` 变量**：实测确实生效。没问题。
- **各打包脚本硬编码的 SDK 路径**：`makeappx` / `signtool` 的
  `10.0.15063.0` / `17763.0` / `19041.0` / `22621.0` 本机全部存在，
  且都已有候选列表 + `Test-Path`。当前可用。
- **`_tmp/fix_gateway.py` 已失效**：它是个会**改写 `gateway.py`** 的一次性补丁，
  而写回的内容正是前几轮修掉的东西（`mid = uuid.uuid4().hex`、
  `send_quote` 失败后盲目重发、丢掉 `client_msg_id`）。
  但它第 6 行的锚点 `def send_quote(self, to, content, quote)` 现在**匹配不上**了
  （真实签名多了 `client_msg_id`），会在写任何东西之前 `SystemExit`。
  属于该删的历史残留，不是活的风险——**但别去"修"它的锚点**。
- **`_tmp/*.ps1`** 只做 `Stop-Process MoonWeChat` + 启动/截图，无凭证、无破坏性操作。
- **pfx / cer 都已被 `.gitignore` 排除**（已逐个核对），没有进 git。

### 验证方式

7 个 `.ps1` 在 **PowerShell 5.1 与 7 两个宿主**下都做了 AST 解析（0 失败）；
`start.ps1` 用 `powershell.exe` 实跑到调用 python 之前，确认中文输出字节正确；
`TestWeChatPadPro.ps1` 用假凭证实跑，确认输出无明文泄漏且默认不签发 token；
`poll_login.py` 编译通过并单测掩码逻辑（含长度 0/5/8/9/32 边界）；
`$pwd` 覆盖、`@args` 展开、`$script:` 跨作用域赋值三处行为都用最小复现脚本实测过。

**没有验证的**：没有真机 / 模拟器，所以打包、部署、Device Portal 这些路径只做了静态检查
和语法验证，没有实际跑通一次完整打包或部署。

### 下一步：实机验收（尚未做）

上一轮交接文档里的验收顺序仍然有效，另外补充本轮新增的检查点：

1. 网关 `config.json` 里的 adminKey **会在下次启动时轮换**（上一版文档写「已轮换」是错的，
   详见上文「安全」小节的更正）。启动后重新从 `config.json` 读新值。
2. 启动网关后确认横幅是掩码的；用 `--show-secrets` 才看完整值。
3. 发送时确认响应里带 `Verified: "editor-readback"`。如果是空的，
   说明走的不是 canvas 路径，要单独确认。
4. **只用「文件传输助手」**。现在非白名单目标会被网关直接拒绝，
   如果你想放开，改 `WeixinCanvasUi.ALLOWED_TARGETS` —— 但要清楚定坐标点击的误发风险。
5. 故意断开网关，确认手机上显示失败原因且草稿还在。
6. 用 `--mock` 跑一次，确认手机气泡上出现「网关处于 mock 模式」的提示。
7. 示例↔真实来回切换后进同一个会话，确认不会出现「发送已拦截」以外的异常行为，
   也确认切换后不会拿旧会话对象发送。
8. 观察会话列表：收到消息时未读角标/预览应该原地更新，**滚动位置不应该跳回顶部**；
   同一条消息不应该每几秒重复出现。

本轮（第三轮）新增的实机检查点：

9. **剪贴板**：先复制一张图片（不是文字），再从手机发一条消息，
   然后回电脑 `Ctrl+V` —— 图片应该还在。这条只能实机验，断言覆盖不到。
10. **搜索框 readback**：发送过程中故意用别的窗口抢一下焦点，
    应该看到「无法读回搜索框内容」或「搜索框内容与目标联系人不一致」并中止，
    而且**不应该**在任何真实会话里留下草稿。
11. **失败要如实**：把电脑微信最小化或用别的窗口压住，让手机拉一次消息/通讯录。
    手机应该显示失败原因，**不应该**显示「通讯录：1 个联系人」或安静地假装同步成功。
    紧接着连续多拉几次——20 秒冷却期内不应该反复去抢微信窗口。
12. **朋友圈时间**：确认不再全是「刚刚」，且排序正确。
13. **掉线误判**：第 11 步失败时，确认手机**没有**因此退回扫码页
    （失败文案已刻意避开「未登录」等关键词，前端不要再用文本关键词判在线）。

### 遗留：本轮启动的测试进程

**已清理，无需再操作。** 上一版遗留的三个 `--mock` 网关
（`127.0.0.1:18797/18798/18799`，PID 21816 / 21996 / 1996）已在本轮停掉。

`0.0.0.0:18765` 上那个真实网关（PID 17348）也已停掉——它跑的是轮换逻辑落盘之前的旧代码，
adminKey 就是人人可猜的 `moonwechat_local_2026`，且监听在 `0.0.0.0`。
**当前没有任何网关在跑**，重启后才会加载本轮的修复并轮换 adminKey。

重启命令（前端重写完再执行）：

```powershell
cd "H:\项目\UWP\Project-大月墜落狂想\server\pyweixin_gateway"
python gateway.py --host 0.0.0.0 --port 18765 --show-secrets
```

只在本机自测时建议改成 `--host 127.0.0.1`，不要把 UI 自动化网关暴露到局域网。

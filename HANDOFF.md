# 交接文档

写给接手这个项目的下一个 AI 助手（或者人）。  
SETUP.md 是「怎么跑起来」；这份是「来龙去脉 + 现状 + 接下来怎么办」。

## 这是什么项目

在 **Windows 10 Mobile / UWP** 上的第三方微信客户端前端（命名空间 `MoonWeChat`，应用显示名「大月微信」）。  
后端对接自托管 [WeChatPadPro](https://github.com/WeChatPadPro/WeChatPadPro)（微信 iPad 协议 HTTP API，不是官方接口）。

最低系统：**10.0.10240**。视觉：扁平 Metro，直角，无 Spacing/CornerRadius/亚克力/Reveal。

## 当前状态（2026-08-06）

### 产品定位（重要）

**WP / W10M 手机是瘦客户端，绝不在手机上跑 WeChatPadPro。**  
后端装在电脑或云上；手机只填远程地址 + Token + 扫码。

### 前端（已完成，用户已满意）

- 会话列表 / 聊天页（全消息类型气泡）/ 通讯录 / 发现占位 / 我（设置入口）
- **欢迎页** `WelcomePage`：说明架构 +「连接远程」/「示例数据」
- **远程连接向导** `ConnectPage`：地址规范化、Token / AdminKey 生成、测连通、去扫码
- 示例数据可预览 UI；首次启动落在欢迎页（`HasCompletedOnboarding=false`）
- 已在本机 MSBuild x64 Debug 编译通过

### 后端接入

| 能力 | 状态 |
|------|------|
| 欢迎 / 远程连接向导（WP 主路径） | ✅ WelcomePage + ConnectPage |
| 高级设置页 | ✅ SettingsPage |
| 扫码登录 + Newinit + 轮询 | ✅ LoginPage；路径对齐 knowhub |
| HTTP 客户端 | ✅ 主路径见下表，带旧别名回退 |
| 拉好友 / 发文本 / Sync 轮询 | ✅ |
| 心跳保活 `/Login/HeartBeat` | ✅ 约 45s |
| 自己资料 `/User/GetContractProfile` | ✅ Refresh 时拉取昵称 |
| 发图片（选图 → base64 → UploadImg/SendCDNImg） | ✅ |
| 发位置 `/Msg/ShareLocation` | ✅（演示坐标） |
| 发名片 `/Msg/ShareCard` | ✅（默认 filehelper 演示） |
| 发链接 `/Msg/ShareLink` | ✅ |
| 失败消息点击重发 | ✅ |
| 远程头像 URL（AvatarView 回落文字） | ✅ |
| 语音采集 / 真文件 CDN / 红包转账 | ❌ 有意不做或仅占位 |

### knowhub 文档已确认主路径（无头 Chrome 抓侧栏）

| 步骤 | 路径 |
|------|------|
| 生成 Token | `POST /Admin/GenAuthKey` |
| 取二维码 | `POST /Login/GetQRMac` |
| 轮询扫码 | `POST/GET /Login/CheckMacQR` |
| 初始化 | `POST /Login/Newinit` |
| 好友列表 | `POST /Friend/GetContractList` |
| 发文本 | `POST /Msg/SendTxt` |
| 同步消息 | `POST /Msg/Sync` |

文档站：https://wx.knowhub.cloud/

## 启动路由

```
!HasCompletedOnboarding → WelcomePage
UseSampleData           → ChatListPage
!IsRemoteConfigured     → ConnectPage
!IsLoggedIn             → LoginPage
else                    → ChatListPage
```

## 怎么连真实后端（WP 场景）

1. **电脑/云**部署 WeChatPadPro（手机不装）。
2. 保证手机能访问该地址（同一 Wi‑Fi，或 frp/caddy 等穿透）。
3. 打开 App → 欢迎页「**连接远程服务器**」：
   - 地址：`192.168.x.x:1239` 或 `https://你的域名`
   - Token：Swagger 生成，或填 AdminKey 让 App 调 `/Admin/GenAuthKey`
   - **测试连接** → **保存并去扫码登录**
4. 手机微信扫码确认 → 自动 Newinit + 刷通讯录 → 可发文本。

## 关键代码入口

```
AppServices.Data          → IChatDataService（Sample 或 Live）
AppServices.Api           → WeChatPadApiClient
AppServices.Rebuild()     → 设置变更后重建数据源
AppSettings.*             → LocalSettings 持久化
```

- 发消息：`ChatViewModel` → `IChatDataService.SendTextAsync`
- 会话列表：`ChatListViewModel` 订 `SessionsChanged`
- 协议消息 → UI：`MessageMapper`

## 项目结构（相对交接时的增量）

```
Services/
  AppSettings.cs / AppServices.cs / IChatDataService.cs
  SampleChatDataService.cs / SampleDataService.cs
  WeChatPad/
    WeChatPadApiClient.cs   ← HTTP + 路径回退 + JSON 容错
    WeChatPadDtos.cs
    LiveChatDataService.cs  ← 会话内存库 + 轮询
    MessageMapper.cs
Views/
  SettingsPage / LoginPage  ← 新增
```

## 环境与编译

```powershell
$env:TEMP = "E:\Desktop\项目\UWP\Project-大月墜落狂想\_tmp"
$env:TMP  = $env:TEMP
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
$sln = "E:\Desktop\项目\UWP\Project-大月墜落狂想\MoonWeChat.sln"
& $msbuild $sln /t:Restore,Build /p:Configuration=Debug /p:Platform=x64
```

跨盘 TEMP 会导致 `PRI210`，临时把 TEMP/TMP 指到 E 盘。

## 明确边界

- 非官方协议，有封号/风控风险；勿用主力微信号狂测。
- 字段名/路径以**你实例的 Swagger** 为准；笔记见 `docs/WeChatPadPro-API-notes.md`。
- AvatarView 已改为 code-behind 刷新，避免部分 XAML 编译器对函数绑定报 `WMC1110`。

## 建议下一步

1. 对着真实 Swagger 收紧 `WeChatPadApiClient` 路径（去掉无效候选，补你版本独有的）。
2. 接图片/文件发送与本地选图（FileOpenPicker）。
3. AvatarView 加真实 `Image` + URL 失败回落文字头像。
4. 若部署支持 WebSocket，可替代轮询降延迟。
5. 「发现」朋友圈时间线（WeChatPadPro 有朋友圈接口）。

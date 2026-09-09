# MoonWeChat — Windows Phone 8.1 WinRT

从 UWP 工程 `src/MoonWeChat` 移植的 **Windows Phone 8.1（WinRT / Store）** 客户端。

## 要求

- Windows Phone 8.1 SDK
- Visual Studio 2015 或 2017（带 Windows Phone 开发组件）
- 本机已验证：VS2017 MSBuild 15 + SDK 8.1，`Debug|x86` 编译通过

## 编译

```powershell
$env:TEMP = "<项目盘上的临时目录>"
$env:TMP  = $env:TEMP
$msbuild = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe"
& $msbuild MoonWeChat.WP81.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x86
```

- **x86**：模拟器  
- **ARM**：真机部署  

输出：`bin\x86\Debug\MoonWeChat.exe`（及配套 appx 布局）。

## 与 UWP 版的关系

| | UWP | WP8.1 |
|--|-----|-------|
| 路径 | `src/MoonWeChat` | `src/MoonWeChat.WP81` |
| 绑定 | `x:Bind` | `{Binding}` |
| 选图 | `PickSingleFileAsync` | `PickSingleFileAndContinue` |
| 返回键 | 系统标题栏 / 手势 | `HardwareButtons.BackPressed` |
| 业务逻辑 | WeChatPad 客户端 | **同一套**（Services / ViewModels） |

功能与 UWP 对齐：欢迎 / 连接远程 / 扫码登录 / 会话 / 聊天 / 通讯录 / 朋友圈 / 设置。

## 部署提示

1. 手机开发者模式 + 解锁  
2. 用 VS「部署」到设备，或 `WinAppDeployCmd`  
3. 手机连电脑同网 Wi‑Fi，填 `http://电脑IP:18765` 连接 pyweixin 网关  

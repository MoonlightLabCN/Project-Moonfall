# Project Moonfall / 大月墜落狂想

面向 **Windows Phone 8.1 与 Windows 10 Mobile / UWP** 的非官方微信瘦客户端。两个手机界面共享业务层，通过电脑上的 HTTP 网关操作已登录的桌面微信。

## 当前维护线

当前维护分支是 **`refactor/shell-rewrite-and-core-unify`**。GitHub 默认 `main` 分支保留较早代码；下述目录、功能与构建入口以维护分支为准。

```powershell
git fetch origin
git switch refactor/shell-rewrite-and-core-unify
```

也可直接查看 [维护分支源码](https://github.com/MoonlightLabCN/Project-Moonfall/tree/refactor/shell-rewrite-and-core-unify)。

## 功能与结构

- 共享 Models / Services / ViewModels，提供会话、聊天、通讯录、连接与设置流程。
- UWP 和 WP8.1 使用各自的 XAML Shell 与导航适配，保留与年代匹配的原生界面。
- 电脑网关提供登录状态探测、二维码取得、会话轮询和文本发送等调用；可用 mock 模式检查客户端流程。
- 示例数据与 mock 传输用于开发验证，不能当作消息已通过真实微信发送的证据。

| 路径 | 内容 |
|---|---|
| `src/MoonWeChat.Core/` | 共享业务、设置、接口与视图模型 |
| `src/MoonWeChat/` | Windows 10 / Windows 10 Mobile UWP Shell |
| `src/MoonWeChat.WP81/` | Windows Phone 8.1 Shell |
| `server/pyweixin_gateway/` | Windows 电脑上的微信 UI 自动化网关 |
| `tools/` | 打包、部署与静态冒烟检查脚本 |
| `HANDOFF.md` | 架构、历史修复与验证边界；`SETUP.md` 含更早的原型记录 |

## 构建手机端

需要 Windows、VS2017 MSBuild 15，以及 UWP / Windows Phone 8.1 对应工作负载和 SDK。UWP 工程目标 SDK 为 **19041**、清单最低为 **10240**；WP8.1 工程使用 **8.1 SDK**。在维护分支仓库根目录执行：

```powershell
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
& $msbuild 'src\MoonWeChat\MoonWeChat.csproj' /t:Restore,Build `
  /p:Configuration=Release /p:Platform=ARM /p:VisualStudioVersion=15.0 `
  /p:UseDotNetNativeToolchain=true /p:GenerateAppxPackageOnBuild=true `
  /p:AppxBundle=Never /p:AppxPackageSigningEnabled=false
& $msbuild 'src\MoonWeChat.WP81\MoonWeChat.WP81.csproj' /t:Build `
  /p:Configuration=Release /p:Platform=ARM /p:VisualStudioVersion=15.0
```

UWP 输出位于工程的 `AppPackages/`；WP8.1 编译布局位于 `bin/ARM/Release/`，其打包入口见 `tools/PackWp81Appx.ps1`。发布包还需正确签名和匹配的 ARM 框架依赖。若遇 PRI210 跨盘临时文件错误，按 `HANDOFF.md` 将该次构建的 TEMP/TMP 指向项目所在盘。

## 连接电脑网关与安装包

电脑需运行已登录的微信与 Python 3.10+，在 `server/pyweixin_gateway/` 安装 `requirements.txt` 后运行 `start.ps1`。手机填写电脑局域网地址和首启生成的 token；详细步骤与权限边界见 [网关 README](server/pyweixin_gateway/README.md)。

安装包从 [GitHub Releases](https://github.com/MoonlightLabCN/Project-Moonfall/releases) 获取，区分 `MoonWeChat`（UWP）与 `MoonWeChatWP81`。安装 ZIP 提供公开开发证书和相应依赖；证书私钥不分发。

网关依赖桌面微信的可见 UI、版本和登录状态，手机不能脱离电脑独立登录；部分消息类型只支持文本降级或明确返回失败。2026-11-01 已完成两个 ARM 工程的构建和包检查，本轮未做真实微信消息发送或手机安装验收。历史 mock/静态冒烟通过也不代表当前实机端到端可用。

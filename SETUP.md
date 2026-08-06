# 交付说明

这一版做的是**真实可编译的 UWP XAML/C# 页面**（不是网页/HTML 视觉稿），目标是 Windows 10 Mobile
（也能在桌面 UWP 下跑，方便你先在电脑上预览）。数据是本地示例数据，没有接 WeChatPadPro 真实接口，
但字段和交互都是照着 [docs/WeChatPadPro-API-notes.md](docs/WeChatPadPro-API-notes.md) 里调研到的后端真实能力设计的。

**已经用你机器上装的 Visual Studio（`MSBuild.exe`）实际编译 + 侧载安装 + 跑起来看过了**，不是只有源码
没验证过。过程中真的编译器和真的运行时各揪出一个 bug（下面"踩过的坑"那节详细写了），已经修好。

**视觉风格按 Windows 10 版本 1511（November Update, 10.0.10586）那个年代来**：扁平 Metro，直角，
无圆角、无亚克力背景模糊、无 Reveal 高光——这些都是 2017 年 Creators Update（10.0.15063）往后才有的
Fluent Design 元素，10586 时代根本没有。控件也尽量用原生默认长相（原生 `Button`/`ListViewItem` 自带的
浅色按下反馈就是那个年代的标准交互，没有另外重画一套模板），只有底部四 Tab（图标在上、文字在下、
选中变色）这种原生控件没有对应默认长相的，才自己写了一个最简单的 `ControlTemplate`。

## 关于最低版本号：最终定的是 10240（比你要的 10586 还早一版）

一开始你要的是最低版本 10.0.10586（November Update），我这台机器上没装那个 SDK 组件，中途拿
15063（Creators Update）先顶了一版做验证。后来你说手头有 **10.0.10240（Windows 10 RTM，
2015 年 7 月那个最初版本）** 的 SDK，那就更狠——直接把 `MoonWeChat.csproj` 和
`Package.appxmanifest` 里的 `TargetPlatformMinVersion` / `TargetDeviceFamily MinVersion`
都改成了 `10.0.10240.0`，重新 Restore + Build + 侧载安装 + 启动，**实测通过**，界面和之前
15063 那版截图一样，没有任何差异。也就是说现在这份代码对 10240 首发版就是干净的，比最初要求的
10586 门槛还低，向下兼容只会更宽松，不用你再装额外的 SDK 组件。

（如果以后想反过来把门槛提回 10586/15063，只需要改回这两处的版本号数字，代码不用动——
这套源码本身在整个 10240→最新 的范围里都不依赖任何版本相关的 API。）

## 这次做了什么

- **会话列表页**（`Views/ChatListPage.xaml`）：“微信”主页，置顶/免打扰/未读角标/群人数都做了。
- **聊天会话页**（`Views/ChatPage.xaml`）：单聊、群聊共用一个页面，覆盖了后端支持的全部消息类型——
  文本（含引用回复）、图片、表情包、语音（带时长和未读绿点）、视频、文件、位置、名片、链接分享、
  小程序卡片、红包、转账、系统提示（撤回/拍一拍/入群公告）、发送中/发送失败三态。
  输入栏做了文字/语音切换、表情面板、"+" 更多面板（相册/拍摄/位置/名片/文件/红包/转账入口）。
- **通讯录页**（`Views/ContactsPage.xaml`）、**发现/我 占位页**（`Views/PlaceholderPage.xaml`）：
  撑起底部四个 Tab，不让导航有死路，但没有深入做（不在这次“聊天页面”的范围里）。
- 底部有一个真的能跳转的四 Tab 导航（`Controls/BottomTabBar.xaml`）。
- **工程骨架**（`MoonWeChat.sln`、`src/MoonWeChat/MoonWeChat.csproj`、`Package.appxmanifest`、
  `Assets/*.png`）：这次是补上了的，不用再自己新建项目往里拖文件，双击 `.sln` 直接开。
  `Assets` 里的图标是脚本生成的纯色占位图（微信绿底 + 白色圆点），能让打包/清单校验通过，
  真要发布之前换成正式图标即可。

## 目录结构

```
Project-大月墜落狂想/
├─ MoonWeChat.sln                     ← 双击这个，或者直接开 src/MoonWeChat/MoonWeChat.csproj
├─ docs/
│  └─ WeChatPadPro-API-notes.md      ← 后端能力调研笔记，先看这个
├─ SETUP.md                          ← 就是这份文件
└─ src/MoonWeChat/
   ├─ MoonWeChat.csproj              ← 工程文件，TargetPlatformMinVersion=10.0.10240.0（见上）
   ├─ Package.appxmanifest           ← 应用清单
   ├─ Assets/                        ← 占位图标（脚本生成，正式发布前换掉）
   ├─ Properties/AssemblyInfo.cs
   ├─ App.xaml / App.xaml.cs         ← 应用入口，首页指向 ChatListPage
   ├─ Controls/                      ← AvatarView（文字头像）、BottomTabBar（底部四Tab）
   ├─ Converters/                    ← 各种 IValueConverter
   ├─ Common/                        ← BindableBase、RelayCommand、Glyphs（图标码点）、
   │                                    两个 DataTemplateSelector
   ├─ Models/                        ← ChatSession / ChatMessage / Contact / 两个枚举
   ├─ Services/SampleDataService.cs  ← 示例数据（9 个会话，2 个覆盖全部消息类型）
   ├─ ViewModels/                    ← ChatListViewModel、ChatViewModel
   ├─ Views/                         ← ChatListPage / ChatPage / ContactsPage / PlaceholderPage
   └─ Styles/                        ← ColorsAndBrushes / ControlStyles / MessageBubbleTemplates
```

## 怎么跑起来

双击 `MoonWeChat.sln`（或者直接拿 `src/MoonWeChat/MoonWeChat.csproj` 开），选 x64，F5。
命名空间已经是 `MoonWeChat`，不需要再批量替换。首页是会话列表，点一个会话进去就是聊天页。

**如果你的项目盘符和系统临时目录（`%TEMP%`，一般在 C 盘）不是同一个盘**（我这台机器上项目在 E 盘，
就踩到了这个），打包阶段的 `GenerateProjectPriFile` 步骤有几率报
`PRI210: 0x800704c8 文件移动失败`——这是 MakePri 工具自己"生成资源索引文件时先写临时目录、
再跨盘移动"的老毛病，不是咱代码的问题。碰到的话在开始编译前把 `TEMP`/`TMP` 环境变量临时指到
项目所在的盘（随便一个文件夹都行）就好，编译完再改回来没影响。

## 关于兼容性：10240 能用到的 API 边界

最初的草稿用了几个实际是 **Fall Creators Update（RS3, 10.0.16299）以后才加进 UWP 的属性**——
`StackPanel.Spacing`、`Grid.RowSpacing`/`ColumnSpacing`、`Control.CornerRadius`（RS5,
10.0.17763 才有）。这一版已经**全部替换掉了**：间距一律改用显式 `Margin`，圆角一律去掉（这个年代
的扁平 Metro 风格本来就不带圆角）。也就是说，现在整套代码在 API 层面对 10586 是干净的。

**Segoe MDL2 Assets** 这个图标字体从 Windows 10 RTM（10240）就随系统自带，10586 上没问题；
但这套项目用到的极少数生僻图标码点，实测跑起来后发现"+"更多面板里"相册"那个图标（原来用的
`U+E155`）显示出来是个"电脑+放大镜"，根本不是相册，已经换成了图片消息占位用的同一个码点
`U+EB9F`（Segoe MDL2 里的 "Pictures"，实测确认显示正常）。如果还有别的图标在你机器上显示不对，
去 `Common/Glyphs.cs`（以及直接写在 XAML 里的 `&#x...;`）挑一个 Character Map 里当时就有的码点
换掉就行，纯视觉小修，不影响其它代码。

## 踩过的坑（编译器/运行时揪出来的真 bug，已修）

代码写完之后我拿真的 MSBuild 编了一遍、又侧载装到本机跑了一遍，不是只交源码不验证。过程中发现
并修了三个问题：

1. **`SampleDataService.cs` 里一处 `GroupText(...)` 调用少传了 3 个参数**——编译期报错
   `CS7036`，纯粹是我手滑，编译器一次就抓出来了。
2. **`TextBox.Text` 的 `x:Bind` 原本写的是 `Mode=TwoWay, UpdateSourceEventName=TextChanged`**——
   这台机器上这套 XAML 编译器不认 `UpdateSourceEventName` 这个参数（`WMC0011` 编译错误）。
   改成了 `Mode=OneWay` 绑定显示 + 后台 `TextChanged` 事件手动回写 `ViewModel.DraftText`，
   效果一样（打字的同时"+"按钮实时变成"发送"），但不依赖那个参数。
3. **`BottomTabBar.xaml` 里 `ToggleButton` 上直接写 `IsChecked="True"`，实测运行时直接崩溃**
   （`XamlParseException: Failed to assign to property 'ToggleButton.IsChecked'`）——
   `IsChecked` 是 `bool?`，这个版本的运行时解析器处理不了标记里的字面量赋值。改成在
   `BottomTabBar` 的构造函数里用代码 `ChatTab.IsChecked = true;` 赋值，规避掉了。

这三个都不是"理论上可能有问题"，是真的编译失败 / 真的启动崩溃过，复现→定位→修复→重新验证通过。

## 哪些是真的、哪些是摆样子的

- **消息气泡的样式、布局、状态切换（发送中/失败/未读/群昵称等）是真的**，接后端时这部分不用改。
- **头像是"文字头像"**（取名字最后一个字 + 固定底色），因为没有真实图床。接后端后把
  `Controls/AvatarView.xaml.cs` 加一个 `Image`（绑定真实头像 URL，加载失败 fallback 回文字头像）就行，
  外面所有用到 `AvatarView` 的地方不用动。
- **图片/视频消息是纯色块占位**，没有真实图片解码逻辑。
- **地图缩略图是纯色块 + 图钉图标**，不是真实地图瓦片。
- **"+" 更多面板点了会真的往聊天记录里插一条对应类型的气泡**（图片/位置/名片/文件/红包/转账，字段
  是占位内容但气泡模板是真的），不会真的调用系统相册/相机/定位——`ViewModels/ChatViewModel.cs` 的
  `SendPlaceholder` 方法就是将来接后端发送接口的地方。
- **发送文字消息是真的**会加进当前会话的消息列表并滚动到底部，但只存在内存里，重启就没了。

## 后端接入（已接骨架，2026-08-06）

默认仍是**示例数据**。接真实 WeChatPadPro：

1. App 内打开 **服务器设置**（会话列表齿轮 / 「我」Tab / 「+」菜单）。
2. 取消勾选「使用本地示例数据」。
3. 填 `http://<电脑IP>:<端口>`、AdminKey 或 Token，保存并测连通。
4. **扫码登录** → 成功后拉通讯录，发文本走 `SendTextMessage`，收消息靠 sync 轮询。

实现位置：`Services/AppServices.cs`、`Services/WeChatPad/*`、`Views/SettingsPage`、`Views/LoginPage`。  
细节与未完成项见 [HANDOFF.md](HANDOFF.md)。

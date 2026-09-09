# MoonWeChat.Core

**共享业务层**（Shared Project），被两个 Shell 同时编译进各自的 App 包。

## 内容

- `Models/` — 会话、消息、联系人等
- `Services/` — AppSettings、WeChatPad HTTP、会话保活、示例数据
- `ViewModels/` — 列表 / 聊天 / 登录 / 设置 / 朋友圈
- `Common/`、`Converters/` — MVVM 与转换器

## 不含

- 页面 XAML、App 生命周期、平台导航 → 放在各 Shell

## 平台差异

可用条件编译：

```csharp
#if WINDOWS_UWP
// Win10 / W10M
#elif WINDOWS_PHONE_APP
// WP 8.1 WinRT
#endif
```

## 依赖关系

```
MoonWeChat.Core  (shared)
       ↑
       ├── MoonWeChat.UWP   (Win10 Mobile / 桌面 UWP UI)
       └── MoonWeChat.WP81  (Windows Phone 8.1 UI)
```

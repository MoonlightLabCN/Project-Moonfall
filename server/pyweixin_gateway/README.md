# MoonWeChat pyweixin 网关

电脑上的 PC 微信 RPA 桥。手机端（UWP / WP 8.1）仍是瘦客户端，只连这个 HTTP 服务。

## 电脑要准备什么

1. Windows 10/11，已登录 **微信 4.1.6+**（不要最小化到托盘后完全退出）
2. Python 3.10+
3. 安装依赖：

```powershell
cd server\pyweixin_gateway
pip install -r requirements.txt
```

## 启动

```powershell
powershell -ExecutionPolicy Bypass -File .\start.ps1
```

默认监听 `0.0.0.0:18765`。首次启动会在同目录写出 `config.json`，里面有 `admin_key` 和 `token`。

手机和电脑同一 Wi‑Fi，客户端填：

- 地址：`http://电脑局域网IP:18765`
- Token：`config.json` 里的 `token`（也可用 AdminKey 在 App 里生成）

**首次启动时 `admin_key` 与 `token` 都会被随机生成**。如需重置 token，
在 App 里用 `admin_key` 生成；如需重置 `admin_key` 本身，删 `config.json` 重新启动网关。

## 登录

- 电脑微信已经登录：手机点「扫码登录」会探测到在线，直接进会话。
- 电脑停在登录页：网关截取登录二维码，手机 App 显示后用另一台微信扫。

## 能力边界

这是 UI 自动化，不是协议后端：

- 好友/群用**备注或显示名**当会话 id，不是 wxid
- 收消息靠轮询会话列表，比协议同步慢，也不适合高频刷
- 位置 / 名片 / 链接会降级成文本发出
- 撤回、拍一拍等没有稳定 RPA 入口，会返回失败

## 调试

不依赖本机微信时：

```powershell
powershell -ExecutionPolicy Bypass -File .\start.ps1 -Mock
```

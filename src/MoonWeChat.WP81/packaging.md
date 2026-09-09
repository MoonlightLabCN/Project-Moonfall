# MoonWeChat.WP81 WP8.1 打包

## 目标

为 Lumia 920T / Windows Phone 8.1 Update 2 生成 `AnyCPU/neutral` 未签名 `.appx`，供 CMD Injector 部署。ARM 配置和原有 ARM 包保留。

## 构建

本项目的 AnyCPU 配置已经存在。源码使用 C# 7.3，因此使用 VS2017 的 MSBuild 15：

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe' 'MoonWeChat.WP81.csproj' '/t:Rebuild;Publish' '/p:Configuration=Debug' '/p:Platform=AnyCPU' '/p:VisualStudioVersion=15.0' '/p:AppxPackageSigningEnabled=false' '/p:AppxBundle=Never' '/p:BuildAppxSideloadPackageForUap=true' '/p:AppxPackageDir=AppPackages\AnyCPU_Debug\' '/m:1'
```

## 输出

当前源清单版本为 `1.1.0.0`，输出为：

```text
AppPackages\AnyCPU_Debug\MoonWeChat.WP81_1.1.0.0_AnyCPU_Debug_Test\MoonWeChat.WP81_1.1.0.0_AnyCPU_Debug.appx
```

只使用上面的 `.appx`，不要选择 `.appxupload` 或 `.appxsym`。

## 920T 部署

1. 在 CMD Injector 中选择 `.appx`。
2. 选择 `Deployment Mode`，不要对 `.appx` 使用 `Register`。
3. 将包复制到手机后执行部署。

这是未签名包，不需要安装 MoonWeChat 的 `.cer`。包应满足：清单 `ProcessorArchitecture=neutral`，不存在 `AppxSignature.p7x` 和 `AppxMetadata\CodeIntegrity.cat`。

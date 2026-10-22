# Malody V ThemeLoader

面向 Windows x64 / IL2CPP 的 Malody V 主题加载器，包含离线安装器和桌面主题管理器。

- 安装一次，之后照常从 Steam 启动游戏。
- 自动复用兼容的 BepInEx 6 IL2CPP 环境。
- 主题在 themes 目录中以可编辑文件夹存储；ZIP 作为导入导出格式，兼容导入旧 `.mtp`。
- 支持图片、字体、Live2D 纹理和循环背景视频；缺少资源时使用游戏原始外观。
- Default 由源工程原始资源生成，可导出作为主题制作起点。
- 主题搜索与预览、实时更新、画面控制，以及按清单修复和卸载。
- 内置 Live2D 工作台，可导入 Cubism 模型目录或 ZIP，选择待机和点击动画，一键应用到当前看板角色。
- 界面基于 Microsoft Fluent 2 和 Windows 11 Mica 规范，旧系统自动回退到中性背景。
- 主题管理器默认暗黑，底部太阳／月亮开关可切换并保存外观；“赞助支持”打开[爱发电主页](https://ifdian.net/a/mcpsde)。安装器固定亮色。
- 管理器会检查 GitHub Releases，下载并校验新版安装器后自动关闭并完成更新。

安装器与管理器无需协议阅读、设备授权或机器码校验。

替换看板模型（2.3.0 起）：

1. 更新加载器，重新启动游戏一次。
2. 打开管理器的 **Live2D**，选择要编辑的主题和模型。已导入的 Murasame 会自动列出，无需重新导入。
3. 选择待机、点击动画，点击 **保存并应用模型**。该操作会启用主题和实时模式。
4. 进入游戏中显示看板角色的界面；管理器会显示实际加载结果。需要恢复时点击 **恢复游戏模型**。

Murasame 样本的 `Idle / motion01.motion3.json` 适合作为待机，其他动作可设为点击动画。支持后续导入 `.motion3.json` 并重新分配；大小和位置可调整。

![主题管理器](.impeccable/review/manager-desktop.png)

![离线安装器](.impeccable/review/installer-desktop.png)

构建和验证使用 PowerShell 7：

```powershell
dotnet build MalodyVThemeLoader.slnx -c Release
pwsh -NoProfile -File scripts/build-release.ps1
pwsh -NoProfile -File scripts/test-runtime-reuse.ps1
pwsh -NoProfile -File scripts/test-theme-manager.ps1
python scripts/verify-payload.py
pwsh -NoProfile -File scripts/publish-github-release.ps1
```

离线 EXE、Default 目录 和 SHA-256 校验文件输出到 `artifacts/release`。
详见 [开发指南](docs/DEVELOPMENT_GUIDE.md)、[踩坑记录](docs/PITFALLS.md)、[Fluent 2 视觉基准](docs/FLUENT_2.md)和[桌面设计约定](DESIGN.md)。

by 猫猫虫小窝(451586654)

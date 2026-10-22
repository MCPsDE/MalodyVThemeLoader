# Malody V ThemeLoader

面向 Windows x64 / IL2CPP 的 Malody V 主题加载器，包含离线安装器和桌面主题管理器。

- 安装一次，之后照常从 Steam 启动游戏。
- 自动复用兼容的 BepInEx 6 IL2CPP 环境。
- 主题在 themes 目录中以可编辑文件夹存储；ZIP 作为导入导出格式，兼容导入旧 `.mtp`。
- 支持图片、字体、Live2D 纹理和循环背景视频；缺少资源时使用游戏原始外观。
- Default 由源工程原始资源生成，可导出作为主题制作起点。
- 主题搜索与预览、实时更新、画面控制，以及按清单修复和卸载。
- 内置 Live2D 工作台，可预览模型和动作、手动画点击区域、绑定 M1–M5 结算动画，并为每个动画添加 OGG/WAV 音频。
- 资源编辑器提供图片与音频目录树、场景和控件独立图片、可拖动的九宫格切片与拉伸预览，保存后实时应用。
- 可替换九类界面音效和结算音乐，支持 OGG、WAV、MP3 导入与试听。
- 界面基于 Microsoft Fluent 2 和 Windows 11 Mica 规范，旧系统自动回退到中性背景。
- 主题管理器默认暗黑，底部太阳／月亮开关可切换并保存外观；“赞助支持”打开[爱发电主页](https://ifdian.net/a/mcpsde)。安装器固定亮色。
- 管理器会检查 GitHub Releases，下载并校验新版安装器后自动关闭并完成更新。

安装器与管理器无需协议阅读、设备授权或机器码校验。

替换看板模型（2.3.0 起）：

1. 更新加载器，重新启动游戏一次。
2. 打开管理器的 **Live2D**，选择要编辑的主题和模型。已导入的 Murasame 会自动列出，无需重新导入。
3. 选择待机、点击动画，点击 **保存并应用模型**。该操作会启用主题和实时模式。
4. 进入游戏中显示看板角色的界面；管理器会显示实际加载结果。需要恢复时点击 **恢复游戏模型**。

点击 **预览与绑定动画** 打开编辑器。左侧可试播动作或框选区域；右侧选择区域、待机或 M1–M5 结算项，再指定动画。不会自动识别或绑定身体部件。重叠区域以后添加的为准，未绑定位置使用默认点击动画。

每个动画可绑定 OGG（Vorbis）或 WAV 音频，试播时可同时试听。相同动画用于多个触发项时共用音频；新动画会停止上一段音频，结算动画结束后回到待机。完成绑定后，在工作台点击 **保存并应用模型**。支持继续导入 `.motion3.json` 和调整大小、位置。

编辑图片与音频（2.5.0 起）：在 **资源编辑** 中选择主题和资源，再选择应用范围。可为不同场景导入不同图片；双击“使用位置”可指定控件，也可手动填写控件路径。勾选 **九宫格切片** 后拖动导线或输入四边像素值，查看下方拉伸效果。音频可导入 OGG、WAV、MP3 并试听。点击 **保存并实时应用** 会启用当前主题及实时加载；**恢复此范围** 只移除当前覆盖，继续继承场景或全局资源。升级加载器后需重启游戏一次。

大厅 BGM 来自玩家曲库，不作为主题音频替换；谱面音乐与打击音效保持游戏原有逻辑。

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

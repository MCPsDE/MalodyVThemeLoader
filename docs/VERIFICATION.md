# 2.1.0 验证记录

验证日期：2026-10-22。所有安装操作均针对仓库 `artifacts` 内的可丢弃假游戏目录，未启动游戏。

## 构建与打包

- 全部保留的项目使用 Release 构建；设备授权工具已删除。编译为零警告、零错误。
- 发布自包含 x64 单文件安装器和主题管理器，使用固定 BepInEx `6.0.0-be.785`。
- `verify-plugin.ps1` 检查实际插件程序集：引用 BepInEx IL2CPP、Il2CppInterop 和游戏互操作程序集，没有 UnityEditor 或已删除的授权类型。
- `verify-payload.py` 检查离线 ZIP 的完整性和路径，逐一校验固定运行库文件。Default 目录中的 1,617 个资源文件与源工程逐字节哈希一致，包含 1,428 张目录图片；另有主题清单。
- 最终 EXE、Default 目录、README 的 SHA-256 由发布脚本写入 `artifacts/release/SHA256SUMS.txt`。

## 安装与主题回归

| 检查 | 结果 |
| --- | --- |
| 最终单文件 EXE 的内嵌离线安装、修复、验证、卸载 | 通过 |
| 安装前 dry-run 不写入目标目录 | 通过 |
| 拒绝覆盖来源不明的 winhttp.dll | 通过 |
| 复用兼容 BepInEx，安装与修复不取得外部运行时所有权 | 通过 |
| 修复时复用自有兼容 BepInEx，保留 DLL 时间戳和用户配置，只补缺失的自有文件 | 通过 |
| 卸载后外部 BepInEx、原有插件和配置逐文件哈希保持一致 | 通过 |
| 目标文件被锁时，中途失败并恢复安装前文件树 | 通过 |
| Doorstop 禁用、Mono 入口、核心 DLL 缺失的环境保持不变并给出诊断 | 通过 |
| ZIP 导入后提取为可编辑目录、旧 MTP 迁移、导出、激活、设置持久化 | 通过 |
| Default 目录完整导出、重新导入、移除后虚拟原始外观回退、模板导出 | 通过 |
| 有效 manifest 配合路径穿越条目的压缩包被拒绝 | 通过 |
| 主题管理器卸载保留用户导入主题 | 通过 |
| 独立 BepInEx 安装器的离线 EXE 回归 | 通过 |

对应脚本：`test-installer.ps1`、`test-runtime-reuse.ps1`、`test-theme-manager.ps1` 和 `test-bepinex-installer.ps1`。完整运行输出位于本地 `artifacts`。

## 目录主题实时编辑验证

主题管理器导入 ZIP 后会写入 `ThemeLoader/themes/<id>/`，而不是把 ZIP 作为工作副本。运行时目录优先于同名 ZIP，并且 realtime watcher 递归监听 `theme.json`、图片、字体和 Live2D 文件变化；同名旧 ZIP 仅作为兼容回退。

## GitHub 自动更新验证

管理器请求 `MCPsDE/MalodyVThemeLoader` 的 latest release，只在版本高于当前程序集时下载对应安装器和 `SHA256SUMS.txt`。校验成功后以 `--install <game-dir>` 启动安装器并关闭管理器，避免文件锁。更新失败、仓库没有 release、缺失校验条目或 SHA-256 不匹配都会停止更新。发布脚本为 `scripts/publish-github-release.ps1`。

当前公开 Release：[v2.1.0](https://github.com/MCPsDE/MalodyVThemeLoader/releases/tag/v2.1.0)。

## Fluent 2 视觉复核

本轮按 Microsoft Fluent 2 公开规范重新检查了安装器、主题管理器和独立 BepInEx 安装器。官方参考见 `docs/FLUENT_2.md`。共享视觉 token 已切换为 Fluent 中性背景、brand blue、成功/危险状态；Windows 11 使用 DWM Mica，截图和旧系统安全回退为纯色背景。截图确认没有青绿色产品 chrome、渐变、发光或大胶囊按钮。

## 原生界面审查

使用全局 `impeccable` skill；原生截图从 Form.DrawToBitmap 获取，避免全屏游戏遮挡窗口。检查安装器、管理器和独立 BepInEx 安装器的正常尺寸、最小尺寸和 150% 布局缩放，以及设置、维护、搜索空结果和路径诊断，共 14 张截图。

独立审查提出的七项问题及一次修正引入的清单文字截断均已解决，修正项复核结论为 **ship**。证据与报告见 [审查目录](../.impeccable/review)及[最终复核](../.impeccable/review/finish-verdict.md)。设计实现记录在 `DESIGN.md` 和 `.impeccable/design.json`。

检查范围：150% 为布局压力检查，未声称已验证物理显示器的 DPI 切换；独立安装器的忙碌状态按源码执行顺序验证。未进行真实游戏内视觉回归。该原生项目不适用 HTML/CSS 检测器。未执行 skill 的概念种子/网页方案选择流程，这一偏离已在 surface brief 中如实记录。

## 只读输入

完整快照对比发现：源工程及稳定游戏程序文件没有变化；运行中的游戏自行更新了数据库、回放和 WebView2 缓存。完整差异保存在 `artifacts/reference-changes.json`，`check-reference-inputs.ps1 -Mode VerifyStable` 明确排除这些已观察到的活动数据，并单独验证其余文件。未回滚或编辑玩家运行产生的数据。

## 美术来源

Default 资源来自只读源工程，逐文件哈希验证；界面预览来自主题内真实图片。应用图标 `src/MalodyVThemeLoader.Windows/Assets/mtp.png` 和 `mtp.ico` 为工作区已有文件，本次未修改。没有生成或替换美术资源。

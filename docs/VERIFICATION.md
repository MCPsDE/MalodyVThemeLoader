# 2.2.2 验证记录

验证日期：2026-10-22。所有安装操作均针对仓库 `artifacts` 内的可丢弃假游戏目录，未启动游戏。

## 构建与打包

- 全部保留的项目使用 Release 构建；设备授权工具已删除。编译为零警告、零错误。
- 发布自包含 x64 单文件安装器和主题管理器，使用固定 BepInEx `6.0.0-be.785`。
- `verify-plugin.ps1` 检查实际插件程序集：引用 BepInEx IL2CPP、Il2CppInterop 和游戏互操作程序集，没有 UnityEditor 或已删除的授权类型。
- `verify-payload.py` 检查离线 ZIP 的完整性和路径，逐一校验固定运行库文件。Default 目录中的 1,617 个资源文件与源工程逐字节哈希一致，包含 1,428 张目录图片；另有主题清单。
- 最终 EXE 和 README 的 SHA-256 由发布脚本写入 `artifacts/release/SHA256SUMS.txt`；Default 目录的资源哈希由 payload 检查覆盖。

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
| 重复 Unicode Live2D ZIP 项导入采用最后一项并完成目录提取 | 通过 |
| 同 ID 中文主题覆盖导入，以及中途解压失败保留已有主题 | 通过 |
| Live2D 工作台模型目录、ZIP、动画和已有纹理替换路径 | 已编译并通过目录/压缩包校验；真实游戏内 rig/Animator 仍需逐模型验证 |
| Default 目录完整导出、重新导入、移除后虚拟原始外观回退、模板导出 | 通过 |
| 有效 manifest 配合路径穿越条目的压缩包被拒绝 | 通过 |
| 主题管理器卸载保留用户导入主题 | 通过 |
| 独立 BepInEx 安装器的离线 EXE 回归 | 通过 |

对应脚本：`test-installer.ps1`、`test-runtime-reuse.ps1`、`test-theme-manager.ps1` 和 `test-bepinex-installer.ps1`。完整运行输出位于本地 `artifacts`。

## 目录主题实时编辑验证

主题管理器导入 ZIP 后会写入 `ThemeLoader/themes/<id>/`，而不是把 ZIP 作为工作副本。运行时目录优先于同名 ZIP，并且 realtime watcher 递归监听 `theme.json`、图片、字体和 Live2D 文件变化；同名旧 ZIP 仅作为兼容回退。

## GitHub 自动更新验证

2.6.2 增加 `test-update-service.ps1`，48 项断言用本地 HttpMessageHandler 和替代启动器验证真实更新服务，不连接 GitHub、不启动安装器。覆盖流式字节与百分比、未知长度、SHA-256 不匹配、缺失校验文件、截断下载、取消、拒绝关闭未保存编辑、启动失败、当前版本、HTTP/格式失败和失败清理；另在无 UI 消息泵超过六秒时检查后台租约续期、其他客户端所有权和关闭断开。`test-update-ui.ps1` 在两个外观、正常/最小窗口、合成 150% 和 125% 字体下验证更新条、取消/重试、可访问值和文本尺寸。新截图为 `.impeccable/review/update-dark` 与 `update-light`，均为合成更新数据；不代表真实网络测速或物理 DPI/屏幕阅读器实测。

2.6.2 制谱器性能与调试恢复：`test-inspector-runtime.ps1 -Standalone` 通过 4,842 项断言，含 2,400 同名同级节点按线性读取构建精确地址、无节点规则时不建立索引、重排/改名的新批次无旧缓存、销毁自有 CanvasRenderer 后重建、瞬时故障后重试和恢复后的最终屏幕线框像素。仍是 Unity Windows Mono 隔离播放器，不能冒充已安装 IL2CPP 游戏的端到端复测。此前真实游戏冻结时的主线程栈已指向逐图重复地址计算，本次代码移除此无条件路径。明暗两套在线调试编辑器回归和共享控件约束通过。参考目录始终只读，生成数据均位于仓库。

2.6.2 全目录元数据审计仍保留失败：运行游戏生成了空 `chart.bin_unqlite_journal`，并改写 BetterMalodyV 的配置文件时间戳；没有为此扩大通用排除列表或重录基线。完整差异保存在 `artifacts/reference-changes.json`。单独精确检查源工程全树、所有游戏 DLL/EXE、MalodyV_Data、所有插件二进制和用户主题均为 0 项变化，记录在 `artifacts/reference-protected-2.6.2.json`。这不声称整个活动游戏目录完全未变。

管理器请求 `MCPsDE/MalodyVThemeLoader` 的 latest release，只在版本高于当前程序集时下载对应安装器和 `SHA256SUMS.txt`。校验成功后以 `--install <game-dir>` 启动安装器并关闭管理器，避免文件锁。更新失败、仓库没有 release、缺失校验条目或 SHA-256 不匹配都会停止更新。发布脚本为 `scripts/publish-github-release.ps1`。

本次版本：[v2.4.0](https://github.com/MCPsDE/MalodyVThemeLoader/releases/tag/v2.4.0)。

## 2.4 手动动画编辑器、结算与音频

查阅 Emiria 的 PanelResultScore、SceneResult 与 PlayEnums，确认 M1–M5 对应已绘制评级 D/C/B/A/S。插件只观察三个结算显示 Fill 重载后的 rankLayers；歌曲信息 Fill 不触发事件，不补丁 Score 或分数/判定/提交逻辑。接口检查包含 3 个看板钩子、3 个结算显示目标签名，以及新增点击投射、结果 UI 和 AudioSource 的真实 interop 引用。

`test-live2d-editor.ps1` 用 artifacts 中的样本副本和运行库副本验证：真实 moc/纹理和不同动作画面、手动画区域、重叠优先与点击回退、M1–M5 独立绑定、非法区域拒绝、JSON 往返，以及静音 OGG/WAV 的解码、导入、试播准备与中断。明暗各四张原生截图覆盖正常/最小窗口、结算绑定及模拟 150% 布局。没有用平面纹理冒充模型预览。

GPU 绘制缓存不可变纹理与 shader；独立采样在本机预热后约 7–8 ms/420×560 帧，包含脚本截图/文件写入的两帧回归约 150–180 ms，不能视为所有机器的帧率承诺。测试音频是静音样本，只证明解码/调用和停止状态，不声称听觉音质验证。

编辑器在自己的进程中加载原游戏 Cubism Core，不操作用户游戏窗口。真实安装目录保持只读；结算动画与游戏内音频仍需安装后端到端确认。物理显示器 DPI、完整辅助功能及所有外部模型的物理/表情控制器未验证；这些不在当前原始模型动画播放路径内。

## 2.3.2 返回大厅的模型切换时机

用户在 2.3.1 已成功显示 Murasame，但返回大厅时会先闪现原模型。检查确认角色只在每 0.4 秒的发现扫描中替换，SceneLoaded 也早于角色 Start，存在可见间隙。2.3.2 在游戏看板 Start 与 Show 返回时同步调用替换，不受扫描冷却影响；已有替换只重申隐藏状态，已销毁场景对象先清理。失败时仍保留原模型。

发布接口检查新增三个 Live2D Harmony 字符串目标的唯一性及签名校验，覆盖初始化、重新显示和点击。Release 编译和假目录回归不代表已在用户正在运行的游戏中完成切场景首帧验证，真实目录保持只读；安装后仍需重启并确认该画面。

## 2.3.1 真实运行接口故障

真实游戏已加载 2.3.0，Murasame 的 replacement.json、活动主题和 realtime 均正确，但日志逐帧报 `WidgetLive2dControl.get_bkmv()` 不存在，状态文件仍为 waiting。实际 interop 缓存和编译缓存的私有字段不同；这次故障发生在模型创建之前。

新增元数据链接检查，无需启动或写入游戏即可复现旧版两个失效引用。修正版移除这些引用，从公开角色容器查找已实例化的模型，并逐项校验插件对真实 interop 的游戏/Unity 成员引用。发布脚本执行同一检查，并将经过校验的当前接口缓存打包。顶层运行错误会停止 Live2D 更新并写 error 状态，防止逐帧重复刷日志。

修正版分别针对旧编译缓存与真实游戏当前接口完成 Release 编译；210 项直接成员引用及 9 项 Cubism 委托接口签名在两套缓存中均通过校验。Cubism 适配层按保留的 SDK accessor 方法名绑定托管委托，不依赖显示属性别名或裸指针调用。

此检查证明接口可解析，不能替代真实游戏内渲染验证；修正版仍需安装后重启游戏确认。真实安装目录保持只读。

## 2.3 外部 Cubism 模型与动画

实际 Murasame 主题已经启用 realtime，但旧日志的 Live2D overrides 为 0：原工具仅复制模型目录，既无目标绑定也无原始 Cubism 模型创建流程。2.3 增加 `replacement.json`、原生 moc/model/render 工厂、遮罩和待机/点击动作驱动。严格匹配已验证 GameAssembly SHA-256；更换游戏版本时保留原模型并返回状态。

`test-live2d.ps1` 使用只读外部样本的副本，验证递归发现、12 个动作、反斜杠引用、四种插值、中文动作导入、重复覆盖、缺失依赖保留旧模型、自定义 live2dRoot、绑定失败回滚、实际窗体 Apply/Restore、主题/实时模式激活，以及旧插件更新提示和运行状态反馈。

`test-live2d-render.ps1` 在 artifacts 内独立 Unity 2022.3.62f3 工程中使用同一模型工厂与原游戏 Cubism SDK 副本。Murasame 渲染结果为 86 个网格、72 个参数；生产曲线计算器采样的待机和点击动作产生不同画面。实际打开截图核对层级及刘海遮罩。外部模型、美术和该测试工程均未加入发布包或源码仓库。

这不是已安装 IL2CPP 游戏中的端到端运行验证：编辑器测试使用对应公开 SDK 属性，发行插件编译验证了生成 interop 的适配接口；真实游戏需要更新后重启一次。未操作原游戏进程或更改玩家目录。外部应用的物理、表情控制器和音频不在当前原始模型播放路径内。

界面检查覆盖明暗、正常/最小窗口、模拟 150% 布局，以及滚动与 Tab 到达动画及尺寸字段。捕获脚本断言字段和滚动区域都在实际父视口内。真实物理显示器 DPI 切换仍未验证。证据见 `.impeccable/review/live2d-dark` 和 `live2d-light`；独立复核与文档交接记录位于同级报告。

## Fluent 2 视觉复核

本轮按 Microsoft Fluent 2 公开规范重新检查了安装器、主题管理器和独立 BepInEx 安装器。官方参考见 `docs/FLUENT_2.md`。共享视觉 token 已切换为 Fluent 中性背景、brand blue、成功/危险状态；Windows 11 使用 DWM Mica，截图和旧系统安全回退为纯色背景。截图确认没有青绿色产品 chrome、渐变、发光或大胶囊按钮。

## 原生界面审查

2.2.2 将管理器外观下拉框替换为手绘太阳／月亮双段开关，验证鼠标点击、左右键和无障碍选中状态；偏好保存和新进程恢复沿用现有测试。两个安装器固定亮色，检查无外观开关或赞助按钮，并确认启动安装器没有改写管理器的暗色偏好。输出直接使用 `artifacts/release`，旧安装器打开时仍能构建新版本。

2.2.1 补齐了暗色默认、即时明暗切换、独立偏好持久化、指定作者水印、仅含“赞助支持”的多色按钮，以及 Live2D 简洁文案。执行了真实外观选择器切换、全部控件颜色往返比对和新进程亮色恢复；测试不打开浏览器，不覆盖真实用户偏好。

明暗两组共 36 张窗口截图覆盖正常尺寸、最小尺寸、设置、Live2D、维护、空搜索、目录错误、切换/恢复和模拟 150% 布局。独立审查结论为 `ship`，见 [外观复核](../.impeccable/review/appearance-verdict.md)；配色记录和文档交接已完成。截图不验证物理显示器 DPI 或系统合成材质。

Live2D 长篇格式/边界说明已从界面移除，技术资料留在开发文档中；GIF 文件类型显示为“GIF 首帧”。动画后缀修正由 `.motion3.json` 列表回归检查覆盖。

使用全局 `impeccable` skill；原生截图从 Form.DrawToBitmap 获取，避免全屏游戏遮挡窗口。检查安装器、管理器和独立 BepInEx 安装器的正常尺寸、最小尺寸和 150% 布局缩放，以及设置、维护、搜索空结果和路径诊断，共 14 张截图。

独立审查提出的七项问题及一次修正引入的清单文字截断均已解决，修正项复核结论为 **ship**。证据与报告见 [审查目录](../.impeccable/review)及[最终复核](../.impeccable/review/finish-verdict.md)。设计实现记录在 `DESIGN.md` 和 `.impeccable/design.json`。

检查范围：150% 为布局压力检查，未声称已验证物理显示器的 DPI 切换；独立安装器的忙碌状态按源码执行顺序验证。未进行真实游戏内视觉回归。该原生项目不适用 HTML/CSS 检测器。未执行 skill 的概念种子/网页方案选择流程，这一偏离已在 surface brief 中如实记录。

## 只读输入

完整快照对比发现：源工程及稳定游戏程序文件没有变化；运行中的游戏自行更新了数据库、回放和 WebView2 缓存。完整差异保存在 `artifacts/reference-changes.json`，`check-reference-inputs.ps1 -Mode VerifyStable` 明确排除这些已观察到的活动数据，并单独验证其余文件。未回滚或编辑玩家运行产生的数据。

## 2.5 资源编辑验证

- Release 构建通过，0 警告、0 错误。实际游戏 interop 的 238 个游戏/Unity 引用、9 个 Cubism 接口、3 个看板钩子、3 个结算显示入口、3 个 UI 音频入口、九个音效枚举及唯一实例音量 getter 校验通过。
- 源目录生成 1,428 张图片、380 个直接场景使用位置和 10 个 UI 音频事件；Cancel 与 MsgFail 保持独立事件，允许分别替换。
- `test-resource-editor.ps1` 在独立假游戏目录中通过暗色、亮色验证：目录树、全局/场景/控件文件与切片、手动范围重新读取、取消定位、同主题外部配置重读、ZIP 物化后的主列表目录身份、恢复继承、原子验证失败、OGG/WAV/MP3、静音试听启停、活动主题及实时设置。
- `test-resource-render.ps1` 在仓库独立 Unity 项目中用发行图片解析代码创建并渲染两个实际切片 Sprite；检查纹理隔离、来源、控件覆盖、border、pivot、PPU、游玩场景排除和缺失/非法资源回退。结果在 `artifacts/resource-render-test/result.json`，图像为 `scene-slices.png`。IL2CPP byte-array 包装及未调用的 Mono 写入重载是测试适配，未改变发行逻辑。
- 新 UI 的十张捕获位于 `.impeccable/review/resources-dark`、`resources-light`：图片、控件范围、音频、最小窗口和合成 150%。Live2D 模型/动画回归、外观偏好写入、新进程恢复及配色往返通过。

没有在实际安装游戏中端到端运行新功能。音频回归使用静音样本，未验证用户声卡输出；合成缩放未验证物理显示器 DPI 切换；静态位置列表未声称穷举动态 prefab 或提供整场景实时预览。

## 2.5.1 数字输入与文字完整性修正

- 整个解决方案 Release 构建通过，0 警告、0 错误。新增共享 `NumberField`、`SingleLineLabel`、`ButtonRow` 和 `FieldGrid`；原生数值解析器保留，白色上下箭头隐藏并裁出可见区域。
- `test-ui-constraints.ps1` 和 `assert-ui-fit.ps1` 检查五个管理器页面及两个安装器：完整动作/导航文案、输入与步进区域、数值范围文本、父容器边界，以及正常/最小窗口、模拟 125/150/200% 和 125% 字体。暗色、亮色均通过。
- 输入回归覆盖部分负号输入经过布局仍保留、提交校验、原生方向键、减/加按钮、范围及禁用状态、可写无障碍值。`inspect-ui.ps1` 另外通过从待机、点击动画到尺寸字段的真实 Tab 路径与滚动边界检查。
- 资源编辑和 Live2D 工作台/动画回归通过；动画编辑器临时 harness 调用相同文字几何断言。外观偏好写入、新进程恢复、明暗往返和两个固定亮色安装器检查通过。
- 32 张本次捕获及独立复核见 `.impeccable/review/controls-*`。项目和用户全局 `AGENTS.md` 均保存通用视觉约束与补丁版本策略，版本为 2.5.1。

这些检查不代表物理显示器 DPI 切换、屏幕阅读器、声卡输出或实际安装游戏的端到端验证。用户在本轮验证期间自行启动了游戏，完整快照保留其日志、缓存和配置差异；稳定资源检查精确排除已观察到的运行文件，不保证配置内容未变，也没有修改或还原玩家文件。

## 2.6 在线调试与节点装饰

- 全解决方案 Release 构建通过，0 警告、0 错误；实际游戏 interop 的 311 个游戏/Unity 引用及现有 Cubism、看板、结算、音频入口校验通过。
- `test-scene-inspector.ps1` 明暗检查通过：实时层级/搜索/隐藏节点、空 UI 节点底图和 VP8 WebM、视频不透明度与填充、非法编码保留旧规则、中键选取后的精确资源定位、同名兄弟隔离、主题/实时启用、恢复和 ZIP。所有可见命令和数字框通过共享几何断言；正常、最小、合成 150% 捕获各八张。
- `test-inspector-runtime.ps1` 在仓库独立 Unity 2022.3.62f3 播放工程中通过 28 项断言：真实 VP8 Alpha 解码、45% 视频与底图的实际像素混合、前景保留、裁剪拾取、重叠轮选、双向选择、自有 UI 中键拦截/普通点击穿透、4,000 节点导致的线框分批、投影坐标反变换、租约失效和错误资源清理。实际打开 `hierarchy.png`、`node-video.png`；合成图片和视频只用于验证。
- 原有资源编辑流程、真实 Unity 切片渲染、五个管理器页面/两个安装器的数字与完整标签矩阵（明暗、正常/最小、合成 125/150/200% 及 125% 字体）、外观新进程恢复和安装器固定亮色回归均通过。

证据在 `.impeccable/review/inspector-dark`、`inspector-light` 和 `inspector-runtime`。独立测试适配 IL2CPP 数组/委托及 Mono 写入重载，没有操作真实安装游戏，亦未验证物理 DPI、屏幕阅读器或实际 IL2CPP 输入事件交付。当前仅对已加载的非游玩界面提供检查及 UI 节点装饰，详细规则和边界见 `docs/SCENE_INSPECTOR.md`。

本轮完整目录审计没有整体通过：期间发生了其他 BetterMalodyV 插件安装、murasame 主题导入和活动主题变更，完整 1,725 条差异记录保留在 `artifacts/reference-changes.json`，没有重录基线或回滚玩家文件。精确复查源工程全树，以及游戏 exe、UnityPlayer、GameAssembly、Data、BepInEx 核心/interop/unity-libs、本加载器插件/管理器/文档和 Default，变化记录均为 0；范围和分类在 `artifacts/reference-scoped-2.6.json`。这不宣称整个游戏目录未变。

## 2.6.1 调试覆盖层修复

- 从当前安装游戏只读日志确认 `Inspector drawing stopped: Method unstripping failed`。实际 interop 的 `Font.CreateDynamicFontFromOSFont(string,int)` 方法体只抛 NotSupportedException；旧 2.6.0 DLL 在新增检查中被拒绝，修复版的 311 个接口引用通过检查。原有可选字体映射的局部失败回退被单独记录，不视为调试绘制的可用接口。
- 修复复用游戏字体、内置字体回退和无字体重试，保证文字失败不停止边框；清理保留借用字体。全解决方案隔离 Release 构建通过，0 警告、0 错误。
- `test-inspector-runtime.ps1 -Standalone` 在 Unity 2022.3.62f3 Windows Mono 播放器中通过 33 项断言，包括正常屏幕呈现的边框/名称像素、借用字体、字体缺失后的边框、名称恢复，以及关闭后原字体保留。最终画面在 `.impeccable/review/overlay-2.6.1/overlay-presented.png`，另含离屏对照、视频和 result.json。
- 播放器在从不切换的独立 Windows desktop 中正常呈现；没有操作用户游戏窗口。该测试适配 IL2CPP 数组/委托包装和 Mono 写入重载，仍不是实际 Malody V 的 IL2CPP 端到端运行。

## 美术来源

Default 资源来自只读源工程，逐文件哈希验证；界面预览来自主题内真实图片。应用图标 `src/MalodyVThemeLoader.Windows/Assets/mtp.png` 和 `mtp.ico` 为工作区已有文件，本次未修改。没有生成或替换美术资源。

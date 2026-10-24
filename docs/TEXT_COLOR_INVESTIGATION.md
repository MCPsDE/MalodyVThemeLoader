# 游戏界面文字颜色调查

调查日期：2026-10-24。当前加载器版本：2.6.10。本次只读检查游戏与源工程，没有修改运行逻辑、主题配置或发布新版本。

## 当前编辑器能力

`ThemeResources` 只有图片、音频和节点底图/视频规则，没有文字颜色规则。`ThemeNodeRule` 不含文字颜色、选中态或悬停态字段。`SceneThemeApplier` 对 `TMP_Text` 与旧 `UnityEngine.UI.Text` 仅替换字体；字体恢复清单也只记录字体。因此当前管理器无法设置游戏文字颜色，替换背景图片或字体文件不会覆盖组件的颜色。

## GLOBAL / REALTIME / LOCAL

源资源为 `E:\UnityProjects\Emiria\Assets\Malody\Prefabs\Panel\PanelChart.prefab`。这三个标签都是旧版 UGUI Text，关闭富文本，没有独立文字图片。

| 标签 | 预制体内相对节点 | 文字组件 fileID |
| --- | --- | --- |
| GLOBAL | scoreTab/Text | 667552649575119164 |
| REALTIME | scoreTab/Text (2) | 8925468560786768941 |
| LOCAL | scoreTab/Text (1) | 3646829466193316654 |

不要把这些预制体相对路径当成已验证的游戏场景完整地址，也不要只按 Text 的名字匹配。运行时仍需从实际实例获取场景、层级与重复节点地址。

`Scripts/Scene/Widget/WidgetRankToggle.cs` 的 `SetCurrent` 每次按当前分类重写三个标签的颜色：

- 选中：RGB 约 `#160202`，接近黑色的暗红，代码赋值 Alpha 为 1。
- 未选中：RGB 约 `#17697B`，青蓝色，代码赋值 Alpha 为 1。
- Awake 与 Toggle 都会调用 SetCurrent，因此一次性修改 Text.color 或初始预制体颜色会在切分类后失效。

三个标签还带有 `AniAlpha`，预制体配置普通透明度为 0.8、悬停为 1，使用 0.15 秒渐变。SetCurrent 直接赋完整 Color 与悬停渐变共同决定实际透明度；不能笼统描述为任何时刻都是 80%。

稳定的主题实现应观察已显示的选中背景或既有文字状态，在游戏原有更新之后只应用主题表现层的普通/选中 RGB；保留原透明度动画。无需修改排行榜分类设置、请求或切换行为。

## 其他文字的颜色来源

| 来源 | 本次查到的实例 | 修改方式与限制 |
| --- | --- | --- |
| UGUI Text.color | 上述排行榜标签和普通 UI Text | 对选定场景、完整节点地址与组件应用 RGB；Alpha 单独选择是否覆盖 |
| TMP_Text.color | 场景中的 TextMeshProUGUI | 设置目标组件颜色；同时检查是否使用顶点渐变与富文本 |
| 脚本状态着色 | WidgetRankToggle；ItemScore 中红蓝队名色；PanelBindDisplay 的有效/无效色 | 支持状态颜色映射，不能只在场景载入时改一次，也不能为改色修改业务状态 |
| 富文本颜色 | PanelProfileInfo、PanelChatBottom 等字符串中的 `<color=...>` | 基础组件颜色不能保证替换所有分段颜色；必须明确选择保留富文本配色还是统一文字颜色，并保持内容和本地化不变 |
| TMP 顶点渐变与材质 | SceneSong 序列化了 fontColor / fontColorGradient；PanelConfig 复制 fontSharedMaterial 与 color | 纯色、渐变、面色、描边是不同层次；如需改材质，使用目标实例独立材质并在恢复时释放，不能直接改共享字体材质 |
| 悬停和动画透明度 | AniAlpha、UIHoverAlpha、UIHoverAlphaTMP；多处动画有 m_Color.a 曲线 | 默认仅换 RGB，保留渐隐、悬停和父 CanvasGroup 的透明度；显式覆盖 Alpha 需要另外定义行为 |
| 图片中的字 | 图标和按钮图片内烘焙文字 | 使用图片替换流程，文字组件颜色无效 |

## 适合现有管理器的扩展

在线调试选择文字节点后提供文字颜色编辑：显示当前颜色，支持 RGB/HEX，独立的透明度覆盖开关、普通/选中/悬停配色，以及恢复原色。规则存储在主题内，按场景、完整节点地址和文字组件定位；不按显示文本或字体名全局替换。重复列表行需提供明确的实例/模板范围，不能悄悄影响其他同名节点。

运行时只维护已经绑定且仍存活的文字组件，利用目标场景与原有有界发现机制补齐新实例，不逐帧全局扫描制谱器。关闭主题/重载时恢复原本表现；状态色和动画不能只记首帧的颜色后永久覆盖。范围继续限于已支持的非游玩 UI，不进入成绩、判定、输入或联网逻辑。

以上为实施前调查记录。扩展现已在 2.6.11 实现；使用方式、配置和验证范围见 [文字配色与开屏说明](TEXT_AND_OPENING.md)。原调查中关于资源与颜色来源的只读结论继续保留。

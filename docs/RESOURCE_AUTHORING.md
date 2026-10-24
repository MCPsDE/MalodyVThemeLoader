# 图片与界面音频编辑（2.5）

“资源编辑”直接编辑主题目录。在目录树中选择图片或界面音频，选择场景，导入替换文件，然后点击“保存并实时应用”。保存会启用该主题和实时模式；预览中的修改在保存前不会写入主题。关闭或切换资源时可以放弃未保存修改。

图片可应用于所有场景、单个场景，或该场景中的一个控件。“使用位置”来自源工程中直接序列化的场景图片，可双击填入场景、层级和类型；运行时动态创建的控件也可手动填写完整路径。列表不穷举动态工厂、嵌套 prefab 或脚本运行时赋图。控件路径从场景根对象起，使用 `/` 分隔，匹配每一层；仅去掉对象名末尾的 `(Clone)`。

手动修改控件路径或类型后，点击“读取此范围”，再编辑其图片和切片。读取前保存和导入被禁用；“取消定位”返回已加载范围并保留原草稿。双击使用位置会直接读取。无未保存修改时，F5 或同主题刷新会重读目录配置；有草稿时保留当前修改。

覆盖优先级为控件、场景、全局，最后使用主题原有 images 目录或游戏原图。“恢复此范围”移除当前范围的规则，恢复继承；它不删除原图片或其他场景的设置。

“九宫格切片”使用左、右、上、下四个像素边距。可拖动原图上的四条导线，也可填写数值；下方显示指定宽高下的拉伸结果。左右之和不能超过图片宽度，上下之和不能超过高度。运行时为 Image 使用 Sliced，为 SpriteRenderer 使用 Sliced draw mode；RawImage 只显示整张纹理。恢复或热重载会还原原有显示类型。原图 pivot 和 pixels-per-unit 保留。

透明图片使用固定的中灰棋盘预览，便于在明暗界面中观察浅色或深色边缘，不改变图片本身。

音频包括 Click、MsgOK、MsgFail、Switch、Back、Cancel、Screenshot、MsgNotify、ClickLight 九个 UI 事件，以及 SceneResult 的 music_finish。支持 OGG Vorbis、PCM/float WAV、MP3。UI 事件使用独立 AudioSource，继承游戏音效音量；结算音乐使用调用者传入的音量，离开结算或调用 Stop 时停止。没有替换或加载失败时使用原音频。用户曲库、歌曲预览、谱面音乐和打击音效不被替换。

## 目录格式

主题仍使用 formatVersion 1。theme.json 新增可选 `audioRoot`，默认 `audio`。原文件可以继续直接放在 `images/<logicalId>` 下；新编辑器把独立文件存入各自根目录的 `overrides/<sha256>.<ext>`。`resources.json` 最后原子发布，图片和音频使用主题内相对路径。导出 ZIP 会保留全部规则和文件。

```json
{
  "formatVersion": 1,
  "images": [{
    "logicalId": "Textures/Malody/mainlist_bg2.png",
    "scene": "SceneSong",
    "hierarchy": "",
    "component": "",
    "file": "overrides/<sha256>.png",
    "displayName": "大厅.png",
    "border": {"x": 12, "y": 12, "z": 12, "w": 12},
    "mode": "sliced"
  }],
  "audio": [{
    "event": "Click",
    "scene": "",
    "file": "overrides/<sha256>.ogg",
    "displayName": "点击.ogg"
  }]
}
```

border 使用 Unity 顺序：x 左、y 下、z 右、w 上。空 scene 表示全局，空 hierarchy 表示该场景内全部使用位置；component 可为空、Image、RawImage、SpriteRenderer。重复或无效规则被拒绝；等优先级的含糊匹配不任意选取。图片始终按 catalog logical ID 识别，同名且同尺寸无法唯一确认时保留原资源。

## 验证范围

同一张原图可以同时拥有多条场景替换。在右侧点击“新增场景替换”，选择“应用范围”，导入新图后保存；已有场景不会被移走。在“已保存的替换”中切回大厅、选曲或具体控件，可以继续编辑各自的文件和九宫格。“恢复此范围”只删除当前选中的规则。“所有场景”作为基础图片，各场景规则优先于它。

`test-resource-editor.ps1` 使用独立假游戏目录，覆盖目录树、场景和控件优先级、切片拖动、非法配置不覆盖、三种音频解码、静音试听启停、保存和恢复继承、实时模式及活动主题。明暗和合成 150% 结果保存在 `.impeccable/review/resources-dark`、`resources-light`。

`test-resource-render.ps1` 在仓库内的独立 Unity 2022.3 项目中编译发行图片解析代码，使用同一资源配置验证不同场景纹理、来源追踪、Unity Sprite.border、pivot/PPU、控件范围、缺失/非法回退及实际九宫格渲染。只替换 IL2CPP byte-array 包装、日志和 Mono 不支持的未调用写入重载；不修改源工程或安装游戏。它不等于实际 IL2CPP 游戏端到端测试。`test-interop-compatibility.ps1` 另行校验实际 interop 的 UI 音频方法、九个枚举值与音量适配器。

安装、修复、卸载和离线包校验仍按主项目要求执行。静音试听不证明用户设备的实际听感；没有声称在用户正在运行的游戏中实测新功能。

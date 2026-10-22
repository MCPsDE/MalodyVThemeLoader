Malody V ThemeLoader 2.3.1
=======================

安装
1. 关闭游戏，运行安装器。
2. 确认或选择包含 Malody V.exe 的游戏目录。
3. 点击“安装 ThemeLoader”。已有兼容的 BepInEx 6 IL2CPP x64 将直接复用。
4. 打开桌面“主题管理器”，或照常从 Steam 启动游戏。

主题
主题在 ThemeLoader\themes\<id>\ 中以文件夹直接存储，根目录包含 theme.json；实时模式会监听目录内的文件变化。ZIP 是导入导出格式，可以直接拖入管理器，
搜索、预览、应用、导出和移除。旧 .mtp 包仍可导入，导出统一使用 .zip。
安装器附带由源工程原始资源生成的 Default\ 目录，可以导出为 ZIP 作为制作起点。
缺少的主题文件继续使用游戏原始资源。预览读取 preview.png / preview.jpg、
cover.png / cover.jpg，或主题内的 mainlist_bg2.png。

主题目录：ThemeLoader\themes
当前主题：ThemeLoader\active-theme.txt
设置文件：ThemeLoader\settings.json

设置
主题管理器默认暗黑。底部太阳／月亮开关可切换亮色与暗黑，选择会自动保存。
安装器固定使用亮色，不显示外观切换或赞助按钮。
“实时更新主题”会在运行中重新加载变更。关闭时，下次启动游戏生效。
Live2D：导入模型目录或 ZIP，选择模型、待机和点击动画，点击“保存并应用模型”。
主题与实时模式会同时启用。升级到 2.3.0 后请重新启动游戏一次，之后编辑可实时加载。
已导入的 Murasame 无需重新导入；待机推荐 Idle / motion01.motion3.json，点击可选 motion02 等。
进入游戏中显示看板角色的界面，管理器会显示实际加载结果；“恢复游戏模型”移除替换。
可继续导入 motion3 动画、调整模型大小和位置、替换纹理。GIF 文件取首帧作为纹理。
画面控制支持 Ctrl + WASD、鼠标和滚轮，并可设置底图和顶图。
背景视频支持 .mp4、.webm、.ogv；沿用静态图片的逻辑路径并修改扩展名。
例如 images/Textures/Malody/mainlist_bg2.mp4。

维护
重新运行安装器可修复安装。在主题管理器“维护”页或安装器中卸载。
卸载按文件清单与哈希处理，保留自定义主题和修改过的文件。
由其他安装器安装的 BepInEx 不归 ThemeLoader 所有，卸载会保留。
游戏不会自动启动。

日志：BepInEx\LogOutput.log
第三方组件信息：THIRD_PARTY_NOTICES.txt

by 猫猫虫小窝(451586654)
赞助支持：https://ifdian.net/a/mcpsde

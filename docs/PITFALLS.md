# 开发踩坑记录（持续维护）

每次遇到容易重现的问题，都在这里记录现象、原因、解决方式和验证方法。开始修改前先读本文件。

## Windows 命令与补丁

- 当前终端是 PowerShell。不要使用 Bash heredoc（`<<'PATCH'`）或假定 `&&` 在 Windows PowerShell 5.1 可用。
- 优先使用工具提供的 `apply_patch`。从 PowerShell 调 shim 时，用单引号 here-string 保存 diff，再调用 `codex.exe --codex-run-as-apply-patch $patch`；shim 不接受 stdin 补丁。
- 构建、发布和测试使用 `pwsh -NoProfile -File ...`。Windows PowerShell 5.1 无法加载本项目的 .NET 6/8 程序集，也没有 `Convert.ToHexString`。
- shell 字符串中的反引号、`$()` 和美元变量会被执行；不要把 JSON 序列化当作 shell 转义。
- 删除或移动目录前核实最终绝对路径；自动测试只能操作仓库 `artifacts` 下的假游戏目录。源工程与 Steam 游戏目录始终只读。

## 安装所有权与 BepInEx

- 不能仅凭 `BepInEx` 文件夹存在就判定可复用；检查 x64 Doorstop、启用状态、IL2CPP 入口、BepInEx 6 与插件依赖 API。
- 复用外部 BepInEx 时，不能把外部运行时加入 ThemeLoader 所有权清单，否则卸载会误删。升级原先由本安装器安装的环境时要保留既有所有权。
- “已安装且兼容就复用”同样适用于本安装器以前安装的 BepInEx。修复时保留现有运行库和配置，只补缺失的自有文件，并继承原有所有权，不能因为保留而丢失卸载记录。
- Unity / Il2Cpp 游戏程序集可能首次启动时才生成，BepInEx 的可选输入模块也可能不存在；检查核心运行依赖时要排除这些延迟依赖。使用固定官方 payload 做正向兼容性测试。
- `.NET InvalidDataException` 不在这里的 `IOException` 分支中捕获；环境诊断要显式捕获它，返回说明而非抛出到 CLI 或 UI。
- 不覆盖来源不明的 `winhttp.dll`。环境不完整时给出可操作的诊断，不直接重装或清理用户运行时。
- 所有写入先暂存、记录回滚信息；修复必须保留用户主题、配置与活动主题。

## 主题与界面

- ZIP 切换需覆盖导入、导出、枚举、运行时、构建器、测试和文档；旧 `.mtp` 仍应可读。
- Default 导出必须包含真实源资源；虚拟“原始外观”不能冒充完整主题。发布构建从只读源工程生成 Default 目录。
- 协议检查曾分散在安装器、管理器、CLI、插件与设置写入；删除弹窗时也必须清理凭据与授权依赖。
- WinForms 固定坐标和不换行工具栏容易在中文、缩放或小窗口中截断；使用布局容器、滚动区域，并实测正常尺寸、最小尺寸和高 DPI。
- 主题预览从 ZIP 读出后要复制图像并释放流，切换主题时释放旧 Bitmap；不要锁住主题包导致导入覆盖失败。
- Windows 桌面截图按窗口矩形截屏时，可能拍到遮挡它的全屏游戏。检查图片内容；原生控件检查优先 `Form.DrawToBitmap`，不要操作或关闭用户正在运行的游戏来获得截图。
- `apply_patch` 一次补丁不能对同一文件同时 Delete 和 Add；用 Update，或依次删除与添加。
- PowerShell 管道 `Where-Object` 可能给内部 C# 对象添加 PSObject 包装，反射参数要传 `.PSObject.BaseObject`。
- WinForms `TableLayoutPanel` 的 AutoSize 默认会保留初始高度，FlowLayoutPanel 还可能按无限宽度测量后重新换行，给父容器虚报高度。内容堆栈使用 GrowAndShrink，固定一行的按钮栏明确高度，检查 `GetRowHeights()`。
- ListView 在 Resize 中把列宽设为 `-2` 自动填满，可能在缩放或销毁时引起滚动条和 Resize 互相触发。使用单列、显式宽度、销毁状态与宽度变化保护。
- 本机 ListView 即使只有一列且列宽小于 ClientSize，仍可能保留原生横向滚动范围。主题列表实际是单列选择任务，最终采用 OwnerDrawFixed ListBox，并禁用 HorizontalScrollbar，保留原生键盘选择与纵向滚动。
- 真实游戏运行期间会自行更新存档、回放和 WebView2 缓存；保留完整目录差异，再单独验证源工程和稳定程序文件。不要把活动游戏目录整体误报为“完全未变”，也不要回滚玩家产生的数据。

## 工作区与发布

- 本次开始时已有 BepInEx 独立安装器修改及未跟踪的 `My/` 用户主题；保留这些内容，用户主题不自动提交。
- 大型默认主题和离线 EXE 放 `artifacts`，不要提交进 Git；源码推送与二进制交付分开验证。
- 全局 `impeccable` skill 已安装于用户技能目录，避免重复安装覆盖现有版本。
- 构建输出的 InformationalVersion 默认包含 Git SHA；用户界面只显示 `+` 前的产品版本，完整版本留给构建诊断。

- “现代科技感”很容易退化成青绿色、渐变、玻璃模糊和大圆角的 AI 模板。先选已有公开规范，再从规范的角色和状态实现；本项目的基准是 Fluent 2，来源和取舍记录在 `docs/FLUENT_2.md`。
- WinForms 不能凭 CSS 方式实现真正的液态玻璃；不要用半透明渐变伪造。Windows 11 通过 DWM Mica 提供系统材质，其他系统使用同 token 的纯色回退。
- 自动更新不能只下载 EXE：必须绑定 release 版本、校验文件名和 SHA-256，校验成功后再启动安装器，并让当前管理器退出释放文件锁。
- Live2D ZIP 可能包含重复的中央目录项，尤其是中文或特殊字符路径；导入校验和提取必须使用规范化路径的最后一项，不能把普通 ZIP 编辑器的重复项误报成不可覆盖错误。
- 图片或 GIF 不能自动变成真正的 Live2D rig；缺少 model3/moc/参数绑定时，伪造文件会让游戏加载失败。工具只能把 GIF 第一帧作为已有纹理替换，并明确告诉用户动画不会被生成。
- 用户要求删除界面上的 Live2D 长篇边界说明：技术细节留在开发文档，界面保留操作和结果；GIF 文件筛选项用“GIF 首帧”准确表达实际行为即可。
- 暗色模式的文字强调色与按钮底色必须分开；浅蓝文字可以读，浅蓝底上白字会丢失对比。即时切换要更新隐藏页、原生输入框、弹窗及 owner-drawn 控件，同时保持已选择的游戏主题。
- 外观偏好用 LocalAppData 中独立配置，测试通过专用路径隔离，不要覆盖用户偏好或触发真实自动更新。截图脚本中禁用更新检查。
- 追加复制 Default 目录到已存在的测试目录会形成 Default/Default，文件数量翻倍。只重置 artifacts 内已核实的 UI fixture，再复制。
- 使用 `Path.GetExtension` 判断 `.motion3.json` 或 `.mtn.bytes` 只得到最后一段，动画列表会漏项；用完整文件名的 `EndsWith`。
- ZIP 覆盖失败发生在解压阶段时，旧目标目录尚未移动；回滚不能无条件删除目标。恢复失败也不能在 finally 删除备份。
- 发布目录里的旧安装器可能由用户打开，导致整目录重建时被锁。不要结束用户窗口；统一写入 `artifacts/release`，保留旧 EXE，只覆盖当前版本并单独重建 Default。
- GitHub Release 创建时，`--target` 传短 SHA 在本环境会返回 HTTP 422（target_commitish invalid）。使用已推送的分支名或完整 40 位 SHA，上传前先核实目标。
- 用户明确要求统一输出 `artifacts/release`，不能再因旧文件被锁另建版本文件夹。发布脚本只覆盖当前版本，保留旧安装器，单独重建 Default 并只为当前版本写校验值。
- 管理器外观必须用左太阳、右月亮的直接切换控件，不能用原生 ComboBox 代替。两个安装器固定亮色，只有作者署名，不显示外观或赞助操作。
- C# 字段初始化早于构造函数体；安装器若先创建暗色控件再强制亮色，会留下混合配色。彩色字段在设置亮色之后创建；安装器的进程内覆盖不写管理器偏好。
- 外部 `.model3.json` 复制到 `live2d/models` 不代表游戏会加载它。必须保存完整模型路径、目标和动画绑定，并在运行时真正创建网格；仅启用主题或重启无法补上缺失的加载逻辑。
- Unity 源工程中的 `CubismModel3Json.ToModel` 在发行版 IL2CPP 中已裁剪。先检查实际 interop 程序集；2.3 使用保留的 moc/model 工厂和渲染接口。混淆后的属性集中在 6.7 适配层，未知游戏版本保留原模型。
- 外部动画引用可混用 `motion\\file.motion3.json` 与 `/`；按模型 JSON 所在目录解析，再保存主题 live2dRoot 的相对路径。不能按纹理或动画 basename 绑定。
- 只列动画文件或复制 motion3 并不会让动画播放。导入动作必须进入待机/点击选择器，保存绑定后由渲染模型的参数驱动；Murasame 的 PartOpacity 和 Model/Opacity 曲线也要处理。
- WinForms ComboBox 在暗色下即便设置 BackColor、FlatStyle 仍会出现白色原生外壳。使用保留原生键盘/无障碍的 ChoiceBox，自绘关闭态、箭头和下拉选项；同时验证 DrawToBitmap 和明暗切换。
- 固定在页面底部的操作区不要通过 FlowLayoutPanel AutoSize 测高，否则初次宽度不足的换行会虚报高度并挤掉编辑区。固定操作行高，内容独立滚动。
- Unity 编辑器验证脚本在 Assembly-CSharp-Editor 中，不能直接调用 Assembly-CSharp 的 internal 工厂。测试副本显式添加 InternalsVisibleTo；不要为了测试改变发行接口或修改只读源工程。
- Cubism 渲染器使用双缓冲；第一次 ForceUpdateNow 后排序标记还没应用，遮罩也需要注册到 mask texture。初始模型就绪前刷新两次并初始化遮罩，实际渲染截图检查刘海和前后层级，不能以“有像素”作为唯一通过条件。
- 动画参数在普通 Update 写入可能被 CubismModel.Update 从上一帧原生缓冲读回覆盖。放在 LateUpdate，确保晚于读回、早于 Cubism 的 PreLateUpdate 模型计算。隐藏原模型使用 forceRenderingOff 并恢复旧值，避免 SDK 自行重置 enabled 后再次显示。
- TableLayoutPanel 只有 ColumnCount=1、没有百分比 ColumnStyle 时，DPI/尺寸变化会让列按旧子控件的首选宽度撑开。显式设 100% 列宽，并检查滚动区域自身也完整位于页面内；只检查字段在滚动区域内部会漏掉整块区域超出窗口的问题。
- 多层 TableLayoutPanel 内的 NumericUpDown 焦点切换不会总是自动滚入视野；编辑字段的 Enter 显式调用 ScrollControlIntoView，测试滚动后 Tab 从待机到点击再到尺寸输入。
- 明暗切换递归遍历时，NumericUpDown 会先把新文字色传给内部编辑框。不能再次把这个文字色解释成背景 token（暗色文字和亮色画布同为 #F5F5F5），颜色转换需要区分前景和背景。
- 插件目标是 .NET 6，SHA256.HashData(Stream) 重载不可用；流式哈希使用 SHA256.Create().ComputeHash(stream)。测试工程编译器版本较新并不意味着插件运行时 API 也较新。
- 工具策略曾拒绝一条合并截图批量清理与构建的 PowerShell 命令，仅返回 blocked by policy，未说明细分原因。构建与验证改为独立调用即可继续；交付不要依赖可选清理成功，也不要把这类拒绝误报成代码构建失败。

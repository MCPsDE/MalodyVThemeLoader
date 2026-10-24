# 开发踩坑记录（持续维护）

- 移除原始色块只改目标 Image.color.a，不能把 CanvasGroup.alpha 设为 0、禁用整个 GameObject 或删除裁剪组件，否则用户添加的底图、文字、点击或裁剪会一起失效。2.6.8 仅匹配入口 tip 与结算 bgmask/三个底栏的五条完整场景层级，并逐级检查同名唯一性；其他 Image、真实背景纹理、调试装饰保持各自行为。验证透明节点仍能穿透拾取、添加半透明 PNG，并比较原色块与 PNG 重建后的渲染像素。

- 半透明 UI 不一定有专用遮罩图片。入口 `mask/tip` 用通用纯白 `g-white.png` 配 Image 黑色、alpha≈0.502；结算 `Canvas/bgmask` 与底栏也靠 Image 着色，而 `Canvas/Image` 另外叠加真实 `shop_bg1.png`（alpha≈0.447）。只换 Sprite 不会清除 Image.color，移动 RectTransform 也不会改变它。调查时分别核对纹理、组件颜色和独立叠层；不可全局清除同名 mask 或替换复用的 g-white 来处理局部效果。

- IL2CPP 的 `Transform.Find` / `parent` 可返回 CLR 基础 Transform 包装器，即便底层确实是 RectTransform，C# `as` / `is` 也会失败。2.6.6 的入口提示平移因此完全没有绑定，旧 Mono 隔离测试无法发现；改用 `GetComponent<RectTransform>()`，并在原生 IL2CPP 播放器中回放原动画、逐帧检查固定宽度和位移。场景 Start 后绑定使用实例所属 scene，避免 additive 场景下误用 active scene。
- .NET 字典保留 AudioClip 包装器不等于 Unity 资源保活：`Resources.UnloadUnusedAssets` 会清除未绑定音源的缓存语音。自建缓存设置 `DontUnloadUnusedAsset`，显式管理销毁；逐项检查 `WasCollected` 及 Unity 对象存活，失效后重建，不能一次报错就永久关闭语音。固定 Il2CppInterop 的全局 wrapper pool 命中没有排除 WasCollected；自建 AudioClip 使用与游戏 helper 相同的 Construct_Internal / CreateUserSound 接口创建新包装器，显式保留 SetData 数组至调用结束，不修改全局池。
- 必须区分自然复现与故障注入：本轮隔离 f3 IL2CPP 自然复现的是缓存被 UnloadUnusedAssets 清除后的 native NullReference，用户的 ObjectCollectedException 没有完整现场栈，不能说成相同根因已自然复现。测试主动使自有 clip/source 句柄失效，确认抛出该异常及后续触发恢复；隔离 Unity f3 的通过不能替代实际游戏 f2 的现场验证。

- PowerShell 调用 rg 时不要把 README* 或 src/Foo*.cs 当作路径参数：通配符不一定由 shell 展开，Windows 会报文件名语法错误。传真实目录，再用 -g 'README*' / -g 'Foo*.cs' 过滤；先用 rg --files 确认文件名，避免猜测协议类的文件路径。
- GitHub 草稿 Release 已存在且附件上传完成时，按 /releases/tags/<tag> 查询仍可能返回 404，gh create 还会打印 untagged 草稿地址。这不等于创建失败；用授权后的 releases 列表按 tag_name 定位，再按 release ID 校验附件的 state/size/digest。不要重复创建或重传；核对完成再发布，随后用 /releases/latest 确认正式标签。

- 即使在线调试已关闭，只要主题存在一条 nodes 规则，旧 NodeDecorationService 仍每 0.5 秒 Collect 所有已加载场景。现场 murasame 只有 SceneResult 的底图规则，却在 SceneEditor 持续遍历约 1.2 万 Image / 1.2 万 TMP 的 EditModeKeyV2。节点装饰必须预编译规则路径，只检查对应已加载场景及路径经过的同级集合；目标场景未加载时，不读取任何 Transform。每次重新验证同名数量，不能为提速按 basename 或旧实例缓存猜测目标。
- 只在拿到 Resources.FindObjectsOfTypeAll 数组后按 scene 过滤，仍然会全局搜索并创建包含音符池的大数组。普通主题与 Live2D 补漏查询应先定位已加载场景根，再 GetComponentsInChildren；制谱 UI 的 SceneEditor / SceneEditorSkin 保留支持，EditMode* 音符层与 PlayMode* 一样排除。避免为一条结算底图规则重建整份调试树；用大型无关分支和缺席场景的零节点读取断言验证。
- 扫描范围收窄应放在运行时查询入口，不能直接收紧 ThemeResources 的格式校验：旧主题可能保存过 EditMode* 规则，直接拒绝会连带整份主题无法加载。旧配置保持可读，运行时跳过音符层，并验证旧规则不会触发节点遍历。

- 对可见顶层 Form 调用 Scale(2) 时，Windows 会按当前桌面的最大跟踪尺寸限制窗口；此时子控件已放大，窗口却没放大，截图会误成“200% 布局”，树和内容区还可能被挤成一条线。大于桌面的合成检查应把已缩放的客户端控件移到指定尺寸的离屏画布，断言候选树仍有可用高度；与物理显示器 DPI 检查分别记录。

- 多文件 apply-patch 不保证整批原子完成；后面的文件匹配失败时，前面的文件可能已经写入。先检查 git diff 和目标内容，再只补失败部分，避免整批重跑造成重复段落或覆盖已完成修改。

- 普通拾取遵守 Graphic / CanvasGroup 透明度和 UI 遮罩，不能直接复用为“穿透搜索”。穿透模式只检测活动 Canvas 下有效 RectTransform 的几何区域，保留透明、禁用 Graphic 和被遮罩节点；关闭模式后恢复原过滤，不修改游戏对象。鼠标移回管理器时必须保留最后的候选，否则用户无法点选；切场景、断连、过期和关闭模式才清除。新增 IPC 开关要同步复制到后台 InspectorHeartbeat，避免续租一秒后把开关复原。
- 资源目录中的 prefab 名不保证当前场景中已有实例。`itemScore` 由成绩列表动态 Instantiate，列表行未创建、已回收或 inactive 时，不能靠扫描预制体资产伪造可点击区域。定位问题需同时确认源码生成位置与当前游戏快照，并保留同名实例的完整地址。

- 闪退不能仅看 BepInEx 最后一行。2026-10-24 的 Windows Application / .NET Runtime 记录显示 `ClassInjector.Finalize -> GCHandle.FromIntPtr` 的未初始化句柄异常，现存日志不能确定具体注入类型，也不能归责其他插件。调试拦截层改为原生 Image + 注入 MonoBehaviour / ICanvasRaycastFilter，减少对 Unity 序列化 UI 继承链的依赖；隔离 IL2CPP 压力测试包含双端 GC、600 次拾取、100 次底图重载。当前独立播放器是 2022.3.62f3，与实际游戏 f2/Metadata V29 并不等同，不能把隔离通过说成真实多插件现场已复现并消除。
- 调试 IPC 的原子替换也可能被短暂文件锁或扫描程序拒绝访问。快照写入失败应延迟重试，保留选中节点和覆盖层，不能走绘制异常的 ResetOverlay；这会在每次拾取时反复销毁、注入 UI。用独占打开 snapshot.json 的测试验证覆盖层身份和选择仍然保留。
- 节点热重载先 Stop 视频、断开 targetTexture / RawImage.texture / Image.sprite，再隐藏并销毁自有宿主，延迟销毁纹理和 Sprite。RenderTexture.Release 是立即释放 GPU 资源，不能与延迟销毁的消费者交错使用。视频准备通过轮询与超时处理，避免未保存的托管回调跨 IL2CPP 生命周期；清理逐项隔离，防止失效对象阻止剩余资源释放。
- 在线调试仅枚举当前 active scene 的真实根对象；Resources.FindObjectsOfTypeAll 会包含后台资源、持久对象及预制体。场景变化立即清空选中状态，只绘制选中节点的 4px 框和一个带底板的名称标签。RectMask2D 的角点计算同样复用原生缓冲，不能只优化节点本身却在逐帧裁剪中重复分配 IL2CPP 数组。
- 多场景替换的存储键本来包含 scene，但没有可见的已保存范围列表，容易让作者误以为修改场景是在移动原规则。编辑器应显示该资源所有已存范围，提供新增场景入口；验证全局、大厅、选曲独立保存、切回恢复预览，以及单独删除某一范围不影响其他范围。

- 在线调试不能把一次 MissingReferenceException 锁存为永久停绘。切场景可能销毁节点、标签或加载器的网格/renderer；清理自有覆盖层后限频重试，重建全部渲染资源，保留借用字体。独立播放器应检查恢复后的最终屏幕像素，而不只检查 IsInspecting。
- 调试连接的五秒租约不能只靠 WinForms Timer 续期。阻塞界面、文件对话框或长操作会让框消失；用独立线程计时器写加锁的控制快照，后台不读控件，关闭时停止续期并断开，只更新自己持有的连接。测试无 UI 消息泵超过五秒。
- HttpClient 八秒总超时不适合约 300 MiB 的更新包。下载按流读取并报告实际字节，区分已知/未知长度，单次网络停顿限时、整次下载另设上限，哈希放后台执行；未通过校验、用户取消、未保存修改拒绝退出时都不能启动安装器。成功、失败与取消都要正确清理暂存文件。
- 合成字体放大也会挤掉底栏“外观”二字，固定 44px 列在正常字体下通过并不足够。短标签同样用共享 SingleLineLabel 和内容列测量；不要缩字或折行。自动更新的百分比测试使用准确字节比例，避免把 36.9997% 截断当成可访问性失败。

- 2.6.1 进入制谱器出现长时间无响应，现场主线程栈为 ThemeRescanScheduler.Update → ApplyScene → ApplyImageSprite → SceneNodeLocator.AddressOf → UnityEngine.Object.get_name。编辑层约 1,589 个 Image，AddressOf 对每张图、每级父节点都重新遍历全部同级节点并反复跨 IL2CPP 读 name，另有多次延迟扫描，形成近似平方级成本。现场主题没有 resources.json、实时与在线调试均关闭，仍无条件算精确地址；不能把实时开关当成此路径的开关。后续应只在确有节点地址规则时计算地址，并在一次扫描中缓存/批量构建同级索引。第二次采样进程自行恢复，属于严重主线程阻塞，不能仅凭窗口无响应称为永久死锁。
- 多插件诊断必须采集卡住时的调用栈，不能因最后一条警告来自另一插件就归责。此次 Rework Overlay 的编辑器元数据警告被局部捕获并返回；现场实际耗时栈在 ThemeLoader 的节点地址计算。没有同一现场线程栈或隔离对照时，不把日志先后当作因果证明。

- 生成的 IL2CPP API 能编译、能 Resolve，不代表能调用。2.6.0 的 Font.CreateDynamicFontFromOSFont(string,int) 在真实 interop 中只有“Method unstripping failed”异常体，令整个调试层停绘。调试标签复用已加载字体/内置字体，字体失败只影响文字；接口检查拒绝没有局部失败回退的异常桩调用。不能在清理时 Destroy 借用的游戏字体。
- 从 Library/ScriptAssemblies 复制的 UnityEngine.UI.dll 含 UNITY_EDITOR 引用，可以做编辑器验证，但不能直接打进独立播放器。播放器测试应从 com.unity.ugui 包编译运行版本，不能为了让构建通过关闭程序集校验。
- Windows Unity 播放器以 SW_HIDE 隐藏窗口启动时，正常屏幕呈现可能整帧为黑，离屏 Camera.Render 却正常。验证真正的 ScreenSpaceOverlay 时，测试播放器使用从不切换到的独立 Windows desktop，保持其自身窗口正常呈现，既不显示在用户桌面，也不触碰真实游戏。检查原界面和覆盖层像素，不能把隐藏窗口的黑帧直接判成覆盖层缺陷。
- 全解决方案并行 NuGet 还原可能报“当文件已存在时，无法创建该文件”。用独立 artifacts 输出、`-m:1 -p:RestoreDisableParallel=true` 串行还原/构建可继续；不要清空共享缓存或结束用户的 IDE 进程。

- Unity 2022.3 的 CanvasRenderer.SetMesh 不能直接沿用三维网格的 UInt32 索引；隔离渲染中会出现从原点铺开的巨大三角形。调试线框使用 UInt16，每批最多 60,000 顶点，超出拆批；必须检查实际像素，网格顶点数非零不代表画面对。
- IL2CPP 的 Il2CppStructArray 不是 IDisposable，GetWorldCorners 需要真实的 IL2CPP 数组缓冲区；不要把 C# 数组隐式复制后当作原位输出，也不要每帧为每个节点重复分配原生角点数组。
- Unity 的旧输入 API 模块没有 com.unity.modules.inputlegacy 这个内置包名。隔离工程只声明实际存在的模块；反射输入入口不要求凭名称猜一个包。
- 游戏 UIHalfPanel 的 OnPointerClick 没有检查鼠标按钮，中键调试拾取会同时关闭面板。通过加载器自有透明 Image 的条件 Raycast 接收中键及释放帧，不补丁原输入派发；验证 EventSystem.RaycastAll 的中键排序和普通左键穿透。
- 验证期间其他工具可能安装新插件、导入主题；完整目录审计应保留失败与全差异，不通过扩大排除列表或重录基线掩盖。再精确检查源工程和游戏/加载器稳定文件，说明此验证范围；不修改或回滚玩家同期文件。

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
- 2.3.0 在真实游戏报 `WidgetLive2dControl.get_bkmv()` / `get_bkmw()` 不存在：编译缓存来自旧游戏，真实 interop 中私有角色字段已改名。用公开 container 下已实例化的组件查找目标，排除本加载器创建的对象，并在目标不唯一时拒绝替换；不能硬编码混淆字段。
- 编译通过、GameAssembly 哈希检查和 Unity 编辑器样本渲染均不能证明 CLR 能链接真实 interop。发布前用 `test-interop-compatibility.ps1` 逐项解析发行插件的游戏/Unity MemberRef，指定正在使用的 interop 目录；2.3.0 的两个缺失引用可在不运行游戏的情况下复现。
- Cubism 属性的显示别名可以变化，但其 accessor MethodDef 仍是 `get_TextureIndex`、`set_Material` 等 SDK 名称。以编译后实际 MemberRef 和方法签名为准，不要看到属性别名不同就改用未验证的裸指针调用。
- 为了让源代码也能针对新旧缓存编译，Cubism 适配层按保留的 SDK accessor 方法名绑定托管委托。发布检查同时校验这些反射接口的返回值、参数及唯一性；仅检查直接 MemberRef 会漏掉反射绑定。
- artifacts/interop-host 可能保留更新前的 GameAssembly，而 Data junction 指向更新后的数据，不能视为当前游戏版本的依据。2.3.1 发布包采用经校验的实际 interop 目录，不再复制旧 host 的接口缓存。
- 顶层每帧捕获并打印同一个异常会把游戏日志刷到数百 MB，状态却停在 waiting。运行故障应停止该项更新、尝试恢复原模型、写入 error 状态；用户重新应用主题后才重试。
- 回大厅时原版模型闪现：新看板的 Start 已创建原模型，但加载器还在等 0.4 秒的下一次发现扫描。场景加载回调也可能早于 Start，因此不能只在 SceneLoaded 扫描。对 WidgetLive2dControl.Start / Show 使用定向 postfix，同步应用模型，成功后隐藏原图；扫描仅保留补漏，不在这些回调中检查扫描冷却。
- Harmony 的字符串目标不一定生成直接 MemberRef；新生命周期钩子必须检查实际 interop 中同名方法的唯一性与签名。接口校验通过仍不等于已实测切场景时的首帧画面。
- 点击区域不能从部件名、HitAreas 或动画组名自动推断。用户在默认姿态预览中框选区域，统一存储为模型默认网格范围内、左上原点的归一化矩形；游戏点击先投射到替换模型平面，再转换到同一坐标。窗口尺寸或游戏模型缩放不应改变绑定。
- M1–M5 不是枚举值 1–5 的顺序。Emiria 的 ScoreRank 为 S=1、A=2、B=3、C=4、D=5；PanelResultScore 绘制的 rank_score_<难度><评级> 最后一位 1 对应 M5、5 对应 M1。只观察显示面板，不读取/修改 Score 或补丁结算判定。
- PanelResultScore.Fill(MaChart) 只填歌曲信息，评级仍可能是 prefab 默认值。结算动画只观察另外三个 Fill 重载完成后的评级图层；还要处理面板先于看板 Start 的情况，按场景暂存结果，并避免补漏扫描重复播放同一结果。
- PowerShell 的 `-and` 与 `-or` 优先级与 C# 不同；混合筛选重载时为每个分支加括号，否则两个参数的 Fill 会被错误排除。`[single].5` 也会被解释成成员访问，浮点测试显式写 `[single]0.5`。
- 软件 Skia 绘制可变 4096 纹理时，每次创建 bitmap shader 会导致巨量重复工作。纹理设为 immutable 并缓存 SKImage/shader；可用时用独立隐藏 WGL 上下文加速，按网格边界裁切遮罩层，CPU 模式保留低分辨率回退。不要借用或操作真实游戏窗口。
- WinForms Timer 的绘制若超过 tick 间隔，持续开启会令 DoEvents 测试被绘制饿死。Tick 先停表，完成一帧后再启表，限制预览分辨率；停止、切换、释放时同步释放图像和绘图上下文。
- 音频按完整 motion 相对路径绑定，不按动作组/部位名称绑定。同一动作复用时共用音频，新动作停止上一段；游戏播放使用加载器自己的 AudioSource，不能控制游戏 SoundManager 或全局音量。
- 插件项目默认不会复制所有传递 NuGet 运行依赖；启用 CopyLocalLockFileAssemblies，并将 NVorbis.dll 加进离线 payload 和完整性检查，否则编辑器能解码 OGG，游戏插件却会缺程序集。SkiaSharp 原生依赖随管理器单文件自解压，原游戏 Cubism Core 只从所选游戏目录加载。
- 只读验证期间用户仍可能运行游戏。Emiria 的 Config 保存逻辑会写 config.json，已安装旧插件也会写 ThemeLoader/live2d-status.json（本次状态版本仍为 2.3.1）；它们不是稳定程序资源。完整差异必须保留，VerifyStable 仅精确排除这些已确认的运行数据，不排除整个 ThemeLoader 目录，也不回滚用户设置。
- 场景独立图片不能只在 UIGlobal.GetSprite 中按活动场景返回：调用者可能缓存并复用同一 Sprite。图集钩子只做全局替换，组件应用时使用对象所属场景、完整控件路径和组件类型；每个替换 Sprite 保留原图来源，缓存键包含文件、切片和原图形状。
- Unity Image.overrideSprite 的 getter 在没有显式覆盖时也返回 activeSprite。用它备份后再恢复，会把普通 sprite 意外固定成 override。备份和检查显式覆盖应使用实际 interop 的 m_OverrideSprite，同时恢复 Image.type / SpriteRenderer.drawMode；销毁旧纹理必须晚于组件还原。
- 同一 Image 会被游戏重新设置为其他图标；还原清单应更新到最新的原图来源，而不是永久记住首次扫描的图标。替换对象仍需解包回真正原图，不能把上一轮主题图片当原资源。
- Malody 的 UI 音效由 FMOD 播放，AudioEngineFMOD.CreateAudio 还按 key 缓存；不要通过全局音频工厂替换所有声音。限定 SoundManager 的 UI EffectType 和 SceneResult 的 music_finish，使用加载器独立 AudioSource，并读取已有音效音量。effectVol 在实际 interop 中已混淆，适配器按唯一的实例 float getter 绑定并在发布前校验；不可照抄源工程 Config 类名或私有字段别名。
- 源音频采用 .ogg.bytes / .mp3.bytes，试听解码需识别复合扩展名。新增 MP3 后，NLayer.dll、许可和离线完整性检查必须同时加入；Default 音频仍逐字节复制源文件，不转换编码。
- 音频按 UI 事件绑定。Cancel 与 MsgFail 使用同一源文件，但用户仍应能分别替换；目录树和规则不能仅以文件名或源路径合并这两个事件。
- 明暗 UI 测试即使使用不同主题，也不能共享 active-theme.txt / settings.json。本次并行验证曾因此把亮色测试写到暗色主题；现改为每种外观独立假游戏目录，仅通过 junction 共享只读 Default fixture。
- Unity Mono 的 netstandard2.1 不提供 File.Move 的覆盖重载。资源渲染测试在 artifacts 中仅为未调用的写入方法做兼容替换；实际 .NET 6 原子保存由桌面编辑器测试覆盖，不能把这个测试适配当成发行逻辑。
- 原生 TreeView 会按深层路径扩展水平滚动范围，导致窄窗口的文件名被硬裁切。资源树使用 TVS_NOHSCROLL 和带省略号的原生文本自绘，保留层级、提示和键盘行为；文件展示名独立于内容哈希存储路径。
- 手动输入控件路径不等于读取了该范围。路径/组件类型未提交时必须禁止资源导入与保存，并通过“读取此范围”重新载入文件、边距、模式与预览，否则会把上一范围的状态覆盖到已有控件规则。取消定位保留原草稿，同主题 F5 仅在干净状态重读；ZIP 编辑后要刷新主列表的目录身份。
- 旧界面的回归脚本可能重写已提交的截图，GPU 和状态文本会造成少量像素差异。若该界面没有修改，只恢复本轮脚本刚生成的那些 PNG，保留真正新功能的捕获和他人文件；不要把无关截图混入发布提交。
- NumericUpDown 的 UpDownBase 会强制原生高度并重排内部按钮；直接叠加自绘按钮会变薄或被编辑框遮挡。统一使用外层 NumberField：内部保留原生解析/方向键，隐藏并裁出原生箭头区，外层负责边框和横向减/加。原生箭头保留区宽度从实际子控件读取，不能用系统 DPI 的固定宽度代替。
- 布局测量不能读取 NumericUpDown.Value：getter 可能提交正在输入的负号或小数。尺寸只测量范围样本，步进禁用状态使用最近已提交值；显式提交和读取数值才触发原生校验。
- 数值组合控件须保留可 Tab 进入的外层，再把焦点交给内部编辑器；检查 ContainsFocus，而不是只看外层 Focused。验证从动画选择到模型大小的真实 Tab 路径，避免外观正常但整组数值被跳过。
- 自绘按钮的左右内边距必须计入 GetPreferredSize 和最小尺寸。不能继续套用原生按钮的测量后再用 EndEllipsis 掩盖；动作名称和导航不得折行、裁切或溢出省略，打开对话框的“…”是文案标点，可以保留。
- 百分比等宽列中只给第一列设右间距，会让第一列少一整段空间，刚好能容纳两个输入框时仍裁切。FieldGrid 使用独立间距轨道，在尺寸不足时改为单列；ButtonRow 同样按完整文案决定并排或堆叠。
- 修改共享 Windows 程序集后，多应用同进程检查前先构建整个 slnx。否则不同项目输出中的同版本 DLL 可能具有不同 MVID，LoadFrom 报 same name is already loaded；这不是发行包缺依赖。
- PowerShell 的 $Host 是只读自动变量，测试变量用 inputViewport 等专用名；插值字符串中变量后接冒号要写 ${caseName}:，否则解析为作用域变量并失败。
- 原始 PNG 可能带有旧的 iCCP 色彩配置，解码会输出 libpng warning。不要为消除测试日志修改 Default 美术；保留原图哈希，分别确认解码和界面断言是否真正失败。
- 用户在验证途中自行重启游戏时，BepInEx 也会重写 ErrorLog.log、chainloader_typeloader.dat 和 BepInEx.cfg，游戏另写 log/*.log；时间与实际进程启动相符。本次完整差异照常保留，VerifyStable 精确识别这些运行文件，不泛化排除 BepInEx 目录，并明确不保证配置内容未变。不要修改、还原配置或重录基线来让检查通过。
- Unity Animator 每帧都会重写它绑定的 RectTransform 属性；同一帧直接固定动画中的锚点会让动画失效。需要把动画采样值当作进度，在 LateUpdate 转换为位置，并在动画回到无 Motion 的状态后保留最终进度，否则离场动画结束时可能突然恢复显示。定位动画目标要使用完整场景层级，不能把同名 Prefab 实例一并改动。
- 游戏场景会销毁并重建看板 Widget；需要跨场景播放的 IL2CPP AudioSource 不要挂在游戏 Widget 上。使用加载器自己持有的 DontDestroyOnLoad 宿主，先 Stop 并清空 clip 再延迟 Destroy 宿主与 AudioClip；播放期对象失效时只停用音频，不能连带中断模型动画。
- Unity 2022.3 编辑器的脚本编译器使用 C# 9，即使插件项目使用较新的语言版本；把插件源文件拷入 Unity 测试工程时需用兼容命名空间包装，并补全项目隐式导入的 using。`Unity.exe` 是 GUI 子系统程序，PowerShell 直接调用不会可靠等待或提供 `$LASTEXITCODE`；用 `Start-Process -PassThru` 获取主进程并 `WaitForExit()`，不要用会等整个子进程树的 `Start-Process -Wait`。

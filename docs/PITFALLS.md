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
- Default 导出必须包含真实源资源；虚拟“原始外观”不能冒充完整主题。发布构建从只读源工程生成 Default ZIP。
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

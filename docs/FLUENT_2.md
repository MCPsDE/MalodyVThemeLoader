# Fluent 2 视觉基准

本次界面改造采用 Microsoft Fluent 2 的公开规范作为外部视觉基准：

- 总览：https://fluent2.microsoft.design/
- 颜色角色：https://fluent2.microsoft.design/styles/color
- Windows 材质：https://learn.microsoft.com/windows/apps/design/signature-experiences/materials

采用的规则：

- 用 `colorNeutralBackground*` 的中性层级搭建工作区，让内容和真实主题 artwork 成为视觉重点。
- 用 `colorBrandBackground` 的蓝色作为操作色，状态文字同时表达成功、错误和禁用，不依赖颜色单独传达含义。
- 控件使用紧凑密度、4px 小圆角、清晰的 focus 和 hover 状态；不使用渐变、发光、胶囊按钮或伪造的玻璃模糊。
- Windows 11 使用系统 Mica backdrop；不支持的系统回退到相同的中性背景色。
- 层级通过材质、分隔线和留白表达，不用堆叠大量带阴影的卡片。

Fluent 2 是设计参考，不引入 Fluent UI Web 或运行时网络依赖。实现仍是离线、自包含的原生 WinForms；`WindowsBackdrop` 只调用 Windows DWM，失败时安全回退。

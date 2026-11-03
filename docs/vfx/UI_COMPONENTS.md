# Studio+ UI 组件复用

当前运行包已包含扁平 UI 组件。本次核对了实际 DLL 的方法签名，新增面板通过 [StudioUiTheme.cs](../../src/StudioPlusPreview/StudioUiTheme.cs) 绑定当前 UI 实例，不需要复制其实现或另装界面框架。

源码定位基线为 `Mods/StudioPlusSnapshots/1.079-20261102/studio_icon_work/Source/UI`；实际运行检查使用 Studio+ 1.0.7 build1030，并不表示重建了完整 1.079 主插件。

| 可复用部分 | 接口 | 源码位置 |
| --- | --- | --- |
| 扁平按钮、边框与反馈 | `FlatButton` | `NativeEditorUIMatched.cs`、`ButtonFeedback.cs` |
| 标题、标签与段落 | `UiText`、`UiParagraph` | `NativeEditorUIMatched.cs` |
| 中文与数字字体度量、光栅绘制 | `UiFontRaster`、`FontPixels` | `UiFontRaster.cs`、`NativeEditorUIText.cs` |
| 数值、选区与光标绘制 | `PaintInput`、`InputHitIndex` | `NativeEditorUIInputDrawing.cs` |
| 细线滑杆与滚动条 | `SetupSkin` 所管理的 `GUISkin` | `NativeEditorUIPlugin.cs`、`NativeEditorUIRefinements.cs` |

中文使用宿主的 `Chiron Hei HK N`，数字使用 `Bai Jamjuree Medium`；字体加载和缓存由宿主管理。本插件借用皮肤并在绘制后恢复 `GUI.skin`，不新建字体，也不销毁共享字体或纹理。反射仅用于初始绑定，绘制使用缓存委托。

## 复用边界

`UiInput`、`MatchedToggle`、`ContentRow` 等也存在，但会涉及宿主谱面输入草稿、提交回调或面板布局状态。演出参数继续使用自己的输入草稿、临时预览、一次拖动一次撤销和保存事务，只复用绘制、字体度量与控件皮肤。数值字段的选区和鼠标定位使用宿主的同一套字宽计算。

面板布局由演出插件负责，采用无默认皮肤的窗口与宿主面板配色；未保存确认仍为单个非阻塞界面，沿用退出防重入保护。

这些是当前版本的内部接口，不是上游已承诺稳定的 SDK。更新宿主时先核对 UI 类型、方法签名、字体度量与实际交互，不能只根据程序集绑定版本 1.0.7.0 判定兼容。视觉与鼠标键盘验收仍由人工完成。

0.4.0 布局使用统一可用缩放，约束库、16:9 预览、检查器与时间轴的边界。模板操作仅在自定义库显示，背景独立成页，检查器按实际内容滚动。文字按宿主字宽度量省略，保留完整参数标识与数据。拖动控制从绘制分离，在入口捕获指针，重绘只负责跟随卡片与落点提示。

0.5.0 参数轨道按实际高度布局，绘制/命中/插入提示共用行边界；关键帧按数值定位，拖动期间固定纵轴范围，松开后重新适配。自定义页显示当前效果与编译状态，Shader 参数直接展开并提供打帧入口。

0.6.0 高度命中区与悬停高亮共用 ResizeRow，覆盖分隔线上下两侧；Unity 窗口内缓存拖动光标，失焦/离开视图/退出时恢复，纹理由本插件释放。效果库增加搜索，背景栏不再显示材质或特效参数。

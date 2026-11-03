自定义程序化效果

直接体验：在效果库“自定义”点击“导入已编译效果”，选择 SampleCompiled/effect.json，再拖到时间轴。

推荐：在编辑器“自定义→新建模板”生成独立工程，VS Code Ctrl+Shift+B 或“构建并更新”后自动预览。
备用手动方式：编辑 templates/custom-effect/effect.json（参数、范围、名称）与 ProceduralPulse.shader（HLSL）。
双击 Build-Sample.bat；首次选择 Unity 6000.3.25f1 的 Unity.exe。
构建结束后，导入新目录中的 effect.json。已有预览包的使用者不需要安装 Unity。

接口：postprocess 采样 _BlitTexture；background 使用普通平面顶点和 UV。
_PresentationTime 为全局秒数；_EffectTime 为片段内秒数；_EffectProgress 为片段进度。
Float、Color、Vector 参数可添加关键帧。代码效果以 Shader 执行，不加载用户 C# DLL。
新增参数需在 Shader Properties 与 effect.json 中同时声明，名称与类型一致。
当前构建入口支持单个 Shader 及同目录 .hlsl/.cginc；复杂材质可自行构建 Bundle 后导入。

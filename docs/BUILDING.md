> 范围说明：本文保留上游 Studio 1.0.10 宿主文档；其中版本和能力描述不代表 Studio+ 1.079 增强插件或本项目当前状态。本项目入口见 [异象开发说明](vfx/DEVELOPMENT.md)。

# 打开、检查和构建

## 环境

使用 `ProjectSettings/ProjectVersion.txt` 指定的 **Unity 6000.3.25f1**，安装 Windows 构建支持。目标为 Windows x64 / Mono，URP 17.3.0。保留 `Packages/manifest.json` 和锁文件，不需要从旧更新包复制资源。

仓库根目录直接包含 `Assets`、`Packages`、`ProjectSettings`。首次克隆不包含 `Library`，Unity 会重建缓存并恢复包。没有个人歌曲时仍可打开和构建编辑器，之后从界面选择自己的歌曲目录。

## 在 Unity 中运行

打开 `Tools → InFalsus Studio → Open Demo Scene`，进入 Play。实际场景为 `Assets/InFalsusStudio/Scenes/InFalsusStudio_Demo.unity`，不是模板 `SampleScene`。UI 和舞台由运行时代码生成。

## 检查菜单

均位于 `Tools → InFalsus Studio`，停止 Play、等待编译结束后执行：

| 菜单 | 检查内容 |
| --- | --- |
| `Run 1.0.10 Release Checks` | 当前与累积版本功能、格式和资源检查 |
| `Run CSharp Core Checks` | 既有核心模型与编辑/保存规则 |
| `Run Grid Color and Range Selection Checks` | 网格颜色、完整长音符段选 |
| `Run 1.0.6 Generated Mesh Checks` | 现行材质和网格回归 |
| `Run 1.0.10 Flick Shape Checks` | 当前 Flick 曲线与实际 Unity 网格 |

文件名带旧版本号不表示可以删除；当前检查和构建仍会调用这些入口。

## Windows 构建

交互方式：完成相关检查后执行 `Tools → InFalsus Studio → Build Windows Editor 1.0.10...`，选择输出父目录。

批处理方式：以下 PowerShell 命令在仓库根目录运行。输出目录必须尚不存在，日志目录可以存在。`VerifyAndBuildBatch` 会运行组合检查，再调用当前 `StudioBuild08` 构建。

```powershell
$unityExe = 'C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe'
$projectDir = (Get-Location).Path
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$env:INFALSUS_BUILD_OUTPUT = Join-Path $projectDir "Builds\InFalsusStudio_1.0.10_$runId"
$logDir = Join-Path $projectDir 'Logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$logFile = Join-Path $logDir "Build-$runId.log"
$unityArgs = @(
    '-batchmode', '-quit', '-projectPath', ('"' + $projectDir + '"'),
    '-buildTarget', 'Win64',
    '-executeMethod', 'InFalsusStudio.EditorTools.StudioRelease110Checks.VerifyAndBuildBatch',
    '-logFile', ('"' + $logFile + '"')
)
$process = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "Unity build failed; see $logFile" }
```

先关闭正在使用同一工程的 Unity Editor，避免项目锁冲突。可选环境变量：

- `INFALSUS_REFERENCE_CHART`：指向本地 Enigma 参考歌曲目录，启用相应只读检查；未设置时报告跳过，不影响常规构建。此个人参考目录不随仓库分发。
- `INFALSUS_BUILD_EVIDENCE`：已存在的目录，用于输出 Flick 几何采样 CSV。

批处理会输出 `BuildReport.txt`、Core/Feature/Grid/Flick 检查结果与公告文本。查看退出码、构建报告和日志，不能只看到 EXE 就认定全部通过。完整输出目录中的 `_Data`、Mono 与 DLL 必须一并交付。

## 发布前

同步版本常量、Unity bundleVersion、最新三版应用公告、CHANGELOG 与发布公告。保留产品和公司名，以维持已有用户偏好位置。不要将构建输出或日志提交到源码仓库；正式二进制包可以由维护者另行上传 GitHub Releases。

变更涉及的原生界面、渲染观感、Windows 选择器及音频设备需要分别验收。历史检查记录见 [VALIDATION.md](VALIDATION.md)，它们不等于当前机器的新测试结果。

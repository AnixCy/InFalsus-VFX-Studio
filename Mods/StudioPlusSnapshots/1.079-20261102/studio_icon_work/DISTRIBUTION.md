# 分发清单

2026-10-25 起，提交到 GitHub 及新制作的 Windows 包仅附带以下外置工具：

| ID | 工具 |
| --- | --- |
| MakeRain | 紫条雨 |
| GradientTiming | 渐变 Timing |
| TimingSplit | 按时序切分 SkyArea |

清单由 [distribution.json](distribution.json) 管理。新工具仍可由用户自行安装；不要把本机 ExternalTools 全目录、捕获缓存、个人默认值或脚本缓存加入提交/安装包。升级副本时保留用户已安装的其他工具。底栏快速工具中的删除/复制 Timing 属于程序功能，不受外置脚本分发清单影响。

GitHub 继续只提交源码、作者性图形和构建说明。Windows 完整包可以使用已有匹配运行环境在本机制作；本次包仅本地交付，不上传 GitHub。构建命令见 [BUILDING.md](BUILDING.md)。

# 创作端谱面编译工具

复用已验证的项目 ICP1 / SPC2 编译器，来源与哈希见 ORIGIN.json。只接受有界 JSON 数据，不执行谱面代码、不读取游戏安装或写入源工程。

compiler.py 从 stdin 接收最多四个难度，stdout 返回编译字节及统计。Studio 配套私有免安装解释器调用它；谱师不需要安装 Python。它只进入创作端 GameExport/ChartCompiler，不进入玩家 Mod 或曲包。

编译保留既有坐标共同分母、显式分组、一次音频偏移和 SPC2 timinggroup 语义。未知或不支持语法明确失败；不丢弃物件来完成导出。SPC2 成品需要匹配的社区壳，不能交给官壳。

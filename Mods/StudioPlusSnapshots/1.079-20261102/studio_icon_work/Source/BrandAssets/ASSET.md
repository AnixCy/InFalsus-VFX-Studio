# 设置页 Q 版 Mei 描边图

当前 `about-mascot.png` 是由内置 imagegen 根据用户提供的 Q 版 Mei 参考图生成的线稿，嵌入资源名为 `infstudioplus.about.png`。未把角色原始图集复制入仓库。

用户最终选择只用亮蓝。`SettingsBrandLineArt.cs` 在加载时将线稿解析为透明描边遮罩，清理低 alpha 残留和小碎点；`ProductBrand.cs` 使用统一 `#7ee4fd` 绘制。面部仅保留两笔闭眼和一笔短嘴，不增加细致五官。人物内外空白均透明，无加号、底板或框。

角色 Mei 及原作权利归原权利人；社区派生线稿不表示官方背书，详见根目录来源记录。

using InFalsus.Presentation;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class EffectLibrary
{
    void AddSceneControls()
    {
        string[] names = { "轨道透明度", "分数透明度", "曲名资料透明度", "通关状态透明度", "判定文字透明度" };
        for (int i = 0; i < names.Length; i++) Effects.Add(new EffectDefinition {
            Id = SceneOpacityPlan.EffectIds[i], Label = names[i], Category = "效果", Target = "postprocess",
            Material = SceneOpacityPlan.Material, Parameters = new[] { P(SceneOpacityPlan.Properties[i], "不透明度", 1, 0, 1) } });
    }
}

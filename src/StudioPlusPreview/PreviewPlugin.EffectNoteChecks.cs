namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    void CheckEffectNotes(Action<bool,string> check)
    {
        var effects=Model!.Library.Effects.Where(e=>e.Category!="自定义").ToArray();
        check(effects.Length>=96&&effects.All(e=>EffectNotes.For(e).Known&&EffectNotes.For(e).Summary.Length>8),"all builtin effects controls and presets have concrete effect notes");
        check(effects.SelectMany(e=>e.Parameters).Where(p=>!p.Hidden).All(p=>EffectNotes.HasParameter(p.Property)),"all visible builtin shader parameters have reference notes");
        var feedback=effects.Single(e=>e.Id=="bg-control-feedbackamount");
        check(EffectNotes.For(feedback).Text.Contains("不读取上一帧")&&EffectNotes.Parameter(feedback,"_FeedbackRotation").Contains("弧度"),"feedback reference distinguishes same-frame UV nesting and radians");
        check(EffectNotes.For(effects.Single(e=>e.Id=="bg-control-neuroamount")).Caution.Length>0,"unverified procedural branches retain an explicit uncertainty note");
        var unknown=new EffectDefinition{Id="unknown-custom",Material="custom/unreviewed",Category="自定义"};
        check(!EffectNotes.For(unknown).Known&&EffectNotes.Parameter(unknown,"_Frequency").Contains("源码"),"arbitrary custom shaders do not inherit guessed builtin semantics");
        check(ReferenceEquals(EffectNotes.Help(feedback),EffectNotes.Help(feedback)),"effect help strings are reused between GUI repaints");
        var theme=StudioUiTheme.TryBind();
        check(theme!=null&&theme.ParagraphHeight(EffectNotes.Help(feedback),250,12)>0,"effect help uses the host text wrapping metrics");
    }
}

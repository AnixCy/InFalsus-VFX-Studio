using UnityEngine;

namespace InFalsus.Vfx.StudioPlus;
public sealed partial class PreviewPlugin
{
    // Optional authored flow-background fixture; default check runs are unchanged.
    void CheckBackgroundProject(Action<bool,string> check,Dictionary<string,string> hashes)
    {
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-vfx-background-project");if(at<0)return;
        string file=args[at+1];byte[] before=File.ReadAllBytes(file);var data=Decode(File.ReadAllText(file));
        var loaded=Store().Open(file);Model!.Replace(loaded.Session);
        Surface!.ApplyImage(Surface.PrepareImage(data.BackgroundPath));
        check(Model.Document.Runtime.Nodes.Length==1,"background project opens through the actual editor store and effect library");
        var snapshot=Bridge.Snapshot();
        string Render(double t)=>HashTarget(Surface.Render(Model,snapshot with{AudioSeconds=t,ChartSeconds=t},256,144));
        string still=Render(0),animated=Render(151.8),reverse=Render(159.1);
        check(still!=animated&&animated!=reverse,"background project renders distinct animated GPU states");
        Render(166);check(Render(151.8)==animated,"background project seek order does not change its output");
        foreach(var name in new[]{"_EphemeralAmount","_FeedbackAmount","_FeedbackRotation"}) {
            string id="background/"+name;if(Model.Catalog.Parameters.Any(p=>p.Id==id))Model.UpdateParameter(id,new InFalsus.Presentation.ParameterValue(System.Numerics.Vector4.Zero));
        }
        check(Render(151.8)!=animated,"UV effects change output independently of the brightness track");Model.Cancel();
        check(Render(151.8)==animated,"cancel restores the complete background project output");
        check(before.SequenceEqual(File.ReadAllBytes(file)),"background project validation does not rewrite the input JSON");
        hashes["backgroundProjectAnimated"]=animated;hashes["backgroundProjectReverse"]=reverse;
        Surface.CheckBackgroundRenderContract(Model,snapshot with{AudioSeconds=0,ChartSeconds=0},check);
        Surface.ApplyImage(null);
    }
}

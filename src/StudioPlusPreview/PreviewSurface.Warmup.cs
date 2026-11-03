using InFalsus.Presentation;

namespace InFalsus.Vfx.StudioPlus;
internal sealed partial class PreviewSurface
{
    PackageDocument? primedDocument;
    // Compile/load representative states before starting the audio clock, at most once per edit.
    internal int PrimePlayback(PreviewModel model,PlaybackSnapshot snapshot,int requestedWidth,int requestedHeight)
    {
        var document=model.RenderDocument;if(ReferenceEquals(primedDocument,document))return 0;
        double current=(document.TimeDomain==TimeDomain.Audio?snapshot.AudioSeconds:snapshot.ChartSeconds)+document.OffsetSeconds;
        var times=new List<double>{current};
        void Add(double time){if(times.Count<7&&!times.Any(t=>Math.Abs(t-time)<.000001))times.Add(time);}
        foreach(var node in document.Nodes.Where(n=>n.Enabled))Add(node.StartSeconds+.0001);
        foreach(var channel in document.Channels) {
            var key=channel.Keys.FirstOrDefault(k=>k.Value!=null&&!k.Value.SequenceEqual(channel.Default));
            if(key!=null)Add(key.Seconds??model.Package.Tempo.ToSeconds(key.Beat!.Value));
        }
        int draws=0;
        try {
            foreach(double time in times) {
                double delta=time-current;
                Render(model,snapshot with{AudioSeconds=snapshot.AudioSeconds+delta,ChartSeconds=snapshot.ChartSeconds+delta},requestedWidth,requestedHeight);
                draws++;
            }
        } finally {
            // No preview time, parameter override, selection or authoring transaction may leak out.
            Render(model,snapshot,requestedWidth,requestedHeight);draws++;clock.Clear();
        }
        primedDocument=document;return draws;
    }
}

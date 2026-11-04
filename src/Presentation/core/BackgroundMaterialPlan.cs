using System.Numerics;

namespace InFalsus.Presentation;

/// <summary>Resource/parameter metadata, never shader code or executable expressions.</summary>
public sealed class BackgroundBindingDocument
{
    public string SourceId {get;set;}="";
    public string StrengthProperty {get;set;}="";
    public string TextureProperty {get;set;}="_MainTex";
    public bool Native {get;set;}
    public BackgroundPropertyDocument[] Properties {get;set;}=Array.Empty<BackgroundPropertyDocument>();
    public string[] Keywords {get;set;}=Array.Empty<string>();
    public string[][] ExclusiveKeywordGroups {get;set;}=Array.Empty<string[]>();
    public BackgroundBindingDocument Copy()=>new(){SourceId=SourceId,StrengthProperty=StrengthProperty,TextureProperty=TextureProperty,Native=Native,
        Properties=Properties.Select(p=>new BackgroundPropertyDocument{Property=p.Property,Kind=p.Kind,Value=p.Value.ToArray()}).ToArray(),
        Keywords=Keywords.ToArray(),ExclusiveKeywordGroups=ExclusiveKeywordGroups.Select(g=>g.ToArray()).ToArray()};
    public static bool Same(BackgroundBindingDocument? a,BackgroundBindingDocument? b)
    {
        if(ReferenceEquals(a,b))return true;if(a==null||b==null)return false;
        return a.SourceId==b.SourceId&&a.StrengthProperty==b.StrengthProperty&&a.TextureProperty==b.TextureProperty&&a.Native==b.Native
            &&a.Keywords.SequenceEqual(b.Keywords)&&a.ExclusiveKeywordGroups.Select(g=>string.Join("\0",g)).SequenceEqual(b.ExclusiveKeywordGroups.Select(g=>string.Join("\0",g)))
            &&a.Properties.Length==b.Properties.Length&&a.Properties.Where((p,i)=>p.Property!=b.Properties[i].Property||p.Kind!=b.Properties[i].Kind||!p.Value.SequenceEqual(b.Properties[i].Value)).Any()==false;
    }
}
public sealed class BackgroundPropertyDocument
{
    public string Property {get;set;}="";
    public ParameterKind Kind {get;set;}
    public double[] Value {get;set;}=new[]{0d};
}

/// <summary>One selected background material. Sampling allocates no objects and uses no history texture.</summary>
public sealed class BackgroundMaterialPlan
{
    public const string Capability="background.material-plan.v1";
    public sealed class State
    {
        public MaterialNodeDocument Node{get;}
        public BackgroundBindingDocument Definition=>Node.BackgroundBinding!;
        public ParameterValue[] Values{get;}
        public bool[] Keywords{get;}
        internal readonly ParameterValue[] Defaults;
        internal readonly Dictionary<string,int> Indices;
        internal readonly int[][] KeywordProperties,ExclusiveGroups;
        internal readonly int[] KeywordControls;
        internal State(MaterialNodeDocument node)
        {
            Node=node;var d=Definition;
            Defaults=d.Properties.Select(p=>new ParameterValue(p.Kind==ParameterKind.Float?new Vector4((float)p.Value[0],0,0,0):new Vector4((float)p.Value[0],(float)p.Value[1],(float)p.Value[2],(float)p.Value[3]))).ToArray();
            Values=Defaults.ToArray();Keywords=new bool[d.Keywords.Length];Indices=d.Properties.Select((p,i)=>(p.Property,i)).ToDictionary(p=>p.Property,p=>p.i,StringComparer.Ordinal);
            KeywordProperties=d.Keywords.Select(k=>d.Properties.Select((p,i)=>(p,i)).Where(p=>!p.p.Property.StartsWith("_KW_",StringComparison.Ordinal)&&Canonical(p.p.Property)==Canonical(k.StartsWith("USE_",StringComparison.Ordinal)?k.Substring(4):k)).Select(p=>p.i).ToArray()).ToArray();
            KeywordControls=d.Keywords.Select(k=>Indices.TryGetValue("_KW_"+k,out int i)?i:-1).ToArray();
            ExclusiveGroups=d.ExclusiveKeywordGroups.Select(g=>g.Select(k=>Array.IndexOf(d.Keywords,k)).ToArray()).ToArray();
        }
    }
    readonly record struct Binding(int Channel,int Property);
    sealed class Operator
    {
        internal MaterialNodeDocument Node=null!;internal State State=null!;internal Binding[] Bindings=Array.Empty<Binding>();
        internal int Opacity=-1,Strength=-1;internal string StrengthName="";
        internal int[] Keywords=Array.Empty<int>();internal int MainKeyword=-1;
    }
    readonly State[] states;
    readonly Operator[] operators;
    readonly Binding[][] inputBindings;
    readonly int tint=-1,brightness=-1;
    public bool Enabled=>states.Length>0;
    public State? Base=>Enabled?states[0]:null;
    public IReadOnlyList<State> States=>states;
    public int Conflicts{get;private set;}
    public State? Current{get;private set;}
    public static bool IsControl(MaterialNodeDocument n)=>n.Target=="postprocess"&&n.BackgroundBinding!=null;
    static bool Active(MaterialNodeDocument n,double t)=>n.Enabled&&t>=n.StartSeconds&&(!n.EndSeconds.HasValue||t<n.EndSeconds.Value);
    public BackgroundMaterialPlan(PackageDocument doc)
    {
        var nodes=doc.Nodes.Where(n=>n.BackgroundBinding!=null).ToArray();
        if(nodes.Length==0){states=Array.Empty<State>();operators=Array.Empty<Operator>();inputBindings=Array.Empty<Binding[]>();return;}
        if(!doc.Requires.Contains(Capability)||nodes.Length>65)throw new InvalidDataException("Background material plans require their capability and at most 64 controls.");
        var input=nodes.SingleOrDefault(n=>n.Target=="background")??throw new InvalidDataException("Background plan needs one input background.");
        foreach(var n in nodes) {
            var d=n.BackgroundBinding!;
            if(n.Target is not ("background" or "postprocess")||string.IsNullOrWhiteSpace(d.SourceId)||d.SourceId.Length>160
                ||d.TextureProperty==null||d.StrengthProperty==null||d.Properties==null||d.Keywords==null||d.ExclusiveKeywordGroups==null
                ||d.Properties.Length>256||d.Keywords.Length>64||d.ExclusiveKeywordGroups.Length>16)
                throw new InvalidDataException("Invalid background material metadata.");
            foreach(var p in d.Properties)if(p==null||string.IsNullOrWhiteSpace(p.Property)||p.Property.Length>120||p.Kind==ParameterKind.Choice
                ||!Enum.IsDefined(typeof(ParameterKind),p.Kind)||p.Value==null||p.Value.Length!=(p.Kind==ParameterKind.Float?1:4)||p.Value.Any(v=>!double.IsFinite(v)||Math.Abs(v)>float.MaxValue))throw new InvalidDataException("Invalid background property.");
            if(d.Properties.Select(p=>p.Property).Distinct().Count()!=d.Properties.Length||d.Keywords.Any(string.IsNullOrWhiteSpace)||d.Keywords.Distinct().Count()!=d.Keywords.Length
                ||d.ExclusiveKeywordGroups.Any(g=>g==null||g.Length<2||g.Any(k=>!d.Keywords.Contains(k))||g.Distinct().Count()!=g.Length))throw new InvalidDataException("Invalid background keyword/schema metadata.");
        }
        var groups=nodes.Where(n=>n.Target!="background").GroupBy(n=>n.BackgroundBinding!.SourceId,StringComparer.Ordinal).ToArray();
        if(groups.Length>16)throw new InvalidDataException("A background plan supports at most 16 source materials.");
        states=new[]{new State(input)}.Concat(groups.Select(g=>new State(g.First()))).ToArray();
        var ops=new List<Operator>();
        int Channel(string node,string property)=>Array.FindIndex(doc.Channels,c=>c.Node==node&&c.Property==property);
        inputBindings=states.Select(s=>s.Definition.Properties.Select((p,i)=>(Channel:Channel(input.Id,p.Property),Property:i)).Where(p=>p.Channel>=0).Select(p=>new Binding(p.Channel,p.Property)).ToArray()).ToArray();
        foreach(var s in states)foreach(var b in inputBindings[Array.IndexOf(states,s)])if(doc.Channels[b.Channel].Kind!=s.Definition.Properties[b.Property].Kind)throw new InvalidDataException("Background input parameter type mismatch.");
        tint=Channel(input.Id,"_Tint");brightness=Channel(input.Id,"_Brightness");
        foreach(var n in nodes.Where(n=>n.Target!="background")) {
            var s=states.Skip(1).Single(s=>s.Definition.SourceId==n.BackgroundBinding!.SourceId);var d=n.BackgroundBinding!;
            if(n.Bundle!=s.Node.Bundle||n.Material!=s.Node.Material||d.Native!=s.Definition.Native||d.TextureProperty!=s.Definition.TextureProperty
                ||!d.Keywords.SequenceEqual(s.Definition.Keywords)||d.Properties.Length!=s.Definition.Properties.Length
                ||d.Properties.Where((p,i)=>p.Property!=s.Definition.Properties[i].Property||p.Kind!=s.Definition.Properties[i].Kind||!p.Value.SequenceEqual(s.Definition.Properties[i].Value)).Any()
                ||!d.ExclusiveKeywordGroups.Select(g=>string.Join("\0",g)).SequenceEqual(s.Definition.ExclusiveKeywordGroups.Select(g=>string.Join("\0",g))))throw new InvalidDataException("One background source id must describe the same resource and schema.");
            var bindings=new List<Binding>();
            foreach(var item in doc.Channels.Select((c,i)=>(c,i)).Where(p=>p.c.Node==n.Id&&p.c.Property!="_VfxOpacity")) {
                if(!s.Indices.TryGetValue(item.c.Property,out int p)||item.c.Kind!=s.Definition.Properties[p].Kind)throw new InvalidDataException("Background control channel is absent from its source schema.");
                bindings.Add(new(item.i,p));
            }
            string canonical=Canonical(d.StrengthProperty);
            ops.Add(new(){Node=n,State=s,Bindings=bindings.ToArray(),Opacity=Channel(n.Id,"_VfxOpacity"),Strength=d.StrengthProperty.Length==0?-1:s.Indices.TryGetValue(d.StrengthProperty,out int strength)?strength:throw new InvalidDataException("Unknown background strength property."),StrengthName=d.StrengthProperty,
                Keywords=d.Keywords.Select((k,i)=>(Key:Canonical(k.StartsWith("USE_",StringComparison.Ordinal)?k.Substring(4):k),i)).Where(p=>p.Key==canonical||p.Key.Length>=4&&canonical.StartsWith(p.Key,StringComparison.Ordinal)).Select(p=>p.i).ToArray(),
                MainKeyword=Array.FindIndex(d.Keywords,k=>Canonical(k.StartsWith("USE_",StringComparison.Ordinal)?k.Substring(4):k)==canonical)});
        }
        operators=ops.ToArray();Current=states[0];
    }
    public State? Sample(double seconds,ReadOnlySpan<ParameterValue> values)
    {
        if(!Enabled)return null;
        if(!double.IsFinite(seconds))throw new ArgumentException("Non-finite background time.");
        Conflicts=0;State selected=states[0];
        foreach(var op in operators)if(Active(op.Node,seconds)&&(op.Opacity<0||values[op.Opacity].Number.X>0))selected=op.State;
        if(selected!=states[0])for(int i=1;i<states.Length;i++)if(states[i]!=selected){foreach(var op in operators)if(op.State==states[i]&&Active(op.Node,seconds)&&(op.Opacity<0||values[op.Opacity].Number.X>0)){Conflicts++;break;}}
        Array.Copy(selected.Defaults,selected.Values,selected.Values.Length);
        foreach(var b in inputBindings[Array.IndexOf(states,selected)])selected.Values[b.Property]=values[b.Channel];
        if(selected!=states[0]&&!Base!.Definition.Native&&selected.Indices.TryGetValue("_BaseColor",out int color)) {
            var v=tint<0?Vector4.One:values[tint].Number;float k=brightness<0?1:values[brightness].Number.X;
            selected.Values[color]=new ParameterValue(new Vector4(v.X*k,v.Y*k,v.Z*k,v.W));
        }
        foreach(var op in operators)if(op.State==selected&&Active(op.Node,seconds)) {
            float opacity=op.Opacity<0?1:Math.Clamp(values[op.Opacity].Number.X,0,1);if(opacity<=0)continue;
            foreach(var b in op.Bindings){var value=values[b.Channel];if(b.Property==op.Strength||op.Strength<0)value=new ParameterValue(Vector4.Lerp(selected.Defaults[b.Property].Number,value.Number,opacity));selected.Values[b.Property]=value;}
        }
        for(int k=0;k<selected.Keywords.Length;k++) {
            bool enabled=false;
            foreach(int p in selected.KeywordProperties[k])enabled|=Math.Abs(selected.Values[p].Number.X)>.000001;
            foreach(var op in operators)if(op.State==selected&&Active(op.Node,seconds)&&op.Strength>=0&&(op.Opacity<0||values[op.Opacity].Number.X>0)) {
                if(Array.IndexOf(op.Keywords,k)>=0)enabled|=selected.Values[op.Strength].Number.LengthSquared()>.00000001f;
            }
            int control=selected.KeywordControls[k];if(control>=0){float v=selected.Values[control].Number.X;if(v>.5f)enabled=true;else if(v<-.5f)enabled=false;}
            selected.Keywords[k]=enabled;
        }
        foreach(var group in selected.ExclusiveGroups) {
            int winner=-1,count=0;foreach(int keyword in group)if(selected.Keywords[keyword]){winner=keyword;count++;}
            foreach(var op in operators)if(op.State==selected&&Active(op.Node,seconds)&&op.Strength>=0&&(op.Opacity<0||values[op.Opacity].Number.X>0)&&Math.Abs(selected.Values[op.Strength].Number.X)>.000001) {
                if(Array.IndexOf(group,op.MainKeyword)>=0)winner=op.MainKeyword;
            }
            if(count>1)Conflicts++;
            if(winner>=0)foreach(int keyword in group)selected.Keywords[keyword]&=keyword==winner;
        }
        Current=selected;return selected;
    }
    static string Canonical(string name)=>name.Replace("_","").Replace("Amount","").ToUpperInvariant();
}

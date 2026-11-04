extern alias UnityCore;
using InFalsus.Presentation;
using UnityCore::UnityEngine;
using UnityCore::UnityEngine.Rendering;

namespace InFalsus.Presentation.Game;
public sealed partial class UnityRenderBackend
{
    private sealed partial class MaterialEffect
    {
        readonly BackgroundMaterialPlan _backgroundPlan;
        readonly BackgroundResource[] _backgroundResources;
        BackgroundMaterialPlan.State? _lastBackground;
        bool _backgroundSelectionChanged;
        sealed class BackgroundResource
        {
            internal BackgroundMaterialPlan.State State=null!;internal MaterialResource Resource=null!;
            internal int[] Ids=Array.Empty<int>();internal bool[] Integers=Array.Empty<bool>(),Skips=Array.Empty<bool>();
            internal ParameterValue[] Last=Array.Empty<ParameterValue>();internal bool[] LastKeywords=Array.Empty<bool>();internal bool Sampled;
            internal IntPtr Texture;
            internal bool VectorAspect,HasAspect;
        }
        static BackgroundResource[] PrepareBackgroundResources(BackgroundMaterialPlan plan,MaterialResource[] resources)
        {
            return plan.States.Select(s=>{
                var r=resources.Single(r=>r.Id==s.Node.Id);var m=r.Material;
                var ids=s.Definition.Properties.Select(p=>Shader.PropertyToID(p.Property)).ToArray();var integers=new bool[ids.Length];var skips=new bool[ids.Length];
                for(int i=0;i<ids.Length;i++) {
                    var p=s.Definition.Properties[i];if(p.Property.StartsWith("_KW_",StringComparison.Ordinal)){skips[i]=true;continue;}
                    if(!m.HasProperty(ids[i]))throw new InvalidDataException("Background schema references an absent Shader property: "+p.Property);
                    int at=m.shader.FindPropertyIndex(p.Property);var type=m.shader.GetPropertyType(at);
                    bool valid=p.Kind switch{ParameterKind.Float=>type is ShaderPropertyType.Float or ShaderPropertyType.Range or ShaderPropertyType.Int,
                        ParameterKind.Vector=>type==ShaderPropertyType.Vector,ParameterKind.Color=>type==ShaderPropertyType.Color,_=>false};
                    if(!valid)throw new InvalidDataException("Background Shader property type mismatch: "+p.Property);integers[i]=type==ShaderPropertyType.Int;
                }
                int aspect=m.shader.FindPropertyIndex("_Aspect");
                return new BackgroundResource{State=s,Resource=r,Ids=ids,Integers=integers,Skips=skips,Last=new ParameterValue[ids.Length],LastKeywords=new bool[s.Keywords.Length],
                    HasAspect=s.Definition.Native||m.HasProperty("_Aspect"),VectorAspect=s.Definition.Native||aspect>=0&&m.shader.GetPropertyType(aspect)==ShaderPropertyType.Vector};
            }).ToArray();
        }
        void ApplyBackground(in FrameContext frame,ReadOnlySpan<ParameterValue> values)
        {
            var state=_backgroundPlan.Sample(frame.Seconds,values);if(state==null)return;
            _backgroundSelectionChanged=state!=_lastBackground;_lastBackground=state;
            BackgroundResource selected=_backgroundResources[0];foreach(var candidate in _backgroundResources)if(candidate.State==state){selected=candidate;break;}var material=selected.Resource.Material;
            for(int i=0;i<state.Values.Length;i++) {
                int id=selected.Ids[i];var value=state.Values[i];if(selected.Skips[i]||selected.Sampled&&value==selected.Last[i])continue;
                var v=value.Number;switch(state.Definition.Properties[i].Kind) {
                    case ParameterKind.Color:material.SetColor(id,new Color(v.X,v.Y,v.Z,v.W));break;
                    case ParameterKind.Vector:material.SetVector(id,new Vector4(v.X,v.Y,v.Z,v.W));break;
                    default:if(selected.Integers[i])material.SetInteger(id,(int)Math.Round(v.X));else material.SetFloat(id,v.X);break;
                }
                selected.Last[i]=value;
            }
            for(int i=0;i<state.Keywords.Length;i++)if(!selected.Sampled||selected.LastKeywords[i]!=state.Keywords[i]) {
                if(state.Keywords[i])material.EnableKeyword(state.Definition.Keywords[i]);else material.DisableKeyword(state.Definition.Keywords[i]);selected.LastKeywords[i]=state.Keywords[i];
            }
            selected.Sampled=true;
            if(state!=_backgroundPlan.Base&&state.Definition.TextureProperty.Length>0) {
                var input=_backgroundPlan.Base!.Definition.TextureProperty.Length>0?_backgroundResources[0].Resource.Material.GetTexture(_backgroundPlan.Base.Definition.TextureProperty):Texture2D.whiteTexture;
                if(input!=null&&input.Pointer!=selected.Texture){material.SetTexture(state.Definition.TextureProperty,input);selected.Texture=input.Pointer;}
            }
            if(_active&&material.Pointer!=_assigned&&OwnsBinding()){_owner._bridge.SetMaterial(_target,material.Pointer);_assigned=material.Pointer;}
        }
        static void ApplyMaterialClocks(BackgroundResource resource,in FrameContext frame,float aspect)
        {
            var m=resource.Resource.Material;
            m.SetFloat("_CurrentTime",(float)frame.Seconds);m.SetInteger("_FrameCount",checked((int)frame.VisualTick));
            m.SetVector("_R2",new Vector4((float)(frame.VisualTick*.754877666%1),(float)(frame.VisualTick*.569840296%1),0,0));
            if(resource.VectorAspect)m.SetVector("_Aspect",new Vector4(aspect,1/aspect,0,0));else if(resource.HasAspect)m.SetFloat("_Aspect",aspect);
            // Operators gate parameters, not the always-active background's local clock.
            m.SetFloat("_EffectTime",(float)frame.Seconds);m.SetFloat("_EffectDuration",0);m.SetFloat("_EffectProgress",0);
        }
    }
}

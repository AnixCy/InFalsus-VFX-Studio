using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace InFalsusStudio.NativeEditorUI {
 public sealed partial class NativeEditorUIPlugin {
  bool toolsLegacy;
  readonly Dictionary<string,Texture2D> icons=new Dictionary<string,Texture2D>();
  string hoverText="";Rect hoverRect;
  void LayoutDocked(){
   viewport=new Rect(0,0,Screen.width,Screen.height);scale=Mathf.Max(.25f,Mathf.Min(Screen.width/2560f,Screen.height/1351f));
   float rail=104*scale,top=205*scale,bottom=92*scale;
   footerRect=new Rect(0,Screen.height-bottom,Screen.width,bottom);LayoutWave();railCell=WaveVisible?80:96;float lower=WaveVisible?waveRect.y:Screen.height-bottom;
   leftRect=new Rect(0,top,rail,Mathf.Max(0,lower-top));
   rightRect=new Rect(Screen.width-rail,top,rail,leftRect.height);
   float maxCell=Mathf.Max(1,(leftRect.height/scale-60f)/9f);railCell=Mathf.Min(railCell,maxCell);
   paramRect=Rect.zero;
   bool left=panel=="编辑"||panel=="网格"||panel=="放置"||panel=="波形"||panel=="检查"||panel=="渲染"||panel=="选取过滤"||panel=="段落过滤";
   float panelWidth=460*Mathf.Max(.8f,scale);panelRect=new Rect(left?rail:Screen.width-rail-panelWidth,top,panelWidth,leftRect.height);
  }
  void DockFill(Rect r,bool vertical){
   Fill(r,Css("#091c25fc"));float t=Mathf.Max(1,scale);
   if(vertical)Fill(new Rect(r.x<Screen.width*.5f?r.xMax-t:r.x,r.y,t,r.height),new Color(.22f,.37f,.42f));
   else Fill(new Rect(r.x,r.y,r.width,t),new Color(.22f,.37f,.42f));
  }
  bool IconButton(Rect r,string key,string tip,bool on){
   tip=T(tip);bool hover=r.Contains(Event.current.mousePosition);
   if(GUI.enabled&&(hover||(on&&r.y<footerRect.y)))Fill(r,new Color(.13f,.27f,.32f,on?.6f:.9f));
   if(on&&r.y<footerRect.y){float x=r.x<Screen.width*.5f?r.x:r.xMax-3*scale;Fill(new Rect(x,r.y+18*scale,3*scale,Mathf.Max(2*scale,r.height-36*scale)),new Color(.66f,.94f,.95f));}
   if(on&&key=="magnet"){float dot=Mathf.Max(2,Mathf.Round(4*scale));Fill(new Rect(Mathf.Round(r.center.x-dot*.5f),Mathf.Round(FooterAnnotationCenter()+CompactFooterOffset()-dot*.5f),dot,dot),Css("#a4e6e8"));}
   bool clicked=FeedbackButton(r,key,new GUIContent("",tip));
   var old=GUI.color;GUI.color=old*(!GUI.enabled?Css("#3c535f"):on?new Color(.73f,.96f,.97f):hover?new Color(.87f,.98f,1):Css("#8eadbc"));
   float size=(r.y>=footerRect.y?(key=="play"?30:key=="tools"?32:key=="magnet"?28:r.width<=52*scale?22:25):38)*scale;
   if(r.y>=footerRect.y){float center=key=="magnet"||key=="range"||key=="copy"?33.5f:46.5f;PaintFooterIcon(new Rect(r.center.x-size*.5f,footerRect.y+center*scale-size*.5f,size,size),key);}else GUI.DrawTexture(new Rect(r.center.x-size*.5f,r.center.y-size*.5f,size,size),Icon(key));GUI.color=old;
   if(hover){hoverText=tip;hoverRect=r;}
   return clicked;
  }
  void RailButton(Rect rail,float y,string key,string tip,string target){
   var r=new Rect(rail.x,y,rail.width,railCell*scale);
   if(IconButton(r,key,tip,panel==target))Open(target);
  }
  void DrawDockedRails(){
   hoverText="";DockFill(leftRect,true);DockFill(rightRect,true);
   var radial=Get(boot,"radial");bool point=(bool)Prop(radial,"InPointMode");float y=leftRect.y;
   if(IconButton(new Rect(leftRect.x,y,leftRect.width,railCell*scale),"point","点立得",point))Queue(delegate{radial.GetType().GetProperty("InPointMode",F).SetValue(radial,!point,null);});
   y+=railCell*scale;
   if(IconButton(new Rect(leftRect.x,y,leftRect.width,railCell*scale),"range","段落选择",false))Command("BeginRangeSelection");
   y+=(railCell+20)*scale;RailButton(leftRect,y,"copy","复制与批量编辑","编辑");y+=railCell*scale;
   bool autoplay=Val<bool>(boot,"autoCursor");Rect autoRect=new Rect(leftRect.x,y,leftRect.width,railCell*scale);if(IconButton(autoRect,"autoplay",autoplay?"自动播放：开":"自动播放：关",autoplay))Queue(delegate{Set(boot,"autoCursor",!autoplay);});
   y+=(railCell+20)*scale;RailButton(leftRect,y,"grid","网格与吸附","网格");y+=railCell*scale;RailButton(leftRect,y,"air","放置默认值","放置");
   y+=(railCell+20)*scale;RailButton(leftRect,y,"wave","音频波形","波形");y+=railCell*scale;RailButton(leftRect,y,"check","谱面检查","检查");y+=railCell*scale;RailButton(leftRect,y,"render","视频渲染","渲染");
   string[] names={"设置","时序","时间组","工具","工程"};string[] keys={"settings","timing","groups","authoring","folder"};
   for(int i=0;i<names.Length;i++)RailButton(rightRect,rightRect.y+i*railCell*scale,keys[i],names[i],names[i]);
   DrawRailDifficulties();
   RailButton(rightRect,rightRect.yMax-2*railCell*scale,"help","操作帮助","帮助");
   if(IconButton(new Rect(rightRect.x,rightRect.yMax-railCell*scale,rightRect.width,railCell*scale),"exit","退出制谱器",false))Command("RequestExit07");
   foreach(float line in new[]{leftRect.y+(2*railCell+10)*scale,leftRect.y+(4*railCell+30)*scale,leftRect.y+(6*railCell+50)*scale})Fill(new Rect(27*scale,line,50*scale,scale),new Color(.19f,.32f,.38f));
  }
  void DrawRailDifficulties(){
   float top=rightRect.y+5*railCell*scale,bottom=rightRect.yMax-2*railCell*scale,available=bottom-top;
   if(available<4*32*scale)return;
   float height=Mathf.Min(64*scale,(available-20*scale)/4),start=top+(available-height*4)*.5f;
   int active=Val<int>(boot,"guiSongIndex");bool enabled=GUI.enabled;GUI.enabled=enabled&&Get(boot,"songProject")!=null;
   Color[] colors={new Color(8/255f,198/255f,1),new Color(21/255f,192/255f,179/255f),new Color(185/255f,103/255f,215/255f),new Color(225/255f,49/255f,79/255f)};
   try{for(int row=0;row<4;row++){
    int index=3-row;Rect r=new Rect(rightRect.x,start+row*height,rightRect.width,height);bool selected=index==active,hover=r.Contains(Event.current.mousePosition);
    if(selected||hover)Fill(r,selected?new Color(colors[index].r*.28f,colors[index].g*.28f,colors[index].b*.28f,.8f):Css("#173743"));
    Fill(new Rect(r.xMax-3*scale,r.y+16*scale,3*scale,height-32*scale),colors[index]);
    bool clicked=FeedbackButton(r,new[]{"MIN","EVO","ULT","FBD"}[index],new GUIContent("",T("切换难度 · "+new[]{"MIN","EVO","ULT","FBD"}[index])));
    UiText(new Rect(r.x+9*scale,r.y,r.width-18*scale,r.height),new[]{"MIN","EVO","ULT","FBD"}[index],17,GUI.enabled?(selected?Css("#efffff"):Css("#b4ced6")):Css("#637d88"),NumFace,1);
    if(clicked)Queue(delegate{Call(boot,"CancelGesture");Call(boot,"ActivateSongDifficulty",index,true);panelScroll=Vector2.zero;});
   }}finally{GUI.enabled=enabled;}
  }
  void FooterField(float x,float width,string icon,string title,string key,string value,Action<string> commit,string unit,float inputWidth){
   float y=footerRect.y;var old=GUI.color;GUI.color=old*new Color(.45f,.64f,.73f);GUI.DrawTexture(new Rect((x+15)*scale,y+34*scale,27*scale,27*scale),Icon(icon));GUI.color=old;
   GUI.Label(new Rect((x+56)*scale,y+14*scale,(width-66)*scale,20*scale),title,caption);
   Rect r=new Rect((x+56)*scale,y+34*scale,inputWidth*scale,41*scale);
   var prev=GUI.skin.textField;var field=new GUIStyle(number);field.normal.background=null;field.focused.background=null;field.hover.background=null;field.active.background=null;field.border=new RectOffset();field.padding=new RectOffset(0,0,0,0);field.fontSize=Mathf.Max(12,(int)(26*scale));field.alignment=TextAnchor.MiddleLeft;GUI.skin.textField=field;
   try{Edit(r,key,value,commit,false);}finally{GUI.skin.textField=prev;}
   if(unit.Length>0)GUI.Label(new Rect(r.xMax+7*scale,y+46*scale,30*scale,20*scale),unit,caption);
   if(GUI.GetNameOfFocusedControl()==key)Fill(new Rect(r.x,r.yMax-2*scale,r.width,scale),new Color(.61f,.9f,.94f));
  }
  void DrawDockedFooterOld(){
   DockFill(footerRect,false);float y=footerRect.y;
   if(IconButton(new Rect(0,y,104*scale,92*scale),"play","正式播放 · 隐藏编辑界面",false))RequestPlay(false);
   HistoryButton(new Rect(104*scale,y,60*scale,92*scale),false);
   HistoryButton(new Rect(164*scale,y,60*scale,92*scale),true);
   if(IconButton(new Rect(224*scale,y,60*scale,92*scale),"save","保存 · Ctrl + S",false))Command("SaveWorkingCopy",false);
   if(Transport==null||Profile==null)return;
   double bpm=Convert.ToDouble(Get(boot,"viewReferenceBpm"));if(bpm<=0)bpm=Convert.ToDouble(Prop(boot,"InitialBpm"));
   FooterField(310,220,"bpm","基准 BPM · 视图","field_chrome_reference",Num(bpm),OriginalCommit("<DrawExternalChrome>b__55_1"),"",120);
   FooterField(530,190,"offset","音频偏移","field_chrome_delay",Num(Transport.AudioOffsetMs),OriginalCommit("<DrawExternalChrome>b__55_2"),"ms",76);
   FooterField(720,250,"clock","当前时间","field_chrome_time",Num((double)Prop(boot,"CurrentMs")),OriginalCommit("<DrawExternalChrome>b__55_3"),"ms",130);
   FooterField(970,180,"speed","视速","field_chrome_scroll",Num(Profile.scrollMultiplier),OriginalCommit("<DrawExternalChrome>b__55_0"),"",80);
   FooterField(1180,210,"rate","播放速度","field_nativeui_rate",Num(Transport.Rate*100),delegate(string value){double n=double.Parse(value,CultureInfo.InvariantCulture);if(double.IsNaN(n)||double.IsInfinity(n)||n<10||n>400)throw new ArgumentOutOfRangeException("播放速度","10..400%");Transport.SetRate(n/100);},"%",70);
   foreach(float x in new[]{298f,1165f,1410f})Fill(new Rect(x*scale,y+24*scale,scale,44*scale),new Color(.2f,.33f,.38f));
   bool grid=Val<bool>(boot,"showGrid");
   if(IconButton(new Rect(1430*scale,y,78*scale,92*scale),"magnet","时间吸附："+(grid?"开":"关"),grid))Command("SetBeatGrid",!grid);
   int pick=Val<int>(boot,"pickFilter");string[] filters={"全部物件","地面物件","空域物件"};string[] shapes={"all","ground","air"};
   for(int i=0;i<3;i++){int chosen=i;if(IconButton(new Rect((1518+50*i)*scale,y,50*scale,92*scale),shapes[i],"选取："+filters[i],pick==i))Queue(delegate{Set(boot,"pickFilter",chosen);});}
   GUI.Label(new Rect(Screen.width-128*scale,y+36*scale,120*scale,26*scale),(bool)Prop(boot,"HasUnsavedEdits")?"◇ 待保存":"· 已保存",caption);
  }
  void DrawHover(){
   if(hoverText.Length==0||Event.current.type!=EventType.Repaint)return;
   var style=new GUIStyle(caption){fontSize=FontPixels(17),normal={textColor=new Color(.82f,.95f,.97f)}};
   style.wordWrap=true;Vector2 size=HoverSize(hoverText,style);float width=size.x,height=size.y;
   float x=hoverRect.x<Screen.width*.5f?hoverRect.xMax+12*scale:hoverRect.x-width-12*scale,y=hoverRect.center.y-height*.5f;
   if(hoverRect.y>=footerRect.y){x=hoverRect.x;y=footerRect.y-height-10*scale;}
   x=Mathf.Clamp(x,0,Screen.width-width);y=Mathf.Clamp(y,0,Screen.height-height);
   Fill(new Rect(x,y,width,height),new Color(.025f,.105f,.14f,.97f));Fill(new Rect(x,y,2*scale,height),new Color(.51f,.83f,.88f));RasterText(new Rect(x+12*scale,y,width-24*scale,height),hoverText,17,new Color(.82f,.95f,.97f),CNFace,0);
  }
  void GridOptions(){
   Section("显示与吸附");bool grid=Val<bool>(boot,"showGrid");bool next=GUILayout.Toggle(grid,"节拍网格 / 时间吸附");if(next!=grid)Command("SetBeatGrid",next);Flag("小节线","showMeasureLines");
   bool linear=Val<bool>(boot,"linearTimeLayout");bool result=GUILayout.Toggle(linear,"统一流速");if(result!=linear)Queue(delegate{Call(boot,"CancelGesture");Set(boot,"linearTimeLayout",result);});
  }
  void PlacementDefaults(){
   Section("Tap / Hold");foreach(string name in new[]{"tapWidth","holdWidth"}){string field=name;Row(name=="tapWidth"?"Tap 默认宽度":"Hold 默认宽度","field_nativeui_"+name,Get(boot,name).ToString(),delegate(string value){int n;if(!int.TryParse(value,out n)||n<1||n>4)throw new FormatException("宽度须为 1～4");Set(boot,field,n);},false);}
   Placement();
  }
  void GroupBrowser(){
   Section("浏览组");Row("浏览组号","field_current_sky_group",Get(boot,"currentSkyGroup").ToString(),delegate(string value){Call(boot,"SetCurrentSkyGroup",value);},false);
   GUILayout.BeginHorizontal();if(GUILayout.Button("上一组起点"))Command("JumpWithinSkyGroup",-1);if(GUILayout.Button("下一组起点"))Command("JumpWithinSkyGroup",1);GUILayout.EndHorizontal();
   Help("这里用于浏览。物件 groupid 仍在物件参数中编辑。");
  }
  void WaveOptions(){
   Section("波形");Flag("显示详细波形时间轴","showDetailTimeline");Section("音乐音量");float current=Transport.Volume;float next=GUILayout.HorizontalSlider(current,0,1);if(next!=current)Queue(delegate{Transport.Volume=next;});
  }
  void ToolsHome(){Section("分组与几何");if(GUILayout.Button("组浏览"))Open("组浏览");if(GUILayout.Button("完整谱面工具"))Queue(delegate{toolsLegacy=true;Set(boot,"authoringToolsOpen",true);});Help("分组、拆分与修复继续使用原有工具。");}
  Texture2D Icon(string key){
   Texture2D cached;if(icons.TryGetValue(key,out cached))return cached;
   string asset=PathForIcon(key);if(System.IO.File.Exists(asset)){cached=new Texture2D(2,2,TextureFormat.RGBA32,false);typeof(ImageConversion).GetMethod("LoadImage",new[]{typeof(Texture2D),typeof(byte[]),typeof(bool)}).Invoke(null,new object[]{cached,System.IO.File.ReadAllBytes(asset),false});cached.filterMode=FilterMode.Bilinear;icons[key]=cached;return cached;}
   var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
   Action<float[]> path=delegate(float[] p){for(int i=2;i<p.Length;i+=2)PaintLine(pixels,p[i-2],p[i-1],p[i],p[i+1]);};
   Action<float,float,float> circle=delegate(float x,float y,float r){float px=x+r,py=y;for(int i=1;i<=32;i++){float a=i*Mathf.PI*2/32,qx=x+Mathf.Cos(a)*r,qy=y+Mathf.Sin(a)*r;PaintLine(pixels,px,py,qx,qy);px=qx;py=qy;}};
   switch(key){
    case "radial-point":path(new float[]{6,2,29,19,19,21,25,30,19,33,13,23,6,29,6,2});break;
    case "point":circle(17,17,5);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;path(new[]{17+Mathf.Cos(a)*10,17+Mathf.Sin(a)*10,17+Mathf.Cos(a)*15,17+Mathf.Sin(a)*15});}break;
    case "range":path(new float[]{3,11,3,3,11,3});path(new float[]{23,3,31,3,31,11});path(new float[]{3,23,3,31,11,31});path(new float[]{23,31,31,31,31,23});break;
    case "copy":path(new float[]{3,3,24,3,24,26,3,26,3,3});path(new float[]{10,8,31,8,31,31,10,31,10,8});break;
    case "delete":path(new float[]{7,10,9,31,26,31,28,10});path(new float[]{4,8,31,8});path(new float[]{12,8,12,3,23,3,23,8});path(new float[]{14,14,14,26});path(new float[]{21,14,21,26});break;
    case "cut":circle(8,25,5);circle(25,25,5);path(new float[]{11,21,28,3});path(new float[]{22,21,5,3});break;
    case "mirror":path(new float[]{17,2,17,32});path(new float[]{3,6,13,16,3,27,3,6});path(new float[]{31,6,21,16,31,27,31,6});break;
    case "grid":path(new float[]{4,4,30,4,30,30,4,30,4,4});foreach(float a in new[]{12f,21f}){path(new[]{4f,a,30f,a});path(new[]{a,4f,a,30f});}break;
    case "air":path(new float[]{4,29,12,4,22,4,30,29,4,29});path(new float[]{6,21,28,21});path(new float[]{9,13,25,13});path(new float[]{17,4,17,29});break;
    case "render":path(new float[]{3,7,25,7,25,28,3,28,3,7});path(new float[]{25,13,32,9,32,26,25,22});path(new float[]{10,12,18,17,10,23,10,12});break;
    case "wave":for(int i=0;i<7;i++){float h=5+Mathf.Abs(Mathf.Sin(i*2.1f))*22;path(new[]{3+i*4f,17-h*.5f,3+i*4f,17+h*.5f});}break;
    case "loop":path(new float[]{26,8,12,8,5,12,3,19});path(new float[]{8,27,22,27,29,22,31,15});path(new float[]{26,3,31,8,26,13});path(new float[]{8,22,3,27,8,32});break;
    case "settings":circle(17,17,10);circle(17,17,4);for(int i=0;i<8;i++){float a=i*Mathf.PI/4;path(new[]{17+Mathf.Cos(a)*12,17+Mathf.Sin(a)*12,17+Mathf.Cos(a)*15,17+Mathf.Sin(a)*15});}break;
    case "params":for(int i=0;i<3;i++){float y=7+i*10,x=11+i*5%12;path(new[]{3f,y,31f,y});path(new[]{x,y-4,x,y+4});}break;
    case "timing":path(new float[]{3,8,19,8});path(new float[]{3,17,12,17});path(new float[]{3,26,12,26});circle(24,21,8);path(new float[]{24,16,24,21,28,23});break;
    case "groups":path(new float[]{3,4,13,4,13,13,3,13,3,4});path(new float[]{21,21,31,21,31,30,21,30,21,21});path(new float[]{8,13,8,26,21,26});path(new float[]{13,8,26,8,26,21});break;
    case "tools":path(new float[]{24,3,16,5,12,11,14,17,3,28,6,31,18,20,25,21,31,16,31,10,25,16,19,10,24,3});break;
    case "authoring":circle(7,17,4);circle(27,6,4);circle(27,28,4);path(new float[]{11,17,17,17,17,6,23,6});path(new float[]{17,17,17,28,23,28});break;
    case "check":for(int i=0;i<3;i++){float y=7+i*10;path(new[]{2f,y,5f,y+3,11f,y-3});path(new[]{15f,y,31f,y});}break;
    case "folder":path(new float[]{3,8,3,5,13,5,17,10,31,10,31,30,3,30,3,8});path(new float[]{3,12,31,12});break;
    case "help":circle(17,17,13);path(new float[]{12,12,13,9,19,8,23,12,21,16,17,18,17,22});path(new float[]{17,26,17,27});break;
    case "exit":path(new float[]{18,4,4,4,4,31,18,31});path(new float[]{13,17,32,17,25,10});path(new float[]{32,17,25,24});break;
    case "play":path(new float[]{9,3,30,17,9,31,9,3});break;
    case "undo":path(new float[]{11,4,3,12,11,20});path(new float[]{3,12,21,12,29,18,31,24,31,29});break;
    case "redo":path(new float[]{23,4,31,12,23,20});path(new float[]{31,12,13,12,5,18,3,24,3,29});break;
    case "save":path(new float[]{4,3,27,3,32,8,32,31,4,31,4,3});path(new float[]{11,3,11,13,25,13,25,3});path(new float[]{11,31,11,20,25,20,25,31});break;
    case "bpm":path(new float[]{3,20,9,20,13,7,19,29,24,16,33,16});break;
    case "offset":path(new float[]{17,3,17,31});path(new float[]{3,10,14,10,10,6});path(new float[]{14,10,10,14});path(new float[]{31,24,20,24,24,20});path(new float[]{20,24,24,28});break;
    case "clock":circle(17,17,13);path(new float[]{17,7,17,18,24,22});break;
    case "speed":path(new float[]{3,6,14,17,3,28});path(new float[]{19,6,30,17,19,28});break;
    case "rate":circle(17,17,13);path(new float[]{13,10,23,17,13,24,13,10});break;
    case "magnet":path(new float[]{7,3,7,20,10,26,17,28,24,26,27,20,27,3,21,3,21,20,17,22,13,20,13,3,7,3});path(new float[]{7,9,13,9});path(new float[]{21,9,27,9});break;
    case "all":path(new float[]{3,8,31,8});path(new float[]{3,26,31,26});path(new float[]{10,8,7,26});path(new float[]{24,8,27,26});break;
    case "ground":path(new float[]{3,26,31,26});path(new float[]{7,19,12,19});path(new float[]{16,19,21,19});path(new float[]{25,19,30,19});break;
   }
   texture.SetPixels(pixels);texture.Apply();texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;icons[key]=texture;return texture;
  }
  static void PaintLine(Color[] pixels,float ax,float ay,float bx,float by){
   ax=ax*64/34;ay=ay*64/34;bx=bx*64/34;by=by*64/34;var a=new Vector2(ax,ay);var delta=new Vector2(bx-ax,by-ay);float den=delta.sqrMagnitude;
   int xmin=Mathf.Max(0,(int)Mathf.Min(ax,bx)-2),xmax=Mathf.Min(63,(int)Mathf.Max(ax,bx)+2),ymin=Mathf.Max(0,(int)Mathf.Min(ay,by)-2),ymax=Mathf.Min(63,(int)Mathf.Max(ay,by)+2);
   for(int y=ymin;y<=ymax;y++)for(int x=xmin;x<=xmax;x++){var p=new Vector2(x+.5f,y+.5f);float t=den<.001f?0:Mathf.Clamp01(Vector2.Dot(p-a,delta)/den);float alpha=Mathf.Clamp01(1.65f-(p-(a+delta*t)).magnitude);int index=(63-y)*64+x;if(alpha>pixels[index].a)pixels[index]=new Color(1,1,1,alpha);}
  }
 }
}

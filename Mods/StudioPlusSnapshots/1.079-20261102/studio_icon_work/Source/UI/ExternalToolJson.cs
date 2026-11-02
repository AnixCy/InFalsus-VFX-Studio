using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
namespace InFalsusStudio.NativeEditorUI {
 // A local DTO codec: Unity's runtime type tree omits arrays of newly loaded
 // plugin DTOs. This does not touch chart parsing or any existing preferences.
 internal static class ToolJson {
  static readonly CultureInfo CI=CultureInfo.InvariantCulture;
  internal static T Read<T>(string text){return (T)Read(text,typeof(T));}
  internal static object Read(string text,Type type){var parser=new Parser(text);object value=parser.Value(0);parser.Space();if(!parser.End)throw new FormatException("Trailing JSON data.");return ConvertValue(value,type);}
  static object ConvertValue(object value,Type type){
   if(value==null)return null;if(type==typeof(string)){if(!(value is string))throw new FormatException("Expected JSON string.");return value;}if(type==typeof(bool)){if(!(value is bool))throw new FormatException("Expected JSON boolean.");return value;}
   if(type.IsPrimitive){double number=Convert.ToDouble(value,CI);if(double.IsNaN(number)||double.IsInfinity(number))throw new FormatException("Nonfinite JSON number.");if(type==typeof(int)){if(number!=Math.Truncate(number)||number<int.MinValue||number>int.MaxValue)throw new FormatException("Expected integer JSON number.");return (int)number;}return Convert.ChangeType(value,type,CI);}
   if(type.IsArray){var list=value as List<object>;if(list==null)throw new FormatException("Expected JSON array.");var result=Array.CreateInstance(type.GetElementType(),list.Count);for(int i=0;i<list.Count;i++)result.SetValue(ConvertValue(list[i],type.GetElementType()),i);return result;}
   var map=value as Dictionary<string,object>;if(map==null)throw new FormatException("Expected JSON object.");var instance=Activator.CreateInstance(type,true);foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance)){object member;if(map.TryGetValue(field.Name,out member))field.SetValue(instance,ConvertValue(member,field.FieldType));}return instance;
  }
  internal static string Write(object value){var output=new StringBuilder();Append(output,value,0);return output.ToString();}
  static void Append(StringBuilder b,object value,int depth){
   if(depth>32)throw new FormatException("JSON nesting too deep.");if(value==null){b.Append("null");return;}if(value is string){Quote(b,(string)value);return;}if(value is bool){b.Append((bool)value?"true":"false");return;}
   var type=value.GetType();if(type.IsPrimitive||value is decimal){double n=Convert.ToDouble(value,CI);if(double.IsNaN(n)||double.IsInfinity(n))throw new FormatException("Nonfinite JSON number.");b.Append(value is double?((double)value).ToString("R",CI):Convert.ToString(value,CI));return;}
   var list=value as IEnumerable;if(list!=null){b.Append('[');bool first=true;foreach(var item in list){if(!first)b.Append(',');Append(b,item,depth+1);first=false;}b.Append(']');return;}
   b.Append('{');bool initial=true;foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance)){if(!initial)b.Append(',');Quote(b,field.Name);b.Append(':');Append(b,field.GetValue(value),depth+1);initial=false;}b.Append('}');
  }
  static void Quote(StringBuilder b,string value){b.Append('"');foreach(char ch in value){switch(ch){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(ch<32)b.Append("\\u"+((int)ch).ToString("x4",CI));else b.Append(ch);break;}}b.Append('"');}
  sealed class Parser {
   readonly string text;int at;
   internal Parser(string value){text=(value??"").TrimStart('\ufeff');if(text.Length>8000000)throw new FormatException("JSON is too large.");}
   internal bool End{get{return at>=text.Length;}}internal void Space(){while(!End&&char.IsWhiteSpace(text[at]))at++;}
   bool Take(char ch){Space();if(!End&&text[at]==ch){at++;return true;}return false;}
   void Need(char ch){if(!Take(ch))throw new FormatException("Expected JSON '"+ch+"'.");}
   internal object Value(int depth){
    if(depth>32)throw new FormatException("JSON nesting too deep.");Space();if(End)throw new FormatException("Unexpected end of JSON.");char ch=text[at];if(ch=='"')return String();
    if(ch=='{'){at++;var map=new Dictionary<string,object>(StringComparer.Ordinal);if(Take('}'))return map;do{Space();string key=String();Need(':');if(map.ContainsKey(key))throw new FormatException("Duplicate JSON key.");map.Add(key,Value(depth+1));}while(Take(','));Need('}');return map;}
    if(ch=='['){at++;var list=new List<object>();if(Take(']'))return list;do{list.Add(Value(depth+1));if(list.Count>100000)throw new FormatException("JSON array is too large.");}while(Take(','));Need(']');return list;}
    if(ch=='t'&&Literal("true"))return true;if(ch=='f'&&Literal("false"))return false;if(ch=='n'&&Literal("null"))return null;
    int begin=at;if(ch=='-')at++;if(End)throw new FormatException("Invalid JSON number.");if(text[at]=='0')at++;else{if(text[at]<'1'||text[at]>'9')throw new FormatException("Invalid JSON value.");while(!End&&text[at]>='0'&&text[at]<='9')at++;}
    if(!End&&text[at]=='.'){at++;int digit=at;while(!End&&text[at]>='0'&&text[at]<='9')at++;if(digit==at)throw new FormatException("Invalid JSON fraction.");}
    if(!End&&(text[at]=='e'||text[at]=='E')){at++;if(!End&&(text[at]=='+'||text[at]=='-'))at++;int digit=at;while(!End&&text[at]>='0'&&text[at]<='9')at++;if(digit==at)throw new FormatException("Invalid JSON exponent.");}
    double number;if(!double.TryParse(text.Substring(begin,at-begin),NumberStyles.Float,CI,out number)||double.IsNaN(number)||double.IsInfinity(number))throw new FormatException("Invalid JSON number.");return number;
   }
   bool Literal(string value){if(at+value.Length>text.Length||string.CompareOrdinal(text,at,value,0,value.Length)!=0)return false;at+=value.Length;return true;}
   string String(){
    if(End||text[at++]!='"')throw new FormatException("Expected JSON string.");var b=new StringBuilder();while(!End){char ch=text[at++];if(ch=='"')return b.ToString();if(ch<32)throw new FormatException("Unescaped JSON control character.");if(ch!='\\'){b.Append(ch);continue;}if(End)break;char escape=text[at++];switch(escape){case '"':case '\\':case '/':b.Append(escape);break;case 'b':b.Append('\b');break;case 'f':b.Append('\f');break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;case 'u':if(at+4>text.Length)throw new FormatException("Invalid JSON Unicode escape.");ushort code;if(!ushort.TryParse(text.Substring(at,4),NumberStyles.HexNumber,CI,out code))throw new FormatException("Invalid JSON Unicode escape.");b.Append((char)code);at+=4;break;default:throw new FormatException("Invalid JSON escape.");}}throw new FormatException("Unterminated JSON string.");
   }
  }
 }
}

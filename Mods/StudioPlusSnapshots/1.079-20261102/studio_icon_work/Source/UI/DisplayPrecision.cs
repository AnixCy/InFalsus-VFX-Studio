using System;
using System.Globalization;
namespace InFalsusStudio.NativeEditorUI {
 internal static class DisplayPrecision {
  internal static string Number(string raw,int digits){double value;if(string.IsNullOrEmpty(raw)||!double.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out value)||double.IsNaN(value)||double.IsInfinity(value))return raw;double rounded=Math.Round(value,digits,MidpointRounding.AwayFromZero);if(rounded==0)rounded=0;return rounded.ToString(digits==4?"0.####":"0.########",CultureInfo.InvariantCulture);}
  internal static string Coordinate(string key,string raw){return key=="sx"||key=="ex"||key=="sw"||key=="ew"||key=="bothWidths"||key=="x"||key=="width"?Number(raw,4):raw;}
 }
}

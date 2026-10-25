using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace NTEAssistant {
public sealed class EnergyDepletedException:Exception {public EnergyDepletedException(int value):base("领取后闪电能量为 "+value+"，小于18，已停止所有步骤和后续循环。") {}}
public static class EnergyReader {
 public static string LastText,LastReason;
 public static bool MustStop(int value){return value<18;}
 public static int? Parse(string text){if(string.IsNullOrWhiteSpace(text))return null;text=text.Replace('／','/').Replace('\\','/');var matches=Regex.Matches(text,@"(?<![0-9A-Za-z])([0-9OoIl|bSs]{1,5})\s*/\s*([5Ss][0Oo][0Oo])(?![0-9A-Za-z])");if(matches.Count!=1)return null;string current=matches[0].Groups[1].Value.Replace('O','0').Replace('o','0').Replace('I','1').Replace('l','1').Replace('|','1').Replace('b','6').Replace('S','5').Replace('s','5');int value;return int.TryParse(current,out value)&&value<=500?(int?)value:null;}
 static async Task<string> Text(Bitmap image){var engine=OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))??OcrEngine.TryCreateFromUserProfileLanguages();if(engine==null)throw new Exception("系统没有可用的 Windows OCR 语言。");using(var ms=new MemoryStream())using(var stream=new InMemoryRandomAccessStream()){image.Save(ms,ImageFormat.Png);using(var writer=new DataWriter(stream.GetOutputStreamAt(0))){writer.WriteBytes(ms.ToArray());await writer.StoreAsync();await writer.FlushAsync();writer.DetachStream();}stream.Seek(0);var decoder=await BitmapDecoder.CreateAsync(stream);using(var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied)){return (await engine.RecognizeAsync(bitmap)).Text;}}}
 public static async Task<int?> Read(Bitmap image){LastText=null;LastReason=null;var trace=new System.Text.StringBuilder();for(int mode=0;mode<3;mode++){int scale=mode==0?3:4;if(image.Width*scale+32>OcrEngine.MaxImageDimension||image.Height*scale+32>OcrEngine.MaxImageDimension)continue;using(var prepared=new Bitmap(image.Width*scale+32,image.Height*scale+32)){using(var g=Graphics.FromImage(prepared)){g.Clear(mode==1?Color.FromArgb(30,30,30):Color.White);if(mode==1){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(image,16,16,image.Width*scale,image.Height*scale);}}if(mode!=1)for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){var c=image.GetPixel(x,y);int bright=Math.Max(c.R,Math.Max(c.G,c.B)),dark=Math.Min(c.R,Math.Min(c.G,c.B));int shade=mode==0?(dark>=155&&bright-dark<75?0:255):255-(c.R*30+c.G*59+c.B*11)/100;var color=Color.FromArgb(shade,shade,shade);for(int dy=0;dy<scale;dy++)for(int dx=0;dx<scale;dx++)prepared.SetPixel(16+x*scale+dx,16+y*scale+dy,color);}string raw=await Text(prepared);trace.AppendLine((mode==0?"白字过滤":mode==1?"原图放大":"灰度反相")+": "+raw);LastText=trace.ToString();var value=Parse(raw);if(value.HasValue){LastReason="识别成功 · "+(mode==0?"白字过滤":mode==1?"原图放大":"灰度反相");return value;}}}LastReason="三种图像处理均未确认 当前值/500，请查看OCR原文和裁剪区域。";return null;}
}
public sealed class RevenueGate {
 int hits;
 public bool Observe(int? value){hits=value.HasValue&&value.Value>=1500?hits+1:0;return hits>=2;}
 public void Reset(){hits=0;}
}
public static class RevenueReader {
 public static string LastText,LastReason;
 public static int? Parse(string raw){if(raw==null)return null;string t=Regex.Replace(raw,@"\s+","");if(Regex.IsMatch(t,@"^\d{1,3}(?:[,，.．]\d{3})+$"))t=Regex.Replace(t,@"[,，.．]","");int value;return Regex.IsMatch(t,@"^\d{1,6}$")&&int.TryParse(t,out value)?(int?)value:null;}
 public static async Task<int?> Read(Bitmap image) {
  LastText=null;LastReason=null;
  int minX=image.Width,minY=image.Height,maxX=-1,maxY=-1;
  for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){
   var c=image.GetPixel(x,y);if(Yellow(c)){minX=Math.Min(x,minX);minY=Math.Min(y,minY);maxX=Math.Max(x,maxX);maxY=Math.Max(y,maxY);}
  }
  if(maxX<minX||maxY-minY<4){LastReason="区域内没有足够的黄色数字像素，检查标定框和当前界面。";return null;}
  // Only yellow glyphs survive; the white target value cannot enter OCR.
  using(var mask=new Bitmap((maxX-minX+1)*4+40,(maxY-minY+1)*4+40)) {
   using(var g=Graphics.FromImage(mask))g.Clear(Color.White);
   for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)if(Yellow(image.GetPixel(x,y)))for(int dy=0;dy<4;dy++)for(int dx=0;dx<4;dx++)mask.SetPixel(20+(x-minX)*4+dx,20+(y-minY)*4+dy,Color.Black);
   var engine=OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))??OcrEngine.TryCreateFromUserProfileLanguages();
   if(engine==null)throw new Exception("系统没有可用的 Windows OCR 语言。");
   if(mask.Width>OcrEngine.MaxImageDimension||mask.Height>OcrEngine.MaxImageDimension)throw new Exception("营业额区域包含过多黄色内容，请缩小标定范围。");
   using(var ms=new MemoryStream())using(var stream=new InMemoryRandomAccessStream()) {
    mask.Save(ms,ImageFormat.Png);var bytes=ms.ToArray();
    using(var writer=new DataWriter(stream.GetOutputStreamAt(0))) {writer.WriteBytes(bytes);await writer.StoreAsync();await writer.FlushAsync();writer.DetachStream();}
    stream.Seek(0);var decoder=await BitmapDecoder.CreateAsync(stream);
    using(var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied)){
     var result=await engine.RecognizeAsync(bitmap);LastText=result.Text;var value=Parse(result.Text);LastReason=value.HasValue?(minX<=1||maxX>=image.Width-2?"识别成功，但黄色字符贴近标定边缘，建议留出更多空间。":"识别成功（支持千位分隔符）。"):(string.IsNullOrWhiteSpace(result.Text)?"OCR没有读到文本。":"OCR文本不是有效整数或千位分组数字。");return value;
    }
   }
  }
 }
 public static bool Yellow(Color c){return c.R>=170&&c.G>=105&&c.B<=115&&c.R-c.B>=75&&c.G-c.B>=40;}
}
}

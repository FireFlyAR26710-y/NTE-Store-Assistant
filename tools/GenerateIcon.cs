using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

class GenerateIcon {
 static void Main(string[] args) {
  string source=args[0],folder=args[1];Directory.CreateDirectory(folder);
  int[] sizes={16,20,24,32,40,48,64,128,256};
  var frames=new byte[sizes.Length][];
  using(var original=new Bitmap(source)) {
   for(int i=0;i<sizes.Length;i++)using(var frame=new Bitmap(sizes[i],sizes[i],PixelFormat.Format32bppArgb)) {
    using(var g=Graphics.FromImage(frame)){
     g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.CompositingQuality=CompositingQuality.HighQuality;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
     double scale=(sizes[i]-2.0)/Math.Max(original.Width,original.Height);int w=(int)Math.Round(original.Width*scale),h=(int)Math.Round(original.Height*scale);
     g.DrawImage(original,(sizes[i]-w)/2,(sizes[i]-h)/2,w,h);
    }
    using(var ms=new MemoryStream()){frame.Save(ms,ImageFormat.Png);frames[i]=ms.ToArray();}
    if(sizes[i]==256)frame.Save(Path.Combine(folder,"app.png"),ImageFormat.Png);
   }
  }
  using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,"app.ico")))) {
   writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
   int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}
   foreach(var frame in frames)writer.Write(frame);
  }
 }
}

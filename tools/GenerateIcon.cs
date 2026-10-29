using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
class GenerateIcon {
 static GraphicsPath Round(float x,float y,float w,float h,float r){var p=new GraphicsPath();float d=r*2;p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;}
 static Bitmap Render(int size){
 var large=new Bitmap(size*4,size*4,PixelFormat.Format32bppArgb);
 using(var g=Graphics.FromImage(large)){
 g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Color.Transparent);g.ScaleTransform(size*4/256f,size*4/256f);
 using(var shape=Round(10,10,236,236,55)){
 using(var brush=new LinearGradientBrush(new Point(25,15),new Point(225,246),Color.FromArgb(19,29,47),Color.FromArgb(31,49,65)))g.FillPath(brush,shape);
 using(var pen=new Pen(Color.FromArgb(110,112,164,183),3))g.DrawPath(pen,shape);
 }
 if(size>=32){
 using(var b=new SolidBrush(Color.FromArgb(80,110,231,192)))g.FillRectangle(b,51,166,26,31);
 using(var b=new SolidBrush(Color.FromArgb(80,126,187,255)))g.FillRectangle(b,94,183,26,14);
 using(var b=new SolidBrush(Color.FromArgb(80,193,163,255)))g.FillRectangle(b,137,157,26,40);
 using(var b=new SolidBrush(Color.FromArgb(80,110,231,192)))g.FillRectangle(b,180,146,26,51);
 }
 using(var path=new GraphicsPath()){
 path.AddLines(new[]{new PointF(44,120),new PointF(78,120),new PointF(100,73),new PointF(131,167),new PointF(155,120),new PointF(212,120)});
 using(var glow=new Pen(Color.FromArgb(28,110,231,192),26)){glow.LineJoin=LineJoin.Round;glow.StartCap=glow.EndCap=LineCap.Round;g.DrawPath(glow,path);}
 using(var pen=new Pen(Color.FromArgb(110,231,192),14)){pen.LineJoin=LineJoin.Round;pen.StartCap=pen.EndCap=LineCap.Round;g.DrawPath(pen,path);}
 }
 if(size>=32)using(var b=new SolidBrush(Color.FromArgb(110,231,192)))g.FillEllipse(b,192,52,13,13);
 }
 var result=new Bitmap(size,size,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.CompositingQuality=CompositingQuality.HighQuality;g.DrawImage(large,0,0,size,size);}large.Dispose();return result;
 }
 static byte[] Data(Bitmap b){using(var stream=new MemoryStream()){if(b.Width>=64){b.Save(stream,ImageFormat.Png);return stream.ToArray();}
 using(var w=new BinaryWriter(stream)){int n=b.Width,maskStride=((n+31)/32)*4;w.Write(40);w.Write(n);w.Write(n*2);w.Write((short)1);w.Write((short)32);w.Write(0);w.Write(n*n*4);w.Write(0);w.Write(0);w.Write(0);w.Write(0);
 for(int y=n-1;y>=0;y--)for(int x=0;x<n;x++){var c=b.GetPixel(x,y);w.Write(c.B);w.Write(c.G);w.Write(c.R);w.Write(c.A);}
 for(int y=n-1;y>=0;y--){byte[] mask=new byte[maskStride];for(int x=0;x<n;x++)if(b.GetPixel(x,y).A==0)mask[x/8]|=(byte)(128>>(x%8));w.Write(mask);}return stream.ToArray();}
 }}
 static void Main(string[] args){string dir=args[0];Directory.CreateDirectory(dir);int[] sizes={16,20,24,32,40,48,64,128,256};byte[][] data=new byte[sizes.Length][];
 for(int i=0;i<sizes.Length;i++)using(var b=Render(sizes[i])){data[i]=Data(b);if(sizes[i]==256)b.Save(Path.Combine(dir,"TaskbarPulse.png"),ImageFormat.Png);}
 using(var w=new BinaryWriter(File.Create(Path.Combine(dir,"TaskbarPulse.ico")))){w.Write((short)0);w.Write((short)1);w.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((short)1);w.Write((short)32);w.Write(data[i].Length);w.Write(offset);offset+=data[i].Length;}foreach(var bytes in data)w.Write(bytes);}
 }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("TaskbarPulse")]
[assembly: System.Reflection.AssemblyDescription("Monitor zasobów na pasku zadań Windows")]
[assembly: System.Reflection.AssemblyProduct("TaskbarPulse")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace TaskbarPulse {
static class Branding { public static readonly Icon AppIcon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
public class Settings {
 public bool Disk=false;
 public bool DockToTray=true;
 public int TrayGap=6;
 public bool CPU=true, RAM=true, GPU=true, Network=true, Bars=true, HideFullscreen=true, AutoColor=true;
 public int Offset=16, Width=360, Interval=1000, FontSize=9;
 public string Adapter="", Foreground="#E8EDF5", Background="#242424", Accent="#6EE7C0";
 public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TaskbarPulse"); } }
 public static string FilePath { get { return Path.Combine(Dir,"settings.xml"); } }
 public static Settings Load() { try { using(var f=File.OpenRead(FilePath)) { var s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f); s.TrayGap=Math.Max(0,Math.Min(100,s.TrayGap)); s.Offset=Math.Max(0,Math.Min(10000,s.Offset)); s.Width=Math.Max(180,Math.Min(1200,s.Width)); s.Interval=Math.Max(500,Math.Min(5000,s.Interval)); s.FontSize=Math.Max(7,Math.Min(14,s.FontSize)); return s; } } catch { return new Settings(); } }
 public void Save() { Directory.CreateDirectory(Dir); string tmp=FilePath+".tmp"; using(var f=File.Create(tmp)) new XmlSerializer(typeof(Settings)).Serialize(f,this); if(File.Exists(FilePath)) File.Replace(tmp,FilePath,null); else File.Move(tmp,FilePath); }
 public static bool Startup { get { using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) return k!=null && k.GetValue("TaskbarPulse")!=null; } set { using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) { if(value) k.SetValue("TaskbarPulse","\""+Application.ExecutablePath+"\""); else k.DeleteValue("TaskbarPulse",false); } } }
}
static class Native {
 public static bool TrayBounds(IntPtr taskbar,out Rectangle bounds) {
  bounds=Rectangle.Empty; var tray=FindWindowEx(taskbar,IntPtr.Zero,"TrayNotifyWnd",null); Rect r;
  if(tray==IntPtr.Zero || !IsWindowVisible(tray) || !GetWindowRect(tray,out r) || r.R<=r.L)return false;
  bounds=r.Box;return true;
 }
 public static Rectangle DockBounds(Rectangle bar,Rectangle tray,int width,int height,int gap) {
  width=Math.Min(width,Math.Max(0,tray.Left-bar.Left-gap));
  return new Rectangle(tray.Left-gap-width,bar.Top+(bar.Height-height)/2,width,height);
 }

 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int L,T,R,B; public Rectangle Box { get { return Rectangle.FromLTRB(L,T,R,B); } } }
 [StructLayout(LayoutKind.Sequential)] public struct Mem { public uint Length,Load; public ulong Total,Available,Page,AvailPage,Virtual,AvailVirtual,Extended; }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr w,out Rect r);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr w);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr w,IntPtr after,int x,int y,int cx,int cy,uint flags);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
 [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref Mem m);
 [DllImport("pdh.dll",CharSet=CharSet.Unicode)] public static extern uint PdhOpenQuery(string src,IntPtr data,out IntPtr q);
 [DllImport("pdh.dll",CharSet=CharSet.Unicode)] public static extern uint PdhAddEnglishCounter(IntPtr q,string path,IntPtr data,out IntPtr c);
 [DllImport("pdh.dll")] public static extern uint PdhCollectQueryData(IntPtr q);
 [DllImport("pdh.dll",CharSet=CharSet.Unicode)] public static extern uint PdhGetFormattedCounterArray(IntPtr c,uint fmt,ref uint size,out uint count,IntPtr items);
 [DllImport("pdh.dll")] public static extern uint PdhCloseQuery(IntPtr q);
 [StructLayout(LayoutKind.Explicit,Size=16)] public struct Value { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Number; }
 [StructLayout(LayoutKind.Sequential)] public struct Item { public IntPtr Name; public Value Value; }
}
public class Sample { public double Cpu=-1,Ram=-1,Gpu=-1,Disk=-1,UsedGB,TotalGB,Down,Up; public string Error="",GpuStatus="Oczekiwanie na licznik GPU"; }
class Metrics : IDisposable {
 long idle,kernel,user; IntPtr query,counter,diskQuery,diskCounter; Dictionary<string,long[]> net=new Dictionary<string,long[]>(); Stopwatch clock=Stopwatch.StartNew(); double previous;
 public Metrics() { Native.GetSystemTimes(out idle,out kernel,out user); if(Native.PdhOpenQuery(null,IntPtr.Zero,out query)==0) { Native.PdhAddEnglishCounter(query,@"\GPU Engine(*)\Utilization Percentage",IntPtr.Zero,out counter); Native.PdhCollectQueryData(query); } }
 public Sample Read(string adapter,bool diskEnabled=false) {
 var s=new Sample(); long i,k,u;
 if(Native.GetSystemTimes(out i,out k,out u)) { long delta=k-kernel+u-user; if(delta>0) s.Cpu=Math.Max(0,Math.Min(100,100.0*(delta-(i-idle))/delta)); idle=i;kernel=k;user=u; }
 var m=new Native.Mem(); m.Length=(uint)Marshal.SizeOf(m); if(Native.GlobalMemoryStatusEx(ref m)) { s.Ram=100.0*(m.Total-m.Available)/m.Total;s.UsedGB=(m.Total-m.Available)/1073741824.0;s.TotalGB=m.Total/1073741824.0; }
 double now=clock.Elapsed.TotalSeconds, dt=now-previous; previous=now; var live=new HashSet<string>();
 foreach(var n in NetworkInterface.GetAllNetworkInterfaces()) {
 if(n.OperationalStatus!=OperationalStatus.Up || n.NetworkInterfaceType==NetworkInterfaceType.Loopback || n.NetworkInterfaceType==NetworkInterfaceType.Tunnel || (adapter!="" && n.Id!=adapter)) continue;
 try { var ns=n.GetIPStatistics(); long[] old; live.Add(n.Id); if(net.TryGetValue(n.Id,out old)&&dt>0) { s.Down+=Math.Max(0,ns.BytesReceived-old[0])/dt;s.Up+=Math.Max(0,ns.BytesSent-old[1])/dt; } net[n.Id]=new long[]{ns.BytesReceived,ns.BytesSent}; } catch(NetworkInformationException) { }
 }
 foreach(var id in net.Keys.Where(x=>!live.Contains(x)).ToArray()) net.Remove(id);
 if(query!=IntPtr.Zero && counter!=IntPtr.Zero && Native.PdhCollectQueryData(query)==0) {
 uint bytes=0,count; Native.PdhGetFormattedCounterArray(counter,0x200,ref bytes,out count,IntPtr.Zero);
 if(bytes>0 && bytes<32*1024*1024) { IntPtr p=Marshal.AllocHGlobal((int)bytes); try {
 if(Native.PdhGetFormattedCounterArray(counter,0x200,ref bytes,out count,p)==0) {
 var engines=new Dictionary<string,double>(); int stride=Marshal.SizeOf(typeof(Native.Item));
 for(int j=0;j<count;j++) { var item=(Native.Item)Marshal.PtrToStructure(IntPtr.Add(p,j*stride),typeof(Native.Item)); if(item.Value.Status>1 || double.IsNaN(item.Value.Number)) continue;
 string name=Marshal.PtrToStringUni(item.Name); int start=name.IndexOf("luid_"); string key=start>=0?name.Substring(start):name; double val; engines.TryGetValue(key,out val); engines[key]=val+Math.Max(0,item.Value.Number);
 }
 if(engines.Count>0) { s.Gpu=Math.Min(100,engines.Values.Max());s.GpuStatus="Najbardziej obciążony silnik spośród wszystkich GPU"; }
 } } finally { Marshal.FreeHGlobal(p); } }
 }
 if(!diskEnabled && diskQuery!=IntPtr.Zero){Native.PdhCloseQuery(diskQuery);diskQuery=IntPtr.Zero;diskCounter=IntPtr.Zero;}
 if(diskEnabled){
 if(diskQuery==IntPtr.Zero && Native.PdhOpenQuery(null,IntPtr.Zero,out diskQuery)==0){Native.PdhAddEnglishCounter(diskQuery,@"\PhysicalDisk(*)\% Idle Time",IntPtr.Zero,out diskCounter);Native.PdhCollectQueryData(diskQuery);}
 if(diskCounter!=IntPtr.Zero && Native.PdhCollectQueryData(diskQuery)==0){
 uint size=0,count;Native.PdhGetFormattedCounterArray(diskCounter,0x200,ref size,out count,IntPtr.Zero);
 if(size>0 && size<8*1024*1024){IntPtr buffer=Marshal.AllocHGlobal((int)size);try{
 if(Native.PdhGetFormattedCounterArray(diskCounter,0x200,ref size,out count,buffer)==0){
 int stride=Marshal.SizeOf(typeof(Native.Item));for(int j=0;j<count;j++){
 var item=(Native.Item)Marshal.PtrToStructure(IntPtr.Add(buffer,j*stride),typeof(Native.Item));
 if(item.Value.Status>1 || double.IsNaN(item.Value.Number) || Marshal.PtrToStringUni(item.Name)=="_Total")continue;
 s.Disk=Math.Max(s.Disk,Math.Max(0,Math.Min(100,100-item.Value.Number)));
 }
 }
 }finally{Marshal.FreeHGlobal(buffer);}}
 }
 }
 if(s.Gpu<0) s.GpuStatus="GPU: licznik niedostępny (wymagany sterownik WDDM).";
 return s;
 }
 public void Dispose() { if(query!=IntPtr.Zero) Native.PdhCloseQuery(query);if(diskQuery!=IntPtr.Zero)Native.PdhCloseQuery(diskQuery); }
}
// One layout calculation drives both the window bounds and the drawing.
class PanelLayout {
 public const float Padding=10;
 public float[] Metrics;
 public float Network;
 public int Width;
 public static PanelLayout Measure(Settings s,Sample sample) {
 using(var bmp=new Bitmap(1,1)){bmp.SetResolution(96,96);using(var g=Graphics.FromImage(bmp))
 using(var label=new Font("Segoe UI",Math.Max(7,s.FontSize-1)*96f/72f,FontStyle.Regular,GraphicsUnit.Pixel))
 using(var value=new Font("Segoe UI",(s.FontSize+1)*96f/72f,FontStyle.Bold,GraphicsUnit.Pixel)) {
 var widths=new List<float>();
 foreach(var name in new[]{s.CPU?"CPU":null,s.RAM?"RAM":null,s.GPU?"GPU":null,s.Disk?"DYSK":null}) {
 if(name!=null)widths.Add((float)Math.Ceiling(Math.Max(g.MeasureString("100%",value).Width,g.MeasureString(name,label).Width+8))+12);
 }
 float net=0;if(s.Network){// Fixed capacity through 1023.9 KiB/MiB/GiB/TiB per second; readings never resize the tile.
 float text=g.MeasureString("1023.9 M/s",label).Width;net=(float)Math.Ceiling(text)+20;}
 return new PanelLayout{Metrics=widths.ToArray(),Network=net,Width=(int)Math.Ceiling(2*Padding+widths.Sum()+net)};
 }}
 }
}
class Widget : Form {
 Settings cfg; Sample sample=new Sample(); Metrics metrics; System.Windows.Forms.Timer layoutTimer, sampleTimer; bool busy,stopping; SettingsForm settingsWindow; ToolTip tip=new ToolTip(); Font numberFont,labelFont; string lastTip=""; bool hovered;
 public Widget() {
 cfg=Settings.Load(); Icon=Branding.AppIcon; Text="TaskbarPulse"; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=Program.Preview; TopMost=true; DoubleBuffered=true; StartPosition=FormStartPosition.Manual;
 SetFonts();
 MouseEnter+=(a,b)=>{hovered=true;Invalidate();};MouseLeave+=(a,b)=>{hovered=false;Invalidate();};
 var menu=new ContextMenuStrip(); menu.Items.Add("Ustawienia…",null,(a,b)=>OpenSettings()); menu.Items.Add("Menedżer zadań",null,(a,b)=>Process.Start("taskmgr.exe")); menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Zakończ",null,(a,b)=>Close()); ContextMenuStrip=menu;
 MouseDoubleClick+=(a,b)=>{if(b.Button==MouseButtons.Left)OpenSettings();};
 layoutTimer=new System.Windows.Forms.Timer{Interval=200}; layoutTimer.Tick+=(a,b)=>Place(); layoutTimer.Start();
 sampleTimer=new System.Windows.Forms.Timer{Interval=cfg.Interval}; sampleTimer.Tick+=async(a,b)=>await RefreshSample(); sampleTimer.Start();
 Shown+=async(a,b)=>{Place(); await RefreshSample();};
 SystemEvents.DisplaySettingsChanged+=DisplayChanged;
 }
 void DisplayChanged(object a,EventArgs b) { if(!IsDisposed&&IsHandleCreated) BeginInvoke((Action)Place); }
 protected override bool ShowWithoutActivation {get{return true;}}
 protected override CreateParams CreateParams {get{var p=base.CreateParams;if(!Program.Preview)p.ExStyle|=0x08000080;return p;}}
 void SetFonts() { if(numberFont!=null) numberFont.Dispose();if(labelFont!=null)labelFont.Dispose();numberFont=new Font("Segoe UI",cfg.FontSize+1,FontStyle.Bold);labelFont=new Font("Segoe UI",Math.Max(7,cfg.FontSize-1),FontStyle.Regular); }
 async Task RefreshSample() {
 if(busy||stopping)return;busy=true;
 string adapter=cfg.Adapter;bool diskEnabled=cfg.Disk;
 try { var next=await Task.Run(()=>{ if(metrics==null) metrics=new Metrics();return metrics.Read(adapter,diskEnabled); }); if(!stopping) {sample=next;Place();Invalidate();UpdateTip();} }
 catch(Exception ex) { sample.Error=ex.Message; } finally {busy=false;if(stopping && metrics!=null){metrics.Dispose();metrics=null;} }
 }
 public static string Rate(double n) { if(n>=1099511627776)return (n/1099511627776).ToString("0.0")+" T"; if(n>=1073741824)return (n/1073741824).ToString("0.0")+" G"; if(n>=1048576)return (n/1048576).ToString("0.0")+" M"; if(n>=1024)return (n/1024).ToString("0")+" K";return n.ToString("0")+" B"; }
 static string Percent(double n) {return n<0?"—":n.ToString("0")+"%";}
 void UpdateTip() { string text=String.Format("CPU {0}  |  RAM {1:0.0} / {2:0.0} GB\nGPU {3} — {4}\n↓ {5}B/s   ↑ {6}B/s\nDwuklik: ustawienia • prawy przycisk: menu",Percent(sample.Cpu),sample.UsedGB,sample.TotalGB,Percent(sample.Gpu),sample.GpuStatus,Rate(sample.Down).TrimEnd('B'),Rate(sample.Up).TrimEnd('B')); if(cfg.Disk)text+="\nDYSK "+Percent(sample.Disk)+" — aktywność najbardziej obciążonego dysku fizycznego"; if(text!=lastTip) { tip.SetToolTip(this,text);lastTip=text; } }
 void Place() {
 IntPtr taskbar=Native.FindWindow("Shell_TrayWnd",null); Native.Rect r;
 if(taskbar==IntPtr.Zero||!Native.GetWindowRect(taskbar,out r)||!Native.IsWindowVisible(taskbar)){Visible=false;return;}
 Rectangle bar=r.Box; var screen=Screen.FromHandle(taskbar); Rectangle sb=screen.Bounds;
 bool horizontal=bar.Width>bar.Height; bool hidden=horizontal?Rectangle.Intersect(bar,sb).Height<8:Rectangle.Intersect(bar,sb).Width<8;
 IntPtr fg=Native.GetForegroundWindow(); Native.Rect fr; bool full=false;
 if(cfg.HideFullscreen && fg!=IntPtr.Zero && fg!=Handle && fg!=taskbar && Native.GetWindowRect(fg,out fr)) { Rectangle f=fr.Box; full=f.Left<=sb.Left&&f.Top<=sb.Top&&f.Right>=sb.Right&&f.Bottom>=sb.Bottom; }
 if(hidden||full){Visible=false;return;}
 float scale=DeviceDpi/96f;int w=Math.Min((int)Math.Ceiling(PanelLayout.Measure(cfg,sample).Width*scale),Math.Max(100,bar.Width));int h=Math.Min((int)(40*scale),bar.Height-4);
 if(!horizontal){Visible=false;return;}
 int x=bar.Left+Math.Min((int)(cfg.Offset*scale),Math.Max(0,bar.Width-w)),y=bar.Top+(bar.Height-h)/2;
 if(cfg.DockToTray) {
  Rectangle icons;
  if(!Native.TrayBounds(taskbar,out icons)){Visible=false;return;}
  Rectangle dock=Native.DockBounds(bar,icons,w,h,(int)(cfg.TrayGap*scale));
  if(dock.Width<20*scale){Visible=false;return;}
  x=dock.X;y=dock.Y;w=dock.Width;h=dock.Height;
 }
 if(!Visible)Show();
 // Keep above Explorer without stealing focus. Move only when layout changes.
 Native.SetWindowPos(Handle,new IntPtr(-1),x,y,w,h,0x10|0x40);

 }
 Color ColorOf(string value,Color fallback) {try{return ColorTranslator.FromHtml(value);}catch{return fallback;}}
 static GraphicsPath Rounded(RectangleF r,float radius) {
 var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
 }
 protected override void OnSizeChanged(EventArgs e) {
 base.OnSizeChanged(e);if(Width<20||Height<10)return;
 using(var path=Rounded(new RectangleF(0,0,Width,Height),Math.Min(10,Height/3f))) {var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}
 }
 protected override void OnPaint(PaintEventArgs e) {
 base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
 float scale=DeviceDpi/96f;g.ScaleTransform(scale,scale);float width=Width/scale,height=Height/scale;
 bool light=false;if(cfg.AutoColor){try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))light=Convert.ToInt32(k.GetValue("SystemUsesLightTheme",0))==1;}catch{}}
 Color bg=cfg.AutoColor?(light?Color.FromArgb(247,249,252):Color.FromArgb(30,35,43)):ColorOf(cfg.Background,Color.FromArgb(30,35,43));
 Color fg=cfg.AutoColor?(light?Color.FromArgb(30,40,53):Color.FromArgb(240,244,250)):ColorOf(cfg.Foreground,Color.White);
 Color muted=light?Color.FromArgb(95,108,125):Color.FromArgb(153,168,187),accent=ColorOf(cfg.Accent,Color.Turquoise);
 g.Clear(bg);
 using(var path=Rounded(new RectangleF(.5f,.5f,width-1,height-1),9)) {
 using(var brush=new LinearGradientBrush(new PointF(0,0),new PointF(0,height),hovered?ControlPaint.Light(bg,.09f):ControlPaint.Light(bg,.035f),bg))g.FillPath(brush,path);
 using(var pen=new Pen(Color.FromArgb(light?38:28,fg)))g.DrawPath(pen,path);
 }
 var names=new List<string>();var levels=new List<double>();var colors=new List<Color>();
 if(cfg.CPU){names.Add("CPU");levels.Add(sample.Cpu);colors.Add(accent);}
 if(cfg.RAM){names.Add("RAM");levels.Add(sample.Ram);colors.Add(light?Color.FromArgb(40,125,207):Color.FromArgb(126,187,255));}
 if(cfg.GPU){names.Add("GPU");levels.Add(sample.Gpu);colors.Add(light?Color.FromArgb(126,83,195):Color.FromArgb(193,163,255));}
 if(cfg.Disk){names.Add("DYSK");levels.Add(sample.Disk);colors.Add(light?Color.FromArgb(183,123,29):Color.FromArgb(240,197,112));}
 var layout=PanelLayout.Measure(cfg,sample);float pad=PanelLayout.Padding;float networkWidth=layout.Network;float metricX=pad;
 float labelY=Math.Max(3,height/2-16),valueY=height/2-3;
 using(var lf=new Font("Segoe UI",Math.Max(7,cfg.FontSize-1)*96f/72f,FontStyle.Regular,GraphicsUnit.Pixel))
 using(var vf=new Font("Segoe UI",(cfg.FontSize+1)*96f/72f,FontStyle.Bold,GraphicsUnit.Pixel))
 using(var sf=new StringFormat{Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})
 using(var text=new SolidBrush(fg))using(var secondary=new SolidBrush(muted)) {
 for(int j=0;j<names.Count;j++) {
 float metricWidth=layout.Metrics[j],x=metricX;metricX+=metricWidth;
 using(var dot=new SolidBrush(colors[j]))g.FillEllipse(dot,x,labelY+4,4,4);
 g.DrawString(names[j],lf,secondary,new RectangleF(x+8,labelY,metricWidth-9,13),sf);
 g.DrawString(Percent(levels[j]),vf,text,new RectangleF(x-1,valueY,metricWidth-6,20),sf);
 if(cfg.Bars&&levels[j]>=0) {
 float bw=Math.Max(4,metricWidth-15),by=height-4;
 using(var track=new Pen(Color.FromArgb(30,fg),2))g.DrawLine(track,x,by,x+bw,by);
 using(var fill=new Pen(levels[j]>85?Color.FromArgb(255,160,113):colors[j],2)){fill.StartCap=LineCap.Round;fill.EndCap=LineCap.Round;g.DrawLine(fill,x,by,x+(float)(bw*levels[j]/100),by);}
 }
 }
 if(cfg.Network) {
 float nx=metricX;float lineY=height/2;
 if(names.Count>0)using(var pen=new Pen(Color.FromArgb(25,fg)))g.DrawLine(pen,nx-6,9,nx-6,height-9);
 using(var arrow=new SolidBrush(accent))g.DrawString("↓",vf,arrow,nx+1,lineY-17);
 using(var aligned=new StringFormat{Alignment=StringAlignment.Far,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(Rate(sample.Down)+"/s",lf,text,new RectangleF(nx+18,lineY-15,networkWidth-18,16),aligned);
 using(var arrow=new SolidBrush(light?Color.FromArgb(186,113,42):Color.FromArgb(239,188,120)))g.DrawString("↑",vf,arrow,nx+1,lineY);
 using(var aligned=new StringFormat{Alignment=StringAlignment.Far,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})g.DrawString(Rate(sample.Up)+"/s",lf,secondary,new RectangleF(nx+18,lineY+2,networkWidth-18,16),aligned);
 }
 }
 }
 public void OpenSettings() { if(settingsWindow!=null&&!settingsWindow.IsDisposed){settingsWindow.Activate();return;}settingsWindow=new SettingsForm(cfg,()=>{cfg.Save();SetFonts();sampleTimer.Interval=cfg.Interval;Place();});settingsWindow.Show();settingsWindow.Activate(); }
 protected override void OnFormClosed(FormClosedEventArgs e) { stopping=true;layoutTimer.Dispose();sampleTimer.Dispose();tip.Dispose();SystemEvents.DisplaySettingsChanged-=DisplayChanged;if(!busy&&metrics!=null)metrics.Dispose();if(settingsWindow!=null)settingsWindow.Close();numberFont.Dispose();labelFont.Dispose();base.OnFormClosed(e); }
}
class SettingsForm : Form {
 Settings cfg; Action apply; TableLayoutPanel table; int nextRow=1; CheckBox cpu,ram,gpu,network,disk,bars,fullscreen,auto,startup,dock; NumericUpDown offset,interval,font,gap; ComboBox adapter; Button background,foreground,accent; List<string> adapterIds=new List<string>();
 public SettingsForm(Settings s,Action onApply) {
 cfg=s;apply=onApply;Icon=Branding.AppIcon;Text="TaskbarPulse — ustawienia";Size=new Size(560,800);MinimumSize=new Size(520,620);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(24,29,39);ForeColor=Color.FromArgb(231,237,246);AutoScaleMode=AutoScaleMode.Dpi;
 table=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=2,AutoScroll=true}; table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));Controls.Add(table);
 var title=new Label{Text="Twój pasek. Twoje wskaźniki.",AutoSize=true,Font=new Font("Segoe UI",16,FontStyle.Bold),Margin=new Padding(0,0,0,12)};table.Controls.Add(title);table.SetColumnSpan(title,2);
 var checks=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,WrapContents=true};cpu=Check("CPU",s.CPU);ram=Check("RAM",s.RAM);gpu=Check("GPU",s.GPU);network=Check("Sieć",s.Network);disk=Check("Dysk",s.Disk);checks.Controls.AddRange(new Control[]{cpu,ram,gpu,network,disk});Row("Widoczne wskaźniki",checks);
 dock=Check("Przytul do ikon po prawej",s.DockToTray);Row("Automatyczna pozycja",dock);
 gap=Num(s.TrayGap,0,100,1);Row("Odstęp od ikon (px)",gap);
 offset=Num(s.Offset,0,10000,4);offset.Enabled=!s.DockToTray;Row("Ręcznie: od lewej (px)",offset);dock.CheckedChanged+=(a,b)=>{offset.Enabled=!dock.Checked;gap.Enabled=dock.Checked;};gap.Enabled=s.DockToTray;Row("Szerokość panelu",new Label{Text="Automatyczna · według modułów",AutoSize=true});interval=Num(s.Interval,500,5000,250);Row("Odświeżanie (ms)",interval);font=Num(s.FontSize,7,14,1);Row("Rozmiar tekstu",font);
 adapter=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill};adapter.Items.Add("Wszystkie aktywne karty");adapterIds.Add("");foreach(var n in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.NetworkInterfaceType!=NetworkInterfaceType.Loopback && n.NetworkInterfaceType!=NetworkInterfaceType.Tunnel)){adapter.Items.Add(n.Name);adapterIds.Add(n.Id);}adapter.SelectedIndex=Math.Max(0,adapterIds.IndexOf(s.Adapter));Row("Karta sieciowa",adapter);
 auto=Check("Kolory zgodne z Windows",s.AutoColor);Row("Wygląd",auto);background=ColorButton(s.Background);Row("Tło własne",background);foreground=ColorButton(s.Foreground);Row("Tekst własny",foreground);accent=ColorButton(s.Accent);Row("Kolor akcentu",accent);
 bars=Check("Paski obciążenia",s.Bars);Row("",bars);fullscreen=Check("Ukryj na pełnym ekranie",s.HideFullscreen);Row("",fullscreen);startup=Check("Uruchamiaj przy logowaniu",Settings.Startup);Row("Autostart",startup);
 var note=new Label{AutoSize=true,MaximumSize=new Size(470,0),Text="Panel automatycznie podąża za ikonami zasobnika po prawej stronie głównego paska. Szerokość dopasowuje się do włączonych modułów. Dysk (opcjonalny) pokazuje aktywność, nie zajęte miejsce. Panel nie rezerwuje miejsca przed przyciskami otwartych aplikacji. GPU pokazuje najbardziej obciążony silnik ze wszystkich kart. Sieć: bajty/s (K = KiB, M = MiB).",ForeColor=Color.FromArgb(163,178,197),Margin=new Padding(0,12,0,12)};table.Controls.Add(note,0,nextRow++);table.SetColumnSpan(note,2);
 var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Bottom,Padding=new Padding(24,10,24,14),FlowDirection=FlowDirection.RightToLeft};var save=new Button{Text="Zastosuj",AutoSize=true,BackColor=Color.FromArgb(110,231,192),ForeColor=Color.FromArgb(20,34,32),FlatStyle=FlatStyle.Flat};save.Click+=(a,b)=>Save();var close=new Button{Text="Zamknij",AutoSize=true};close.Click+=(a,b)=>Close();buttons.Controls.Add(save);buttons.Controls.Add(close);Controls.Add(buttons);table.BringToFront();AcceptButton=save;CancelButton=close;
 EventHandler modulesChanged=(a,b)=>{
 if(!cpu.Checked&&!ram.Checked&&!gpu.Checked&&!network.Checked&&!disk.Checked){((CheckBox)a).Checked=true;return;}
 cfg.CPU=cpu.Checked;cfg.RAM=ram.Checked;cfg.GPU=gpu.Checked;cfg.Network=network.Checked;cfg.Disk=disk.Checked;apply();
 };
 foreach(var cb in new[]{cpu,ram,gpu,network,disk})cb.CheckedChanged+=modulesChanged;

 }
 CheckBox Check(string t,bool v){return new CheckBox{Text=t,Checked=v,AutoSize=true,Margin=new Padding(0,4,8,4)};}
 NumericUpDown Num(int v,int lo,int hi,int step){return new NumericUpDown{Minimum=lo,Maximum=hi,Value=v,Increment=step,Dock=DockStyle.Fill};}
 void Row(string text,Control control){int r=nextRow++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));table.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(0,8,8,8)},0,r);table.Controls.Add(control,1,r);control.Margin=new Padding(0,4,0,4);}
 Button ColorButton(string c){Color color;try{color=ColorTranslator.FromHtml(c);}catch{color=Color.White;}var b=new Button{Text=ColorTranslator.ToHtml(color),BackColor=color,ForeColor=color.GetBrightness()>.5?Color.Black:Color.White,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat};b.Click+=(a,e)=>{using(var d=new ColorDialog{Color=b.BackColor,FullOpen=true})if(d.ShowDialog(this)==DialogResult.OK){b.BackColor=d.Color;b.ForeColor=d.Color.GetBrightness()>.5?Color.Black:Color.White;b.Text=ColorTranslator.ToHtml(d.Color);}};return b;}
 void Save(){try {if(!cpu.Checked&&!ram.Checked&&!gpu.Checked&&!network.Checked&&!disk.Checked){MessageBox.Show(this,"Wybierz przynajmniej jeden wskaźnik.");return;}cfg.DockToTray=dock.Checked;cfg.TrayGap=(int)gap.Value;cfg.CPU=cpu.Checked;cfg.RAM=ram.Checked;cfg.GPU=gpu.Checked;cfg.Network=network.Checked;cfg.Disk=disk.Checked;cfg.Offset=(int)offset.Value;cfg.Interval=(int)interval.Value;cfg.FontSize=(int)font.Value;cfg.Adapter=adapterIds[adapter.SelectedIndex];cfg.AutoColor=auto.Checked;cfg.Background=ColorTranslator.ToHtml(background.BackColor);cfg.Foreground=ColorTranslator.ToHtml(foreground.BackColor);cfg.Accent=ColorTranslator.ToHtml(accent.BackColor);cfg.Bars=bars.Checked;cfg.HideFullscreen=fullscreen.Checked;if(startup.Checked!=Settings.Startup)Settings.Startup=startup.Checked;apply();}catch(Exception ex){MessageBox.Show(this,"Nie udało się zapisać ustawień: "+ex.Message);}}
}
static class Program { public static bool Preview;
 [STAThread] static void Main(string[] args) {
 Preview=args.Contains("--preview"); Native.SetProcessDPIAware();
 if(args.Contains("--layout-info")) { var taskbar=Native.FindWindow("Shell_TrayWnd",null);Native.Rect r;Native.GetWindowRect(taskbar,out r);Rectangle tray;bool found=Native.TrayBounds(taskbar,out tray);Directory.CreateDirectory(Settings.Dir);var cfg=Settings.Load();Native.Rect current;Native.GetWindowRect(Native.FindWindow(null,"TaskbarPulse"),out current);var expected=Native.DockBounds(r.Box,tray,PanelLayout.Measure(cfg,new Sample()).Width,Math.Min(40,r.Box.Height-4),cfg.TrayGap);File.WriteAllText(Path.Combine(Settings.Dir,"layout.txt"),"BAR="+r.Box+"\r\nTRAY_FOUND="+found+"\r\nTRAY="+tray+"\r\nEXPECTED_96DPI="+expected+"\r\nACTUAL="+current.Box+"\r\nACTUAL_GAP="+(tray.Left-current.R));return; }
 Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
 if(args.Length>0 && args[0]=="--diagnose") {using(var m=new Metrics()){m.Read("",true);Thread.Sleep(1200);var s=m.Read("",true);Native.Rect r;Native.GetWindowRect(Native.FindWindow("Shell_TrayWnd",null),out r);Directory.CreateDirectory(Settings.Dir);File.WriteAllText(Path.Combine(Settings.Dir,"diagnostics.txt"),String.Format("CPU={0:F2}\r\nRAM={1:F2}\r\nRAM_GB={2:F2}/{3:F2}\r\nGPU={4:F2}\r\nGPU_STATUS={5}\r\nDOWN_BPS={6:F2}\r\nUP_BPS={7:F2}\r\nTASKBAR={8}\r\n",s.Cpu,s.Ram,s.UsedGB,s.TotalGB,s.Gpu,s.GpuStatus,s.Down,s.Up,r.Box)+"DISK="+s.Disk.ToString("F2")+Environment.NewLine);}return;}
 bool created;using(var mutex=new Mutex(true,"Local\\TaskbarPulse.Marek.v1",out created)){if(!created)return;Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(a,e)=>{Directory.CreateDirectory(Settings.Dir);File.AppendAllText(Path.Combine(Settings.Dir,"error.log"),DateTime.Now+" "+e.Exception+Environment.NewLine);};var widget=new Widget();if(args.Contains("--settings")) { EventHandler firstIdle=null; firstIdle=(a,b)=>{Application.Idle-=firstIdle;widget.OpenSettings();};Application.Idle+=firstIdle; }Application.Run(widget);}
 }
}
}

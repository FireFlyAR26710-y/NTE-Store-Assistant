using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace NTEAssistant {
public class Step {
 public string Name {get;set;}
 public string PointLabel {get{return EnergyCheck?"只读能量":RevenueEsc?"键盘 Esc":Recorded?X.ToString("P1")+", "+Y.ToString("P1"):"未标定";}}
 public bool RevenueEsc {get;set;}
 public bool EnergyCheck {get;set;}
 public double X {get;set;}
 public double Y {get;set;}
 public double Wait {get;set;}
 public bool Recorded {get;set;}
 public int[] Patch {get;set;}
 public int CaptureW {get;set;}
 public int CaptureH {get;set;}
}
public class Settings {
 public string Profile = "店长特供 1-9";
 public int Rounds = 1;
 public bool Guard = true;
 public int Timeout = 180;
 public bool EnergyRecorded = false;
 public double EnergyX, EnergyY, EnergyW, EnergyH;
 public bool RevenueRecorded = false;
 public double RevenueX, RevenueY, RevenueW, RevenueH;
 public List<Step> Steps = new List<Step>();
}
public class Native {
 [StructLayout(LayoutKind.Sequential)] public struct RECT {public int L,T,R,B;}
 [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT {public int dx,dy; public uint mouseData,dwFlags,time; public UIntPtr extra;}
 [StructLayout(LayoutKind.Explicit)] public struct UNION {[FieldOffset(0)] public MOUSEINPUT mi;}
 [StructLayout(LayoutKind.Sequential)] public struct INPUT {public uint type; public UNION u;}
 public delegate bool EnumProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb,IntPtr p);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder b,int n);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h,out RECT r);
 [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h,ref POINT p);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint n,INPUT[] v,int size);
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] public static extern int GetWindowLong(IntPtr h,int index);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int cmd);
 [DllImport("user32.dll")] public static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 public static string Title(IntPtr h) {var s=new StringBuilder(512);GetWindowText(h,s,512);return s.ToString();}
 public static Rectangle Client(IntPtr h) {RECT r;GetClientRect(h,out r);var p=new POINT();ClientToScreen(h,ref p);return new Rectangle(p.X,p.Y,r.R-r.L,r.B-r.T);}
 public static Point Position(Rectangle r,Step s) {return new Point(r.X+(int)Math.Round(s.X*(r.Width-1)),r.Y+(int)Math.Round(s.Y*(r.Height-1)));}
}
public class WindowItem {public IntPtr H;public string Text;public override string ToString(){return Text;}}
public partial class MainForm:Form {
 readonly string root=AppDomain.CurrentDomain.BaseDirectory;
 Settings cfg;
 ComboBox windows=new ComboBox();
 DataGridView grid=new DataGridView();
 NumericUpDown rounds=new NumericUpDown(), timeout=new NumericUpDown();
 CheckBox guard=new CheckBox();
 TextBox log=new TextBox();
 Label status=new Label();
 Button start;
 CancellationTokenSource cancel;
 IntPtr target;
 uint targetPid;
 int recordRow=-1;
 bool busy;
 bool recordRevenue;
 Native.POINT revenueCorner;
 bool hasCorner;
 Label revenueStatus=new Label();
 bool recordEnergy;
 Label energyStatus=new Label();
 public MainForm() {
  Text="异环 · 店长特供助手 | 1-9 · 工作台 2.0";ClientSize=new Size(1100,890);MinimumSize=new Size(1100,920);
  Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(242,245,249);StartPosition=FormStartPosition.CenterScreen;
  var title=new Label {Text="店长特供 1-9",Font=new Font(Font.FontFamily,22,FontStyle.Bold),AutoSize=true,Location=new Point(22,15)};Controls.Add(title);
  Controls.Add(new Label {Text="配队完成关卡，助手按你标定的流程点击。不会自动选关，请先在游戏中选好 1-9。",AutoSize=true,Location=new Point(24,65)});
  windows.SetBounds(24,99,720,32);windows.DropDownStyle=ComboBoxStyle.DropDownList;Controls.Add(windows);
  AddButton("刷新窗口",758,98,190,delegate{RefreshWindows();});
  grid.SetBounds(24,147,1052,231);grid.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.RowHeadersVisible=false;grid.MultiSelect=false;
  grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.BackgroundColor=Color.White;
  grid.Columns.Add("name","操作名称");grid.Columns.Add("point","游戏内位置（比例坐标）");grid.Columns.Add("wait","点击后等待 / 秒");grid.Columns.Add("state","标定状态");
  var revenueCol=new DataGridViewCheckBoxColumn{Name="revenue",HeaderText="等营业额1500后Esc"};grid.Columns.Add(revenueCol);grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="energy",HeaderText="领取后检查能量"});
  grid.Columns[1].ReadOnly=true;grid.Columns[3].ReadOnly=true;Controls.Add(grid);
  AddButton("标定所选步骤 · F6",24,391,225,delegate{ArmRecord();});
  AddButton("新增步骤",260,391,135,delegate{if(busy)return;ReadGrid();cfg.Steps.Add(new Step{Name="新步骤",Wait=2});FillGrid();});
  AddButton("删除步骤",407,391,135,delegate{if(busy)return;ReadGrid();if(grid.CurrentRow!=null)cfg.Steps.RemoveAt(grid.CurrentRow.Index);FillGrid();Save();});
  AddButton("保存设置",554,391,135,delegate{try{ReadGrid();Save();Write("设置已保存。");}catch(Exception e){Write(e.Message);}});
  Controls.Add(new Label{Text="标定：选择步骤 → 点标定 → 切回游戏，把鼠标放在按钮中心 → 按 F6。",AutoSize=true,Location=new Point(24,437)});
  Controls.Add(new Label{Text="每轮完成下表所有步骤一次；按实际流程增删步骤。F7 开始，F8 随时停止。切离游戏会暂停。",AutoSize=true,Location=new Point(24,463)});
  Controls.Add(new Label{Text="循环次数",AutoSize=true,Location=new Point(24,503)});rounds.SetBounds(104,499,83,30);rounds.Minimum=1;rounds.Maximum=9999;Controls.Add(rounds);
  guard.Text="匹配按钮画面后再点击";guard.SetBounds(213,500,241,29);Controls.Add(guard);
  Controls.Add(new Label{Text="画面等待上限 / 秒",AutoSize=true,Location=new Point(475,503)});timeout.SetBounds(641,499,89,30);timeout.Minimum=10;timeout.Maximum=3600;Controls.Add(timeout);
  start=AddButton("开始 · F7",24,544,225,delegate{StartRun();});start.BackColor=Color.FromArgb(37,99,235);start.ForeColor=Color.White;start.FlatStyle=FlatStyle.Flat;
  AddButton("停止 · F8",260,544,135,delegate{StopRun();});
  status.SetBounds(415,549,532,32);status.Text="待标定";Controls.Add(status);
  AddButton("标定营业额区域",24,591,225,delegate{if(busy||!Bind())return;recordRow=-1;recordEnergy=false;recordRevenue=true;hasCorner=false;Write("切回游戏：鼠标放在黄色营业额数字区域左上角按 F9，右下角按 F10。只框黄色当前值。");});
  revenueStatus.SetBounds(265,595,680,30);Controls.Add(revenueStatus);
  Controls.Add(new Label{Text="勾选“等营业额1500后Esc”的步骤不点击坐标；连续两次识别黄色当前值 ≥1500 后按 Esc。",AutoSize=true,Location=new Point(24,680)});
  AddButton("标定闪电能量区域",24,632,225,delegate{if(busy||!Bind())return;recordRow=-1;recordRevenue=false;recordEnergy=true;hasCorner=false;Write("切回领取完成后的界面，框选能量当前值/500：左上角 F9，右下角 F10。");});
  energyStatus.SetBounds(265,636,800,30);Controls.Add(energyStatus);
  log.SetBounds(24,720,1052,145);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Color.White;log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(log);
  cfg=LoadConfiguration(Path.Combine(root,"settings.json"));
  if(cfg==null||cfg.Steps==null)cfg=new Settings();
  if(cfg.Steps.Count==0)cfg.Steps.AddRange(new[]{new Step{Name="开始挑战",Wait=2},new Step{Name="领取",Wait=3},new Step{Name="再次挑战 / 返回",Wait=3}});
  if(!cfg.Steps.Exists(s=>s.RevenueEsc)) {int after=cfg.Steps.FindIndex(s=>s.Name.Contains("开始"));cfg.Steps.Insert(after>=0?after+1:cfg.Steps.Count,new Step{Name="营业额达到1500 → Esc",Wait=2,RevenueEsc=true});}
  if(!cfg.Steps.Exists(s=>s.EnergyCheck)){int claim=cfg.Steps.FindIndex(s=>s.Name.Contains("领取"));if(claim>=0)cfg.Steps.Insert(claim+1,new Step{Name="领取后检查能量（<18停止）",EnergyCheck=true,Wait=1});}
  foreach(var energyStep in cfg.Steps)if(energyStep.EnergyCheck)energyStep.Name=energyStep.Name.Replace("<6","<18");
  energyStatus.Text=cfg.EnergyRecorded?"能量区域已标定 · 领取后 <18 停止，≥18 继续":"能量区域未标定 · 仅在领取完成后检查";
  revenueStatus.Text=cfg.RevenueRecorded?"营业额区域已标定 · 阈值 1500":"营业额区域未标定 · 先框选黄色当前值";
  rounds.Value=Math.Max(1,Math.Min(9999,cfg.Rounds));guard.Checked=cfg.Guard;timeout.Value=Math.Max(10,Math.Min(3600,cfg.Timeout));FillGrid();RefreshWindows();
  BuildPages();
  Shown+=delegate{if(PreviewMode)return;bool a=Native.RegisterHotKey(Handle,6,0x4000,0x75),b=Native.RegisterHotKey(Handle,7,0x4000,0x76),c=Native.RegisterHotKey(Handle,8,0x4000,0x77);bool d=Native.RegisterHotKey(Handle,9,0x4000,0x78),e=Native.RegisterHotKey(Handle,10,0x4000,0x79);Write(a&&b&&c&&d&&e?"热键就绪：F6 标定，F7 开始，F8 停止。":"部分热键被占用；请先关闭占用热键的工具。界面按钮仍可使用。");Write("默认步骤只是待标定模板；尚未验证游戏 1-9 的按钮和时序。");};
  FormClosing+=delegate{StopRun();Native.UnregisterHotKey(Handle,6);Native.UnregisterHotKey(Handle,7);Native.UnregisterHotKey(Handle,8);Native.UnregisterHotKey(Handle,9);Native.UnregisterHotKey(Handle,10);if(!busy&&!PreviewMode)try{ReadGrid();Save();}catch{}};
 }
 public static Settings LoadConfiguration(string path){var serializer=new JavaScriptSerializer();if(File.Exists(path))try{var saved=serializer.Deserialize<Settings>(File.ReadAllText(path));if(saved!=null&&saved.Steps!=null)return saved;}catch{}using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("NTEAssistant.DefaultSettings.json")){if(stream==null)throw new Exception("默认参数资源缺失，请重新下载程序。");using(var reader=new StreamReader(stream,Encoding.UTF8))return serializer.Deserialize<Settings>(reader.ReadToEnd());}}
 void ValidateWindowMode(){if((Native.GetWindowLong(target,-16)&0x00C00000)!=0x00C00000)throw new Exception("必须使用窗口模式。请关闭全屏或无边框全屏后重试。");var client=Native.Client(target);if(client.Width!=1920||client.Height!=1080)Write("当前窗口 "+client.Width+"×"+client.Height+"。默认参数按1920×1080标定；其他窗口分辨率请自行测试，默认参数不保证可用。");}
 Button AddButton(string text,int x,int y,int w,Action action){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(w,36)};b.Click+=delegate{action();};Controls.Add(b);return b;}
 void Write(string s){if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action<string>(Write),s);return;}string line=DateTime.Now.ToString("HH:mm:ss")+"  "+s;log.AppendText(line+Environment.NewLine);try{Directory.CreateDirectory(Path.Combine(root,"Logs"));File.AppendAllText(Path.Combine(root,"Logs",DateTime.Now.ToString("yyyy-MM-dd")+".log"),line+Environment.NewLine);}catch{}}
 void FillGrid(){grid.Rows.Clear();foreach(var s in cfg.Steps)grid.Rows.Add(s.Name,s.EnergyCheck?"检查能量，无点击":s.RevenueEsc?"键盘 Esc":s.Recorded?s.X.ToString("P1")+", "+s.Y.ToString("P1"):"未标定",s.Wait,s.EnergyCheck?"<18停止全部步骤":s.RevenueEsc?"等黄色营业额 ≥1500":s.Recorded?"已标定":"待标定",s.RevenueEsc,s.EnergyCheck);}
 void ReadGrid(){grid.EndEdit();for(int i=0;i<cfg.Steps.Count;i++){double n;if(!double.TryParse(Convert.ToString(grid.Rows[i].Cells[2].Value),out n)||double.IsNaN(n)||double.IsInfinity(n)||n<0.3||n>3600)throw new Exception("等待时间需为 0.3–3600 秒。");cfg.Steps[i].Name=Convert.ToString(grid.Rows[i].Cells[0].Value);cfg.Steps[i].Wait=n;cfg.Steps[i].RevenueEsc=Convert.ToBoolean(grid.Rows[i].Cells[4].Value);cfg.Steps[i].EnergyCheck=Convert.ToBoolean(grid.Rows[i].Cells[5].Value);if(cfg.Steps[i].EnergyCheck&&cfg.Steps[i].RevenueEsc)throw new Exception("同一步骤不能同时作为营业额退出和能量检查。");}cfg.Rounds=(int)rounds.Value;cfg.Timeout=(int)timeout.Value;cfg.Guard=guard.Checked;}
 void Save(){if(PreviewMode)return;File.WriteAllText(Path.Combine(root,"settings.json"),new JavaScriptSerializer().Serialize(cfg),Encoding.UTF8);}
 void RefreshWindows(){if(busy)return;windows.Items.Clear();Native.EnumWindows(delegate(IntPtr h,IntPtr p){string t=Native.Title(h);if(Native.IsWindowVisible(h)&&h!=Handle&&(t.Contains("异环")||t.IndexOf("Neverness",StringComparison.OrdinalIgnoreCase)>=0||t.Equals("NTE",StringComparison.OrdinalIgnoreCase))&&!t.Contains("助手")&&!t.Contains("桌宠")&&!t.Contains("启动器")&&!t.Contains("Launcher"))windows.Items.Add(new WindowItem{H=h,Text=t});return true;},IntPtr.Zero);if(windows.Items.Count>0)windows.SelectedIndex=0;else windows.Text="";Write(windows.Items.Count==0?"未发现异环游戏窗口。进入游戏后点击“刷新窗口”。":"已发现游戏窗口，请核对后标定。");}
 bool Bind(){var w=windows.SelectedItem as WindowItem;if(w==null||!Native.IsWindow(w.H)){Write("请先进游戏，再刷新并选择异环窗口。");return false;}target=w.H;Native.GetWindowThreadProcessId(target,out targetPid);return true;}
 void ArmRecord(){if(busy)return;try{ReadGrid();}catch(Exception e){Write(e.Message);return;}if(!Bind()||grid.CurrentRow==null)return;if(cfg.Steps[grid.CurrentRow.Index].EnergyCheck){Write("此步骤只读取能量，无需坐标；使用“标定闪电能量区域”。");return;}recordEnergy=false;if(cfg.Steps[grid.CurrentRow.Index].RevenueEsc){Write("此步骤发送 Esc，无需坐标；请使用“标定营业额区域”。");return;}recordRevenue=false;recordRow=grid.CurrentRow.Index;status.Text="等待 F6："+cfg.Steps[recordRow].Name;Write("切回游戏，把鼠标放到“"+cfg.Steps[recordRow].Name+"”按钮上，按 F6 记录；F8 取消。");}
 static int[] Patch(Point p){using(var b=new Bitmap(24,16)){using(var g=Graphics.FromImage(b))g.CopyFromScreen(p.X-12,p.Y-8,0,0,b.Size);var a=new int[72];int k=0;for(int y=0;y<16;y+=4)for(int x=0;x<24;x+=4){Color c=b.GetPixel(x,y);a[k++]=c.R;a[k++]=c.G;a[k++]=c.B;}return a;}}
 public static double Difference(int[] a,int[] b){if(a==null||b==null||a.Length!=b.Length||a.Length==0)return 999;double d=0;for(int i=0;i<a.Length;i++)d+=Math.Abs(a[i]-b[i]);return d/a.Length;}
 void Record(){if(busy)return;if(recordRow<0){Write("请先在工具里选择步骤并点“标定所选步骤”。");return;}if(Native.GetForegroundWindow()!=target){Write("F6 未记录：请将异环切到前台。");return;}var r=Native.Client(target);Native.POINT p;Native.GetCursorPos(out p);if(r.Width<100||r.Height<100||!r.Contains(p.X,p.Y)){Write("鼠标须在游戏画面内。");return;}var s=cfg.Steps[recordRow];s.X=(double)(p.X-r.X)/(r.Width-1);s.Y=(double)(p.Y-r.Y)/(r.Height-1);s.Patch=Patch(new Point(p.X,p.Y));s.CaptureW=r.Width;s.CaptureH=r.Height;s.Recorded=true;Write("已标定 "+s.Name+"："+(p.X-r.X)+", "+(p.Y-r.Y)+" / "+r.Width+"×"+r.Height);recordRow=-1;FillGrid();Save();status.Text="标定已保存";}
 protected override void WndProc(ref Message m){if(m.Msg==0x312){int k=m.WParam.ToInt32();if(k==6)Record();if(k==7){try{if(BeforeShellStart!=null)BeforeShellStart();StartRun();}catch(Exception e){status.Text=e.Message;Write(e.Message);}}if(k==8)StopRun();if(k==9)RevenueCorner(false);if(k==10)RevenueCorner(true);}base.WndProc(ref m);}
 void StopRun(){recordEnergy=false;recordRevenue=false;hasCorner=false;recordRow=-1;if(cancel!=null)cancel.Cancel();if(!busy)status.Text="已停止";}
 async void StartRun(){if(busy||testing||PreviewMode)return;try{ReadGrid();if(!Bind())return;ValidateWindowMode();if(cfg.Steps.Count==0||cfg.Steps.Exists(s=>!s.EnergyCheck&&!s.RevenueEsc&&!s.Recorded))throw new Exception("请先标定所有步骤。");if(cfg.Guard&&cfg.Steps.Exists(s=>!s.EnergyCheck&&!s.RevenueEsc&&(s.Patch==null||s.Patch.Length!=72)))throw new Exception("请重新标定步骤以记录按钮画面。");if(cfg.Steps.Exists(s=>s.RevenueEsc)&&!cfg.RevenueRecorded)throw new Exception("请先标定黄色营业额的识别区域。");if(!cfg.Steps.Exists(s=>s.EnergyCheck))throw new Exception("请在领取步骤后添加一行，并勾选“领取后检查能量”。");if(!cfg.EnergyRecorded)throw new Exception("请先标定闪电能量当前值/500区域。");for(int e=0;e<cfg.Steps.Count;e++)if(cfg.Steps[e].EnergyCheck&&(e==0||!cfg.Steps[e-1].Name.Contains("领取")||cfg.Steps[e-1].EnergyCheck||cfg.Steps[e-1].RevenueEsc))throw new Exception("能量检查必须紧跟普通的“领取”步骤。请核对顺序及步骤名称。");Save();}catch(Exception e){Write(e.Message);return;}
  recordEnergy=false;recordRevenue=false;recordRow=-1;busy=true;grid.Enabled=false;windows.Enabled=false;start.Enabled=false;rounds.Enabled=false;guard.Enabled=false;timeout.Enabled=false;cancel=new CancellationTokenSource();var ct=cancel.Token;
  try{Write("启动校验通过。尝试切回异环；应停留在“"+cfg.Steps[0].Name+"”对应的界面。");Native.ShowWindowAsync(target,9);bool foreground=Native.SetForegroundWindow(target);Write(foreground?"已请求将游戏切到前台。":"系统未允许切换前台，请手动切回游戏。");for(int countdown=3;countdown>0;countdown--){status.Text=countdown+" 秒后开始 · F8 停止";await Task.Delay(1000,ct);}
   for(int n=1;n<=cfg.Rounds;n++)for(int i=0;i<cfg.Steps.Count;i++){
    var s=cfg.Steps[i];status.Text="第 "+n+" / "+cfg.Rounds+" 轮 · "+s.Name;if(s.EnergyCheck){await CheckEnergy(ct);await Task.Delay((int)(s.Wait*1000),ct);continue;}if(s.RevenueEsc){await WaitRevenue(ct);ct.ThrowIfCancellationRequested();if(Native.GetForegroundWindow()!=target)throw new Exception("游戏失去焦点，未发送 Esc。");await SimulateEscape(ct);Write("营业额达标，已发送 Esc。");await Task.Delay((int)(s.Wait*1000),ct);continue;}await WaitReady(s,ct);
    ct.ThrowIfCancellationRequested();uint pid;Native.GetWindowThreadProcessId(target,out pid);if(pid!=targetPid||Native.GetForegroundWindow()!=target)throw new Exception("目标窗口发生变化，已停止。");
    await SimulateMouseClick(s,ct);
    Write("第 "+n+" 轮 · "+s.Name+" 鼠标按下/松开已发送（尚未确认游戏响应），等待 "+s.Wait+" 秒。");await Task.Delay((int)(s.Wait*1000),ct);
   }
   status.Text="循环完成";Write("已执行 "+cfg.Rounds+" 轮点击流程。请以游戏实际结算为准。");
  }catch(EnergyDepletedException e){status.Text="能量不足18 · 已停止全部步骤";Write(e.Message);}catch(OperationCanceledException){status.Text="已停止";Write("已停止，不再点击。");}catch(Exception e){status.Text="已停止 · 需要检查";Write(e.Message);}finally{busy=false;grid.Enabled=true;windows.Enabled=true;start.Enabled=true;rounds.Enabled=true;guard.Enabled=true;timeout.Enabled=true;cancel.Dispose();cancel=null;}
 }
 void RevenueCorner(bool end){if(recordEnergy){EnergyCorner(end);return;}if(busy||!recordRevenue)return;if(Native.GetForegroundWindow()!=target){Write("请在游戏前台标定营业额区域。");return;}Native.POINT p;Native.GetCursorPos(out p);var r=Native.Client(target);if(!r.Contains(p.X,p.Y))return;if(!end){revenueCorner=p;hasCorner=true;Write("已记录左上角，鼠标移至黄色数字右下角按 F10。");return;}if(!hasCorner||p.X<=revenueCorner.X+5||p.Y<=revenueCorner.Y+5){Write("请先按 F9 标定左上角，再在其右下角按 F10。");return;}cfg.RevenueX=(double)(revenueCorner.X-r.X)/r.Width;cfg.RevenueY=(double)(revenueCorner.Y-r.Y)/r.Height;cfg.RevenueW=(double)(p.X-revenueCorner.X)/r.Width;cfg.RevenueH=(double)(p.Y-revenueCorner.Y)/r.Height;cfg.RevenueRecorded=true;recordRevenue=false;hasCorner=false;Save();revenueStatus.Text="营业额区域已标定 · 阈值 1500";Write("黄色营业额区域已保存。");}
 public static bool ValidateMouseCoordinates(){return AbsoluteAxis(0,0,1920)==0&&AbsoluteAxis(1919,0,1920)==65535&&AbsoluteAxis(-1920,-1920,3840)==0&&AbsoluteAxis(1919,-1920,3840)==65535;}
 async Task SimulateEscape(CancellationToken ct){ct.ThrowIfCancellationRequested();RequireGameFocus();Native.keybd_event(0x1B,0,0,UIntPtr.Zero);try{await Task.Delay(100,ct);}finally{Native.keybd_event(0x1B,0,2,UIntPtr.Zero);}}
 static int AbsoluteAxis(int coordinate,int origin,int span){if(span<2)throw new Exception("桌面尺寸无效。");return (int)Math.Max(0,Math.Min(65535,Math.Round((coordinate-origin)*65535.0/(span-1))));}
 void MouseInput(int dx,int dy,uint flags){var input=new Native.INPUT{type=0,u=new Native.UNION{mi=new Native.MOUSEINPUT{dx=dx,dy=dy,dwFlags=flags}}};if(Native.SendInput(1,new[]{input},Marshal.SizeOf(typeof(Native.INPUT)))!=1)throw new Exception("鼠标输入被系统拒绝（错误码 "+Marshal.GetLastWin32Error()+"）。请检查助手与游戏是否以相同权限运行。");}
 void RequireGameFocus(){uint pid;Native.GetWindowThreadProcessId(target,out pid);if(!Native.IsWindow(target)||pid!=targetPid||Native.GetForegroundWindow()!=target)throw new Exception("游戏失去焦点或窗口已变化，停止模拟点击。");}
 async Task SimulateMouseClick(Step step,CancellationToken ct){ct.ThrowIfCancellationRequested();RequireGameFocus();var rect=Native.Client(target);var goal=Native.Position(rect,step);Native.POINT from;Native.GetCursorPos(out from);int left=Native.GetSystemMetrics(76),top=Native.GetSystemMetrics(77),width=Native.GetSystemMetrics(78),height=Native.GetSystemMetrics(79);Write("模拟鼠标：移动至“"+step.Name+"” · 游戏坐标 "+(goal.X-rect.X)+","+(goal.Y-rect.Y)+"，屏幕坐标 "+goal.X+","+goal.Y);for(int i=1;i<=12;i++){ct.ThrowIfCancellationRequested();RequireGameFocus();double t=(double)i/12;int x=(int)Math.Round(from.X+(goal.X-from.X)*t),y=(int)Math.Round(from.Y+(goal.Y-from.Y)*t);MouseInput(AbsoluteAxis(x,left,width),AbsoluteAxis(y,top,height),0xC001);await Task.Delay(20,ct);}await Task.Delay(180,ct);ct.ThrowIfCancellationRequested();RequireGameFocus();Native.POINT actual;Native.GetCursorPos(out actual);if(Math.Abs(actual.X-goal.X)>8||Math.Abs(actual.Y-goal.Y)>8)throw new Exception("鼠标未停留在目标位置（可能被游戏锁定或发生人工移动），未点击。实际 "+actual.X+","+actual.Y+"；目标 "+goal.X+","+goal.Y);bool down=false;try{MouseInput(0,0,2);down=true;Write("鼠标左键按下："+step.Name);await Task.Delay(100,ct);}finally{if(down){MouseInput(0,0,4);Write("鼠标左键松开："+step.Name);}}await Task.Delay(120,ct);}
 void EnergyCorner(bool end){if(busy||!recordEnergy)return;if(Native.GetForegroundWindow()!=target){Write("请在游戏前台标定能量区域。");return;}Native.POINT p;Native.GetCursorPos(out p);var r=Native.Client(target);if(!r.Contains(p.X,p.Y))return;if(!end){revenueCorner=p;hasCorner=true;Write("能量区域左上角已记录，移至当前值/500右下角按 F10。");return;}if(!hasCorner||p.X<=revenueCorner.X+5||p.Y<=revenueCorner.Y+5){Write("先用 F9 记录左上角，再用 F10 记录右下角。");return;}cfg.EnergyX=(double)(revenueCorner.X-r.X)/r.Width;cfg.EnergyY=(double)(revenueCorner.Y-r.Y)/r.Height;cfg.EnergyW=(double)(p.X-revenueCorner.X)/r.Width;cfg.EnergyH=(double)(p.Y-revenueCorner.Y)/r.Height;cfg.EnergyRecorded=true;recordEnergy=false;hasCorner=false;Save();energyStatus.Text="能量区域已标定 · 领取后 <18 停止，≥18 继续";Write("领取后能量识别区域已保存。");}
 async Task CheckEnergy(CancellationToken ct){var watch=Stopwatch.StartNew();int? previous=null;int hits=0;long nextDiagnostic=0;Write("领取步骤已完成，检查闪电能量；小于18停止全部步骤。");while(true){ct.ThrowIfCancellationRequested();uint pid;Native.GetWindowThreadProcessId(target,out pid);if(!Native.IsWindow(target)||pid!=targetPid)throw new Exception("游戏窗口或进程已变化。");if(Native.GetForegroundWindow()!=target||Native.IsIconic(target)){previous=null;hits=0;watch.Restart();await Task.Delay(350,ct);continue;}var r=Native.Client(target);var crop=new Rectangle(r.X+(int)(cfg.EnergyX*r.Width),r.Y+(int)(cfg.EnergyY*r.Height),(int)(cfg.EnergyW*r.Width),(int)(cfg.EnergyH*r.Height));if(crop.Width<6||crop.Height<6||!r.Contains(crop))throw new Exception("能量区域无效，请重新标定。");int? value;using(var b=new Bitmap(crop.Width,crop.Height)){using(var g=Graphics.FromImage(b))g.CopyFromScreen(crop.Location,Point.Empty,crop.Size);value=await EnergyReader.Read(b);}ct.ThrowIfCancellationRequested();if(Native.GetForegroundWindow()!=target){previous=null;hits=0;continue;}hits=value.HasValue?(value==previous?hits+1:1):0;previous=value;if(watch.ElapsedMilliseconds>=nextDiagnostic){Write("能量诊断："+(EnergyReader.LastReason??"无")+"；OCR="+(EnergyReader.LastText??"空").Replace(Environment.NewLine," | "));nextDiagnostic=watch.ElapsedMilliseconds+5000;}status.Text="领取后能量："+(value.HasValue?value.ToString():"识别中")+" / 500";if(hits>=2){Write("领取后闪电能量："+value.Value+"/500");if(EnergyReader.MustStop(value.Value))throw new EnergyDepletedException(value.Value);Write("能量不少于18，继续后续步骤。");return;}if(watch.Elapsed.TotalSeconds>Math.Min(cfg.Timeout,45))throw new Exception("领取后无法确认能量，已停止全部步骤。请检查区域或游戏界面。");await Task.Delay(650,ct);}}
 async Task WaitRevenue(CancellationToken ct){var gate=new RevenueGate();int? last=null;var watch=Stopwatch.StartNew();long nextDiagnostic=0;Write("等待黄色营业额达到1500；连续两次读到达标值才按 Esc。");while(true){ct.ThrowIfCancellationRequested();uint pid;Native.GetWindowThreadProcessId(target,out pid);if(!Native.IsWindow(target)||pid!=targetPid)throw new Exception("游戏窗口或进程已变化。");if(Native.GetForegroundWindow()!=target||Native.IsIconic(target)){gate.Reset();watch.Restart();await Task.Delay(350,ct);continue;}var r=Native.Client(target);var crop=new Rectangle(r.X+(int)(cfg.RevenueX*r.Width),r.Y+(int)(cfg.RevenueY*r.Height),(int)(cfg.RevenueW*r.Width),(int)(cfg.RevenueH*r.Height));if(crop.Width<6||crop.Height<6||!r.Contains(crop))throw new Exception("营业额区域无效，请重新标定。");int? value;using(var b=new Bitmap(crop.Width,crop.Height)){using(var g=Graphics.FromImage(b))g.CopyFromScreen(crop.Location,Point.Empty,crop.Size);value=await RevenueReader.Read(b);}ct.ThrowIfCancellationRequested();if(Native.GetForegroundWindow()!=target){gate.Reset();continue;}if(value!=last||!value.HasValue&&watch.ElapsedMilliseconds<1500){Write("当前黄色营业额："+(value.HasValue?value.ToString():"未识别"));last=value;}status.Text="黄色营业额 "+(value.HasValue?value.ToString():"识别中")+" / 1500";if(!value.HasValue&&watch.ElapsedMilliseconds>=nextDiagnostic){Write("营业额诊断：OCR原文="+(RevenueReader.LastText??"无")+"；"+(RevenueReader.LastReason??"未知")+"；识别区 "+crop.Width+"×"+crop.Height);nextDiagnostic=watch.ElapsedMilliseconds+5000;}if(gate.Observe(value))return;if(watch.Elapsed.TotalSeconds>cfg.Timeout)throw new Exception("等待营业额达标超时，已停止；不会按 Esc。");await Task.Delay(750,ct);}}
 static int[] ScaledPatch(Point p,Rectangle client,Step s){int oldW=s.CaptureW>0?s.CaptureW:1920,oldH=s.CaptureH>0?s.CaptureH:1080;int w=Math.Max(8,(int)Math.Round(24.0*client.Width/oldW)),h=Math.Max(6,(int)Math.Round(16.0*client.Height/oldH));using(var raw=new Bitmap(w,h)){using(var g=Graphics.FromImage(raw))g.CopyFromScreen(p.X-w/2,p.Y-h/2,0,0,raw.Size);using(var b=new Bitmap(24,16)){using(var g=Graphics.FromImage(b))g.DrawImage(raw,0,0,24,16);var a=new int[72];int k=0;for(int y=0;y<16;y+=4)for(int x=0;x<24;x+=4){Color c=b.GetPixel(x,y);a[k++]=c.R;a[k++]=c.G;a[k++]=c.B;}return a;}}}
 async Task WaitReady(Step s,CancellationToken ct){var watch=Stopwatch.StartNew();bool paused=false;int matches=0;long nextLog=0;Write("准备步骤："+s.Name+"；"+(cfg.Guard?"等待按钮画面匹配":"固定坐标模式"));while(true){ct.ThrowIfCancellationRequested();if(!Native.IsWindow(target))throw new Exception("游戏窗口已关闭。");uint pid;Native.GetWindowThreadProcessId(target,out pid);if(pid!=targetPid)throw new Exception("目标进程已变化。");bool active=Native.GetForegroundWindow()==target&&!Native.IsIconic(target);if(!active){status.Text="已暂停：请切回异环 · 等待“"+s.Name+"”";if(!paused){Write("游戏不在前台，暂停点击；切回游戏可继续，F8 停止。");paused=true;}watch.Restart();nextLog=0;matches=0;await Task.Delay(250,ct);continue;}if(paused){Write("已回到游戏，继续等待按钮。");paused=false;}var r=Native.Client(target);if(r.Width<100||r.Height<100)throw new Exception("游戏画面尺寸异常。");if(!cfg.Guard)return;var p=Native.Position(r,s);double diff=Difference(s.Patch,ScaledPatch(p,r,s));status.Text="等待“"+s.Name+"” · 画面差值 "+diff.ToString("F0")+" / 32";if(watch.ElapsedMilliseconds>=nextLog){Write("等待“"+s.Name+"”：画面差值 "+diff.ToString("F1")+"（需≤32），游戏 "+r.Width+"×"+r.Height+"，坐标 "+(p.X-r.X)+","+(p.Y-r.Y)+"。请确认游戏处于对应步骤；样本不符可重新标定。");nextLog=watch.ElapsedMilliseconds+5000;}matches=diff<=32?matches+1:0;if(matches>=2){Write("按钮画面已匹配："+s.Name);return;}if(watch.Elapsed.TotalSeconds>cfg.Timeout)throw new Exception("等待“"+s.Name+"”画面超时，已停止。请回到该步骤界面或重新标定；如需按固定坐标运行，可关闭画面匹配。");await Task.Delay(350,ct);}}

}
static class Program {
 [STAThread] static void Main(string[] args){if(args.Length>0&&args[0]=="--defaults-test"){var folder=AppDomain.CurrentDomain.BaseDirectory;var temp=Path.Combine(folder,"defaults-test-"+Guid.NewGuid().ToString("N")+".json");try{var defaults=MainForm.LoadConfiguration(temp);if(defaults.Rounds!=1100||defaults.Timeout!=180||!defaults.Guard||defaults.Steps.Count!=5||!defaults.EnergyRecorded||!defaults.RevenueRecorded||defaults.Steps[0].CaptureW!=1920)Environment.Exit(60);File.WriteAllText(temp,new JavaScriptSerializer().Serialize(new Settings{Rounds=7}));if(MainForm.LoadConfiguration(temp).Rounds!=7)Environment.Exit(61);File.WriteAllText(Path.Combine(folder,"defaults-smoke.txt"),"PASS: embedded 1920x1080 defaults, 1100 rounds, recorded points and regions, personal settings take priority\r\n");}finally{if(File.Exists(temp))File.Delete(temp);}return;}if(args.Length>0&&args[0]=="--fluent-smoke"){ModernShell.Smoke();return;}if(args.Length>2&&args[0]=="--fluent-preview"){ModernShell.Preview(args[1],args[2]);return;}if(args.Length>2&&args[0]=="--diagnostic-preview"){MainForm.PreviewMode=true;Native.SetProcessDPIAware();Application.EnableVisualStyles();var f=new MainForm();f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-10000,-10000);f.Shown+=async delegate{try{await f.RenderDiagnosticPreview(args[1],args[2]);}catch(Exception e){File.WriteAllText(args[2]+".error.txt",e.ToString());Environment.ExitCode=30;}finally{f.Close();}};Application.Run(f);return;}if(args.Length>1&&args[0]=="--layout-test"){Native.SetProcessDPIAware();Application.EnableVisualStyles();using(var form=new MainForm())using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);}return;}if(args.Length>1&&args[0]=="--energy-suite"){var report=new StringBuilder();if(EnergyReader.Parse("2bb/5OO")!=266||EnergyReader.Parse("266/500")!=266||EnergyReader.Parse("266/500 33/33")!=266||EnergyReader.Parse("999/500").HasValue)Environment.Exit(23);if(!EnergyReader.MustStop(0)||!EnergyReader.MustStop(5)||!EnergyReader.MustStop(6)||!EnergyReader.MustStop(17)||EnergyReader.MustStop(18)||EnergyReader.MustStop(464)||EnergyReader.Parse("33/33").HasValue||EnergyReader.Parse("5/500")!=5)Environment.Exit(20);report.AppendLine("PASS: 0, 5, 6, 17 stop; 18 and 464 continue; 33/33 ignored");using(var b=new Bitmap(args[1])){int? v=Task.Run(async delegate{return await EnergyReader.Read(b);}).GetAwaiter().GetResult();if(v!=464){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"energy-debug.txt"),EnergyReader.LastText??"null");Environment.Exit(21);}report.AppendLine("PASS: user screenshot 464/500, other reward 33/33 -> 464");}foreach(int n in new[]{0,5,6,17,18,248,266,284,464})using(var b=new Bitmap(270,65)){using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(35,35,35));using(var f=new Font("Arial",24,FontStyle.Bold))g.DrawString(n+"/500",f,Brushes.White,12,8);}int? v=Task.Run(async delegate{return await EnergyReader.Read(b);}).GetAwaiter().GetResult();if(v!=n)Environment.Exit(22);report.AppendLine("PASS: OCR "+n+"/500 -> "+v);}using(var faint=new Bitmap(270,65)){using(var g=Graphics.FromImage(faint)){g.Clear(Color.FromArgb(25,25,25));using(var f=new Font("Segoe UI",18,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(145,145,145)))g.DrawString("266/500",f,brush,12,8);}int? v=Task.Run(async delegate{return await EnergyReader.Read(faint);}).GetAwaiter().GetResult();if(v!=266||!EnergyReader.LastText.Contains("原图放大"))Environment.Exit(24);report.AppendLine("PASS: low-contrast 266/500 uses original-image fallback -> 266");}File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"energy-suite.txt"),report.ToString());return;}if(args.Length>1&&args[0]=="--ocr-suite"){var report=new StringBuilder();if(RevenueReader.Parse("1,035")!=1035||RevenueReader.Parse("1,500")!=1500||RevenueReader.Parse("1，500")!=1500||RevenueReader.Parse("1.500")!=1500||RevenueReader.Parse("1,50").HasValue||RevenueReader.Parse("970 / 1,500").HasValue)Environment.Exit(15);report.AppendLine("PASS: grouped thousands 1,035 / 1,500 / Chinese comma; malformed group and mixed current/target rejected");var testGate=new RevenueGate();if(testGate.Observe(1499)||testGate.Observe(1500)||testGate.Observe(null)||testGate.Observe(1500)||!testGate.Observe(1500))Environment.Exit(13);testGate.Reset();if(testGate.Observe(1700))Environment.Exit(14);report.AppendLine("PASS: threshold 1500, two consecutive hits, unreadable reset, pause reset");using(var screenshot=new Bitmap(args[1])){int? v=Task.Run(async delegate{return await RevenueReader.Read(screenshot);}).GetAwaiter().GetResult();if(v!=970)Environment.Exit(10);report.AppendLine("PASS: user screenshot yellow 970 / white 1500 -> 970");}foreach(string text in new[]{"1499","1500","1700","1,035","1,500","1,560","WHITE"}){using(var b=new Bitmap(400,80)){using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(30,30,30));using(var f=new Font("Arial",24,FontStyle.Bold|FontStyle.Italic)){g.DrawString(text=="WHITE"?"1500":text,f,text=="WHITE"?Brushes.White:Brushes.Gold,10,15);g.DrawString(" / 1500",f,Brushes.White,130,15);}}int? v=Task.Run(async delegate{return await RevenueReader.Read(b);}).GetAwaiter().GetResult();int expected;if(text=="WHITE"){if(v.HasValue)Environment.Exit(11);}else if(!int.TryParse(text.Replace(",",""),out expected)||v!=expected)Environment.Exit(12);report.AppendLine("PASS: yellow "+text+" / white 1500 -> "+(v.HasValue?v.ToString():"UNREADABLE"));}}File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ocr-suite.txt"),report.ToString());return;}if(args.Length>1&&args[0]=="--ocr-test"){using(var b=new Bitmap(args[1])){var v=Task.Run(async delegate{return await RevenueReader.Read(b);}).GetAwaiter().GetResult();File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ocr-test.txt"),v.HasValue?v.ToString():"UNREADABLE");Environment.Exit(v.HasValue?0:5);}return;}if(args.Length>0&&args[0]=="--self-test"){if(Marshal.SizeOf(typeof(Native.INPUT))!=40)Environment.Exit(40);if(!MainForm.ValidateMouseCoordinates())Environment.Exit(41);var s=new Step{X=.5,Y=.25};var p=Native.Position(new Rectangle(100,200,1921,1081),s);if(p.X!=1060||p.Y!=470)Environment.Exit(2);if(MainForm.Difference(new[]{1,2,3},new[]{1,2,3})!=0)Environment.Exit(3);var c=new Settings();c.Steps.Add(new Step{Name="测试",Wait=90,Recorded=true,X=.25,Y=.5,Patch=new int[72]});var j=new JavaScriptSerializer();var d=j.Deserialize<Settings>(j.Serialize(c));if(d.Steps[0].Patch.Length!=72||d.Steps[0].X!=.25||d.Rounds!=1)Environment.Exit(4);File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),"PASS: x64 mouse INPUT layout, absolute desktop coordinates, coordinate scaling, patch comparison, settings round-trip\r\n");return;}
  bool owned;using(var mutex=new Mutex(true,"NTEOwnerSelectionAssistant_Local",out owned)){if(!owned){MessageBox.Show("助手已经在运行。");return;}Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);ModernShell.Run();}
 }
}
}

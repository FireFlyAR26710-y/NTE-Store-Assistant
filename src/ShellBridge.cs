using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

namespace NTEAssistant {
public partial class MainForm {
 public Action BeforeShellStart;
 public Settings ShellSettings {get{return cfg;}}
 public string ShellStatus {get{return status.Text;}}
 public string ShellLog {get{return log.Text.Length>30000?log.Text.Substring(log.Text.Length-30000):log.Text;}}
 public bool ShellBusy {get{return busy||testing;}}
 public string ShellGame {get{return windows.SelectedItem==null?"未检测到游戏":windows.SelectedItem.ToString();}}
 public string ShellReport {get{return diagnosticText.Text;}}
 public string ShellTestStatus {get{return diagnosticStatus.Text;}}
 public string ShellRevenue {get{return revenueGroup.Text;}}
 public string ShellEnergy {get{return energyGroup.Text;}}
 public void ShellInitialize(){var h=Handle;for(int n=6;n<=10;n++)Native.RegisterHotKey(h,n,0x4000,(uint)(0x6F+n));}
 public void ShellRefresh(){RefreshWindows();}
 public void ShellApply(Settings settings,int selected){if(ShellBusy)return;cfg=settings;rounds.Value=Math.Max(1,Math.Min(9999,cfg.Rounds));timeout.Value=Math.Max(10,Math.Min(3600,cfg.Timeout));guard.Checked=cfg.Guard;FillGrid();if(selected>=0&&selected<grid.Rows.Count)grid.CurrentCell=grid.Rows[selected].Cells[0];ReadGrid();}
 public void ShellStart(){StartRun();}
 public void ShellStop(){StopRun();}
 public void ShellSave(){ReadGrid();Save();Write("参数已保存。");}
 public void ShellRecord(){ArmRecord();}
 public void ShellArea(bool energy){ArmModernArea(energy);}
 public async Task ShellClick(){await TestSelectedMouseClick();}
 public async Task ShellImport(string path,bool whole){await LoadDiagnosticImage(path,whole);}
 public async Task ShellAnalyze(bool revenue,bool energy,bool whole){wholeImage.Checked=whole;await AnalyzeDiagnostics(revenue,energy);}
 public async Task ShellCapture(){if(ShellBusy)throw new Exception("请先按F8停止运行。");if(!Bind()){RefreshWindows();if(!Bind())throw new Exception("未找到异环窗口。");}testing=true;try{Native.ShowWindowAsync(target,9);Native.SetForegroundWindow(target);await Task.Delay(900);if(Native.GetForegroundWindow()!=target)throw new Exception("未能切换到游戏，请先将游戏置前再试。");var r=Native.Client(target);if(r.Width<100||r.Height<100)throw new Exception("游戏画面尺寸异常。");var fresh=new Bitmap(r.Width,r.Height);try{using(var g=Graphics.FromImage(fresh))g.CopyFromScreen(r.Location,Point.Empty,r.Size);}catch{fresh.Dispose();throw;}if(diagnosticFrame!=null)diagnosticFrame.Dispose();diagnosticFrame=fresh;wholeImage.Checked=false;frameOrigin="游戏截图 · "+DateTime.Now.ToString("HH:mm:ss")+" · "+r.Width+"×"+r.Height;ShowDiagnosticFrame();}finally{testing=false;}}
 public byte[] ShellImage(int kind){var picture=kind==0?framePreview:kind==1?revenuePreview:energyPreview;if(picture.Image==null)return null;using(var ms=new MemoryStream()){picture.Image.Save(ms,ImageFormat.Png);return ms.ToArray();}}
 public void ShellDispose(){StopRun();for(int n=6;n<=10;n++)Native.UnregisterHotKey(Handle,n);Dispose();}
}
}

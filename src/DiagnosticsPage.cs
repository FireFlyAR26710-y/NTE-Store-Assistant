using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NTEAssistant {
public partial class MainForm {
 public static bool PreviewMode;
 public async Task RenderDiagnosticPreview(string path,string output){pages.SelectedIndex=1;await LoadDiagnosticImage(path,true);PerformLayout();Application.DoEvents();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(output,System.Drawing.Imaging.ImageFormat.Png);}File.WriteAllText(output+".txt",diagnosticText.Text);pages.SelectedIndex=0;PerformLayout();Application.DoEvents();using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(output+".run.png",System.Drawing.Imaging.ImageFormat.Png);}pages.SelectedIndex=1;}
 TabControl pages;
 PictureBox framePreview=new PictureBox(),revenuePreview=new PictureBox(),energyPreview=new PictureBox();
 TextBox diagnosticText=new TextBox();
 GroupBox revenueGroup,energyGroup;
 Label diagnosticStatus=new Label();
 CheckBox wholeImage=new CheckBox();
 Bitmap diagnosticFrame;
 bool testing;
 string frameOrigin;
 void BuildPages(){
  var existing=new Control[Controls.Count];Controls.CopyTo(existing,0);
  var test=BuildModernShell(existing);
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,77));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,105));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,57));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));test.Controls.Add(layout);
  var heading=new Label{Text="识别测试\r\n检查截图、标定区域和读数。模拟点击按钮会实际操作鼠标。",Dock=DockStyle.Fill,Font=UiFont(11),ForeColor=Color.FromArgb(29,53,80)};layout.Controls.Add(heading,0,0);
  var toolbar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};layout.Controls.Add(toolbar,0,1);
  TestButton(toolbar,"查找游戏",delegate{if(testing||busy)return;RefreshWindows();diagnosticStatus.Text=windows.Items.Count>0?"发现游戏："+windows.Items[0].ToString():"未找到游戏，请先打开异环。";});
  TestButton(toolbar,"截图并测试",async delegate{await CaptureDiagnostics();});
  TestButton(toolbar,"测试营业额",async delegate{await AnalyzeDiagnostics(true,false);});
  TestButton(toolbar,"测试能量",async delegate{await AnalyzeDiagnostics(false,true);});
  TestButton(toolbar,"导入截图",async delegate{if(testing||busy)return;using(var dialog=new OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.bmp"})if(dialog.ShowDialog(this)==DialogResult.OK)await LoadDiagnosticImage(dialog.FileName,false);});
  TestButton(toolbar,"模拟点击所选步骤",async delegate{await TestSelectedMouseClick();});
  wholeImage.Text="整图识别（用于裁剪图）";wholeImage.AutoSize=true;wholeImage.Margin=new Padding(8,12,0,0);toolbar.Controls.Add(wholeImage);wholeImage.CheckedChanged+=delegate{if(diagnosticFrame!=null)ShowDiagnosticFrame();};
  diagnosticStatus.Dock=DockStyle.Fill;diagnosticStatus.ForeColor=Color.FromArgb(75,95,115);diagnosticStatus.Text="尚未截图。先让游戏显示要检查的界面，再点“截图并测试”。";layout.Controls.Add(diagnosticStatus,0,2);
  var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));layout.Controls.Add(content,0,3);
  var shot=new GroupBox{Text="截图预览 · 绿色框：营业额 / 蓝色框：能量",Dock=DockStyle.Fill,Padding=new Padding(10)};SetupPicture(framePreview);shot.Controls.Add(framePreview);content.Controls.Add(shot,0,0);
  var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};right.RowStyles.Add(new RowStyle(SizeType.Absolute,105));right.RowStyles.Add(new RowStyle(SizeType.Absolute,105));right.RowStyles.Add(new RowStyle(SizeType.Percent,100));content.Controls.Add(right,1,0);
  var rg=new GroupBox{Text="营业额区域 · 只读黄色当前值",Dock=DockStyle.Fill};revenueGroup=rg;SetupPicture(revenuePreview);rg.Controls.Add(revenuePreview);right.Controls.Add(rg,0,0);
  var eg=new GroupBox{Text="能量区域 · 当前值 / 500",Dock=DockStyle.Fill};energyGroup=eg;SetupPicture(energyPreview);eg.Controls.Add(energyPreview);right.Controls.Add(eg,0,1);
  diagnosticText.Dock=DockStyle.Fill;diagnosticText.Multiline=true;diagnosticText.ReadOnly=true;diagnosticText.ScrollBars=ScrollBars.Vertical;diagnosticText.BackColor=Color.White;diagnosticText.Font=UiFont(9);diagnosticText.Text="识别结果会显示在这里。\r\n\r\n营业额 ≥1500：满足发送 Esc 条件。\r\n领取后能量 <18：停止全部步骤。\r\n\r\n测试只给出判断，不执行操作。";right.Controls.Add(diagnosticText,0,2);
  FormClosed+=delegate{if(diagnosticFrame!=null)diagnosticFrame.Dispose();DisposeImage(framePreview);DisposeImage(revenuePreview);DisposeImage(energyPreview);};
 }
 void TestButton(Control owner,string text,Action action){var b=UiButton(text,action,text=="截图并测试");b.Size=new Size(135,38);b.Margin=new Padding(0,0,8,6);owner.Controls.Add(b);}
 void SetupPicture(PictureBox p){p.Dock=DockStyle.Fill;p.SizeMode=PictureBoxSizeMode.Zoom;p.BackColor=Color.FromArgb(229,235,241);}
 static void DisposeImage(PictureBox p){var image=p.Image;p.Image=null;if(image!=null)image.Dispose();}
 static void SetImage(PictureBox p,Bitmap b){DisposeImage(p);p.Image=b;}
 Rectangle DiagnosticRegion(bool energy){return new Rectangle((int)((energy?cfg.EnergyX:cfg.RevenueX)*diagnosticFrame.Width),(int)((energy?cfg.EnergyY:cfg.RevenueY)*diagnosticFrame.Height),(int)((energy?cfg.EnergyW:cfg.RevenueW)*diagnosticFrame.Width),(int)((energy?cfg.EnergyH:cfg.RevenueH)*diagnosticFrame.Height));}
 bool ValidRegion(Rectangle r){return r.Width>=6&&r.Height>=6&&new Rectangle(Point.Empty,diagnosticFrame.Size).Contains(r);}
 void ShowDiagnosticFrame(){var marked=(Bitmap)diagnosticFrame.Clone();using(var g=Graphics.FromImage(marked)){foreach(bool energy in new[]{false,true}){if(wholeImage.Checked)continue;if(!(energy?cfg.EnergyRecorded:cfg.RevenueRecorded))continue;var r=DiagnosticRegion(energy);if(!ValidRegion(r))continue;using(var pen=new Pen(energy?Color.DodgerBlue:Color.LimeGreen,Math.Max(2,marked.Width/500)))g.DrawRectangle(pen,r);}}SetImage(framePreview,marked);}
 public async Task LoadDiagnosticImage(string path,bool entire){if(testing||busy)return;using(var loaded=new Bitmap(path)){if(diagnosticFrame!=null)diagnosticFrame.Dispose();diagnosticFrame=new Bitmap(loaded);}wholeImage.Checked=entire;frameOrigin="导入："+Path.GetFileName(path);ShowDiagnosticFrame();await AnalyzeDiagnostics(true,true);}
 async Task TestSelectedMouseClick(){if(testing||busy||PreviewMode)return;try{ReadGrid();if(!Bind())return;if(grid.CurrentRow==null)throw new Exception("请先到自动运行页选择一个普通点击步骤。");var step=cfg.Steps[grid.CurrentRow.Index];if(step.RevenueEsc||step.EnergyCheck||!step.Recorded)throw new Exception("请在自动运行页选中一个已标定的普通点击步骤。");testing=true;cancel=new System.Threading.CancellationTokenSource();var ct=cancel.Token;diagnosticText.Text="单次鼠标测试："+step.Name+"\r\n3秒后将实际移动鼠标并点击一次，F8可停止。\r\n此测试跳过画面匹配，只验证鼠标输入；请让游戏位于该步骤的界面。";Write("单次鼠标测试已请求："+step.Name+"，3秒后模拟移动并点击一次。");Native.ShowWindowAsync(target,9);Native.SetForegroundWindow(target);for(int n=3;n>0;n--){diagnosticStatus.Text="单次点击倒计时："+n+" · "+step.Name+" · F8停止";await Task.Delay(1000,ct);}await SimulateMouseClick(step,ct);diagnosticStatus.Text="已发送一次模拟鼠标点击："+step.Name+"；请观察游戏响应。";diagnosticText.AppendText("\r\n鼠标按下/松开已发送；请以游戏界面变化为准。");}catch(OperationCanceledException){diagnosticStatus.Text="单次鼠标测试已停止。";}catch(Exception e){diagnosticStatus.Text="单次鼠标测试失败。";diagnosticText.Text=e.Message;Write(e.Message);}finally{testing=false;if(cancel!=null){cancel.Dispose();cancel=null;}}}
 async Task CaptureDiagnostics(){
  if(testing||busy){diagnosticStatus.Text="请先停止自动运行，再进行识别测试。";return;}if(!Bind()){RefreshWindows();if(!Bind()){diagnosticStatus.Text="未找到游戏窗口。";return;}}
  testing=true;bool hidden=false;
  try{diagnosticStatus.Text="正在截取游戏画面…";Hide();hidden=true;Native.ShowWindowAsync(target,9);Native.SetForegroundWindow(target);await Task.Delay(850);if(Native.GetForegroundWindow()!=target)throw new Exception("无法将游戏切到前台，截图未执行；请先切到游戏，再重试。");var r=Native.Client(target);if(r.Width<100||r.Height<100)throw new Exception("游戏客户区尺寸异常。");var fresh=new Bitmap(r.Width,r.Height);try{using(var g=Graphics.FromImage(fresh))g.CopyFromScreen(r.Location,Point.Empty,r.Size);}catch{fresh.Dispose();throw;}if(diagnosticFrame!=null)diagnosticFrame.Dispose();diagnosticFrame=fresh;wholeImage.Checked=false;frameOrigin="游戏截图 "+DateTime.Now.ToString("HH:mm:ss")+" · "+r.Width+"×"+r.Height;ShowDiagnosticFrame();}
  catch(Exception e){diagnosticStatus.Text=e.Message;diagnosticText.Text="截图失败："+e.Message;return;}
  finally{if(hidden){Show();Activate();}testing=false;}
  await AnalyzeDiagnostics(true,true);
 }
 async Task AnalyzeDiagnostics(bool revenue,bool energy){
  if(testing||busy){diagnosticStatus.Text="正在运行或测试，请稍候。";return;}if(diagnosticFrame==null){diagnosticStatus.Text="请先截图或导入图片。";return;}testing=true;
  try{ReadGrid();diagnosticStatus.Text=frameOrigin+" · 正在识别…";var report=new StringBuilder();report.AppendLine(frameOrigin);report.AppendLine("模式："+(wholeImage.Checked?"整图识别（忽略标定框）":"使用保存的标定区域"));report.AppendLine("测试不会点击、按 Esc 或启动循环。");
   foreach(bool isEnergy in new[]{false,true}){if(isEnergy&&!energy||!isEnergy&&!revenue)continue;report.AppendLine();report.AppendLine(isEnergy?"【领取后闪电能量】":"【黄色营业额】");if(!wholeImage.Checked&&!(isEnergy?cfg.EnergyRecorded:cfg.RevenueRecorded)){report.AppendLine("未标定区域，请到自动运行页标定。");continue;}var r=wholeImage.Checked?new Rectangle(Point.Empty,diagnosticFrame.Size):DiagnosticRegion(isEnergy);if(!ValidRegion(r)){report.AppendLine("区域超出图片边界，请重新标定或使用整图识别。");continue;}using(var crop=diagnosticFrame.Clone(r,diagnosticFrame.PixelFormat)){SetImage(isEnergy?energyPreview:revenuePreview,(Bitmap)crop.Clone());int? value=isEnergy?await EnergyReader.Read(crop):await RevenueReader.Read(crop);if(isEnergy)energyGroup.Text="能量："+(value.HasValue?value+"/500 · "+(EnergyReader.MustStop(value.Value)?"停止全部步骤":"可以继续"):"未识别");else revenueGroup.Text="营业额："+(value.HasValue?value+" · "+(value.Value>=1500?"达标，可退出":"未达标，等待"):"未识别");report.AppendLine("区域："+r.X+","+r.Y+" / "+r.Width+"×"+r.Height);report.AppendLine("识别值："+(value.HasValue?value.ToString():"未识别"));if(isEnergy){report.AppendLine("OCR文本："+(EnergyReader.LastText??"无"));report.AppendLine("诊断："+(EnergyReader.LastReason??"无"));report.AppendLine(!value.HasValue?"判断：无法确认，运行时会等待并在超时后停止。":EnergyReader.MustStop(value.Value)?"判断：能量小于18，领取后停止全部步骤。":"判断：能量不少于18，领取后可以继续。");}else{report.AppendLine("OCR文本："+(RevenueReader.LastText??"无"));report.AppendLine("诊断："+(RevenueReader.LastReason??"无"));report.AppendLine(!value.HasValue?"判断：无法识别，继续等待，不发送 Esc。":value.Value>=1500?"判断：营业额达标；运行时连续确认两次后按 Esc。":"判断：营业额未达标，继续等待。");}}}
   diagnosticText.Text=report.ToString();diagnosticStatus.Text=frameOrigin+" · 测试完成（未执行任何游戏输入）";
  }catch(Exception e){diagnosticText.Text="测试失败："+e.Message;diagnosticStatus.Text="测试失败，详情见右侧。";}finally{testing=false;}
 }
}
}

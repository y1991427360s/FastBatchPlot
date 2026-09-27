using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private string _lastSinglePdfPath = "";
        private Button btnSinglePdf = null!;

        private async Task ShowSinglePdfOptions()
        {
            if (_isPlotting) return;
            using (var dialog = new Form { Text="单张 PDF", ClientSize=new Size(460,200), FormBorderStyle=FormBorderStyle.FixedDialog,
                StartPosition=FormStartPosition.CenterParent, Font=Font, MaximizeBox=false, MinimizeBox=false })
            {
                var range = new ComboBox { Left=20,Top=20,Width=420,DropDownStyle=ComboBoxStyle.DropDownList };
                range.Items.AddRange(new object[]{"列表中选中的一行（不按复选框）","两点确定打印范围","选择图形集合为一张图纸"});range.SelectedIndex=0;
                var save = new CheckBox { Left=20,Top=57,Width=420,Text="先选择保存位置",AutoSize=true };
                var note = new Label { Left=20,Top=88,Width=420,Height=48,Text="使用主窗的设备、样式、留白及 PDF 参数。默认存到临时文件夹并打开，随后可另存；不合并、不改批量列表。" };
                var ok = new Button { Left=230,Top=150,Width=100,Text="输出 PDF",DialogResult=DialogResult.OK };
                var cancel = new Button { Left=340,Top=150,Width=100,Text="取消",DialogResult=DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[]{range,save,note,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;
                if (dialog.ShowDialog(this)!=DialogResult.OK) return;
                string? destination=null;
                bool replaceExisting=false;
                if (save.Checked)
                {
                    using (var picker=new SaveFileDialog {Filter="PDF 文件 (*.pdf)|*.pdf",DefaultExt="pdf",AddExtension=true,FileName="图纸.pdf",OverwritePrompt=true})
                    {
                        if(picker.ShowDialog(this)!=DialogResult.OK)return;
                        replaceExisting=File.Exists(picker.FileName);
                        destination=picker.FileName;
                    }
                }
                try
                {
                    string? path=await ExecuteSinglePdf(range.SelectedIndex,replaceExisting?null:destination);
                    if(path==null)return;
                    if(replaceExisting)
                    {
                        try { SinglePdfFile.SaveCopy(path,destination!,true); path=destination!; _lastSinglePdfPath=path; }
                        catch(Exception ex) { ShowSinglePdfResult(path,"另存失败，原始成果保留："+ex.Message); return; }
                    }
                    ShowSinglePdfResult(path, null);
                }
                catch(Exception ex){lblStatus.Text="单张 PDF 未完成："+ex.Message;MessageBox.Show(this,ex.Message,"单张 PDF",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            }
        }

        // 可离线替换宿主验证；此方法只生成文件，打开查看器由交互入口负责。
        private async Task<string?> ExecuteSinglePdf(int rangeMode,string? destination)
        {
            if(_isPlotting)throw new InvalidOperationException("任务正在执行。");
            if(!CadHostProvider.IsInitialized)throw new InvalidOperationException("未连接到 CAD 宿主。");
            if(rangeMode<0||rangeMode>2)throw new ArgumentOutOfRangeException(nameof(rangeMode));
            dgvDrawings.EndEdit();
            CapturePlotSettings();
            var host=CadHostProvider.Host!;var plotter=CadHostProvider.Plotter!;
            bool wasVisible=Visible;
            SetPlottingState(true);
            try
            {
                PlotFrame frame;
                if(rangeMode==0)
                {
                    var selected=dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(r=>r.Tag).OfType<PlotFrame>().ToList();
                    if(selected.Count!=1)throw new InvalidOperationException("请选择且仅选择一行图纸。");
                    frame=selected[0];
                }
                else
                {
                    if(!(host is ICadFrameSelectionHost selection))throw new NotSupportedException("当前宿主不支持手工范围。");
                    Hide();
                    if(!selection.PromptManualFrame(rangeMode==1?ManualFrameSelectionMode.TwoCorners:ManualFrameSelectionMode.EntityGroup,out var picked)||picked==null)
                    {lblStatus.Text="已取消单张 PDF，批量列表保留。";return null;}
                    frame=picked;
                }
                EnsureFrameContextsAccessible(new[]{frame});
                var prepared=PrepareStampFrames(new[]{frame})[0];
                var config=new BatchPlotRun(new[]{new BatchPage(prepared,"")},_config).Config;
                config.ExportFormat=PlotExportFormat.PDF;config.SendToPrinter=false;config.Copies=1;config.MergeToSinglePdf=false;
                var plan=PlotPlanBuilder.Create(prepared,config);
                string path=destination==null?SinglePdfFile.CreateTemporaryPath():PlanOutputPath(Path.GetDirectoryName(Path.GetFullPath(destination))!,Path.GetFileName(destination),new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase),true);
                if(!string.Equals(Path.GetExtension(path),".pdf",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("目标必须是 PDF 文件。");
                config.OutputDirectory=Path.GetDirectoryName(path)!;config.MergedFileName="";
                var run=new BatchPlotRun(new[]{new BatchPage(prepared,path)},config);
                BeginPdfTaskRecord(run,"");
                Hide();
                // 单张也使用统一任务调度、历史和来源核验；原列表状态不被临时任务覆盖。
                await RunValidatedPlotPages(run,new[]{prepared},host,plotter,p=>PlotOutputCommitter.Validate(p,plan));
                var page=run.Pages[0];
                if(page.State!=BatchPageState.Succeeded)throw new InvalidOperationException(page.Error.Length==0?"单张 PDF 未生成。":page.Error);
                SinglePdfFile.Validate(path);
                _lastSinglePdfPath=path;
                lblStatus.Text="单张 PDF 已生成："+path+(string.IsNullOrWhiteSpace(page.Error)?"":"；警告："+page.Error);
                return path;
            }
            finally
            {
                EndTaskHistoryRecording();FinishPlotTask();SetPlottingState(false);
                if(wasVisible){Show();Activate();}
            }
        }

        /// <summary>测试可注入的外部打开委托，离线测试环境不实际调用外部进程。</summary>
        internal static Func<string, string>? SinglePdfViewerLauncher { get; set; }

        private static string TryOpenSinglePdfCore(string path)
        {
            try
            {
                if (SinglePdfViewerLauncher != null) return SinglePdfViewerLauncher(path);
                SinglePdfFile.Validate(path);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return "";
            }
            catch (Exception ex)
            {
                return "文件已保留，但打开查看器失败：" + ex.Message;
            }
        }

        private static Task<string> OpenSinglePdfAsync(string path)
            => Task.Run(() => TryOpenSinglePdfCore(path));

        private void ShowSinglePdfResult(string path, string? initialError)
        {
            using (var dialog = new Form
            {
                Text = "单张 PDF 已生成",
                ClientSize = new Size(580, 190),
                StartPosition = FormStartPosition.CenterParent,
                Font = Font,
                MinimizeBox = false,
                MaximizeBox = false
            })
            {
                var label = new TextBox
                {
                    Left = 16,
                    Top = 16,
                    Width = 548,
                    Height = 100,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Text = path + Environment.NewLine +
                           (string.IsNullOrEmpty(initialError) ? "已请求打开默认 PDF 查看器…" : initialError) +
                           Environment.NewLine + "临时文件可能被系统清理，请及时另存。"
                };
                var open = new Button { Left = 16, Top = 140, Width = 110, Text = "再次打开" };
                open.Click += async (s, e) =>
                {
                    open.Enabled = false;
                    try
                    {
                        string err = await OpenSinglePdfAsync(path);
                        if (!dialog.IsDisposed && !label.IsDisposed)
                        {
                            if (err.Length > 0) label.AppendText(Environment.NewLine + err);
                            else label.AppendText(Environment.NewLine + "已再次请求打开 PDF 查看器。");
                        }
                    }
                    finally
                    {
                        if (!dialog.IsDisposed && !open.IsDisposed) open.Enabled = true;
                    }
                };
                var save = new Button { Left = 138, Top = 140, Width = 110, Text = "另存为…" };
                save.Click += (s, e) =>
                {
                    using (var picker = new SaveFileDialog { Filter = "PDF 文件 (*.pdf)|*.pdf", DefaultExt = "pdf", AddExtension = true, FileName = Path.GetFileName(path), OverwritePrompt = true })
                    {
                        if (picker.ShowDialog(dialog) != DialogResult.OK) return;
                        try { SinglePdfFile.SaveCopy(path, picker.FileName, true); label.AppendText(Environment.NewLine + "已另存：" + picker.FileName); }
                        catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "另存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                    }
                };
                var close = new Button { Left = 454, Top = 140, Width = 110, Text = "关闭", DialogResult = DialogResult.OK };
                dialog.Controls.AddRange(new Control[] { label, open, save, close });
                dialog.AcceptButton = close;
                dialog.CancelButton = close;

                // 若没有明确提供错误，后台触发异步打开，完成时若有异常更新到窗口上
                if (initialError == null)
                {
                    _ = OpenSinglePdfAsync(path).ContinueWith(t =>
                    {
                        if (t.IsFaulted || !string.IsNullOrEmpty(t.Result))
                        {
                            string err = t.IsFaulted ? t.Exception?.InnerException?.Message ?? "启动失败" : t.Result;
                            try
                            {
                                if (!dialog.IsDisposed && dialog.IsHandleCreated)
                                    dialog.BeginInvoke((Action)(() => label.AppendText(Environment.NewLine + err)));
                            }
                            catch { }
                        }
                    });
                }

                dialog.ShowDialog(this);
            }
        }
    }
}

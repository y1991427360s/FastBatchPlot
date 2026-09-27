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

        private async Task YieldToUiMessageLoopAsync()
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (IsHandleCreated && !IsDisposed)
            {
                BeginInvoke((Action)(() => tcs.TrySetResult(true)));
            }
            else
            {
                tcs.TrySetResult(true);
            }
            await tcs.Task;
        }

        private async Task ShowSinglePdfOptions()
        {
            if (_isPlotting) return;
            int selectedRangeIndex;
            string? destination = null;
            bool replaceExisting = false;
            using (var dialog = new Form { Text="单张 PDF", ClientSize=new Size(460,200), FormBorderStyle=FormBorderStyle.FixedDialog,
                StartPosition=FormStartPosition.CenterParent, Font=Font, MaximizeBox=false, MinimizeBox=false })
            {
                var range = new ComboBox { Left=20,Top=20,Width=420,DropDownStyle=ComboBoxStyle.DropDownList };
                range.Items.AddRange(new object[]{"列表中选中的一行（不按复选框）","两点确定打印范围","选择图形集合为一张图纸"});range.SelectedIndex=0;
                var save = new CheckBox { Left=20,Top=57,Width=420,Text="先选择保存位置",AutoSize=true };
                var note = new Label { Left=20,Top=88,Width=420,Height=48,Text="使用主窗的设备、样式、留白及 PDF 参数。默认存到临时文件夹；不合并、不改批量列表。" };
                var ok = new Button { Left=230,Top=150,Width=100,Text="输出 PDF",DialogResult=DialogResult.OK };
                var cancel = new Button { Left=340,Top=150,Width=100,Text="取消",DialogResult=DialogResult.Cancel };
                dialog.Controls.AddRange(new Control[]{range,save,note,ok,cancel});dialog.AcceptButton=ok;dialog.CancelButton=cancel;
                if (dialog.ShowDialog(this)!=DialogResult.OK) return;
                selectedRangeIndex = range.SelectedIndex;
                if (save.Checked)
                {
                    using (var picker=new SaveFileDialog {Filter="PDF 文件 (*.pdf)|*.pdf",DefaultExt="pdf",AddExtension=true,FileName="图纸.pdf",OverwritePrompt=true})
                    {
                        if(picker.ShowDialog(this)!=DialogResult.OK)return;
                        replaceExisting=File.Exists(picker.FileName);
                        destination=picker.FileName;
                    }
                }
            }

            try
            {
                string? path=await ExecuteSinglePdf(selectedRangeIndex,replaceExisting?null:destination);
                if(path==null)return;
                if(replaceExisting)
                {
                    try
                    {
                        SinglePdfFile.SaveCopy(path,destination!,true);
                        path=destination!;
                        _lastSinglePdfPath=path;
                        lblStatus.Text="单张 PDF 已生成并另存为："+path;
                    }
                    catch(Exception ex)
                    {
                        lblStatus.Text="另存失败，原始临时文件已保留："+path+"；错误："+ex.Message;
                        MessageBox.Show(this,"另存失败，原始成果已保留："+ex.Message,"单张 PDF",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                        return;
                    }
                }
                if (btnOpenSinglePdf != null) btnOpenSinglePdf.Enabled = true;
                if (btnSaveSinglePdfAs != null) btnSaveSinglePdfAs.Enabled = true;
            }
            catch(Exception ex){lblStatus.Text="单张 PDF 未完成："+ex.Message;MessageBox.Show(this,ex.Message,"单张 PDF",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
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
            SinglePdfTrace.Write("ExecuteSinglePdf.Start", $"rangeMode={rangeMode}");
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
                    bool pickedOk = false;
                    try
                    {
                        pickedOk = selection.PromptManualFrame(rangeMode==1?ManualFrameSelectionMode.TwoCorners:ManualFrameSelectionMode.EntityGroup,out var picked) && picked!=null;
                        if(!pickedOk)
                        {
                            lblStatus.Text="已取消单张 PDF，批量列表保留。";
                            SinglePdfTrace.Write("ExecuteSinglePdf.Cancelled", "Manual selection cancelled");
                            return null;
                        }
                        frame=picked!;
                    }
                    finally
                    {
                        if(wasVisible){Show();BringToFront();}
                    }
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
                // 单张也使用统一任务调度、历史和来源核验；原列表状态不被临时任务覆盖。
                await RunValidatedPlotPages(run,new[]{prepared},host,plotter,p=>PlotOutputCommitter.Validate(p,plan));
                await YieldToUiMessageLoopAsync();
                var page=run.Pages[0];
                if(page.State!=BatchPageState.Succeeded)throw new InvalidOperationException(page.Error.Length==0?"单张 PDF 未生成。":page.Error);
                SinglePdfFile.Validate(path);
                _lastSinglePdfPath=path;
                if (btnOpenSinglePdf != null) btnOpenSinglePdf.Enabled = true;
                if (btnSaveSinglePdfAs != null) btnSaveSinglePdfAs.Enabled = true;
                lblStatus.Text="单张 PDF 已生成："+path+(string.IsNullOrWhiteSpace(page.Error)?"":"；警告："+page.Error);
                SinglePdfTrace.Write("ExecuteSinglePdf.Succeeded", $"Generated path={path}");
                return path;
            }
            finally
            {
                EndTaskHistoryRecording();FinishPlotTask();SetPlottingState(false);
                SinglePdfTrace.Write("ExecuteSinglePdf.Finally", "Cleaned up plotting state");
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
    }
}

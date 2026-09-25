using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private BatchPlotRun? _activeRun;
        private const string _baseTitle="FastBatchPlot - CAD批打印（PDF）";

        private void RefreshRowStatuses(IReadOnlyList<PlotFrame> frames)
        {
            var targets=new HashSet<PlotFrame>(frames);
            bool wasUpdating=_isUpdatingGrid;_isUpdatingGrid=true;
            try
            {
                foreach(DataGridViewRow row in dgvDrawings.Rows)
                {
                    if(!(row.Tag is PlotFrame frame)||!targets.Contains(frame))continue;
                    if(!Equals(row.Cells[8].Value,frame.Status))row.Cells[8].Value=frame.Status;
                    row.Cells[8].ToolTipText=!string.IsNullOrWhiteSpace(frame.ErrorMessage)?frame.ErrorMessage:frame.Status;
                    row.Cells[8].Style.ForeColor=StatusColor(frame.Status);
                }
            }
            finally{_isUpdatingGrid=wasUpdating;}
        }

        private static System.Drawing.Color StatusColor(string? status)
        {
            status??="";
            if(status.StartsWith("失败"))return System.Drawing.Color.Firebrick;
            if(status.StartsWith("文件已生成")||status.StartsWith("已提交设备"))return System.Drawing.Color.SeaGreen;
            return System.Drawing.Color.Empty;
        }

        // 让正在输出的行保持可见，便于大批次时观察进度。
        private void ScrollToFrame(PlotFrame frame)
        {
            foreach(DataGridViewRow row in dgvDrawings.Rows)
            {
                if(!ReferenceEquals(row.Tag,frame))continue;
                if(!row.Displayed)try{dgvDrawings.FirstDisplayedScrollingRowIndex=Math.Max(0,row.Index-3);}catch(InvalidOperationException){}
                return;
            }
        }
        private Button btnCancelPlot=null!;
        private static string FormatSuccessWarnings(IEnumerable<string> warnings)
        {
            var items=warnings.ToList();
            if(items.Count==0)return string.Empty;
            string summary=$"\n成功页面有 {items.Count} 条警告（成功状态和已有成果保留）：\n"+string.Join("\n",items.Take(3));
            if(items.Count>3)summary+=$"\n另有 {items.Count-3} 条警告，请查看页面列表或任务历史。";
            return summary;
        }
        private void RequestPlotCancellation()
        {
            if(_activeRun==null)return;
            _activeRun.RequestCancel();btnCancelPlot.Enabled=false;
            _mergeCancellation?.Cancel();
            lblStatus.Text=_mergeCancellation!=null
                ?"已收到取消合并请求；将在文件提交前取消，已提交结果和单页 PDF 保留。"
                :"已请求取消；当前页完成后停止，已完成文件保留。";
        }
        // WinForms Timer 保证每页在窗口线程执行，不把 CAD 调用放进线程池。
        private Task RunPlotPages(BatchPlotRun run,IReadOnlyList<PlotFrame> original,ICadHost host,ICadPlotter plotter)
            => RunValidatedPlotPages(run,original,host,plotter,null);

        private Task RunValidatedPlotPages(BatchPlotRun run,IReadOnlyList<PlotFrame> original,ICadHost host,ICadPlotter plotter,Action<string>? validateOutput)
        {
            if(_activeRun!=null)throw new InvalidOperationException("已有任务正在执行。");
            _activeRun=run;btnCancelPlot.Enabled=true;
            var completion=new TaskCompletionSource<bool>();var timer=new Timer{Interval=75};bool inTick=false;
            Action finish=()=>
            {
                timer.Stop();timer.Dispose();Text=_baseTitle;
                var config=run.Config;
                bool awaitingMerge=run.CanMerge && config.MergeToSinglePdf && !config.SendToPrinter && config.ExportFormat==PlotExportFormat.PDF;
                if(!awaitingMerge)FinishPlotTask();
            };
            timer.Tick+=(s,e)=>
            {
                if(inTick)return;inTick=true;
                try
                {
                    // 最后一页之后再交还一次消息循环，接收合并前取消。
                    if(run.IsFinished){finish();completion.SetResult(true);return;}
                    run.Step((page,config)=>
                    {
                        if(!ReferenceEquals(host,CadHostProvider.Host)||!ReferenceEquals(plotter,CadHostProvider.Plotter))
                            throw new InvalidOperationException("CAD 宿主已变化，未提交该页。");
                        var frame=page.Frame;
                        int done=run.Pages.Count(p=>p.State!=BatchPageState.Pending&&p.State!=BatchPageState.Running)+1;
                        Text=$"{_baseTitle} - 正在输出 {done}/{run.Pages.Count}";
                        lblStatus.Text=$"正在输出第 {done}/{run.Pages.Count} 张：{frame.TitleInfo.DrawingNo} {frame.TitleInfo.DrawingName}（本页完成后可停止）";lblStatus.Refresh();
                        int pageIndex=run.Pages.IndexOf(page);
                        if(pageIndex>=0&&pageIndex<original.Count)ScrollToFrame(original[pageIndex]);
                        EnsureFrameContextsAccessible(new[]{frame});
                        RecordPageStarting(page);
                        // 仅历史重试需与旧页混用，才逐页核验来源；新批次在同一会话连续输出，不做重复的全空间指纹。
                        var consistency=_retryingRecordedTask && !config.SendToPrinter && config.ExportFormat==PlotExportFormat.PDF
                            ? new CadPageConsistency(host,plotter,frame,config):null;
                        bool ok=DispatchPage(plotter,frame,config,page.OutputPath,out var error);
                        if(ok)
                        {
                            try {validateOutput?.Invoke(page.OutputPath);consistency?.Verify();}
                            catch(Exception ex) {return new BatchPageResult(false,ex.Message+(string.IsNullOrWhiteSpace(error)?"":"；输出接口警告："+error));}
                        }
                        return new BatchPageResult(ok,error);
                    });
                    RecordRunProgress(run);
                    bool printer=run.Config.SendToPrinter;
                    for(int i=0;i<run.Pages.Count;i++)
                    {
                        var page=run.Pages[i];original[i].ErrorMessage=page.Error;
                        switch(page.State)
                        {
                            case BatchPageState.Succeeded:
                                original[i].Status=(printer?"已提交设备":"文件已生成")+
                                    (string.IsNullOrWhiteSpace(page.Error)?string.Empty:"（警告："+page.Error.Trim()+"）");
                                break;
                            case BatchPageState.Failed:original[i].Status="失败："+page.Error;break;
                            case BatchPageState.Cancelled:original[i].Status="已取消（未提交）";break;
                            case BatchPageState.NotSubmitted:original[i].Status="未提交：前页失败";break;
                        }
                    }
                    // 只刷新本批次行的状态列；整表重建会让大批次闪烁、跳回顶部并重复计算命名。
                    RefreshRowStatuses(original);
                }
                catch(Exception ex){finish();FinishPlotTask();completion.SetException(ex);}
                finally{inTick=false;}
            };
            timer.Start();return completion.Task;
        }
    }
}

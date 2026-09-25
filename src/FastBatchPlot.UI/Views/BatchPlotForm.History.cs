using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private PdfTaskRecord? _recordingTask;
        private string? _recordingTaskPath;
        private bool _retryingRecordedTask;
        private FileStream? _taskHistoryLease;
        private string? TaskHistoryDirectory => _settingsPath == null ? null :
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_settingsPath))!, "tasks");

        private void BeginPdfTaskRecord(BatchPlotRun run, string mergedPath)
        {
            _recordingTask=null; _recordingTaskPath=null; _retryingRecordedTask=false;
            var config=run.Config;
            if (TaskHistoryDirectory==null || config.SendToPrinter || config.ExportFormat!=PlotExportFormat.PDF) return;
            var record=PdfTaskHistory.Create(run,mergedPath);
            if (CadHostProvider.Host is ICadRevisionHost revision)
                foreach (var frame in record.Pages.Select(p=>p.Frame).GroupBy(f=>f.SourceDocumentId).Select(g=>g.First()))
                    record.SourceRevisions.Add(frame.SourceDocumentId,revision.GetSourceRevision(frame));
            if(CadHostProvider.Plotter is ICadPlotResourceHost resources)record.PlotResourceRevision=resources.GetPlotResourceRevision(config);
            string path=Path.Combine(TaskHistoryDirectory,record.Id+".json");
            Directory.CreateDirectory(TaskHistoryDirectory);
            AcquireTaskHistoryLease(path);
            PdfTaskHistory.Save(path,record); // 记录不能落盘时尚未提交任何页面。
            _recordingTask=record; _recordingTaskPath=path;
        }

        private void AcquireTaskHistoryLease(string path)
        {
            if(_taskHistoryLease!=null)throw new InvalidOperationException("已有历史任务正在执行。");
            _taskHistoryLease=new FileStream(path+".runlock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        private void EndTaskHistoryRecording()
        {
            _recordingTask=null;_recordingTaskPath=null;_retryingRecordedTask=false;
            _taskHistoryLease?.Dispose();_taskHistoryLease=null;
        }

        private void SavePdfTaskRecord()
        {
            if (_recordingTask!=null && _recordingTaskPath!=null) PdfTaskHistory.Save(_recordingTaskPath,_recordingTask);
        }
        private void RecordPageStarting(BatchPage page)
        {
            if (_recordingTask==null) return;
            if (_retryingRecordedTask) RequireRecordedSource(_recordingTask);
            int index=_recordingTask.Pages.FindIndex(p=>string.Equals(p.OutputPath,page.OutputPath,StringComparison.OrdinalIgnoreCase));
            if(index<0)throw new InvalidOperationException("任务页不在原始历史记录中，未提交打印。");
            PdfTaskHistory.RecordPage(_recordingTask,index,BatchPageState.Running,"");
            SavePdfTaskRecord();
        }
        private void RecordRunProgress(BatchPlotRun run)
        {
            if(_recordingTask==null)return;
            foreach(var page in run.Pages)
            {
                if(page.State==BatchPageState.Pending || page.State==BatchPageState.Running)continue;
                int index=_recordingTask.Pages.FindIndex(p=>string.Equals(p.OutputPath,page.OutputPath,StringComparison.OrdinalIgnoreCase));
                if(index<0)throw new InvalidOperationException("任务页与历史记录不一致。");
                var saved=_recordingTask.Pages[index];
                if(saved.State==page.State && saved.Error==page.Error)continue;
                PdfTaskHistory.RecordPage(_recordingTask,index,page.State,page.Error);
            }
            SavePdfTaskRecord();
        }
        private static void RequireRecordedResources(PdfTaskRecord record)
        {
            if(CadHostProvider.Plotter is ICadPlotResourceHost resources)
            {
                if(string.IsNullOrEmpty(record.PlotResourceRevision)||!string.Equals(record.PlotResourceRevision,resources.GetPlotResourceRevision(record.Config),StringComparison.Ordinal))
                    throw new InvalidOperationException("打印样式或设备配置已变化，或旧历史缺少资源标记；请新建完整批次，不能混用旧页。");
            }
            else if(record.PlotResourceRevision!=null)throw new NotSupportedException("当前宿主无法核对原打印资源。");
        }
        private static void RequireRecordedSource(PdfTaskRecord record)
        {
            if(!(CadHostProvider.Host is ICadRevisionHost revision))
                throw new NotSupportedException("当前宿主不能核对原图修改记录，请重新搜索并新建完整批次。");
            RequireRecordedResources(record);
            var frames=record.Pages.Select(p=>p.Frame).ToList();
            EnsureFrameContextsAccessible(frames);
            foreach(var frame in frames.GroupBy(f=>f.SourceDocumentId).Select(g=>g.First()))
                if(!record.SourceRevisions.TryGetValue(frame.SourceDocumentId,out var saved) ||
                    !string.Equals(saved,revision.GetSourceRevision(frame),StringComparison.Ordinal))
                    throw new InvalidOperationException("原图会话或修改记录已变化，不能混用已完成旧页；请重新搜索并新建完整批次。");
        }

        private async Task<PdfMergeTaskResult> MergeRecordedPdfsAsync(BatchPlotRun run, IList<PdfMergeItem> items, string target)
        {
            if(_recordingTask==null)return await MergeBatchPdfsAsync(run,items,target);
            var record=_recordingTask;
            var locks=new List<FileStream>();
            try
            {
                // 保持读锁直到合并结束，校验通过的输入不能在导入期间被替换或改写。
                foreach(var page in record.Pages)locks.Add(new FileStream(page.OutputPath,FileMode.Open,FileAccess.Read,FileShare.Read));
                if(_activeRun!=null && !ReferenceEquals(_activeRun,run))throw new InvalidOperationException("批量任务已改变。");
                _activeRun=run;btnCancelPlot.Enabled=true;
                await Task.Run(()=>PdfTaskHistory.ValidateCompleted(record));
                if(Directory.Exists(target)||(!record.Config.OverwriteExisting&&File.Exists(target)))throw new IOException("合并目标已存在，拒绝覆盖："+target);
                record.MergeState=PdfTaskMergeState.Running;record.MergeError="";record.MergedSha256="";
                SavePdfTaskRecord();
                var result=await MergeBatchPdfsAsync(run,items,target);
                record.MergeState=result.Success?PdfTaskMergeState.Succeeded:result.Cancelled?PdfTaskMergeState.Cancelled:PdfTaskMergeState.Failed;
                record.MergeError=result.Error;
                if(result.Success)
                    using(var stream=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read))
                    using(var hash=SHA256.Create())record.MergedSha256=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");
                SavePdfTaskRecord();
                return result;
            }
            catch(Exception ex)
            {
                // 成功提交后日志失败时不把实际成果说成未生成；原错误仍向主流程呈现。
                if(record.MergeState!=PdfTaskMergeState.Succeeded)
                {
                    record.MergeState=PdfTaskMergeState.Failed;record.MergeError=ex.Message;record.MergedSha256="";
                    try{SavePdfTaskRecord();}catch(Exception save){throw new IOException(ex.Message+"；任务记录保存失败："+save.Message,ex);}
                }
                throw;
            }
            finally{foreach(var stream in locks)stream.Dispose();}
        }

        private async void ShowPdfTaskHistory()
        {
            if(_isPlotting)return;
            try
            {
                string? directory=TaskHistoryDirectory;
                var entries=new List<PdfTaskHistoryEntry>();
                if(directory!=null && Directory.Exists(directory))
                    foreach(var path in new DirectoryInfo(directory).EnumerateFiles("*.json").OrderByDescending(f=>f.LastWriteTimeUtc).Take(200))
                    {
                        try
                        {
                            var record=PdfTaskHistory.Load(path.FullName);
                            entries.Add(new PdfTaskHistoryEntry {Path=path.FullName,Name=Path.GetFileName(record.Config.OutputDirectory)+" / "+record.Id.Substring(0,8),
                                Created=new DateTime(record.CreatedUtcTicks,DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),Summary=DescribePdfTask(record)});
                        }
                        catch(Exception ex){entries.Add(new PdfTaskHistoryEntry{Path=path.FullName,Name=path.Name,Error=ex.Message});}
                    }
                using(var history=new PdfTaskHistoryForm(entries){Text="PDF任务历史（最近 200 条）"})
                {
                    if(history.ShowDialog(this)!=DialogResult.OK)return;
                    var record=PdfTaskHistory.Load(history.SelectedPath);
                    using(var details=new PdfTaskDetailsForm(record))
                    {
                        var action=details.ShowDialog(this);
                        if(action!=DialogResult.Retry && action!=DialogResult.Yes)return;
                        string summary=await ResumePdfTaskAsync(history.SelectedPath,action==DialogResult.Yes);
                        MessageBox.Show(this,summary,"PDF任务结果",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    }
                }
            }
            catch(Exception ex){lblStatus.Text="历史任务未执行完成："+ex.Message;MessageBox.Show(this,lblStatus.Text,"PDF任务",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        }
        private static string DescribePdfTask(PdfTaskRecord record)
        {
            int succeeded=record.Pages.Count(p=>p.State==BatchPageState.Succeeded);
            return $"单页成功 {succeeded}/{record.Pages.Count}，未完成 {record.Pages.Count-succeeded}；合并：{record.MergeState}";
        }
        private static string DescribePdfTaskWarnings(PdfTaskRecord record) => FormatSuccessWarnings(
            record.Pages.Where(p=>p.State==BatchPageState.Succeeded && !string.IsNullOrWhiteSpace(p.Error))
                .Select(p=>$"第 {p.Frame.OrderIndex:D2} 页：{p.Error.Trim()}"));

        private async Task<string> ResumePdfTaskAsync(string path,bool mergeOnly)
        {
            if(_isPlotting)throw new InvalidOperationException("已有任务正在执行。");
            SetPlottingState(true);
            try
            {
                AcquireTaskHistoryLease(path);
                var record=PdfTaskHistory.Load(path);
                if(record.MergeState==PdfTaskMergeState.Succeeded)throw new InvalidOperationException("合并成果已完成，本入口不覆盖已有成果。");
                var pending=await Task.Run(()=>PdfTaskHistory.PendingPages(record));
                if(mergeOnly && pending.Count>0)throw new InvalidOperationException("还有未完成页面，不能合并不完整批次。");
                if(!mergeOnly && pending.Count==0)throw new InvalidOperationException("没有需要重试的页面，请选择重新合并完整单页。");
                if(record.Config.MergeToSinglePdf && !record.Config.OverwriteExisting && (File.Exists(record.MergedOutputPath)||Directory.Exists(record.MergedOutputPath)))
                    throw new IOException("合并目标已有文件，未启动恢复；请核对成果，不能覆盖："+record.MergedOutputPath);
                if(record.Config.MergeToSinglePdf)
                    PdfBookmarkItems.Create(record.Pages.Select(p=>new BatchPage(p.Frame,p.OutputPath)), record.Config);
                if(!mergeOnly)
                {
                    if(!CadHostProvider.IsInitialized)throw new InvalidOperationException("未连接原 CAD 宿主，不能重试页面。");
                    RequireRecordedSource(record);
                    var plotter=CadHostProvider.Plotter!;
                    var config=record.Config;
                    if(!plotter.GetAvailablePlotters().Contains(config.PrinterDevice,StringComparer.OrdinalIgnoreCase)||
                        !plotter.GetAvailablePlotStyles().Contains(config.PlotStyleTable,StringComparer.OrdinalIgnoreCase))
                        throw new InvalidOperationException("原任务设备或打印样式已不可用，不能用当前其他设置替代；请新建批次。");
                }
                _recordingTask=record;_recordingTaskPath=path;_retryingRecordedTask=!mergeOnly;
                SavePdfTaskRecord(); // 先取得当前版本，过期任务记录不得启动输出。
                if(!mergeOnly)
                {
                    var run=new BatchPlotRun(pending,record.Config);
                    await RunPlotPages(run,pending.Select(p=>p.Frame).ToList(),CadHostProvider.Host!,CadHostProvider.Plotter!);
                    if(run.CancellationRequested || record.Pages.Any(p=>p.State!=BatchPageState.Succeeded))
                        return DescribePdfTask(record)+"。未生成合并文件；已完成单页保留，可在历史中查看失败原因。"+DescribePdfTaskWarnings(record);
                }
                string summary=DescribePdfTask(record);
                if(record.Config.MergeToSinglePdf)
                {
                    if(!mergeOnly)RequireRecordedSource(record);
                    // 重建完整原顺序，已成功页绝不再次送往 CAD。
                    FinishPlotTask();
                    var aggregate=new BatchPlotRun(record.Pages.Select(p=>new BatchPage(p.Frame,p.OutputPath)),record.Config);
                    while(!aggregate.IsFinished)aggregate.Step((page,config)=>new BatchPageResult(true));
                    var items=PdfBookmarkItems.Create(aggregate.Pages, aggregate.Config);
                    var result=await MergeRecordedPdfsAsync(aggregate,items,record.MergedOutputPath);
                    summary=result.Success?"完整批次已合并："+record.MergedOutputPath:result.Error;
                }
                return summary+DescribePdfTaskWarnings(record);
            }
            finally
            {
                EndTaskHistoryRecording();
                FinishPlotTask();SetPlottingState(false);
            }
        }
    }
}

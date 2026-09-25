using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private CancellationTokenSource? _mergeCancellation;
        private sealed class PdfMergeTaskResult
        {
            public bool Success { get; }
            public bool Cancelled { get; }
            public string Error { get; }
            public PdfMergeTaskResult(bool success, bool cancelled, string error)
            { Success = success; Cancelled = cancelled; Error = error; }
        }

        // 调用方保持 SetPlottingState(true)，直到本任务完成和结果展示后统一恢复控件。
        private async Task<PdfMergeTaskResult> MergeBatchPdfsAsync(BatchPlotRun run, IList<PdfMergeItem> items, string target)
        {
            if (_mergeCancellation != null) throw new InvalidOperationException("已有 PDF 合并任务正在执行。");
            if (!run.CanMerge) return new PdfMergeTaskResult(false, run.CancellationRequested, "批次未全部完成，未合并。");
            if (_activeRun != null && !ReferenceEquals(_activeRun, run)) throw new InvalidOperationException("批量任务已改变。");
            var snapshot = items.Select(item => new PdfMergeItem(item.FilePath, item.BookmarkTitle)).ToList();
            _activeRun = run;
            var cancellation = new CancellationTokenSource();
            _mergeCancellation = cancellation;
            btnCancelPlot.Enabled = true;
            lblStatus.Text = "正在合并 PDF，可取消；单页 PDF 保留。";
            var progress = new Progress<PdfMergeProgress>(value =>
            {
                // 忽略已结束任务延后送达的消息，避免覆盖最终结果。
                if (!ReferenceEquals(_mergeCancellation, cancellation) || cancellation.IsCancellationRequested) return;
                lblStatus.Text = value.ReadyToCommit
                    ? "合并内容已生成，正在完成文件提交。"
                    : $"正在合并 PDF：已处理 {value.CompletedPages} 页（{value.TotalFiles} 个文件），可取消。";
            });
            var throttledProgress = new MergeUiProgress(progress);
            try
            {
                return await Task.Run(() =>
                {
                    bool success = PdfMerger.MergePdfFiles(snapshot, target, cancellation.Token, out var error, overwrite: run.Config.OverwriteExisting, progress: throttledProgress);
                    return new PdfMergeTaskResult(success, false, error);
                });
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return new PdfMergeTaskResult(false, true, "PDF 合并已取消；合并文件未提交，单页 PDF 已保留。");
            }
            finally
            {
                _mergeCancellation = null;
                cancellation.Dispose();
                btnCancelPlot.Enabled = false;
            }
        }

        private void FinishPlotTask()
        {
            btnCancelPlot.Enabled = false;
            _activeRun = null;
        }

        // 大批页面不逐页向 UI 消息队列塞入回调，保留取消按钮的响应时间。
        private sealed class MergeUiProgress : IProgress<PdfMergeProgress>
        {
            private readonly IProgress<PdfMergeProgress> target;
            private readonly Stopwatch clock = Stopwatch.StartNew();
            private long lastReport = -100;
            public MergeUiProgress(IProgress<PdfMergeProgress> target) { this.target = target; }
            public void Report(PdfMergeProgress value)
            {
                long now = clock.ElapsedMilliseconds;
                if (!value.ReadyToCommit && now - lastReport < 100) return;
                lastReport = now; target.Report(value);
            }
        }
    }
}

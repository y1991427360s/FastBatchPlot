using System.Reflection;
using System.Threading;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.UI.Views;
using PdfSharpCore.Pdf;

internal static partial class Program
{
    private static void CheckPdfMergeUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PdfMergeUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "single.pdf"), output = Path.Combine(directory, "merged.pdf");
            using (var pdf = new PdfDocument()) { pdf.AddPage(); pdf.Save(input); }
            using var form = new BatchPlotForm(null);
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-32000, -32000);
            form.Show();
            var run = new BatchPlotRun(new[] { new BatchPage(new PlotFrame { OrderIndex = 8, TitleInfo = new TitleBlockInfo { DrawingName = "平面/详图" } }, input) }, new PlotConfig { MergeToSinglePdf = true, BookmarkTemplate = "首批/{Index:D3}:{DwgName}" });
            run.Step((page, config) => new BatchPageResult(true));
            Call(form, "SetPlottingState", true);
            var task = (System.Threading.Tasks.Task)Call(form, "MergeBatchPdfsAsync", run,
                PdfBookmarkItems.Create(run.Pages, run.Config), output)!;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(5); }
            if (!task.IsCompleted) throw new TimeoutException("离线 PDF 合并超时。");
            task.GetAwaiter().GetResult();
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            Check((bool)result.GetType().GetProperty("Success")!.GetValue(result)! && File.Exists(output) && File.Exists(input),
                "异步合并入口生成合并文件并保留单页 PDF");
            using (var merged = PdfSharpCore.Pdf.IO.PdfReader.Open(output, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.Import))
                Check(merged.Outlines[0].Title == "首批/008:平面/详图", "首次合并文件使用任务书签模板并保留中文及标点");
            Check(!Field<DataGridView>(form, "dgvDrawings").Enabled && ReferenceEquals(Field<BatchPlotRun>(form, "_activeRun"), run),
                "合并返回后保持批次与编辑锁，等待主流程统一收尾");
            Check(!Field<Button>(form, "btnCancelPlot").Enabled,
                "文件合并提交完毕后禁用取消，避免把完成结果误取消");
            Call(form, "FinishPlotTask");
            Call(form, "SetPlottingState", false);
            Check(typeof(BatchPlotForm).GetField("_activeRun", PrivateInstance)!.GetValue(form) == null &&
                Field<DataGridView>(form, "dgvDrawings").Enabled, "批次统一收尾释放活动任务并恢复列表编辑");

            // 独立验证按钮将取消信号交给文件工作线程，避免依赖大文件耗时制造竞争。
            var pending = new BatchPlotRun(new[] { new BatchPage(new PlotFrame(), input) }, new PlotConfig());
            using var cancellation = new CancellationTokenSource();
            typeof(BatchPlotForm).GetField("_activeRun", PrivateInstance)!.SetValue(form, pending);
            typeof(BatchPlotForm).GetField("_mergeCancellation", PrivateInstance)!.SetValue(form, cancellation);
            Field<Button>(form, "btnCancelPlot").Enabled = true;
            Call(form, "RequestPlotCancellation");
            Check(pending.CancellationRequested && cancellation.IsCancellationRequested && !Field<Button>(form, "btnCancelPlot").Enabled,
                "合并取消按钮同时记录任务取消并发送线程安全取消信号");
            Check(Field<Label>(form, "lblStatus").Text.Contains("已提交结果") && File.Exists(output),
                "取消提示准确保留已经提交的合并成果");
            typeof(BatchPlotForm).GetField("_mergeCancellation", PrivateInstance)!.SetValue(form, null);
            Call(form, "FinishPlotTask");
        }
        finally { Directory.Delete(directory, true); }
    }
}

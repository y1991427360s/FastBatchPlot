using System.Drawing;
using System.Drawing.Imaging;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckPdfTaskDetailsUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PdfTaskDetailsUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            PdfTaskRecord CreateRecord(bool mergeEnabled = true)
            {
                var config = new PlotConfig { PrinterDevice = "原任务设备.pc3", PlotStyleTable = "原任务样式.ctb", OutputDirectory = directory,
                    ExportFormat = PlotExportFormat.PDF, MergeToSinglePdf = mergeEnabled, MergedFileName = "合并.pdf" };
                var frame = new PlotFrame { OrderIndex = 7, MinX = 0, MinY = 0, MaxX = 841, MaxY = 594, CalculatedScale = 1,
                    SourceDocumentId = "offline-document", SourceLayoutId = "offline-layout", TitleInfo = new TitleBlockInfo { DrawingNo = "电施-07", DrawingName = "照明平面" } };
                return PdfTaskHistory.Create(new BatchPlotRun(new[] { new BatchPage(frame, Path.Combine(directory, "page.pdf")) }, config),
                    mergeEnabled ? Path.Combine(directory, "合并.pdf") : "");
            }
            var failed = CreateRecord(); failed.Pages[0].State = BatchPageState.Failed; failed.Pages[0].Error = "测试设备纸张匹配失败";
            failed.MergeState = PdfTaskMergeState.Failed; failed.MergeError = "单页未齐，不合并";
            using (var form = new PdfTaskDetailsForm(failed))
            {
                ShowPdfHistoryOffscreen(form); form.Size = form.MinimumSize; form.PerformLayout();
                var grid = Field<DataGridView>(form, "pages");
                Check(grid.ReadOnly && !grid.MultiSelect && grid.Rows.Count == 1 && grid.Columns.Count == 7,
                    "PDF任务详情提供只读逐页七列信息，不允许修改任务");
                Check(Convert.ToInt32(grid.Rows[0].Cells[0].Value) == 7 && Convert.ToString(grid.Rows[0].Cells[1].Value) == "电施-07" &&
                    Convert.ToString(grid.Rows[0].Cells[2].Value) == "照明平面" && Convert.ToString(grid.Rows[0].Cells[3].Value) == "A1 / 1:1",
                    "PDF任务详情按原快照显示序号、图号、图名及纸张比例");
                Check(Convert.ToString(grid.Rows[0].Cells[4].Value) == "失败" &&
                    Field<TextBox>(form, "pageDetails").Text.Contains(failed.Pages[0].Error) && Field<TextBox>(form, "pageDetails").Text.Contains(failed.Pages[0].OutputPath),
                    "失败页显示状态、完整错误和单页输出路径");
                string summary = Field<TextBox>(form, "summary").Text;
                Check(summary.Contains("原任务设备.pc3") && summary.Contains("原任务样式.ctb") && summary.Contains(directory) &&
                    summary.Contains("创建") && summary.Contains("更新") && summary.Contains("合并错误"),
                    "任务详情显示原设备样式、输出目录、记录时间及合并错误");
                Check(Field<Button>(form, "retry").Enabled && !Field<Button>(form, "merge").Enabled && form.AcceptButton == null,
                    "存在未完成页仅允许明确点击重试，Enter不绑定输出动作");
                string guidance = Field<Label>(form, "instructions").Text;
                Check(guidance.Contains("原任务设置") && guidance.Contains("原图会话") && guidance.Contains("外参、字体、打印样式") && guidance.Contains("不会覆盖"),
                    "详情直接说明原设置复用、源会话限制、外部文件盲区和不覆盖规则");
                foreach (string field in new[] { "summary", "pages", "pageDetails", "instructions", "retry", "merge", "close" })
                {
                    var control = Field<Control>(form, field);
                    Check(control.Visible && control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds),
                        "PDF详情最小窗口控件完整可见：" + field);
                }
                string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
                Directory.CreateDirectory(evidence);
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(Path.Combine(evidence, "ui-pdf-task-details.png"), ImageFormat.Png);
                }
                typeof(PdfTaskDetailsForm).GetMethod("MergeComplete", PrivateInstance)!.Invoke(form, null);
                Check(form.DialogResult != DialogResult.Yes, "未完成任务即使直接触发合并也不返回允许");
                typeof(PdfTaskDetailsForm).GetMethod("RetryIncomplete", PrivateInstance)!.Invoke(form, null);
                Check(form.DialogResult == DialogResult.Retry && failed.Pages[0].State == BatchPageState.Failed,
                    "重试按钮只返回请求，不自动改任务状态或打印");
            }
            var complete = CreateRecord(); complete.Pages[0].State = BatchPageState.Succeeded;
            using (var form = new PdfTaskDetailsForm(complete))
            {
                Check(!Field<Button>(form, "retry").Enabled && Field<Button>(form, "merge").Enabled,
                    "全部单页成功且原配置启用合并时只允许合并");
                typeof(PdfTaskDetailsForm).GetMethod("RetryIncomplete", PrivateInstance)!.Invoke(form, null);
                Check(form.DialogResult != DialogResult.Retry, "全部成功任务不能直接触发重试");
                typeof(PdfTaskDetailsForm).GetMethod("MergeComplete", PrivateInstance)!.Invoke(form, null);
                Check(form.DialogResult == DialogResult.Yes && complete.MergeState == PdfTaskMergeState.Pending,
                    "合并按钮只返回明确请求，不检查文件、不改变任务状态");
            }
            complete.MergeState = PdfTaskMergeState.Succeeded;
            using (var alreadyMerged = new PdfTaskDetailsForm(complete))
            {
                Check(!Field<Button>(alreadyMerged, "merge").Enabled && !Field<Button>(alreadyMerged, "retry").Enabled,
                    "已合并成功任务禁止重复合并与重试，避免覆盖");
                typeof(PdfTaskDetailsForm).GetMethod("MergeComplete", PrivateInstance)!.Invoke(alreadyMerged, null);
                Check(alreadyMerged.DialogResult != DialogResult.Yes, "已合并成功任务不能绕过禁用按钮请求覆盖");
            }
            var noMerge = CreateRecord(false); noMerge.Pages[0].State = BatchPageState.Succeeded;
            using (var form = new PdfTaskDetailsForm(noMerge))
                Check(!Field<Button>(form, "merge").Enabled, "原任务未启用合并时详情不扩展任务范围");
            var noTarget = CreateRecord(); noTarget.Pages[0].State = BatchPageState.Succeeded; noTarget.MergedOutputPath = "";
            using (var form = new PdfTaskDetailsForm(noTarget))
                Check(!Field<Button>(form, "merge").Enabled, "缺少原合并目标路径时不允许合并");
            foreach (var state in new[] { BatchPageState.Pending, BatchPageState.Running, BatchPageState.Cancelled, BatchPageState.NotSubmitted })
            {
                var record = CreateRecord(); record.Pages[0].State = state;
                using var form = new PdfTaskDetailsForm(record);
                Check(Field<Button>(form, "retry").Enabled && !Field<Button>(form, "merge").Enabled, "未完成状态只允许重试请求：" + state);
            }
            using (var form = new PdfTaskDetailsForm(failed))
            {
                ShowPdfHistoryOffscreen(form); Field<Button>(form, "close").PerformClick();
                Check(form.DialogResult == DialogResult.Cancel, "详情关闭返回取消，不自动重试或合并");
            }
            Check(Directory.GetFiles(directory).Length == 0, "任务详情操作不创建历史记录或任何PDF输出");
        }
        finally { Directory.Delete(directory); }
    }
}

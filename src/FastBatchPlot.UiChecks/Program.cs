using System.ComponentModel;
using System.Reflection;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static int _checks;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, PrivateInstance)!.GetValue(target)!;
    private static object? Call(object? target, string name, params object[] args) => typeof(BatchPlotForm)
        .GetMethod(name, BindingFlags.NonPublic | (target == null ? BindingFlags.Static : BindingFlags.Instance))!.Invoke(target, args);
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        Console.WriteLine("通过：" + message);
    }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { Check(true, message); return; }
        throw new Exception("未拒绝：" + message);
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            using var form = new BatchPlotForm(null);
            // 构造离线表单，不注册宿主，不调用 CAD；在屏幕外布局供渲染检查。
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-32000, -32000);
            form.ShowInTaskbar = false;
            form.Show();
            form.CreateControl();
            var frames = Field<List<PlotFrame>>(form, "_frames");
            var grid = Field<DataGridView>(form, "dgvDrawings");
            _ = form.Handle;
            _ = grid.Handle;
            var a = new PlotFrame { OrderIndex = 1, MinX = 0, MaxX = 841, MaxY = 594, TitleInfo = new TitleBlockInfo { DrawingNo = "B", DrawingName = "甲" } };
            var b = new PlotFrame { OrderIndex = 2, MinX = 1000, MaxX = 1841, MaxY = 594, TitleInfo = new TitleBlockInfo { DrawingNo = "A", DrawingName = "乙" } };
            frames.AddRange(new[] { a, b });
            Call(form, "RefreshGrid");
            grid.Sort(grid.Columns[2], ListSortDirection.Ascending);
            Check(ReferenceEquals(grid.Rows[0].Tag, b), "表头排序保留行与对象绑定");
            grid.Rows[0].Cells[0].Value = false;
            Check(!b.IsSelected && a.IsSelected, "排序后勾选作用于显示对象");
            grid.Rows[0].Cells[2].Value = "已修改";
            Check(b.TitleInfo.DrawingNo == "已修改" && a.TitleInfo.DrawingNo == "B", "排序后编辑作用于显示对象");
            grid.Rows[0].Cells[7].Value = "人工文件名";
            Call(form, "RefreshGrid");
            Call(form, "ReorderFrames");
            Check(b.CustomOutputFileName == "人工文件名", "手工文件名经刷新与排序保留");
            Check((bool)Call(null, "TrySetPaper", b, "A3")!, "标准纸张可完整切换");
            Check(b.DetectedPaper.WidthMm == 420 && b.DetectedPaper.HeightMm == 297, "纸张尺寸同步更新");
            Check(!ReferenceEquals(b.DetectedPaper, PaperSize.StandardSizes.First(p => p.Name == "A3")), "纸张对象不共享全局可变引用");
            Check(!(bool)Call(null, "TrySetPaper", b, "错误图幅")!, "未知图幅不伪装有效纸张");
            grid.ClearSelection();
            var bRow = grid.Rows.Cast<DataGridViewRow>().Single(r => ReferenceEquals(r.Tag, b));
            bRow.Selected = true;
            Check(grid.SelectedRows.Count == 1 && ReferenceEquals(grid.SelectedRows[0].Tag, b), "离线行选择定位到目标对象");
            Call(form, "RemoveSelectedRows");
            Check(frames.Count == 1 && ReferenceEquals(frames[0], a), "排序后的删除作用于指定对象");
            Call(form, "ReorderFrames");
            Check(frames.Count == 1 && ReferenceEquals(frames[0], a), "单图列表重新排序不丢失");

            var blocks = new List<RawBlockCandidate>
            {
                new() { BlockName = "A0框", Bounds = new Rect2D(0, 0, 1189, 841) },
                new() { BlockName = "A4框", Bounds = new Rect2D(2000, 0, 2297, 210) }
            };
            var polylines = new List<RawPolylineCandidate> { new() { Vertices = new() { new(3000, 0), new(3420, 0), new(3420, 297), new(3000, 297) } } };
            var mixed = (List<PlotFrame>)Call(null, "CombineAndFilterFrames", polylines, blocks, "")!;
            Check(mixed.Count == 3, "混合块名、多段线及小图幅联合保留");
            var missing = (List<PlotFrame>)Call(null, "CombineAndFilterFrames", polylines, blocks, "不存在")!;
            Check(missing.Count == 0, "指定块无匹配时严格为空");

            string temp = Path.Combine(Path.GetTempPath(), "FastBatchPlot.UiChecks-" + Guid.NewGuid().ToString("N"));
            var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string plannedFile = (string)Call(null, "PlanOutputPath", temp, "图纸.pdf", planned, false)!;
            Check(Path.GetDirectoryName(plannedFile) == temp, "输出限定在指定目录");
            Reject(() => Call(null, "PlanOutputPath", temp, "图纸.pdf", planned, true), "拒绝重复输出名");
            Reject(() => Call(null, "PlanOutputPath", temp, "..\\逃逸.pdf", planned, true), "拒绝路径逃逸");
            Reject(() => Call(null, "PlanOutputPath", temp, "CON.pdf", planned, true), "拒绝 Windows 保留名称");
            Directory.CreateDirectory(temp);
            File.WriteAllText(Path.Combine(temp, "已有.pdf"), "existing");
            try
            {
                Reject(() => Call(null, "PlanOutputPath", temp, "已有.pdf", planned, false), "未勾选覆盖时拒绝覆盖既有成果");
                string replaced = (string)Call(null, "PlanOutputPath", temp, "已有.pdf", new HashSet<string>(StringComparer.OrdinalIgnoreCase), true)!;
                Check(replaced == Path.Combine(temp, "已有.pdf"), "勾选覆盖时允许复用同名成果路径");
                using (new FileStream(Path.Combine(temp, "已有.pdf"), FileMode.Open, FileAccess.Read, FileShare.Read))
                    Reject(() => Call(null, "PlanOutputPath", temp, "已有.pdf", new HashSet<string>(StringComparer.OrdinalIgnoreCase), true), "同名文件被阅读器占用时在出图前报告");
            }
            finally { File.Delete(Path.Combine(temp, "已有.pdf")); Directory.Delete(temp); }

            Call(form, "SetPlottingState", true);
            Check(!grid.Enabled && !Field<Button>(form, "btnStartPlot").Enabled, "打印时禁止修改任务与重复启动");
            var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
            typeof(Form).GetMethod("OnFormClosing", PrivateInstance)!.Invoke(form, new object[] { closing });
            Check(closing.Cancel, "打印时阻止关闭窗口");
            Call(form, "SetPlottingState", false);
            Check(grid.Enabled && Field<Button>(form, "btnStartPlot").Enabled, "任务结束恢复交互");
            CheckFrameSelectionUi();
            CheckPreferencesUi();
            CheckTemplatesUi();
            CheckOutputUi();
            CheckCatalogUi();
            CheckListEditsUi();
            CheckLayoutUi();
            CheckSplitUi();
            CheckMarginsUi();
            CheckTitleWriteUi();
            CheckTaskRunUi();
            CheckPreviewUi();
            CheckSinglePdfUi();
            CheckPageConsistency();
            CheckPdfScopeUi();
            CheckPdfHistoryFormUi();
            CheckPdfTaskDetailsUi();
            CheckPdfTaskRecoveryUi();
            CheckPdfMergeUi();
            CheckBookmarkEditor();
            CheckPdfParameters();
            CheckCustomPaper();
            CheckPaperColumns();
            CheckSourceNaming();
            CheckTemplateOutputUi();
            CheckCropUi();
            CheckTemplateDetectionUi();
            form.Size = form.MinimumSize;
            form.PerformLayout();
            foreach (string field in new[] { "btnStartPlot", "txtMergedFileName", "lblStatus", "lblStats", "numDetectionScale", "btnDetectionReport", "btnSinglePdf", "chkRemoveDuplicates", "chkRemoveNestedFrames" })
            {
                var control = Field<Control>(form, field);
                Check(control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds), "最小窗口控件边界有效：" + field);
            }
            string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
            Directory.CreateDirectory(evidence);
            using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height));
                bitmap.Save(Path.Combine(evidence, "ui-minimum-window.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
            Console.WriteLine($"离线 UI 回归通过，共 {_checks} 项；没有连接或操作 CAD。");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}








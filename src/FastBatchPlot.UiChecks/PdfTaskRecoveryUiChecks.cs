using System.Security.Cryptography;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.UI.Views;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

internal static partial class Program
{
    private static void CheckPdfTaskRecoveryUi()
    {
        var originalHost = CadHostProvider.Host;
        var originalPlotter = CadHostProvider.Plotter;
        try
        {
            CheckInitialPdfTaskRecording();
            using (var scenario = new RecoveryScenario())
            {
                var host = new RecoveryHost(); var plotter = new RecoveryPlotter { SuccessWarning = "临时纸张配置清理失败：新页残留配置" };
                PdfTaskHistory.RecordPage(scenario.Record, 0, BatchPageState.Succeeded, "临时纸张配置清理失败：原页残留配置");
                PdfTaskHistory.Save(scenario.HistoryPath, scenario.Record);
                CadHostProvider.Host = host; CadHostProvider.Plotter = plotter;
                string firstHash = RecoveryHash(scenario.Record.Pages[0].OutputPath);
                using var form = RecoveryForm(scenario);
                var changedConfig = Field<PlotConfig>(form, "_config");
                changedConfig.PrinterDevice = "changed.pc3"; changedConfig.PlotStyleTable = "changed.ctb";
                changedConfig.MarginMm = 40; changedConfig.BookmarkTemplate = "当前模板不得使用";
                Field<NumericUpDown>(form, "numMargin").Value = 40;
                Field<TextBox>(form, "txtOutputFolder").Text = Path.Combine(scenario.DirectoryPath, "different-output");
                string summary = RecoveryResume(form, scenario.HistoryPath, false);
                var saved = PdfTaskHistory.Load(scenario.HistoryPath);
                Check(plotter.Orders.SequenceEqual(new[] { 2, 3 }), "恢复任务只向假宿主提交失败和取消页，不重复输出成功页");
                Check(plotter.Settings.All(s => s == "offline.pc3|monochrome.ctb|2.5"),
                    "重试使用原任务设备、样式和留白，当前主窗设置不能替代任务快照");
                Check(RecoveryHash(scenario.Record.Pages[0].OutputPath) == firstHash,
                    "重试及合并前后原成功单页内容摘要不变");
                Check(saved.Pages.All(p => p.State == BatchPageState.Succeeded) && saved.MergeState == PdfTaskMergeState.Succeeded &&
                    saved.MergedSha256 == RecoveryHash(saved.MergedOutputPath), "恢复结果持久化全部成功页和真实合并文件摘要");
                Check(saved.Pages[0].Error.Contains("原页残留配置") && saved.Pages.Skip(1).All(p => p.Error == plotter.SuccessWarning) &&
                    summary.Contains("成功页面有 3 条警告") && summary.Contains("第 01 页：临时纸张配置清理失败：原页残留配置") &&
                    summary.Contains("第 03 页：" + plotter.SuccessWarning),
                    "恢复摘要和历史同时保留原成功页及新成功页警告，不把已生成页面变为失败");
                using (var merged = PdfReader.Open(saved.MergedOutputPath, PdfDocumentOpenMode.Import))
                {
                    Check(merged.PageCount == 3 && Enumerable.Range(0, 3).All(i =>
                        Math.Abs(merged.Pages[i].Width.Millimeter - PlotPlanBuilder.Create(saved.Pages[i].Frame, saved.Config).PaperWidthMm) < 0.01),
                        "重合并包含原成功页与新页，保持完整原始顺序和各页毫米尺寸");
                    Check(merged.Outlines.Count == 3 && merged.Outlines[0].Title == "原任务/001:图纸1 1:1" && merged.Outlines[2].Title == "原任务/003:图纸3 1:1",
                        "恢复后的完整合并书签保持原图纸顺序");
                }
                Check(summary.Contains("完整批次已合并") && Field<DataGridView>(form, "dgvDrawings").Enabled &&
                    !Field<Button>(form, "btnCancelPlot").Enabled, "恢复完成摘要与成果一致并恢复列表控件");
            }

            using (var scenario = new RecoveryScenario())
            {
                string xref=Path.Combine(scenario.DirectoryPath,"external.dwg");File.WriteAllText(xref,"AAAA");
                var host=new RecoveryHost{ExternalPath=xref};var plotter=new RecoveryPlotter();
                CadHostProvider.Host=host;CadHostProvider.Plotter=plotter;
                scenario.Record.SourceRevisions[scenario.Record.Pages[0].Frame.SourceDocumentId]=host.GetSourceRevision(scenario.Record.Pages[0].Frame);
                PdfTaskHistory.Save(scenario.HistoryPath,scenario.Record);
                var stamp=File.GetLastWriteTimeUtc(xref);File.WriteAllText(xref,"BBBB");File.SetLastWriteTimeUtc(xref,stamp);
                using var form=RecoveryForm(scenario);
                RecoveryReject(()=>RecoveryResume(form,scenario.HistoryPath,false),"外参同长度同时间戳替换后拒绝混用历史页面");
                Check(plotter.Orders.Count==0&&!File.Exists(scenario.Record.MergedOutputPath),"外参内容变化在任何重试页面提交前被阻止");
            }

            foreach(bool missing in new[]{false,true})
            {
                using var scenario=new RecoveryScenario();
                var host=new RecoveryHost();var plotter=new ResourceRecoveryPlotter();
                CadHostProvider.Host=host;CadHostProvider.Plotter=plotter;
                scenario.Record.PlotResourceRevision=missing?null:"old-resource";PdfTaskHistory.Save(scenario.HistoryPath,scenario.Record);
                using var form=RecoveryForm(scenario);
                RecoveryReject(()=>RecoveryResume(form,scenario.HistoryPath,false),missing?"旧历史缺少打印资源证据时拒绝自动重试":"样式或驱动配置内容变化时拒绝重试");
                Check(plotter.Orders.Count==0&&!File.Exists(scenario.Record.MergedOutputPath),"资源校验失败不提交任何页、不生成混合 PDF");
            }
            using (var scenario=new RecoveryScenario())
            {
                var host=new RecoveryHost();var plotter=new ResourceRecoveryPlotter{ResourceToken="original"};
                CadHostProvider.Host=host;CadHostProvider.Plotter=plotter;
                scenario.Record.PlotResourceRevision="original";PdfTaskHistory.Save(scenario.HistoryPath,scenario.Record);
                plotter.AfterWrite=()=>plotter.ResourceToken="changed";
                using var form=RecoveryForm(scenario);
                string summary=RecoveryResume(form,scenario.HistoryPath,false);
                Check(plotter.Orders.SequenceEqual(new[]{2})&&!File.Exists(scenario.Record.MergedOutputPath)&&summary.Contains("未生成合并文件"),"页间打印资源变更阻止下一页及混合合并");
            }

            foreach (string invalid in new[] { "revision", "session", "device", "style", "changed-success", "occupied-pending", "missing-revision" })
            {
                using var scenario = new RecoveryScenario();
                var host = new RecoveryHost(); var plotter = new RecoveryPlotter();
                CadHostProvider.Host = host; CadHostProvider.Plotter = plotter;
                if (invalid == "revision") host.Token = "changed-revision";
                if (invalid == "session") host.ContextAvailable = false;
                if (invalid == "device") plotter.DeviceAvailable = false;
                if (invalid == "style") plotter.StyleAvailable = false;
                if (invalid == "changed-success") File.AppendAllText(scenario.Record.Pages[0].OutputPath, "tampered");
                if (invalid == "occupied-pending") File.WriteAllText(scenario.Record.Pages[1].OutputPath, "preexisting-unknown");
                if (invalid == "missing-revision")
                {
                    scenario.Record.SourceRevisions.Clear(); PdfTaskHistory.Save(scenario.HistoryPath, scenario.Record);
                }
                byte[] historyBefore = File.ReadAllBytes(scenario.HistoryPath);
                using var form = RecoveryForm(scenario);
                RecoveryReject(() => RecoveryResume(form, scenario.HistoryPath, false), "拒绝无法安全恢复的历史条件：" + invalid);
                Check(plotter.Orders.Count == 0 && !File.Exists(scenario.Record.MergedOutputPath),
                    "恢复校验失败时零 CAD 页面调用且不生成合并文件：" + invalid);
                Check(File.ReadAllBytes(scenario.HistoryPath).SequenceEqual(historyBefore) &&
                    (invalid != "occupied-pending" || File.ReadAllText(scenario.Record.Pages[1].OutputPath) == "preexisting-unknown"),
                    "恢复前置校验不改历史、不覆盖未知已有文件：" + invalid);
            }

            using (var scenario = new RecoveryScenario())
            {
                var host = new RecoveryHost(); var plotter = new RecoveryPlotter();
                CadHostProvider.Host = host; CadHostProvider.Plotter = plotter;
                using var form = RecoveryForm(scenario);
                using (var otherWindow = new FileStream(scenario.HistoryPath + ".runlock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    RecoveryReject(() => RecoveryResume(form, scenario.HistoryPath, false), "另一窗口占用运行锁时拒绝并发恢复同一任务");
                    Check(plotter.Orders.Count == 0 && !File.Exists(scenario.Record.MergedOutputPath),
                        "历史运行锁冲突发生在提交任何页面之前");
                }
                string summary = RecoveryResume(form, scenario.HistoryPath, false);
                Check(summary.Contains("完整批次已合并") && plotter.Orders.SequenceEqual(new[] { 2, 3 }),
                    "释放另一窗口运行锁后，同一窗口可正常重试原任务");
                using var released = new FileStream(scenario.HistoryPath + ".runlock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Check(released.CanRead, "恢复完成最终释放运行锁，不遗留占用");
            }

            using (var scenario = new RecoveryScenario())
            {
                for (int i = 1; i < scenario.Record.Pages.Count; i++)
                {
                    RecoveryWritePdf(scenario.Record.Pages[i].Frame, scenario.Record.Config, scenario.Record.Pages[i].OutputPath);
                    PdfTaskHistory.RecordPage(scenario.Record, i, BatchPageState.Succeeded, "");
                }
                scenario.Record.MergeState = PdfTaskMergeState.Failed;
                scenario.Record.MergeError = "模拟历史合并失败";
                PdfTaskHistory.RecordPage(scenario.Record, 0, BatchPageState.Succeeded, "临时纸张配置清理失败：合并前原有警告");
                PdfTaskHistory.Save(scenario.HistoryPath, scenario.Record);
                string[] oldHashes = scenario.Record.Pages.Select(p => RecoveryHash(p.OutputPath)).ToArray();
                CadHostProvider.Host = null; CadHostProvider.Plotter = null;
                using var form = RecoveryForm(scenario);
                string summary = RecoveryResume(form, scenario.HistoryPath, true);
                var saved = PdfTaskHistory.Load(scenario.HistoryPath);
                Check(summary.Contains("完整批次已合并") && saved.MergeState == PdfTaskMergeState.Succeeded &&
                    oldHashes.SequenceEqual(saved.Pages.Select(p => RecoveryHash(p.OutputPath))),
                    "全部单页完整时可不连接 CAD 恢复合并，且保持单页文件原样");
                using (var pdf = PdfReader.Open(saved.MergedOutputPath, PdfDocumentOpenMode.Import))
                    Check(pdf.Outlines[1].Title == "原任务/002:图纸2 1:1", "无 CAD 的重合并也保持原书签模板与标点");
                Check(summary.Contains("成功页面有 1 条警告") && summary.Contains("合并前原有警告") &&
                    saved.Pages[0].State == BatchPageState.Succeeded && saved.Pages[0].Error.Contains("合并前原有警告"),
                    "无需 CAD 的重合并仍从原始任务记录显示成功页警告，不被临时合并批次丢失");
            }

            using (var scenario = new RecoveryScenario())
            {
                var host = new RecoveryHost(); var plotter = new RecoveryPlotter();
                plotter.AfterWrite = () => host.Token = "modified-between-retry-pages";
                CadHostProvider.Host = host; CadHostProvider.Plotter = plotter;
                using var form = RecoveryForm(scenario);
                string summary = RecoveryResume(form, scenario.HistoryPath, false);
                var saved = PdfTaskHistory.Load(scenario.HistoryPath);
                Check(plotter.Orders.SequenceEqual(new[] { 2 }) && saved.Pages[1].State == BatchPageState.Failed && saved.Pages[1].Error.Contains("输出后来源核验失败") &&
                    saved.Pages[2].State == BatchPageState.Failed && saved.Pages[2].Error.Contains("修改记录"),
                    "当前页输出期间源图版本变化，该页与下一页均拒绝记为成功");
                Check(!File.Exists(saved.Pages[2].OutputPath) && !File.Exists(saved.MergedOutputPath) && summary.Contains("未生成合并文件"),
                    "页间源图变更不提交后续页、不把不同版本页面伪报完整合并");
            }
        }
        finally { CadHostProvider.Host = originalHost; CadHostProvider.Plotter = originalPlotter; }
    }

    private static void CheckInitialPdfTaskRecording()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PdfTaskInitial-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var host = new RecoveryHost(); var plotter = new RecoveryPlotter {
                FailOrder = 2, SuccessWarning = "临时纸张配置清理失败：offline-residual.pmp" };
            CadHostProvider.Host = host; CadHostProvider.Plotter = plotter;
            var frames = Enumerable.Range(1, 2).Select(i => new PlotFrame {
                OrderIndex = i, SourceDocumentId = "layout-document", SourceLayoutId = "space-1",
                MinX = 0, MinY = 0, MaxX = 420, MaxY = 297, CalculatedScale = 1,
                DetectedPaper = new PaperSize("A3", 420, 297), CustomOutputFileName = "initial-" + i }).ToArray();
            var config = new PlotConfig { PrinterDevice = "offline.pc3", PlotStyleTable = "monochrome.ctb", OutputDirectory = directory,
                MergeToSinglePdf = true, MergedFileName = "initial-merged.pdf" };
            var run = new BatchPlotRun(frames.Select(f => new BatchPage(f, Path.Combine(directory, f.CustomOutputFileName + ".pdf"))), config);
            using var form = new BatchPlotForm(Path.Combine(directory, "settings.json"));
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-32000, -32000); form.Show();
            Field<List<PlotFrame>>(form, "_frames").AddRange(frames);
            Call(form, "RefreshGrid");
            Call(form, "SetPlottingState", true);
            string historyPath = "";
            try
            {
                Call(form, "BeginPdfTaskRecord", run, Path.Combine(directory, config.MergedFileName));
                historyPath = Field<string>(form, "_recordingTaskPath");
                var initial = PdfTaskHistory.Load(historyPath);
                Check(initial.Pages.Count == 2 && initial.Pages.All(p => p.State == BatchPageState.Pending && p.Sha256 == "" && !File.Exists(p.OutputPath)) &&
                    initial.SourceRevisions["layout-document"] == host.Token && plotter.Orders.Count == 0,
                    "真实首次任务入口在打印前落盘全部待执行页及来源版本，尚未生成任何 PDF");
                plotter.BeforeOutput = frame =>
                {
                    var persisted = PdfTaskHistory.Load(historyPath);
                    Check(persisted.Pages[frame.OrderIndex - 1].State == BatchPageState.Running,
                        "首次任务第 " + frame.OrderIndex + " 页在假宿主调用前持久化运行中状态");
                    if (frame.OrderIndex == 2)
                        Check(persisted.Pages[0].State == BatchPageState.Succeeded && persisted.Pages[0].Sha256 == RecoveryHash(persisted.Pages[0].OutputPath),
                            "首次任务进入第二页前已持久化第一成功页的实际摘要");
                };
                var task = (System.Threading.Tasks.Task)Call(form, "RunPlotPages", run, frames, host, plotter)!;
                RecoveryWait(task);
                var completed = PdfTaskHistory.Load(historyPath);
                Check(plotter.Orders.SequenceEqual(new[] { 1, 2 }) && completed.Pages[0].State == BatchPageState.Succeeded &&
                    completed.Pages[0].Sha256 == RecoveryHash(completed.Pages[0].OutputPath) && completed.Pages[0].Bytes > 0,
                    "首次逐页输出通过实际记录钩子落盘成功文件摘要与大小");
                Check(run.Pages[0].State == BatchPageState.Succeeded && completed.Pages[0].Error == plotter.SuccessWarning &&
                    frames[0].ErrorMessage == plotter.SuccessWarning && frames[0].Status.Contains("文件已生成（警告：") &&
                    Field<DataGridView>(form, "dgvDrawings").Rows[0].Cells[8].Value?.ToString() == frames[0].Status,
                    "成功后清理警告完整进入历史与主列表，已有 PDF 保持成功且不被标为失败");
                Check(completed.Pages[1].State == BatchPageState.Failed && completed.Pages[1].Error == "模拟第二页设备失败" &&
                    completed.Pages[1].Sha256 == "" && !File.Exists(completed.Pages[1].OutputPath) && !run.CanMerge,
                    "首次逐页失败原因准确持久化，失败页不伪造成功摘要或允许完整合并");
            }
            finally
            {
                Call(form, "EndTaskHistoryRecording"); Call(form, "FinishPlotTask"); Call(form, "SetPlottingState", false);
            }
            using var lease = new FileStream(historyPath + ".runlock", FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Check(lease.CanRead, "首次任务记录收尾释放运行锁，后续恢复不被旧锁阻塞");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static BatchPlotForm RecoveryForm(RecoveryScenario scenario)
    {
        var form = new BatchPlotForm(Path.Combine(scenario.DirectoryPath, "settings.json"));
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual;
        form.Location = new System.Drawing.Point(-32000, -32000); form.Show();
        return form;
    }
    private static string RecoveryResume(BatchPlotForm form, string path, bool mergeOnly)
    {
        var task = (System.Threading.Tasks.Task<string>)Call(form, "ResumePdfTaskAsync", path, mergeOnly)!;
        RecoveryWait(task);
        return task.GetAwaiter().GetResult();
    }
    private static void RecoveryWait(System.Threading.Tasks.Task task)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
        if (!task.IsCompleted) throw new TimeoutException("PDF 历史恢复离线检查超时。");
        task.GetAwaiter().GetResult();
    }
    private static void RecoveryReject(Action action, string message)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is NotSupportedException)
        { Check(true, message); return; }
        throw new Exception("未拒绝：" + message);
    }
    private static string RecoveryHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void RecoveryWritePdf(PlotFrame frame, PlotConfig config, string path)
    {
        var plan = PlotPlanBuilder.Create(frame, config);
        using var document = new PdfDocument(); var page = document.AddPage();
        page.Width = PdfSharpCore.Drawing.XUnit.FromMillimeter(plan.PaperWidthMm);
        page.Height = PdfSharpCore.Drawing.XUnit.FromMillimeter(plan.PaperHeightMm);
        document.Save(path);
    }
    private sealed class RecoveryScenario : IDisposable
    {
        public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "PdfTaskRecovery-" + Guid.NewGuid().ToString("N"));
        public string HistoryPath => Path.Combine(DirectoryPath, "tasks", Record.Id + ".json");
        public PdfTaskRecord Record { get; }
        public RecoveryScenario()
        {
            Directory.CreateDirectory(DirectoryPath);
            var frames = Enumerable.Range(1, 3).Select(i => new PlotFrame { OrderIndex = i, Type = FrameType.ClosedPolyline,
                SourceDocumentId = "layout-document", SourceLayoutId = i == 3 ? "space-2" : "space-1", HandleOrId = i.ToString("X"),
                LayoutName = i == 3 ? "详图" : "总图", MinX = 0, MinY = 0, MaxX = 420 + i * 10, MaxY = 297, CalculatedScale = 1,
                DetectedPaper = new PaperSize("自定义" + i, 420 + i * 10, 297),
                TitleInfo = new TitleBlockInfo { DrawingNo = "S-" + i, DrawingName = "图纸" + i }, CustomOutputFileName = "page-" + i }).ToArray();
            var config = new PlotConfig { OverwriteExisting = false, PrinterDevice = "offline.pc3", PlotStyleTable = "monochrome.ctb", OutputDirectory = DirectoryPath,
                ExportFormat = PlotExportFormat.PDF, MarginMm = 2.5, MergeToSinglePdf = true, MergedFileName = "merged.pdf", BookmarkTemplate = "原任务/{Index:D3}:{DwgName} {Scale}" };
            var run = new BatchPlotRun(frames.Select(f => new BatchPage(f, Path.Combine(DirectoryPath, f.CustomOutputFileName + ".pdf"))), config);
            Record = PdfTaskHistory.Create(run, Path.Combine(DirectoryPath, "merged.pdf"));
            Record.SourceRevisions.Add("layout-document", "revision-original");
            RecoveryWritePdf(frames[0], config, Record.Pages[0].OutputPath);
            PdfTaskHistory.RecordPage(Record, 0, BatchPageState.Succeeded, "");
            PdfTaskHistory.RecordPage(Record, 1, BatchPageState.Failed, "模拟可重试错误");
            PdfTaskHistory.RecordPage(Record, 2, BatchPageState.Cancelled, "模拟原任务取消");
            PdfTaskHistory.Save(HistoryPath, Record);
        }
        public void Dispose() { Directory.Delete(DirectoryPath, true); }
    }
    private class RecoveryPlotter : ICadPlotter
    {
        public bool DeviceAvailable = true, StyleAvailable = true;
        public List<int> Orders { get; } = new();
        public List<string> Settings { get; } = new();
        public Action? AfterWrite;
        public Action<PlotFrame>? BeforeOutput;
        public int FailOrder;
        public string SuccessWarning = "";
        public List<string> GetAvailablePlotters() => DeviceAvailable ? new() { "offline.pc3" } : new();
        public List<string> GetAvailablePlotStyles() => StyleAvailable ? new() { "monochrome.ctb" } : new();
        public List<string> GetPaperSizesForPlotter(string device) => new();
        public bool PlotFrameToFile(PlotFrame frame, PlotConfig config, string path, out string error)
        {
            Orders.Add(frame.OrderIndex);
            Settings.Add(config.PrinterDevice + "|" + config.PlotStyleTable + "|" + config.MarginMm.ToString(System.Globalization.CultureInfo.InvariantCulture));
            BeforeOutput?.Invoke(frame);
            if (frame.OrderIndex == FailOrder) { error = "模拟第二页设备失败"; return false; }
            RecoveryWritePdf(frame, config, path); AfterWrite?.Invoke(); error = SuccessWarning; return true;
        }
    }
    private sealed class ResourceRecoveryPlotter:RecoveryPlotter,ICadPlotResourceHost
    {
        public string ResourceToken="current-resource";
        public string GetPlotResourceRevision(PlotConfig config)=>ResourceToken;
    }
    private sealed class RecoveryHost : ICadHost, ICadLayoutHost, ICadRevisionHost
    {
        private readonly LayoutHost inner = new();
        public string Token = "revision-original";
        public string? ExternalPath;
        public bool ContextAvailable { get => inner.Accessible; set => inner.Accessible = value; }
        public string GetSourceRevision(PlotFrame frame)
        { if (!CanAccessFrameContext(frame)) throw new InvalidOperationException("原文档会话已关闭。"); return ExternalPath==null?Token:Token+ExternalFileRevision.Read(ExternalPath); }
        public string PlatformName => "离线恢复假宿主";
        public string Version => "test";
        public CadCandidateSnapshot CollectCandidates(CadScanScope scope, string layerFilter = "*") => inner.CollectCandidates(scope, layerFilter);
        public bool CanAccessFrameContext(PlotFrame frame) => inner.CanAccessFrameContext(frame);
        public string GetCurrentDocumentPath() => inner.GetCurrentDocumentPath();
        public string GetCurrentDocumentName() => inner.GetCurrentDocumentName();
        public void WriteMessage(string message) { }
        public List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter = "*") => inner.CollectPolylineCandidates(layerFilter);
        public List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter = "*") => inner.CollectBlockCandidates(blockNameFilter);
        public bool PromptSelectFrames(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks) => inner.PromptSelectFrames(out polylines, out blocks);
        public bool PromptSelectSampleBlock(out string blockName, out string layerName) => inner.PromptSelectSampleBlock(out blockName, out layerName);
        public void ZoomToFrame(double minX, double minY, double maxX, double maxY) { }
        public List<string> GetAllLayers() => inner.GetAllLayers();
        public List<string> GetAllBlockNames() => inner.GetAllBlockNames();
    }
}

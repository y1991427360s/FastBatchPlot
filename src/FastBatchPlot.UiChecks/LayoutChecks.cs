using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckLayoutUi()
    {
        var previous = CadHostProvider.Host;
        var host = new LayoutHost();
        CadHostProvider.Host = host;
        try
        {
            using var form = new BatchPlotForm(null);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-32000, -32000);
            form.ShowInTaskbar = false;
            form.Show();
            var frames = Field<List<PlotFrame>>(form, "_frames");
            var scope = Field<ComboBox>(form, "cboScanScope");
            var grid = Field<DataGridView>(form, "dgvDrawings");
            foreach (var value in new[] { CadScanScope.CurrentSpace, CadScanScope.Model, CadScanScope.AllLayouts, CadScanScope.ModelAndLayouts })
            {
                scope.SelectedIndex = (int)value;
                Call(form, "AutoDetectFrames");
                Check(host.LastScope == value, "扫描范围传给支持布局的宿主：" + value);
            }
            Check(host.Calls == 4 && host.LegacyScanCalls == 0, "布局宿主每次搜索仅采集一次快照");
            Check(frames.Count == 2 && frames[0].LayoutName == "总图" && frames[1].LayoutName == "详图", "不同布局同坐标图框保留，并按布局顺序排列");
            Check(grid.Columns["LayoutName"]!.ReadOnly && grid.Columns["LayoutName"]!.DisplayIndex == 2 &&
                Convert.ToString(grid.Rows[0].Cells["LayoutName"].Value) == "总图", "布局列显示来源布局且不可编辑");
            grid.Rows[0].Cells[7].Value = "跨布局人工名";
            Check(frames[0].CustomOutputFileName == "跨布局人工名", "插入布局展示列后原文件名编辑仍作用于正确对象");

            var before = frames.ToArray();
            host.ThrowScan = true;
            Call(form, "AutoDetectFrames");
            Check(frames.SequenceEqual(before) && frames[0].CustomOutputFileName == "跨布局人工名" &&
                Field<Label>(form, "lblStatus").Text.Contains("模拟布局采集失败"), "跨布局采集异常保留原列表和人工名");
            host.ThrowScan = false;
            Call(form, "PickFramesFromCad");
            Check(frames.SequenceEqual(before) && form.Visible, "取消当前空间框选保留跨布局列表");
            host.ThrowSelection = true;
            Call(form, "PickFramesFromCad");
            Check(frames.SequenceEqual(before) && form.Visible && Field<Label>(form, "lblStatus").Text.Contains("模拟框选失败"), "框选异常保留跨布局列表并恢复窗口");

            Call(null, "EnsureFrameContextsAccessible", (object)frames);
            Check(host.ContextChecks == 2, "打印预检允许同文档内仍存在的其他布局，不要求当前空间");
            host.Accessible = false;
            Reject(() => Call(null, "EnsureFrameContextsAccessible", (object)frames), "来源文档或布局失效时打印预检拒绝");
            host.Accessible = true;
            grid.ClearSelection();
            grid.Rows[0].Selected = true;
            Call(form, "LocateSelectedFrameInCad");
            Check(host.ZoomCalls == 0 && Field<Label>(form, "lblStatus").Text.Contains("原文档和布局"), "跨布局定位仍安全拒绝，不调用宿主缩放");

            var legacy = new SelectionHost();
            CadHostProvider.Host = legacy;
            Call(form, "AutoDetectFrames");
            Check(legacy.SnapshotCalls == 0 && frames.SequenceEqual(before) && Field<Label>(form, "lblStatus").Text.Contains("不支持跨布局"),
                "旧宿主选择跨布局范围时明确拒绝并保留列表，不静默回退");
            Reject(() => Call(null, "EnsureFrameContextsAccessible", (object)frames), "旧宿主仍按当前空间校验，不能越过来源限制");
            var legacyFrame = ManualFrameFactory.Create(new Rect2D(0, 0, 841, 594), 1, "fake-document", "fake-model", "Model");
            Call(null, "EnsureFrameContextsAccessible", (object)new[] { legacyFrame });
            Check(true, "旧宿主当前空间有效图框可通过预检");
            scope.SelectedIndex = (int)CadScanScope.CurrentSpace;
            Call(form, "AutoDetectFrames");
            Check(legacy.SnapshotCalls == 1, "旧宿主当前空间搜索保持兼容");
            form.Size = form.MinimumSize;
            form.PerformLayout();
            Check(scope.Parent!.ClientRectangle.Contains(scope.Bounds) && scope.Visible, "最小窗口搜索范围控件完整可见");
        }
        finally { CadHostProvider.Host = previous; }
    }

    private sealed class LayoutHost : ICadHost, ICadLayoutHost, ICadContextHost
    {
        public string PlatformName => "离线布局假宿主";
        public string Version => "test";
        public CadScanScope LastScope { get; private set; }
        public int Calls { get; private set; }
        public int LegacyScanCalls { get; private set; }
        public int ContextChecks { get; private set; }
        public int ZoomCalls { get; private set; }
        public bool Accessible { get; set; } = true;
        public bool ThrowScan { get; set; }
        public bool ThrowSelection { get; set; }
        public CadCandidateSnapshot CollectCandidates(CadScanScope scope, string layerFilter = "*")
        {
            Calls++;
            LastScope = scope;
            if (ThrowScan) throw new InvalidOperationException("模拟布局采集失败");
            var result = new CadCandidateSnapshot();
            foreach (var index in new[] { 2, 1 })
                result.Blocks.Add(new RawBlockCandidate
                {
                    Handle = "layout-frame-" + index, BlockName = "A1框", Layer = "FRAME",
                    Bounds = new Rect2D(0, 0, 841, 594), SourceDocumentId = "layout-document",
                    SourceLayoutId = "space-" + index, LayoutName = index == 1 ? "总图" : "详图", LayoutOrder = index
                });
            return result;
        }
        public bool CanAccessFrameContext(PlotFrame frame)
        {
            ContextChecks++;
            return Accessible && frame.SourceDocumentId == "layout-document" &&
                (frame.SourceLayoutId == "space-1" || frame.SourceLayoutId == "space-2");
        }
        public bool IsFrameContextCurrent(PlotFrame frame) => false;
        public string GetCurrentDocumentPath() => @"D:\offline-fixture\layouts.dwg";
        public string GetCurrentDocumentName() => "layouts.dwg";
        public void WriteMessage(string message) { }
        public List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter = "*")
        { LegacyScanCalls++; throw new InvalidOperationException("布局宿主不应调用旧采集接口"); }
        public List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter = "*")
        { LegacyScanCalls++; throw new InvalidOperationException("布局宿主不应调用旧采集接口"); }
        public bool PromptSelectFrames(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks)
        {
            polylines = new(); blocks = new();
            if (ThrowSelection) throw new InvalidOperationException("模拟框选失败");
            return false;
        }
        public bool PromptSelectSampleBlock(out string blockName, out string layerName)
        { blockName = ""; layerName = ""; return false; }
        public void ZoomToFrame(double minX, double minY, double maxX, double maxY) { ZoomCalls++; }
        public List<string> GetAllLayers() => new() { "FRAME" };
        public List<string> GetAllBlockNames() => new() { "A1框" };
    }
}

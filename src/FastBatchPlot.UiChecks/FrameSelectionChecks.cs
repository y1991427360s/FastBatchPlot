using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckFrameSelectionUi()
    {
        var previousHost = CadHostProvider.Host;
        var fake = new SelectionHost();
        CadHostProvider.Host = fake;
        try
        {
            using var form = new BatchPlotForm(null);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-32000, -32000);
            form.ShowInTaskbar = false;
            form.Show();
            var frames = Field<List<PlotFrame>>(form, "_frames");
            var grid = Field<DataGridView>(form, "dgvDrawings");
            var existing = ManualFrameFactory.Create(new Rect2D(0, 0, 841, 594), 1, "fake-document", "fake-model", "Model");
            existing.Id = 40;
            existing.TitleInfo.DrawingName = "已有图纸";
            frames.Add(existing);
            Call(form, "RefreshGrid");
            grid.Rows[0].Cells[7].Value = "原有人工文件名";

            var manual = ManualFrameFactory.Create(new Rect2D(2000, 0, 2830, 600), 2.5, "fake-document", "fake-model", "Model");
            fake.ManualFrame = manual;
            Call(form, "AddManualFrame", ManualFrameSelectionMode.TwoCorners);
            Check(frames.Count == 2 && frames.Contains(existing) && frames.Contains(manual), "两点添加范围追加到列表，不覆盖已有图纸");
            Check(existing.CustomOutputFileName == "原有人工文件名", "手工追加保留已有人工文件名");
            Check(fake.LastManualMode == ManualFrameSelectionMode.TwoCorners && manual.Id == 41, "两点模式传给宿主且分配不重复标识");
            Check(fake.IsFrameContextCurrent(manual) && manual.LayoutName == "Model", "手工追加保留来源文档和空间");
            var manualRow = grid.Rows.Cast<DataGridViewRow>().Single(r => ReferenceEquals(r.Tag, manual));
            Check(Convert.ToString(manualRow.Cells[5].Value) == $"1:{2.5:0.##}", "手工范围的小数比例在表格显示中保持精度");
            Check(manual.DetectedPaper.Name == "自定义" && manual.DetectedPaper.WidthMm == 332 && manual.DetectedPaper.HeightMm == 240,
                "非标手工范围保留按比例换算的实际纸张尺寸");

            var group = ManualFrameFactory.Create(new Rect2D(4000, 0, 4841, 594), 1, "fake-document", "fake-model", "Model");
            fake.ManualFrame = group;
            Call(form, "AddManualFrame", ManualFrameSelectionMode.EntityGroup);
            Check(fake.LastManualMode == ManualFrameSelectionMode.EntityGroup && frames.Count == 3 && frames.Contains(group), "图集添加独立传递模式并继续追加");
            var beforeCancel = frames.ToArray();
            fake.ManualFrame = null;
            Call(form, "AddManualFrame", ManualFrameSelectionMode.TwoCorners);
            Check(frames.SequenceEqual(beforeCancel), "取消手工范围不删除或重排既有图纸");
            fake.ThrowManual = true;
            Call(form, "AddManualFrame", ManualFrameSelectionMode.EntityGroup);
            Check(frames.SequenceEqual(beforeCancel) && form.Visible && Field<Label>(form, "lblStatus").Text.Contains("模拟手工失败"),
                "宿主手工选择异常保留列表、恢复窗口并显示失败原因");
            fake.ThrowManual = false;

            fake.Snapshot.Blocks.Add(new RawBlockCandidate { Handle = "large", BlockName = "A0框", Layer = "FRAME", Bounds = new Rect2D(0, 0, 1189, 841), SourceDocumentId = "fake-document", SourceLayoutId = "fake-model" });
            fake.Snapshot.Polylines.Add(Polyline("small", "FRAME", 2000, 0, 2297, 210));
            fake.Snapshot.Warnings.Add("模拟一个坏实体已跳过");
            var area = Field<NumericUpDown>(form, "numAreaFilter");
            area.Value = 0;
            Call(form, "AutoDetectFrames");
            Check(fake.SnapshotCalls == 1 && fake.LegacyScanCalls == 0, "自动搜索只采集一次统一快照，不调用旧双扫描接口");
            Check(frames.Count == 2 && frames.Any(f => f.HandleOrId == "large") && frames.Any(f => f.HandleOrId == "small"), "面积阈值0%同时保留混合来源的A0和A4图框");
            Check(Field<Label>(form, "lblStatus").Text.Contains("采集警告 1"), "自动搜索向用户展示快照采集警告数量");
            var reportText = Field<string>(form, "_lastDetectionReport");
            Check(reportText.Contains("模拟一个坏实体已跳过") && reportText.Contains("候选 2 个，保留 2 个，过滤 0 个"), "识别报告保留采集警告原文与候选完整计数");
            using (var reportDialog = (Form)Call(form,"CreateDetectionReportDialog")!)
            {
                var reportBox = reportDialog.Controls.OfType<TextBox>().Single();
                Check(reportBox.ReadOnly && reportBox.Text == reportText && reportBox.ScrollBars == ScrollBars.Both,"报告窗口显示快照正文并支持只读滚动选择");
            }
            area.Value = 10;
            Check(frames.Count == 2, "修改面积阈值不立即丢弃当前列表");
            Call(form, "AutoDetectFrames");
            Check(frames.Count == 1 && frames[0].HandleOrId == "large" && Field<Label>(form, "lblStatus").Text.Contains("面积过滤 1"),
                "面积阈值10%在下次搜索过滤不足最大面积十分之一的A4");
            Check(Field<string>(form,"_lastDetectionReport").Contains("面积阈值过滤") && Field<string>(form,"_lastDetectionReport").Contains("实体=small"),"面积过滤报告可定位被排除的图框");
            Check(fake.SnapshotCalls == 2 && fake.LegacyScanCalls == 0, "每次搜索各采集一个快照");
            var beforeFailure = frames.ToArray();
            fake.ThrowSnapshot = true;
            Call(form, "AutoDetectFrames");
            Check(frames.SequenceEqual(beforeFailure) && Field<Label>(form, "lblStatus").Text.Contains("模拟采集失败"), "快照采集失败保留上次有效列表");
            Check(Field<string>(form,"_lastDetectionReport").Contains("搜索失败，原列表已保留") && !Field<string>(form,"_lastDetectionReport").Contains("搜索（"),"失败报告不伪装成上次搜索成功");
            fake.ThrowSnapshot = false;
            area.Value = 0;

            fake.SampleLayer = "frame";
            Call(form, "PickLayerFilter");
            Check(Field<Button>(form, "btnLayerFilter").Text.Contains("frame") && frames.SequenceEqual(beforeFailure), "点选图层更新筛选提示且保留当前列表");
            fake.SampleLayer = null;
            Call(form, "PickLayerFilter");
            Check(Field<string>(form, "_specifiedLayerName") == "frame", "取消图层点选保留原筛选条件");
            var nested = new List<RawPolylineCandidate>
            {
                Polyline("other-outer", "OTHER", 3000, 0, 3440, 317),
                Polyline("wanted-inner", "FRAME", 3010, 10, 3430, 307)
            };
            var candidates = new List<RawBlockCandidate>
            {
                new() { Handle = "wanted-block", BlockName = "A4框", Layer = "FrAmE", Bounds = new Rect2D(4000, 0, 4297, 210) },
                new() { Handle = "other-block", BlockName = "A4框", Layer = "OTHER", Bounds = new Rect2D(5000, 0, 5297, 210) }
            };
            var selected = (FrameSelectionResult)Call(form, "SelectCandidates", nested, candidates)!;
            var overlapping = new List<RawPolylineCandidate>(nested) { Polyline("wanted-copy","FRAME",3010,10,3430,307) };
            Field<CheckBox>(form,"chkRemoveDuplicates").Checked=false;
            Field<CheckBox>(form,"chkRemoveNestedFrames").Checked=false;
            var unfiltered = (FrameSelectionResult)Call(form, "SelectCandidates", overlapping, candidates)!;
            Check(unfiltered.Frames.Count == selected.Frames.Count+1,"关闭重复和内含过滤保留联合候选，未提前删除多段线");
            Field<CheckBox>(form,"chkRemoveDuplicates").Checked=true;
            Field<CheckBox>(form,"chkRemoveNestedFrames").Checked=true;
            Check(selected.Frames.Count == 2 && selected.Frames.Any(f => f.HandleOrId == "wanted-inner") && selected.Frames.Any(f => f.HandleOrId == "wanted-block"),
                "图层筛选忽略大小写、同时约束块和多段线，并在嵌套去重前保留指定层内框");
            Check(selected.Report.TotalCount == 2 && selected.Report.Counts["图层筛选"] == 2,"识别前图层筛选的两个排除对象被准确计数");
            fake.Snapshot.Polylines.Clear();
            fake.Snapshot.Polylines.AddRange(nested);
            fake.Snapshot.Blocks.Clear();
            fake.Snapshot.Blocks.AddRange(candidates);
            Call(form, "AutoDetectFrames");
            Check(frames.Count == 2 && frames.All(f => string.Equals(f.SourceLayer, "frame", StringComparison.OrdinalIgnoreCase)),
                "图层筛选经自动搜索入口实际约束生成列表");
            var beforeCancelledPick = frames.ToArray();
            Call(form,"PickFramesFromCad");
            Check(frames.SequenceEqual(beforeCancelledPick) && Field<string>(form,"_lastDetectionReport").Contains("框选取消"),"取消框选保留列表并标明本次没有识别报告");
            area.Value = 6.3m;
            Check(Math.Abs(Field<double>(form, "_minimumAreaPercent") - 6.3) < 1e-9 && area.DecimalPlaces >= 1, "面积阈值控件接受并传递一位小数");
        }
        finally { CadHostProvider.Host = previousHost; }
    }

    private static RawPolylineCandidate Polyline(string handle, string layer, double left, double bottom, double right, double top) => new()
    {
        Handle = handle, Layer = layer, SourceDocumentId = "fake-document", SourceLayoutId = "fake-model",
        Vertices = new() { new(left, bottom), new(right, bottom), new(right, top), new(left, top) }
    };

    private sealed class SelectionHost : ICadHost, ICadFrameSelectionHost, ICadContextHost
    {
        public string PlatformName => "离线假宿主";
        public string Version => "test";
        public CadCandidateSnapshot Snapshot { get; } = new();
        public PlotFrame? ManualFrame { get; set; }
        public string? SampleLayer { get; set; }
        public ManualFrameSelectionMode? LastManualMode { get; private set; }
        public bool ThrowManual { get; set; }
        public bool ThrowSnapshot { get; set; }
        public int SnapshotCalls { get; private set; }
        public int LegacyScanCalls { get; private set; }
        public CadCandidateSnapshot CollectCurrentSpaceCandidates(string layerFilter = "*")
        {
            SnapshotCalls++;
            if (ThrowSnapshot) throw new InvalidOperationException("模拟采集失败");
            return Snapshot;
        }
        public bool PromptManualFrame(ManualFrameSelectionMode mode, out PlotFrame? frame)
        {
            LastManualMode = mode;
            if (ThrowManual) throw new InvalidOperationException("模拟手工失败");
            frame = ManualFrame;
            return frame != null;
        }
        public bool IsFrameContextCurrent(PlotFrame frame) => frame.SourceDocumentId == "fake-document" && frame.SourceLayoutId == "fake-model";
        public string GetCurrentDocumentPath() => @"D:\offline-fixture\test.dwg";
        public string GetCurrentDocumentName() => "test.dwg";
        public void WriteMessage(string message) { }
        public List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter = "*")
        { LegacyScanCalls++; throw new InvalidOperationException("不应执行独立多段线扫描"); }
        public List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter = "*")
        { LegacyScanCalls++; throw new InvalidOperationException("不应执行独立块扫描"); }
        public bool PromptSelectFrames(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks)
        { polylines = new(); blocks = new(); return false; }
        public bool PromptSelectSampleBlock(out string blockName, out string layerName)
        { blockName = ""; layerName = SampleLayer ?? ""; return SampleLayer != null; }
        public void ZoomToFrame(double minX, double minY, double maxX, double maxY) => throw new InvalidOperationException("离线检查禁止缩放CAD");
        public List<string> GetAllLayers() => new() { "FRAME", "OTHER" };
        public List<string> GetAllBlockNames() => new() { "A0框", "A4框" };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Detection
{
    public sealed class FrameSelectionOptions
    {
        public string BlockName { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public double MinimumAreaPercent { get; set; }
        public bool RemoveDuplicates { get; set; } = true;
        public bool RemoveNestedFrames { get; set; } = true;
    }

    public sealed class FrameSelectionResult
    {
        public DetectionReport Report { get; internal set; } = new DetectionReport();
        public List<PlotFrame> Frames { get; } = new List<PlotFrame>();
        public int DuplicateCount { get; internal set; }
        public int AreaFilteredCount { get; internal set; }
    }

    /// <summary>与界面和CAD无关的候选合并策略；去重只在同一文档和空间内进行。</summary>
    public static class FrameSelectionService
    {
        private static Rect2D Bounds(PlotFrame frame)=>frame.PrintBounds??new Rect2D(frame.MinX,frame.MinY,frame.MaxX,frame.MaxY);
        public static FrameSelectionResult Select(IEnumerable<PlotFrame> candidates, FrameSelectionOptions options, DetectionReport? report = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (double.IsNaN(options.MinimumAreaPercent) || options.MinimumAreaPercent < 0 || options.MinimumAreaPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(options.MinimumAreaPercent));
            var result = new FrameSelectionResult { Report = report ?? new DetectionReport() };
            var filtered = new List<PlotFrame>();
            foreach (var frame in candidates)
            {
                if (!string.IsNullOrEmpty(options.BlockName) && (frame.Type != FrameType.BlockReference || !string.Equals(frame.SourceBlockName, options.BlockName, StringComparison.OrdinalIgnoreCase)))
                { result.Report.Add("块名筛选", frame); continue; }
                if (!string.IsNullOrEmpty(options.LayerName) && !string.Equals(frame.SourceLayer, options.LayerName, StringComparison.OrdinalIgnoreCase))
                { result.Report.Add("图层筛选", frame); continue; }
                filtered.Add(frame);
            }
            foreach (var group in filtered.GroupBy(f => new { f.SourceDocumentId, f.SourceLayoutId }))
            {
                var kept = new List<PlotFrame>();
                foreach (var frame in group.OrderByDescending(f=>f.PrintBounds.HasValue).ThenByDescending(f => Bounds(f).Area).ThenBy(f => f.Type == FrameType.BlockReference ? 0 : 1))
                {
                    var bounds = Bounds(frame);
                    if (kept.Any(other => {
                        var existing = Bounds(other);
                        bool duplicate = existing.IsNearlyEqual(bounds, 0.02);
                        // 重叠框与内框互斥分类，关闭重复过滤后不能再被内框开关删除。
                        return duplicate ? options.RemoveDuplicates : (options.RemoveNestedFrames &&
                            existing.Contains(bounds, 1.0));
                    })) { result.DuplicateCount++; result.Report.Add("重复或嵌套内框", frame); continue; }
                    kept.Add(frame);
                }
                double maximum = kept.Count == 0 ? 0 : kept.Max(f => Bounds(f).Area);
                foreach (var frame in kept)
                {
                    if (Bounds(frame).Area < maximum * options.MinimumAreaPercent / 100.0) { result.AreaFilteredCount++; result.Report.Add("面积阈值过滤", frame); }
                    else result.Frames.Add(frame);
                }
            }
            return result;
        }
    }
}

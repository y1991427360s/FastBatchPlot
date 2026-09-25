using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Paper;

namespace FastBatchPlot.Core.Detection
{
    public class RawPolylineCandidate
    {
        public string Handle { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public string SourceDocumentId { get; set; } = string.Empty;
        public string SourceFileName { get; set; } = string.Empty;
        public string SourceLayoutId { get; set; } = string.Empty;
        public string LayoutName { get; set; } = "Model";
        public int LayoutOrder { get; set; }
        public List<Point2D> Vertices { get; set; } = new List<Point2D>();
        public Rect2D Bounds => GeometryUtils.GetBoundingBox(Vertices);
    }

    /// <summary>
    /// 封闭多段线图框智能检测与去重过滤引擎
    /// </summary>
    public static class PolylineFrameDetector
    {
        /// <summary>
        /// 从多段线候选集中检测有效矩形图框并自动过滤内层边框与重复框
        /// </summary>
        public static List<PlotFrame> FilterAndCreateFrames(IEnumerable<RawPolylineCandidate> candidates, double preferredScale = 0, DetectionReport? report = null)
            => CreateFrames(candidates, preferredScale, report, true);

        /// <summary>联合块与多段线识别时保留全部有效候选，由统一筛选器消费用户过滤开关。</summary>
        public static List<PlotFrame> CreateCandidates(IEnumerable<RawPolylineCandidate> candidates, double preferredScale = 0, DetectionReport? report = null)
            => CreateFrames(candidates, preferredScale, report, false);

        private static List<PlotFrame> CreateFrames(IEnumerable<RawPolylineCandidate> candidates, double preferredScale, DetectionReport? report, bool filterNested)
        {
            PaperSizeDetector.ValidatePreferredScale(preferredScale);
            var validRectangles = new List<RawPolylineCandidate>();

            foreach (var cand in candidates)
            {
                // 与宿主采集的闭合容差一致（首尾距离 < 2），并允许边上的共线多余顶点。
                var corners = GeometryUtils.SimplifyClosedPolygon(cand.Vertices, 2.0);
                if (corners.Count != 4)
                { report?.Add("顶点数不符合矩形", cand); continue; }
                if (!GeometryUtils.IsOrthogonalRectangle(corners, 5.0))
                { report?.Add("不是正交矩形", cand); continue; }
                var bounds = cand.Bounds;
                if (!(bounds.Width >= 100 && bounds.Height >= 100))
                { report?.Add("尺寸小于 100×100", cand); continue; }
                if (!(preferredScale > 0 || PaperSizeDetector.Detect(bounds.Width, bounds.Height).MatchScore <= PaperSizeDetector.AcceptableMatchError))
                { report?.Add("自动纸张匹配失败（可指定识别比例）", cand); continue; }
                validRectangles.Add(cand);
            }

            // 过滤重复和内外嵌套图框 (如装订内边框与外边框嵌套)
            var filtered = filterNested ? validRectangles
                .GroupBy(c => new { c.SourceDocumentId, c.SourceLayoutId })
                .SelectMany(group => FilterNestedAndDuplicates(group.ToList(), report)) : validRectangles;

            // 转换为 PlotFrame 对象并匹配图幅与出图比例
            var frames = new List<PlotFrame>();
            int idx = 1;
            foreach (var cand in filtered)
            {
                var bounds = cand.Bounds;
                var detection = preferredScale>0?PaperSizeDetector.AtExactScale(bounds.Width,bounds.Height,preferredScale):PaperSizeDetector.Detect(bounds.Width, bounds.Height);

                // 如果多段线与标准图幅的匹配误差过大（超过 7%），说明是图纸内部的表格、电气箱或设备外框，直接过滤
                if (detection.MatchScore > PaperSizeDetector.AcceptableMatchError)
                {
                    continue;
                }

                var frame = new PlotFrame
                {
                    Id = idx,
                    OrderIndex = idx,
                    MinX = bounds.MinX,
                    MinY = bounds.MinY,
                    MaxX = bounds.MaxX,
                    MaxY = bounds.MaxY,
                    Type = FrameType.ClosedPolyline,
                    SourceLayer = cand.Layer,
                    HandleOrId = cand.Handle,
                    SourceDocumentId = cand.SourceDocumentId,
                    SourceFileName = cand.SourceFileName,
                    SourceLayoutId = cand.SourceLayoutId,
                    LayoutName = cand.LayoutName,
                    LayoutOrder = cand.LayoutOrder,
                    DetectedPaper = detection.Paper,
                    CalculatedScale = detection.Scale,
                    IsLandscape = detection.IsLandscape,
                    TitleInfo = new TitleBlockInfo
                    {
                        DrawingName = $"图纸_{idx:D2}",
                        DrawingNo = $"{idx:D2}"
                    }
                };

                frames.Add(frame);
                idx++;
            }

            return frames;
        }

        /// <summary>
        /// 过滤嵌套内框与完全重叠的边框 (保留面积更大的外图框)
        /// </summary>
        private static List<RawPolylineCandidate> FilterNestedAndDuplicates(List<RawPolylineCandidate> candidates, DetectionReport? report)
        {
            if (candidates.Count <= 1) return candidates;

            // 按面积降序排序，先保留大框
            var sorted = candidates.OrderByDescending(c => c.Bounds.Area).ToList();
            var kept = new List<RawPolylineCandidate>();

            foreach (var cand in sorted)
            {
                var b = cand.Bounds;
                bool isRedundant = false;

                foreach (var existing in kept)
                {
                    var eb = existing.Bounds;

                    // 1. 检查重叠/近似相等
                    if (eb.IsNearlyEqual(b, 0.02))
                    {
                        isRedundant = true;
                        break;
                    }

                    // 2. 检查是否为嵌套内框 (包含装订内线以及图框内部的表格、明细栏、元器件外框等)
                    if (eb.Contains(b, 5.0))
                    {
                        isRedundant = true;
                        break;
                    }
                }

                if (isRedundant) report?.Add("重复或嵌套内框", cand);
                if (!isRedundant)
                {
                    kept.Add(cand);
                }
            }

            return kept;
        }
    }
}

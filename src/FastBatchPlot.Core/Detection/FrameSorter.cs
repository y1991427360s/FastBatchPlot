using System;
using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.Core.Detection
{
    /// <summary>
    /// 图框空间几何与逻辑排序引擎
    /// </summary>
    public static class FrameSorter
    {
        public static List<PlotFrame> Sort(List<PlotFrame> frames, SortOrderRule rule)
        {
            if (frames == null) return new List<PlotFrame>();
            if (frames.Count <= 1 || rule == SortOrderRule.Custom)
            {
                var single = frames.ToList();
                for (int i = 0; i < single.Count; i++) single[i].OrderIndex = i + 1;
                return single;
            }

            // 图号与模板优先级是全局逻辑顺序：跨布局、跨文件统一排序（目录页等可排到最前）。
            if (rule == SortOrderRule.ByDrawingNo || rule == SortOrderRule.ByBlockPriority)
            {
                // 先按布局与几何位置得到稳定基准，图号相同或缺失时保持图面顺序。
                var global = SortWithinSpace(Sort(frames, SortOrderRule.LeftToRight_TopToBottom), rule);
                for (int i = 0; i < global.Count; i++) global[i].OrderIndex = i + 1;
                return global;
            }

            // 文档保留首次出现顺序，各布局按 CAD 标签顺序；仅在同一空间内比较坐标。
            var result = new List<PlotFrame>();
            foreach (var document in frames.GroupBy(f => f.SourceDocumentId))
            {
                foreach (var layout in document.GroupBy(f => f.SourceLayoutId)
                    .OrderBy(g => g.Min(f => f.LayoutOrder)))
                    result.AddRange(SortWithinSpace(layout.ToList(), rule));
            }
            for (int i = 0; i < result.Count; i++) result[i].OrderIndex = i + 1;
            return result;
        }

        private static List<PlotFrame> SortWithinSpace(List<PlotFrame> frames, SortOrderRule rule)
        {
            List<PlotFrame> result;

            switch (rule)
            {
                case SortOrderRule.LeftToRight_TopToBottom:
                    result = SortLeftToRightTopToBottom(frames);
                    break;

                case SortOrderRule.TopToBottom_LeftToRight:
                    result = SortTopToBottomLeftToRight(frames);
                    break;

                case SortOrderRule.ByDrawingNo:
                    result = SortByDrawingNumber(frames);
                    break;

                case SortOrderRule.ByBlockPriority:
                    result = frames.OrderBy(f => f.TemplatePriority).ThenBy(f => f.TitleInfo.DrawingNo, new NaturalStringComparer()).ToList();
                    break;

                default:
                    result = frames.ToList();
                    break;
            }

            // 更新排序序号
            for (int i = 0; i < result.Count; i++)
            {
                result[i].OrderIndex = i + 1;
            }

            return result;
        }

        /// <summary>
        /// 从左到右，从上到下：分行聚类算法 (支持混排图幅与底线对齐)
        /// </summary>
        private static List<PlotFrame> SortLeftToRightTopToBottom(List<PlotFrame> frames) => SortByAxis(frames, true);

        private static List<PlotFrame> SortTopToBottomLeftToRight(List<PlotFrame> frames) => SortByAxis(frames, false);

        // 每组维护累积统计，避免每插入一张图就重新遍历整组。
        // 候选组仍按创建顺序判断，保持混合图幅重叠/底线规则和稳定顺序。
        private static List<PlotFrame> SortByAxis(List<PlotFrame> frames, bool rows)
        {
            if (frames.Count <= 1) return frames;
            var ordered = rows ? frames.OrderByDescending(f => f.MaxY) : frames.OrderBy(f => f.MinX);
            var groups = new List<AxisGroup>();
            foreach (var frame in ordered)
            {
                double min = rows ? frame.MinY : frame.MinX, max = rows ? frame.MaxY : frame.MaxX;
                AxisGroup? found = null;
                foreach (var group in groups)
                {
                    double overlap = Math.Max(0, Math.Min(max, group.Maximum) - Math.Max(min, group.Minimum));
                    double smaller = Math.Min(max - min, group.SumSize / group.Frames.Count);
                    if (overlap >= smaller * 0.35 || Math.Abs(min - group.SumMinimum / group.Frames.Count) <= smaller * 0.20)
                    { found = group; break; }
                }
                if (found == null) { found = new AxisGroup(); groups.Add(found); }
                found.Add(frame, min, max);
            }
            var sortedGroups = rows ? groups.OrderByDescending(g => g.SumCenter / g.Frames.Count)
                : groups.OrderBy(g => g.SumCenter / g.Frames.Count);
            var result = new List<PlotFrame>(frames.Count);
            foreach (var group in sortedGroups)
                result.AddRange(rows ? group.Frames.OrderBy(f => f.MinX) : group.Frames.OrderByDescending(f => f.MaxY));
            return result;
        }

        private sealed class AxisGroup
        {
            public readonly List<PlotFrame> Frames = new List<PlotFrame>();
            public double Minimum = double.PositiveInfinity, Maximum = double.NegativeInfinity;
            public double SumSize, SumMinimum, SumCenter;
            public void Add(PlotFrame frame, double min, double max)
            {
                Frames.Add(frame); Minimum = Math.Min(Minimum, min); Maximum = Math.Max(Maximum, max);
                SumSize += max - min; SumMinimum += min; SumCenter += (min + max) / 2.0;
            }
        }

        /// <summary>
        /// 图号自然排序 (支持 "建施-01", "建施-02", "建施-10" 等非纯数字自然排列)
        /// </summary>
        private static List<PlotFrame> SortByDrawingNumber(List<PlotFrame> frames)
        {
            return frames.OrderBy(f => f.TitleInfo.DrawingNo, new NaturalStringComparer()).ToList();
        }

        private class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return -1;
                if (y == null) return 1;

                return NaturalCompare(x, y);
            }

            private static int NaturalCompare(string a, string b)
            {
                int ai = 0, bi = 0;
                while (ai < a.Length && bi < b.Length)
                {
                    int ae = EndOfRun(a, ai), be = EndOfRun(b, bi);
                    int comparison;
                    if (AsciiDigits(a, ai, ae) && AsciiDigits(b, bi, be))
                    {
                        int an = ai, bn = bi;
                        while (an < ae && a[an] == '0') an++;
                        while (bn < be && b[bn] == '0') bn++;
                        comparison = (ae - an).CompareTo(be - bn);
                        if (comparison == 0) comparison = string.CompareOrdinal(a, an, b, bn, ae - an);
                    }
                    else
                    {
                        int length = Math.Min(ae - ai, be - bi);
                        comparison = string.Compare(a, ai, b, bi, length, StringComparison.OrdinalIgnoreCase);
                        if (comparison == 0) comparison = (ae - ai).CompareTo(be - bi);
                    }
                    if (comparison != 0) return comparison;
                    ai = ae; bi = be;
                }
                return (ai < a.Length ? 1 : 0).CompareTo(bi < b.Length ? 1 : 0);
            }
            private static int EndOfRun(string value, int start)
            {
                bool digit = char.IsDigit(value[start]); int end = start + 1;
                while (end < value.Length && char.IsDigit(value[end]) == digit) end++;
                return end;
            }
            private static bool AsciiDigits(string value, int start, int end)
            {
                for (int i = start; i < end; i++) if (value[i] < '0' || value[i] > '9') return false;
                return true;
            }

        }
    }
}

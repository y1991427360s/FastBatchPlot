using System.Collections.Generic;
using System;
using System.Linq;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class FrameSorterTests
    {
        [Theory]
        [InlineData(true)] [InlineData(false)]
        public void CachedGroupingPreservesMixedPaperOrdering(bool rows)
        {
            var random = new Random(41827);
            for (int trial = 0; trial < 8; trial++)
            {
                var frames = Enumerable.Range(0, 350).Select(i => {
                    double x = random.Next(0, 15) * 500 + random.Next(-10, 11);
                    double y = random.Next(0, 15) * 400 + random.Next(-10, 11);
                    return new PlotFrame { Id = i, MinX = x, MinY = y, MaxX = x + new[] { 210, 297, 420, 594 }[random.Next(4)],
                        MaxY = y + new[] { 210, 297, 420 }[random.Next(3)] };
                }).ToList();
                // 独立参照保留旧规则的逐组计算，检查缓存优化不改变重叠、底线和稳定排序语义。
                Func<PlotFrame, double> min = f => rows ? f.MinY : f.MinX;
                Func<PlotFrame, double> max = f => rows ? f.MaxY : f.MaxX;
                var groups = new List<List<PlotFrame>>();
                foreach (var frame in rows ? frames.OrderByDescending(f => f.MaxY) : frames.OrderBy(f => f.MinX))
                {
                    var group = groups.FirstOrDefault(g => {
                        double smaller = Math.Min(max(frame) - min(frame), g.Average(f => max(f) - min(f)));
                        return Math.Max(0, Math.Min(max(frame), g.Max(max)) - Math.Max(min(frame), g.Min(min))) >= smaller * 0.35
                            || Math.Abs(min(frame) - g.Average(min)) <= smaller * 0.20;
                    });
                    if (group == null) { group = new List<PlotFrame>(); groups.Add(group); }
                    group.Add(frame);
                }
                var ordered = rows ? groups.OrderByDescending(g => g.Average(f => (min(f) + max(f)) / 2))
                    : groups.OrderBy(g => g.Average(f => (min(f) + max(f)) / 2));
                var expected = ordered.SelectMany(g => rows ? g.OrderBy(f => f.MinX) : g.OrderByDescending(f => f.MaxY)).ToList();
                Assert.Equal(expected, FrameSorter.Sort(frames, rows ? SortOrderRule.LeftToRight_TopToBottom : SortOrderRule.TopToBottom_LeftToRight));
            }
        }

        [Theory]
        [InlineData("电施-99999999999999999999", "电施-100000000000000000000")]
        [InlineData("电施-000000000000000000000002", "电施-10")]
        public void NaturalSortHandlesArbitrarilyLongNumbers(string smaller, string larger)
        {
            var first = new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingNo = smaller } };
            var second = new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingNo = larger } };
            Assert.Equal(new[] { first, second }, FrameSorter.Sort(new List<PlotFrame> { second, first }, SortOrderRule.ByDrawingNo));
        }

        [Fact]
        public void EqualNumericValuesPreserveInputOrderAndCompareFollowingText()
        {
            var names = new[] { "电施-0002-b", "电施-02-A", "电施-2-a", "电施-2" };
            var frames = new List<PlotFrame>();
            foreach (var name in names) frames.Add(new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingNo = name } });
            Assert.Equal(new[] { frames[3], frames[1], frames[2], frames[0] }, FrameSorter.Sort(frames, SortOrderRule.ByDrawingNo));
        }
        [Fact]
        public void TemplatePriorityUsesNumericPriorityThenNaturalDrawingNumber()
        {
            var catalog = new PlotFrame { TemplatePriority = 1, SourceBlockName = "Z", TitleInfo = new TitleBlockInfo { DrawingNo = "99" } };
            var drawing10 = new PlotFrame { TemplatePriority = 2, SourceBlockName = "A", TitleInfo = new TitleBlockInfo { DrawingNo = "电施-10" } };
            var drawing2 = new PlotFrame { TemplatePriority = 2, SourceBlockName = "B", TitleInfo = new TitleBlockInfo { DrawingNo = "电施-2" } };
            var result = FrameSorter.Sort(new List<PlotFrame> { drawing10, catalog, drawing2 }, SortOrderRule.ByBlockPriority);
            Assert.Equal(new[] { catalog, drawing2, drawing10 }, result);
        }

        [Fact]
        public void Sort_LeftToRightTopToBottom_ShouldOrderGridCorrectly()
        {
            // 模拟 2行 x 2列 图纸排版
            // 第一行 (顶部，Y 较大):
            //   左上图框: X=0..84100, Y=60000..120000
            //   右上图框: X=90000..174100, Y=60100..120100 (轻微高差100mm)
            // 第二行 (底部，Y 较小):
            //   左下图框: X=0..84100, Y=0..60000
            //   右下图框: X=90000..174100, Y=-50..59950 (轻微高差50mm)

            var frameLT = new PlotFrame { Id = 1, MinX = 0, MaxX = 84100, MinY = 60000, MaxY = 120000, TitleInfo = new TitleBlockInfo { DrawingNo = "LT" } };
            var frameRT = new PlotFrame { Id = 2, MinX = 90000, MaxX = 174100, MinY = 60100, MaxY = 120100, TitleInfo = new TitleBlockInfo { DrawingNo = "RT" } };
            var frameLB = new PlotFrame { Id = 3, MinX = 0, MaxX = 84100, MinY = 0, MaxY = 60000, TitleInfo = new TitleBlockInfo { DrawingNo = "LB" } };
            var frameRB = new PlotFrame { Id = 4, MinX = 90000, MaxX = 174100, MinY = -50, MaxY = 59950, TitleInfo = new TitleBlockInfo { DrawingNo = "RB" } };

            // 乱序输入
            var rawList = new List<PlotFrame> { frameRB, frameLT, frameLB, frameRT };

            var sorted = FrameSorter.Sort(rawList, SortOrderRule.LeftToRight_TopToBottom);

            Assert.Equal("LT", sorted[0].TitleInfo.DrawingNo);
            Assert.Equal("RT", sorted[1].TitleInfo.DrawingNo);
            Assert.Equal("LB", sorted[2].TitleInfo.DrawingNo);
            Assert.Equal("RB", sorted[3].TitleInfo.DrawingNo);
        }

        [Fact]
        public void Sort_ByDrawingNo_ShouldNaturalSort()
        {
            var f1 = new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingNo = "建施-01" } };
            var f2 = new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingNo = "建施-02" } };
            var f10 = new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingNo = "建施-10" } };
            var f9 = new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingNo = "建施-09" } };

            var list = new List<PlotFrame> { f10, f2, f9, f1 };

            var sorted = FrameSorter.Sort(list, SortOrderRule.ByDrawingNo);

            Assert.Equal("建施-01", sorted[0].TitleInfo.DrawingNo);
            Assert.Equal("建施-02", sorted[1].TitleInfo.DrawingNo);
            Assert.Equal("建施-09", sorted[2].TitleInfo.DrawingNo);
            Assert.Equal("建施-10", sorted[3].TitleInfo.DrawingNo);
        }

        [Fact]
        public void Sort_MixedPaperHeights_ShouldRemainInSameRowAndOrderLeftToRight()
        {
            // 同一行排版，底边均在 Y=0:
            // 图1 (A1 横向): X=0..84100, Y=0..59400 (高度 59400)
            // 图2 (A3 横向): X=90000..132000, Y=0..29700 (高度 29700, 仅为A1的一半高度)
            // 图3 (A2 横向): X=140000..199400, Y=0..42000 (高度 42000)

            var frameA1 = new PlotFrame { Id = 1, MinX = 0, MaxX = 84100, MinY = 0, MaxY = 59400, TitleInfo = new TitleBlockInfo { DrawingNo = "A1_Frame" } };
            var frameA3 = new PlotFrame { Id = 2, MinX = 90000, MaxX = 132000, MinY = 0, MaxY = 29700, TitleInfo = new TitleBlockInfo { DrawingNo = "A3_Frame" } };
            var frameA2 = new PlotFrame { Id = 3, MinX = 140000, MaxX = 199400, MinY = 0, MaxY = 42000, TitleInfo = new TitleBlockInfo { DrawingNo = "A2_Frame" } };

            // 乱序输入
            var rawList = new List<PlotFrame> { frameA2, frameA1, frameA3 };

            var sorted = FrameSorter.Sort(rawList, SortOrderRule.LeftToRight_TopToBottom);

            // 验证3张图均被正确归入同一行，并按水平坐标 X 严格从左到右排列
            Assert.Equal("A1_Frame", sorted[0].TitleInfo.DrawingNo);
            Assert.Equal("A3_Frame", sorted[1].TitleInfo.DrawingNo);
            Assert.Equal("A2_Frame", sorted[2].TitleInfo.DrawingNo);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class LayoutIsolationTests
    {
        private static RawPolylineCandidate Rectangle(string document, string space, string handle)
            => new RawPolylineCandidate {
                SourceDocumentId = document, SourceLayoutId = space, Handle = handle,
                LayoutName = "布局" + space, LayoutOrder = 3,
                Vertices = new List<Point2D> { new Point2D(0,0), new Point2D(841,0), new Point2D(841,594), new Point2D(0,594) }
            };

        [Fact]
        public void PolylineDeduplicationMustNotLoseIdenticalFramesInOtherLayoutsOrDocuments()
        {
            var result = PolylineFrameDetector.FilterAndCreateFrames(new[] {
                Rectangle("doc1", "A", "1"), Rectangle("doc1", "A", "2"),
                Rectangle("doc1", "B", "3"), Rectangle("doc2", "A", "4")
            });
            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { "1", "3", "4" }, result.Select(f => f.HandleOrId));
            Assert.All(result, f => Assert.Equal(3, f.LayoutOrder));
            Assert.Equal("布局B", result[1].LayoutName);
        }

        [Fact]
        public void BlockDetectionPreservesLayoutIdentityAndOrder()
        {
            var frame = Assert.Single(BlockFrameDetector.ProcessBlockCandidates(new[] {
                new RawBlockCandidate { SourceDocumentId = "doc", SourceLayoutId = "AB", LayoutName = "电气平面", LayoutOrder = 7,
                    Bounds = new Rect2D(0,0,841,594) }
            }));
            Assert.Equal("AB", frame.SourceLayoutId);
            Assert.Equal("电气平面", frame.LayoutName);
            Assert.Equal(7, frame.LayoutOrder);
        }

        [Theory]
        [InlineData(SortOrderRule.LeftToRight_TopToBottom)]
        [InlineData(SortOrderRule.TopToBottom_LeftToRight)]
        public void LayoutTabOrderTakesPrecedenceOverUnrelatedSpaceCoordinates(SortOrderRule rule)
        {
            var a = new PlotFrame { Id = 1, SourceDocumentId = "doc", SourceLayoutId = "A", LayoutOrder = 1,
                MinX = 10000, MinY = -10000, MaxX = 10841, MaxY = -9406, TitleInfo = new TitleBlockInfo { DrawingNo = "99" } };
            var b = new PlotFrame { Id = 2, SourceDocumentId = "doc", SourceLayoutId = "B", LayoutOrder = 2,
                MinX = 0, MinY = 10000, MaxX = 841, MaxY = 10594, TitleInfo = new TitleBlockInfo { DrawingNo = "01" } };
            var model = new PlotFrame { Id = 0, SourceDocumentId = "doc", SourceLayoutId = "M", LayoutOrder = 0 };
            var input = new List<PlotFrame> { b, a, model };
            var result = FrameSorter.Sort(input, rule);
            Assert.Equal(new[] { 0, 1, 2 }, result.Select(f => f.Id));
            Assert.Equal(new[] { 1, 2, 3 }, result.Select(f => f.OrderIndex));
            Assert.Same(b, input[0]);
        }

        [Fact]
        public void DrawingNumberSortIsGlobalAcrossLayoutsAndFiles()
        {
            var a = new PlotFrame { Id = 1, SourceDocumentId = "doc1", SourceLayoutId = "A", LayoutOrder = 1, MaxX = 841, MaxY = 594,
                TitleInfo = new TitleBlockInfo { DrawingNo = "电-02" } };
            var b = new PlotFrame { Id = 2, SourceDocumentId = "doc1", SourceLayoutId = "B", LayoutOrder = 2, MaxX = 841, MaxY = 594,
                TitleInfo = new TitleBlockInfo { DrawingNo = "电-01" } };
            var c = new PlotFrame { Id = 3, SourceDocumentId = "doc2", SourceLayoutId = "M", LayoutOrder = 0, MaxX = 841, MaxY = 594,
                TitleInfo = new TitleBlockInfo { DrawingNo = "电-00" } };
            var result = FrameSorter.Sort(new List<PlotFrame> { a, b, c }, SortOrderRule.ByDrawingNo);
            Assert.Equal(new[] { 3, 2, 1 }, result.Select(f => f.Id));
            Assert.Equal(new[] { 1, 2, 3 }, result.Select(f => f.OrderIndex));
        }

        [Fact]
        public void AreaThresholdIsRelativeToEachLayout()
        {
            var large = new PlotFrame { SourceDocumentId = "doc", SourceLayoutId = "model", MaxX = 84100, MaxY = 59400 };
            var small = new PlotFrame { SourceDocumentId = "doc", SourceLayoutId = "paper", MaxX = 420, MaxY = 297 };
            var result = FrameSelectionService.Select(new[] { large, small }, new FrameSelectionOptions { MinimumAreaPercent = 50 });
            Assert.Equal(2, result.Frames.Count);
            Assert.Equal(0, result.AreaFilteredCount);
        }
    }
}

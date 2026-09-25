using System;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class FrameSelectionTests
    {
        [Fact]
        public void SingleFrameSortReturnsIndependentListAndRenumbers()
        {
            var frame = Frame(0,420,297); frame.OrderIndex = 8;
            var source = new System.Collections.Generic.List<PlotFrame> { frame };
            var sorted = FrameSorter.Sort(source,SortOrderRule.ByDrawingNo);
            source.Clear();
            Assert.Single(sorted); Assert.Equal(1,sorted[0].OrderIndex);
        }
        [Fact]
        public void SameCoordinatesInDifferentLayoutsMustNotBeDeduplicated()
        {
            var first = Frame(0, 1189, 841); var second = Frame(0, 1189, 841);
            first.SourceLayoutId = "model"; second.SourceLayoutId = "layout";
            Assert.Equal(2, FrameSelectionService.Select(new[] {first,second}, new FrameSelectionOptions()).Frames.Count);
        }

        [Fact]
        public void AreaFilterCanBeDisabledOrExplicitlyEnabled()
        {
            var input = new[] { Frame(0,1189,841), Frame(2000,297,210) };
            Assert.Equal(2, FrameSelectionService.Select(input,new FrameSelectionOptions()).Frames.Count);
            var result = FrameSelectionService.Select(input,new FrameSelectionOptions { MinimumAreaPercent = 10 });
            Assert.Single(result.Frames); Assert.Equal(1,result.AreaFilteredCount);
        }

        [Fact]
        public void LayerFilterRunsBeforeDuplicateRemoval()
        {
            var outer = Frame(0,420,297); outer.SourceLayer = "外框";
            var other = Frame(0,420,297); other.SourceLayer = "需要打印";
            var result = FrameSelectionService.Select(new[] {outer,other},new FrameSelectionOptions {LayerName="需要打印"});
            Assert.Single(result.Frames); Assert.Same(other,result.Frames[0]);
        }

        [Fact]
        public void ExplicitBlockMustNeverFallBackToOtherCandidates()
            => Assert.Empty(FrameSelectionService.Select(new[] {Frame(0,420,297)},new FrameSelectionOptions {BlockName="missing"}).Frames);

        [Fact]
        public void ManualNonstandardWindowPreservesPhysicalDimensionsAndSource()
        {
            var frame = ManualFrameFactory.Create(new Rect2D(50000,10000,0,0),100,"doc","space","Model");
            Assert.Equal(500,frame.DetectedPaper.WidthMm); Assert.Equal(100,frame.DetectedPaper.HeightMm);
            Assert.Equal("自定义",frame.DetectedPaper.Name); Assert.Equal("doc",frame.SourceDocumentId);
            Assert.Equal(FrameType.PickWindow,frame.Type);
        }

        [Fact]
        public void ManualStandardWindowMatchesPaperWithoutGuessingScale()
        {
            var frame = ManualFrameFactory.Create(new Rect2D(0,0,42000,29700),100,"doc","space","Model");
            Assert.Equal("A3",frame.DetectedPaper.Name); Assert.Equal(100,frame.CalculatedScale);
        }

        [Theory]
        [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
        public void ManualInvalidScaleIsRejected(double scale)
            => Assert.Throws<ArgumentException>(() => ManualFrameFactory.Create(new Rect2D(0,0,100,100),scale,"doc","space","Model"));

        private static PlotFrame Frame(double x,double width,double height) => new PlotFrame {
            MinX=x,MinY=0,MaxX=x+width,MaxY=height,SourceDocumentId="doc",SourceLayoutId="model"
        };
    }
}

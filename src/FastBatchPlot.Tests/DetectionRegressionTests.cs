using System.Collections.Generic;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Paper;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class DetectionRegressionTests
    {
        [Fact]
        public void EditingPaperMustNotChangeOtherFramesOrCatalog()
        {
            var first = PaperSizeDetector.Detect(841, 594, 1).Paper;
            var second = PaperSizeDetector.Detect(841, 594, 1).Paper;
            first.Name = "A3";
            first.WidthMm = 420;
            Assert.Equal("A1", second.Name);
            Assert.Equal(841, second.WidthMm);
            Assert.Contains(PaperSize.StandardSizes, p => p.Name == "A1" && p.WidthMm == 841);
        }

        [Fact]
        public void ModelSizedBlockInsertedAtOneMustStillBeRecognized()
        {
            var frames = BlockFrameDetector.ProcessBlockCandidates(new[] {
                new RawBlockCandidate { Bounds = new Rect2D(0, 0, 84100, 59400), ScaleX = 1, ScaleY = 1 }
            });
            Assert.Single(frames);
            Assert.Equal("A1", frames[0].DetectedPaper.Name);
            Assert.Equal(100, frames[0].CalculatedScale);
        }

        [Fact]
        public void InvalidOuterRectangleMustNotSuppressValidInnerFrame()
        {
            var frames = PolylineFrameDetector.FilterAndCreateFrames(new[] {
                Rectangle("outer", 0, 0, 440, 350), Rectangle("drawing", 10, 20, 430, 317)
            });
            Assert.Single(frames);
            Assert.Equal("drawing", frames[0].HandleOrId);
        }

        private static RawPolylineCandidate Rectangle(string handle, double x1, double y1, double x2, double y2)
            => new RawPolylineCandidate { Handle = handle, Vertices = new List<Point2D> {
                new Point2D(x1,y1), new Point2D(x2,y1), new Point2D(x2,y2), new Point2D(x1,y2)
            }};
    }
}

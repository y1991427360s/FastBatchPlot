using System.Collections.Generic;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PolylineDetectorTests
    {
        [Fact]
        public void FilterAndCreateFrames_ShouldFilterInnerBorderAndDuplicates()
        {
            // 模拟图纸中经常存在的双层边框：
            // 外边框: 84100 x 59400 (A1 1:100)
            // 内边框: 82100 x 57400 (内缩留白边线，完全包含在外框内部，面积占比 > 85%)
            // 另外一个完全独立的A2图框: 59400 x 42000

            var outerA1 = new RawPolylineCandidate
            {
                Handle = "H_OUTER_A1",
                Layer = "TK_OUTER",
                Vertices = new List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(84100, 0),
                    new Point2D(84100, 59400),
                    new Point2D(0, 59400)
                }
            };

            var innerA1 = new RawPolylineCandidate
            {
                Handle = "H_INNER_A1",
                Layer = "TK_INNER",
                Vertices = new List<Point2D>
                {
                    new Point2D(1000, 1000),
                    new Point2D(83100, 1000),
                    new Point2D(83100, 58400),
                    new Point2D(1000, 58400)
                }
            };

            var a2 = new RawPolylineCandidate
            {
                Handle = "H_A2",
                Layer = "TK_OUTER",
                Vertices = new List<Point2D>
                {
                    new Point2D(100000, 0),
                    new Point2D(159400, 0),
                    new Point2D(159400, 42000),
                    new Point2D(100000, 42000)
                }
            };

            var candidates = new List<RawPolylineCandidate> { innerA1, outerA1, a2 };

            var frames = PolylineFrameDetector.FilterAndCreateFrames(candidates);

            // 期望结果：内框被智能去重过滤，只保留 2 个图框 (外框 A1 和 A2)
            Assert.Equal(2, frames.Count);
            Assert.Contains(frames, f => f.HandleOrId == "H_OUTER_A1");
            Assert.DoesNotContain(frames, f => f.HandleOrId == "H_INNER_A1");
            Assert.Contains(frames, f => f.HandleOrId == "H_A2");
        }

        [Fact]
        public void IsOrthogonalRectangle_ShouldRejectTiltedPolygonsAndAcceptOrthogonal()
        {
            // 正交矩形
            var ortho = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 50),
                new Point2D(0, 50)
            };
            Assert.True(GeometryUtils.IsOrthogonalRectangle(ortho));

            // 闭合5点多边形 (第5点与第1点重合)
            var ortho5 = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(100, 0),
                new Point2D(100, 50),
                new Point2D(0, 50),
                new Point2D(0, 0)
            };
            Assert.True(GeometryUtils.IsOrthogonalRectangle(ortho5));

            // 45度倾斜正方形 (虽然是矩形但非正交)
            var tilted45 = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(100, 100),
                new Point2D(0, 200),
                new Point2D(-100, 100)
            };
            Assert.False(GeometryUtils.IsOrthogonalRectangle(tilted45));
        }
    
        [Fact]
        public void RectangleWithCollinearExtraVerticesIsDetected()
        {
            var cand = new RawPolylineCandidate { Handle = "mid", SourceDocumentId = "doc", SourceLayoutId = "model", Vertices = new List<Point2D> {
                new Point2D(0, 0), new Point2D(420, 0), new Point2D(841, 0), new Point2D(841, 594), new Point2D(0, 594), new Point2D(0, 300) } };
            var frames = PolylineFrameDetector.CreateCandidates(new[] { cand });
            Assert.Single(frames);
            Assert.Equal("A1", frames[0].DetectedPaper.StandardName);
        }

        [Fact]
        public void ClosingGapWithinHostToleranceIsAccepted()
        {
            var cand = new RawPolylineCandidate { Handle = "gap", SourceDocumentId = "doc", SourceLayoutId = "model", Vertices = new List<Point2D> {
                new Point2D(0, 0), new Point2D(841, 0), new Point2D(841, 594), new Point2D(0, 594), new Point2D(0, 1.5) } };
            Assert.Single(PolylineFrameDetector.CreateCandidates(new[] { cand }));
        }
}
}

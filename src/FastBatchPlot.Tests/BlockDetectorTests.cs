using System.Collections.Generic;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class BlockDetectorTests
    {
        [Fact]
        public void ProcessBlockCandidates_ShouldUseBlockScaleFactorAsScaleHint()
        {
            // 模拟一个图块，定义图框尺寸为 A1 (841 x 594 mm)，插入比例为 150
            // 则图形中实际包围盒为 841*150=126150 x 594*150=89100
            var candidate = new RawBlockCandidate
            {
                Handle = "BLK_A1_150",
                BlockName = "STANDARD_A1",
                Layer = "TK_BLOCK",
                ScaleX = 150.0,
                ScaleY = 150.0,
                Bounds = new Rect2D(0, 0, 126150, 89100),
                Attributes = new Dictionary<string, string>
                {
                    { "图号", "建施-08" },
                    { "图名", "立面图" },
                    { "比例", "1:150" }
                }
            };

            var frames = BlockFrameDetector.ProcessBlockCandidates(new List<RawBlockCandidate> { candidate });

            Assert.Single(frames);
            var frame = frames[0];
            Assert.Equal("A1", frame.DetectedPaper.StandardName);
            Assert.Equal(150.0, frame.CalculatedScale);
            Assert.Equal("建施-08", frame.TitleInfo.DrawingNo);
            Assert.Equal("立面图", frame.TitleInfo.DrawingName);
        }
    }
}

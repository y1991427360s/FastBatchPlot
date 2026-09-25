using FastBatchPlot.Core.Common;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class AciColorHelperTests
    {
        // 固定参考表中的已知值，覆盖基础色之外的色环、阴影和灰阶。
        [Theory]
        [InlineData(10, 255, 0, 0)]
        [InlineData(11, 255, 127, 127)]
        [InlineData(12, 165, 0, 0)]
        [InlineData(18, 38, 0, 0)]
        [InlineData(20, 255, 63, 0)]
        [InlineData(30, 255, 127, 0)]
        [InlineData(29, 38, 23, 19)]
        [InlineData(90, 0, 255, 0)]
        [InlineData(91, 127, 255, 127)]
        [InlineData(145, 63, 111, 127)]
        [InlineData(170, 0, 0, 255)]
        [InlineData(192, 82, 0, 165)]
        [InlineData(203, 145, 82, 165)]
        [InlineData(250, 0, 0, 0)]
        [InlineData(251, 101, 101, 101)]
        [InlineData(254, 204, 204, 204)]
        public void GetAciRgb_ShouldUseDocumentedReferenceValues(short aci, byte r, byte g, byte b)
        {
            Assert.Equal((r, g, b), AciColorHelper.GetAciRgb(aci));
        }

        [Theory]
        [InlineData(255, 127, 0, 30)]
        [InlineData(165, 0, 0, 12)]
        [InlineData(0, 0, 0, 250)]
        public void RgbToAci_ShouldMatchNonPrimaryReferenceColors(byte r, byte g, byte b, short aci)
        {
            Assert.Equal(aci, AciColorHelper.RgbToAci(r, g, b));
        }

        [Theory]
        [InlineData(255, 0, 0, 1)]       // 纯红 -> ACI 1 (Red)
        [InlineData(255, 255, 0, 2)]     // 纯黄 -> ACI 2 (Yellow)
        [InlineData(0, 255, 0, 3)]       // 纯绿 -> ACI 3 (Green)
        [InlineData(0, 255, 255, 4)]     // 纯青 -> ACI 4 (Cyan)
        [InlineData(0, 0, 255, 5)]       // 纯蓝 -> ACI 5 (Blue)
        [InlineData(255, 0, 255, 6)]     // 洋红 -> ACI 6 (Magenta)
        [InlineData(255, 255, 255, 7)]   // 白色 -> ACI 7 (White/Black)
        [InlineData(128, 128, 128, 8)]   // 深灰 -> ACI 8 (Dark Gray)
        [InlineData(192, 192, 192, 9)]   // 浅灰 -> ACI 9 (Light Gray)
        public void RgbToAci_ShouldMapStandardColorsCorrectly(byte r, byte g, byte b, short expectedAci)
        {
            short aci = AciColorHelper.RgbToAci(r, g, b);
            Assert.Equal(expectedAci, aci);
        }

        [Fact]
        public void RgbToAci_ShouldMapNearRedToAci1()
        {
            // 稍有偏差的近红色 (250, 5, 2)
            short aci = AciColorHelper.RgbToAci(250, 5, 2);
            Assert.Equal(1, aci);
        }
    }
}

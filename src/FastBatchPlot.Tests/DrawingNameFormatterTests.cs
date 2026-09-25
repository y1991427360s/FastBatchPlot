using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class DrawingNameFormatterTests
    {
        [Fact]
        public void FieldValuesAreNotInterpretedAsNestedTemplates()
        {
            var frame = new PlotFrame { OrderIndex=7, TitleInfo=new TitleBlockInfo { DrawingNo="{DwgName}",DrawingName="{Date}",Date="2026" } };
            Assert.Equal("{DwgName}_{Date}_2026",DrawingNameFormatter.Format("{DwgNo}_{DwgName}_{Date}",frame));
        }
        [Fact]
        public void HugePrecisionDoesNotAllocateOrCrash()
        {
            Assert.Equal("{Index_D999999999}",DrawingNameFormatter.Format("{Index:D999999999}",new PlotFrame()));
            Assert.Equal("{Index_D0}",DrawingNameFormatter.Format("{Index:D0}",new PlotFrame()));
        }
        [Theory]
        [InlineData("预算$&版")]
        [InlineData("预算$$版")]
        [InlineData("预算$'版")]
        [InlineData("预算$`版")]
        public void Format_ShouldKeepRegexReplacementSequencesLiteral(string value)
        {
            var frame = new PlotFrame
            {
                TitleInfo = new TitleBlockInfo
                {
                    DrawingNo = value, DrawingName = value, ProjectName = value,
                    Date = value, Revision = value
                },
                DetectedPaper = new PaperSize(value, 420, 297)
            };
            foreach (var token in new[] { "DwgNo", "DwgName", "ProjectName", "Date", "Rev", "PaperSize", "DwgFileName" })
                Assert.Equal(value, DrawingNameFormatter.Format("{" + token + "}", frame, value + ".dwg"));
        }

        [Fact]
        public void Format_ShouldPreserveFractionalScale()
        {
            var frame = new PlotFrame { CalculatedScale = 2.5 };
            Assert.Equal("1-2.5", DrawingNameFormatter.Format("{Scale}", frame));
            Assert.Contains("1:2.5", frame.ToString());
        }

        [Fact]
        public void Format_ShouldReplaceAllTokensAndSanitize()
        {
            var frame = new PlotFrame
            {
                OrderIndex = 3,
                DetectedPaper = new PaperSize("A1", 841, 594, true),
                CalculatedScale = 100,
                TitleInfo = new TitleBlockInfo
                {
                    DrawingNo = "S/01:A",       // 包含非法字符 / 和 :
                    DrawingName = "标准层*平面图?", // 包含非法字符 * 和 ?
                    ProjectName = "科技示范园区",
                    Revision = "B",
                    Date = "2026.09"
                }
            };

            string template = "{Index:D3}_{ProjectName}_{DwgNo}_{DwgName}_{PaperSize}_{Scale}";
            string result = DrawingNameFormatter.Format(template, frame, "OfficeBuilding.dwg");

            // 检查格式化和非法字符替换
            Assert.Contains("003", result);
            Assert.Contains("科技示范园区", result);
            Assert.DoesNotContain("/", result);
            Assert.DoesNotContain(":", result);
            Assert.DoesNotContain("*", result);
            Assert.DoesNotContain("?", result);
            Assert.Contains("A1", result);
            Assert.Contains("1-100", result);
        }

        [Fact]
        public void Format_ShouldSupportChinesePlaceholderTokens()
        {
            var frame = new PlotFrame
            {
                OrderIndex = 1,
                DetectedPaper = new PaperSize("A2", 594, 420, true),
                CalculatedScale = 50,
                TitleInfo = new TitleBlockInfo
                {
                    DrawingNo = "建施-01",
                    DrawingName = "总平面图",
                    ProjectName = "智慧大厦",
                    Revision = "1",
                    Date = "2026-09"
                }
            };

            string template = "{序号:D2}_{项目名称}_{图号}_{图名}_{图幅}_{比例}";
            string result = DrawingNameFormatter.Format(template, frame, "Building.dwg");

            Assert.Equal("01_智慧大厦_建施-01_总平面图_A2_1-50", result);
        }
    }
}

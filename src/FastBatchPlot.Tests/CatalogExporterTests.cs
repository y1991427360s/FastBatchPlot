using System.Collections.Generic;
using System.IO;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class CatalogExporterTests
    {
        [Fact]
        public void ExportToCsv_ShouldKeepFractionalScaleAndUtf8Bom()
        {
            string path = Path.Combine(Path.GetTempPath(), "CatalogFractional_" + System.Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                CatalogExporter.ExportToCsv(new[] { new PlotFrame { CalculatedScale = 2.5 } }, path);
                byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal(0xEF, bytes[0]);
                Assert.Equal(0xBB, bytes[1]);
                Assert.Equal(0xBF, bytes[2]);
                Assert.Contains(",1:2.5,", File.ReadAllText(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void ExportToCsv_ShouldProduceValidCsvFile()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "Test_Catalog_" + System.Guid.NewGuid().ToString("N") + ".csv");

            try
            {
                var frames = new List<PlotFrame>
                {
                    new PlotFrame
                    {
                        OrderIndex = 1,
                        DetectedPaper = new PaperSize("A1", 841, 594, true),
                        CalculatedScale = 100,
                        Status = "待打印",
                        TitleInfo = new TitleBlockInfo
                        {
                            DrawingNo = "建施-01",
                            DrawingName = "总平面图",
                            Revision = "A",
                            Date = "2026-09-14"
                        }
                    },
                    new PlotFrame
                    {
                        OrderIndex = 2,
                        DetectedPaper = new PaperSize("A2", 594, 420, true),
                        CalculatedScale = 50,
                        Status = "待打印",
                        TitleInfo = new TitleBlockInfo
                        {
                            DrawingNo = "建施-02",
                            DrawingName = "一层平面图",
                            Revision = "A",
                            Date = "2026-09-14"
                        }
                    }
                };

                CatalogExporter.ExportToCsv(frames, tempFile);

                Assert.True(File.Exists(tempFile));
                string content = File.ReadAllText(tempFile, System.Text.Encoding.UTF8);

                Assert.Contains("建施-01", content);
                Assert.Contains("总平面图", content);
                Assert.Contains("A1", content);
                Assert.Contains("建施-02", content);
                Assert.Contains("一层平面图", content);
                Assert.Contains("【图幅统计汇总】", content);
                Assert.Contains("A1,1 张", content);
                Assert.Contains("A2,1 张", content);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}

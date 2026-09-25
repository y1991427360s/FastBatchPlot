using System.Collections.Generic;
using System.IO;
using FastBatchPlot.Core.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PdfMergerTests
    {
        [Fact]
        public void MergePdfFiles_ShouldMergeWithBookmarksCorrectly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FastBatchPlot_Test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 创建测试用的两个临时 PDF 文件
                string pdf1 = Path.Combine(tempDir, "doc1.pdf");
                string pdf2 = Path.Combine(tempDir, "doc2.pdf");

                CreateSamplePdf(pdf1, "Page 1 - A1 Floor Plan");
                CreateSamplePdf(pdf2, "Page 2 - A2 Detail Section");

                string outputPdf = Path.Combine(tempDir, "Merged_Result.pdf");

                var items = new List<PdfMergeItem>
                {
                    new PdfMergeItem(pdf1, "01 建施-01 一层平面图"),
                    new PdfMergeItem(pdf2, "02 结施-01 节点大样图")
                };

                bool success = PdfMerger.MergePdfFiles(items, outputPdf, out string error);

                Assert.True(success, error);
                Assert.True(File.Exists(outputPdf));

                // 验证合并后的 PDF 页数与书签
                using (var mergedDoc = PdfReader.Open(outputPdf, PdfDocumentOpenMode.Import))
                {
                    Assert.Equal(2, mergedDoc.PageCount);
                    Assert.Equal(2, mergedDoc.Outlines.Count);
                    Assert.Equal("01 建施-01 一层平面图", mergedDoc.Outlines[0].Title);
                    Assert.Equal("02 结施-01 节点大样图", mergedDoc.Outlines[1].Title);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void RotatePdf_ShouldSupportPositiveAndNegativeAnglesCorrectly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FastBatchPlot_RotTest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string testPdf = Path.Combine(tempDir, "rotate_sample.pdf");
                CreateSamplePdf(testPdf, "Rotation Test Document");

                // 测试 -90 度旋转 (应规范化为 270 度，而非负数)
                bool okNeg = PdfMerger.RotatePdf(testPdf, -90, out string errNeg);
                Assert.True(okNeg, errNeg);

                using (var doc = PdfReader.Open(testPdf, PdfDocumentOpenMode.Import))
                {
                    Assert.Equal(270, doc.Pages[0].Rotate);
                }

                // 再次顺时针旋转 90 度 (回到 0 度)
                bool okPos = PdfMerger.RotatePdf(testPdf, 90, out string errPos);
                Assert.True(okPos, errPos);

                using (var doc = PdfReader.Open(testPdf, PdfDocumentOpenMode.Import))
                {
                    Assert.Equal(0, doc.Pages[0].Rotate);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        private static void CreateSamplePdf(string path, string text)
        {
            using (var doc = new PdfDocument())
            {
                var page = doc.AddPage();
                using (var gfx = XGraphics.FromPdfPage(page))
                {
                    var font = new XFont("Arial", 16, XFontStyle.Regular);
                    gfx.DrawString(text, font, XBrushes.Black, new XPoint(50, 100));
                }
                doc.Save(path);
            }
        }
    }
}

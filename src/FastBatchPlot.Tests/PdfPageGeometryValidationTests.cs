using System;
using System.Globalization;
using System.IO;
using System.Text;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class PdfPageGeometryValidationTests : IDisposable
    {
        private const string Media = "[10 20 1200.55118110236 861.889763779528]";
        private readonly string directory = Path.Combine(Path.GetTempPath(), "FastBatchPlotPageGeometry-" + Guid.NewGuid().ToString("N"));

        public PdfPageGeometryValidationTests() { Directory.CreateDirectory(directory); }

        [Theory]
        [InlineData("/UserUnit 2")]
        [InlineData("/UserUnit 0.5")]
        [InlineData("/UserUnit 0")]
        [InlineData("/UserUnit -1")]
        public void NonstandardUserUnitCannotBeCommitted(string pageProperties)
        {
            Reject("", pageProperties);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "/UserUnit 1")]
        [InlineData("/UserUnit 2", "")]
        public void DefaultOrExplicitUnitAndInheritedMediaAreAccepted(string parentProperties, string pageProperties)
        {
            // UserUnit 属于页面，/Pages 上的同名值不是可继承属性。
            Accept(parentProperties, pageProperties);
        }

        [Fact]
        public void SmallerInheritedCropBoxCannotBeCommitted()
        {
            Reject("/CropBox [11 20 1200.55118110236 861.889763779528]", "");
        }

        [Fact]
        public void SameSizeShiftedCropBoxCannotBeCommitted()
        {
            Reject("", "/CropBox [11 20 1201.55118110236 861.889763779528]");
        }

        [Fact]
        public void PageCropBoxOverridesSmallerParentCropBox()
        {
            Accept("/CropBox [11 21 1199 860]", "/CropBox " + Media);
        }

        [Fact]
        public void LargerCropBoxAndHarmlessCoordinateRoundingAreAccepted()
        {
            Accept("", "/CropBox [9 19 1201 862]");
            Accept("", "/CropBox [10.001 20.001 1200.55018110236 861.888763779528]");
        }

        [Fact]
        public void EmptyCropBoxCannotBeCommitted()
        {
            Reject("", "/CropBox [10 20 10 20]");
        }

        [Fact]
        public void InheritedRotationIsAppliedOnce()
        {
            string input = CreatePdf("/Rotate 90", "");
            string target = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".pdf");
            PlotOutputCommitter.ValidateAndCommit(input, target, Plan(portrait: true));
            Assert.True(File.Exists(target));
        }

        [Fact]
        public void InvalidRotationCannotBeCommitted()
        {
            Reject("/Rotate 45", "");
        }

        private void Accept(string parent, string page)
        {
            string input = CreatePdf(parent, page);
            string target = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".pdf");
            PlotOutputCommitter.ValidateAndCommit(input, target, Plan());
            Assert.True(File.Exists(target));
            Assert.False(File.Exists(input));
        }

        private void Reject(string parent, string page)
        {
            string input = CreatePdf(parent, page);
            string target = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".pdf");
            Assert.Throws<IOException>(() => PlotOutputCommitter.ValidateAndCommit(input, target, Plan()));
            Assert.False(File.Exists(target));
            Assert.True(File.Exists(input));
        }

        private static PlotPlan Plan(bool portrait = false)
        {
            return PlotPlanBuilder.Create(new PlotFrame {
                MinX = 0, MinY = 0, MaxX = portrait ? 297 : 420, MaxY = portrait ? 420 : 297,
                CalculatedScale = 1, DetectedPaper = new PaperSize("A3", 420, 297), IsLandscape = !portrait
            }, new PlotConfig { MarginMm = 0 });
        }

        private string CreatePdf(string parentProperties, string pageProperties)
        {
            // 手工生成页面树，避免 PDF 写入器把继承属性提前摊平成页面属性，削弱回归覆盖。
            var objects = new[] {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox " + Media + " " + parentProperties + " >>",
                "<< /Type /Page /Parent 2 0 R /Resources << >> " + pageProperties + " >>"
            };
            var pdf = new StringBuilder("%PDF-1.7\n");
            var offsets = new int[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                offsets[i] = Encoding.ASCII.GetByteCount(pdf.ToString());
                pdf.Append(i + 1).Append(" 0 obj\n").Append(objects[i]).Append("\nendobj\n");
            }
            int xref = Encoding.ASCII.GetByteCount(pdf.ToString());
            pdf.Append("xref\n0 4\n0000000000 65535 f \n");
            foreach (int offset in offsets) pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
            pdf.Append("trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".pdf");
            File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
            return path;
        }

        public void Dispose() { Directory.Delete(directory, true); }
    }
}

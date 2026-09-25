using System;
using System.IO;
using FastBatchPlot.Core.Pdf;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PdfSafetyTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "FastBatchPlotSafety-" + Guid.NewGuid().ToString("N"));
        public PdfSafetyTests() { Directory.CreateDirectory(_directory); }

        [Fact]
        public void FailedMergePreservesExistingOutput()
        {
            var target = Create("existing.pdf");
            var original = File.ReadAllBytes(target);
            var missing = Path.Combine(_directory, "missing.pdf");
            Assert.False(PdfMerger.MergePdfFiles(new[] { new PdfMergeItem(missing, "缺失") }, target, out _));
            Assert.Equal(original, File.ReadAllBytes(target));
            Assert.Empty(Directory.GetFiles(_directory, ".batchplot-*"));
        }

        [Fact]
        public void MergeCannotOverwriteItsOwnInput()
        {
            var source = Create("source.pdf");
            var original = File.ReadAllBytes(source);
            Assert.False(PdfMerger.MergePdfFiles(new[] { new PdfMergeItem(source, "来源") }, source, out _));
            Assert.Equal(original, File.ReadAllBytes(source));
        }

        [Fact]
        public void MergeReplacesExistingOutputOnlyAfterSuccessfulCompletion()
        {
            var source = Create("source.pdf");
            var target = Create("target.pdf");
            Assert.True(PdfMerger.MergePdfFiles(new[] { new PdfMergeItem(source, "一"), new PdfMergeItem(source, "二") }, target, out var error), error);
            using var pdf = PdfReader.Open(target, PdfDocumentOpenMode.Import);
            Assert.Equal(2, pdf.PageCount);
            Assert.Empty(Directory.GetFiles(_directory, ".batchplot-*"));
        }

        [Fact]
        public void InvalidRotationPreservesOriginalBytes()
        {
            var source = Create("source.pdf");
            var original = File.ReadAllBytes(source);
            Assert.False(PdfMerger.RotatePdf(source, 45, out _));
            Assert.Equal(original, File.ReadAllBytes(source));
        }

        private string Create(string name)
        {
            var path = Path.Combine(_directory, name);
            using var pdf = new PdfDocument();
            pdf.AddPage();
            pdf.Save(path);
            return path;
        }

        public void Dispose() { Directory.Delete(_directory, true); }
    }
}

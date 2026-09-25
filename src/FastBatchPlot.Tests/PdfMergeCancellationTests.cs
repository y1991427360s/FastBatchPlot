using System;
using System.IO;
using System.Threading;
using FastBatchPlot.Core.Pdf;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class PdfMergeCancellationTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "PdfMergeCancellation-" + Guid.NewGuid().ToString("N"));
        public PdfMergeCancellationTests() { Directory.CreateDirectory(directory); }
        private string Target => Path.Combine(directory, "merged.pdf");
        private string Input()
        {
            string path = Path.Combine(directory, "single.pdf");
            using (var document = new PdfDocument())
            {
                for (int i = 0; i < 3; i++) { var page = document.AddPage(); page.Width = 300 + i; page.Height = 400 + i; }
                document.Save(path);
            }
            return path;
        }

        [Fact]
        public void PreCancelledMergeDoesNotCreateDirectoriesOrReadMissingInput()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                string output = Path.Combine(directory, "uncreated", "merged.pdf");
                Assert.Throws<OperationCanceledException>(() => PdfMerger.MergePdfFiles(
                    new[] { new PdfMergeItem("missing.pdf", "") }, output, cancellation.Token, out _));
                Assert.False(Directory.Exists(Path.GetDirectoryName(output)));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CancellationBetweenPagesOrBeforeCommitPreservesInputsAndExistingTarget(bool cancelAtCommit)
        {
            string input = Input(); byte[] original = File.ReadAllBytes(input);
            File.WriteAllText(Target, "existing-result");
            using (var cancellation = new CancellationTokenSource())
            {
                int imported = 0; bool reachedCommit = false;
                var progress = new InlineProgress(value =>
                {
                    imported = value.CompletedPages;
                    reachedCommit |= value.ReadyToCommit;
                    if (value.ReadyToCommit == cancelAtCommit) cancellation.Cancel();
                });
                Assert.Throws<OperationCanceledException>(() => PdfMerger.MergePdfFiles(
                    new[] { new PdfMergeItem(input, "原始页") }, Target, cancellation.Token, out _, overwrite: true, progress: progress));
                Assert.Equal(cancelAtCommit ? 3 : 1, imported);
                Assert.Equal(cancelAtCommit, reachedCommit);
                Assert.Equal("existing-result", File.ReadAllText(Target));
                Assert.Equal(original, File.ReadAllBytes(input));
                Assert.Empty(Directory.GetFiles(directory, ".batchplot-*"));
            }
        }

        [Fact]
        public void CancelledMergeDoesNotPublishANewOutput()
        {
            string input = Input();
            using (var cancellation = new CancellationTokenSource())
            {
                var progress = new InlineProgress(value => { if (value.ReadyToCommit) cancellation.Cancel(); });
                Assert.Throws<OperationCanceledException>(() => PdfMerger.MergePdfFiles(
                    new[] { new PdfMergeItem(input, "页") }, Target, cancellation.Token, out _, overwrite: false, progress: progress));
                Assert.False(File.Exists(Target)); Assert.True(File.Exists(input));
                Assert.Empty(Directory.GetFiles(directory, ".batchplot-*"));
            }
        }

        [Fact]
        public void TargetCreatedDuringMergeIsNeverOverwrittenByBatchOutput()
        {
            string input = Input();
            var progress = new InlineProgress(value => { if (value.ReadyToCommit) File.WriteAllText(Target, "another-writer"); });
            Assert.False(PdfMerger.MergePdfFiles(new[] { new PdfMergeItem(input, "页") }, Target,
                CancellationToken.None, out var error, overwrite: false, progress: progress));
            Assert.NotEmpty(error); Assert.Equal("another-writer", File.ReadAllText(Target));
            Assert.True(File.Exists(input)); Assert.Empty(Directory.GetFiles(directory, ".batchplot-*"));
        }

        [Fact]
        public void CancellableApiStillPreservesAllPageSizesAndBookmarksOnSuccess()
        {
            string input = Input();
            Assert.True(PdfMerger.MergePdfFiles(new[] { new PdfMergeItem(input, "三页图纸") }, Target,
                CancellationToken.None, out var error, overwrite: false), error);
            using (var result = PdfReader.Open(Target, PdfDocumentOpenMode.Import))
            {
                Assert.Equal(3, result.PageCount);
                for (int i = 0; i < 3; i++) { Assert.Equal(300 + i, result.Pages[i].Width.Point, 6); Assert.Equal(400 + i, result.Pages[i].Height.Point, 6); }
                Assert.Equal("三页图纸", result.Outlines[0].Title);
            }
            Assert.True(File.Exists(input)); Assert.Empty(Directory.GetFiles(directory, ".batchplot-*"));
        }

        private sealed class InlineProgress : IProgress<PdfMergeProgress>
        {
            private readonly Action<PdfMergeProgress> report;
            public InlineProgress(Action<PdfMergeProgress> report) { this.report = report; }
            public void Report(PdfMergeProgress value) => report(value);
        }
        public void Dispose() { Directory.Delete(directory, true); }
    }
}

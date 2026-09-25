using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.Core.Templates;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class SourceNamingTests
    {
        [Fact]
        public void FileNamesAndBookmarksUseSourceSnapshotBeforeActiveDocument()
        {
            var frame = new PlotFrame { SourceFileName = "原图{Date}$&.dwg", LayoutName = "电气/总图" };
            Assert.Equal("原图{Date}$&_电气_总图", DrawingNameFormatter.Format("{文件名}_{布局}", frame, "错误活动图.dwg"));
            Assert.Equal("原图{Date}$& / 电气/总图", DrawingNameFormatter.FormatBookmark("{DwgFileName} / {Layout}", frame));
            var run = new BatchPlotRun(new[] { new BatchPage(frame, "page.pdf") }, new PlotConfig { BookmarkTemplate = "{文件名}/{布局}" });
            frame.SourceFileName = "更名.dwg"; frame.LayoutName = "修改布局";
            Assert.Equal("原图{Date}$&/电气/总图", PdfBookmarkItems.Create(run.Pages, run.Config)[0].BookmarkTitle);
            Assert.Equal("Drawing", DrawingNameFormatter.FormatBookmark("{文件名}", new PlotFrame()));
        }

        [Fact]
        public void AllDetectionPathsKeepSourceName()
        {
            var block = new RawBlockCandidate { BlockName = "A3框", SourceFileName = "来源.dwg", SourceDocumentId = "doc", SourceLayoutId = "model", Bounds = new Rect2D(0, 0, 420, 297) };
            Assert.Equal("来源.dwg", BlockFrameDetector.ProcessBlockCandidates(new[] { block }).Single().SourceFileName);
            var template = new TitleBlockTemplate { Name = "A3框", BlockName = "A3框", PrintScale = 1, PrintRegion = new TemplateRegion { X2 = 420, Y2 = 297 } };
            var library = new TitleTemplateLibrary(); library.Templates.Add(template);
            Assert.Equal("来源.dwg", TemplateFrameDetector.Detect(new[] { block }, library, (_, __) => new Rect2D(0, 0, 420, 297)).Single().SourceFileName);
            var poly = new RawPolylineCandidate { SourceFileName = "矩形.dwg", Vertices = new List<Point2D> { new Point2D(0, 0), new Point2D(420, 0), new Point2D(420, 297), new Point2D(0, 297) } };
            Assert.Equal("矩形.dwg", PolylineFrameDetector.FilterAndCreateFrames(new[] { poly }.ToList()).Single().SourceFileName);
        }

        [Fact]
        public void HistoryRetainsSourceAndOldHistoryDoesNotGuessCurrentFile()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SourceNaming-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try {
                var frame = new PlotFrame { SourceFileName = "历史原图.dwg", SourceDocumentId = "doc", SourceLayoutId = "model", LayoutName = "布局A", MaxX = 420, MaxY = 297, CalculatedScale = 1 };
                var config = new PlotConfig { OutputDirectory = directory, MergeToSinglePdf = false, BookmarkTemplate = "{文件名}/{布局}" };
                var record = PdfTaskHistory.Create(new BatchPlotRun(new[] { new BatchPage(frame, Path.Combine(directory, "page.pdf")) }, config), "");
                string path = Path.Combine(directory, "task.json"); PdfTaskHistory.Save(path, record);
                var loaded = PdfTaskHistory.Load(path);
                Assert.Equal("历史原图.dwg", loaded.Pages[0].Frame.SourceFileName);
                Assert.Equal("历史原图/布局A", PdfBookmarkItems.Create(PdfTaskHistory.PendingPages(loaded), loaded.Config)[0].BookmarkTitle);
                var json = JsonNode.Parse(File.ReadAllText(path))!; json["Pages"]![0]!["Frame"]!.AsObject().Remove("SourceFileName");
                File.WriteAllText(path, json.ToJsonString()); loaded = PdfTaskHistory.Load(path);
                Assert.Equal("Drawing/布局A", PdfBookmarkItems.Create(PdfTaskHistory.PendingPages(loaded), loaded.Config)[0].BookmarkTitle);
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}

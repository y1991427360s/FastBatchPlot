using System;
using System.IO;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Tasks;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class PdfBookmarkTests
    {
        [Fact]
        public void BookmarksKeepPunctuationAndSnapshotText()
        {
            var frame = new PlotFrame { OrderIndex = 7, CalculatedScale = 2.5,
                TitleInfo = new TitleBlockInfo { DrawingNo = "S/01:A", DrawingName = "图名?{Date}$&", Date = "2026/09" } };
            Assert.Equal("007 S/01:A 图名?{Date}$& 1:2.5 2026/09",
                DrawingNameFormatter.FormatBookmark("{序号:D3} {图号} {图名} {比例} {日期}", frame));
            Assert.Equal("S_01_A_1-2.5", DrawingNameFormatter.Format("{图号}_{比例}", frame));
        }

        [Theory]
        [InlineData("{Wrong}")]
        [InlineData("{Index:D0}")]
        [InlineData("{Index:D999999999}")]
        [InlineData("{DwgNo")]
        [InlineData("{DwgNo}}")]
        [InlineData("{}")]
        public void InvalidTemplatesFailBeforeOutput(string template) =>
            Assert.Throws<ArgumentException>(() => DrawingNameFormatter.FormatBookmark(template, new PlotFrame()));

        [Fact]
        public void BlankAndMissingValuesHaveReadableFallbacks()
        {
            var frame = new PlotFrame { OrderIndex = 3 };
            Assert.Equal("03 图纸3 图面3", DrawingNameFormatter.FormatBookmark("  ", frame));
            Assert.Equal("图纸 3", DrawingNameFormatter.FormatBookmark("{Date}", frame));
            frame.TitleInfo.DrawingName = "甲\r\n乙\0丙";
            Assert.Equal("甲  乙 丙", DrawingNameFormatter.FormatBookmark("{DwgName}", frame));
        }

        [Fact]
        public void ExcessiveExpandedTitlesAreRejected()
        {
            var frame = new PlotFrame { TitleInfo = new TitleBlockInfo { DrawingName = new string('中', 20000) } };
            Assert.Throws<ArgumentException>(() => DrawingNameFormatter.FormatBookmark("{DwgName}{DwgName}", frame));
            Assert.Throws<ArgumentException>(() => DrawingNameFormatter.ValidateBookmarkTemplate(new string('x', 32769)));
        }

        [Fact]
        public void SharedMergeItemsUseFrozenTaskConfigAndFrames()
        {
            var frame = new PlotFrame { OrderIndex = 2, TitleInfo = new TitleBlockInfo { DrawingName = "原图" } };
            var config = new PlotConfig { BookmarkTemplate = "原任务/{Index:D3}:{DwgName}" };
            var run = new BatchPlotRun(new[] { new BatchPage(frame, "page.pdf") }, config);
            config.BookmarkTemplate = "新任务"; frame.TitleInfo.DrawingName = "新图";
            Assert.Equal("原任务/002:原图", PdfBookmarkItems.Create(run.Pages, run.Config)[0].BookmarkTitle);
        }

        [Fact]
        public void PreferencesRoundTripAndOldFilesUseDefault()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "settings.json");
            try
            {
                UserSettingsStore.Save(path, new BatchPlotPreferences { BookmarkTemplate = "{图名} / {比例}" });
                Assert.Equal("{图名} / {比例}", UserSettingsStore.Load(path).BookmarkTemplate);
                string json = File.ReadAllText(path);
                using (var parsed = System.Text.Json.JsonDocument.Parse(json))
                {
                    var fields = new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>();
                    foreach (var property in parsed.RootElement.EnumerateObject())
                        if (property.Name != "bookmarkTemplate") fields.Add(property.Name, property.Value);
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(fields));
                }
                Assert.Equal(DrawingNameFormatter.DefaultBookmarkTemplate, UserSettingsStore.Load(path).BookmarkTemplate);
                var original = File.ReadAllBytes(path);
                Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(path, new BatchPlotPreferences { BookmarkTemplate = "{Unknown}" }));
                Assert.Equal(original, File.ReadAllBytes(path));
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}

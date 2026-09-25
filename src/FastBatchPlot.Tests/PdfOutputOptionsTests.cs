using System;
using System.IO;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class PdfOutputOptionsTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "PdfOptions-" + Guid.NewGuid().ToString("N"));
        public PdfOutputOptionsTests() { Directory.CreateDirectory(directory); }
        private static PdfOutputOptions Options() => new PdfOutputOptions { VectorResolutionDpi = 1200,
            RasterResolutionDpi = 400, TextToGeometry = true, IncludeLayers = false, MergeLines = null };

        [Fact]
        public void PreferencesRoundTripAndLegacyDefaults()
        {
            string path = Path.Combine(directory, "settings.json");
            UserSettingsStore.Save(path, new BatchPlotPreferences { PdfOptions = Options() });
            var loaded = UserSettingsStore.Load(path).PdfOptions;
            Assert.Equal(1200, loaded.VectorResolutionDpi); Assert.Equal(400, loaded.RasterResolutionDpi);
            Assert.True(loaded.TextToGeometry); Assert.False(loaded.IncludeLayers); Assert.Null(loaded.MergeLines);
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); json.Remove("pdfOptions");
            File.WriteAllText(path, json.ToJsonString());
            Assert.False(UserSettingsStore.Load(path).PdfOptions.HasOverrides);
        }

        [Theory]
        [InlineData(0, null)] [InlineData(150, null)] [InlineData(999, null)] [InlineData(null, 2400)]
        public void InvalidResolutionNeverReplacesSettings(int? vector, int? raster)
        {
            string path = Path.Combine(directory, "settings.json");
            UserSettingsStore.Save(path, new BatchPlotPreferences()); var before = File.ReadAllBytes(path);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(path,
                new BatchPlotPreferences { PdfOptions = new PdfOutputOptions { VectorResolutionDpi = vector, RasterResolutionDpi = raster } }));
            Assert.Equal(before, File.ReadAllBytes(path));
        }

        [Fact]
        public void RunAndHistoryAreIsolatedAndLegacyHistoryFollowsDriver()
        {
            var config = new PlotConfig { OutputDirectory = directory, MergeToSinglePdf = false, PdfOptions = Options() };
            var frame = new PlotFrame { MaxX = 420, MaxY = 297, CalculatedScale = 1, IsLandscape = true,
                DetectedPaper = new PaperSize("A3", 420, 297), SourceDocumentId = "doc", SourceLayoutId = "model" };
            var run = new BatchPlotRun(new[] { new BatchPage(frame, Path.Combine(directory, "page.pdf")) }, config);
            config.PdfOptions.VectorResolutionDpi = 300; run.Config.PdfOptions.VectorResolutionDpi = 600;
            Assert.Equal(1200, run.Config.PdfOptions.VectorResolutionDpi);
            var record = PdfTaskHistory.Create(run, ""); record.Config.PdfOptions.VectorResolutionDpi = 2400;
            string path = Path.Combine(directory, "task.json"); PdfTaskHistory.Save(path, record);
            var loaded = PdfTaskHistory.Load(path).Config.PdfOptions;
            Assert.Equal(1200, loaded.VectorResolutionDpi); Assert.False(loaded.IncludeLayers); Assert.True(loaded.TextToGeometry);
            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); json["Config"]!.AsObject().Remove("PdfOptions");
            File.WriteAllText(path, json.ToJsonString());
            Assert.False(PdfTaskHistory.Load(path).Config.PdfOptions.HasOverrides);
            json["Config"]!["PdfOptions"] = new JsonObject { ["vectorResolutionDpi"] = 123 };
            File.WriteAllText(path, json.ToJsonString());
            Assert.Throws<InvalidDataException>(() => PdfTaskHistory.Load(path));
        }
        public void Dispose() { Directory.Delete(directory, true); }
    }
}

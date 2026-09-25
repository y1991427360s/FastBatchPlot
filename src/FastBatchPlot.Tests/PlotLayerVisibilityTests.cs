using System;
using System.IO;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Tasks;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class PlotLayerVisibilityTests
    {
        [Fact]
        public void SuppressionOnlyMatchesExplicitFullNames()
        {
            var config = new PlotConfig { PrintSignatures = false, SignatureLayerName = "图签|签名" };
            var layers = PlotLayerVisibility.LayersToSuppress(config);
            Assert.Single(layers); Assert.Contains("图签|签名", layers);
            Assert.DoesNotContain("签名", layers); Assert.DoesNotContain("图签|签名说明", layers);
            Assert.DoesNotContain("MS_Stamp", layers);
            config.SignatureLayerName = "MS_Sign";
            Assert.Contains("ms_sign", PlotLayerVisibility.LayersToSuppress(config));
            config.PrintSignatures = true;
            Assert.Empty(PlotLayerVisibility.LayersToSuppress(config));
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData("0")] [InlineData("defpoints")]
        [InlineData("MS_*")] [InlineData("签名?")] [InlineData(" MS_Sign")]
        [InlineData("MS_Stamp")] [InlineData("a\nb")]
        public void InvalidOrConflictingMappingsAreRejected(string? layer)
            => Assert.Throws<ArgumentException>(() => PlotLayerVisibility.Validate(layer!, "MS_Stamp"));

        [Fact]
        public void SnapshotRetainsBothControlsAndMapping()
        {
            var config = new PlotConfig { PrintSignatures = false, PrintStamps = false,
                SignatureLayerName = "项目签名", StampLayerName = "项目印章" };
            var run = new BatchPlotRun(new[] { new BatchPage(new PlotFrame(), "a.pdf") }, config);
            config.SignatureLayerName = "其他层"; config.PrintStamps = true;
            run.Config.StampLayerName = "更改副本";
            run.Step((page, snapshot) =>
            {
                var layers = PlotLayerVisibility.LayersToSuppress(snapshot);
                Assert.Equal(2, layers.Count); Assert.Contains("项目签名", layers); Assert.Contains("项目印章", layers);
                return new BatchPageResult(true);
            });
            Assert.True(run.CanMerge);
        }

        [Fact]
        public void PreferencesRoundTripAndLegacyDefaultsPreserveOutputs()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FastBatchPlot-Layers-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "settings.json");
            try
            {
                UserSettingsStore.Save(path, new BatchPlotPreferences { PrintSignatures = false, PrintStamps = false,
                    SignatureLayerName = "自定义签名", StampLayerName = "图纸|印章" });
                var loaded = UserSettingsStore.Load(path);
                Assert.False(loaded.PrintSignatures); Assert.False(loaded.PrintStamps);
                Assert.Equal("自定义签名", loaded.SignatureLayerName); Assert.Equal("图纸|印章", loaded.StampLayerName);
                var original = File.ReadAllText(path);
                Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(path,
                    new BatchPlotPreferences { SignatureLayerName = "0" }));
                Assert.Equal(original, File.ReadAllText(path));
                var legacy = JsonNode.Parse(original)!.AsObject();
                foreach (string field in new[] { "printSignatures", "printStamps", "signatureLayerName", "stampLayerName" }) legacy.Remove(field);
                File.WriteAllText(path, legacy.ToJsonString());
                loaded = UserSettingsStore.Load(path);
                Assert.True(loaded.PrintSignatures); Assert.True(loaded.PrintStamps);
                Assert.Equal("MS_Sign", loaded.SignatureLayerName); Assert.Equal("MS_Stamp", loaded.StampLayerName);
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }
    }
}

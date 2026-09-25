using System;
using System.IO;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class UserSettingsStoreTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "FastBatchPlotSettings-" + Guid.NewGuid().ToString("N"));
        private string SettingsPath => Path.Combine(_directory, "preferences.json");

        [Fact]
        public void MissingFileReturnsDefaultsWithoutWriting()
        {
            var preferences = UserSettingsStore.Load(SettingsPath);
            Assert.Equal(1, preferences.SchemaVersion);
            Assert.Equal("DWG To PDF.pc3", preferences.PrinterDevice);
            Assert.True(preferences.MergeToSinglePdf);
            Assert.False(Directory.Exists(_directory));
        }

        [Fact]
        public void RoundTripPreservesAllFieldsAndUnicode()
        {
            var expected = new BatchPlotPreferences
            {
                PrinterDevice = "中望 PDF.pc5", PlotStyleTable = "彩色.ctb", OutputDirectory = @"D:\工程\图纸",
                NamingTemplate = "{Index:D3}_{DwgName}", MergedFileName = "完整施工图.pdf", MergeToSinglePdf = false,
                MarginMm = -12.5, MinimumAreaPercent = 87.25, SortRuleIndex = 3,
                ExportFormat = PlotExportFormat.DWF, SendToPrinter = true, Copies = 5
            };
            UserSettingsStore.Save(SettingsPath, expected);
            var actual = UserSettingsStore.Load(SettingsPath);
            Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
            Assert.Equal(expected.PrinterDevice, actual.PrinterDevice);
            Assert.Equal(expected.PlotStyleTable, actual.PlotStyleTable);
            Assert.Equal(expected.OutputDirectory, actual.OutputDirectory);
            Assert.Equal(expected.NamingTemplate, actual.NamingTemplate);
            Assert.Equal(expected.MergedFileName, actual.MergedFileName);
            Assert.Equal(expected.MergeToSinglePdf, actual.MergeToSinglePdf);
            Assert.Equal(expected.MarginMm, actual.MarginMm);
            Assert.Equal(expected.MinimumAreaPercent, actual.MinimumAreaPercent);
            Assert.Equal(expected.SortRuleIndex, actual.SortRuleIndex);
            Assert.Equal(expected.ExportFormat, actual.ExportFormat);
            Assert.Equal(expected.SendToPrinter, actual.SendToPrinter);
            Assert.Equal(expected.Copies, actual.Copies);
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Fact]
        public void ValidExistingFileCanBeReplaced()
        {
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences { MergedFileName = "第二份.pdf" });
            Assert.Equal("第二份.pdf", UserSettingsStore.Load(SettingsPath).MergedFileName);
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Fact]
        public void LegacyPreferencesDefaultToPdfAndOneCopy()
        {
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            string json = File.ReadAllText(SettingsPath);
            json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*""(?:exportFormat|sendToPrinter|copies)""\s*:\s*(?:\d+|false|true)", "");
            File.WriteAllText(SettingsPath, json);
            var value = UserSettingsStore.Load(SettingsPath);
            Assert.Equal(PlotExportFormat.PDF, value.ExportFormat);
            Assert.False(value.SendToPrinter);
            Assert.Equal(1, value.Copies);
        }

        [Theory]
        [InlineData("{broken")]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("{\"schemaVersion\":1}")]
        public void InvalidFilesAreReportedAndNeverOverwritten(string content)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(SettingsPath, content);
            var original = File.ReadAllBytes(SettingsPath);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Load(SettingsPath));
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences()));
            Assert.Equal(original, File.ReadAllBytes(SettingsPath));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Fact]
        public void FutureSchemaIsReportedAndNeverOverwritten()
        {
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            File.WriteAllText(SettingsPath, File.ReadAllText(SettingsPath).Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            var original = File.ReadAllBytes(SettingsPath);
            Assert.Contains("版本 2", Assert.Throws<InvalidDataException>(() => UserSettingsStore.Load(SettingsPath)).Message);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences()));
            Assert.Equal(original, File.ReadAllBytes(SettingsPath));
        }

        [Fact]
        public void TrailingGarbageCannotBeSilentlyIgnored()
        {
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            File.AppendAllText(SettingsPath, " unexpected");
            var original = File.ReadAllBytes(SettingsPath);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Load(SettingsPath));
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences()));
            Assert.Equal(original, File.ReadAllBytes(SettingsPath));
        }

        [Fact]
        public void OversizedNewValuesPreserveExistingSettingsAndCleanTemporaryFile()
        {
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            var original = File.ReadAllBytes(SettingsPath);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(SettingsPath,
                new BatchPlotPreferences { NamingTemplate = new string('x', 1024 * 1024) }));
            Assert.Equal(original, File.ReadAllBytes(SettingsPath));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }

        [Theory]
        [InlineData(-50.01, 0, 0)]
        [InlineData(100.01, 0, 0)]
        [InlineData(double.NaN, 0, 0)]
        [InlineData(double.PositiveInfinity, 0, 0)]
        [InlineData(0, -0.01, 0)]
        [InlineData(0, 100.01, 0)]
        [InlineData(0, double.NaN, 0)]
        [InlineData(0, double.NegativeInfinity, 0)]
        [InlineData(0, 0, -1)]
        [InlineData(0, 0, 5)]
        public void InvalidNumbersCannotReplacePreviousSettings(double margin, double area, int sort)
        {
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            var original = File.ReadAllBytes(SettingsPath);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(SettingsPath,
                new BatchPlotPreferences { MarginMm = margin, MinimumAreaPercent = area, SortRuleIndex = sort }));
            Assert.Equal(original, File.ReadAllBytes(SettingsPath));
        }

        [Fact]
        public void NullTextCannotBeSavedOrLoaded()
        {
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(SettingsPath,
                new BatchPlotPreferences { NamingTemplate = null! }));
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            var content = File.ReadAllText(SettingsPath).Replace("\"plotStyleTable\":\"monochrome.ctb\"", "\"plotStyleTable\":null");
            File.WriteAllText(SettingsPath, content);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Load(SettingsPath));
        }

        [Fact]
        public void InvalidStoredNumbersCannotBeLoadedOrOverwritten()
        {
            UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences());
            File.WriteAllText(SettingsPath, File.ReadAllText(SettingsPath).Replace("\"marginMm\":0", "\"marginMm\":101"));
            var original = File.ReadAllBytes(SettingsPath);
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Load(SettingsPath));
            Assert.Throws<InvalidDataException>(() => UserSettingsStore.Save(SettingsPath, new BatchPlotPreferences()));
            Assert.Equal(original, File.ReadAllBytes(SettingsPath));
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}

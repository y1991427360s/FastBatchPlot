using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class TkFormatTests
    {
        // 真实样本随测试工程分发，不依赖个人桌面文件。
        private static readonly string RealTkPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-frames.tk");
        private static readonly string LargeTkPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample-frames-large.tk");

        private static List<string> Lines(string text) => text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        [Fact]
        public void ParseRealTkFileCorrectly()
        {
            Assert.True(File.Exists(RealTkPath), "测试所需的真实 .tk 样本缺失：" + RealTkPath);

            var tk = TkFile.Load(RealTkPath);
            Assert.Equal("20260806", tk.Version);
            Assert.Equal(27, tk.Templates.Count);
            Assert.Equal(18, tk.Section26.Count);
            Assert.Equal(62, tk.Section27.Count);
            Assert.Equal("Continuous", tk.Section26[11]);
            Assert.Equal("47", tk.Section27[14]);
            Assert.Contains("A3+1∕2", tk.Section27);

            // 第 1 行是块名，第 2 行是文件命名规则（原版格式）。
            var t0 = tk.Templates[0];
            Assert.Equal("目录1-标准化", t0.BlockName);
            Assert.Equal("A-MLE", t0.NamingRule);
            Assert.Equal(297, t0.PaperWidth);
            Assert.Equal(210, t0.PaperHeight);
            Assert.Equal(1, t0.Scale);
            Assert.NotNull(t0.FieldSlots[0]); // 图号
            Assert.Null(t0.FieldSlots[2]);    // 目录无图名
            Assert.NotNull(t0.FieldSlots[4]); // 信息1

            var t8 = tk.Templates[8];
            Assert.Equal("A3", t8.BlockName);
            Assert.Equal("A-C", t8.NamingRule);
            Assert.Equal(420, t8.PaperWidth);
            Assert.Equal(297, t8.PaperHeight);
            Assert.Equal(2, t8.Priority);
        }

        [Fact]
        public void ToLibraryUsesBlockNamesNamingRulesAndLocalRegions()
        {
            var lib = TkFile.Load(RealTkPath).ToLibrary();
            Assert.Equal(27, lib.Templates.Count);
            TitleTemplateService.Validate(lib);

            var a3 = lib.Templates.Single(t => t.BlockName == "A3");
            Assert.Equal("A3", a3.Name);
            Assert.Equal("{图号}-{图名}", a3.NamingTemplate);
            Assert.Equal(420, a3.PaperWidth);
            Assert.Equal(297, a3.PaperHeight);
            Assert.Equal(2, a3.Priority);
            Assert.Equal(-420, a3.PrintRegion!.X1, 1);
            Assert.Equal(297, a3.PrintRegion!.Y2, 1);

            // 以图框右下角为基准的本地坐标
            var no = a3.Fields.Single(f => f.Field == TitleField.DrawingNo);
            Assert.Equal(-80, no.Region.X1, 1);
            Assert.Equal(11.96, no.Region.Y1, 1);
            Assert.Equal(-24, no.Region.X2, 1);
            Assert.Equal(19, no.Region.Y2, 1);
            var name = a3.Fields.Single(f => f.Field == TitleField.DrawingName);
            Assert.Equal(-95, name.Region.X1, 1);
            Assert.Equal(19, name.Region.Y1, 1);
            Assert.Equal(-5, name.Region.X2, 1);
            Assert.Equal(33, name.Region.Y2, 1);

            // 图框型模式按块名识别：每个块名都能找到模板
            var byBlock = lib.Templates.ToLookup(t => t.BlockName, StringComparer.OrdinalIgnoreCase);
            Assert.Single(byBlock["A4竖"]);
            Assert.Single(byBlock["恒诚信A1"]);
        }

        [Theory]
        [InlineData("sample-frames.tk")]
        [InlineData("sample-frames-large.tk")]
        public void ParseAndSerializeIsLosslessForRealFiles(string fixture)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
            string original = File.ReadAllText(path, TkFile.GetGbkEncoding());
            string serialized = TkFile.Parse(original).Serialize();
            Assert.Equal(Lines(original), Lines(serialized));
        }

        [Fact]
        public void LargeFileKeepsVariableLengthRecordsAndSingleTrailingSection()
        {
            var tk = TkFile.Load(LargeTkPath);
            Assert.Equal(236, tk.Templates.Count);
            Assert.Equal("52块", tk.Templates[0].BlockName);
            Assert.Empty(tk.Section26);
            Assert.Equal("47", tk.Section27[14]);

            var extended = tk.Templates.Single(t => t.BlockName == "A1横版");
            Assert.Contains("MSteel_HZ.shx", extended.TrailingLines);

            var lib = tk.ToLibrary();
            Assert.Equal(236, lib.Templates.Count);
            Assert.Equal(lib.Templates.Count, lib.Templates.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void RecordedScaleAndRotationAreConvertedToBlockLocalCoordinates()
        {
            var lib = TkFile.Load(LargeTkPath).ToLibrary();

            // 录入时插入比例 100：块范围 63100 → 本地 631
            var scaled = lib.Templates.Single(t => t.BlockName == "A3+0.5横图框");
            Assert.Equal(630, scaled.PaperWidth);
            Assert.Equal(-631, scaled.PrintRegion!.X1, 3);
            Assert.Equal(297, scaled.PrintRegion!.Y2, 3);

            // 录入时旋转 270°：世界范围 297×525（竖）→ 本地 525×297（横）
            var rotated = lib.Templates.Single(t => t.BlockName == "3.25");
            Assert.Equal(-525, rotated.PrintRegion!.X1, 2);
            Assert.Equal(297, rotated.PrintRegion!.Y2, 2);
            var no = rotated.Fields.Single(f => f.Field == TitleField.DrawingNo);
            Assert.True(no.Region.X2 - no.Region.X1 > no.Region.Y2 - no.Region.Y1, "旋转还原后图号框应为横向");
            Assert.True(no.Region.X2 <= 0 && no.Region.Y1 >= 0, "字段区域应在右下角基准的左上方");
        }

        [Fact]
        public void LibraryRoundTripPreservesTemplates()
        {
            var original = TkFile.Load(RealTkPath);
            var lib = original.ToLibrary();
            var exported = TkFile.FromLibrary(lib, original);
            Assert.Equal(Lines(original.Serialize()), Lines(exported.Serialize()));

            var lib2 = TkFile.Parse(exported.Serialize()).ToLibrary();
            Assert.Equal(lib.Templates.Count, lib2.Templates.Count);
            for (int i = 0; i < lib.Templates.Count; i++)
            {
                var a = lib.Templates[i];
                var b = lib2.Templates[i];
                Assert.Equal(a.Name, b.Name);
                Assert.Equal(a.BlockName, b.BlockName);
                Assert.Equal(a.NamingTemplate, b.NamingTemplate);
                Assert.Equal(a.PaperWidth, b.PaperWidth, 2);
                Assert.Equal(a.PaperHeight, b.PaperHeight, 2);
                Assert.Equal(a.Priority, b.Priority);
                Assert.Equal(a.Fields.Count, b.Fields.Count);
                for (int f = 0; f < a.Fields.Count; f++)
                {
                    Assert.Equal(a.Fields[f].Field, b.Fields[f].Field);
                    Assert.Equal(a.Fields[f].Region.X1, b.Fields[f].Region.X1, 2);
                    Assert.Equal(a.Fields[f].Region.Y1, b.Fields[f].Region.Y1, 2);
                    Assert.Equal(a.Fields[f].Region.X2, b.Fields[f].Region.X2, 2);
                    Assert.Equal(a.Fields[f].Region.Y2, b.Fields[f].Region.Y2, 2);
                }
            }
        }

        [Theory]
        [InlineData("A-C", "{图号}-{图名}")]
        [InlineData("A-目录-0E-T", "{图号}-目录-0{信息1}-{图幅}")]
        [InlineData("FF-A", "F-{图号}")]
        [InlineData("C", "{图名}")]
        public void NamingRulesConvertBothWays(string rule, string template)
        {
            Assert.Equal(template, TkFile.ConvertNamingRule(rule));
            Assert.Equal(rule, TkFile.ToNamingRule(template));
        }

        [Fact]
        public void ConvertedNamingRuleProducesExpectedFileName()
        {
            var frame = new PlotFrame { OrderIndex = 3, TitleInfo = new TitleBlockInfo { DrawingNo = "建施03", DrawingName = "一层建筑平面图", ProjectName = "1" },
                DetectedPaper = new PaperSize("A1", 841, 594) };
            Assert.Equal("建施03-一层建筑平面图", FastBatchPlot.Core.Naming.DrawingNameFormatter.Format(TkFile.ConvertNamingRule("A-C"), frame));
            Assert.Equal("建施03-目录-01-A1", FastBatchPlot.Core.Naming.DrawingNameFormatter.Format(TkFile.ConvertNamingRule("A-目录-0E-T"), frame));
        }

        [Fact]
        public void FromScratchLibraryExportsValidTkWithDefaultSections()
        {
            var lib = new TitleTemplateLibrary();
            lib.Templates.Add(new TitleBlockTemplate
            {
                Name = "自定义A3",
                BlockName = "TITLE_A3",
                PaperWidth = 420,
                PaperHeight = 297,
                Priority = 2,
                Fields = new List<TitleFieldRule>
                {
                    new TitleFieldRule { Field = TitleField.DrawingNo, Region = new TemplateRegion { X1 = -80, Y1 = 12, X2 = -24, Y2 = 19 } },
                    new TitleFieldRule { Field = TitleField.DrawingName, Region = new TemplateRegion { X1 = -95, Y1 = 19, X2 = -5, Y2 = 33 } }
                }
            });

            string tempFile = Path.Combine(Path.GetTempPath(), "test-export-" + Guid.NewGuid().ToString("N") + ".tk");
            try
            {
                TkFormatService.Save(tempFile, lib);
                var raw = TkFile.Load(tempFile);
                Assert.Equal("TITLE_A3", raw.Templates[0].BlockName);
                Assert.Equal("A-C", raw.Templates[0].NamingRule);
                Assert.Equal(18, raw.Section26.Count);
                Assert.Equal(62, raw.Section27.Count);

                var loaded = TkFormatService.Load(tempFile);
                var t = Assert.Single(loaded.Templates);
                Assert.Equal("TITLE_A3", t.BlockName);
                Assert.Equal(420, t.PaperWidth);
                Assert.Equal(297, t.PaperHeight);

                var noRule = t.Fields.Single(f => f.Field == TitleField.DrawingNo);
                Assert.Equal(-80, noRule.Region.X1, 1);
                Assert.Equal(12, noRule.Region.Y1, 1);
                Assert.Equal(-24, noRule.Region.X2, 1);
                Assert.Equal(19, noRule.Region.Y2, 1);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void LegacySwappedImportIsDetectedButCorrectImportIsNot()
        {
            var correct = TkFile.Load(RealTkPath).ToLibrary();
            Assert.Null(TitleTemplateService.DescribeLegacyTkImport(correct));
            var legacy = TitleTemplateService.CopyLibrary(correct);
            foreach (var t in legacy.Templates) { t.BlockName = "A-C"; t.NamingTemplate = null; }
            Assert.Contains("A-C", TitleTemplateService.DescribeLegacyTkImport(legacy));
        }

        [Fact]
        public void InvalidTkContentThrowsProperExceptions()
        {
            Assert.Throws<InvalidDataException>(() => TkFile.Parse(""));
            Assert.Throws<InvalidDataException>(() => TkFile.Parse("20260805"));
            Assert.Throws<InvalidDataException>(() => TkFile.Parse("20260805\nnot_a_number"));
            Assert.Throws<InvalidDataException>(() => TkFile.Parse("20260805\n2\n<><\nBad"));
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class FrameLibraryTests
    {
        private static TitleBlockTemplate Frame(string name, string block, double width, double height) => new TitleBlockTemplate
        {
            Name = name, BlockName = block, PaperWidth = width, PaperHeight = height, Priority = 2, NamingTemplate = "{图号}-{图名}",
            Fields = new List<TitleFieldRule> { new TitleFieldRule { Field = TitleField.DrawingNo, Region = new TemplateRegion { X1 = -80, Y1 = 12, X2 = -24, Y2 = 19 } } }
        };

        [Fact]
        public void SlotsFollowOriginalLettersAndTkFieldOrder()
        {
            Assert.Equal("ABCDEF", new string(FrameLibrary.Slots.Select(s => s.Letter).ToArray()));
            Assert.Equal(new[] { "图号(A)", "版次(B)", "图名(C)", "日期(D)", "信息1(E)", "信息2(F)" }, FrameLibrary.Slots.Select(s => s.Caption));
            Assert.Equal(new[] { TitleField.DrawingNo, TitleField.Revision, TitleField.DrawingName, TitleField.Date, TitleField.ProjectName, TitleField.SubProject },
                FrameLibrary.Slots.Select(s => s.Field));
        }

        [Theory]
        [InlineData(594, 420, "A2")]
        [InlineData(420, 594, "A2")]
        [InlineData(525, 297, "A3+1/4")]
        [InlineData(1189.5, 841, "A0")]
        [InlineData(600, 425, "600×425")]
        [InlineData(0, 0, "自动识别")]
        public void PaperLabelNamesStandardExtendedAndCustomSizes(double width, double height, string expected)
            => Assert.Equal(expected, FrameLibrary.PaperLabel(width, height));

        [Fact]
        public void ProposePaperKeepsFrameOrientationAndRejectsUnknownShapes()
        {
            var landscape = FrameLibrary.ProposePaper(59400, 42000)!;
            Assert.Equal("A2", landscape.Name); Assert.Equal(594, landscape.WidthMm); Assert.Equal(420, landscape.HeightMm);
            var portrait = FrameLibrary.ProposePaper(210, 297)!;
            Assert.Equal(210, portrait.WidthMm); Assert.Equal(297, portrait.HeightMm);
            Assert.Null(FrameLibrary.ProposePaper(12345, 100));
            Assert.Null(FrameLibrary.ProposePaper(double.NaN, 100));
        }

        [Theory]
        [InlineData("A-C", "{图号}-{图名}")]
        [InlineData("A(B版)(T)_C_RFF", "{图号}({版次}版)({图幅})_{图名}_RF")]
        [InlineData("  ", null)]
        [InlineData("{DwgNo}_自定义", "{DwgNo}_自定义")]
        public void NamingRuleConvertsLettersAndKeepsPlaceholderTemplates(string rule, string? expected)
            => Assert.Equal(expected, FrameLibrary.NamingTemplateFromRule(rule));

        [Theory]
        [InlineData("A-C")]
        [InlineData("A(B版)(T)_C_RFF")]
        [InlineData("A-目录-0E-T")]
        public void NamingRuleRoundTripsThroughStoredTemplate(string rule)
            => Assert.Equal(rule, FrameLibrary.NamingRuleText(FrameLibrary.NamingTemplateFromRule(rule)));

        [Fact]
        public void NamingRuleTextShowsRawTemplateWhenLettersCannotExpressIt()
        {
            Assert.Equal("", FrameLibrary.NamingRuleText(null));
            Assert.Equal("{Index:D2}_{DwgNo}", FrameLibrary.NamingRuleText("{Index:D2}_{DwgNo}"));
        }

        [Fact]
        public void PreviewMatchesOriginalHelpExample()
        {
            Assert.Equal("建施03(A版)(A1)_一层建筑平面图_RF", FrameLibrary.PreviewFileName("A(B版)(T)_C_RFF"));
            Assert.Contains("通用命名", FrameLibrary.PreviewFileName(""));
        }

        [Fact]
        public void DraftUsesDefaultPriorityPaperAndUniqueNameForSameBlock()
        {
            var library = new TitleTemplateLibrary { Templates = new List<TitleBlockTemplate> { Frame("TK", "TK", 594, 420) } };
            var draft = FrameLibrary.CreateDraft(library, " TK ", new PaperSize("A1", 841, 594), new TemplateRegion { X1 = -841, Y1 = 0, X2 = 0, Y2 = 594 });
            Assert.Equal("TK", draft.BlockName); Assert.Equal("TK (A1)", draft.Name); Assert.Equal(FrameLibrary.DefaultPriority, draft.Priority);
            Assert.Equal(841, draft.PaperWidth); Assert.Null(draft.NamingTemplate); Assert.Equal(-841, draft.PrintRegion!.X1);
            library.Templates.Add(draft);
            Assert.Equal("TK (2)", FrameLibrary.UniqueName(library, "TK", 841, 594));
            TitleTemplateService.Validate(library);
        }

        [Fact]
        public void FindRecordedMatchesBlockAndPaperRegardlessOfOrientation()
        {
            var a2 = Frame("TK", "TK", 594, 420); var a1 = Frame("TK (A1)", "TK", 841, 594); var legacy = Frame("旧", "OLD", 0, 0);
            var library = new TitleTemplateLibrary { Templates = new List<TitleBlockTemplate> { a2, a1, legacy } };
            Assert.Same(a1, FrameLibrary.FindRecorded(library, "tk", 594, 841));
            Assert.Null(FrameLibrary.FindRecorded(library, "TK", 420, 297));
            Assert.Same(legacy, FrameLibrary.FindRecorded(library, "OLD", 420, 297));
        }

        [Fact]
        public void SetFieldRegionReplacesTagRuleAndKeepsFieldOrder()
        {
            var template = Frame("TK", "TK", 594, 420);
            template.Fields.Insert(0, new TitleFieldRule { Field = TitleField.DrawingName, AttributeTag = "TITLE" });
            var region = new TemplateRegion { X1 = -95, Y1 = 19, X2 = -5, Y2 = 33 };
            FrameLibrary.SetFieldRegion(template, TitleField.DrawingName, region);
            region.X1 = 0;
            var title = template.Fields.Single(f => f.Field == TitleField.DrawingName);
            Assert.Equal("", title.AttributeTag); Assert.Equal(-95, title.Region.X1);
            Assert.Equal(new[] { TitleField.DrawingNo, TitleField.DrawingName }, template.Fields.Select(f => f.Field));
            FrameLibrary.ClearField(template, TitleField.DrawingNo);
            Assert.False(FrameLibrary.HasField(template, TitleField.DrawingNo));
        }

        [Fact]
        public void CopyInformationCopiesNamingAndFieldsOnly()
        {
            var source = Frame("源", "SRC", 841, 594); source.PrintRegion = new TemplateRegion { X1 = -841, Y1 = 0, X2 = 0, Y2 = 594 };
            var target = new TitleBlockTemplate { Name = "新", BlockName = "NEW", PaperWidth = 420, PaperHeight = 297, Priority = 5 };
            FrameLibrary.CopyInformation(source, target);
            Assert.Equal(source.NamingTemplate, target.NamingTemplate);
            Assert.Equal(-80, target.Fields.Single().Region.X1);
            Assert.NotSame(source.Fields[0].Region, target.Fields[0].Region);
            Assert.Equal(420, target.PaperWidth); Assert.Equal(5, target.Priority); Assert.Null(target.PrintRegion); Assert.Equal("NEW", target.BlockName);
        }

        [Fact]
        public void MergeReplacesSameBlockAndPaperKeepingIdentityAndAppendsOthers()
        {
            var current = new TitleTemplateLibrary { Templates = new List<TitleBlockTemplate> { Frame("电气", "A1框", 841, 594), Frame("A2", "A2框", 594, 420) } };
            var replacement = Frame("A1框", "A1框", 594, 841); replacement.Priority = 1;
            var added = Frame("A2", "A2框", 1189, 420); added.Id = current.Templates[1].Id;
            var imported = new TitleTemplateLibrary { Templates = new List<TitleBlockTemplate> { replacement, added } };

            var merged = FrameLibrary.Merge(current, imported, keepCurrent: true);
            Assert.Equal(3, merged.Templates.Count);
            Assert.Equal(current.Templates[0].Id, merged.Templates[0].Id); Assert.Equal("电气", merged.Templates[0].Name); Assert.Equal(1, merged.Templates[0].Priority);
            var appended = merged.Templates[2];
            Assert.NotEqual(current.Templates[1].Id, appended.Id); Assert.Equal("A2框", appended.Name);
            Assert.Equal(2, current.Templates[0].Priority);

            var replaced = FrameLibrary.Merge(current, imported, keepCurrent: false);
            Assert.Equal(new[] { "A1框", "A2" }, replaced.Templates.Select(t => t.Name));
        }

        [Fact]
        public void NewFrameExportsToTkAndImportsWithSameLocalRegions()
        {
            var library = new TitleTemplateLibrary();
            var draft = FrameLibrary.CreateDraft(library, "图框A2", new PaperSize("A2", 594, 420), new TemplateRegion { X1 = -59400, Y1 = 0, X2 = 0, Y2 = 42000 });
            FrameLibrary.SetFieldRegion(draft, TitleField.DrawingNo, new TemplateRegion { X1 = -8000, Y1 = 1200, X2 = -2400, Y2 = 1900 });
            draft.NamingTemplate = FrameLibrary.NamingTemplateFromRule("A-C");
            library.Templates.Add(draft);

            var reloaded = TkFile.Parse(TkFile.FromLibrary(library).Serialize()).ToLibrary().Templates.Single();
            Assert.Equal("图框A2", reloaded.BlockName); Assert.Equal(594, reloaded.PaperWidth); Assert.Equal(420, reloaded.PaperHeight);
            Assert.Equal("{图号}-{图名}", reloaded.NamingTemplate); Assert.Equal(FrameLibrary.DefaultPriority, reloaded.Priority);
            var region = reloaded.Fields.Single().Region;
            Assert.Equal(-8000, region.X1, 6); Assert.Equal(1200, region.Y1, 6); Assert.Equal(-2400, region.X2, 6); Assert.Equal(1900, region.Y2, 6);
            Assert.Equal(-59400, reloaded.PrintRegion!.X1, 6); Assert.Equal(42000, reloaded.PrintRegion.Y2, 6);
        }

        [Fact]
        public void NewFrameDetectsRegisteredPaperAndScale()
        {
            var library = new TitleTemplateLibrary();
            var draft = FrameLibrary.CreateDraft(library, "图框A2", new PaperSize("A2", 594, 420), new TemplateRegion { X1 = -594, Y1 = 0, X2 = 0, Y2 = 420 });
            library.Templates.Add(draft);
            var candidate = new RawBlockCandidate { BlockName = "图框A2", Handle = "1", Bounds = new Rect2D(0, 0, 59400, 42000), ScaleX = 100, ScaleY = 100,
                SourceDocumentId = "doc", SourceLayoutId = "model", LayoutName = "Model" };
            var frame = TemplateFrameDetector.Detect(new[] { candidate }, library, (f, r) => new Rect2D(0, 0, 59400, 42000)).Single();
            Assert.Equal("A2", frame.DetectedPaper.Name); Assert.Equal(100, frame.CalculatedScale); Assert.Equal(FrameLibrary.DefaultPriority, frame.TemplatePriority);
        }
    }
}

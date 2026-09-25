using System;
using System.Collections.Generic;
using System.IO;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class TitleTemplateTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "TitleTemplateTests-" + Guid.NewGuid().ToString("N"));
        private string FilePath => Path.Combine(directory, "templates.json");
        private static TitleBlockTemplate Template() => new TitleBlockTemplate { Name = "电气图框", BlockName = "电气A1", Priority = 2,
            Fields = new List<TitleFieldRule> {
                new TitleFieldRule { Field = TitleField.DrawingName, Region = new TemplateRegion { X1 = -200, Y1 = 0, X2 = -10, Y2 = 30 } },
                new TitleFieldRule { Field = TitleField.DrawingNo, AttributeTag = "DWG_NO" }
            } };
        private static TitleTemplateLibrary Library() => new TitleTemplateLibrary { Templates = new List<TitleBlockTemplate> { Template() } };

        [Fact]
        public void ExtractUsesRightBottomOffsetsAndSpatialOrderWithoutMutatingSource()
        {
            var old = new TitleBlockInfo { DrawingName = "旧名", DrawingNo = "旧号", Revision = "B" };
            old.RawAttributes.Add("A", "原属性");
            var result = TitleTemplateService.Extract(Template(), new[] {
                new TemplateText { X = -60, Y = 5, Text = "下行" },
                new TemplateText { X = -50, Y = 20, Text = "右侧" },
                new TemplateText { X = -150, Y = 20, Text = "左侧" },
                new TemplateText { X = 5000, Y = 3000, Text = "电施-02", AttributeTag = "dwg_no" },
                new TemplateText { X = -201, Y = 20, Text = "区外" }
            }, old);
            Assert.Equal("左侧 右侧 下行", result.Info.DrawingName);
            Assert.Equal("电施-02", result.Info.DrawingNo);
            Assert.Equal("B", result.Info.Revision);
            Assert.Empty(result.Warnings);
            result.Info.RawAttributes["A"] = "修改";
            Assert.Equal("原属性", old.RawAttributes["A"]);
            Assert.Equal("旧名", old.DrawingName);
        }
        [Fact]
        public void MissingOrAmbiguousAttributesKeepExistingValuesAndReportWarnings()
        {
            var result = TitleTemplateService.Extract(Template(), new[] {
                new TemplateText { Text = "1", AttributeTag = "DWG_NO" },
                new TemplateText { Text = "2", AttributeTag = "DWG_NO" }
            }, new TitleBlockInfo { DrawingName = "保留图名", DrawingNo = "保留图号" });
            Assert.Equal("保留图名", result.Info.DrawingName);
            Assert.Equal("保留图号", result.Info.DrawingNo);
            Assert.Equal(2, result.Warnings.Count);
        }
        [Fact]
        public void PartialCatalogTitleIsReadWithoutChangingOriginalRegion()
        {
            var rule = new TitleFieldRule { Field = TitleField.DrawingName,
                Region = new TemplateRegion { X1=-97.103, X2=-79.8834, Y1=273.383, Y2=284.158 } };
            var template = new TitleBlockTemplate { Name="目录", BlockName="目录", Fields=new List<TitleFieldRule>{rule} };
            var text = new TemplateText { Text="图纸目录", X=-97.8259325, Y=278.8676045,
                Bounds=new TemplateRegion { X1=-114.9879437, X2=-80.6639213, Y1=274.9960961, Y2=282.7391129 } };
            var result = TitleTemplateService.Extract(template,new[]{text},new TitleBlockInfo());
            Assert.Equal("图纸目录",result.Info.DrawingName);
            Assert.Empty(result.Warnings);
            Assert.Equal(-97.103,rule.Region.X1);
        }
        [Fact]
        public void BoundaryFallbackRejectsAmbiguityTouchingAndNeighborFieldText()
        {
            var template = new TitleBlockTemplate { Name="边界", BlockName="边界", Fields=new List<TitleFieldRule>{
                new TitleFieldRule { Field=TitleField.DrawingName, Region=new TemplateRegion{X1=0,X2=10,Y1=0,Y2=10}} } };
            var crossing = new TemplateText { Text="跨界",X=-1,Y=5,Bounds=new TemplateRegion{X1=-3,X2=1,Y1=3,Y2=7} };
            var old = new TitleBlockInfo{DrawingName="原名"};
            Assert.Equal("原名",TitleTemplateService.Extract(template,new[]{crossing,crossing},old).Info.DrawingName);
            crossing.Bounds.X2=0;
            Assert.Equal("原名",TitleTemplateService.Extract(template,new[]{crossing},old).Info.DrawingName);
            crossing.Bounds.X2=1;
            template.Fields.Add(new TitleFieldRule{Field=TitleField.DrawingNo,Region=new TemplateRegion{X1=-5,X2=0,Y1=0,Y2=10}});
            var result=TitleTemplateService.Extract(template,new[]{crossing},old);
            Assert.Equal("原名",result.Info.DrawingName);
            Assert.Equal("跨界",result.Info.DrawingNo);
            template.Fields.RemoveAt(1);
            var centered=new TemplateText{Text="中心优先",X=5,Y=5};
            Assert.Equal("中心优先",TitleTemplateService.Extract(template,new[]{crossing,centered},old).Info.DrawingName);
        }
        [Fact]
        public void CloneMakesIndependentRulesAndNewIdentity()
        {
            var original = Template(); var clone = TitleTemplateService.Clone(original);
            Assert.NotEqual(original.Id, clone.Id);
            clone.Fields[0].Region.X1 = -50;
            Assert.Equal(-200, original.Fields[0].Region.X1);
        }
        [Fact]
        public void StoreRoundTripsThenAtomicallyReplacesLibrary()
        {
            var library = Library();
            TitleTemplateStore.Save(FilePath, library);
            var loaded = TitleTemplateStore.Load(FilePath);
            Assert.Equal(library.Templates[0].Id, loaded.Templates[0].Id);
            Assert.Equal("电气图框", loaded.Templates[0].Name);
            loaded.Templates[0].Priority = 5;
            TitleTemplateStore.Save(FilePath, loaded);
            Assert.Equal(5, TitleTemplateStore.Load(FilePath).Templates[0].Priority);
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        [Fact]
        public void MissingFileDoesNotCreateAnything()
        {
            Assert.Empty(TitleTemplateStore.Load(FilePath).Templates);
            Assert.False(Directory.Exists(directory));
        }
        [Theory]
        [InlineData("{损坏")]
        [InlineData("{\"SchemaVersion\":99,\"Templates\":[]}")]
        [InlineData("{\"SchemaVersion\":1,\"Templates\":[]}garbage")]
        [InlineData("null")]
        public void InvalidLibraryIsNotOverwritten(string invalid)
        {
            Directory.CreateDirectory(directory); File.WriteAllText(FilePath, invalid);
            Assert.Throws<InvalidDataException>(() => TitleTemplateStore.Save(FilePath, Library()));
            Assert.Equal(invalid, File.ReadAllText(FilePath));
        }
        [Fact]
        public void RejectDuplicateBlockNamesAndFields()
        {
            var library = Library(); library.Templates.Add(TitleTemplateService.Clone(library.Templates[0]));
            Assert.Throws<InvalidDataException>(() => TitleTemplateService.Validate(library));
            library.Templates.RemoveAt(1); library.Templates[0].Fields.Add(library.Templates[0].Fields[0]);
            Assert.Throws<InvalidDataException>(() => TitleTemplateService.Validate(library));
        }
        [Theory]
        [InlineData(double.NaN, 0)]
        [InlineData(10, -10)]
        [InlineData(-10, -10)]
        public void RejectNonfiniteAndInvalidRegion(double left, double right)
        {
            var library = Library(); var r = library.Templates[0].Fields[0].Region; r.X1 = left; r.X2 = right;
            Assert.Throws<InvalidDataException>(() => TitleTemplateService.Validate(library));
        }
        [Fact]
        public void RejectUnknownFieldAndOversizedFile()
        {
            var library = Library(); library.Templates[0].Fields[0].Field = (TitleField)999;
            Assert.Throws<InvalidDataException>(() => TitleTemplateService.Validate(library));
            Directory.CreateDirectory(directory); File.WriteAllBytes(FilePath, new byte[4*1024*1024+1]);
            Assert.Throws<InvalidDataException>(() => TitleTemplateStore.Load(FilePath));
        }
        public void Dispose()
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}

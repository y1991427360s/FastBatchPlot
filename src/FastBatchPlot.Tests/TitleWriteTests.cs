using System;
using System.Collections.Generic;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class TitleWriteTests
    {
        private static PlotFrame Frame()=>new PlotFrame{Type=FrameType.BlockReference,HandleOrId="F",SourceBlockName="A",SourceDocumentId="doc",SourceLayoutId="space"};
        private static TitleBlockTemplate Template()=>new TitleBlockTemplate{Name="A",BlockName="A",Fields=new List<TitleFieldRule>{new TitleFieldRule{Field=TitleField.DrawingNo,AttributeTag="NO"}}};
        private static TemplateText Text()=>new TemplateText{Text="旧号",RawText="旧号",Handle="1",Owner="F",Kind="Attribute",AttributeTag="NO",WriteBlockReason=""};
        private static TitleWritePlan Plan(TemplateText[]? texts=null,string value="新号")=>TitleWritePlanner.Create(Frame(),Template(),new Dictionary<TitleField,string>{{TitleField.DrawingNo,value}},texts??new[]{Text()});
        [Fact] public void PlanSnapshotsIdentityValuesAndOldContent()
        {
            var f=Frame();var t=Template();var text=Text();var values=new Dictionary<TitleField,string>{{TitleField.DrawingNo,"新号"}};
            var p=TitleWritePlanner.Create(f,t,values,new[]{text});f.HandleOrId="changed";t.Fields.Clear();text.RawText="edited";values[TitleField.DrawingNo]="edited";
            Assert.True(p.CanApply);Assert.Equal("F",p.Source.HandleOrId);Assert.Single(p.Template.Fields);Assert.Equal("旧号",p.Changes[0].Before);Assert.Equal("新号",p.Changes[0].After);
        }
        [Fact] public void MissingAmbiguousAndSharedTargetsAreBlocked()
        {
            Assert.False(Plan(Array.Empty<TemplateText>()).CanApply);Assert.False(Plan(new[]{Text(),Text()}).CanApply);
            var t=Text();t.WriteBlockReason="共享定义";Assert.Contains("共享定义",Plan(new[]{t}).Errors[0]);
        }
        [Fact] public void EmptyAttributeCanBeFilledAndUnchangedTextPreservesFormatting()
        {
            var t=Text();t.Text=t.RawText="";Assert.Single(Plan(new[]{t}).Changes);
            t.Text="新号";t.RawText="{\\C1;新号}";Assert.Empty(Plan(new[]{t}).Changes);
        }
        [Fact] public void MultipleFieldsAndFramesCannotWriteSameObject()
        {
            var template=Template();template.Fields.Add(new TitleFieldRule{Field=TitleField.Revision,AttributeTag="NO"});
            var p=TitleWritePlanner.Create(Frame(),template,new Dictionary<TitleField,string>{{TitleField.DrawingNo,"N"},{TitleField.Revision,"A"}},new[]{Text()});Assert.False(p.CanApply);
            Assert.Throws<InvalidOperationException>(()=>TitleWritePlanner.ValidateBatch(new[]{Plan(),Plan()}));
        }
        [Fact] public void ChangedRawValueOrLocationInvalidatesPreview()
        {
            var p=Plan();var t=Text();t.RawText="外部编辑";Assert.False(TitleWritePlanner.SameTargets(p,Plan(new[]{t})));
            t=Text();t.X=4;Assert.False(TitleWritePlanner.SameTargets(p,Plan(new[]{t})));
            Assert.True(TitleWritePlanner.SameTargets(p,Plan()));
        }
        [Fact] public void MultilineEscapesControlSyntaxAndSingleLineRejectsNewlines()
        {
            Assert.False(Plan(value:"甲\n乙").CanApply);var t=Text();t.Kind="MText";
            var p=Plan(new[]{t},"甲{\\A}\r\n乙");Assert.True(p.CanApply);Assert.Equal("甲\\{\\\\A\\}\\P乙",p.Changes[0].After);
            Assert.False(Plan(value:"%<field>%").CanApply);
        }
        [Fact] public void InvalidPayloadAndLocationsAreRejected()
        {
            Assert.False(Plan(value:new string('A',4097)).CanApply);Assert.False(Plan(value:"a\0b").CanApply);
            var t=Text();t.X=double.NaN;Assert.Throws<ArgumentException>(()=>Plan(new[]{t}));
        }
        [Fact] public void BatchRejectsMixedDocuments()
        {
            var f=Frame();f.SourceDocumentId="other";var t=Text();t.Handle="2";
            var other=TitleWritePlanner.Create(f,Template(),new Dictionary<TitleField,string>{{TitleField.DrawingNo,"N"}},new[]{t});
            Assert.Throws<InvalidOperationException>(()=>TitleWritePlanner.ValidateBatch(new[]{Plan(),other}));
        }
    }
}


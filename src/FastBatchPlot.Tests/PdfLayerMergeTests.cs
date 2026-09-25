using System;
using System.IO;
using System.Linq;
using System.Text;
using FastBatchPlot.Core.Pdf;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.IO;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class PdfLayerMergeTests : IDisposable
    {
        private readonly string folder=Path.Combine(Path.GetTempPath(),"PdfLayers-"+Guid.NewGuid().ToString("N"));
        public PdfLayerMergeTests(){Directory.CreateDirectory(folder);}
        private string FileAt(string name)=>Path.Combine(folder,name+".pdf");
        private static PdfItem Resolve(PdfItem item)=>item is PdfReference r?r.Value:item;
        private static PdfDictionary Dict(PdfItem item)=>(PdfDictionary)Resolve(item);
        private static PdfArray List(PdfItem item)=>(PdfArray)Resolve(item);
        private static PdfArray Array(PdfDocument doc,params PdfItem[] items)
        {var array=new PdfArray(doc);foreach(var item in items)array.Elements.Add(item);return array;}
        private void Source(string name,bool enabled=false,bool membership=false,Action<PdfDocument,PdfDictionary,PdfDictionary>? edit=null)
        {
            using(var doc=new PdfDocument())
            {
                doc.Version=15;var page=doc.AddPage();page.Width=300;page.Height=200;
                var group=new PdfDictionary(doc);group.Elements.SetName("/Type","/OCG");group.Elements["/Name"]=new PdfString("同名图层",PdfStringEncoding.Unicode);doc.Internals.AddObject(group);
                PdfDictionary contentLayer=group;
                if(membership)
                {
                    contentLayer=new PdfDictionary(doc);contentLayer.Elements.SetName("/Type","/OCMD");
                    contentLayer.Elements["/OCGs"]=Array(doc,group.Reference);contentLayer.Elements.SetName("/P","/AnyOn");doc.Internals.AddObject(contentLayer);
                }
                var props=new PdfDictionary(doc);props.Elements["/L1"]=contentLayer.Reference;
                var resources=new PdfDictionary(doc);resources.Elements["/Properties"]=props;page.Elements["/Resources"]=resources;
                var stream=new PdfDictionary(doc);stream.CreateStream(Encoding.ASCII.GetBytes("0 0 1 rg 20 20 70 70 re f\n/OC /L1 BDC 1 0 0 rg 160 20 70 70 re f EMC"));
                doc.Internals.AddObject(stream);page.Elements["/Contents"]=stream.Reference;
                var config=new PdfDictionary(doc);config.Elements.SetName("/BaseState",enabled?"/ON":"/OFF");
                config.Elements["/Order"]=Array(doc,new PdfString("原图分组",PdfStringEncoding.Unicode),group.Reference);
                config.Elements["/Locked"]=Array(doc,group.Reference);
                var properties=new PdfDictionary(doc);properties.Elements["/OCGs"]=Array(doc,group.Reference);properties.Elements["/D"]=config;
                doc.Internals.Catalog.Elements["/OCProperties"]=properties;
                edit?.Invoke(doc,config,group);doc.Save(FileAt(name));
            }
        }
        private bool Merge(out string error)=>PdfMerger.MergePdfFiles(new[]{new PdfMergeItem(FileAt("one"),"图纸一"),new PdfMergeItem(FileAt("two"),"图纸二")},FileAt("merged"),out error,false);

        [Theory]
        [InlineData(false)] [InlineData(true)]
        public void SameNamedLayersKeepIndependentStatesAndPageReferences(bool membership)
        {
            Source("one",false,membership);Source("two",true,membership);
            Assert.True(Merge(out var error),error);
            using(var output=PdfReader.Open(FileAt("merged"),PdfDocumentOpenMode.Import))
            {
                Assert.Equal(2,output.PageCount);Assert.Equal(2,output.Outlines.Count);Assert.True(output.Version>=15);
                var properties=Dict(output.Internals.Catalog.Elements["/OCProperties"]);
                var groups=List(properties.Elements["/OCGs"]);Assert.Equal(2,groups.Elements.Count);
                Assert.NotSame(Resolve(groups.Elements[0]),Resolve(groups.Elements[1]));
                var config=Dict(properties.Elements["/D"]);
                Assert.Same(Resolve(groups.Elements[0]),Resolve(List(config.Elements["/OFF"]).Elements[0]));
                Assert.Same(Resolve(groups.Elements[1]),Resolve(List(config.Elements["/ON"]).Elements[0]));
                Assert.Equal(2,List(config.Elements["/Locked"]).Elements.Count);
                Assert.Equal(2,List(config.Elements["/Order"]).Elements.Count);
                for(int i=0;i<2;i++)
                {
                    var layer=Dict(Dict(output.Pages[i].Elements["/Resources"]).Elements["/Properties"]).Elements["/L1"];
                    if(membership)layer=List(Dict(layer).Elements["/OCGs"]).Elements[0];
                    Assert.Same(Resolve(groups.Elements[i]),Resolve(layer));
                }
            }
        }

        [Fact]
        public void ExplicitOverridesAndAlternateConfigurationsRemainLocalToTheirSource()
        {
            Source("one",true,false,(doc,config,group)=>
            {
                config.Elements["/OFF"]=Array(doc,group.Reference);
                var alternate=new PdfDictionary(doc);alternate.Elements["/Name"]=new PdfString("打开本图",PdfStringEncoding.Unicode);alternate.Elements.SetName("/BaseState","/OFF");
                alternate.Elements["/ON"]=Array(doc,group.Reference);alternate.Elements["/Order"]=Array(doc,new PdfString("备用顺序",PdfStringEncoding.Unicode),group.Reference);
                Dict(doc.Internals.Catalog.Elements["/OCProperties"]).Elements["/Configs"]=Array(doc,alternate);
            });
            Source("two",false);Assert.True(Merge(out var error),error);
            using(var output=PdfReader.Open(FileAt("merged"),PdfDocumentOpenMode.Import))
            {
                var properties=Dict(output.Internals.Catalog.Elements["/OCProperties"]);
                Assert.Equal(2,List(Dict(properties.Elements["/D"]).Elements["/OFF"]).Elements.Count);
                var alternate=Dict(List(properties.Elements["/Configs"]).Elements[0]);
                Assert.Contains("打开本图",alternate.Elements.GetString("/Name"));
                Assert.Single(List(alternate.Elements["/ON"]).Elements);Assert.Single(List(alternate.Elements["/OFF"]).Elements);
                var branch=List(List(alternate.Elements["/Order"]).Elements[0]);
                Assert.Equal("备用顺序",((PdfString)branch.Elements[1]).Value);
            }
        }

        [Fact]
        public void UnscopedUsageRulesDoNotAffectOtherInputDrawings()
        {
            Source("one",false,false,(doc,config,group)=>
            {
                var rule=new PdfDictionary(doc);rule.Elements.SetName("/Event","/Print");rule.Elements["/Category"]=Array(doc,new PdfName("/Print"));
                config.Elements["/AS"]=Array(doc,rule);config.Elements["/RBGroups"]=Array(doc,Array(doc,group.Reference));
                var usage=new PdfDictionary(doc);var print=new PdfDictionary(doc);print.Elements.SetName("/PrintState","/ON");usage.Elements["/Print"]=print;group.Elements["/Usage"]=usage;
            });
            Source("two",false);Assert.True(Merge(out var error),error);
            using(var output=PdfReader.Open(FileAt("merged"),PdfDocumentOpenMode.Import))
            {
                var properties=Dict(output.Internals.Catalog.Elements["/OCProperties"]);var config=Dict(properties.Elements["/D"]);
                var rule=Dict(List(config.Elements["/AS"]).Elements[0]);
                Assert.Single(List(rule.Elements["/OCGs"]).Elements);
                Assert.Same(Resolve(List(properties.Elements["/OCGs"]).Elements[0]),Resolve(List(rule.Elements["/OCGs"]).Elements[0]));
                Assert.Single(List(config.Elements["/RBGroups"]).Elements);
                Assert.Equal("/ON",Dict(Dict(Dict(List(properties.Elements["/OCGs"]).Elements[0]).Elements["/Usage"]).Elements["/Print"]).Elements.GetName("/PrintState"));
            }
        }

        [Theory]
        [InlineData("missing")] [InlineData("unknown")] [InlineData("overlap")]
        [InlineData("unchanged")] [InlineData("intent")] [InlineData("extension")]
        public void AmbiguousLayerConfigurationsFailWithoutReplacingOutputOrSources(string defect)
        {
            Source("one",false,false,(doc,config,group)=>
            {
                switch(defect)
                {
                    case "missing":doc.Internals.Catalog.Elements.Remove("/OCProperties");break;
                    case "unknown":var other=new PdfDictionary(doc);doc.Internals.AddObject(other);config.Elements["/OFF"]=Array(doc,other.Reference);break;
                    case "overlap":config.Elements["/ON"]=Array(doc,group.Reference);config.Elements["/OFF"]=Array(doc,group.Reference);break;
                    case "unchanged":config.Elements.SetName("/BaseState","/Unchanged");break;
                    case "intent":config.Elements.SetName("/Intent","/Design");break;
                    case "extension":config.Elements.SetString("/PrivateExtension","unsupported");break;
                }
            });
            Source("two",false);
            var before=File.ReadAllBytes(FileAt("one"));File.WriteAllText(FileAt("merged"),"existing");
            Assert.False(PdfMerger.MergePdfFiles(new[]{new PdfMergeItem(FileAt("one"),"one"),new PdfMergeItem(FileAt("two"),"two")},FileAt("merged"),out var error,true));
            Assert.Contains("图层",error);Assert.Equal("existing",File.ReadAllText(FileAt("merged")));Assert.Equal(before,File.ReadAllBytes(FileAt("one")));
            Assert.Empty(Directory.GetFiles(folder,".batchplot-*.pdf"));
        }

        [Fact]
        public void UnusedRegisteredLayerIsPreservedWithoutDanglingReferences()
        {
            Source("one",false,false,(doc,config,group)=>
            {
                var extra=new PdfDictionary(doc);extra.Elements.SetName("/Type","/OCG");extra.Elements.SetString("/Name","unused");doc.Internals.AddObject(extra);
                List(Dict(doc.Internals.Catalog.Elements["/OCProperties"]).Elements["/OCGs"]).Elements.Add(extra.Reference);
            });
            Source("two",false);Assert.True(Merge(out var error),error);
            using(var output=PdfReader.Open(FileAt("merged"),PdfDocumentOpenMode.Import))
            {
                var groups=List(Dict(output.Internals.Catalog.Elements["/OCProperties"]).Elements["/OCGs"]);
                Assert.Equal(3,groups.Elements.Count);Assert.Equal("unused",Dict(groups.Elements[1]).Elements.GetString("/Name"));
            }
        }
        public void Dispose(){Directory.Delete(folder,true);}
    }
}

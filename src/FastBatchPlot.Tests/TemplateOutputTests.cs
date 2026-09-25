using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Templates;
using Xunit;

namespace FastBatchPlot.Tests
{
    public class TemplateOutputTests : IDisposable
    {
        private readonly string dir=Path.Combine(Path.GetTempPath(),"template-output-"+Guid.NewGuid().ToString("N"));
        private TitleTemplateLibrary Library()=>new TitleTemplateLibrary{Templates=new List<TitleBlockTemplate>{new TitleBlockTemplate{Name="电气图框",BlockName="A",NamingTemplate="{DwgNo}_{Rev}",Catalog=new CatalogOptions{Title="电气目录",RowHeight=32,Columns=new List<CatalogColumn>{new CatalogColumn{Field=CatalogField.DrawingNo,Header="工程图号",Width=25}}}}}};
        [Fact] public void TemplateStoreRoundTripsOutputRulesAndDeepCopies()
        {
            var library=Library();string path=Path.Combine(dir,"templates.json");TitleTemplateStore.Save(path,library);
            var loaded=TitleTemplateStore.Load(path);Assert.Equal("{DwgNo}_{Rev}",loaded.Templates[0].NamingTemplate);Assert.Equal("工程图号",loaded.Templates[0].Catalog!.Columns[0].Header);
            var copy=TitleTemplateService.CopyLibrary(library);copy.Templates[0].Catalog!.Columns[0].Header="changed";Assert.Equal("工程图号",library.Templates[0].Catalog!.Columns[0].Header);
        }
        [Fact] public void OldLibraryWithoutOptionalOutputFieldsRemainsUsable()
        {
            string path=Path.Combine(dir,"templates.json");TitleTemplateStore.Save(path,Library());
            var root=JsonNode.Parse(File.ReadAllText(path))!;var template=root["Templates"]![0]!.AsObject();template.Remove("NamingTemplate");template.Remove("Catalog");File.WriteAllText(path,root.ToJsonString());
            var loaded=TitleTemplateStore.Load(path);Assert.Null(loaded.Templates[0].Catalog);Assert.Equal("fallback",TemplateOutputSettings.Naming(new PlotFrame{Type=FrameType.BlockReference,SourceBlockName="A"},loaded,"fallback"));
        }
        [Fact] public void CatalogDefaultsPersistAndLegacySettingsGetIndependentDefaults()
        {
            string path=Path.Combine(dir,"prefs.json");var prefs=new BatchPlotPreferences{Catalog=Library().Templates[0].Catalog!};UserSettingsStore.Save(path,prefs);
            Assert.Equal("电气目录",UserSettingsStore.Load(path).Catalog.Title);
            var root=JsonNode.Parse(File.ReadAllText(path))!.AsObject();root.Remove("catalog");File.WriteAllText(path,root.ToJsonString());
            var a=UserSettingsStore.Load(path);var b=UserSettingsStore.Load(path);a.Catalog.Columns.Clear();Assert.NotEmpty(b.Catalog.Columns);
        }
        [Fact] public void InvalidEmbeddedCatalogCannotReplaceOldLibrary()
        {
            string path=Path.Combine(dir,"templates.json");var library=Library();TitleTemplateStore.Save(path,library);var bytes=File.ReadAllBytes(path);
            library.Templates[0].Catalog!.Columns[0].Width=double.NaN;Assert.Throws<InvalidDataException>(()=>TitleTemplateStore.Save(path,library));Assert.Equal(bytes,File.ReadAllBytes(path));
        }
        [Fact] public void MatchingUsesBlockNameAndDistinctTemplateChoices()
        {
            var library=Library();var frame=new PlotFrame{Type=FrameType.BlockReference,SourceBlockName="a"};
            Assert.Equal("{DwgNo}_{Rev}",TemplateOutputSettings.Naming(frame,library,"fallback"));
            var choices=TemplateOutputSettings.CatalogChoices(new[]{frame,frame},library,new CatalogOptions());Assert.Equal(2,choices.Count);choices[1].Value.Columns.Clear();Assert.Single(library.Templates[0].Catalog!.Columns);
            frame.Type=FrameType.ClosedPolyline;Assert.Equal("fallback",TemplateOutputSettings.Naming(frame,library,"fallback"));
        }
        public void Dispose(){if(Directory.Exists(dir)){foreach(var f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class CatalogXlsxTests : IDisposable
    {
        private readonly string directory=Path.Combine(Path.GetTempPath(),"CatalogXlsx-"+Guid.NewGuid().ToString("N"));
        private string Target=>Path.Combine(directory,"目录.xlsx");
        private static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static List<PlotFrame> Frames()=>new List<PlotFrame>{
            new PlotFrame{OrderIndex=1,DetectedPaper=new PaperSize("A1",841,594),CalculatedScale=2.5,TitleInfo=new TitleBlockInfo{DrawingNo="001",DrawingName="配电平面",Date="2026-09-20"}},
            new PlotFrame{OrderIndex=2,DetectedPaper=new PaperSize("自定义",500,300),TitleInfo=new TitleBlockInfo{DrawingNo="=1+1",DrawingName="含逗号,和引号\"标题"}},
            new PlotFrame{OrderIndex=3,DetectedPaper=new PaperSize("自定义",800,300),IsSelected=false}
        };
        private static XDocument Read(ZipArchive zip,string name){using(var stream=zip.GetEntry(name)!.Open())return XDocument.Load(stream);}
        [Fact]
        public void XlsxPreservesIdentifiersAsTextAndUsesTypedDatesAndFormulas()
        {
            CatalogXlsxExporter.Export(Frames(),Target);
            using(var zip=ZipFile.OpenRead(Target))
            {
                var sheet=Read(zip,"xl/worksheets/sheet1.xml");
                var cells=sheet.Descendants(S+"c").ToDictionary(c=>(string)c.Attribute("r")!);
                Assert.Equal("001",cells["B5"].Descendants(S+"t").Single().Value);
                Assert.Equal("inlineStr",(string)cells["B6"].Attribute("t")!);
                Assert.Empty(cells["B6"].Elements(S+"f"));
                Assert.Equal("=1+1",cells["B6"].Descendants(S+"t").Single().Value);
                Assert.Equal("1:2.5",cells["E5"].Descendants(S+"t").Single().Value);
                Assert.NotNull(cells["G5"].Element(S+"v"));
                Assert.Equal("A4:H6",(string)sheet.Descendants(S+"autoFilter").Single().Attribute("ref")!);
                Assert.Equal("4",(string)sheet.Descendants(S+"pane").Single().Attribute("ySplit")!);
                var stats=Read(zip,"xl/worksheets/sheet2.xml");
                Assert.Contains(stats.Descendants(S+"f"),f=>f.Value=="E6/$B$3");
                Assert.Contains(stats.Descendants(S+"f"),f=>f.Value=="SUM(D6:D7)");
                Assert.Equal(2,Read(zip,"xl/workbook.xml").Descendants(S+"sheet").Count());
            }
        }
        [Fact]
        public void StatisticsSeparateSameNameDifferentSizeAndIgnoreRotation()
        {
            var frames=Frames();frames.Add(new PlotFrame{DetectedPaper=new PaperSize("A1",594,841)});
            var usage=PaperUsageCalculator.Calculate(frames);
            Assert.Equal(3,usage.Count);
            Assert.Equal(2,usage.Single(u=>u.Name=="A1").Count);
            Assert.Equal(2,usage.Single(u=>u.Name=="A1").EquivalentA1,10);
            Assert.Equal((2*841*594+500*300+800*300)/1000000.0,usage.Sum(u=>u.AreaSquareMetres),10);
        }
        [Fact]
        public void ColumnOrderAndAllRowsAreConfigurableWithoutStatistics()
        {
            var options=new CatalogOptions{SelectedOnly=false,IncludeStatistics=false,Columns=new List<CatalogColumn>{
                new CatalogColumn{Field=CatalogField.DrawingName,Header="图面内容",Width=45},new CatalogColumn{Field=CatalogField.DrawingNo,Header="编号",Width=20}}};
            CatalogXlsxExporter.Export(Frames(),Target,options);
            using(var zip=ZipFile.OpenRead(Target))
            {
                Assert.Null(zip.GetEntry("xl/worksheets/sheet2.xml"));
                var sheet=Read(zip,"xl/worksheets/sheet1.xml");
                Assert.Equal("图面内容",sheet.Descendants(S+"c").Single(c=>(string)c.Attribute("r")! =="A4").Descendants(S+"t").Single().Value);
                Assert.Equal("A4:B7",(string)sheet.Descendants(S+"autoFilter").Single().Attribute("ref")!);
            }
        }
        [Fact]
        public void ExportIsAtomicAndDoesNotSilentlyOverwrite()
        {
            CatalogXlsxExporter.Export(Frames(),Target);byte[] old=File.ReadAllBytes(Target);
            Assert.Throws<IOException>(()=>CatalogXlsxExporter.Export(Frames(),Target));
            var bad=Frames();bad[0].TitleInfo.DrawingName="非法\u0001文字";
            Assert.ThrowsAny<Exception>(()=>CatalogXlsxExporter.Export(bad,Target,overwrite:true));
            Assert.Equal(old,File.ReadAllBytes(Target));
            Assert.Empty(Directory.GetFiles(directory,".catalog-*"));
        }
        [Fact]
        public void InvalidPaperAndDuplicateColumnsAreRejected()
        {
            var frames=Frames();frames[0].DetectedPaper.WidthMm=double.NaN;
            Assert.Throws<ArgumentException>(()=>CatalogXlsxExporter.Export(frames,Target));
            var options=new CatalogOptions();options.Columns.Add(options.Columns[0]);
            Assert.Throws<ArgumentException>(()=>CatalogXlsxExporter.Export(Frames(),Target,options));
            Assert.False(File.Exists(Target));
        }
        [Fact]
        public void ConfiguredCsvUsesBomAndNeutralizesSpreadsheetFormulas()
        {
            string csv=Path.Combine(directory,"目录.csv");
            CatalogExporter.ExportConfiguredCsv(Frames(),csv,new CatalogOptions());
            var bytes=File.ReadAllBytes(csv);Assert.Equal(new byte[]{239,187,191},bytes.Take(3));
            string text=File.ReadAllText(csv);Assert.Contains("'=1+1",text);Assert.Contains("\"含逗号,和引号\"\"标题\"",text);
            Assert.Contains("折算口径",text);Assert.DoesNotContain("800",text);
        }
        public void Dispose(){if(Directory.Exists(directory)){foreach(var path in Directory.GetFiles(directory))File.Delete(path);Directory.Delete(directory);}}
    }
}

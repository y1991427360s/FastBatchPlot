using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class PlotFileFormatTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "PlotFormatTests-" + Guid.NewGuid().ToString("N"));
        private static PlotPlan Plan() => PlotPlanBuilder.Create(new PlotFrame { MinX=0,MinY=0,MaxX=420,MaxY=297,
            CalculatedScale=1, IsLandscape=true, DetectedPaper=new PaperSize("A3",420,297,true) }, new PlotConfig());
        private string Target(PlotExportFormat format) { Directory.CreateDirectory(directory); return Path.Combine(directory,"成果"+PlotFileFormats.Extension(format)); }
        [Theory]
        [InlineData(PlotExportFormat.PNG)]
        [InlineData(PlotExportFormat.JPG)]
        public void RealRasterImageIsDecodedAndCommitted(PlotExportFormat format)
        {
            string target=Target(format), temp=PlotFileFormats.CreateTemporaryPath(target,format);
            using (var image=new Image<Rgb24>(420,297)) image.Save(temp);
            PlotFileFormats.ValidateAndCommit(temp,target,format,Plan());
            Assert.True(File.Exists(target)); Assert.False(File.Exists(temp));
        }
        [Fact]
        public void WrongImageFormatAndWrongAspectAreRejected()
        {
            string target=Target(PlotExportFormat.PNG), temp=PlotFileFormats.CreateTemporaryPath(target,PlotExportFormat.PNG);
            using (var image=new Image<Rgb24>(100,100)) image.SaveAsPng(temp);
            Assert.Throws<InvalidDataException>(()=>PlotFileFormats.ValidateAndCommit(temp,target,PlotExportFormat.PNG,Plan()));
            using (var image=new Image<Rgb24>(420,297)) image.SaveAsJpeg(temp);
            Assert.Throws<InvalidDataException>(()=>PlotFileFormats.ValidateAndCommit(temp,target,PlotExportFormat.PNG,Plan()));
            Assert.False(File.Exists(target));
        }
        [Theory]
        [InlineData(PlotExportFormat.SVG,"<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0L1 1\"/></svg>")]
        [InlineData(PlotExportFormat.EPS,"%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 420 297\n0 0 moveto 1 1 lineto stroke\n%%EOF")]
        [InlineData(PlotExportFormat.PLT,"IN;SP1;PA0,0;PD100,100;PU;")]
        public void StructuredVectorFilesCommitAndRefuseExistingDestination(PlotExportFormat format,string content)
        {
            string target=Target(format), temp=PlotFileFormats.CreateTemporaryPath(target,format);
            File.WriteAllText(temp,content,new UTF8Encoding(false));
            PlotFileFormats.ValidateAndCommit(temp,target,format,Plan());
            Assert.Equal(content,File.ReadAllText(target));
            File.WriteAllText(temp,content,new UTF8Encoding(false));
            Assert.Throws<IOException>(()=>PlotFileFormats.ValidateAndCommit(temp,target,format,Plan()));
            Assert.Equal(content,File.ReadAllText(target));
        }
        [Theory]
        [InlineData(PlotExportFormat.DWF,"(DWF V06.00)only header")]
        [InlineData(PlotExportFormat.SVG,"<svg xmlns=\"http://www.w3.org/2000/svg\"><path>")]
        [InlineData(PlotExportFormat.EPS,"%PDF-1.7 fake renamed")]
        [InlineData(PlotExportFormat.PLT,"<html>driver failure</html>")]
        public void WrongOrTruncatedFilesNeverCommit(PlotExportFormat format,string content)
        {
            string target=Target(format),temp=PlotFileFormats.CreateTemporaryPath(target,format);
            File.WriteAllText(temp,content);
            Assert.ThrowsAny<Exception>(()=>PlotFileFormats.ValidateAndCommit(temp,target,format,Plan()));
            Assert.False(File.Exists(target));
        }
        [Fact]
        public void DwfContainerRequiresReadableManifestAndResources()
        {
            string target=Target(PlotExportFormat.DWF),temp=PlotFileFormats.CreateTemporaryPath(target,PlotExportFormat.DWF);
            using (var stream=File.Create(temp))
            {
                var prefix=Encoding.ASCII.GetBytes("(DWF V06.00)"); stream.Write(prefix,0,prefix.Length);
                using (var zip=new ZipArchive(stream,ZipArchiveMode.Create))
                {
                    using (var writer=new StreamWriter(zip.CreateEntry("manifest.xml").Open())) writer.Write("<Manifest/>" );
                    using (var writer=new StreamWriter(zip.CreateEntry("section/graphics.w2d").Open())) writer.Write("test resource");
                }
            }
            PlotFileFormats.ValidateAndCommit(temp,target,PlotExportFormat.DWF,Plan());
            Assert.True(File.Exists(target));
        }
        [Fact]
        public void SvgExternalEntitiesAreRejectedWithoutReadingExternalFile()
        {
            string target=Target(PlotExportFormat.SVG),temp=PlotFileFormats.CreateTemporaryPath(target,PlotExportFormat.SVG);
            File.WriteAllText(temp,"<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///missing'>]><svg xmlns='http://www.w3.org/2000/svg'><text>&x;</text></svg>");
            Assert.ThrowsAny<Exception>(()=>PlotFileFormats.ValidateAndCommit(temp,target,PlotExportFormat.SVG,Plan()));
            Assert.False(File.Exists(target));
        }
        [Fact]
        public void FormatExtensionAndCopiesMustBeValid()
        {
            Assert.Throws<ArgumentException>(()=>PlotFileFormats.CreateTemporaryPath(Target(PlotExportFormat.PNG),PlotExportFormat.JPG));
            Assert.Throws<ArgumentException>(()=>PlotFileFormats.Extension((PlotExportFormat)99));
            Assert.Throws<ArgumentException>(()=>PlotPlanBuilder.Create(new PlotFrame(),new PlotConfig {Copies=0}));
            Assert.Throws<ArgumentException>(()=>PlotPlanBuilder.Create(new PlotFrame(),new PlotConfig {Copies=1000}));
        }
        public void Dispose() { if (Directory.Exists(directory)) { foreach(var p in Directory.GetFiles(directory)) File.Delete(p); Directory.Delete(directory); } }
    }
}

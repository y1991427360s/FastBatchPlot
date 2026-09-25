using System;
using System.IO;
using System.Linq;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.Core.Templates;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace FastBatchPlot.Tests
{
    public sealed class StampLibraryTests : IDisposable
    {
        private readonly string directory=Path.Combine(Path.GetTempPath(),"StampTests-"+Guid.NewGuid().ToString("N"));
        private string FilePath=>Path.Combine(directory,"库.json");
        private static byte[] Png(byte backgroundAlpha=0)
        {
            using(var image=new Image<Rgba32>(12,6,new Rgba32(255,0,0,backgroundAlpha)))
            using(var stream=new MemoryStream())
            {image[6,3]=new Rgba32(255,0,0,255);image.SaveAsPng(stream);return stream.ToArray();}
        }
        private static StampAsset Asset()=>StampAsset.Import("测试红色图形",Png());
        [Fact] public void ImportDecodesCanonicalPngAndRequiresTransparency()
        {
            var asset=Asset();asset.Validate();Assert.Equal(12,asset.PixelWidth);Assert.Equal(6,asset.PixelHeight);
            Assert.Throws<InvalidDataException>(()=>StampAsset.Import("不透明",Png(255)));
            using(var blank=new Image<Rgba32>(12,6))using(var stream=new MemoryStream())
            {blank.SaveAsPng(stream);Assert.Throws<InvalidDataException>(()=>StampAsset.Import("空图片",stream.ToArray()));}
        }
        [Fact] public void CorruptTruncatedAndTrailingPngAreRejected()
        {
            var png=Png();Assert.Throws<InvalidDataException>(()=>StampAsset.Import("截断",png.Take(png.Length-12).ToArray()));
            Assert.Throws<InvalidDataException>(()=>StampAsset.Import("多余",png.Concat(new byte[]{0}).ToArray()));
            png[29]^=1;Assert.Throws<InvalidDataException>(()=>StampAsset.Import("CRC错误",png));
            Assert.Throws<InvalidDataException>(()=>StampAsset.Import("超限",new byte[StampAsset.MaximumPngBytes+1]));
        }
        [Fact] public void PortableLibraryRetainsPngAndRejectsConcurrentEdits()
        {
            var library=StampLibraryStore.Load(FilePath);Assert.False(Directory.Exists(directory));library.Assets.Add(Asset());
            StampLibraryStore.Save(FilePath,library);
            var first=StampLibraryStore.Load(FilePath);var second=StampLibraryStore.Load(FilePath);
            Assert.Equal(library.Assets[0].PngBase64,first.Assets[0].PngBase64);
            first.Assets[0].Name="第一窗口";StampLibraryStore.Save(FilePath,first);var bytes=File.ReadAllBytes(FilePath);
            second.Assets[0].Name="旧窗口";Assert.Throws<IOException>(()=>StampLibraryStore.Save(FilePath,second));
            Assert.Equal(bytes,File.ReadAllBytes(FilePath));Assert.Empty(Directory.GetFiles(directory,"*.tmp"));
            string portable=Path.Combine(directory,"移动副本.json");File.Copy(FilePath,portable);
            Assert.Equal("第一窗口",StampLibraryStore.Load(portable).Assets[0].Name);
        }
        [Fact] public void CorruptOrDeletedLibraryCannotBeOverwrittenByStaleEditor()
        {
            var library=new StampLibrary();library.Assets.Add(Asset());StampLibraryStore.Save(FilePath,library);
            File.WriteAllText(FilePath,"{broken");Assert.Throws<InvalidDataException>(()=>StampLibraryStore.Load(FilePath));
            Assert.Throws<InvalidDataException>(()=>StampLibraryStore.Save(FilePath,library));Assert.Equal("{broken",File.ReadAllText(FilePath));
            File.Delete(FilePath);Assert.Throws<IOException>(()=>StampLibraryStore.Save(FilePath,library));Assert.False(File.Exists(FilePath));
        }
        [Fact] public void InvalidAssetMetadataOrDuplicateIdsCannotReplaceExistingLibrary()
        {
            var library=new StampLibrary();library.Assets.Add(Asset());StampLibraryStore.Save(FilePath,library);var bytes=File.ReadAllBytes(FilePath);
            var invalid=library.Copy();invalid.Assets[0].PixelWidth=99;Assert.Throws<InvalidDataException>(()=>StampLibraryStore.Save(FilePath,invalid));
            invalid=library.Copy();invalid.Assets.Add(invalid.Assets[0].Copy());Assert.Throws<InvalidDataException>(()=>StampLibraryStore.Save(FilePath,invalid));
            Assert.Equal(bytes,File.ReadAllBytes(FilePath));
        }
        [Fact] public void SharedLockPreventsAnotherWriter()
        {
            var library=new StampLibrary();StampLibraryStore.Save(FilePath,library);
            using(var guard=new FileStream(FilePath+".lock",FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                Assert.Throws<IOException>(()=>StampLibraryStore.Save(FilePath,library));
        }
        [Theory]
        [InlineData(100,0,0,100,0,25,100,0,0,50)]
        [InlineData(0,100,-100,0,-25,0,0,100,-50,0)]
        [InlineData(-100,0,0,100,0,25,-100,0,0,50)]
        public void PlacementPreservesAspectRotationAndMirroring(double ux,double uy,double vx,double vy,double x,double y,double expectedUx,double expectedUy,double expectedVx,double expectedVy)
        {
            var placed=StampPlacement.Fit(0,0,0,ux,uy,vx,vy,200,100);
            Assert.Equal(x,placed.X,6);Assert.Equal(y,placed.Y,6);Assert.Equal(expectedUx,placed.Ux,6);Assert.Equal(expectedUy,placed.Uy,6);
            Assert.Equal(expectedVx,placed.Vx,6);Assert.Equal(expectedVy,placed.Vy,6);
        }
        [Fact] public void InvalidTransformAndOutOfWindowPlacementFailPreflight()
        {
            Assert.Throws<ArgumentException>(()=>StampPlacement.Fit(0,0,0,0,0,0,1,12,6));
            Assert.Throws<NotSupportedException>(()=>StampPlacement.Fit(0,0,0,1,0,1,1,12,6));
            Assert.Throws<ArgumentException>(()=>StampPlacement.Fit(double.NaN,0,0,1,0,0,1,12,6));
            var frame=new PlotFrame{MaxX=841,MaxY=594,CalculatedScale=1};var plan=PlotPlanBuilder.Create(frame,new PlotConfig());
            StampPlacement.Fit(0,0,0,100,0,0,100,12,6).EnsureWithin(plan);
            Assert.Throws<InvalidOperationException>(()=>StampPlacement.Fit(800,500,0,100,0,0,100,12,6).EnsureWithin(plan));
        }
        [Fact] public void JobSnapshotProtectsStampContentAndTemplateRegion()
        {
            var config=new PlotConfig{Stamp=Asset()};var frame=new PlotFrame{StampRegion=new TemplateRegion{X2=100,Y2=100}};
            var run=new BatchPlotRun(new[]{new BatchPage(frame,"a.pdf")},config);
            config.Stamp.Name="新印章";frame.StampRegion.X2=999;run.Config.Stamp!.PngBase64="corrupted";
            run.Step((page,snapshot)=>{snapshot.Stamp!.Validate();Assert.Equal("测试红色图形",snapshot.Stamp.Name);Assert.Equal(100,page.Frame.StampRegion!.X2);return new BatchPageResult(true);});
            Assert.True(run.CanMerge);
        }
        [Fact] public void TemplateRoundTripKeepsStampRegionIndependentFromCrop()
        {
            Directory.CreateDirectory(directory);var library=new TitleTemplateLibrary();
            var template=new TitleBlockTemplate{Name="图框",BlockName="框",StampRegion=new TemplateRegion{X1=-100,Y1=10,X2=-20,Y2=60}};
            library.Templates.Add(template);string path=Path.Combine(directory,"templates.json");TitleTemplateStore.Save(path,library);
            var loaded=TitleTemplateStore.Load(path);Assert.Null(loaded.Templates[0].PrintRegion);Assert.Equal(-100,loaded.Templates[0].StampRegion!.X1);
            TitleTemplateService.Clone(template).StampRegion!.X1=-200;Assert.Equal(-100,template.StampRegion.X1);
        }
        public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.Core.Templates;
using Xunit;
namespace FastBatchPlot.Tests
{
    public class TemplateCropTests
    {
        [Fact] public void MirroredAndRightAngleCornersKeepExactRectangle()
        {
            var r=TemplateCropGeometry.FromCorners(new Point2D(500,10),new Point2D(500,110),new Point2D(300,110),new Point2D(300,10));
            Assert.Equal(300,r.MinX);Assert.Equal(200,r.Width);Assert.Equal(100,r.Height);
            r=TemplateCropGeometry.FromCorners(new Point2D(500,10),new Point2D(300,10),new Point2D(300,110),new Point2D(500,110));Assert.Equal(300,r.MinX);
        }
        [Fact] public void SkewedRotatedAndDegenerateCornersAreRejected()
        {
            Assert.Throws<NotSupportedException>(()=>TemplateCropGeometry.FromCorners(new Point2D(0,0),new Point2D(10,10),new Point2D(0,20),new Point2D(-10,10)));
            Assert.Throws<NotSupportedException>(()=>TemplateCropGeometry.FromCorners(new Point2D(0,0),new Point2D(10,0),new Point2D(12,10),new Point2D(0,10)));
            Assert.Throws<ArgumentException>(()=>TemplateCropGeometry.FromCorners(new Point2D(),new Point2D(),new Point2D(),new Point2D()));
        }
        [Fact] public void PlotPlanUsesCropWithoutChangingSourceIdentityOrBounds()
        {
            var f=new PlotFrame{MinX=-100,MinY=-100,MaxX=1000,MaxY=1000,CalculatedScale=1,PrintBounds=new Rect2D(10,20,430,317),DetectedPaper=new PaperSize("A3",420,297),IsLandscape=true,HandleOrId="A"};
            var plan=PlotPlanBuilder.Create(f,new PlotConfig());Assert.Equal(10,plan.MinX);Assert.Equal(317,plan.MaxY);Assert.Equal(420,plan.ContentWidthMm);Assert.Equal(-100,f.MinX);Assert.Equal("A",f.HandleOrId);
        }
        [Fact] public void SnapshotCopiesAppliedRegion()
        {
            var f=new PlotFrame{AppliedPrintRegion=new TemplateRegion{X1=-420,Y1=0,X2=0,Y2=297},PrintBounds=new Rect2D(0,0,420,297)};
            var page=new BatchPage(f,"a.pdf");f.AppliedPrintRegion.X1=-999;page.Frame.AppliedPrintRegion!.X1=-123;Assert.Equal(-420,page.Frame.AppliedPrintRegion!.X1);
        }
        [Fact] public void TemplateRegionRoundTripsAndInvalidRegionDoesNotOverwrite()
        {
            var dir=Path.Combine(Path.GetTempPath(),"crop-template-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(dir,"templates.json");
            try
            {
                var template=new TitleBlockTemplate{Name="图框",BlockName="A",PrintRegion=new TemplateRegion{X1=-420,Y1=0,X2=0,Y2=297}};
                var lib=new TitleTemplateLibrary{Templates=new List<TitleBlockTemplate>{template}};TitleTemplateStore.Save(path,lib);Assert.Equal(-420,TitleTemplateStore.Load(path).Templates[0].PrintRegion!.X1);
                var copy=TitleTemplateService.Clone(template);copy.PrintRegion!.X1=-100;Assert.Equal(-420,template.PrintRegion.X1);
                var bytes=File.ReadAllBytes(path);template.PrintRegion.X1=double.NaN;Assert.Throws<InvalidDataException>(()=>TitleTemplateStore.Save(path,lib));Assert.Equal(bytes,File.ReadAllBytes(path));
            }
            finally{if(Directory.Exists(dir)){foreach(var f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}}
        }
    }
}

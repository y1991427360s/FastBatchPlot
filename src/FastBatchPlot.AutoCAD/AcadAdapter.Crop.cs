using System;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
namespace FastBatchPlot.AutoCAD
{
    public partial class AcadAdapter : ICadTemplateCropHost
    {
        public Rect2D ResolveTemplatePrintBounds(PlotFrame frame,TemplateRegion region)
        {
            TemplateCropGeometry.Validate(region);
            var doc=Application.DocumentManager.MdiActiveDocument;
            if(doc==null)throw new InvalidOperationException("没有活动文档。");
            using(doc.LockDocument())
            using(var tr=doc.Database.TransactionManager.StartTransaction())
            {
                var block=OpenTemplateBlock(doc,tr,frame);
                var definition=(BlockTableRecord)tr.GetObject(block.BlockTableRecord,OpenMode.ForRead);
                if(definition.IsFromExternalReference)throw new NotSupportedException("外参图框裁切尚未支持。");
                var anchor=TemplateAnchor(block,tr);
                var local=new[]{new Point3d(region.X1+anchor.X,region.Y1+anchor.Y,anchor.Z),new Point3d(region.X2+anchor.X,region.Y1+anchor.Y,anchor.Z),new Point3d(region.X2+anchor.X,region.Y2+anchor.Y,anchor.Z),new Point3d(region.X1+anchor.X,region.Y2+anchor.Y,anchor.Z)};
                var world=new Point3d[4];for(int i=0;i<4;i++)world[i]=local[i].TransformBy(block.BlockTransform);
                for(int i=1;i<4;i++)if(Math.Abs(world[i].Z-world[0].Z)>1e-7)throw new NotSupportedException("倾斜图框裁切尚未支持。");
                return TemplateCropGeometry.FromCorners(new Point2D(world[0].X,world[0].Y),new Point2D(world[1].X,world[1].Y),new Point2D(world[2].X,world[2].Y),new Point2D(world[3].X,world[3].Y));
            }
        }
    }
}

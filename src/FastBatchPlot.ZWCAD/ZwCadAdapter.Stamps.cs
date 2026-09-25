using System;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;

namespace FastBatchPlot.ZWCAD
{
    public partial class ZwCadAdapter : ICadStampHost
    {
        public StampPlacement ResolveStampPlacement(PlotFrame frame, TemplateRegion region, int pixelWidth, int pixelHeight)
        {
            TemplateCropGeometry.Validate(region);
            var doc=Application.DocumentManager.MdiActiveDocument;
            if(doc==null)throw new InvalidOperationException("没有活动文档。");
            using(doc.LockDocument())
            using(var tr=doc.Database.TransactionManager.StartTransaction())
            {
                var block=OpenTemplateBlock(doc,tr,frame);
                var definition=(BlockTableRecord)tr.GetObject(block.BlockTableRecord,OpenMode.ForRead);
                if(definition.IsFromExternalReference)throw new NotSupportedException("外参图框印章区域尚未支持。");
                var anchor=TemplateAnchor(block,tr);
                var lower=new Point3d(anchor.X+region.X1,anchor.Y+region.Y1,anchor.Z).TransformBy(block.BlockTransform);
                var right=new Point3d(anchor.X+region.X2,anchor.Y+region.Y1,anchor.Z).TransformBy(block.BlockTransform);
                var upper=new Point3d(anchor.X+region.X1,anchor.Y+region.Y2,anchor.Z).TransformBy(block.BlockTransform);
                var u=right-lower; var v=upper-lower;
                if(Math.Abs(u.Z)>1e-7||Math.Abs(v.Z)>1e-7)throw new NotSupportedException("倾斜图框印章区域尚未支持。");
                return StampPlacement.Fit(lower.X,lower.Y,lower.Z,u.X,u.Y,v.X,v.Y,pixelWidth,pixelHeight);
            }
        }
    }
}

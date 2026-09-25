using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace FastBatchPlot.AutoCAD
{
    public partial class AcadAdapter : ICadDwgSplitHost
    {
        public DwgSplitPlan InspectDwgSplit(PlotFrame frame)
        {
            var doc=Application.DocumentManager.MdiActiveDocument;
            if(doc==null)throw new InvalidOperationException("没有活动文档。");
            using(doc.LockDocument())
            using(var tr=doc.Database.TransactionManager.StartTransaction())
                return InspectSplit(doc,tr,frame);
        }
        private static DwgSplitPlan InspectSplit(Document doc,Transaction tr,PlotFrame frame)
        {
            DwgSplitGeometry.Classify(frame,frame.MinX,frame.MinY,frame.MaxX,frame.MaxY);
            if(frame.SourceDocumentId!=DocumentSessionIdentity.Get(doc))throw new InvalidOperationException("请切回来源文档并重新搜索。");
            var db=doc.Database;
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForRead);
            var model=(BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForRead);
            if(model.Handle.ToString()!=frame.SourceLayoutId || frame.LayoutName!="Model")
                throw new NotSupportedException("当前仅支持模型空间拆分；布局视口需要保留模型及视口关系，暂不输出。");
            if(!string.IsNullOrWhiteSpace(frame.HandleOrId))
            {
                var source=tr.GetObject(SplitId(db,frame.HandleOrId),OpenMode.ForRead) as Entity;
                if(source==null || source.IsErased || source.OwnerId!=model.ObjectId)throw new InvalidOperationException("图框已删除或移动，请重新搜索。");
                var bounds=source.GeometricExtents;
                if(Math.Abs(bounds.MinPoint.X-frame.MinX)>1e-6 || Math.Abs(bounds.MinPoint.Y-frame.MinY)>1e-6
                    || Math.Abs(bounds.MaxPoint.X-frame.MaxX)>1e-6 || Math.Abs(bounds.MaxPoint.Y-frame.MaxY)>1e-6)
                    throw new InvalidOperationException("图框范围已变化，请重新搜索。");
            }
            var handles=new List<string>();var warnings=new List<string>();var errors=new List<string>();int crossing=0;
            var visited=new HashSet<ObjectId>();
            foreach(ObjectId id in model)
            {
                var entity=tr.GetObject(id,OpenMode.ForRead) as Entity;
                if(entity==null || entity.IsErased)continue;
                SplitRelation relation;
                try
                {
                    var e=entity.GeometricExtents;
                    relation=DwgSplitGeometry.Classify(frame,e.MinPoint.X,e.MinPoint.Y,e.MaxPoint.X,e.MaxPoint.Y);
                }
                catch(Exception ex){errors.Add("实体 "+id.Handle+" 无法取得范围，不能确认是否遗漏："+ex.Message);continue;}
                if(relation==SplitRelation.Outside)continue;
                handles.Add(id.Handle.ToString());
                if(relation==SplitRelation.Crossing)crossing++;
                InspectSplitDependency(entity,tr,visited,errors,0);
            }
            if(handles.Count==0)errors.Add("范围内没有可拆分实体。");
            if(crossing>0)warnings.Add(crossing+" 个实体跨边界，将整件保留，可能带入相邻图纸内容。");
            warnings.Add("按 WCS 包围盒相交选取（含边界、隐藏及关闭图层实体），不裁切、不移动坐标；复杂块可能超出图框。");
            warnings.Add("外部字体不打包；对象字段及跨图关联需在目标图复核，拆分结果待 CAD 原生验收。");
            return new DwgSplitPlan(frame,handles,crossing,warnings,errors);
        }
        private static ObjectId SplitId(Database db,string handle) => db.GetObjectId(false,
            new Handle(long.Parse(handle,NumberStyles.HexNumber,CultureInfo.InvariantCulture)),0);
        private static void InspectSplitDependency(Entity entity,Transaction tr,HashSet<ObjectId> visited,List<string> errors,int depth)
        {
            if(depth>32){errors.Add("块嵌套超过 32 层，无法完整核验依赖。");return;}
            // 外部资源和代理实体需要专门的依赖打包流程，当前拒绝产生可能缺图的结果。
            string type=entity.GetRXClass().DxfName.ToUpperInvariant();
            if(type.Contains("IMAGE") || type.Contains("UNDERLAY") || type.Contains("PDF") || type.Contains("DGN")
                || type.Contains("DWF") || type.Contains("PROXY") || type.Contains("OLE") || type.Contains("POINTCLOUD"))
                errors.Add("实体 "+entity.Handle+" 为 "+type+"，外部资源/代理对象暂不支持可靠拆分。");
            if(entity is BlockReference block && visited.Add(block.BlockTableRecord))
            {
                var definition=(BlockTableRecord)tr.GetObject(block.BlockTableRecord,OpenMode.ForRead);
                if(definition.IsFromExternalReference || definition.IsFromOverlayReference)
                {errors.Add("外参块 "+definition.Name+"："+definition.PathName+" 尚未打包，暂不拆分。");return;}
                foreach(ObjectId id in definition)
                    if(tr.GetObject(id,OpenMode.ForRead) is Entity child)InspectSplitDependency(child,tr,visited,errors,depth+1);
            }
        }
        public void ExportDwgSplit(DwgSplitPlan inspectedPlan,string targetPath)
        {
            if(!inspectedPlan.CanExport)throw new InvalidOperationException("拆分预检未通过。");
            var target=Path.GetFullPath(targetPath);
            if(!string.Equals(Path.GetExtension(target),".dwg",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("目标必须为 DWG。");
            if(File.Exists(target)||Directory.Exists(target))throw new IOException("目标已存在，拒绝覆盖："+target);
            var doc=Application.DocumentManager.MdiActiveDocument;
            if(doc==null)throw new InvalidOperationException("没有活动文档。");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string temp=Path.Combine(Path.GetDirectoryName(target)!,".split-"+Guid.NewGuid().ToString("N")+".dwg");
            try
            {
                using(doc.LockDocument())
                using(var tr=doc.Database.TransactionManager.StartTransaction())
                {
                    var fresh=InspectSplit(doc,tr,inspectedPlan.Source);
                    if(!inspectedPlan.Matches(fresh))throw new InvalidOperationException("拆分范围或依赖发生变化，请重新预检。");
                    var ids=new ObjectIdCollection(fresh.Handles.Select(h=>SplitId(doc.Database,h)).ToArray());
                    // Wblock 深克隆关联符号及块定义，源数据库只读；不使用屏幕选择或逐对象爆炸。
                    using(var output=doc.Database.Wblock(ids,Point3d.Origin))
                    {output.Insunits=doc.Database.Insunits;output.SaveAs(temp,DwgVersion.AC1027);}
                }
                using(var check=new Database(false,true))
                {
                    check.ReadDwgFile(temp,FileOpenMode.OpenForReadAndAllShare,false,"");check.CloseInput(true);
                    using(var tr=check.TransactionManager.StartTransaction())
                    {
                        var table=(BlockTable)tr.GetObject(check.BlockTableId,OpenMode.ForRead);
                        var model=(BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForRead);
                        if(model.Cast<ObjectId>().Count()!=inspectedPlan.Handles.Count)
                            throw new InvalidDataException("拆分后顶层实体数量不一致，未提交目标文件。");
                    }
                }
                File.Move(temp,target);
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}

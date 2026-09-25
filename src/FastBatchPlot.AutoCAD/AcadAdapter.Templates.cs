using System;
using System.Collections.Generic;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace FastBatchPlot.AutoCAD
{
    public partial class AcadAdapter
    {
        public bool FindTemplateSample(string blockName, out PlotFrame? frame)
        {
            frame = null;
            if (string.IsNullOrWhiteSpace(blockName)) return false;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("没有活动文档。");

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                double bestArea = -1;
                foreach (ObjectId id in space)
                {
                    var block = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                    if (block == null || block.IsErased) continue;
                    var definition = (BlockTableRecord)tr.GetObject(
                        block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead);
                    if (!string.Equals(definition.Name, blockName.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    Extents3d bounds;
                    try { bounds = block.GeometricExtents; } catch { continue; }
                    double area = Math.Abs((bounds.MaxPoint.X - bounds.MinPoint.X) * (bounds.MaxPoint.Y - bounds.MinPoint.Y));
                    if (area <= bestArea) continue;
                    bestArea = area;
                    frame = new PlotFrame
                    {
                        Type = FrameType.BlockReference,
                        SourceBlockName = definition.Name,
                        SourceLayer = block.Layer,
                        HandleOrId = block.Handle.ToString(),
                        SourceDocumentId = DocumentSessionIdentity.Get(doc),
                        SourceLayoutId = block.OwnerId.Handle.ToString(),
                        SourceFileName = System.IO.Path.GetFileName(doc.Name),
                        MinX = bounds.MinPoint.X,
                        MinY = bounds.MinPoint.Y,
                        MaxX = bounds.MaxPoint.X,
                        MaxY = bounds.MaxPoint.Y
                    };
                }
                tr.Commit();
            }
            return frame != null;
        }

        public bool PromptSelectTemplateSample(out PlotFrame? frame)
        {
            frame = null;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("没有活动文档。");

            var options = new PromptEntityOptions("\n请在 CAD 图面点选此模板对应的图框块：");
            options.SetRejectMessage("\n请选择图框块参照。");
            options.AddAllowedClass(typeof(BlockReference), false);
            var selected = doc.Editor.GetEntity(options);
            if (selected.Status != PromptStatus.OK) return false;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var block = tr.GetObject(selected.ObjectId, OpenMode.ForRead) as BlockReference;
                if (block == null || block.IsErased) throw new InvalidOperationException("拾取对象已失效，请重新选择图框块。");
                if (block.OwnerId != doc.Database.CurrentSpaceId) throw new InvalidOperationException("请在当前模型空间或布局中拾取图框块。");
                var definition = (BlockTableRecord)tr.GetObject(block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead);
                var bounds = block.GeometricExtents;
                frame = new PlotFrame
                {
                    Type = FrameType.BlockReference,
                    SourceBlockName = definition.Name,
                    SourceLayer = block.Layer,
                    HandleOrId = block.Handle.ToString(),
                    SourceDocumentId = DocumentSessionIdentity.Get(doc),
                    SourceLayoutId = block.OwnerId.Handle.ToString(),
                    SourceFileName = System.IO.Path.GetFileName(doc.Name),
                    MinX = bounds.MinPoint.X,
                    MinY = bounds.MinPoint.Y,
                    MaxX = bounds.MaxPoint.X,
                    MaxY = bounds.MaxPoint.Y
                };
                tr.Commit();
            }
            return true;
        }

        private static BlockReference OpenTemplateBlock(Document doc, Transaction tr, PlotFrame frame)
        {
            if (frame.Type != FrameType.BlockReference || frame.SourceDocumentId != DocumentSessionIdentity.Get(doc)
                || string.IsNullOrWhiteSpace(frame.HandleOrId) || string.IsNullOrWhiteSpace(frame.SourceLayoutId))
                throw new InvalidOperationException("请选择当前文档中识别到的图框块。");
            long handle;
            if (!long.TryParse(frame.HandleOrId, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out handle)) throw new InvalidOperationException("图框句柄无效。");
            var id = doc.Database.GetObjectId(false, new Handle(handle), 0);
            var block = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
            if (block == null || block.IsErased || block.OwnerId.Handle.ToString() != frame.SourceLayoutId)
                throw new InvalidOperationException("图框已删除、移动到其他空间或来源不匹配，请重新搜索。");
            var definition = (BlockTableRecord)tr.GetObject(block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord, OpenMode.ForRead);
            if (!string.Equals(definition.Name, frame.SourceBlockName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("图框块名已变化，请重新搜索后应用模板。");
            return block;
        }

        private static Point3d TemplateAnchor(BlockReference block, Transaction tr)
        {
            var definition = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
            double maxX = double.NegativeInfinity, minY = double.PositiveInfinity;
            foreach (ObjectId id in definition)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null || !entity.Visible || entity is DBText || entity is MText) continue;
                var extent = entity.GeometricExtents;
                maxX = Math.Max(maxX, extent.MaxPoint.X); minY = Math.Min(minY, extent.MinPoint.Y);
            }
            if (!TitleTemplateService.Finite(maxX) || !TitleTemplateService.Finite(minY))
                return Point3d.Origin; // 纯文字图框没有稳定的线框范围；块原点可在所有同定义实例间保持一致。
            return new Point3d(maxX, minY, 0);
        }

        public bool PromptTemplateRegion(PlotFrame frame, out TemplateRegion? region)
        {
            region = null;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || !IsFrameContextCurrent(frame)) throw new InvalidOperationException("请先切回图框来源文档和空间再拾取区域。");
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var block = OpenTemplateBlock(doc, tr, frame);
                var inverse = block.BlockTransform.Inverse();
                var anchor = TemplateAnchor(block, tr);
                var ed = doc.Editor;
                var first = ed.GetPoint("\n选择字段区域第一角点：");
                if (first.Status != PromptStatus.OK) return false;
                var second = ed.GetCorner("\n选择字段区域对角点：", first.Value);
                if (second.Status != PromptStatus.OK) return false;
                // 检查整个 UCS 矩形的两条轴，而非将旋转矩形扩大成包围盒。
                var localTransform = inverse * ed.CurrentUserCoordinateSystem;
                var origin = Point3d.Origin.TransformBy(localTransform);
                var dx = new Point3d(1,0,0).TransformBy(localTransform) - origin;
                var dy = new Point3d(0,1,0).TransformBy(localTransform) - origin;
                bool aligned = Math.Abs(dx.Z) < 1e-8 && Math.Abs(dy.Z) < 1e-8
                    && (Math.Abs(dx.X) < 1e-8 || Math.Abs(dx.Y) < 1e-8)
                    && (Math.Abs(dy.X) < 1e-8 || Math.Abs(dy.Y) < 1e-8);
                if (!aligned) throw new NotSupportedException("当前 UCS 与图框本地坐标不平行，请先对齐 UCS，或在模板窗口输入本地区域坐标。");
                var a = first.Value.TransformBy(localTransform); var b = second.Value.TransformBy(localTransform);
                region = new TemplateRegion { X1 = Math.Min(a.X,b.X)-anchor.X, Y1 = Math.Min(a.Y,b.Y)-anchor.Y,
                    X2 = Math.Max(a.X,b.X)-anchor.X, Y2 = Math.Max(a.Y,b.Y)-anchor.Y };
                if (region.X1 >= region.X2 || region.Y1 >= region.Y2) throw new InvalidOperationException("区域宽高必须大于零。");
                return true;
            }
        }

        public List<TemplateText> CollectTemplateTexts(PlotFrame frame)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("没有活动文档。");
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var block = OpenTemplateBlock(doc, tr, frame);
                if ((block.Normal - Vector3d.ZAxis).Length > 1e-8) throw new NotSupportedException("暂不支持倾斜平面的图框模板。");
                var inverse = block.BlockTransform.Inverse();
                var anchor = TemplateAnchor(block, tr);
                var result = new List<TemplateText>();
                var space = (BlockTableRecord)tr.GetObject(block.OwnerId, OpenMode.ForRead);
                foreach (ObjectId id in space)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity is DBText || entity is MText)
                        AddTemplateText(entity, Matrix3d.Identity, inverse, anchor, "", result);
                }
                CollectBlockTexts(block, Matrix3d.Identity, inverse, anchor, tr, result, new HashSet<ObjectId>(), 0);
                return result;
            }
        }

        private static void CollectBlockTexts(BlockReference block, Matrix3d parentToWorld, Matrix3d worldToSource,
            Point3d anchor, Transaction tr, List<TemplateText> result, HashSet<ObjectId> path, int depth)
        {
            if (depth > 32 || !path.Add(block.BlockTableRecord)) throw new InvalidOperationException("图框嵌套过深或存在循环引用。");
            try
            {
                foreach (ObjectId id in block.AttributeCollection)
                {
                    var attr = tr.GetObject(id, OpenMode.ForRead) as AttributeReference;
                    if (attr != null && !attr.Invisible)
                        AddTemplateText(attr, parentToWorld, worldToSource, anchor, attr.Tag, result);
                }
                var localToWorld = parentToWorld * block.BlockTransform;
                var definition = (BlockTableRecord)tr.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                if (definition.IsFromExternalReference) throw new NotSupportedException("外参内文字提取尚未支持，请使用本地图框块。");
                foreach (ObjectId id in definition)
                {
                    var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity == null || !entity.Visible) continue;
                    if (entity is AttributeDefinition ad)
                    {
                        if (ad.Constant && !ad.Invisible) AddTemplateText(ad, localToWorld, worldToSource, anchor, ad.Tag, result);
                    }
                    else if (entity is BlockReference nested)
                        CollectBlockTexts(nested, localToWorld, worldToSource, anchor, tr, result, path, depth+1);
                    else if (entity is DBText || entity is MText)
                        AddTemplateText(entity, localToWorld, worldToSource, anchor, "", result);
                }
            }
            finally { path.Remove(block.BlockTableRecord); }
        }

        private static void AddTemplateText(Entity entity, Matrix3d toWorld, Matrix3d worldToSource,
            Point3d anchor, string tag, List<TemplateText> result)
        {
            if (!entity.Visible) return;
            string text = entity is MText mt ? mt.Text : ((DBText)entity).TextString;
            if (entity is AttributeReference ar && ar.IsMTextAttribute)
            {
                using (var attributeText = ar.MTextAttribute) text = attributeText.Text;
            }

            Point3d center;
            if (string.IsNullOrWhiteSpace(text)) center=entity is MText emptyMt?emptyMt.Location:((DBText)entity).Position;
            else { var extent = entity.GeometricExtents;
            center = new Point3d((extent.MinPoint.X+extent.MaxPoint.X)/2,
                (extent.MinPoint.Y+extent.MaxPoint.Y)/2,(extent.MinPoint.Z+extent.MaxPoint.Z)/2); }
            center=center.TransformBy(toWorld).TransformBy(worldToSource);
            var item=new TemplateText { Text = text, AttributeTag = tag, X = center.X-anchor.X, Y = center.Y-anchor.Y };
            DescribeWriteTarget(entity,item);result.Add(item);
            if (result.Count > 100000) throw new InvalidOperationException("单次文字采集超过十万项，已停止以避免资源耗尽。");
        }
    }
}


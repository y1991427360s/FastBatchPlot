using System;
using System.Linq;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;

namespace FastBatchPlot.ZWCAD
{
    public partial class ZwCadAdapter
    {
        public CadCandidateSnapshot CollectCurrentSpaceCandidates(string layerFilter = "*")
            => CollectCandidates(CadScanScope.CurrentSpace, layerFilter);

        public CadCandidateSnapshot CollectCandidates(CadScanScope scope, string layerFilter = "*")
        {
            if (!Enum.IsDefined(typeof(CadScanScope), scope)) throw new ArgumentOutOfRangeException(nameof(scope));
            var result = new CadCandidateSnapshot();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var layouts = ZwCadLayoutContext.ReadLayouts(doc.Database, tr)
                    .Where(layout => scope == CadScanScope.ModelAndLayouts
                        || (scope == CadScanScope.Model && layout.ModelType)
                        || (scope == CadScanScope.AllLayouts && !layout.ModelType)
                        || (scope == CadScanScope.CurrentSpace && layout.BlockTableRecordId == doc.Database.CurrentSpaceId))
                    .OrderBy(layout => layout.ModelType ? 0 : 1).ThenBy(layout => layout.TabOrder)
                    .ThenBy(layout => layout.LayoutName, StringComparer.Ordinal).ToList();
                foreach (var layout in layouts)
                {
                    var space = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        try
                        {
                            var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (entity == null || !MatchesFilter(entity.Layer, layerFilter)) continue;
                            ExtractCandidateFromEntity(entity, tr, result.Polylines, result.Blocks,
                                DocumentSessionIdentity.Get(doc), space.Handle.ToString(), layout.LayoutName, layout.TabOrder, System.IO.Path.GetFileName(doc.Name));
                        }
                        catch (Exception ex) { result.Warnings.Add(layout.LayoutName + "/" + id.Handle + "：" + ex.Message); }
                    }
                }
                tr.Commit();
            }
            return result;
        }

        public bool CanAccessFrameContext(PlotFrame frame)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || frame == null || string.IsNullOrEmpty(frame.SourceDocumentId)
                || !string.Equals(frame.SourceDocumentId, DocumentSessionIdentity.Get(doc), StringComparison.Ordinal)
                || string.IsNullOrEmpty(frame.SourceLayoutId)) return false;
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                    return ZwCadLayoutContext.ReadLayouts(doc.Database, tr).Any(layout =>
                        layout.BlockTableRecordId.Handle.ToString() == frame.SourceLayoutId);
            }
            catch { return false; }
        }

        public bool PromptManualFrame(ManualFrameSelectionMode mode, out PlotFrame? frame)
        {
            frame = null;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;
            var ed = doc.Editor;
            Rect2D bounds;
            PromptDoubleResult scale;
            using (BeginUserInteraction(ed))
            {
            if (mode == ManualFrameSelectionMode.TwoCorners)
            {
                var first = ed.GetPoint("\n请选择打印范围第一个角点：");
                if (first.Status != PromptStatus.OK) return false;
                var second = ed.GetCorner("\n请选择打印范围对角点：", first.Value);
                if (second.Status != PromptStatus.OK) return false;
                var ucs = ed.CurrentUserCoordinateSystem;
                var x = ucs.CoordinateSystem3d.Xaxis;
                var y = ucs.CoordinateSystem3d.Yaxis;
                bool aligned = Math.Abs(x.Z) < 1e-8 && Math.Abs(y.Z) < 1e-8 &&
                    (Math.Abs(x.X) < 1e-8 || Math.Abs(x.Y) < 1e-8) &&
                    (Math.Abs(y.X) < 1e-8 || Math.Abs(y.Y) < 1e-8);
                if (!aligned) throw new InvalidOperationException("两点范围暂需与世界坐标轴平行的 UCS；请切换后选择。");
                var p1 = first.Value.TransformBy(ucs);
                var p2 = second.Value.TransformBy(ucs);
                bounds = new Rect2D(p1.X,p1.Y,p2.X,p2.Y);
            }
            else
            {
                var selection = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\n选择作为一张图纸的全部实体，回车确定：" });
                if (selection.Status != PromptStatus.OK || selection.Value == null || selection.Value.Count == 0) return false;
                double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
                double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    int skipped = 0;
                    foreach (SelectedObject selected in selection.Value)
                    {
                        if (selected == null) continue;
                        var entity = tr.GetObject(selected.ObjectId, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;
                        if (entity is Xline || entity is Ray)
                            throw new InvalidOperationException("所选图形包含无限长的构造线/射线，无法确定打印范围，请去掉后重选。");
                        Extents3d extent;
                        // 空文字、空块等没有范围的实体不影响打印范围，跳过并提示数量。
                        try { extent = entity.GeometricExtents; } catch { skipped++; continue; }
                        minX = Math.Min(minX, extent.MinPoint.X); minY = Math.Min(minY, extent.MinPoint.Y);
                        maxX = Math.Max(maxX, extent.MaxPoint.X); maxY = Math.Max(maxY, extent.MaxPoint.Y);
                    }
                    tr.Commit();
                    if (skipped > 0) ed.WriteMessage($"\n[FastBatchPlot] {skipped} 个实体没有几何范围，已忽略。");
                }
                if (!(maxX > minX && maxY > minY)) throw new InvalidOperationException("所选图形没有有效的二维范围。");
                bounds = new Rect2D(minX,minY,maxX,maxY);
            }

            var detected = FastBatchPlot.Core.Paper.PaperSizeDetector.Detect(bounds.Width, bounds.Height);
            bool isHighConfidence = detected.MatchScore <= FastBatchPlot.Core.Paper.PaperSizeDetector.AcceptableMatchError && detected.Scale > 0;

            PromptDoubleOptions scaleOptions;
            if (isHighConfidence)
            {
                string formattedScale = FastBatchPlot.Core.Paper.ScaleCalculator.FormatScale(detected.Scale);
                ed.WriteMessage($"\n[FastBatchPlot] 自动识别：{detected.Paper.Name}，推荐比例 {formattedScale}。");
                scaleOptions = new PromptDoubleOptions($"\n请输入打印比例分母 1:<{detected.Scale:0.##}>：")
                {
                    DefaultValue = detected.Scale,
                    UseDefaultValue = true,
                    AllowNegative = false,
                    AllowZero = false
                };
            }
            else
            {
                ed.WriteMessage("\n[FastBatchPlot] 当前范围无法可靠识别标准图幅和比例，请手工输入打印比例。");
                scaleOptions = new PromptDoubleOptions("\n请输入打印比例分母 1:<100>：")
                {
                    DefaultValue = 100,
                    UseDefaultValue = false,
                    AllowNegative = false,
                    AllowZero = false
                };
            }

            scale = ed.GetDouble(scaleOptions);
            if (scale.Status != PromptStatus.OK) return false;
            }
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                var layout = (Layout)tr.GetObject(space.LayoutId, OpenMode.ForRead);
                frame = ManualFrameFactory.Create(bounds,scale.Value,DocumentSessionIdentity.Get(doc),space.Handle.ToString(),layout.LayoutName);
                frame.LayoutOrder = layout.TabOrder;
                frame.SourceFileName = System.IO.Path.GetFileName(doc.Name);
                tr.Commit();
            }
            return true;
        }
    }
}

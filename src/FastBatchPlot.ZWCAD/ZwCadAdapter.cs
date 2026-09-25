using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    public partial class ZwCadAdapter : ICadHost, ICadContextHost, ICadFrameSelectionHost, ICadLayoutHost, ICadTitleTemplateHost, ICadHighlightHost
    {
        public string PlatformName => "ZWCAD";
        public string Version => Application.Version.ToString();

        public bool IsFrameContextCurrent(PlotFrame frame)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            // 布局中激活了浮动视口时 CurrentSpaceId 为模型空间，但缩放会改变视口比例，不视为可定位。
            bool floating = Convert.ToInt32(Application.GetSystemVariable("TILEMODE")) == 0 &&
                Convert.ToInt32(Application.GetSystemVariable("CVPORT")) != 1;
            return doc != null && !floating && frame.SourceDocumentId == DocumentSessionIdentity.Get(doc) &&
                frame.SourceLayoutId == doc.Database.CurrentSpaceId.Handle.ToString();
        }

        public void WriteMessage(string message)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage(message);
        }

        public string GetCurrentDocumentPath()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc?.Database.Filename ?? string.Empty;
        }

        public string GetCurrentDocumentName()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc?.Name ?? "Drawing";
        }

        public List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter = "*")
            => CollectCurrentSpaceCandidates(layerFilter).Polylines;

        public List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter = "*")
            => CollectCurrentSpaceCandidates().Blocks.Where(b => MatchesFilter(b.BlockName, blockNameFilter)).ToList();

        public bool PromptSelectFrames(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks)
        {
            polylines = new List<RawPolylineCandidate>();
            blocks = new List<RawBlockCandidate>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            var ed = doc.Editor;
            var promptOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\n[FastBatchPlot] 请框选要打印的图框或图纸区域: "
            };

            PromptSelectionResult selRes;
            using (BeginUserInteraction(ed)) selRes = ed.GetSelection(promptOptions);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                return false;
            }

            ExtractSelection(doc, selRes.Value, polylines, blocks);
            return (polylines.Count > 0 || blocks.Count > 0);
        }

        public enum FramePromptOutcome { Cancelled, Selected, Scope }

        /// <summary>
        /// 与原版一致的命令流程：先在图中选择要打印的图框；不选直接回车，则选择遍历模型/布局的范围。
        /// 在命令上下文中提示，不存在从非模态窗口拾取的焦点问题。
        /// </summary>
        public FramePromptOutcome PromptFramesOrScope(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks, out CadScanScope scope)
        {
            polylines = new List<RawPolylineCandidate>();
            blocks = new List<RawBlockCandidate>();
            scope = CadScanScope.CurrentSpace;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return FramePromptOutcome.Cancelled;
            var ed = doc.Editor;
            var selection = ed.GetSelection(new PromptSelectionOptions
            {
                MessageForAdding = "\n[FastBatchPlot] 选择要打印的图框（矩形、图块），直接回车则遍历模型/布局: "
            });
            if (selection.Status == PromptStatus.OK && selection.Value != null && selection.Value.Count > 0)
            {
                ExtractSelection(doc, selection.Value, polylines, blocks);
                if (polylines.Count == 0 && blocks.Count == 0)
                    ed.WriteMessage("\n[FastBatchPlot] 所选对象中没有闭合矩形或图块。");
                return FramePromptOutcome.Selected;
            }
            if (selection.Status == PromptStatus.Cancel) return FramePromptOutcome.Cancelled;

            var options = new PromptKeywordOptions("\n遍历范围 [当前空间(C)/模型(M)/全部布局(L)/模型和布局(A)] <当前空间>: ")
            {
                AppendKeywordsToMessage = false,
                AllowNone = true
            };
            foreach (var keyword in new[] { "C", "M", "L", "A" }) options.Keywords.Add(keyword);
            var answer = ed.GetKeywords(options);
            if (answer.Status == PromptStatus.Cancel) return FramePromptOutcome.Cancelled;
            switch ((answer.Status == PromptStatus.OK ? answer.StringResult : "C").ToUpperInvariant())
            {
                case "M": scope = CadScanScope.Model; break;
                case "L": scope = CadScanScope.AllLayouts; break;
                case "A": scope = CadScanScope.ModelAndLayouts; break;
                default: scope = CadScanScope.CurrentSpace; break;
            }
            return FramePromptOutcome.Scope;
        }

        private static void ExtractSelection(Document doc, SelectionSet selection, List<RawPolylineCandidate> polylines, List<RawBlockCandidate> blocks)
        {
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                var layout = (Layout)tr.GetObject(space.LayoutId, OpenMode.ForRead);
                int skipped = 0;
                foreach (SelectedObject selObj in selection)
                {
                    if (selObj == null) continue;
                    // 空块、未加载外参等无法计算范围的实体只跳过自身，不能让整次框选失败。
                    try
                    {
                        var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                        ExtractCandidateFromEntity(ent, tr, polylines, blocks, DocumentSessionIdentity.Get(doc), space.Handle.ToString(), layout.LayoutName, layout.TabOrder, System.IO.Path.GetFileName(doc.Name));
                    }
                    catch { skipped++; }
                }
                tr.Commit();
                if (skipped > 0) doc.Editor.WriteMessage($"\n[FastBatchPlot] 已跳过 {skipped} 个无法计算范围的实体（空块、未加载外参等）。");
            }
        }

        public bool PromptSelectSampleBlock(out string blockName, out string layerName)
        {
            blockName = string.Empty;
            layerName = string.Empty;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return false;

            var ed = doc.Editor;
            var pOpt = new PromptEntityOptions("\n[FastBatchPlot] 请在图中点选一个图框实体 (图块或矩形边框): ");
            pOpt.SetRejectMessage("\n请选择有效的实体。");

            PromptEntityResult res;
            using (BeginUserInteraction(ed)) res = ed.GetEntity(pOpt);
            if (res.Status != PromptStatus.OK) return false;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Entity;
                if (ent != null)
                {
                    layerName = ent.Layer;
                    if (ent is BlockReference blkRef)
                    {
                        blockName = blkRef.Name;
                        if (blkRef.IsDynamicBlock)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead);
                            blockName = btr.Name;
                        }
                    }
                }
                tr.Commit();
            }

            return !string.IsNullOrEmpty(blockName) || !string.IsNullOrEmpty(layerName);
        }

        private static void ExtractCandidateFromEntity(DBObject ent, Transaction tr, List<RawPolylineCandidate> polylines, List<RawBlockCandidate> blocks, string documentId, string layoutId, string layoutName, int layoutOrder, string sourceFileName)
        {
            if (ent is Polyline pline)
            {
                bool isClosed = pline.Closed || (pline.NumberOfVertices >= 4 && pline.StartPoint.DistanceTo(pline.EndPoint) < 2.0);
                if (isClosed && pline.NumberOfVertices >= 4)
                {
                    var cand = new RawPolylineCandidate
                    {
                        Handle = pline.Handle.ToString(),
                        SourceDocumentId = documentId,
                        SourceFileName = sourceFileName,
                        SourceLayoutId = layoutId,
                        LayoutName = layoutName,
                        LayoutOrder = layoutOrder,
                        Layer = pline.Layer
                    };

                    if ((pline.Normal - Vector3d.ZAxis).Length > 1e-8) return;
                    for (int i = 0; i < pline.NumberOfVertices; i++)
                    {
                        // 圆弧段的四个端点不能作为矩形识别。
                        if (Math.Abs(pline.GetBulgeAt(i)) > 1e-10) return;
                        var pt = pline.GetPoint3dAt(i);
                        cand.Vertices.Add(new Point2D(pt.X, pt.Y));
                    }
                    polylines.Add(cand);
                }
            }
            else if (ent is Polyline2d pl2d)
            {
                // 旧式多段线也必须是 WCS 平面直线段，不能把圆弧/拟合曲线的顶点误认成矩形。
                if ((pl2d.Normal - Vector3d.ZAxis).Length > 1e-8 || pl2d.PolyType != Poly2dType.SimplePoly) return;
                bool isClosed = pl2d.Closed;
                var vertices = new List<Point2D>();
                foreach (ObjectId vId in pl2d)
                {
                    if (tr.GetObject(vId, OpenMode.ForRead) is Vertex2d v)
                    {
                        if (Math.Abs(v.Bulge) > 1e-10) return;
                        vertices.Add(new Point2D(v.Position.X, v.Position.Y));
                    }
                }

                if (!isClosed && vertices.Count >= 4 && vertices[0].DistanceTo(vertices[vertices.Count - 1]) < 2.0)
                {
                    isClosed = true;
                }

                if (isClosed && vertices.Count >= 4)
                {
                    polylines.Add(new RawPolylineCandidate
                    {
                        Handle = pl2d.Handle.ToString(),
                        SourceDocumentId = documentId,
                        SourceFileName = sourceFileName,
                        SourceLayoutId = layoutId,
                        LayoutName = layoutName,
                        LayoutOrder = layoutOrder,
                        Layer = pl2d.Layer,
                        Vertices = vertices
                    });
                }
            }
            else if (ent is BlockReference blkRef)
            {
                string blockName = blkRef.Name;
                if (blkRef.IsDynamicBlock)
                {
                    var btr = (BlockTableRecord)tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead);
                    blockName = btr.Name;
                }

                {
                    var bounds = blkRef.GeometricExtents;
                    var cand = new RawBlockCandidate
                    {
                        Handle = blkRef.Handle.ToString(),
                        SourceDocumentId = documentId,
                        SourceFileName = sourceFileName,
                        SourceLayoutId = layoutId,
                        LayoutName = layoutName,
                        LayoutOrder = layoutOrder,
                        BlockName = blockName,
                        Layer = blkRef.Layer,
                        InsertionX = blkRef.Position.X,
                        InsertionY = blkRef.Position.Y,
                        ScaleX = blkRef.ScaleFactors.X,
                        ScaleY = blkRef.ScaleFactors.Y,
                        RotationDegrees = blkRef.Rotation * 180.0 / Math.PI,
                        Bounds = new Rect2D(bounds.MinPoint.X, bounds.MinPoint.Y, bounds.MaxPoint.X, bounds.MaxPoint.Y)
                    };

                    if (blkRef.AttributeCollection != null)
                    {
                        foreach (ObjectId attId in blkRef.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
                            {
                                cand.Attributes[attRef.Tag] = attRef.TextString;
                            }
                        }
                    }

                    blocks.Add(cand);
                }
            }
        }

        public void ZoomToFrame(double minX, double minY, double maxX, double maxY)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (doc.LockDocument())
            {
                var ed = doc.Editor;
                // 基于当前视图（保留视图方向、目标点与扭转角），把 WCS 范围换算到显示坐标系后再缩放。
                using (var view = ed.GetCurrentView())
                {
                    var worldToDcs = (Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) *
                        Matrix3d.Displacement(view.Target - Point3d.Origin) *
                        Matrix3d.PlaneToWorld(view.ViewDirection)).Inverse();
                    var a = new Point3d(minX, minY, 0).TransformBy(worldToDcs);
                    var b = new Point3d(maxX, maxY, 0).TransformBy(worldToDcs);
                    var c = new Point3d(minX, maxY, 0).TransformBy(worldToDcs);
                    var d = new Point3d(maxX, minY, 0).TransformBy(worldToDcs);
                    double dx0 = Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)), dx1 = Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X));
                    double dy0 = Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)), dy1 = Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y));
                    view.CenterPoint = new Point2d((dx0 + dx1) / 2.0, (dy0 + dy1) / 2.0);
                    view.Width = Math.Max(dx1 - dx0, 1e-6) * 1.1;
                    view.Height = Math.Max(dy1 - dy0, 1e-6) * 1.1;
                    ed.SetCurrentView(view);
                }
                ed.UpdateScreen();
            }
        }

        private static bool MatchesFilter(string value, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == "*") return true;
            string pattern = "^" + Regex.Escape(filter).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public List<string> GetAllLayers()
        {
            var list = new List<string>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return list;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    list.Add(ltr.Name);
                }
                tr.Commit();
            }
            return list;
        }

        public List<string> GetAllBlockNames()
        {
            var list = new List<string>();
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return list;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in bt)
                {
                    var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (!btr.IsLayout && !btr.IsAnonymous)
                    {
                        list.Add(btr.Name);
                    }
                }
                tr.Commit();
            }
            return list;
        }
    }
}

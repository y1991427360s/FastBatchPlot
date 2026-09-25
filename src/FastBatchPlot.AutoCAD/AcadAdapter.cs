using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace FastBatchPlot.AutoCAD
{
    public partial class AcadAdapter : ICadHost, ICadContextHost, ICadFrameSelectionHost, ICadLayoutHost, ICadTitleTemplateHost, ICadHighlightHost
    {
        public string PlatformName => "AutoCAD";
        public string Version => Application.Version.ToString();

        public bool IsFrameContextCurrent(PlotFrame frame)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            return doc != null && frame.SourceDocumentId == DocumentSessionIdentity.Get(doc) &&
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

            var selRes = ed.GetSelection(promptOptions);
            if (selRes.Status != PromptStatus.OK || selRes.Value == null || selRes.Value.Count == 0)
            {
                return false;
            }

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                var layout = (Layout)tr.GetObject(space.LayoutId, OpenMode.ForRead);
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                    ExtractCandidateFromEntity(ent, tr, polylines, blocks, DocumentSessionIdentity.Get(doc), space.Handle.ToString(), layout.LayoutName, layout.TabOrder, System.IO.Path.GetFileName(doc.Name));
                }
                tr.Commit();
            }

            return (polylines.Count > 0 || blocks.Count > 0);
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

            var res = ed.GetEntity(pOpt);
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
                using (var view = new ViewTableRecord())
                {
                    view.CenterPoint = new Point2d((minX + maxX) / 2.0, (minY + maxY) / 2.0);
                    view.Width = Math.Abs(maxX - minX) * 1.1;
                    view.Height = Math.Abs(maxY - minY) * 1.1;
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

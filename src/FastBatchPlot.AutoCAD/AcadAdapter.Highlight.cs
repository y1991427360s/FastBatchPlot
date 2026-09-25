using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.AutoCAD
{
    public partial class AcadAdapter : ICadHighlightHost
    {
        private readonly List<Drawable> _markerDrawables = new List<Drawable>();
        private readonly List<Drawable> _currentHighlightDrawables = new List<Drawable>();
        private ObjectId _currentlyHighlightedEntityId = ObjectId.Null;

        public void ClearFrameMarkers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            try
            {
                UnhighlightActiveEntity(doc);
                var tm = TransientManager.CurrentTransientManager;
                var vp = new IntegerCollection();

                foreach (var d in _markerDrawables)
                {
                    try { tm.EraseTransient(d, vp); d.Dispose(); } catch { }
                }
                _markerDrawables.Clear();

                foreach (var d in _currentHighlightDrawables)
                {
                    try { tm.EraseTransient(d, vp); d.Dispose(); } catch { }
                }
                _currentHighlightDrawables.Clear();

                doc.Editor.UpdateScreen();
            }
            catch { }
        }

        public void ShowFrameMarkers(IReadOnlyList<PlotFrame> frames)
        {
            ClearFrameMarkers();
            if (frames == null || frames.Count == 0) return;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                var tm = TransientManager.CurrentTransientManager;
                var vp = new IntegerCollection();

                foreach (var frame in frames)
                {
                    if (!frame.IsSelected) continue;

                    double minX = Math.Min(frame.MinX, frame.MaxX);
                    double maxX = Math.Max(frame.MinX, frame.MaxX);
                    double minY = Math.Min(frame.MinY, frame.MaxY);
                    double maxY = Math.Max(frame.MinY, frame.MaxY);
                    double w = maxX - minX;
                    double h = maxY - minY;
                    if (w < 1e-3 || h < 1e-3) continue;

                    // 1. 醒目的亮红色外框 (Polyline, ConstantWidth 自适应)
                    var pl = new Polyline(4);
                    pl.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
                    pl.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
                    pl.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
                    pl.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
                    pl.Closed = true;
                    pl.ColorIndex = 1; // 亮红 (Red)
                    pl.ConstantWidth = Math.Max(1.5, Math.Min(w, h) * 0.005);

                    tm.AddTransient(pl, TransientDrawingMode.DirectShortTerm, 128, vp);
                    _markerDrawables.Add(pl);

                    // 2. 顶部纸张规格文字 (洋红色 Magenta = 6, 居中位于图框顶部上方)
                    string paperName = !string.IsNullOrWhiteSpace(frame.DetectedPaper?.Name)
                        ? frame.DetectedPaper!.Name
                        : "自定义";
                    string titleText = $"{paperName} ({(int)Math.Round(w)} X {(int)Math.Round(h)})";
                    double titleHeight = Math.Max(5.0, Math.Min(w, h) * 0.035);
                    double centerX = (minX + maxX) / 2.0;

                    var text = new DBText
                    {
                        TextString = titleText,
                        Height = titleHeight,
                        ColorIndex = 6, // 洋红 (Magenta)
                        HorizontalMode = TextHorizontalMode.TextCenter,
                        VerticalMode = TextVerticalMode.TextBottom,
                        AlignmentPoint = new Point3d(centerX, maxY + titleHeight * 0.3, 0),
                        Position = new Point3d(centerX, maxY + titleHeight * 0.3, 0)
                    };
                    try { text.AdjustAlignment(doc.Database); } catch { }

                    tm.AddTransient(text, TransientDrawingMode.DirectShortTerm, 128, vp);
                    _markerDrawables.Add(text);

                    // 3. 居中超大红色 7 段序号数字 (ColorIndex = 1, 亮红粗笔画)
                    var numberPolys = CreateNumberPolylines(frame.OrderIndex, minX, minY, maxX, maxY);
                    foreach (var np in numberPolys)
                    {
                        tm.AddTransient(np, TransientDrawingMode.DirectShortTerm, 128, vp);
                        _markerDrawables.Add(np);
                    }
                }

                doc.Editor.UpdateScreen();
            }
            catch { }
        }

        public void HighlightCurrentFrame(PlotFrame? frame)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                UnhighlightActiveEntity(doc);
                var tm = TransientManager.CurrentTransientManager;
                var vp = new IntegerCollection();

                foreach (var d in _currentHighlightDrawables)
                {
                    try { tm.EraseTransient(d, vp); d.Dispose(); } catch { }
                }
                _currentHighlightDrawables.Clear();

                if (frame != null)
                {
                    double minX = Math.Min(frame.MinX, frame.MaxX);
                    double maxX = Math.Max(frame.MinX, frame.MaxX);
                    double minY = Math.Min(frame.MinY, frame.MaxY);
                    double maxY = Math.Max(frame.MinY, frame.MaxY);
                    double w = maxX - minX;
                    double h = maxY - minY;
                    if (w > 1e-3 && h > 1e-3)
                    {
                        // 绘制亮黄色的强调边框（区别于常规红框），方便区分当前在列表中高亮选中的行
                        var pl = new Polyline(4);
                        pl.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
                        pl.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
                        pl.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
                        pl.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
                        pl.Closed = true;
                        pl.ColorIndex = 2; // 黄色 (Yellow)
                        pl.ConstantWidth = Math.Max(2.5, Math.Min(w, h) * 0.008);

                        tm.AddTransient(pl, TransientDrawingMode.Highlight, 128, vp);
                        _currentHighlightDrawables.Add(pl);
                    }

                    if (!string.IsNullOrEmpty(frame.HandleOrId) && long.TryParse(frame.HandleOrId, System.Globalization.NumberStyles.HexNumber, null, out long handleVal))
                    {
                        try
                        {
                            var handle = new Handle(handleVal);
                            if (doc.Database.TryGetObjectId(handle, out ObjectId id) && id.IsValid)
                            {
                                using (var tr = doc.Database.TransactionManager.StartTransaction())
                                {
                                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                    if (ent != null)
                                    {
                                        ent.Highlight();
                                        _currentlyHighlightedEntityId = id;
                                    }
                                    tr.Commit();
                                }
                            }
                        }
                        catch { }
                    }
                }

                doc.Editor.UpdateScreen();
            }
            catch { }
        }

        private static List<Polyline> CreateNumberPolylines(int number, double minX, double minY, double maxX, double maxY)
        {
            var polylines = new List<Polyline>();
            if (number <= 0) return polylines;

            double frameW = Math.Abs(maxX - minX);
            double frameH = Math.Abs(maxY - minY);
            if (frameW < 1e-3 || frameH < 1e-3) return polylines;

            double centerX = (minX + maxX) / 2.0;
            double centerY = (minY + maxY) / 2.0;

            // 占图幅高度约 65%
            double digitH = frameH * 0.65;
            double digitW = digitH * 0.48;
            double spacing = digitH * 0.22;

            string s = number.ToString();
            int n = s.Length;
            double totalW = n * digitW + (n - 1) * spacing;

            // 如果总宽度超过图框宽度的 80%，等比缩小
            if (totalW > frameW * 0.80)
            {
                double scale = (frameW * 0.80) / totalW;
                digitH *= scale;
                digitW *= scale;
                spacing *= scale;
                totalW = n * digitW + (n - 1) * spacing;
            }

            double strokeW = Math.Max(1.5, digitH * 0.065);
            double startX = centerX - totalW / 2.0;
            double startY = centerY - digitH / 2.0;

            for (int i = 0; i < n; i++)
            {
                double dx0 = startX + i * (digitW + spacing);
                double dx1 = dx0 + digitW;
                double dy0 = startY;
                double dy1 = startY + digitH;
                AppendDigitPolylines(polylines, s[i], dx0, dy0, dx1, dy1, strokeW);
            }

            return polylines;
        }

        private static void AppendDigitPolylines(List<Polyline> polylines, char digit, double dx0, double dy0, double dx1, double dy1, double strokeW)
        {
            double dyMid = (dy0 + dy1) / 2.0;

            Polyline MakePoly(Point2d[] pts, bool closed)
            {
                var pl = new Polyline(pts.Length);
                for (int i = 0; i < pts.Length; i++)
                {
                    pl.AddVertexAt(i, pts[i], 0, 0, 0);
                }
                pl.Closed = closed;
                pl.ColorIndex = 1; // 红色
                pl.ConstantWidth = strokeW;
                return pl;
            }

            switch (digit)
            {
                case '0':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dy0),
                        new Point2d(dx1, dy0),
                        new Point2d(dx1, dy1),
                        new Point2d(dx0, dy1)
                    }, true));
                    break;

                case '1':
                    double xc = (dx0 + dx1) / 2.0;
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(xc, dy0),
                        new Point2d(xc, dy1)
                    }, false));
                    break;

                case '2':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dy1),
                        new Point2d(dx1, dy1),
                        new Point2d(dx1, dyMid),
                        new Point2d(dx0, dyMid),
                        new Point2d(dx0, dy0),
                        new Point2d(dx1, dy0)
                    }, false));
                    break;

                case '3':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dy1),
                        new Point2d(dx1, dy1),
                        new Point2d(dx1, dyMid),
                        new Point2d(dx0, dyMid)
                    }, false));
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx1, dyMid),
                        new Point2d(dx1, dy0),
                        new Point2d(dx0, dy0)
                    }, false));
                    break;

                case '4':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dy1),
                        new Point2d(dx0, dyMid),
                        new Point2d(dx1, dyMid)
                    }, false));
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx1, dy1),
                        new Point2d(dx1, dy0)
                    }, false));
                    break;

                case '5':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx1, dy1),
                        new Point2d(dx0, dy1),
                        new Point2d(dx0, dyMid),
                        new Point2d(dx1, dyMid),
                        new Point2d(dx1, dy0),
                        new Point2d(dx0, dy0)
                    }, false));
                    break;

                case '6':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx1, dy1),
                        new Point2d(dx0, dy1),
                        new Point2d(dx0, dy0),
                        new Point2d(dx1, dy0),
                        new Point2d(dx1, dyMid),
                        new Point2d(dx0, dyMid)
                    }, false));
                    break;

                case '7':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dy1),
                        new Point2d(dx1, dy1),
                        new Point2d(dx1, dy0)
                    }, false));
                    break;

                case '8':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dyMid),
                        new Point2d(dx1, dyMid),
                        new Point2d(dx1, dy1),
                        new Point2d(dx0, dy1)
                    }, true));
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dy0),
                        new Point2d(dx1, dy0),
                        new Point2d(dx1, dyMid),
                        new Point2d(dx0, dyMid)
                    }, true));
                    break;

                case '9':
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx0, dyMid),
                        new Point2d(dx1, dyMid),
                        new Point2d(dx1, dy1),
                        new Point2d(dx0, dy1)
                    }, true));
                    polylines.Add(MakePoly(new[]
                    {
                        new Point2d(dx1, dyMid),
                        new Point2d(dx1, dy0),
                        new Point2d(dx0, dy0)
                    }, false));
                    break;
            }
        }

        private void UnhighlightActiveEntity(Document doc)
        {
            if (_currentlyHighlightedEntityId.IsNull || !_currentlyHighlightedEntityId.IsValid)
            {
                _currentlyHighlightedEntityId = ObjectId.Null;
                return;
            }
            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(_currentlyHighlightedEntityId, OpenMode.ForRead) as Entity;
                    ent?.Unhighlight();
                    tr.Commit();
                }
            }
            catch { }
            finally
            {
                _currentlyHighlightedEntityId = ObjectId.Null;
            }
        }
    }
}

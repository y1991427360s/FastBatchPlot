using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.ZWCAD;
using App = ZwSoft.ZwCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Phase45ProbeH))]

public class Phase45ProbeH
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-h.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    [CommandMethod("BP45H", CommandFlags.Session)]
    public void Run()
    {
        try
        {
            var doc = App.DocumentManager.MdiActiveDocument;
            if (!string.Equals(doc.Database.Filename, Path.GetFullPath(Path.Combine(Root, "../phase43-native/independent-test.dwg")), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("测试入口只允许本次独立测试图。");

            using (doc.LockDocument())
            {
                var db = doc.Database;
                Log("=== PROBE H START ===");

                db.ObjectModified += (s, e) => Log("[EVENT ObjectModified] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectAppended += (s, e) => Log("[EVENT ObjectAppended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectErased += (s, e) => Log("[EVENT ObjectErased] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Erased=" + e.Erased);
                db.ObjectOpenedForModify += (s, e) => Log("[EVENT ObjectOpenedForModify] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);

                var frames = new List<PlotFrame>();
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                    foreach (DBDictionaryEntry entry in dict)
                    {
                        var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                        var space = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                        frames.Add(new PlotFrame
                        {
                            Id = frames.Count + 1,
                            MinX = 0, MinY = 0, MaxX = 297, MaxY = 210,
                            Type = FrameType.PickWindow,
                            DetectedPaper = new PaperSize("A4", 297, 210, true),
                            CalculatedScale = 1,
                            SourceDocumentId = DocumentSessionIdentity.Get(doc),
                            SourceLayoutId = space.Handle.ToString(),
                            LayoutName = layout.LayoutName
                        });
                    }
                    tr.Commit();
                }

                LayoutManager.Current.CurrentLayout = "Model";
                var host = new ZwCadAdapter();
                var frame = frames.First(f => f.LayoutName == "Model");

                ObjectId textId = ObjectId.Null;
                ObjectId plineId = ObjectId.Null;
                string initialText = "";
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead);
                        if (ent is DBText txt && textId.IsNull)
                        {
                            textId = id;
                            initialText = txt.TextString;
                        }
                        else if (ent is Polyline pl && plineId.IsNull)
                        {
                            plineId = id;
                        }
                    }
                    tr.Commit();
                }

                string rev0 = host.GetSourceRevision(frame);
                Log("Rev0: " + rev0 + ", textId=" + textId + ", plineId=" + plineId);

                // --- 试验 1: TransformBy 移动 DBText ---
                Log("--- Test 1: TransformBy on DBText ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TransformBy(Matrix3d.Displacement(new Vector3d(10, 10, 0)));
                    tr.Commit();
                }
                string rev1 = host.GetSourceRevision(frame);
                Log("Rev1 after TransformBy: " + rev1 + " (Changed: " + (rev1 != rev0) + ")");

                // 移回原位
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TransformBy(Matrix3d.Displacement(new Vector3d(-10, -10, 0)));
                    tr.Commit();
                }

                // --- 试验 2: 修改 Polyline (多段线) 的顶点 ---
                Log("--- Test 2: Modify Polyline vertex ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    Point2d pt = pl.GetPoint2dAt(0);
                    pl.SetPointAt(0, new Point2d(pt.X + 5, pt.Y + 5));
                    tr.Commit();
                }
                string rev2 = host.GetSourceRevision(frame);
                Log("Rev2 after Polyline edit: " + rev2 + " (Changed: " + (rev2 != rev1) + ")");

                // 恢复 Polyline 顶点
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    Point2d pt = pl.GetPoint2dAt(0);
                    pl.SetPointAt(0, new Point2d(pt.X - 5, pt.Y - 5));
                    tr.Commit();
                }

                // --- 试验 3: Erase 已有实体 (将 DBText 删除再 Un-erase) ---
                Log("--- Test 3: Erase & UnErase existing DBText ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.Erase(true);
                    tr.Commit();
                }
                string rev3 = host.GetSourceRevision(frame);
                Log("Rev3 after Erase: " + rev3 + " (Changed: " + (rev3 != rev2) + ")");

                // 恢复删除
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite, true);
                    txt.Erase(false);
                    tr.Commit();
                }
                string rev3Restore = host.GetSourceRevision(frame);
                Log("Rev3 after UnErase: " + rev3Restore);

                // --- 试验 4: UpgradeOpen -> DowngradeOpen 显式调用 ---
                Log("--- Test 4: UpgradeOpen and DowngradeOpen ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForRead);
                    txt.UpgradeOpen();
                    txt.TextString = "DOWNGRADE_TEST";
                    txt.DowngradeOpen();
                    tr.Commit();
                }
                string rev4 = host.GetSourceRevision(frame);
                Log("Rev4 after DowngradeOpen: " + rev4 + " (Changed: " + (rev4 != rev3Restore) + ")");

                // 恢复文字
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = initialText;
                    tr.Commit();
                }

                // --- 试验 5: 修改图层 LayerTableRecord ---
                Log("--- Test 5: Modify LayerTableRecord ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var ltr = (LayerTableRecord)tr.GetObject(lt["0"], OpenMode.ForWrite);
                    ltr.Description = "Changed for test " + DateTime.Now.Ticks;
                    tr.Commit();
                }
                string rev5 = host.GetSourceRevision(frame);
                Log("Rev5 after Layer edit: " + rev5 + " (Changed: " + (rev5 != rev4) + ")");

                Log("=== PROBE H FINISHED ===");
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL: " + ex);
        }
    }
}

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

[assembly: CommandClass(typeof(Phase45ProbeI))]

public class Phase45ProbeI
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-i.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    [CommandMethod("BP45I", CommandFlags.Session)]
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
                Log("=== PROBE I START ===");

                db.ObjectModified += (s, e) => Log("[EVENT DB.ObjectModified] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectAppended += (s, e) => Log("[EVENT DB.ObjectAppended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectErased += (s, e) => Log("[EVENT DB.ObjectErased] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Erased=" + e.Erased);
                db.ObjectOpenedForModify += (s, e) => Log("[EVENT DB.ObjectOpenedForModify] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectUnappended += (s, e) => Log("[EVENT DB.ObjectUnappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectReappended += (s, e) => Log("[EVENT DB.ObjectReappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);

                ObjectId textId = ObjectId.Null;
                ObjectId plineId = ObjectId.Null;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead);
                        if (ent is DBText && textId.IsNull) textId = id;
                        else if (ent is Polyline && plineId.IsNull) plineId = id;
                    }
                    tr.Commit();
                }

                // 精确测试 Polyline 在哪一行触发 ObjectOpenedForModify
                Log("--- Step 1: Polyline line-by-line inspection ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Log("Before tr.GetObject(plineId, OpenMode.ForWrite)");
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    Log("After tr.GetObject(plineId, OpenMode.ForWrite)");
                    Point2d pt = pl.GetPoint2dAt(0);
                    Log("Before SetPointAt");
                    pl.SetPointAt(0, new Point2d(pt.X + 1, pt.Y + 1));
                    Log("After SetPointAt");
                    pl.SetPointAt(0, pt); // restore
                    Log("Before tr.Commit()");
                    tr.Commit();
                    Log("After tr.Commit()");
                }

                // 精确测试 DBText 在哪一行调用
                Log("--- Step 2: DBText line-by-line inspection ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Log("Before tr.GetObject(textId, OpenMode.ForWrite)");
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    Log("After tr.GetObject(textId, OpenMode.ForWrite)");
                    Log("Before txt.TextString assignment");
                    string old = txt.TextString;
                    txt.TextString = "TEST_I_1";
                    Log("After txt.TextString assignment");
                    Log("Before txt.Height assignment");
                    txt.Height = txt.Height + 1.0;
                    Log("After txt.Height assignment");
                    txt.Height = txt.Height - 1.0;
                    txt.TextString = old;
                    Log("Before tr.Commit()");
                    tr.Commit();
                    Log("After tr.Commit()");
                }

                // 测试新建 DBText
                Log("--- Step 3: New DBText line-by-line inspection ---");
                ObjectId newTextId = ObjectId.Null;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    var newTxt = new DBText { TextString = "NEW_CREATED_TEXT", Position = new Point3d(50, 50, 0), Height = 3.5 };
                    newTextId = space.AppendEntity(newTxt);
                    tr.AddNewlyCreatedDBObject(newTxt, true);
                    tr.Commit();
                }
                Log("New DBText created, Id=" + newTextId);

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Log("Before OpenWrite on newly created DBText");
                    var txt = (DBText)tr.GetObject(newTextId, OpenMode.ForWrite);
                    Log("After OpenWrite on newly created DBText");
                    txt.TextString = "MODIFIED_NEW_TEXT";
                    Log("After modify TextString on new DBText");
                    tr.Commit();
                    Log("After Commit modify on new DBText");
                }

                // 删除新建的测试文字
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(newTextId, OpenMode.ForWrite);
                    txt.Erase(true);
                    tr.Commit();
                    Log("Erased new DBText");
                }

                Log("=== PROBE I FINISHED ===");
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL: " + ex);
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.ZWCAD;
using App = ZwSoft.ZwCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Phase45ProbeK))]

public class Phase45ProbeK
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-k.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    [CommandMethod("BP45K", CommandFlags.Session)]
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
                Log("=== PROBE K START ===");

                // 挂载 DB 事件
                db.ObjectModified += (s, e) => Log("[EVENT DB.ObjectModified] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectAppended += (s, e) => Log("[EVENT DB.ObjectAppended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectErased += (s, e) => Log("[EVENT DB.ObjectErased] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Erased=" + e.Erased);
                db.ObjectOpenedForModify += (s, e) => Log("[EVENT DB.ObjectOpenedForModify] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectUnappended += (s, e) => Log("[EVENT DB.ObjectUnappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectReappended += (s, e) => Log("[EVENT DB.ObjectReappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);

                // 挂载 Doc 事件
                doc.CommandWillStart += (s, e) => Log("[EVENT Doc.CommandWillStart] " + e.GlobalCommandName);
                doc.CommandEnded += (s, e) => Log("[EVENT Doc.CommandEnded] " + e.GlobalCommandName);
                doc.LispWillStart += (s, e) => Log("[EVENT Doc.LispWillStart] " + e.FirstLine);
                doc.LispEnded += (s, e) => Log("[EVENT Doc.LispEnded]");

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
                        else if (ent is Polyline && plineId.IsNull)
                        {
                            plineId = id;
                        }
                    }
                    tr.Commit();
                }

                Log("Target textId=" + textId + ", initialText='" + initialText + "', plineId=" + plineId);
                Log("Initial DBMOD=" + App.GetSystemVariable("DBMOD"));

                // --- 试验 1: 单独测试 Polyline 在不同操作下的事件 ---
                Log("--- Test 1: Polyline GetObject(OpenMode.ForWrite) ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Log("Before tr.GetObject Polyline ForWrite");
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    Log("After tr.GetObject Polyline ForWrite");
                    tr.Commit();
                    Log("After Commit Polyline");
                }
                Log("DBMOD after Polyline OpenMode.ForWrite: " + App.GetSystemVariable("DBMOD"));

                // --- 试验 2: 单独测试 DBText 在 GetObject(OpenMode.ForWrite) 下 ---
                Log("--- Test 2: DBText GetObject(OpenMode.ForWrite) ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    Log("Before tr.GetObject DBText ForWrite");
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    Log("After tr.GetObject DBText ForWrite");
                    tr.Commit();
                    Log("After Commit DBText");
                }
                Log("DBMOD after DBText OpenMode.ForWrite: " + App.GetSystemVariable("DBMOD"));

                // --- 试验 3: Polyline SetPointAt ---
                Log("--- Test 3: Polyline SetPointAt with new value ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    Point2d pt = pl.GetPoint2dAt(0);
                    Log("Calling SetPointAt...");
                    pl.SetPointAt(0, new Point2d(pt.X + 2.0, pt.Y + 2.0));
                    Log("Called SetPointAt.");
                    tr.Commit();
                    Log("Committed SetPointAt.");
                }
                Log("DBMOD after Polyline SetPointAt: " + App.GetSystemVariable("DBMOD"));

                // 还原 Polyline
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    Point2d pt = pl.GetPoint2dAt(0);
                    pl.SetPointAt(0, new Point2d(pt.X - 2.0, pt.Y - 2.0));
                    tr.Commit();
                }

                // --- 试验 4: DBText 设置不同的属性 ---
                Log("--- Test 4: DBText change Position / Height ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    Log("Calling txt.Position assignment...");
                    txt.Position = new Point3d(txt.Position.X + 1, txt.Position.Y + 1, 0);
                    Log("Calling txt.Height assignment...");
                    txt.Height = txt.Height + 1.0;
                    Log("Calling txt.TextString assignment...");
                    txt.TextString = "CHANGED_IN_TEST_4";
                    tr.Commit();
                    Log("Committed DBText multi-edit.");
                }
                Log("DBMOD after DBText multi-edit: " + App.GetSystemVariable("DBMOD"));

                // 还原 DBText
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.Position = new Point3d(txt.Position.X - 1, txt.Position.Y - 1, 0);
                    txt.Height = txt.Height - 1.0;
                    txt.TextString = initialText;
                    tr.Commit();
                    Log("Restored DBText.");
                }

                // --- 试验 5: 测试 COM 方式修改文字 ---
                Log("--- Test 5: COM HandleToObject / TextString ---");
                try
                {
                    object acadApp = Marshal.GetActiveObject("ZWCAD.Application");
                    object acadDoc = acadApp.GetType().InvokeMember("ActiveDocument", BindingFlags.GetProperty, null, acadApp, null);
                    object vlaTxt = acadDoc.GetType().InvokeMember("HandleToObject", BindingFlags.InvokeMethod, null, acadDoc, new object[] { "242" });
                    string oldStr = (string)vlaTxt.GetType().InvokeMember("TextString", BindingFlags.GetProperty, null, vlaTxt, null);
                    Log("VLA TextString before: '" + oldStr + "'");
                    vlaTxt.GetType().InvokeMember("TextString", BindingFlags.SetProperty, null, vlaTxt, new object[] { "VLA_MODIFIED_TEXT" });
                    string newStr = (string)vlaTxt.GetType().InvokeMember("TextString", BindingFlags.GetProperty, null, vlaTxt, null);
                    Log("VLA TextString after: '" + newStr + "'");
                    vlaTxt.GetType().InvokeMember("TextString", BindingFlags.SetProperty, null, vlaTxt, new object[] { initialText });
                    Log("VLA TextString restored.");
                }
                catch (System.Exception ex)
                {
                    Log("VLA Test ERR: " + ex.Message);
                }
                Log("DBMOD after VLA edit: " + App.GetSystemVariable("DBMOD"));

                Log("=== PROBE K FINISHED ===");
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL: " + ex);
        }
    }
}

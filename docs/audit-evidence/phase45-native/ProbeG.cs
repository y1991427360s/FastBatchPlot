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

[assembly: CommandClass(typeof(Phase45ProbeG))]

public class Phase45ProbeG
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-g.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    [CommandMethod("BP45G", CommandFlags.Session)]
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
                Log("=== PROBE G START ===");

                // 事件监听
                db.ObjectModified += (s, e) => Log("[EVENT DB.ObjectModified] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectAppended += (s, e) => Log("[EVENT DB.ObjectAppended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectErased += (s, e) => Log("[EVENT DB.ObjectErased] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Erased=" + e.Erased);
                db.ObjectOpenedForModify += (s, e) => Log("[EVENT DB.ObjectOpenedForModify] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectUnappended += (s, e) => Log("[EVENT DB.ObjectUnappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                db.ObjectReappended += (s, e) => Log("[EVENT DB.ObjectReappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);

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
                string initialText = "";
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is DBText txt)
                        {
                            textId = id;
                            initialText = txt.TextString;
                            break;
                        }
                    }
                    tr.Commit();
                }
                Log("Target DBText Id=" + textId + ", InitialText='" + initialText + "'");
                string rev0 = host.GetSourceRevision(frame);
                Log("Rev0: " + rev0);

                // --- 尝试 1: db.TransactionManager + RecordGraphicsModified ---
                Log("--- Test 1: RecordGraphicsModified ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = "MOD_TEST_1";
                    txt.RecordGraphicsModified(true);
                    tr.Commit();
                }
                string rev1 = host.GetSourceRevision(frame);
                Log("Rev1 after Test 1: " + rev1 + " (Changed: " + (rev1 != rev0) + ")");

                // --- 尝试 2: OpenCloseTransaction ---
                Log("--- Test 2: OpenCloseTransaction ---");
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = "MOD_TEST_2";
                    tr.Commit();
                }
                string rev2 = host.GetSourceRevision(frame);
                Log("Rev2 after Test 2: " + rev2 + " (Changed: " + (rev2 != rev1) + ")");

                // --- 尝试 3: 传统 ObjectId.Open(ForWrite) / Close ---
                Log("--- Test 3: ObjectId.Open / Close ---");
                using (var txt = (DBText)textId.Open(OpenMode.ForWrite))
                {
                    txt.TextString = "MOD_TEST_3";
                } // using 结束调用 Dispose / Close
                string rev3 = host.GetSourceRevision(frame);
                Log("Rev3 after Test 3: " + rev3 + " (Changed: " + (rev3 != rev2) + ")");

                // --- 尝试 4: doc.TransactionManager.StartTransaction ---
                Log("--- Test 4: doc.TransactionManager ---");
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = "MOD_TEST_4";
                    tr.Commit();
                }
                string rev4 = host.GetSourceRevision(frame);
                Log("Rev4 after Test 4: " + rev4 + " (Changed: " + (rev4 != rev3) + ")");

                // --- 尝试 5: 修改实体的其它属性，如 Layer、ColorIndex、Position ---
                Log("--- Test 5: Change Color & Layer ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.ColorIndex = txt.ColorIndex == 1 ? 2 : 1;
                    tr.Commit();
                }
                string rev5 = host.GetSourceRevision(frame);
                Log("Rev5 after Test 5: " + rev5 + " (Changed: " + (rev5 != rev4) + ")");

                // --- 尝试 6: 在实体本身挂载 Modified 事件观察 ---
                Log("--- Test 6: Check DBObject.Modified direct hook ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.Modified += (s, e) => Log("[EVENT DBObject.Modified on txt]");
                    txt.TextString = "MOD_TEST_6";
                    tr.Commit();
                }
                string rev6 = host.GetSourceRevision(frame);
                Log("Rev6 after Test 6: " + rev6 + " (Changed: " + (rev6 != rev5) + ")");

                // --- 尝试 7: 检查 Database 实例是否每次访问不同 ---
                Log("--- Test 7: Checking Database wrappers ---");
                var db1 = doc.Database;
                var db2 = doc.Database;
                Log("db1 == db2 (ReferenceEquals): " + ReferenceEquals(db1, db2));
                Log("db1 HashCode: " + db1.GetHashCode() + ", db2 HashCode: " + db2.GetHashCode());
                Log("db1 Unmanaged: " + db1.UnmanagedObject + ", db2 Unmanaged: " + db2.UnmanagedObject);

                // --- 还原文字 ---
                Log("--- Restoring DBText to initial ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = initialText;
                    tr.Commit();
                }

                // 读回确认还原
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForRead);
                    Log("Final Readback: TextString='" + txt.TextString + "' (Restored: " + (txt.TextString == initialText) + ")");
                    tr.Commit();
                }

                Log("=== PROBE G END ===");
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL: " + ex);
        }
    }
}

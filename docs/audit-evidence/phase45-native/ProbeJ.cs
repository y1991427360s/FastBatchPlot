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

[assembly: CommandClass(typeof(Phase45ProbeJ))]

public class Phase45ProbeJ
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-j.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    static void DumpDbState(Database db, string label)
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[" + label + "] ");
            sb.Append("Fingerprint=" + db.FingerprintGuid + "; ");
            sb.Append("Version=" + db.VersionGuid + "; ");
            sb.Append("Tduupdate=" + db.Tduupdate + "; ");
            sb.Append("Tdindwg=" + db.Tdindwg + "; ");
            try
            {
                object dbmod = App.GetSystemVariable("DBMOD");
                sb.Append("DBMOD=" + dbmod + "; ");
            }
            catch (System.Exception ex) { sb.Append("DBMOD_ERR=" + ex.Message + "; "); }
            Log(sb.ToString());
        }
        catch (System.Exception ex)
        {
            Log("[" + label + "] DumpDbState ERR: " + ex.Message);
        }
    }

    [CommandMethod("BP45J", CommandFlags.Session)]
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
                Log("=== PROBE J START ===");

                DumpDbState(db, "Initial State");

                ObjectId textId = ObjectId.Null;
                string originalText = "";
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is DBText txt)
                        {
                            textId = id;
                            originalText = txt.TextString;
                            break;
                        }
                    }
                    tr.Commit();
                }
                Log("Target DBText Id=" + textId + ", OriginalText='" + originalText + "'");

                // 试验 A: 通过 .NET 事务修改 TextString
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = "PROBE_J_EDITED";
                    tr.Commit();
                }
                DumpDbState(db, "After .NET Transaction Edit");

                // 还原
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = originalText;
                    tr.Commit();
                }
                DumpDbState(db, "After .NET Transaction Restore");

                // 试验 B: 检查 TransactionManager 有没有事件
                Log("TransactionManager Type: " + db.TransactionManager.GetType().FullName);
                foreach (var evt in db.TransactionManager.GetType().GetEvents())
                {
                    Log("  TM Event: " + evt.Name);
                }

                // 试验 C: 检查 Document 有没有事件
                Log("Document Events:");
                foreach (var evt in doc.GetType().GetEvents())
                {
                    Log("  Doc Event: " + evt.Name);
                }

                // 试验 D: 检查 Editor / Database 上所有其它事件
                Log("Database Events:");
                foreach (var evt in db.GetType().GetEvents())
                {
                    Log("  DB Event: " + evt.Name);
                }

                Log("=== PROBE J END ===");
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL: " + ex);
        }
    }
}

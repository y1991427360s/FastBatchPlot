using System;
using System.IO;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Runtime;
using App = ZwSoft.ZwCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(ProbeStateM))]

public class ProbeStateM
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s) { File.AppendAllText(Path.Combine(Root, "fixed-m.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine); }

    static void PrintProp(string name, Func<object> getter)
    {
        try { Log("  " + name + ": " + getter()); }
        catch (System.Exception ex) { Log("  " + name + ": ERR (" + ex.GetType().Name + ": " + ex.Message + ")"); }
    }

    [CommandMethod("BP45M", CommandFlags.Session)]
    public void Run()
    {
        try
        {
            var doc = App.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using (doc.LockDocument())
            {
                Log("=== PROBE M: BEFORE EDIT ===");
                PrintProp("Tduupdate", () => db.Tduupdate);
                PrintProp("Tdindwg", () => db.Tdindwg);
                PrintProp("Tdcreate", () => db.Tdcreate);
                PrintProp("Tducreate", () => db.Tducreate);
                PrintProp("CurrentSpaceId", () => db.CurrentSpaceId);
                PrintProp("DBMOD", () => App.GetSystemVariable("DBMOD"));

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

                // 修改 DBText
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = "PROBE_M_EDITED";
                    tr.Commit();
                }

                Log("=== PROBE M: AFTER EDIT ===");
                PrintProp("Tduupdate", () => db.Tduupdate);
                PrintProp("Tdindwg", () => db.Tdindwg);
                PrintProp("DBMOD", () => App.GetSystemVariable("DBMOD"));

                // 还原 DBText
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = originalText;
                    tr.Commit();
                }
                Log("=== PROBE M: AFTER RESTORE ===");
            }
        }
        catch (System.Exception ex) { Log("FATAL: " + ex); }
    }
}

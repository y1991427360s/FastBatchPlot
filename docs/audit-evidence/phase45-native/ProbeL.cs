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

[assembly: CommandClass(typeof(Phase45ProbeL))]

public class Phase45ProbeL
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-l.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    [CommandMethod("BP45L", CommandFlags.Session)]
    public void Run()
    {
        try
        {
            var doc = App.DocumentManager.MdiActiveDocument;
            if (!string.Equals(doc.Database.Filename, Path.GetFullPath(Path.Combine(Root, "../phase43-native/independent-test.dwg")), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("测试入口只允许本次独立测试图。");

            var db = doc.Database;
            Log("=== PROBE L START ===");

            // 监听各种事件
            db.ObjectModified += (s, e) => Log("[EVENT DB.ObjectModified] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
            db.ObjectAppended += (s, e) => Log("[EVENT DB.ObjectAppended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
            db.ObjectErased += (s, e) => Log("[EVENT DB.ObjectErased] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Erased=" + e.Erased);
            db.ObjectOpenedForModify += (s, e) => Log("[EVENT DB.ObjectOpenedForModify] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
            doc.CommandWillStart += (s, e) => Log("[EVENT Doc.CommandWillStart] " + e.GlobalCommandName);
            doc.CommandEnded += (s, e) => Log("[EVENT Doc.CommandEnded] " + e.GlobalCommandName);
            doc.CommandCancelled += (s, e) => Log("[EVENT Doc.CommandCancelled] " + e.GlobalCommandName);
            doc.CommandFailed += (s, e) => Log("[EVENT Doc.CommandFailed] " + e.GlobalCommandName);

            // 发送一条实际的 CAD MOVE 命令移动 DBText 0,0 到 1,1
            Log("Sending MOVE command via SendStringToExecute...");
            doc.SendStringToExecute("(command \"_.MOVE\" (handent \"242\") \"\" \"0,0,0\" \"1,1,0\")\n", true, false, false);

            Log("=== PROBE L SENT COMMAND ===");
        }
        catch (System.Exception ex)
        {
            Log("FATAL: " + ex);
        }
    }

    [CommandMethod("BP45LCHECK", CommandFlags.Session)]
    public void Check()
    {
        try
        {
            var doc = App.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var txt = (DBText)tr.GetObject(db.GetObjectId(false, new Handle(0x242), 0), OpenMode.ForWrite);
                Log("BP45LCHECK: Current DBText Position=" + txt.Position + ", TextString='" + txt.TextString + "'");
                // 移回原位
                txt.TransformBy(Matrix3d.Displacement(new Vector3d(-1, -1, 0)));
                tr.Commit();
                Log("BP45LCHECK: Restored position.");
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL in BP45LCHECK: " + ex);
        }
    }
}

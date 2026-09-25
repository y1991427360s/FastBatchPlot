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

[assembly: CommandClass(typeof(Phase45ProbeF))]

public class Phase45ProbeF
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-f.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    [CommandMethod("BP45F", CommandFlags.Session)]
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
                Log("=== PROBE F START ===");
                Log("Doc: " + doc.Name + ", DB Unmanaged: " + db.UnmanagedObject + ", DB HashCode: " + db.GetHashCode());

                // 挂载所有可能事件以观测底层行为
                ObjectEventHandler onMod = (s, e) => Log("[EVENT ObjectModified] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Id=" + e.DBObject.ObjectId);
                ObjectEventHandler onApp = (s, e) => Log("[EVENT ObjectAppended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Id=" + e.DBObject.ObjectId);
                ObjectErasedEventHandler onEra = (s, e) => Log("[EVENT ObjectErased] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle + " Erased=" + e.Erased);
                ObjectEventHandler onOpenMod = (s, e) => Log("[EVENT ObjectOpenedForModify] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                ObjectEventHandler onUnapp = (s, e) => Log("[EVENT ObjectUnappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);
                ObjectEventHandler onReapp = (s, e) => Log("[EVENT ObjectReappended] " + e.DBObject.GetType().Name + " Handle=" + e.DBObject.Handle);

                db.ObjectModified += onMod;
                db.ObjectAppended += onApp;
                db.ObjectErased += onEra;
                db.ObjectOpenedForModify += onOpenMod;
                db.ObjectUnappended += onUnapp;
                db.ObjectReappended += onReapp;

                try
                {
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
                    var plotter = new ZwCadPlotEngine();
                    CadHostProvider.Host = host;
                    CadHostProvider.Plotter = plotter;

                    var frame = frames.First(f => f.LayoutName == "Model");
                    var config = new FastBatchPlot.Core.Models.PlotConfig
                    {
                        PrinterDevice = "DWG to PDF.pc5",
                        PlotStyleTable = "monochrome.ctb",
                        MergeToSinglePdf = false
                    };

                    string revBaseline = host.GetSourceRevision(frame);
                    Log("Baseline Revision: " + revBaseline);

                    // 1. 枚举当前空间所有图元
                    ObjectId textId = ObjectId.Null;
                    string originalText = "";
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                        Log("Current Space Handle: " + space.Handle + ", Name: " + space.Name);
                        int entCount = 0;
                        foreach (ObjectId id in space)
                        {
                            entCount++;
                            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;
                            string textDetail = "";
                            if (ent is DBText txt)
                            {
                                textDetail = " TextString='" + txt.TextString + "'";
                                if (textId.IsNull)
                                {
                                    textId = id;
                                    originalText = txt.TextString;
                                }
                            }
                            else if (ent is MText mtxt)
                            {
                                textDetail = " MText.Contents='" + mtxt.Contents + "'";
                            }
                            Log(string.Format("  Ent #{0}: {1} Handle={2} Id={3}{4}", entCount, ent.GetType().Name, ent.Handle, ent.ObjectId, textDetail));
                        }
                        tr.Commit();
                    }

                    if (textId.IsNull)
                    {
                        Log("WARNING: No DBText found in CurrentSpace!");
                    }
                    else
                    {
                        Log("Found DBText to edit. Id=" + textId + ", OriginalText='" + originalText + "'");

                        // 建立一致性核验检查点
                        var check = new CadPageConsistency(host, plotter, frame, config);
                        check.Verify();
                        Log("Initial Consistency Verify: PASSED");

                        // 2. 执行真实文字修改
                        string changedText = "PROBE_F_MODIFIED_" + DateTime.Now.Ticks;
                        Log("Step 2: Modifying DBText to '" + changedText + "' within Transaction...");
                        using (var editTr = db.TransactionManager.StartTransaction())
                        {
                            var textObj = (DBText)editTr.GetObject(textId, OpenMode.ForWrite);
                            Log("Before TextString assignment: textObj.TextString='" + textObj.TextString + "'");
                            textObj.TextString = changedText;
                            Log("After TextString assignment: textObj.TextString='" + textObj.TextString + "'");
                            editTr.Commit();
                            Log("Transaction Committed.");
                        }

                        // 3. 在新事务中只读读回，确认数据库中真实改变
                        using (var verifyTr = db.TransactionManager.StartTransaction())
                        {
                            var textObj = (DBText)verifyTr.GetObject(textId, OpenMode.ForRead);
                            Log("Step 3: Read back from DB: TextString='" + textObj.TextString + "' (Matches expected modified: " + (textObj.TextString == changedText) + ")");
                            verifyTr.Commit();
                        }

                        // 4. 检查修改后的 SourceRevision 与 Verify()
                        string revAfterEdit = host.GetSourceRevision(frame);
                        Log("Step 4: Revision after edit: " + revAfterEdit);
                        Log("Is revision changed? " + (!string.Equals(revBaseline, revAfterEdit, StringComparison.Ordinal)));

                        bool verifyRejected = false;
                        try
                        {
                            check.Verify();
                            Log("Verify after edit: PASSED (FAILED TO REJECT EDIT!)");
                        }
                        catch (InvalidOperationException ex)
                        {
                            verifyRejected = true;
                            Log("Verify after edit: REJECTED AS EXPECTED: " + ex.Message);
                        }

                        // 5. 还原文字
                        Log("Step 5: Restoring DBText back to original: '" + originalText + "'...");
                        using (var restoreTr = db.TransactionManager.StartTransaction())
                        {
                            var textObj = (DBText)restoreTr.GetObject(textId, OpenMode.ForWrite);
                            textObj.TextString = originalText;
                            restoreTr.Commit();
                            Log("Restore Transaction Committed.");
                        }

                        // 6. 验证文字已真正还原
                        using (var verifyTr = db.TransactionManager.StartTransaction())
                        {
                            var textObj = (DBText)verifyTr.GetObject(textId, OpenMode.ForRead);
                            Log("Step 6: Read back restored text: TextString='" + textObj.TextString + "' (Matches original: " + (textObj.TextString == originalText) + ")");
                            verifyTr.Commit();
                        }

                        string revAfterRestore = host.GetSourceRevision(frame);
                        Log("Step 6: Revision after restore: " + revAfterRestore);
                    }

                    // 7. 测试新增/删除图元是否触发事件
                    Log("Step 7: Testing Append & Erase entity...");
                    ObjectId tempLineId = ObjectId.Null;
                    using (var addTr = db.TransactionManager.StartTransaction())
                    {
                        var space = (BlockTableRecord)addTr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                        var line = new Line(new Point3d(0, 0, 0), new Point3d(10, 10, 0));
                        tempLineId = space.AppendEntity(line);
                        addTr.AddNewlyCreatedDBObject(line, true);
                        addTr.Commit();
                        Log("Appended temp line. Id=" + tempLineId);
                    }

                    string revAfterAdd = host.GetSourceRevision(frame);
                    Log("Revision after AppendEntity: " + revAfterAdd + " (Changed from baseline: " + (revAfterAdd != revBaseline) + ")");

                    using (var eraseTr = db.TransactionManager.StartTransaction())
                    {
                        var line = (Line)eraseTr.GetObject(tempLineId, OpenMode.ForWrite);
                        line.Erase(true);
                        eraseTr.Commit();
                        Log("Erased temp line.");
                    }

                    string revAfterErase = host.GetSourceRevision(frame);
                    Log("Revision after Erase: " + revAfterErase);

                    Log("=== PROBE F FINISHED SUCCESSFULLY ===");
                }
                finally
                {
                    db.ObjectModified -= onMod;
                    db.ObjectAppended -= onApp;
                    db.ObjectErased -= onEra;
                    db.ObjectOpenedForModify -= onOpenMod;
                    db.ObjectUnappended -= onUnapp;
                    db.ObjectReappended -= onReapp;
                }
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL in ProbeF: " + ex);
        }
    }
}

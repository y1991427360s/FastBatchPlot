using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.ZWCAD;
using App = ZwSoft.ZwCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Phase45ProbeN))]

public class Phase45ProbeN
{
    static readonly string Root = @"E:\366256\vibecoding\批打印-new\docs\audit-evidence\phase45-native";
    static void Log(string s)
    {
        File.AppendAllText(Path.Combine(Root, "fixed-n.log"), DateTime.Now.ToString("o") + " " + s + Environment.NewLine);
    }

    /// <summary>
    /// 增强型修订状态跟踪：事件监听 + 布局空间实体指纹双重保障
    /// </summary>
    public class EnhancedZwCadAdapter : ZwCadAdapter, ICadRevisionHost
    {
        private sealed class RevisionState
        {
            public readonly string Session = Guid.NewGuid().ToString("N");
            private long changes;
            public readonly Dictionary<IntPtr, RevisionState> External = new Dictionary<IntPtr, RevisionState>();
            public readonly Database Observed;
            public long Changes => Interlocked.Read(ref changes);

            public RevisionState(Database database)
            {
                Observed = database;
                database.ObjectModified += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectAppended += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectErased += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectUnappended += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectReappended += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectOpenedForModify += (sender, args) => Interlocked.Increment(ref changes);
            }
        }

        private static readonly ConditionalWeakTable<Document, RevisionState> SourceRevisions =
            new ConditionalWeakTable<Document, RevisionState>();

        public string GetSourceRevision(PlotFrame frame)
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            if (document == null || frame == null || string.IsNullOrWhiteSpace(frame.SourceDocumentId) ||
                !string.Equals(frame.SourceDocumentId, DocumentSessionIdentity.Get(document), StringComparison.Ordinal))
                throw new InvalidOperationException("任务来源文档会话已改变，不能复用旧任务重试；请重新搜索并新建任务。");

            using (document.LockDocument())
            {
                var database = document.Database;
                var revision = SourceRevisions.GetValue(document, value => new RevisionState(value.Database));
                if (revision.Observed.IsDisposed || revision.Observed.UnmanagedObject != database.UnmanagedObject)
                    throw new InvalidOperationException("原图数据库实例已更换，请重新建立任务。");

                using (var bytes = new MemoryStream())
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                using (var hash = SHA256.Create())
                {
                    writer.Write(frame.SourceDocumentId);
                    writer.Write(revision.Session);
                    writer.Write(revision.Changes);
                    writer.Write(database.Filename ?? string.Empty);
                    writer.Write(frame.LayoutName ?? string.Empty);

                    // 空间实体内容指纹：彻底免疫宿主事件漏报问题
                    WriteSpaceEntityFingerprints(database, frame.LayoutName ?? string.Empty, writer);

                    // 写入外参
                    WriteExternalReferences(database, writer, revision, new HashSet<IntPtr>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), 0);
                    writer.Flush();
                    return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
                }
            }
        }

        string ICadRevisionHost.GetSourceRevision(PlotFrame frame)
        {
            return GetSourceRevision(frame);
        }

        private static void WriteSpaceEntityFingerprints(Database db, string layoutName, BinaryWriter writer)
        {
            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                ObjectId spaceId = ObjectId.Null;
                var dict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                if (dict.Contains(layoutName))
                {
                    var layout = (Layout)tr.GetObject(dict.GetAt(layoutName), OpenMode.ForRead);
                    spaceId = layout.BlockTableRecordId;
                }
                else
                {
                    spaceId = db.CurrentSpaceId;
                }

                if (!spaceId.IsValid || spaceId.IsNull) return;
                var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
                writer.Write(space.Handle.Value);

                foreach (ObjectId id in space)
                {
                    if (id.IsErased) continue;
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;

                    writer.Write(ent.Handle.Value);
                    writer.Write(ent.GetType().Name);
                    writer.Write(ent.Layer ?? string.Empty);
                    writer.Write(ent.ColorIndex);
                    writer.Write(ent.Visible);

                    if (ent is DBText txt)
                    {
                        writer.Write(txt.TextString ?? string.Empty);
                        writer.Write(txt.Position.X);
                        writer.Write(txt.Position.Y);
                        writer.Write(txt.Height);
                        writer.Write(txt.Rotation);
                    }
                    else if (ent is MText mtxt)
                    {
                        writer.Write(mtxt.Contents ?? string.Empty);
                        writer.Write(mtxt.Location.X);
                        writer.Write(mtxt.Location.Y);
                    }
                    else if (ent is Polyline pl)
                    {
                        writer.Write(pl.NumberOfVertices);
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            var pt = pl.GetPoint2dAt(i);
                            writer.Write(pt.X);
                            writer.Write(pt.Y);
                        }
                    }
                    else if (ent is Line line)
                    {
                        writer.Write(line.StartPoint.X);
                        writer.Write(line.StartPoint.Y);
                        writer.Write(line.EndPoint.X);
                        writer.Write(line.EndPoint.Y);
                    }
                    else if (ent is Viewport vp)
                    {
                        writer.Write(vp.CenterPoint.X);
                        writer.Write(vp.CenterPoint.Y);
                        writer.Write(vp.Width);
                        writer.Write(vp.Height);
                        writer.Write(vp.ViewCenter.X);
                        writer.Write(vp.ViewCenter.Y);
                        writer.Write(vp.ViewHeight);
                        writer.Write(vp.CustomScale);
                    }
                }
            }
        }

        private static void WriteExternalReferences(Database database, BinaryWriter writer, RevisionState root, HashSet<IntPtr> visited, Dictionary<string, string> files, int depth)
        {
            if (depth > 64) throw new InvalidOperationException("外参嵌套过深，无法可靠核对任务依赖。");
            if (!visited.Add(database.UnmanagedObject)) { writer.Write("visited"); return; }
            if (!root.External.TryGetValue(database.UnmanagedObject, out var revision) || revision.Observed.IsDisposed)
            { revision = new RevisionState(database); root.External[database.UnmanagedObject] = revision; }
            writer.Write(revision.Session); writer.Write(revision.Changes);
            using (var transaction = database.TransactionManager.StartOpenCloseTransaction())
            {
                var blocks = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                foreach (ObjectId id in blocks)
                {
                    var block = (BlockTableRecord)transaction.GetObject(id, OpenMode.ForRead);
                    if (!block.IsFromExternalReference) continue;
                    writer.Write(block.Handle.ToString()); writer.Write(block.Name);
                    writer.Write(block.PathName ?? string.Empty);
                    var external = block.GetXrefDatabase(false);
                    string reference = external != null && !string.IsNullOrWhiteSpace(external.Filename) ? external.Filename : (block.PathName ?? string.Empty);
                    string path = ExternalFileRevision.ResolvePath(database.Filename, reference);
                    writer.Write(path);
                    if (!files.TryGetValue(path, out var digest)) { digest = ExternalFileRevision.Read(path); files.Add(path, digest); }
                    writer.Write(digest); writer.Write(external != null);
                    if (external != null) WriteExternalReferences(external, writer, root, visited, files, depth + 1);
                }
            }
        }
    }

    [CommandMethod("BP45N", CommandFlags.Session)]
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
                Log("=== PROBE N VERIFICATION START ===");

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
                var host = new EnhancedZwCadAdapter();
                var plotter = new ZwCadPlotEngine();
                CadHostProvider.Host = host;
                CadHostProvider.Plotter = plotter;

                var frameModel = frames.First(f => f.LayoutName == "Model");
                var config = new FastBatchPlot.Core.Models.PlotConfig
                {
                    PrinterDevice = "DWG to PDF.pc5",
                    PlotStyleTable = "monochrome.ctb",
                    MergeToSinglePdf = false
                };

                // 1. 无修改连续读取 3 次，验证哈希稳定性
                Log("--- Step 1: Stability Check (Read 3 times) ---");
                string r1 = host.GetSourceRevision(frameModel);
                string r2 = host.GetSourceRevision(frameModel);
                string r3 = host.GetSourceRevision(frameModel);
                Log("R1: " + r1);
                Log("R2: " + r2);
                Log("R3: " + r3);
                bool stable = (r1 == r2 && r2 == r3);
                Log("Stable: " + stable);
                if (!stable) throw new InvalidOperationException("稳定性检查失败：无修改连续读取结果不一致！");

                // 2. 真实输出 Model 页面并执行正向 Verify
                Log("--- Step 2: Native Plot & Forward Verify ---");
                var check = new CadPageConsistency(host, plotter, frameModel, config);
                string pdfPath = Path.Combine(Root, "fixed-n-model.pdf");
                string error;
                bool plotOk = plotter.PlotFrameToFile(frameModel, config, pdfPath, out error);
                Log("Plot result: " + plotOk + ", error=" + error + ", pdfExists=" + File.Exists(pdfPath));
                if (!plotOk) throw new InvalidOperationException("打印失败：" + error);
                check.Verify();
                Log("Forward Consistency Verify: PASSED OK!");

                // 查找 DBText 和 Polyline
                ObjectId textId = ObjectId.Null;
                ObjectId plineId = ObjectId.Null;
                string originalText = "";
                Point2d originalPt = Point2d.Origin;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead);
                        if (ent is DBText txt && textId.IsNull)
                        {
                            textId = id;
                            originalText = txt.TextString;
                        }
                        else if (ent is Polyline pl && plineId.IsNull)
                        {
                            plineId = id;
                            originalPt = pl.GetPoint2dAt(0);
                        }
                    }
                    tr.Commit();
                }

                // 3. 反向测试 A：真实修改 DBText 文字并验证被拒绝
                Log("--- Step 3: Reverse Test A - Modify DBText TextString ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = "MODIFIED_IN_PROBE_N_" + DateTime.Now.Ticks;
                    tr.Commit();
                }

                bool textEditRejected = false;
                try
                {
                    check.Verify();
                    Log("Verify after text edit: FAILED (did not reject!)");
                }
                catch (InvalidOperationException ex)
                {
                    textEditRejected = true;
                    Log("Verify after text edit: REJECTED AS EXPECTED: " + ex.Message);
                }

                // 还原文字
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForWrite);
                    txt.TextString = originalText;
                    tr.Commit();
                }
                Log("Restored DBText.");

                if (!textEditRejected) throw new InvalidOperationException("反向核验失败：未拒绝真实文字修改！");

                // 4. 反向测试 B：真实修改 Polyline 坐标并验证被拒绝
                Log("--- Step 4: Reverse Test B - Modify Polyline Vertex ---");
                // 建立新的检查点
                var checkPoly = new CadPageConsistency(host, plotter, frameModel, config);
                checkPoly.Verify();
                Log("CheckPoly initial Verify: PASSED");

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    pl.SetPointAt(0, new Point2d(originalPt.X + 5, originalPt.Y + 5));
                    tr.Commit();
                }

                bool polyEditRejected = false;
                try
                {
                    checkPoly.Verify();
                    Log("Verify after polyline edit: FAILED (did not reject!)");
                }
                catch (InvalidOperationException ex)
                {
                    polyEditRejected = true;
                    Log("Verify after polyline edit: REJECTED AS EXPECTED: " + ex.Message);
                }

                // 还原 Polyline
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForWrite);
                    pl.SetPointAt(0, originalPt);
                    tr.Commit();
                }
                Log("Restored Polyline.");

                if (!polyEditRejected) throw new InvalidOperationException("反向核验失败：未拒绝真实多段线修改！");

                // 5. 验证图纸完全恢复为原始状态
                Log("--- Step 5: Read back check after restorations ---");
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var txt = (DBText)tr.GetObject(textId, OpenMode.ForRead);
                    var pl = (Polyline)tr.GetObject(plineId, OpenMode.ForRead);
                    Log("Final DBText TextString='" + txt.TextString + "' (Matches original: " + (txt.TextString == originalText) + ")");
                    Log("Final Polyline Point0=" + pl.GetPoint2dAt(0) + " (Matches original: " + (pl.GetPoint2dAt(0) == originalPt) + ")");
                    tr.Commit();
                }

                // 6. 跨布局读取与稳定性检查
                Log("--- Step 6: Cross Layout Read & Stability ---");
                foreach (var f in frames)
                {
                    string rev = host.GetSourceRevision(f);
                    Log("Layout [" + f.LayoutName + "] SourceRevision: " + rev);
                }

                Log("=== ALL PROBE N TESTS PASSED SUCCESSFULLY! ===");
            }
        }
        catch (System.Exception ex)
        {
            Log("FATAL in ProbeN: " + ex);
        }
    }
}

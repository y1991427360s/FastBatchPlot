using System;
using System.IO;
using System.Collections.Generic;
using FastBatchPlot.Core.Tasks;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace FastBatchPlot.AutoCAD
{
    public partial class AcadAdapter : ICadRevisionHost
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
                Observed = database; // 外参包装器可能重建，保持本次会话观察对象。
                // 事件包括回滚/临时操作的噪声；没有证据证明是本工具内部操作时一律不豁免。
                database.ObjectModified += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectAppended += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectErased += (sender, args) => Interlocked.Increment(ref changes);
                // SDK 中 Undo/Redo 的移出、重新加入是独立 ObjectEventHandler 事件。
                database.ObjectUnappended += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectReappended += (sender, args) => Interlocked.Increment(ref changes);
                database.ObjectOpenedForModify += (sender, args) => Interlocked.Increment(ref changes);
            }
        }
        private static readonly ConditionalWeakTable<Document, RevisionState> SourceRevisions =
            new ConditionalWeakTable<Document, RevisionState>();

        public string GetSourceRevision(PlotFrame frame)
        {
            // 同时核对已加载外参修改事件和外参磁盘内容；尚不覆盖字体、样式及其他外部资源。
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
                    WriteSpaceEntityFingerprints(database, frame.LayoutName ?? string.Empty, writer);
                    WriteExternalReferences(database, writer, revision, new HashSet<IntPtr>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), 0);
                    writer.Flush();
                    return Convert.ToBase64String(hash.ComputeHash(bytes.ToArray()));
                }
            }
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
                    // 已加载库的完整文件名优先，保留 CAD 实际搜索路径解析结果。
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
}

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.UI.Views;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.Colors;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.EditorInput;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.Runtime;

[assembly: ExtensionApplication(typeof(FastBatchPlot.ZWCAD.ZwExtensionApp))]
[assembly: CommandClass(typeof(FastBatchPlot.ZWCAD.ZwCommands))]

namespace FastBatchPlot.ZWCAD
{
    public class ZwExtensionApp : IExtensionApplication
    {
        public void Initialize()
        {
            CadHostProvider.Host = new ZwCadAdapter();
            CadHostProvider.Plotter = new ZwCadPlotEngine();

            try
            {
                var cme = new ZwSoft.ZwCAD.Windows.ContextMenuExtension();
                cme.Title = "FastBatchPlot 批打印";

                var miCommon = new ZwSoft.ZwCAD.Windows.MenuItem("批打到文件(通用型)...");
                miCommon.Click += (s, e) => {
                    var doc = ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute("BP_TY\n", true, false, false);
                };
                cme.MenuItems.Add(miCommon);

                var miFrame = new ZwSoft.ZwCAD.Windows.MenuItem("批打到文件(图框型)...");
                miFrame.Click += (s, e) => {
                    var doc = ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute("BP_TK\n", true, false, false);
                };
                cme.MenuItems.Add(miFrame);

                var miMain = new ZwSoft.ZwCAD.Windows.MenuItem("批打印主界面(BP)...");
                miMain.Click += (s, e) => {
                    var doc = ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute("BP\n", true, false, false);
                };
                cme.MenuItems.Add(miMain);

                ZwSoft.ZwCAD.ApplicationServices.Application.AddDefaultContextMenuExtension(cme);
            }
            catch { }

            try
            {
            var ed = ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n=======================================================");
            ed?.WriteMessage("\n  FastBatchPlot 智能批打印系统 (中望CAD版) 加载成功！");
            ed?.WriteMessage("\n  输入 BP 或 BATCHPLOT 启动批打印主界面");
            ed?.WriteMessage("\n  输入 BP_TY 或 BPTY 启动批打到文件(通用型)");
            ed?.WriteMessage("\n  输入 BP_TK 或 BPTK 启动批打到文件(图框型)");
            ed?.WriteMessage("\n  输入 BP_TRUE2INDEX 执行真彩色精准转 ACI 索引色");
            ed?.WriteMessage("\n  输入 BP_INSERT_TK 快速插入标准图框");
            ed?.WriteMessage("\n=======================================================\n");
            }
            catch { }
        }

        public void Terminate() { }
    }

    public class ZwCommands
    {
        private static BatchPlotForm? _activeForm;

        [CommandMethod("BP")]
        [CommandMethod("BATCHPLOT")]
        [CommandMethod("M_BATCHPLOT")]
        [CommandMethod("MSTEEL")]
        public void LaunchBatchPlot()
        {
            OpenBatchPlotForm(null);
        }

        [CommandMethod("BP_TY")]
        [CommandMethod("BPTY")]
        [CommandMethod("BP_COMMON")]
        public void LaunchBatchPlotUniversal()
        {
            LaunchWithSelection(FastBatchPlot.Core.Models.PlotDetectMode.Universal, "通用型");
        }

        [CommandMethod("BP_TK")]
        [CommandMethod("BPTK")]
        [CommandMethod("BP_FRAME")]
        public void LaunchBatchPlotTemplate()
        {
            LaunchWithSelection(FastBatchPlot.Core.Models.PlotDetectMode.Template, "图框型");
        }

        private static BatchPlotForm EnsureForm()
        {
            if (!CadHostProvider.IsInitialized)
            {
                CadHostProvider.Host = new ZwCadAdapter();
                CadHostProvider.Plotter = new ZwCadPlotEngine();
            }
            if (_activeForm == null || _activeForm.IsDisposed) _activeForm = new BatchPlotForm();
            return _activeForm;
        }

        private static void ShowForm(BatchPlotForm form)
        {
            // 窗口已打开时再次执行命令只激活它；再次 Show 已显示的窗体会抛出异常。
            if (form.Visible)
            {
                if (form.WindowState == FormWindowState.Minimized) form.WindowState = FormWindowState.Normal;
                form.Activate();
                return;
            }
            if (form.Owner != null) form.Show();
            else ZwSoft.ZwCAD.ApplicationServices.Application.ShowModelessDialog(form);
        }

        private static void OpenBatchPlotForm(FastBatchPlot.Core.Models.PlotDetectMode? mode)
        {
            var form = EnsureForm();
            if (mode.HasValue) form.SetDetectMode(mode.Value);
            ShowForm(form);
        }

        /// <summary>原版流程：命令 → 在图中选择打印范围（回车则选择遍历模型/布局）→ 弹出已载入图框的对话框。</summary>
        private static void LaunchWithSelection(FastBatchPlot.Core.Models.PlotDetectMode mode, string label)
        {
            var doc = ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var form = EnsureForm();
            if (form.IsBusy) { ShowForm(form); return; }
            form.SetDetectMode(mode);
            doc.Editor.WriteMessage($"\n[FastBatchPlot] 批打印（{label}）");
            bool wasVisible = form.Visible;
            if (wasVisible) form.Hide();
            try
            {
                var adapter = CadHostProvider.Host as ZwCadAdapter ?? new ZwCadAdapter();
                switch (adapter.PromptFramesOrScope(out var polylines, out var blocks, out var scope))
                {
                    case ZwCadAdapter.FramePromptOutcome.Cancelled:
                        if (wasVisible) ShowForm(form);
                        return;
                    case ZwCadAdapter.FramePromptOutcome.Selected:
                        form.LoadSelectedCandidates(polylines, blocks, mode);
                        break;
                    case ZwCadAdapter.FramePromptOutcome.Scope:
                        form.LoadScopeCandidates(scope);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                doc.Editor.WriteMessage("\n[FastBatchPlot] 选择图框失败：" + ex.Message);
            }
            ShowForm(form);
        }

        [CommandMethod("BP_TRUE2INDEX")]
        public void TrueColorToIndexColor()
        {
            var doc = ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;
            int entities = 0, layers = 0, lockedSkipped = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 图层真彩色：ByLayer 的实体按图层颜色出图，必须一并转换。
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in layerTable)
                {
                    var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (!layer.Color.IsByColor || layer.IsDependent) continue;
                    layer.UpgradeOpen();
                    layer.Color = Color.FromColorIndex(ColorMethod.ByAci, AciColorHelper.RgbToAci(layer.Color.Red, layer.Color.Green, layer.Color.Blue));
                    layers++;
                }

                // 当前空间及所有普通块定义（图框、标题栏多为块）中的实体；锁定图层上的实体跳过并计数。
                var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var targets = new List<ObjectId> { db.CurrentSpaceId };
                foreach (ObjectId id in blockTable)
                {
                    var block = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    if (!block.IsLayout && !block.IsFromExternalReference && !block.IsDependent) targets.Add(id);
                }
                foreach (var spaceId in targets)
                {
                    var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
                    foreach (ObjectId id in space)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null || !ent.Color.IsByColor) continue;
                        var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                        if (layer.IsLocked) { lockedSkipped++; continue; }
                        ent.UpgradeOpen();
                        ent.Color = Color.FromColorIndex(ColorMethod.ByAci, AciColorHelper.RgbToAci(ent.Color.Red, ent.Color.Green, ent.Color.Blue));
                        entities++;
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[FastBatchPlot] 转换完成：{entities} 个实体、{layers} 个图层的真彩色已映射为 ACI 索引色（便于黑白/线宽出图）。");
            if (lockedSkipped > 0) ed.WriteMessage($"\n[FastBatchPlot] 锁定图层上的 {lockedSkipped} 个实体未修改，如需转换请先解锁图层。");
            ed.WriteMessage("\n");
        }

        [CommandMethod("BP_INSERT_TK")]
        public void InsertStandardFrame()
        {
            var doc = ZwSoft.ZwCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var ppr = ed.GetPoint("\n请指定图框左下角插入点: ");
            if (ppr.Status != PromptStatus.OK) return;

            // 关键字不能含“+”，加长图幅用 A3L/A2L 表示。
            var pko = new PromptKeywordOptions("\n请选择标准图幅 [A0/A1/A2/A3/A4/A3L(A3+1)/A2L(A2+1)] <A1>: ");
            foreach (var keyword in new[] { "A0", "A1", "A2", "A3", "A4", "A3L", "A2L" }) pko.Keywords.Add(keyword);
            pko.Keywords.Default = "A1";
            pko.AllowNone = true;

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK && pkr.Status != PromptStatus.None) return;
            string paper = string.IsNullOrEmpty(pkr.StringResult) ? "A1" : pkr.StringResult;

            var pno = new PromptIntegerOptions("\n请输入出图比例 1:<100>: ");
            pno.DefaultValue = 100;
            pno.UseDefaultValue = true;
            pno.AllowNegative = false;
            pno.AllowZero = false;
            var pnr = ed.GetInteger(pno);
            if (pnr.Status != PromptStatus.OK) return;
            int scale = pnr.Value;

            double w = 841, h = 594;
            switch (paper.ToUpperInvariant())
            {
                case "A0": w = 1189; h = 841; break;
                case "A1": w = 841; h = 594; break;
                case "A2": w = 594; h = 420; break;
                case "A3": w = 420; h = 297; break;
                case "A4": w = 297; h = 210; break;
                case "A3L": w = 841; h = 297; paper = "A3+1"; break;
                case "A2L": w = 1189; h = 420; paper = "A2+1"; break;
            }

            double fw = w * scale;
            double fh = h * scale;
            // 用户输入点为 UCS 坐标，按当前 UCS 平面生成图框。
            var ucs = ed.CurrentUserCoordinateSystem;
            var pt = ppr.Value;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var currentSpace = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);

                // 创建外框多段线
                var pline = new Polyline(4);
                pline.AddVertexAt(0, new Point2d(pt.X, pt.Y), 0, 0, 0);
                pline.AddVertexAt(1, new Point2d(pt.X + fw, pt.Y), 0, 0, 0);
                pline.AddVertexAt(2, new Point2d(pt.X + fw, pt.Y + fh), 0, 0, 0);
                pline.AddVertexAt(3, new Point2d(pt.X, pt.Y + fh), 0, 0, 0);
                pline.Closed = true;
                pline.Elevation = pt.Z;
                pline.TransformBy(ucs);

                currentSpace.AppendEntity(pline);
                tr.AddNewlyCreatedDBObject(pline, true);

                tr.Commit();
            }

            ed.WriteMessage($"\n[FastBatchPlot] 成功插入标准 {paper} (1:{scale}) 图框。\n");
        }
    }
}

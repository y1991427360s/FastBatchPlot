using System;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Common;
using FastBatchPlot.UI.Views;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(FastBatchPlot.AutoCAD.AcadExtensionApp))]
[assembly: CommandClass(typeof(FastBatchPlot.AutoCAD.AcadCommands))]

namespace FastBatchPlot.AutoCAD
{
    public class AcadExtensionApp : IExtensionApplication
    {
        public void Initialize()
        {
            CadHostProvider.Host = new AcadAdapter();
            CadHostProvider.Plotter = new AcadPlotEngine();

            try
            {
                var cme = new Autodesk.AutoCAD.Windows.ContextMenuExtension();
                cme.Title = "FastBatchPlot 批打印";

                var miCommon = new Autodesk.AutoCAD.Windows.MenuItem("批打到文件(通用型)...");
                miCommon.Click += (s, e) => {
                    var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute("BP_TY\n", true, false, false);
                };
                cme.MenuItems.Add(miCommon);

                var miFrame = new Autodesk.AutoCAD.Windows.MenuItem("批打到文件(图框型)...");
                miFrame.Click += (s, e) => {
                    var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute("BP_TK\n", true, false, false);
                };
                cme.MenuItems.Add(miFrame);

                var miMain = new Autodesk.AutoCAD.Windows.MenuItem("批打印主界面(BP)...");
                miMain.Click += (s, e) => {
                    var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    doc?.SendStringToExecute("BP\n", true, false, false);
                };
                cme.MenuItems.Add(miMain);

                Autodesk.AutoCAD.ApplicationServices.Application.AddDefaultContextMenuExtension(cme);
            }
            catch { }

            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n=======================================================");
            ed?.WriteMessage("\n  FastBatchPlot 智能批打印系统 (AutoCAD版) 加载成功！");
            ed?.WriteMessage("\n  输入 BP 或 BATCHPLOT 启动批打印主界面");
            ed?.WriteMessage("\n  输入 BP_TY 或 BPTY 启动批打到文件(通用型)");
            ed?.WriteMessage("\n  输入 BP_TK 或 BPTK 启动批打到文件(图框型)");
            ed?.WriteMessage("\n  输入 BP_TRUE2INDEX 执行真彩色精准转 ACI 索引色");
            ed?.WriteMessage("\n  输入 BP_INSERT_TK 快速插入标准图框");
            ed?.WriteMessage("\n=======================================================\n");
        }

        public void Terminate() { }
    }

    public class AcadCommands
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
            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[FastBatchPlot] 正在启动批打印 (通用型模式)...");
            OpenBatchPlotForm(FastBatchPlot.Core.Models.PlotDetectMode.Universal);
        }

        [CommandMethod("BP_TK")]
        [CommandMethod("BPTK")]
        [CommandMethod("BP_FRAME")]
        public void LaunchBatchPlotTemplate()
        {
            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[FastBatchPlot] 正在启动批打印 (图框型模式)...");
            OpenBatchPlotForm(FastBatchPlot.Core.Models.PlotDetectMode.Template);
        }

        private static void OpenBatchPlotForm(FastBatchPlot.Core.Models.PlotDetectMode? mode)
        {
            if (!CadHostProvider.IsInitialized)
            {
                CadHostProvider.Host = new AcadAdapter();
                CadHostProvider.Plotter = new AcadPlotEngine();
            }

            if (_activeForm == null || _activeForm.IsDisposed)
            {
                _activeForm = new BatchPlotForm();
            }

            if (mode.HasValue)
            {
                _activeForm.SetDetectMode(mode.Value);
            }

            Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessDialog(_activeForm);
        }

        [CommandMethod("BP_TRUE2INDEX")]
        public void TrueColorToIndexColor()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;
            int count = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                foreach (ObjectId id in currentSpace)
                {
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent != null && ent.Color.IsByColor)
                    {
                        byte r = ent.Color.Red;
                        byte g = ent.Color.Green;
                        byte b = ent.Color.Blue;

                        short aci = AciColorHelper.RgbToAci(r, g, b);
                        ent.Color = Color.FromColorIndex(ColorMethod.ByAci, aci);
                        count++;
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n[FastBatchPlot] 转换完成，已将 {count} 个实体的真彩色精准映射为 AutoCAD ACI 索引色 (便于黑白/线宽出图)。\n");
        }

        [CommandMethod("BP_INSERT_TK")]
        public void InsertStandardFrame()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var ppr = ed.GetPoint("\n请指定图框左下角插入点: ");
            if (ppr.Status != PromptStatus.OK) return;

            var pko = new PromptKeywordOptions("\n请选择标准图幅 [A0/A1/A2/A3/A4/A3+1/A2+1] <A1>: ");
            pko.Keywords.Add("A0");
            pko.Keywords.Add("A1");
            pko.Keywords.Add("A2");
            pko.Keywords.Add("A3");
            pko.Keywords.Add("A4");
            pko.Keywords.Add("A3+1");
            pko.Keywords.Add("A2+1");
            pko.Keywords.Default = "A1";

            var pkr = ed.GetKeywords(pko);
            string paper = string.IsNullOrEmpty(pkr.StringResult) ? "A1" : pkr.StringResult;

            var pno = new PromptIntegerOptions("\n请输入出图比例 1:<100>: ");
            pno.DefaultValue = 100;
            var pnr = ed.GetInteger(pno);
            int scale = pnr.Value > 0 ? pnr.Value : 100;

            double w = 841, h = 594;
            switch (paper.ToUpperInvariant())
            {
                case "A0": w = 1189; h = 841; break;
                case "A1": w = 841; h = 594; break;
                case "A2": w = 594; h = 420; break;
                case "A3": w = 420; h = 297; break;
                case "A4": w = 297; h = 210; break;
                case "A3+1": w = 840; h = 297; break;
                case "A2+1": w = 1188; h = 420; break;
            }

            double fw = w * scale;
            double fh = h * scale;
            var pt = ppr.Value;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var currentSpace = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);

                var pline = new Polyline(4);
                pline.AddVertexAt(0, new Point2d(pt.X, pt.Y), 0, 0, 0);
                pline.AddVertexAt(1, new Point2d(pt.X + fw, pt.Y), 0, 0, 0);
                pline.AddVertexAt(2, new Point2d(pt.X + fw, pt.Y + fh), 0, 0, 0);
                pline.AddVertexAt(3, new Point2d(pt.X, pt.Y + fh), 0, 0, 0);
                pline.Closed = true;

                currentSpace.AppendEntity(pline);
                tr.AddNewlyCreatedDBObject(pline, true);

                tr.Commit();
            }

            ed.WriteMessage($"\n[FastBatchPlot] 成功插入标准 {paper} (1:{scale}) 图框。\n");
        }
    }
}

using System.Drawing;
using System.Drawing.Imaging;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckPdfScopeUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "PdfScopeUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var oldHost = CadHostProvider.Host; var oldPlotter = CadHostProvider.Plotter;
        CadHostProvider.Host = null; CadHostProvider.Plotter = null;
        try
        {
            string broken = Path.Combine(directory, "obsolete-stamps.json"); File.WriteAllText(broken, "{invalid-obsolete-library");
            string missing = Path.Combine(directory, "removed-registration-library.json");
            byte[] png;
            using (var bitmap = new Bitmap(8, 8, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var memory = new MemoryStream())
            {
                graphics.Clear(Color.Transparent); graphics.FillRectangle(Brushes.Red, 2, 2, 4, 4);
                bitmap.Save(memory, ImageFormat.Png); png = memory.ToArray();
            }
            var plain = StampAsset.Import("旧配置清理测试图片", png);
            var now = DateTimeOffset.UtcNow;
            var encrypted = StampAuthorization.Protect(plain, "offline-pdf-scope-code", now.AddDays(2), now);
            var permit = StampAuthorization.Unlock(encrypted, "offline-pdf-scope-code");
            using var form = new BatchPlotForm(Path.Combine(directory, "settings.json"));
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000);
            form.Show(); form.Size = form.MinimumSize; form.PerformLayout();
            string[] forbidden = { "印章", "图章", "签章", "签名层", "注册章" };
            var controls = PdfScopeControls(form).ToArray();
            Check(!controls.Where(c => c is ButtonBase && c.Visible).Any(c => forbidden.Any(word => c.Text.Contains(word))),
                "PDF主窗口无签章开关、图层映射或印章管理可见入口");
            var context = Field<ContextMenuStrip>(form, "ctxMenu");
            Check(!PdfScopeMenuItems(context.Items).Any(i => forbidden.Any(word => (i.Text ?? "").Contains(word))),
                "PDF主窗口右键菜单不包含印章库或附加印章入口");
            using (var template = new TitleTemplateForm(new TitleTemplateLibrary(), null, () => null, frame => null))
            {
                template.ShowInTaskbar = false; template.StartPosition = FormStartPosition.Manual; template.Location = new Point(-32000, -32000); template.Show();
                Check(!PdfScopeControls(template).Where(c => c is ButtonBase && c.Visible).Any(c => forbidden.Any(word => c.Text.Contains(word))),
                    "图框模板窗口不再提供主章或注册章区域编辑入口");
            }
            var config = Field<PlotConfig>(form, "_config");
            config.PrintSignatures = false; config.PrintStamps = false;
            config.PrintPrimaryStamp = true; config.PrintRegistrationStamp = true;
            config.StampLibraryPath = broken; config.StampAssetId = encrypted.Id;
            config.RegistrationStampLibraryPath = missing; config.RegistrationStampAssetId = Guid.NewGuid().ToString("N");
            config.Stamp = encrypted.Copy(); config.RegistrationStamp = encrypted.Copy();
            config.StampPermit = permit; config.RegistrationStampPermit = permit;
            config.SignatureLayerName = "*旧无效映射"; config.StampLayerName = "*旧无效映射";
            var devices = Field<ComboBox>(form, "cboPlotters"); devices.Items.Add("offline-pdf.pc3"); devices.SelectedIndex = devices.Items.Count - 1;
            Field<ComboBox>(form, "cboOutputMode").SelectedIndex = 0;
            var styles = Field<ComboBox>(form, "cboPlotStyles");
            styles.SelectedIndex = -1;
            Reject(() => Call(form, "CapturePlotSettings"), "缺失打印样式时拒绝提交，避免默用布局样式");
            styles.SelectedIndex = 0;
            Call(form, "CapturePlotSettings");
            Check(config.PrinterDevice == "offline-pdf.pc3" && config.ExportFormat == PlotExportFormat.PDF,
                "PDF设置捕获不受旧损坏库、失效选择或旧图层映射阻断");
            Check(config.Stamp == null && config.RegistrationStamp == null && config.StampPermit == null && config.RegistrationStampPermit == null,
                "PDF设置捕获清除两槽历史图片及授权凭据");
            Check(string.IsNullOrEmpty(config.StampLibraryPath) && string.IsNullOrEmpty(config.StampAssetId) &&
                string.IsNullOrEmpty(config.RegistrationStampLibraryPath) && string.IsNullOrEmpty(config.RegistrationStampAssetId) &&
                !config.PrintPrimaryStamp && !config.PrintRegistrationStamp,
                "PDF设置捕获清除历史附加印章选择并停用两槽");
            Check(config.PrintSignatures && config.PrintStamps && PlotLayerVisibility.LayersToSuppress(config).Count == 0,
                "PDF流程尊重原图内容，不因历史开关强制隐藏签名或印章图层");
            Check(config.SignatureLayerName == "MS_Sign" && config.StampLayerName == "MS_Stamp",
                "仅保留底层兼容映射默认值，不沿用旧无效图层名称");
            Check(File.ReadAllText(broken) == "{invalid-obsolete-library" && !File.Exists(missing),
                "清理过时选择不修改、修复或创建历史印章库文件");

            var frame = new PlotFrame
            {
                OrderIndex = 1, MinX = 0, MinY = 0, MaxX = 841, MaxY = 594, CalculatedScale = 1,
                StampRegion = new TemplateRegion { X1 = -80, Y1 = 10, X2 = -20, Y2 = 50 },
                RegistrationStampRegion = new TemplateRegion { X1 = -180, Y1 = 10, X2 = -120, Y2 = 50 },
                StampPlacement = StampPlacement.Fit(10, 10, 0, 40, 0, 0, 40, 8, 8),
                RegistrationStampPlacement = StampPlacement.Fit(80, 10, 0, 40, 0, 0, 40, 8, 8),
                AppliedPrintRegion = new TemplateRegion { X1 = -841, Y1 = 0, X2 = 0, Y2 = 594 },
                TitleInfo = new TitleBlockInfo { DrawingName = "PDF范围检查" }
            };
            // 即使准备函数被直接调用且配置还残留坏引用，也不需要宿主或读取印章库。
            config.Stamp = encrypted.Copy(); config.StampLibraryPath = broken; config.StampAssetId = encrypted.Id;
            config.RegistrationStamp = encrypted.Copy(); config.RegistrationStampLibraryPath = missing;
            var copies = (List<PlotFrame>)Call(form, "PrepareStampFrames", (object)new[] { frame })!;
            var copy = copies.Single();
            Check(copy.StampRegion == null && copy.RegistrationStampRegion == null && copy.StampPlacement == null && copy.RegistrationStampPlacement == null,
                "PDF图框准备无需CAD宿主或库访问，清除任务副本的全部附加印章定位");
            Check(frame.StampRegion != null && frame.RegistrationStampRegion != null && frame.StampPlacement != null && frame.RegistrationStampPlacement != null,
                "PDF图框准备不污染原列表对象的兼容字段");
            Check(!ReferenceEquals(copy, frame) && !ReferenceEquals(copy.DetectedPaper, frame.DetectedPaper) &&
                !ReferenceEquals(copy.TitleInfo, frame.TitleInfo) && !ReferenceEquals(copy.AppliedPrintRegion, frame.AppliedPrintRegion),
                "PDF图框准备深复制纸张、标题和裁切区域，保留打印相关快照隔离");
            copy.TitleInfo.DrawingName = "仅任务副本"; copy.DetectedPaper.WidthMm = 123;
            Check(frame.TitleInfo.DrawingName == "PDF范围检查" && frame.DetectedPaper.WidthMm == 841,
                "修改PDF任务副本不会改变原列表纸张和标题");
            Call(form, "CapturePlotSettings");
            Field<List<PlotFrame>>(form, "_frames").Add(frame); Call(form, "RefreshGrid");
            string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
            Directory.CreateDirectory(evidence);
            using var screenshot = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(screenshot, new Rectangle(0, 0, screenshot.Width, screenshot.Height));
            screenshot.Save(Path.Combine(evidence, "ui-pdf-scope.png"), ImageFormat.Png);
        }
        finally
        {
            CadHostProvider.Host = oldHost; CadHostProvider.Plotter = oldPlotter;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private static IEnumerable<Control> PdfScopeControls(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in PdfScopeControls(child)) yield return nested; }
    }
    private static IEnumerable<ToolStripItem> PdfScopeMenuItems(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            yield return item;
            if (item is ToolStripDropDownItem dropdown)
                foreach (var nested in PdfScopeMenuItems(dropdown.DropDownItems)) yield return nested;
        }
    }
}

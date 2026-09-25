using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckStampUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StampUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var oldHost = CadHostProvider.Host;
        var oldPlotter = CadHostProvider.Plotter;
        try
        {
            byte[] png;
            using (var bitmap = new Bitmap(64, 32, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var buffer = new MemoryStream())
            {
                graphics.Clear(Color.Transparent);
                graphics.FillRectangle(Brushes.Red, 4, 4, 56, 24);
                graphics.FillRectangle(Brushes.White, 10, 10, 44, 12);
                bitmap.Save(buffer, ImageFormat.Png); png = buffer.ToArray();
            }
            string path = Path.Combine(directory, "stamps.json");
            var asset = StampAsset.Import("红色几何测试图", png);
            var library = new StampLibrary { Assets = new() { asset } };
            StampLibraryStore.Save(path, library);
            // 固定旧时间，检测确认选择是否意外重写，而非依赖文件时间精度。
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            byte[] initialBytes = File.ReadAllBytes(path);
            DateTime initialTime = File.GetLastWriteTimeUtc(path);
            using (var dialog = new StampLibraryForm(path, asset.Id))
            {
                Check(dialog.SelectedLibraryPath == "" && dialog.SelectedAssetId == "", "印章管理窗确认前不提交调用方选择");
                Check(Field<ListBox>(dialog, "assets").Items.Count == 1 &&
                    ((StampAsset)Field<ListBox>(dialog, "assets").SelectedItem!).Id == asset.Id,
                    "已有印章库正确加载并恢复所选资产");
                var image = Field<PictureBox>(dialog, "preview").Image;
                Check(image != null && image.Width == 64 && image.Height == 32, "印章管理窗解码内嵌 PNG 并保持像素尺寸");
                RenderStampWindow(dialog, "ui-stamp-library.png");
                InvokeStampEditor(dialog, "ConfirmSelection");
                Check(dialog.DialogResult == DialogResult.OK && dialog.SelectedAssetId == asset.Id && dialog.SelectedLibraryPath == path,
                    "管理窗确认返回已验证的库路径和印章 ID");
            }
            Check(File.GetLastWriteTimeUtc(path) == initialTime && File.ReadAllBytes(path).SequenceEqual(initialBytes),
                "未修改印章库时确认选择不重写文件或修改时间");
            using (var dialog = new StampLibraryForm(path, asset.Id))
            {
                Field<TextBox>(dialog, "assetName").Text = "未保存的草稿";
                Check(Field<StampLibrary>(dialog, "library").Assets[0].Name == "未保存的草稿", "印章名称编辑落在内存草稿");
                // Dispose 相当于丢弃草稿，不触发需要交互确认的 FormClosing。
            }
            Check(File.ReadAllBytes(path).SequenceEqual(initialBytes) && StampLibraryStore.Load(path).Assets[0].Name == asset.Name,
                "丢弃印章编辑器草稿不修改磁盘库");
            using (var stale = new StampLibraryForm(path, asset.Id))
            {
                Field<TextBox>(stale, "assetName").Text = "过期编辑";
                var other = StampLibraryStore.Load(path); other.Assets[0].Name = "另一窗口已保存";
                StampLibraryStore.Save(path, other);
                byte[] current = File.ReadAllBytes(path);
                RejectStampEditor(() => InvokeStampEditor(stale, "ConfirmSelection"), typeof(IOException), "库并发更新时拒绝过期编辑器覆盖");
                Check(File.ReadAllBytes(path).SequenceEqual(current) && stale.SelectedAssetId == "",
                    "并发冲突保留最新库文件且不提交失效选择");
            }
            using (var staleRead = new StampLibraryForm(path, asset.Id))
            {
                var other = StampLibraryStore.Load(path); other.Assets[0].Name = "第二次共享库更新";
                StampLibraryStore.Save(path, other);
                RejectStampEditor(() => InvokeStampEditor(staleRead, "ConfirmSelection"), typeof(IOException),
                    "仅选择印章也检测共享库更新，要求重新加载");
            }
            using (var edited = new StampLibraryForm(path, asset.Id))
            {
                Field<TextBox>(edited, "assetName").Text = "确认保存的几何测试图";
                InvokeStampEditor(edited, "ConfirmSelection");
                Check(StampLibraryStore.Load(path).Assets[0].Name == "确认保存的几何测试图" && edited.SelectedAssetId == asset.Id,
                    "确认编辑的印章名称实际保存到库并返回选择");
            }
            using (var clear = new StampLibraryForm(path, asset.Id))
            {
                Field<CheckBox>(clear, "noStamp").Checked = true;
                InvokeStampEditor(clear, "ConfirmSelection");
                Check(clear.SelectedAssetId == "" && StampLibraryStore.Load(path).Assets.Count == 1,
                    "本次不附加印章只清除选择，不删除库内资产");
            }
            string brokenPath = Path.Combine(directory, "broken.json"); File.WriteAllText(brokenPath, "{broken");
            using (var broken = new StampLibraryForm(brokenPath, asset.Id))
            {
                Check(!Field<Panel>(broken, "editor").Enabled && Field<Label>(broken, "status").Text.Contains("操作未完成"),
                    "损坏印章库显示错误并禁用编辑");
                RejectStampEditor(() => InvokeStampEditor(broken, "ConfirmSelection"), typeof(InvalidOperationException),
                    "损坏印章库不能确认使用资产");
            }
            Check(File.ReadAllText(brokenPath) == "{broken", "打开损坏印章库不会自动覆盖为空库");

            var sourceRegion = new TemplateRegion { X1 = -90, Y1 = 20, X2 = -10, Y2 = 60 };
            var sourceTemplate = new TitleBlockTemplate { Name = "测试图框", BlockName = "STAMP_FRAME", StampRegion = sourceRegion };
            var titles = new TitleTemplateLibrary { Templates = new() { sourceTemplate } };
            using (var editor = new TitleTemplateForm(titles, null, () => null, frame => null))
            {
                var draft = Field<TemplateRegion>(editor, "stampRegion");
                Check(!ReferenceEquals(draft, sourceRegion) && draft.X1 == -90, "模板编辑器克隆印章区域，原始库不共享坐标对象");
                draft.X1 = -80;
                InvokeStampEditor(editor, "CommitEditor");
                Check(editor.Library.Templates[0].StampRegion!.X1 == -80 && sourceRegion.X1 == -90,
                    "模板提交保存印章区域而不污染原始库");
                draft.X1 = -70;
                Check(editor.Library.Templates[0].StampRegion!.X1 == -80, "已提交模板与编辑字段继续保持隔离");
                var copy = TitleTemplateService.Clone(editor.Library.Templates[0]); copy.StampRegion!.X1 = -60;
                Check(editor.Library.Templates[0].StampRegion!.X1 == -80, "复制模板深复制印章区域");
            }
            using (var region = new TemplateCropForm(sourceRegion, () => null, stampMode: true))
            {
                RenderStampWindow(region, "ui-stamp-region.png");
                var hiddenScale = Field<NumericUpDown>(region, "scale");
                Check(region.Text.Contains("印章") && (hiddenScale.Parent == null || !hiddenScale.Visible),
                    "印章区域编辑模式隐藏出图比例，避免与印章大小混淆");
                var value = (TemplateRegion)InvokeStampEditor(region, "ReadRegion")!;
                Check(value.X1 == -90 && value.Y2 == 60, "印章区域编辑器保留本地右下角偏移坐标");
                Field<CheckBox>(region, "enabled").Checked = false;
                Check(InvokeStampEditor(region, "ReadRegion") == null, "取消启用可清除模板附加印章区域");
            }

            var host = new OfflineStampHost(); var plotter = new OfflineStampPreview();
            CadHostProvider.Host = host; CadHostProvider.Plotter = plotter;
            using var main = new BatchPlotForm(Path.Combine(directory, "settings.json"));
            var config = Field<PlotConfig>(main, "_config");
            config.StampLibraryPath = path; config.StampAssetId = asset.Id; config.PrintStamps = true;
            var frame = new PlotFrame { OrderIndex = 1, Type = FrameType.BlockReference, SourceBlockName = "STAMP_FRAME", HandleOrId = "1",
                SourceDocumentId = "stamp-document", SourceLayoutId = "stamp-space", MinX = 0, MinY = 0, MaxX = 841, MaxY = 594, CalculatedScale = 1 };
            var templates = Field<TitleTemplateLibrary>(main, "_titleTemplates"); templates.Templates.Add(sourceTemplate);
            Call(main, "CaptureStampAsset");
            Check(config.Stamp != null && config.Stamp.Id == asset.Id && config.Stamp.PixelWidth == 64, "主窗捕获库内已选择印章内容作为任务快照");
            var prepared = (List<PlotFrame>)Call(main, "PrepareStampFrames", (object)new[] { frame })!;
            Check(host.Calls == 1 && host.LastRegion!.X1 == -90 && host.Width == 64 && host.Height == 32,
                "印章预检按图框模板向假宿主传递区域和真实像素比例");
            Check(prepared[0].StampPlacement != null && prepared[0].StampPlacement!.Bounds.MinX == 751 &&
                prepared[0].StampPlacement!.Bounds.MaxY == 60, "假宿主按图框右下角转换印章位置并通过打印范围校验");
            Check(frame.StampPlacement == null && frame.StampRegion == null && !ReferenceEquals(prepared[0], frame) &&
                !ReferenceEquals(prepared[0].StampRegion, sourceRegion), "印章预检只修改任务副本，不污染列表和模板区域");
            sourceTemplate.StampRegion = null;
            Reject(() => Call(main, "PrepareStampFrames", (object)new[] { frame }), "缺少印章区域时阻止整批打印预检");
            Check(host.Calls == 1, "缺失模板区域不会调用宿主定位");
            sourceTemplate.StampRegion = sourceRegion;
            config.StampAssetId = Guid.NewGuid().ToString("N");
            Reject(() => Call(main, "CaptureStampAsset"), "已选印章被移除时阻止预检");
            Check(config.Stamp == null, "资产失效后不保留上一次捕获的旧印章");
            config.StampAssetId = asset.Id; config.StampLibraryPath = brokenPath; config.PrintStamps = false;
            Call(main, "CaptureStampAsset");
            var without = (List<PlotFrame>)Call(main, "PrepareStampFrames", (object)prepared)!;
            Check(config.Stamp == null && without.All(f => f.StampPlacement == null && f.StampRegion == null) && host.Calls == 1,
                "关闭印章输出时不读取损坏库、不调用定位，并清除旧任务附加印章");
            config.StampLibraryPath = path; config.PrintStamps = true;
            Field<CheckBox>(main, "chkPrintStamps").Checked = true;
            var device = Field<ComboBox>(main, "cboPlotters"); device.Items.Add("offline-stamp.pc3"); device.SelectedIndex = device.Items.Count - 1;
            Field<List<PlotFrame>>(main, "_frames").Add(frame); Call(main, "RefreshGrid");
            main.Size=main.MinimumSize;
            Field<CheckBox>(main, "chkPrintStamps").Text="印章输出（已选附加）";
            RenderStampWindow(main,"ui-stamp-main-minimum.png");
            var grid = Field<DataGridView>(main, "dgvDrawings"); grid.ClearSelection(); grid.Rows[0].Selected = true;
            var outcome = (PlotPreviewOutcome)Call(main, "ExecuteFramePreview")!;
            Check(outcome == PlotPreviewOutcome.Closed && plotter.Calls == 1 && plotter.LastConfig!.Stamp!.Id == asset.Id &&
                plotter.LastFrame!.StampPlacement != null && plotter.LastFrame.StampRegion!.X1 == -90,
                "真实预览入口将印章快照及模板定位传给离线预览宿主");
            Check(plotter.FileCalls == 0 && frame.StampPlacement == null && !ReferenceEquals(plotter.LastConfig!.Stamp, config.Stamp),
                "附加印章预览不提交文件并隔离配置和列表对象");
        }
        finally
        {
            CadHostProvider.Host = oldHost; CadHostProvider.Plotter = oldPlotter;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private static object? InvokeStampEditor(object editor, string method) => editor.GetType().GetMethod(method, PrivateInstance)!.Invoke(editor, null);
    private static void RejectStampEditor(Action action, Type expected, string message)
    {
        try { action(); }
        catch (TargetInvocationException ex) when (ex.InnerException != null && expected.IsInstanceOfType(ex.InnerException)) { Check(true, message); return; }
        throw new Exception("未拒绝：" + message);
    }
    private static void ShowStampOffscreen(Form form)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000);
        form.Show(); form.PerformLayout();
    }
    private static void RenderStampWindow(Form form, string name)
    {
        ShowStampOffscreen(form);
        string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
        Directory.CreateDirectory(evidence);
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height)); image.Save(Path.Combine(evidence, name), ImageFormat.Png);
    }
    private sealed class OfflineStampHost : ICadHost, ICadContextHost, ICadStampHost
    {
        public int Calls; public int Width; public int Height; public TemplateRegion? LastRegion;
        public StampPlacement ResolveStampPlacement(PlotFrame frame, TemplateRegion region, int pixelWidth, int pixelHeight)
        {
            Calls++; Width = pixelWidth; Height = pixelHeight; LastRegion = TemplateCropGeometry.Copy(region);
            return StampPlacement.Fit(frame.MaxX + region.X1, frame.MinY + region.Y1, 0,
                region.X2 - region.X1, 0, 0, region.Y2 - region.Y1, pixelWidth, pixelHeight);
        }
        public bool IsFrameContextCurrent(PlotFrame frame) => frame.SourceDocumentId == "stamp-document" && frame.SourceLayoutId == "stamp-space";
        public string PlatformName => "离线印章假宿主"; public string Version => "test";
        public void WriteMessage(string message) { }
        public string GetCurrentDocumentPath() => @"D:\offline-fixture\stamps.dwg";
        public string GetCurrentDocumentName() => "stamps.dwg";
        public List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter = "*") => new();
        public List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter = "*") => new();
        public bool PromptSelectFrames(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks) { polylines = new(); blocks = new(); return false; }
        public bool PromptSelectSampleBlock(out string blockName, out string layerName) { blockName = ""; layerName = ""; return false; }
        public void ZoomToFrame(double minX, double minY, double maxX, double maxY) { }
        public List<string> GetAllLayers() => new(); public List<string> GetAllBlockNames() => new();
    }
    private sealed class OfflineStampPreview : ICadPlotter, ICadPlotPreview
    {
        public int Calls; public int FileCalls; public PlotConfig? LastConfig; public PlotFrame? LastFrame;
        public List<string> GetAvailablePlotters() => new(); public List<string> GetAvailablePlotStyles() => new();
        public List<string> GetPaperSizesForPlotter(string device) => new();
        public bool PlotFrameToFile(PlotFrame frame, PlotConfig config, string path, out string error) { FileCalls++; error = "离线预览不能输出文件"; return false; }
        public PlotPreviewOutcome PreviewFrame(PlotFrame frame, PlotConfig config, out string error)
        { Calls++; LastConfig = config; LastFrame = frame; error = ""; return PlotPreviewOutcome.Closed; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Pdf;
using ZwSoft.ZwCAD.ApplicationServices;
using ZwSoft.ZwCAD.DatabaseServices;
using ZwSoft.ZwCAD.Geometry;
using ZwSoft.ZwCAD.PlottingServices;
using CorePlotConfig = FastBatchPlot.Core.Models.PlotConfig;

namespace FastBatchPlot.ZWCAD
{
    public partial class ZwCadPlotEngine : ICadPlotter, ICadPrinter, ICadPlotPreview
    {
        public List<string> GetAvailablePlotters()
        {
            MediaCache.Clear();
            SweepStagedMedia();
            var list = new List<string>();
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                using (doc?.LockDocument())
                {
                    var psv = PlotSettingsValidator.Current;
                    var devices = psv.GetPlotDeviceList();
                    foreach (string d in devices)
                    {
                        list.Add(d);
                    }
                }
            }
            catch { }

            return list;
        }

        private static bool _stagedMediaSwept;

        // 只清理本工具异常中断遗留的 FBP_ 暂存配置；不修改任何用户或其他插件（如 M_PDF）的 PC5/PMP。
        private static void SweepStagedMedia()
        {
            if (_stagedMediaSwept) return;
            _stagedMediaSwept = true;
            try
            {
                var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var devices = PlotConfigManager.Devices)
                {
                    foreach (PlotConfigInfo d in devices)
                    {
                        if (!string.IsNullOrEmpty(d.FullPath) && Path.IsPathRooted(d.FullPath))
                            directories.Add(Path.GetDirectoryName(d.FullPath)!);
                    }
                }
                foreach (var directory in directories)
                    FastBatchPlot.Core.Printing.ZwPdfMediaFiles.SweepStaged(directory, TimeSpan.FromHours(1));
            }
            catch { }
        }

        public List<string> GetAvailablePlotStyles()
        {
            var list = new List<string>();
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                using (doc?.LockDocument())
                {
                    var psv = PlotSettingsValidator.Current;
                    var styles = psv.GetPlotStyleSheetList();
                    foreach (string s in styles)
                    {
                        list.Add(s);
                    }
                }
            }
            catch { }

            return list;
        }

        public List<string> GetPaperSizesForPlotter(string plotterDevice)
        {
            var list = new List<string>();
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                using (doc?.LockDocument())
                using (var ps = new PlotSettings(true))
                {
                    var psv = PlotSettingsValidator.Current;
                    psv.SetPlotConfigurationName(ps, plotterDevice, null);
                    psv.RefreshLists(ps);
                    var mediaList = psv.GetCanonicalMediaNameList(ps);
                    foreach (string m in mediaList)
                    {
                        list.Add(m);
                    }
                }
            }
            catch { }
            return list;
        }

        public bool PlotFrameToFile(PlotFrame frame, CorePlotConfig config, string outputFilePath, out string errorMessage)
            => ExecutePlot(frame, config, outputFilePath, false, false, out _, out errorMessage);

        public bool PrintFrameToDevice(PlotFrame frame, CorePlotConfig config, out string errorMessage)
            => ExecutePlot(frame, config, null, true, false, out _, out errorMessage);

        public PlotPreviewOutcome PreviewFrame(PlotFrame frame, CorePlotConfig config, out string errorMessage)
        {
            return ExecutePlot(frame, config, null, false, true, out var outcome, out errorMessage)
                ? outcome : PlotPreviewOutcome.Failed;
        }

        private bool ExecutePlot(PlotFrame frame, CorePlotConfig config, string? outputFilePath, bool toPrinter, bool preview, out PlotPreviewOutcome previewOutcome, out string errorMessage)
        {
            errorMessage = string.Empty;
            previewOutcome = PlotPreviewOutcome.Failed;
            string? temporaryPath = null;
            object? previousBackgroundPlot = null;
            object? previousImageFrame = null;
            string? stampTemporaryPath = null;
            FastBatchPlot.Core.Printing.ZwPdfMediaFiles? generatedMedia = null;
            bool deviceSubmissionStarted = false;
            string stage = "检查打印参数";
            try
            {
                var printableStamp=FastBatchPlot.Core.Assets.StampOutputGuard.Resolve(config);
                if(frame.AppliedPrintRegion!=null)
                {
                    var fresh=new ZwCadAdapter().ResolveTemplatePrintBounds(frame,frame.AppliedPrintRegion);
                    if(!frame.PrintBounds.HasValue||!FastBatchPlot.Core.Templates.TemplateCropGeometry.Same(fresh,frame.PrintBounds.Value))
                        throw new InvalidOperationException("图框位置、缩放或动态范围已改变，请重新应用模板打印范围。");
                }
                if (string.IsNullOrWhiteSpace(config.PlotStyleTable))
                    throw new InvalidOperationException("未选择打印样式，拒绝隐式继承布局样式。");
                var plan = PlotPlanBuilder.Create(frame, config);
                if (string.IsNullOrWhiteSpace(config.PrinterDevice)) throw new ArgumentException("请选择打印设备。");
                if (!preview && !toPrinter && config.Copies != 1) throw new ArgumentException("文件输出份数必须为 1。");
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) throw new InvalidOperationException("当前无活动的 CAD 图纸文档。");
                var db = doc.Database;
                if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                    throw new InvalidOperationException("CAD 正在执行其他打印任务，请完成后重试。");

                if (!preview && !toPrinter) temporaryPath = PlotFileFormats.CreateTemporaryPath(outputFilePath!, config.ExportFormat);
                using (doc.LockDocument())
                using (var context = new ZwCadLayoutContext(doc, frame))
                {
                    stage = "切换来源布局";
                    context.Activate();
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                    var currentBtr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
                    ApplySignatureStampPlotVisibility(tr, db, config);
                    RasterImageDef? stampDefinition=null;
                    Exception? stampFailure=null;
                    try
                    {
                    if(printableStamp!=null)
                    {
                        printableStamp.Validate();
                        if(frame.StampRegion==null||frame.StampPlacement==null)throw new InvalidOperationException("印章定位缺失，请重新预检任务。");
                        var fresh=new ZwCadAdapter().ResolveStampPlacement(frame,frame.StampRegion,printableStamp.PixelWidth,printableStamp.PixelHeight).ForOutput(printableStamp,plan);
                        if(!fresh.Same(frame.StampPlacement))throw new InvalidOperationException("图框或印章区域已变化，请重新开始任务。");
                        fresh.EnsureWithin(plan);
                        stampTemporaryPath=Path.Combine(Path.GetTempPath(),"batchplot-stamp-"+Guid.NewGuid().ToString("N")+".png");
                        var png=Convert.FromBase64String(printableStamp.PngBase64);
                        using(var stampFile=new FileStream(stampTemporaryPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))stampFile.Write(png,0,png.Length);
                        previousImageFrame=Application.GetSystemVariable("IMAGEFRAME");
                        Application.SetSystemVariable("IMAGEFRAME",0);
                        InsertTemporaryStamp(db,tr,currentBtr,printableStamp,fresh,stampTemporaryPath,out stampDefinition);
                    }
                    var layout = (Layout)tr.GetObject(currentBtr.LayoutId, OpenMode.ForRead);
                    using (var plotInfo = new PlotInfo())
                    using (var ps = new PlotSettings(layout.ModelType))
                    using (var piv = new PlotInfoValidator())
                    {
                        plotInfo.Layout = layout.ObjectId;
                        stage = "读取布局打印设置";
                        ps.CopyFrom(layout);
                        var psv = PlotSettingsValidator.Current;
                        stage = "加载打印设备";
                        psv.SetPlotConfigurationName(ps, config.PrinterDevice, null);
                        psv.RefreshLists(ps);
                        stage = "匹配纸张与 PDF 参数";
                        PlotMedia media;
                        try { media = !toPrinter && !config.SendToPrinter && config.ExportFormat == PlotExportFormat.PDF && config.PdfOptions != null && config.PdfOptions.HasOverrides
                            ? ResolvePdfMedia(ps, plan, config.PrinterDevice, config.PdfOptions, out generatedMedia) : FindMedia(ps, plan); }
                        catch (MissingPlotMediaException missing) when (!toPrinter && !config.SendToPrinter &&
                            config.ExportFormat == PlotExportFormat.PDF)
                        {
                            try { media = ResolvePdfMedia(ps, plan, config.PrinterDevice, config.PdfOptions, out generatedMedia); }
                            catch (Exception custom)
                            {
                                throw new InvalidOperationException(missing.Message + " 自动纸张配置失败：" + custom.Message, custom);
                            }
                        }
                        stage = "设置纸张和打印范围";
                        psv.SetCanonicalMediaName(ps, media.Name);
                        psv.SetPlotPaperUnits(ps, PlotPaperUnit.Millimeters);
                        psv.SetPlotRotation(ps, PlotPlanBuilder.RotateMedia(plan, media)
                            ? PlotRotation.Degrees090 : PlotRotation.Degrees000);
                        psv.SetPlotType(ps, ZwSoft.ZwCAD.DatabaseServices.PlotType.Window);
                        // PlotWindowArea 使用 DCS，检测器坐标使用 WCS。
                        using (var view = doc.Editor.GetCurrentView())
                        {
                            var direction = view.ViewDirection.GetNormal();
                            if (view.PerspectiveEnabled || (direction - Vector3d.ZAxis).Length > 1e-8
                                || Math.Abs(Math.Sin(view.ViewTwist / 2)) > 1e-8)
                                throw new NotSupportedException("当前版本仅支持未旋转的二维俯视图，请恢复平面视图后打印。");
                            var toDcs = Matrix3d.Displacement(Point3d.Origin - view.Target);
                            var min = new Point3d(plan.MinX, plan.MinY, 0).TransformBy(toDcs);
                            var max = new Point3d(plan.MaxX, plan.MaxY, 0).TransformBy(toDcs);
                            psv.SetPlotWindowArea(ps, new Extents2d(min.X, min.Y, max.X, max.Y));
                        }
                        stage = "设置样式和比例";
                        if (!string.IsNullOrWhiteSpace(config.PlotStyleTable))
                            psv.SetCurrentStyleSheet(ps, config.PlotStyleTable);
                        ps.PlotPlotStyles = true;
                        ps.PrintLineweights = true;
                        psv.SetPlotCentered(ps, false);
                        PlotPlanBuilder.Origin(plan,media,out double originX,out double originY);
                        psv.SetPlotOrigin(ps,new Point2d(originX,originY));
                        psv.SetUseStandardScale(ps, false);
                        psv.SetCustomPrintScale(ps, new CustomScale(1.0, plan.ScaleDenominator));
                        plotInfo.OverrideSettings = ps;
                        piv.MediaMatchingPolicy = MatchingPolicy.MatchDisabled;
                        using (var requestedSettings = generatedMedia == null ? null : new PlotSettings(ps.ModelType))
                        {
                            // 验证器可能归一化输入；用独立快照保留本次真正请求的值。
                            requestedSettings?.CopyFrom(ps);
                            stage = "验证宿主打印配置";
                            piv.Validate(plotInfo);
                            stage = "核对自定义 PDF 配置";
                            if (generatedMedia != null) ValidateGeneratedPdfMedia(plotInfo, requestedSettings!, plan, generatedMedia);
                        }
                        stage = "检查驱动输出能力";
                        // 中望 2026 的 ValidatedConfig 返回空原生指针，即使验证及输出成功也不可读。
                        // 校验实际 ValidatedSettings 设备；输出能力由 BeginDocument 的原生结果及文件校验确认。
                        using (var validated = plotInfo.ValidatedSettings)
                        {
                            bool deviceMatched = validated != null && (
                                string.Equals(validated.PlotConfigurationName, ps.PlotConfigurationName, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Path.GetFileName(validated.PlotConfigurationName), Path.GetFileName(ps.PlotConfigurationName), StringComparison.OrdinalIgnoreCase));
                            if (!plotInfo.IsValidated || !deviceMatched)
                                throw new InvalidOperationException("宿主没有保留所选打印设备，已停止出图。");
                        }
                        SinglePdfTrace.Write("ZwCadPlotEngine.ProcessPlotState.Check", PlotFactory.ProcessPlotState.ToString());
                        if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                            throw new InvalidOperationException("CAD 打印引擎正忙，未执行本次打印。");

                        // 同步完成后才能检查新文件，并在 finally 恢复用户的后台打印设置。
                        stage = "创建同步打印引擎";
                        previousBackgroundPlot = Application.GetSystemVariable("BACKGROUNDPLOT");
                        Application.SetSystemVariable("BACKGROUNDPLOT", 0);
                        // 0 禁用预览中的打印、上一页和下一页按钮；不从预览转发设备任务。
                        using (var pe = preview ? PlotFactory.CreatePreviewEngine(0) : PlotFactory.CreatePublishEngine())
                        using (var pageInfo = new PlotPageInfo())
                        using (var previewInfo = preview ? new PreviewEndPlotInfo() : null)
                        {
                            FastBatchPlot.Core.Assets.StampOutputGuard.Resolve(config);
                            stage = "开始打印";
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginPlot.Before");
                            pe.BeginPlot(null, null);
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginPlot.After");
                            deviceSubmissionStarted = !preview && toPrinter;
                            stage = "开始输出文档";
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginDocument.Before", temporaryPath ?? "(printer)");
                            pe.BeginDocument(plotInfo, doc.Name, null, !preview && toPrinter ? config.Copies : 1, !preview && !toPrinter, temporaryPath);
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginDocument.After");
                            stage = "开始输出页面";
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginPage.Before");
                            pe.BeginPage(pageInfo, plotInfo, true, null);
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginPage.After");
                            stage = "生成页面图形";
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginGenerateGraphics.Before");
                            pe.BeginGenerateGraphics(null);
                            SinglePdfTrace.Write("ZwCadPlotEngine.BeginGenerateGraphics.After");
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndGenerateGraphics.Before");
                            pe.EndGenerateGraphics(null);
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndGenerateGraphics.After");
                            stage = "结束输出页面";
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndPage.Before");
                            pe.EndPage(previewInfo);
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndPage.After");
                            stage = "结束输出文档";
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndDocument.Before");
                            pe.EndDocument(null);
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndDocument.After");
                            stage = "结束打印";
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndPlot.Before");
                            pe.EndPlot(null);
                            SinglePdfTrace.Write("ZwCadPlotEngine.EndPlot.After");
                            if (previewInfo != null)
                            {
                                switch (previewInfo.Status)
                                {
                                    case PreviewEndPlotStatus.Normal: previewOutcome = PlotPreviewOutcome.Closed; break;
                                    case PreviewEndPlotStatus.Cancel: previewOutcome = PlotPreviewOutcome.Cancelled; break;
                                    case PreviewEndPlotStatus.Plot: previewOutcome = PlotPreviewOutcome.PrintRequested; break;
                                    default: throw new InvalidOperationException("单页预览返回了不支持的翻页状态。");
                                }
                            }
                        }
                        SinglePdfTrace.Write("ZwCadPlotEngine.PublishEngine.Disposed");
                    }
                    }
                    catch(Exception ex){stampFailure=ex;throw;}
                    finally
                    {
                        try { stampDefinition?.Unload(false); }
                        catch(Exception cleanup)
                        {
                            if(stampFailure!=null)throw new AggregateException(stampFailure.Message+" 印章图像卸载失败："+cleanup.Message,stampFailure,cleanup);
                            throw new InvalidOperationException("印章图像卸载失败："+cleanup.Message,cleanup);
                        }
                    }
                    // 所有临时图层修改随事务回滚，成功与异常路径均不提交到源图。
                    stage = "恢复临时数据库操作";
                    tr.Abort();
                    }
                }
                SinglePdfTrace.Write("ZwCadPlotEngine.LayoutContext.Disposed");
                stage = "恢复打印环境";
                if(previousImageFrame!=null){Application.SetSystemVariable("IMAGEFRAME",previousImageFrame);previousImageFrame=null;}
                Application.SetSystemVariable("BACKGROUNDPLOT", previousBackgroundPlot);
                previousBackgroundPlot = null;
                if(stampTemporaryPath!=null){File.Delete(stampTemporaryPath);stampTemporaryPath=null;}
                stage = "校验并提交输出文件";
                if (!preview && !toPrinter)
                {
                    SinglePdfTrace.Write("ZwCadPlotEngine.ValidateAndCommit.Before", outputFilePath);
                    PlotFileFormats.ValidateAndCommit(temporaryPath!, outputFilePath!, config.ExportFormat, plan, config.OverwriteExisting);
                    SinglePdfTrace.Write("ZwCadPlotEngine.ValidateAndCommit.After", outputFilePath);
                }
                SinglePdfTrace.Write("ZwCadPlotEngine.ProcessPlotState.After", PlotFactory.ProcessPlotState.ToString());
                return true;
            }
            catch (Exception ex)
            {
                SinglePdfTrace.Write("ZwCadPlotEngine.Error", $"Stage: {stage}, Error: {ex.Message}");
                LogPlotError(stage, ex);
                errorMessage = stage + "失败：" + ex.Message + (deviceSubmissionStarted ? " 设备可能已接收本页任务，请先检查打印队列，避免重复提交。" : "");
                return false;
            }
            finally
            {
                if (generatedMedia != null)
                {
                    try { generatedMedia.Dispose(); }
                    catch (Exception ex) { errorMessage += " 临时纸张配置清理失败：" + ex.Message; }
                }
                if(previousImageFrame!=null)
                {
                    try{Application.SetSystemVariable("IMAGEFRAME",previousImageFrame);}
                    catch(Exception ex){errorMessage+=" 图片边框设置恢复失败："+ex.Message;}
                }
                if(stampTemporaryPath!=null && File.Exists(stampTemporaryPath))
                {
                    try{File.Delete(stampTemporaryPath);}
                    catch(Exception ex){errorMessage+=" 临时印章文件清理失败："+ex.Message;}
                }
                if (previousBackgroundPlot != null)
                {
                    try { Application.SetSystemVariable("BACKGROUNDPLOT", previousBackgroundPlot); }
                    catch (Exception ex) { errorMessage += " 后台打印设置恢复失败：" + ex.Message; }
                }
                if (temporaryPath != null && File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) { errorMessage += " 临时文件清理失败：" + ex.Message; }
                }
            }
        }

        private static void LogPlotError(string stage, Exception ex)
        {
            try
            {
                // 日志写入本地应用数据目录并限制大小，不在桌面或漫游配置中无限增长。
                var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FastBatchPlot", "logs");
                Directory.CreateDirectory(logDirectory);
                var logFile = Path.Combine(logDirectory, "plot-errors.log");
                if (File.Exists(logFile) && new FileInfo(logFile).Length > 2 * 1024 * 1024)
                    File.Copy(logFile, logFile + ".1", true);
                if (File.Exists(logFile + ".1") && File.Exists(logFile) && new FileInfo(logFile).Length > 2 * 1024 * 1024)
                    File.Delete(logFile);
                var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Stage: {stage}] {ex.GetType().FullName}: {ex.Message}\r\nStack Trace:\r\n{ex.StackTrace}\r\n";
                if (ex.InnerException != null)
                {
                    message += $"Inner Exception: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}\r\nStack Trace:\r\n{ex.InnerException.StackTrace}\r\n";
                }
                message += new string('-', 60) + "\r\n";
                File.AppendAllText(logFile, message);
            }
            catch
            {
            }
        }

        private static void ApplySignatureStampPlotVisibility(Transaction tr, Database db, CorePlotConfig config)
        {
            var requested = PlotLayerVisibility.LayersToSuppress(config);
            if (requested.Count == 0) return;
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (ObjectId id in table)
            {
                var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (!requested.Contains(layer.Name) || !layer.IsPlottable) continue;
                // 不解除锁定、冻结或外参限制；宿主拒绝时整页失败并回滚。
                tr.GetObject(id, OpenMode.ForWrite);
                layer.IsPlottable = false;
                if (layer.IsPlottable) throw new InvalidOperationException("无法禁止图层输出：" + layer.Name);
            }
        }

        // 同一设备的纸张列表在一个批次内不变；逐页枚举并切换每种纸张读取尺寸很慢（纸张多的设备每页 0.1~0.5 秒）。
        // 缓存按设备名保存几分钟，打开窗口（GetAvailablePlotters）时清空，设备配置改动后重新读取。
        private static readonly Dictionary<string, (DateTime Time, List<PlotMedia> Media)> MediaCache =
            new Dictionary<string, (DateTime, List<PlotMedia>)>(StringComparer.OrdinalIgnoreCase);
        private static readonly TimeSpan MediaCacheLifetime = TimeSpan.FromMinutes(10);

        private static PlotMedia FindMedia(PlotSettings settings, PlotPlan plan)
        {
            var psv = PlotSettingsValidator.Current;
            string device = settings.PlotConfigurationName ?? string.Empty;
            if (MediaCache.TryGetValue(device, out var cached) && DateTime.UtcNow - cached.Time < MediaCacheLifetime)
            {
                try { return PlotPlanBuilder.SelectMedia(plan, cached.Media); }
                catch (InvalidOperationException ex) { throw new MissingPlotMediaException(ex.Message, ex); }
            }
            var candidates = new List<PlotMedia>();
            var failures = new List<string>();
            var names = psv.GetCanonicalMediaNameList(settings);
            foreach (string name in names)
            {
                try
                {
                // 从驱动读取真实尺寸和可打印区域，不能从名称猜测毫米或英寸。
                psv.SetCanonicalMediaName(settings, name);
                psv.SetPlotPaperUnits(settings, PlotPaperUnit.Millimeters);
                psv.SetPlotRotation(settings, PlotRotation.Degrees000);
                var size = settings.PlotPaperSize;
                var margins = settings.PlotPaperMargins;
                candidates.Add(new PlotMedia
                {
                    Name = name,
                    WidthMm = size.X,
                    PrintableLeftMm = margins.MinPoint.X,
                    PrintableBottomMm = margins.MinPoint.Y,
                    HeightMm = size.Y,
                    PrintableWidthMm = size.X - margins.MinPoint.X - margins.MaxPoint.X,
                    PrintableHeightMm = size.Y - margins.MinPoint.Y - margins.MaxPoint.Y
                });
                }
                catch (Exception ex) { failures.Add(name + "：" + ex.Message); }
            }
            if (failures.Count == 0 && device.Length > 0) MediaCache[device] = (DateTime.UtcNow, candidates);
            try { return PlotPlanBuilder.SelectMedia(plan, candidates); }
            catch (InvalidOperationException ex)
            {
                // 只有完整枚举后确实缺少匹配纸张才允许自动配置；设备读取错误不能降级掩盖。
                if (failures.Count == 0) throw new MissingPlotMediaException(ex.Message, ex);
                throw new InvalidOperationException(ex.Message + " 部分驱动介质读取失败：" + string.Join("；", failures), ex);
            }
        }

        private sealed class MissingPlotMediaException : InvalidOperationException
        {
            public MissingPlotMediaException(string message, Exception inner) : base(message, inner) { }
        }
    }
}


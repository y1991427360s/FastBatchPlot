using System;
using System.Collections.Generic;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckPageConsistency()
    {
        var previousHost=CadHostProvider.Host;var previousPlotter=CadHostProvider.Plotter;
        string directory=Path.Combine(Path.GetTempPath(),"page-consistency-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            var host=new RecoveryHost();var plotter=new ResourceRecoveryPlotter();
            CadHostProvider.Host=host;CadHostProvider.Plotter=plotter;
            var frame=new PlotFrame {SourceDocumentId="layout-document",SourceLayoutId="space-1",OrderIndex=1,MaxX=420,MaxY=297,CalculatedScale=1,IsLandscape=true,DetectedPaper=new PaperSize("A3",420,297)};
            var config=new PlotConfig{MergeToSinglePdf=false};
            var stable=new CadPageConsistency(host,plotter,frame,config);stable.Verify();Check(true,"无来源和资源变化时页后校验通过");
            var switched=new CadPageConsistency(host,plotter,frame,config);CadHostProvider.Plotter=new RecoveryPlotter();
            bool rejected=false;try{switched.Verify();}catch(InvalidOperationException ex){rejected=ex.Message.Contains("宿主已切换");}
            Check(rejected,"输出期间宿主实例切换被页后校验拒绝");CadHostProvider.Plotter=plotter;
            using var form=new BatchPlotForm(null);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-32000,-32000);form.Show();
            plotter.SuccessWarning="模拟清理警告";plotter.AfterWrite=()=>host.Token="changed";
            string path=Path.Combine(directory,"uncertain.pdf");
            var run=new BatchPlotRun(new[]{new BatchPage(frame,path)},config);
            PumpUntil((System.Threading.Tasks.Task)Call(form,"RunPlotPages",run,new[]{frame},host,plotter)!);
            // 新批次不逐页复核来源（避免中望内部事件误判与逐页全空间扫描）；逐页复核仅用于历史重试。
            Check(run.Pages[0].State==BatchPageState.Succeeded&&File.Exists(path),"新批次不做逐页来源复核，已校验的页面记为成功");
            Check(run.Pages[0].Error.Contains("模拟清理警告"),"成功页保留原打印接口的清理警告");

            foreach (var blank in new[] { (string?)null, "", "   " })
            {
                var blankHost = new ConsistencyRevisionHost { RevisionProvider = _ => blank };
                bool hostConstructRejected = false;
                try { new CadPageConsistency(blankHost, plotter, frame, config); }
                catch (InvalidOperationException) { hostConstructRejected = true; }
                Check(hostConstructRejected, $"宿主返回空标记({(blank == null ? "null" : blank.Length == 0 ? "空串" : "空白")})构造时拒绝");

                var blankPlotter = new ConsistencyResourcePlotter { ResourceRevisionProvider = _ => blank };
                bool plotterConstructRejected = false;
                try { new CadPageConsistency(host, blankPlotter, frame, config); }
                catch (InvalidOperationException) { plotterConstructRejected = true; }
                Check(plotterConstructRejected, $"打印器返回空标记({(blank == null ? "null" : blank.Length == 0 ? "空串" : "空白")})构造时拒绝");
            }

            foreach (var blank in new[] { (string?)null, "", "   " })
            {
                var verifyBlankHost = new ConsistencyRevisionHost { RevisionProvider = _ => "valid-host-rev" };
                CadHostProvider.Host = verifyBlankHost;
                CadHostProvider.Plotter = plotter;
                var verifyHostConsistency = new CadPageConsistency(verifyBlankHost, plotter, frame, config);
                verifyBlankHost.RevisionProvider = _ => blank;
                bool verifyHostBlankFailed = false;
                try { verifyHostConsistency.Verify(); }
                catch (InvalidOperationException ex)
                {
                    verifyHostBlankFailed = ex.Message.Contains("输出后来源核验失败") &&
                                            ex.Message.Contains("已生成文件保留供检查，本页不记为成功");
                }
                Check(verifyHostBlankFailed, $"Verify 时宿主返回空标记({(blank == null ? "null" : blank.Length == 0 ? "空串" : "空白")})核验失败且提示保留文件");

                var verifyBlankPlotter = new ConsistencyResourcePlotter { ResourceRevisionProvider = _ => "valid-plotter-res" };
                CadHostProvider.Host = host;
                CadHostProvider.Plotter = verifyBlankPlotter;
                var verifyPlotterConsistency = new CadPageConsistency(host, verifyBlankPlotter, frame, config);
                verifyBlankPlotter.ResourceRevisionProvider = _ => blank;
                bool verifyPlotterBlankFailed = false;
                try { verifyPlotterConsistency.Verify(); }
                catch (InvalidOperationException ex)
                {
                    verifyPlotterBlankFailed = ex.Message.Contains("输出后来源核验失败") &&
                                               ex.Message.Contains("已生成文件保留供检查，本页不记为成功");
                }
                Check(verifyPlotterBlankFailed, $"Verify 时打印器返回空标记({(blank == null ? "null" : blank.Length == 0 ? "空串" : "空白")})核验失败且提示保留文件");
            }

            var throwHost = new ConsistencyRevisionHost { RevisionProvider = _ => "init-host-rev" };
            CadHostProvider.Host = throwHost;
            CadHostProvider.Plotter = plotter;
            var throwHostCheck = new CadPageConsistency(throwHost, plotter, frame, config);
            throwHost.RevisionProvider = _ => throw new InvalidOperationException("宿主内部状态异常");
            bool throwHostFailed = false;
            try { throwHostCheck.Verify(); }
            catch (InvalidOperationException ex)
            {
                throwHostFailed = ex.Message.Contains("输出后来源核验失败") &&
                                  ex.Message.Contains("宿主内部状态异常") &&
                                  ex.Message.Contains("已生成文件保留供检查，本页不记为成功");
            }
            Check(throwHostFailed, "Verify 时宿主接口抛错核验失败且保留清理提示与已有文件");

            var throwPlotter = new ConsistencyResourcePlotter { ResourceRevisionProvider = _ => "init-plotter-res" };
            CadHostProvider.Host = host;
            CadHostProvider.Plotter = throwPlotter;
            var throwPlotterCheck = new CadPageConsistency(host, throwPlotter, frame, config);
            throwPlotter.ResourceRevisionProvider = _ => throw new InvalidOperationException("打印资源已被占用");
            bool throwPlotterFailed = false;
            try { throwPlotterCheck.Verify(); }
            catch (InvalidOperationException ex)
            {
                throwPlotterFailed = ex.Message.Contains("输出后来源核验失败") &&
                                     ex.Message.Contains("打印资源已被占用") &&
                                     ex.Message.Contains("已生成文件保留供检查，本页不记为成功");
            }
            Check(throwPlotterFailed, "Verify 时打印器接口抛错核验失败且保留清理提示与已有文件");

            var multiHost = new ConsistencyRevisionHost { RevisionProvider = _ => "snap-rev-1" };
            var multiPlotter = new ConsistencyResourcePlotter { ResourceRevisionProvider = _ => "snap-res-1" };
            CadHostProvider.Host = multiHost;
            CadHostProvider.Plotter = multiPlotter;
            var multiConsistency = new CadPageConsistency(multiHost, multiPlotter, frame, config);
            multiConsistency.Verify();
            multiConsistency.Verify();
            Check(true, "多次 Verify 保持初始快照一致时校验通过");
            multiHost.RevisionProvider = _ => "snap-rev-2";
            bool multiFail = false;
            try { multiConsistency.Verify(); }
            catch (InvalidOperationException ex)
            {
                multiFail = ex.Message.Contains("已生成文件保留供检查，本页不记为成功");
            }
            Check(multiFail, "宿主变化后再次 Verify 依然按最初快照判定失败");
            multiHost.RevisionProvider = _ => "snap-rev-1";
            multiConsistency.Verify();
            Check(true, "宿主恢复最初版本标记后 Verify 再次通过，证明基准快照未被后续调用覆盖");

            var snapshotFrame = new PlotFrame { SourceDocumentId="original-doc" };
            var snapshotConfig = new PlotConfig { PlotStyleTable="original.ctb" };
            var isolationHost = new ConsistencyRevisionHost { RevisionProvider=f=>f.SourceDocumentId };
            var isolationPlotter = new ConsistencyResourcePlotter { ResourceRevisionProvider=c=>c.PlotStyleTable };
            CadHostProvider.Host=isolationHost;CadHostProvider.Plotter=isolationPlotter;
            var isolation=new CadPageConsistency(isolationHost,isolationPlotter,snapshotFrame,snapshotConfig);
            snapshotFrame.SourceDocumentId="edited-doc";snapshotConfig.PlotStyleTable="edited.ctb";
            isolation.Verify();Check(true,"构造后外部修改原图框和配置不会污染页后核验快照");
            var plainHost = new ConsistencyMockHost();
            var plainPlotter = new RecoveryPlotter();
            CadHostProvider.Host = plainHost;
            CadHostProvider.Plotter = plainPlotter;
            var plainConsistency = new CadPageConsistency(plainHost, plainPlotter, frame, config);
            plainConsistency.Verify();
            Check(true, "未实现可选接口的兼容宿主与打印器可正常构造与核验");
        }
        finally{CadHostProvider.Host=previousHost;CadHostProvider.Plotter=previousPlotter;Directory.Delete(directory,true);}
    }

    private class ConsistencyMockHost : ICadHost
    {
        public string PlatformName => "MockPlatform";
        public string Version => "1.0";
        public void WriteMessage(string message) {}
        public string GetCurrentDocumentPath() => "";
        public string GetCurrentDocumentName() => "";
        public List<RawPolylineCandidate> CollectPolylineCandidates(string layerFilter = "*") => new List<RawPolylineCandidate>();
        public List<RawBlockCandidate> CollectBlockCandidates(string blockNameFilter = "*") => new List<RawBlockCandidate>();
        public bool PromptSelectFrames(out List<RawPolylineCandidate> polylines, out List<RawBlockCandidate> blocks) { polylines = new List<RawPolylineCandidate>(); blocks = new List<RawBlockCandidate>(); return false; }
        public bool PromptSelectSampleBlock(out string blockName, out string layerName) { blockName = ""; layerName = ""; return false; }
        public void ZoomToFrame(double minX, double minY, double maxX, double maxY) {}
        public List<string> GetAllLayers() => new List<string>();
        public List<string> GetAllBlockNames() => new List<string>();
    }

    private sealed class ConsistencyRevisionHost : ConsistencyMockHost, ICadRevisionHost
    {
        public Func<PlotFrame, string?>? RevisionProvider { get; set; }
        public string GetSourceRevision(PlotFrame frame) => (RevisionProvider != null ? RevisionProvider(frame) : null)!;
    }

    private sealed class ConsistencyResourcePlotter : RecoveryPlotter, ICadPlotResourceHost
    {
        public Func<PlotConfig, string?>? ResourceRevisionProvider { get; set; }
        public string GetPlotResourceRevision(PlotConfig config) => (ResourceRevisionProvider != null ? ResourceRevisionProvider(config) : null)!;
    }
}

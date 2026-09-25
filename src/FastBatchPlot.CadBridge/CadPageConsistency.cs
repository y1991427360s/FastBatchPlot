using System;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.CadBridge
{
    /// <summary>围绕一次输出比较已知依赖；不持有 CAD 锁，不声称覆盖全部外部资源。</summary>
    public sealed class CadPageConsistency
    {
        private readonly ICadHost host;
        private readonly ICadPlotter plotter;
        private readonly BatchPlotRun snapshot;
        private readonly string? sourceRevision;
        private readonly string? resourceRevision;

        public CadPageConsistency(ICadHost host, ICadPlotter plotter, PlotFrame frame, PlotConfig config)
        {
            this.host=host;this.plotter=plotter;
            snapshot=new BatchPlotRun(new[]{new BatchPage(frame,"")},config);
            if (host is ICadRevisionHost revHost)
            {
                sourceRevision = revHost.GetSourceRevision(snapshot.Pages[0].Frame);
                if (string.IsNullOrWhiteSpace(sourceRevision))
                    throw new InvalidOperationException("CAD 宿主实现了版本接口但返回了空标记。");
            }
            if (plotter is ICadPlotResourceHost resHost)
            {
                resourceRevision = resHost.GetPlotResourceRevision(snapshot.Config);
                if (string.IsNullOrWhiteSpace(resourceRevision))
                    throw new InvalidOperationException("打印器实现了资源接口但返回了空标记。");
            }
        }

        public void Verify()
        {
            try
            {
                if(!ReferenceEquals(host,CadHostProvider.Host)||!ReferenceEquals(plotter,CadHostProvider.Plotter))
                    throw new InvalidOperationException("CAD 宿主已切换。");
                if(sourceRevision!=null)
                {
                    var currentSource = ((ICadRevisionHost)host).GetSourceRevision(snapshot.Pages[0].Frame);
                    if(string.IsNullOrWhiteSpace(currentSource))
                        throw new InvalidOperationException("原图修订标记变为空。");
                    if(!string.Equals(sourceRevision,currentSource,StringComparison.Ordinal))
                        throw new InvalidOperationException("原图、外参会话或修改记录已变化。");
                }
                if(resourceRevision!=null)
                {
                    var currentResource = ((ICadPlotResourceHost)plotter).GetPlotResourceRevision(snapshot.Config);
                    if(string.IsNullOrWhiteSpace(currentResource))
                        throw new InvalidOperationException("打印样式或设备配置标记变为空。");
                    if(!string.Equals(resourceRevision,currentResource,StringComparison.Ordinal))
                        throw new InvalidOperationException("打印样式或设备配置已变化。");
                }
            }
            catch(Exception ex)
            {
                throw new InvalidOperationException("输出后来源核验失败："+ex.Message+" 已生成文件保留供检查，本页不记为成功、不参与自动合并；请重新建立完整批次。",ex);
            }
        }
    }
}

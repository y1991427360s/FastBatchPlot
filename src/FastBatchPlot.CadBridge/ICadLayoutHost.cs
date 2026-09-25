using FastBatchPlot.Core.Models;

namespace FastBatchPlot.CadBridge
{
    public enum CadScanScope { CurrentSpace, Model, AllLayouts, ModelAndLayouts }

    /// <summary>只读遍历活动文档的空间，以及验证跨布局图框来源。</summary>
    public interface ICadLayoutHost
    {
        CadCandidateSnapshot CollectCandidates(CadScanScope scope, string layerFilter = "*");
        bool CanAccessFrameContext(PlotFrame frame);
    }
}

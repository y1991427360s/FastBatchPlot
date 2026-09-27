using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.CadBridge
{
    /// <summary>
    /// 录入新图框时读取图框块自身的线框范围（模板本地右下角基准，不含文字），
    /// 登记为图框打印范围，并使导出的 .tk 块范围与块内单位一致。
    /// </summary>
    public interface ICadTemplateFrameHost
    {
        bool TryGetTemplateFrameRegion(PlotFrame frame, out TemplateRegion? region);
    }
}

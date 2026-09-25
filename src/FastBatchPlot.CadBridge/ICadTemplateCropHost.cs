using FastBatchPlot.Core.Common;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
namespace FastBatchPlot.CadBridge
{
    public interface ICadTemplateCropHost
    {
        Rect2D ResolveTemplatePrintBounds(PlotFrame frame,TemplateRegion region);
    }
}

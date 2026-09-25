using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.CadBridge
{
    public interface ICadStampHost
    {
        StampPlacement ResolveStampPlacement(PlotFrame frame,TemplateRegion region,int pixelWidth,int pixelHeight);
    }
}

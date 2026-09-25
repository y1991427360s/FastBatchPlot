using System.Collections.Generic;
using System.Linq;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        // PDF 工作流不附加签章，也不覆盖原图的图层打印状态。
        // 保留旧配置字段用于文件兼容，但每次预览、输出和保存前清除附加资源。
        private void CaptureStampAsset()
        {
            _config.Stamp=null; _config.RegistrationStamp=null;
            _config.StampPermit=null; _config.RegistrationStampPermit=null;
            _config.StampLibraryPath=""; _config.StampAssetId="";
            _config.RegistrationStampLibraryPath=""; _config.RegistrationStampAssetId="";
            _config.PrintPrimaryStamp=false; _config.PrintRegistrationStamp=false;
            _config.PrintSignatures=true; _config.PrintStamps=true;
            _config.SignatureLayerName="MS_Sign"; _config.StampLayerName="MS_Stamp";
            chkPrintSignatures.Checked=true; chkPrintStamps.Checked=true;
        }

        private List<PlotFrame> PrepareStampFrames(IEnumerable<PlotFrame> frames)
        {
            var result=frames.Select(f=>new BatchPage(f,"").Frame).ToList();
            foreach(var frame in result)
            {
                frame.StampPlacement=null; frame.StampRegion=null;
                frame.RegistrationStampPlacement=null; frame.RegistrationStampRegion=null;
            }
            return result;
        }
    }
}

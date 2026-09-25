using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private PageMargins _pageMargins=new PageMargins();
        private Button btnPageMargins=null!;
        private void EditPageMargins()
        {
            if(_isPlotting)return;dgvDrawings.EndEdit();
            using(var dialog=new PageMarginsForm(_pageMargins,(double)numMargin.Value,DisplayOrder().Where(f=>f.IsSelected).ToList(),_config.PrintOuterBorderLine,_config.OuterBorderInsetMm))
                if(dialog.ShowDialog(this)==DialogResult.OK){_pageMargins=dialog.Value;_config.PrintOuterBorderLine=dialog.PrintOuterBorderLine;_config.OuterBorderInsetMm=dialog.OuterBorderInsetMm;UpdateMarginMode();}
        }
        private void UpdateMarginMode()
        {
            numMargin.Enabled=!_isPlotting&&_pageMargins.Mode==MarginMode.Uniform;
            RefreshPaperPlans();
            btnPageMargins.Text=!_config.PrintOuterBorderLine?"裁边 ✓":_pageMargins.Mode==MarginMode.Uniform?"四边…":"四边 ✓";
        }
    }
}

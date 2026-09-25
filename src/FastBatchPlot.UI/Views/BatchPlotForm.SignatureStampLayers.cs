using System.Windows.Forms;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private void ConfigureSignatureStampLayers()
        {
            if (_isPlotting) return;
            using (var form = new SignatureStampLayersForm(_config.SignatureLayerName, _config.StampLayerName))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                _config.SignatureLayerName = form.SignatureLayerName;
                _config.StampLayerName = form.StampLayerName;
                lblStatus.Text = "签章层映射已应用；点击保存默认设置可跨会话使用。";
            }
        }
    }
}

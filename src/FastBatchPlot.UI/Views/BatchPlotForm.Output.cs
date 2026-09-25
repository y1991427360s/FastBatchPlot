using System;
using System.IO;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private void UpdateOutputMode()
        {
            RestoreOutputControlStates();
            lblStatus.Text = cboOutputMode.SelectedIndex == 7 ? "实体打印机会按所选份数提交任务；请核对设备和纸张。"
                : "请手动选择支持 " + cboOutputMode.SelectedItem + " 的打印设备；格式不会自动转换驱动内容。";
        }
        private void RestoreOutputControlStates()
        {
            bool printer = cboOutputMode.SelectedIndex == 7;
            bool pdf = cboOutputMode.SelectedIndex == 0;
            numCopies.Enabled = printer;
            if (!printer) numCopies.Value = 1;
            chkMergePdf.Enabled = pdf;
            btnPdfParameters.Enabled = pdf;
            txtMergedFileName.Enabled = pdf;
            txtOutputFolder.Enabled = btnBrowseOutput.Enabled = txtNamingTemplate.Enabled = !printer;
            btnStartPlot.Text = printer ? "提交到打印机" : "开始批量输出";
        }
        private static bool DispatchPage(ICadPlotter plotter, PlotFrame frame, PlotConfig config, string path, out string error)
        {
            if (config.SendToPrinter)
            {
                if (!(plotter is ICadPrinter printer)) { error = "当前宿主不支持实体打印机提交。"; return false; }
                return printer.PrintFrameToDevice(frame, config, out error);
            }
            if (!config.OverwriteExisting && File.Exists(path)) throw new IOException("目标文件在任务开始后已存在，拒绝覆盖。");
            bool ok = plotter.PlotFrameToFile(frame, config, path, out error);
            if (ok && (!File.Exists(path) || new FileInfo(path).Length == 0))
            { error = "打印引擎未生成有效文件。"; return false; }
            return ok;
        }
    }
}

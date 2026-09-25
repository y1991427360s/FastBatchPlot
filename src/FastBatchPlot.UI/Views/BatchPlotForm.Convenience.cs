using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.UI.Views
{
    /// <summary>对照原版的日常便利功能：纸张统计、打开输出目录、打开单页成果。</summary>
    public partial class BatchPlotForm
    {
        private CheckBox chkOpenFolderWhenDone = null!;

        private void AddConvenienceMenuItems()
        {
            ctxMenu.Items.Insert(0, new ToolStripMenuItem("📊 纸张统计（勾选图纸）", null, (s, e) => ShowPaperStatistics()));
            ctxMenu.Items.Insert(1, new ToolStripMenuItem("📂 打开输出目录", null, (s, e) => OpenOutputFolder()));
            ctxMenu.Items.Insert(2, new ToolStripMenuItem("📄 打开选中行的输出文件", null, (s, e) => OpenSelectedOutputFile()));
            ctxMenu.Items.Insert(3, new ToolStripSeparator());
        }

        internal static string DescribePaperStatistics(System.Collections.Generic.IEnumerable<PlotFrame> frames)
        {
            var list = frames.ToList();
            if (list.Count == 0) return "没有勾选的图纸。";
            var byPaper = CatalogExporter.GetPaperStatistics(list);
            var text = new System.Text.StringBuilder();
            text.AppendLine($"共 {list.Count} 张：");
            foreach (var pair in byPaper.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                text.AppendLine($"  {pair.Key}：{pair.Value} 张");
            // 折合 A1：按纸面面积换算，便于晒图计价。
            double a1 = list.Sum(f => f.DetectedPaper.WidthMm * f.DetectedPaper.HeightMm) / (841.0 * 594.0);
            text.AppendLine($"折合 A1：{a1:0.##} 张（按纸面面积）");
            return text.ToString().TrimEnd();
        }

        private void ShowPaperStatistics()
        {
            string text = DescribePaperStatistics(_frames.Where(f => f.IsSelected));
            if (MessageBox.Show(this, text + "\n\n是否复制到剪贴板？", "纸张统计", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                try { Clipboard.SetText(text); lblStatus.Text = "纸张统计已复制到剪贴板。"; }
                catch (Exception ex) { lblStatus.Text = "复制失败：" + ex.Message; }
            }
        }

        private void OpenLogFolder()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FastBatchPlot", "logs");
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex) { lblStatus.Text = "无法打开日志目录：" + ex.Message; }
        }

        private void OpenOutputFolder()
        {
            try
            {
                string folder = Path.GetFullPath(txtOutputFolder.Text);
                if (!Directory.Exists(folder)) { lblStatus.Text = "输出目录尚不存在：" + folder; return; }
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex) { lblStatus.Text = "无法打开输出目录：" + ex.Message; }
        }

        private void OpenSelectedOutputFile()
        {
            if (dgvDrawings.SelectedRows.Count == 0 || !(dgvDrawings.SelectedRows[0].Tag is PlotFrame frame)) return;
            try
            {
                string path = Path.Combine(Path.GetFullPath(txtOutputFolder.Text), frame.CustomOutputFileName + PlotFileFormats.Extension(
                    cboOutputMode.SelectedIndex == 7 ? PlotExportFormat.PDF : (PlotExportFormat)cboOutputMode.SelectedIndex));
                if (!File.Exists(path)) { lblStatus.Text = "尚未生成该图纸的输出文件：" + path; return; }
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { lblStatus.Text = "无法打开文件：" + ex.Message; }
        }

        /// <summary>批次结束后按用户选择打开合并 PDF 或输出目录。</summary>
        private void OpenResultAfterRun(string mergedTarget, bool merged, int successCount)
        {
            if (chkOpenFolderWhenDone == null || !chkOpenFolderWhenDone.Checked || successCount == 0 || _settingsPath == null) return;
            try
            {
                if (merged && File.Exists(mergedTarget))
                    Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "/select,\"" + mergedTarget + "\"", UseShellExecute = true });
                else OpenOutputFolder();
            }
            catch (Exception ex) { lblStatus.Text = "无法打开输出目录：" + ex.Message; }
        }
    }
}

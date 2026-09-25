using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private void EditCustomPaper()
        {
            if (_isPlotting) return;
            dgvDrawings.EndEdit();
            var frames = dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Tag).OfType<PlotFrame>().ToList();
            if (frames.Count == 0) { lblStatus.Text = "请先选中需要修改纸张尺寸的行。"; return; }
            using (var dialog = new CustomPaperForm(frames, PaperPreviewConfig()))
                if (dialog.ShowDialog(this) == DialogResult.OK) ApplyCustomPaper(dialog.WidthMm, dialog.HeightMm, dialog.FitContent);
        }
        private void EditAllPaperDimensions()
        {
            if(_isPlotting||_frames.Count==0)return;
            dgvDrawings.EndEdit();
            var frames=DisplayOrder();
            using(var dialog=new CustomPaperForm(frames,PaperPreviewConfig()))
            {
                dialog.Text="全部图纸：基础尺寸与比例（含未勾选行）";
                if(dialog.ShowDialog(this)==DialogResult.OK)ApplyPaperDimensions(frames,dialog.WidthMm,dialog.HeightMm,dialog.FitContent);
            }
        }
        private PlotConfig PaperPreviewConfig() => new PlotConfig { MarginMm = (double)numMargin.Value, Margins = _pageMargins.Copy(), AutoOrientation = _config.AutoOrientation,PrintOuterBorderLine=_config.PrintOuterBorderLine,OuterBorderInsetMm=_config.OuterBorderInsetMm };
        private void ApplyCustomPaper(double width, double height, bool fit)
        {
            if (_isPlotting) throw new InvalidOperationException("任务运行时不能修改纸张。");
            var frames = dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Tag).OfType<PlotFrame>().ToList();
            ApplyPaperDimensions(frames,width,height,fit);
        }
        private void ApplyPaperDimensions(List<PlotFrame> frames,double width,double height,bool fit)
        {
            if(_isPlotting)throw new InvalidOperationException("任务运行时不能修改纸张。");
            var candidates = frames.Select(frame => CustomPaperEdit.Preview(frame, width, height, fit)).ToList();
            var config = PaperPreviewConfig();
            foreach (var candidate in candidates) PlotPlanBuilder.Create(candidate, config);
            // 全批验证后应用，不能前几张已改、后几张才因范围无效而失败。
            for (int i = 0; i < frames.Count; i++) {
                frames[i].DetectedPaper = candidates[i].DetectedPaper;
                frames[i].IsLandscape = candidates[i].IsLandscape;
                frames[i].CalculatedScale = candidates[i].CalculatedScale;
            }
            RefreshGrid();
            lblStatus.Text = "已修改 " + frames.Count + " 张纸张设置；打印时仍需驱动精确匹配。";
        }
    }
}

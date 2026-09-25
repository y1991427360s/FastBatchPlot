using System;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Tasks;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private void PreviewSelectedFrame()
        {
            if (_isPlotting) return;
            try { ExecuteFramePreview(); }
            catch (Exception ex)
            {
                lblStatus.Text = "预览未完成：" + ex.Message;
                MessageBox.Show(this, ex.Message, "打印预览", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private PlotPreviewOutcome ExecuteFramePreview()
        {
            if (_isPlotting) throw new InvalidOperationException("任务正在执行，请结束后再预览。");
            if (!CadHostProvider.IsInitialized || !(CadHostProvider.Plotter is ICadPlotPreview preview))
                throw new InvalidOperationException("当前 CAD 宿主不支持打印预览。");
            dgvDrawings.EndEdit();
            var selected = dgvDrawings.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => row.Tag).OfType<PlotFrame>().ToList();
            if (selected.Count != 1) throw new InvalidOperationException("请选择且仅选择一行图纸进行预览。");
            CapturePlotSettings();
            // 与正式任务相同的快照，预览宿主不能修改列表及窗口配置。
            var run = new BatchPlotRun(new[] { new BatchPage(PrepareStampFrames(selected)[0], "") }, _config);
            var frame = run.Pages[0].Frame;
            var config = run.Config;
            EnsureFrameContextsAccessible(new[] { frame });
            PlotPlanBuilder.Create(frame, config);
            bool wasVisible = Visible;
            SetPlottingState(true);
            try
            {
                Hide();
                var outcome = preview.PreviewFrame(frame, config, out var error);
                if (outcome == PlotPreviewOutcome.Failed)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "CAD 未能完成预览。" : error);
                if (!Enum.IsDefined(typeof(PlotPreviewOutcome), outcome))
                    throw new InvalidOperationException("CAD 返回了未知预览状态。");
                lblStatus.Text = outcome == PlotPreviewOutcome.PrintRequested
                    ? "预览已关闭；请使用“开始批量打印”正式提交任务。"
                    : outcome == PlotPreviewOutcome.Cancelled ? "预览已取消。" : "预览已关闭。";
                if (!string.IsNullOrWhiteSpace(error)) lblStatus.Text += " " + error;
                return outcome;
            }
            finally
            {
                SetPlottingState(false);
                if (wasVisible) { Show(); Activate(); }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using FastBatchPlot.Core.Detection;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm
    {
        private string _lastDetectionReport = "尚未搜索或框选图框。";
        private Button btnDetectionReport = null!;

        private void RecordDetectionReport(FrameSelectionResult result, int candidates, string operation, IList<string> warnings)
        {
            var text = new StringBuilder();
            text.AppendLine(operation + " / " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            text.AppendLine($"候选 {candidates} 个，保留 {result.Frames.Count} 个，过滤 {result.Report.TotalCount} 个，采集警告 {warnings.Count} 个。");
            text.AppendLine($"块名筛选：{_specifiedBlockName}；图层筛选：{_specifiedLayerName}；面积阈值：{_minimumAreaPercent}%；识别比例：{numDetectionScale.Value}（0 自动）");
            text.AppendLine("本报告是本次识别快照。仅包含宿主交给识别器的候选；不涵盖宿主未采集的实体。框选接口不提供逐实体采集警告。");
            text.AppendLine("自动纸张匹配失败时可指定识别比例；尺寸/几何不满足时可使用两点添加图纸。");
            if (warnings.Count > 0)
            {
                text.AppendLine("采集警告：");
                for (int i=0; i<Math.Min(warnings.Count,DetectionReport.DetailLimit); i++) text.AppendLine(warnings[i]);
                if (warnings.Count > DetectionReport.DetailLimit) text.AppendLine("采集警告仅展示前 " + DetectionReport.DetailLimit + " 条。");
            }
            text.AppendLine(result.Report.ToText());
            _lastDetectionReport = text.ToString();
        }

        private Form CreateDetectionReportDialog()
        {
            var dialog = new Form { Text="识别报告", Size=new Size(860,580), MinimumSize=new Size(500,320),
                StartPosition=FormStartPosition.CenterParent, Font=Font, ShowInTaskbar=false };
            dialog.Controls.Add(new TextBox { Name="reportText", Multiline=true, ReadOnly=true, Dock=DockStyle.Fill,
                ScrollBars=ScrollBars.Both, WordWrap=false, Text=_lastDetectionReport, BackColor=Color.White });
            return dialog;
        }

        private void ShowDetectionReport()
        {
            using (var dialog = CreateDetectionReportDialog()) dialog.ShowDialog(this);
        }
    }
}

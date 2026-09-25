using System;
using System.Collections.Generic;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckPreviewUi()
    {
        var oldHost = CadHostProvider.Host;
        var oldPlotter = CadHostProvider.Plotter;
        var host = new LayoutHost();
            var preview = new OfflinePreviewPlotter();
        CadHostProvider.Host = host;
        CadHostProvider.Plotter = preview;
        try
        {
            using var form = new BatchPlotForm(null);
            preview.Form = form;
            form.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            form.Location = new System.Drawing.Point(-32000, -32000);
            form.ShowInTaskbar = false;
            form.Show();
            var frames = Field<List<PlotFrame>>(form, "_frames");
            var grid = Field<System.Windows.Forms.DataGridView>(form, "dgvDrawings");
            var devices = Field<System.Windows.Forms.ComboBox>(form, "cboPlotters");
            devices.Items.Add("offline.pc3");
            devices.SelectedIndex = devices.Items.Count - 1;
            frames.Add(new PlotFrame
            {
                OrderIndex = 1, MinX = 0, MinY = 0, MaxX = 84100, MaxY = 59400,
                SourceDocumentId = "layout-document", SourceLayoutId = "space-1"
            });
            Call(form, "RefreshGrid");
            grid.ClearSelection();
            grid.Rows[0].Selected = true;
            Field<CheckBox>(form, "chkPrintSignatures").Checked = false;
            Field<CheckBox>(form, "chkPrintStamps").Checked = false;
            Field<PlotConfig>(form, "_config").StampLayerName = "专用印章";
            bool before = form.Visible;
            var result = (PlotPreviewOutcome)Call(form, "ExecuteFramePreview")!;
            Check(before && form.Visible && result == PlotPreviewOutcome.Closed,
                "预览返回后恢复主窗口和关闭状态");
            Check(preview.Calls == 1 && preview.HiddenDuringCall && preview.LastConfig.Copies == 1,
                "预览使用任务快照并在隐藏主窗口期间只调用预览接口");
            Check(preview.LastConfig.PrintSignatures && preview.LastConfig.PrintStamps &&
                preview.LastConfig.Stamp == null && preview.LastConfig.StampAssetId == "", "预览清除旧附加印章且不隐藏原图签章层，与正式任务共用配置入口");
        }
        finally
        {
            CadHostProvider.Host = oldHost;
            CadHostProvider.Plotter = oldPlotter;
        }
    }

    private sealed class OfflinePreviewPlotter : ICadPlotter, ICadPlotPreview
    {
        public int Calls { get; private set; }
        public bool HiddenDuringCall { get; private set; }
        public System.Windows.Forms.Form? Form { get; set; }
        public PlotConfig LastConfig { get; private set; } = new PlotConfig();
        public List<string> GetAvailablePlotters() => new();
        public List<string> GetAvailablePlotStyles() => new() { "monochrome.ctb" };
        public List<string> GetPaperSizesForPlotter(string device) => new();
        public bool PlotFrameToFile(PlotFrame frame, PlotConfig config, string path, out string error)
        { error = "预览测试不应调用文件接口"; return false; }
        public bool PrintFrameToDevice(PlotFrame frame, PlotConfig config, out string error)
        { error = "预览测试不应调用设备接口"; return false; }
        public PlotPreviewOutcome PreviewFrame(PlotFrame frame, PlotConfig config, out string error)
        {
            Calls++;
            HiddenDuringCall = Form == null || !Form.Visible;
            LastConfig = config;
            error = string.Empty;
            return PlotPreviewOutcome.Closed;
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;

namespace FastBatchPlot.UI.Views
{
    /// <summary>只编辑副本，应用时才替换当前任务的 PDF 参数。</summary>
    public sealed class PdfParametersForm : Form
    {
        private readonly ComboBox vector = Choice();
        private readonly ComboBox raster = Choice();
        private readonly ComboBox textGeometry = Choice();
        private readonly ComboBox layers = Choice();
        private readonly ComboBox mergeLines = Choice();
        private readonly Label status = new Label { Dock = DockStyle.Fill };
        private readonly Button apply = new Button { Text = "应用", AutoSize = true, DialogResult = DialogResult.OK };
        private readonly bool zwcad;
        private readonly bool acad;

        public PdfOutputOptions Value => new PdfOutputOptions
        {
            VectorResolutionDpi = Dpi(vector), RasterResolutionDpi = Dpi(raster),
            TextToGeometry = Mode(textGeometry), IncludeLayers = Mode(layers), MergeLines = Mode(mergeLines)
        };

        public PdfParametersForm(PdfOutputOptions initial, string platform)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            zwcad = platform.IndexOf("ZWCAD", StringComparison.OrdinalIgnoreCase) >= 0 || platform.Contains("中望");
            acad = !zwcad && platform.IndexOf("AutoCAD", StringComparison.OrdinalIgnoreCase) >= 0;
            Text = "PDF 输出参数"; Size = new Size(620, 450); MinimumSize = new Size(580, 450);
            AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 8 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
            for (int i = 0; i < 5; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var note = new Label { Dock = DockStyle.Fill, Text = "跟随驱动：保留所选 PDF 预设的原始设置。\n参数随默认设置保存，并记录到 PDF 任务历史；恢复任务沿用原参数。" };
            root.Controls.Add(note, 0, 0); root.SetColumnSpan(note, 2);
            AddDpi(vector, new[] { 300, 600, 1200, 2400 }, initial.VectorResolutionDpi);
            AddDpi(raster, new[] { 150, 300, 400, 600, 1200 }, initial.RasterResolutionDpi);
            AddMode(textGeometry, initial.TextToGeometry); AddMode(layers, initial.IncludeLayers); AddMode(mergeLines, initial.MergeLines);
            AddRow(root, "矢量分辨率", vector, 1); AddRow(root, "光栅分辨率", raster, 2);
            AddRow(root, "文字转图形", textGeometry, 3); AddRow(root, "输出 PDF 图层", layers, 4); AddRow(root, "直线合并", mergeLines, 5);
            root.Controls.Add(status, 0, 6); root.SetColumnSpan(status, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            var reset = new Button { Text = "全部跟随驱动", AutoSize = true };
            reset.Click += (s, e) => { foreach (var control in new[] { vector, raster, textGeometry, layers, mergeLines }) control.SelectedIndex = 0; };
            buttons.Controls.Add(cancel); buttons.Controls.Add(apply); buttons.Controls.Add(reset);
            root.Controls.Add(buttons, 0, 7); root.SetColumnSpan(buttons, 2); Controls.Add(root);
            AcceptButton = apply; CancelButton = cancel;
            foreach (var control in new[] { vector, raster, textGeometry, layers, mergeLines }) control.SelectedIndexChanged += (s, e) => UpdateSupport();
            UpdateSupport();
        }

        private void UpdateSupport()
        {
            vector.Enabled = textGeometry.Enabled = acad || zwcad;
            raster.Enabled = layers.Enabled = acad || zwcad;
            mergeLines.Enabled = acad || zwcad;
            bool unsupported = (!acad && !zwcad && mergeLines.SelectedIndex != 0) || (!acad && !zwcad && (raster.SelectedIndex != 0 || layers.SelectedIndex != 0))
                || (!acad && !zwcad && (vector.SelectedIndex != 0 || textGeometry.SelectedIndex != 0));
            apply.Enabled = !unsupported;
            status.Text = unsupported ? "当前宿主不支持已保存的部分参数。点击“全部跟随驱动”后可应用。"
                : acad ? "AutoCAD：支持分辨率、文字、图层和直线合并。\n直线合并关闭时为线条覆盖。"
                : zwcad ? "中望 CAD：光栅分辨率不能高于矢量分辨率；文字设置仅控制 TrueType。\n直线合并关闭时为线条覆盖；跟随项在打印前读取配置校验。"
                : "当前未连接受支持的 CAD 宿主，所有参数沿用驱动。";
            try { Value.Validate(); }
            catch (ArgumentException ex) { apply.Enabled = false; status.Text = ex.Message; }
            if(zwcad && Value.RasterResolutionDpi.HasValue && Value.VectorResolutionDpi.HasValue &&
                Value.RasterResolutionDpi.Value>Value.VectorResolutionDpi.Value)
            { apply.Enabled=false; status.Text="光栅分辨率不能高于矢量分辨率，请调整后应用。"; }
        }

        private static ComboBox Choice() => new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private static void AddRow(TableLayoutPanel root, string label, Control control, int row)
        { root.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row); root.Controls.Add(control, 1, row); }
        private static void AddDpi(ComboBox control, int[] choices, int? current)
        {
            control.Items.Add("跟随驱动"); foreach (var value in choices) control.Items.Add(value + " DPI");
            control.SelectedIndex = 0;
            if (current.HasValue) { string text = current.Value + " DPI"; int index = control.Items.IndexOf(text); if (index < 0) index = control.Items.Add(text); control.SelectedIndex = index; }
        }
        private static void AddMode(ComboBox control, bool? current)
        { control.Items.AddRange(new object[] { "跟随驱动", "开启", "关闭" }); control.SelectedIndex = !current.HasValue ? 0 : current.Value ? 1 : 2; }
        private static int? Dpi(ComboBox control) => control.SelectedIndex == 0 ? (int?)null : int.Parse(control.SelectedItem!.ToString()!.Split(' ')[0]);
        private static bool? Mode(ComboBox control) => control.SelectedIndex == 0 ? (bool?)null : control.SelectedIndex == 1;
    }
}

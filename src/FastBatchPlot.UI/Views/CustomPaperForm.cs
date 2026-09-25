using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.UI.Views
{
    public sealed class CustomPaperForm : Form
    {
        private readonly NumericUpDown width = Dimension();
        private readonly NumericUpDown height = Dimension();
        private readonly ComboBox scaleMode = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DataGridView preview = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        private readonly Label status = new Label { Dock = DockStyle.Fill };
        private readonly Button apply = new Button { Text = "应用到所选行", AutoSize = true, DialogResult = DialogResult.OK };
        private readonly IReadOnlyList<PlotFrame> frames;
        private readonly PlotConfig config;
        public double WidthMm => (double)width.Value;
        public double HeightMm => (double)height.Value;
        public bool FitContent => scaleMode.SelectedIndex == 1;

        public CustomPaperForm(IReadOnlyList<PlotFrame> frames, PlotConfig config)
        {
            if (frames == null || frames.Count == 0) throw new ArgumentException("请先选择至少一行图纸。");
            this.frames = frames; this.config = config ?? throw new ArgumentNullException(nameof(config));
            Text = "自定义纸张尺寸与比例"; Size = new Size(820, 500); MinimumSize = new Size(740, 460);
            Font = new Font("Microsoft YaHei UI", 9); AutoScaleMode = AutoScaleMode.Dpi; StartPosition = FormStartPosition.CenterParent;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
            foreach (float value in new[] { 40f, 36f, 58f }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, value));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var dimensions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            for (int i = 0; i < 2; i++) { dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95)); dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); }
            dimensions.Controls.Add(new Label { Text = "纸张宽 mm", Dock = DockStyle.Fill }, 0, 0); dimensions.Controls.Add(width, 1, 0);
            dimensions.Controls.Add(new Label { Text = "纸张高 mm", Dock = DockStyle.Fill }, 2, 0); dimensions.Controls.Add(height, 3, 0); root.Controls.Add(dimensions, 0, 0);
            scaleMode.Items.AddRange(new object[] { "保持各页原打印比例", "按各页打印范围适配纸张（等比，不拉伸）" }); root.Controls.Add(scaleMode, 0, 1);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "宽高决定最终纸面方向。尺寸为添加留白前的基础尺寸，下面显示留白后的纸张与实际比例。\n适配会修改各页比例；驱动能否接受该尺寸仍须打印时核验，不会用近似纸张代替。" }, 0, 2);
            foreach (string title in new[] { "序号 / 图号", "原比例", "实际纸张 mm", "实际比例", "结果" }) preview.Columns.Add(title, title);
            root.Controls.Add(preview, 0, 3); root.Controls.Add(status, 0, 4);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
            buttons.Controls.Add(cancel); buttons.Controls.Add(apply); root.Controls.Add(buttons, 0, 5); Controls.Add(root); CancelButton = cancel;
            var paper = frames[0].DetectedPaper;
            width.Value = Clamp(frames[0].IsLandscape ? paper.LongerEdgeMm : paper.ShorterEdgeMm);
            height.Value = Clamp(frames[0].IsLandscape ? paper.ShorterEdgeMm : paper.LongerEdgeMm);
            width.ValueChanged += (s, e) => RefreshPreview(); height.ValueChanged += (s, e) => RefreshPreview();
            scaleMode.SelectedIndexChanged += (s, e) => RefreshPreview(); scaleMode.SelectedIndex = 0;
        }
        private static decimal Clamp(double value) => double.IsNaN(value) || double.IsInfinity(value) ? 1 : (decimal)Math.Max(1, Math.Min(100000, value));
        private static NumericUpDown Dimension() => new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 100000, DecimalPlaces = 3, Increment = 1 };
        private void RefreshPreview()
        {
            preview.Rows.Clear(); int invalid = 0;
            foreach (var frame in frames)
            {
                string title = frame.OrderIndex + " / " + frame.TitleInfo.DrawingNo;
                try {
                    var candidate = CustomPaperEdit.Preview(frame, WidthMm, HeightMm, FitContent);
                    var plan = PlotPlanBuilder.Create(candidate, config);
                    preview.Rows.Add(title, "1:" + frame.CalculatedScale.ToString("0.########"),
                        $"{plan.PaperWidthMm:0.###} × {plan.PaperHeightMm:0.###}", "1:" + plan.ScaleDenominator.ToString("0.########"), "待驱动核验");
                }
                catch (Exception ex) { int index = preview.Rows.Add(title, "", "", "", ex.Message); preview.Rows[index].DefaultCellStyle.BackColor = Color.MistyRose; invalid++; }
            }
            apply.Enabled = invalid == 0;
            status.Text = invalid == 0 ? "已预检 " + frames.Count + " 张；仅修改列表设置，原 DWG 不变。" : invalid + " 张无法完整容纳；请增大纸张或选择等比适配。";
        }
    }
}

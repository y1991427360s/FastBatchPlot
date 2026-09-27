using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.UI.Views
{
    /// <summary>选择图框对应纸张：标准/加长图幅或自定义宽高（毫米），方向与图框一致。</summary>
    public sealed class PaperChoiceForm : Form
    {
        private const string Custom = "自定义";
        private readonly ComboBox papers = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly NumericUpDown width = new NumericUpDown { Minimum = 1, Maximum = 100000, DecimalPlaces = 2, Width = 95 };
        private readonly NumericUpDown height = new NumericUpDown { Minimum = 1, Maximum = 100000, DecimalPlaces = 2, Width = 95 };
        private readonly CheckBox portrait = new CheckBox { Text = "竖向", AutoSize = true };
        private bool updating;
        public double PaperWidth => (double)width.Value;
        public double PaperHeight => (double)height.Value;

        public PaperChoiceForm(double currentWidth, double currentHeight)
        {
            Text = "选择对应纸张"; Font = new Font("Microsoft YaHei UI", 9); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(420, 170);
            papers.Items.AddRange(PaperSize.StandardSizes.Select(p => (object)p.Name).ToArray());
            papers.Items.Add(Custom);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 14, 6), ColumnCount = 4, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(Caption("纸张"), 0, 0); root.Controls.Add(papers, 1, 0); root.SetColumnSpan(papers, 2); root.Controls.Add(portrait, 3, 0);
            root.Controls.Add(Caption("宽 (mm)"), 0, 1); root.Controls.Add(width, 1, 1);
            root.Controls.Add(Caption("高 (mm)"), 2, 1); root.Controls.Add(height, 3, 1);
            var ok = FrameLibraryStyle.CreateButton("确  定", FrameLibraryStyle.OkIcon);
            var cancel = FrameLibraryStyle.CreateButton("取  消", FrameLibraryStyle.CancelIcon);
            ok.DialogResult = DialogResult.OK; cancel.DialogResult = DialogResult.Cancel;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 0) };
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
            root.Controls.Add(buttons, 0, 2); root.SetColumnSpan(buttons, 4);
            Controls.Add(root);
            AcceptButton = ok; CancelButton = cancel;
            papers.SelectedIndexChanged += (s, e) => ApplyStandard();
            portrait.CheckedChanged += (s, e) => ApplyOrientation();
            width.ValueChanged += (s, e) => SyncFromSize();
            height.ValueChanged += (s, e) => SyncFromSize();
            SetSize(currentWidth > 0 && currentHeight > 0 ? currentWidth : 594, currentWidth > 0 && currentHeight > 0 ? currentHeight : 420);
        }

        private static Label Caption(string text) => new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };

        private void SetSize(double w, double h)
        {
            updating = true;
            try
            {
                width.Value = Clamp(w); height.Value = Clamp(h);
                portrait.Checked = h > w;
                var standard = FrameLibrary.StandardPaper(w, h);
                papers.SelectedItem = standard?.Name ?? Custom;
            }
            finally { updating = false; }
        }

        private decimal Clamp(double value) => Math.Min(width.Maximum, Math.Max(width.Minimum, (decimal)Math.Round(value, 2)));

        private void ApplyStandard()
        {
            if (updating || Equals(papers.SelectedItem, Custom)) return;
            var standard = PaperSize.StandardSizes.First(p => Equals(p.Name, papers.SelectedItem));
            SetSize(portrait.Checked ? standard.ShorterEdgeMm : standard.LongerEdgeMm, portrait.Checked ? standard.LongerEdgeMm : standard.ShorterEdgeMm);
        }

        private void ApplyOrientation()
        {
            if (updating) return;
            double w = PaperWidth, h = PaperHeight;
            if (portrait.Checked == h > w || w == h) return;
            SetSize(h, w);
        }

        private void SyncFromSize()
        {
            if (!updating) SetSize(PaperWidth, PaperHeight);
        }
    }
}

using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.UI.Views
{
    /// <summary>文件命名规则设置（仿原版）：A 图号、B 版次、C 图名、D 日期、E 信息1、F 信息2、T 图幅。</summary>
    public sealed class NamingRuleForm : Form
    {
        private readonly TextBox rule = new TextBox { Dock = DockStyle.Fill, TextAlign = HorizontalAlignment.Center };
        private readonly Label preview = new Label { Dock = DockStyle.Fill, ForeColor = FrameLibraryStyle.Accent, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        public string Rule => rule.Text.Trim();

        public NamingRuleForm(string current)
        {
            Text = "文件命名规则设置"; Font = new Font("Microsoft YaHei UI", 9); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(560, 230);
            var green = Color.FromArgb(0, 110, 50);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 14, 6), ColumnCount = 2, RowCount = 6 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (int height in new[] { 26, 28, 26, 30, 34 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            AddSpanning(root, new Label { Text = "文件命名中用以下字母表示各类信息：", ForeColor = green, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0);
            AddSpanning(root, new Label { Text = "A: 图号    B: 版次    C: 图名    D: 日期    E: 信息1    F: 信息2    T: 图幅", ForeColor = green, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1);
            AddSpanning(root, new Label { Text = "需要字母本身时连写两次（如 FF 表示 F）；留空表示使用主界面的通用命名。", ForeColor = SystemColors.GrayText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2);
            AddSpanning(root, preview, 3);
            root.Controls.Add(new Label { Text = "文件命名", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
            root.Controls.Add(rule, 1, 4);
            var ok = FrameLibraryStyle.CreateButton("确  定", FrameLibraryStyle.OkIcon);
            var cancel = FrameLibraryStyle.CreateButton("取  消", FrameLibraryStyle.CancelIcon);
            ok.DialogResult = DialogResult.OK; cancel.DialogResult = DialogResult.Cancel;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0) };
            buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
            AddSpanning(root, buttons, 5);
            Controls.Add(root);
            AcceptButton = ok; CancelButton = cancel;
            rule.TextChanged += (s, e) => UpdatePreview();
            rule.Text = current ?? "";
            UpdatePreview();
        }

        private void UpdatePreview() => preview.Text = "输出文件名示例：" + FrameLibrary.PreviewFileName(rule.Text);

        private static void AddSpanning(TableLayoutPanel panel, Control control, int row)
        {
            panel.Controls.Add(control, 0, row); panel.SetColumnSpan(control, 2);
        }
    }
}

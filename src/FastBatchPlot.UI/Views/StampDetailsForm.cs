using System;
using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Assets;

namespace FastBatchPlot.UI.Views
{
    public sealed class StampDetailsForm : Form
    {
        private readonly StampDetailsEditor detailsEditor;
        private readonly Label status = new Label { Dock = DockStyle.Fill, ForeColor = Color.Firebrick };
        public StampDetails? Details { get; private set; }
        public StampDetailsForm(StampDetails? details)
        {
            Text = "印章属性"; Size = new Size(670, 440); MinimumSize = new Size(590, 430);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            detailsEditor = new StampDetailsEditor(details) { Dock = DockStyle.Fill };
            root.Controls.Add(detailsEditor, 0, 0); root.Controls.Add(status, 0, 1);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            var confirm = new Button { Text = "应用到库草稿", AutoSize = true };
            confirm.Click += (s, e) => { try { Confirm(); } catch (Exception ex) { status.Text = ex.Message; } };
            footer.Controls.Add(cancel); footer.Controls.Add(confirm); root.Controls.Add(footer, 0, 2);
            Controls.Add(root); CancelButton = cancel; AcceptButton = confirm;
        }
        private void Confirm() { Details = detailsEditor.ReadDetails(); DialogResult = DialogResult.OK; Close(); }
    }
}

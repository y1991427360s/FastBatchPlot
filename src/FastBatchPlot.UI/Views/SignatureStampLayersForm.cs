using System;
using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.UI.Views
{
    public sealed class SignatureStampLayersForm : Form
    {
        private readonly TextBox signature = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox stamp = new TextBox { Dock = DockStyle.Fill };
        private readonly Label status = new Label { Dock = DockStyle.Fill, ForeColor = Color.Firebrick };
        public string SignatureLayerName { get; private set; }
        public string StampLayerName { get; private set; }

        public SignatureStampLayersForm(string signatureLayer, string stampLayer)
        {
            SignatureLayerName = signatureLayer; StampLayerName = stampLayer;
            Text = "签章图层映射"; ClientSize = new Size(590, 330); MinimumSize = new Size(540, 360);
            Font = new Font("Microsoft YaHei UI", 9); AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            signature.Text = signatureLayer; stamp.Text = stampLayer;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var help = new Label { Dock = DockStyle.Fill, Text = "主窗口取消“签名层输出”或“印章输出”后，仅禁止对应完整图层名出图。该层上的全部内容都会受影响；其他图层上的签章仍会输出。关闭印章输出也会停用所选附加印章。\n\n勾选时沿用图层原有状态；图层不存在时不作修改。MS_Stamp 是本程序默认值，请按实际图纸修改。" };
            layout.Controls.Add(help, 0, 0); layout.SetColumnSpan(help, 2);
            layout.Controls.Add(new Label { Text = "签名图层", AutoSize = true }, 0, 1); layout.Controls.Add(signature, 1, 1);
            layout.Controls.Add(new Label { Text = "印章图层", AutoSize = true }, 0, 2); layout.Controls.Add(stamp, 1, 2);
            layout.Controls.Add(status, 0, 3); layout.SetColumnSpan(status, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var apply = new Button { Text = "应用", AutoSize = true };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            apply.Click += (s, e) => { try { ReadMapping(); DialogResult = DialogResult.OK; Close(); } catch (ArgumentException ex) { status.Text = ex.Message; } };
            buttons.Controls.Add(apply); buttons.Controls.Add(cancel); layout.Controls.Add(buttons, 0, 4); layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout); AcceptButton = apply; CancelButton = cancel;
        }

        private void ReadMapping()
        {
            PlotLayerVisibility.Validate(signature.Text, stamp.Text);
            SignatureLayerName = signature.Text; StampLayerName = stamp.Text;
        }
    }
}

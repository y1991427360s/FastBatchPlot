using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;

namespace FastBatchPlot.UI.Views
{
    public sealed class BookmarkTemplateForm : Form
    {
        private readonly TextBox template = new TextBox { Dock = DockStyle.Fill, MaxLength = DrawingNameFormatter.MaximumBookmarkLength };
        private readonly ListBox preview = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true };
        private readonly Button apply = new Button { Text = "应用", AutoSize = true, DialogResult = DialogResult.OK };
        private readonly IReadOnlyList<PlotFrame> frames;
        public string Value => template.Text;

        public BookmarkTemplateForm(string initial, IReadOnlyList<PlotFrame> frames)
        {
            this.frames = frames;
            Text = "PDF 合并书签"; Size = new Size(780, 460); MinimumSize = new Size(680, 420);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.Controls.Add(template, 0, 0);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "可用：{Index:D2} 序号、{DwgNo} 图号、{DwgName} 图名、{ProjectName} 项目\n{PaperSize} 图幅、{Scale} 比例、{Date} 标题栏日期、{Rev} 版次；也支持中文占位符。\n保留中文及标点；留空使用默认模板。预览按当前勾选图纸的显示顺序。\n{DwgFileName} 来源文件、{Layout} 布局；历史任务使用原快照，旧记录缺来源时用 Drawing。" }, 0, 1);
            root.Controls.Add(preview, 0, 2); root.Controls.Add(status, 0, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            var reset = new Button { Text = "恢复默认", AutoSize = true };
            reset.Click += (s, e) => template.Text = DrawingNameFormatter.DefaultBookmarkTemplate;
            buttons.Controls.Add(cancel); buttons.Controls.Add(apply); buttons.Controls.Add(reset);
            root.Controls.Add(buttons, 0, 4); Controls.Add(root); AcceptButton = apply; CancelButton = cancel;
            template.TextChanged += (s, e) => RefreshPreview(); template.Text = initial; RefreshPreview();
        }

        private void RefreshPreview()
        {
            preview.Items.Clear();
            try
            {
                DrawingNameFormatter.ValidateBookmarkTemplate(Value);
                foreach (var frame in frames) preview.Items.Add(DrawingNameFormatter.FormatBookmark(Value, frame));
                status.Text = frames.Count == 0 ? "尚未勾选图纸；模板有效，选图后可预览。" : "已预览 " + frames.Count + " 个书签。";
                apply.Enabled = true;
            }
            catch (ArgumentException ex) { status.Text = ex.Message; apply.Enabled = false; }
        }
    }
}

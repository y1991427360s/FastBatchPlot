using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;

namespace FastBatchPlot.UI.Views
{
    /// <summary>
    /// 录入新图框 / 修改图框（仿原版）：基本信息、点取信息框范围、调整图框打印范围、从已录图框复制信息。
    /// 只修改草稿；由图框信息库在“确定”时统一保存。
    /// </summary>
    public sealed class FrameEntryForm : Form
    {
        public TitleBlockTemplate Template { get; }
        private readonly IReadOnlyList<TitleBlockTemplate> recorded;
        private readonly Func<string, PlotFrame?> findSample;
        private readonly Func<PlotFrame?> pickSample;
        private readonly Func<PlotFrame, TemplateRegion?> pickRegion;
        private PlotFrame? sample;
        private readonly Label paper = new Label { AutoSize = true, ForeColor = FrameLibraryStyle.Accent, Margin = new Padding(34, 0, 3, 10) };
        private readonly Label naming = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(34, 0, 3, 10), MaximumSize = new Size(250, 0) };
        private readonly ComboBox priority = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 64 };
        private readonly Label block = new Label { AutoSize = true, ForeColor = FrameLibraryStyle.Accent, Margin = new Padding(8, 12, 3, 3), MaximumSize = new Size(260, 0) };
        private readonly Label status = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(160, 60, 0), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Dictionary<TitleField, CheckBox> fieldChecks = new Dictionary<TitleField, CheckBox>();
        private readonly CheckBox printCheck = new CheckBox { AutoSize = true, AutoCheck = false, Margin = new Padding(3, 9, 3, 3) };
        private readonly ToolTip tips = new ToolTip();

        public FrameEntryForm(TitleBlockTemplate draft, IReadOnlyList<TitleBlockTemplate> recorded, PlotFrame? sample,
            Func<string, PlotFrame?> findSample, Func<PlotFrame?> pickSample, Func<PlotFrame, TemplateRegion?> pickRegion)
        {
            Template = draft ?? throw new ArgumentNullException(nameof(draft));
            this.recorded = recorded; this.sample = sample; this.findSample = findSample; this.pickSample = pickSample; this.pickRegion = pickRegion;
            Text = "录入新图框"; Font = new Font("Microsoft YaHei UI", 9); StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(620, 470);

            var basic = Group("1、基本信息（必录项）");
            var basicFlow = Flow();
            var paperButton = Action("选择对应纸张", ChoosePaper);
            var namingButton = Action("定义文件命名规则", EditNamingRule);
            var priorityRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(8, 4, 3, 3) };
            priorityRow.Controls.Add(new Label { Text = "排序优先级别：", AutoSize = true, Margin = new Padding(0, 6, 0, 0) });
            priorityRow.Controls.Add(priority);
            priority.Items.AddRange(Enumerable.Range(1, 9).Select(i => (object)i.ToString(CultureInfo.InvariantCulture)).ToArray());
            basicFlow.Controls.AddRange(new Control[] { paperButton, paper, namingButton, naming, priorityRow });
            basic.Controls.Add(basicFlow);
            tips.SetToolTip(paperButton, "标准及加长图幅或自定义宽高；出图时按登记纸张计算比例。");
            tips.SetToolTip(namingButton, "用字母 ABCDEFT 定义输出文件名，未定义时使用主界面的通用命名。");
            tips.SetToolTip(priority, "打印排序时先按优先级别（小的在前），同级再按图号。");

            var hint = new Label { Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, Padding = new Padding(4, 8, 4, 0),
                Text = "说明：\n· 点取范围时在 CAD 中框选该信息所在的格子，坐标以图框右下角为基准，同一图块的其他图纸自动套用。\n· 勾选框表示已录入，点击勾选框可清除该项。\n· 修改在图框信息库点“确定”后才保存。" };
            var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = new Padding(0) };
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 205)); left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.Controls.Add(basic, 0, 0); left.Controls.Add(hint, 0, 1);

            var info = Group("2、信息框录入（非必录）");
            var infoGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(4, 4, 4, 0), ForeColor = SystemColors.ControlText };
            infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var slot in FrameLibrary.Slots)
            {
                var field = slot.Field;
                var check = new CheckBox { AutoSize = true, AutoCheck = false, Margin = new Padding(3, 9, 3, 3) };
                check.Click += (s, e) => Run(() => { if (check.Checked) ClearField(field); else PickField(field); });
                tips.SetToolTip(check, "已录入时点击可清除" + slot.Caption + "范围");
                fieldChecks[field] = check;
                infoGrid.Controls.Add(Action("点取" + slot.Caption + "范围", () => PickField(field)));
                infoGrid.Controls.Add(check);
            }
            printCheck.Click += (s, e) => Run(() => { if (printCheck.Checked) ClearPrintRange(); else AdjustPrintRange(); });
            tips.SetToolTip(printCheck, "已登记打印范围时点击可清除，改为按整个图块打印");
            infoGrid.Controls.Add(Action("调整图框打印范围", AdjustPrintRange));
            infoGrid.Controls.Add(printCheck);
            infoGrid.Controls.Add(block); infoGrid.SetColumnSpan(block, 2);
            info.Controls.Add(infoGrid);

            var copy = FrameLibraryStyle.CreateButton("从已录图框复制信息", FrameLibraryStyle.CopyIcon);
            copy.Click += (s, e) => Run(CopyInformation);
            tips.SetToolTip(copy, "复制已录图框的文件命名规则和信息框位置（以右下角为基准）。");
            var ok = FrameLibraryStyle.CreateButton("确  定", FrameLibraryStyle.OkIcon);
            ok.Click += (s, e) => Run(Accept);
            var cancel = FrameLibraryStyle.CreateButton("取  消", FrameLibraryStyle.CancelIcon);
            cancel.DialogResult = DialogResult.Cancel;
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            foreach (var button in new[] { copy, ok, cancel }) { button.Anchor = AnchorStyles.None; buttons.Controls.Add(button); }

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(10, 8, 10, 4) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.Controls.Add(left, 0, 0); root.Controls.Add(info, 1, 0);
            root.Controls.Add(status, 0, 1); root.SetColumnSpan(status, 2);
            root.Controls.Add(buttons, 0, 2); root.SetColumnSpan(buttons, 2);
            Controls.Add(root);
            CancelButton = cancel;
            priority.Text = Template.Priority.ToString(CultureInfo.InvariantCulture);
            RefreshState();
        }

        private static GroupBox Group(string text) => new GroupBox { Text = text, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(190, 0, 0), Padding = new Padding(6) };

        private static FlowLayoutPanel Flow() => new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(4, 4, 4, 0), ForeColor = SystemColors.ControlText };

        private Button Action(string text, Action action)
        {
            var button = FrameLibraryStyle.CreateButton(text, FrameLibraryStyle.ActionIcon);
            button.ForeColor = SystemColors.ControlText; button.Margin = new Padding(8, 3, 3, 3);
            button.Click += (s, e) => Run(action);
            return button;
        }

        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { status.Text = ex.Message; }
        }

        internal void ShowNotice(string text) => status.Text = text;

        private void RefreshState()
        {
            paper.Text = FrameLibrary.PaperDescription(Template.PaperWidth, Template.PaperHeight);
            string rule = FrameLibrary.NamingRuleText(Template.NamingTemplate);
            naming.Text = rule.Length == 0 ? "未定义（使用主界面通用命名）" : rule;
            naming.ForeColor = rule.Length == 0 ? SystemColors.GrayText : FrameLibraryStyle.Accent;
            foreach (var pair in fieldChecks) pair.Value.Checked = FrameLibrary.HasField(Template, pair.Key);
            printCheck.Checked = Template.PrintRegion != null;
            block.Text = "图框对应的图块：" + Template.BlockName;
        }

        private void ChoosePaper()
        {
            using (var dialog = new PaperChoiceForm(Template.PaperWidth, Template.PaperHeight))
                if (dialog.ShowDialog(this) == DialogResult.OK) ApplyPaper(dialog.PaperWidth, dialog.PaperHeight);
        }

        private void ApplyPaper(double width, double height)
        {
            if (!(width > 0 && height > 0) || !TitleTemplateService.Finite(width) || !TitleTemplateService.Finite(height))
                throw new InvalidOperationException("纸张宽高必须为正数。");
            Template.PaperWidth = width; Template.PaperHeight = height;
            RefreshState(); status.Text = "对应纸张：" + FrameLibrary.PaperLabel(width, height) + "。";
        }

        private void EditNamingRule()
        {
            using (var dialog = new NamingRuleForm(FrameLibrary.NamingRuleText(Template.NamingTemplate)))
                if (dialog.ShowDialog(this) == DialogResult.OK) ApplyNamingRule(dialog.Rule);
        }

        private void ApplyNamingRule(string rule)
        {
            Template.NamingTemplate = FrameLibrary.NamingTemplateFromRule(rule);
            RefreshState();
            status.Text = Template.NamingTemplate == null ? "文件命名规则未定义，将使用主界面的通用命名。" : "文件命名示例：" + FrameLibrary.PreviewFileName(rule);
        }

        /// <summary>编辑已录图框时按块名自动找样本；当前空间没有时请用户在图中点选同名图框。</summary>
        private PlotFrame? EnsureSample()
        {
            string blockName = Template.BlockName.Trim();
            if (sample != null && string.Equals(sample.SourceBlockName.Trim(), blockName, StringComparison.OrdinalIgnoreCase)) return sample;
            sample = findSample(blockName);
            if (sample != null) return sample;
            status.Text = "当前 CAD 空间没有找到图块“" + blockName + "”，请在图中点选一个该图框。";
            var picked = FrameLibraryStyle.HiddenWhile(this, pickSample);
            if (picked == null) { status.Text = "已取消拾取图框。"; return null; }
            if (!string.Equals(picked.SourceBlockName.Trim(), blockName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("所选图块“" + picked.SourceBlockName + "”与本记录的图块“" + blockName + "”不一致，请选择“" + blockName + "”。");
            return sample = picked;
        }

        private TemplateRegion? PickFromSample()
        {
            var frame = EnsureSample();
            return frame == null ? null : FrameLibraryStyle.HiddenWhile(this, () => pickRegion(frame));
        }

        private void PickField(TitleField field)
        {
            var frame = EnsureSample();
            if (frame == null) return;
            var region = FrameLibraryStyle.HiddenWhile(this, () => pickRegion(frame));
            if (region == null) { status.Text = "已取消点取范围。"; return; }
            FrameLibrary.SetFieldRegion(Template, field, region);
            RefreshState(); status.Text = Caption(field) + "范围已录入。";
        }

        private void ClearField(TitleField field)
        {
            FrameLibrary.ClearField(Template, field);
            RefreshState(); status.Text = "已清除" + Caption(field) + "范围。";
        }

        private static string Caption(TitleField field) => FrameLibrary.Slots.First(s => s.Field == field).Caption;

        private void AdjustPrintRange()
        {
            using (var dialog = new TemplateCropForm(Template.PrintRegion, PickFromSample, Template.PrintScale) { Text = "调整图框打印范围" })
                if (dialog.ShowDialog(this) == DialogResult.OK) ApplyPrintRange(dialog.PrintRegion, dialog.PrintScale);
        }

        private void ApplyPrintRange(TemplateRegion? region, double scale)
        {
            if (region != null) TemplateCropGeometry.Validate(region);
            Template.PrintRegion = region == null ? null : TemplateCropGeometry.Copy(region); Template.PrintScale = scale;
            RefreshState(); status.Text = region == null ? "未登记打印范围，按整个图块打印。" : "图框打印范围已更新。";
        }

        private void ClearPrintRange()
        {
            Template.PrintRegion = null;
            RefreshState(); status.Text = "已清除打印范围，按整个图块打印。";
        }

        private void CopyInformation()
        {
            if (recorded.Count == 0) throw new InvalidOperationException("还没有其他已录图框可以复制。");
            using (var dialog = new Form { Text = "从已录图框复制信息", Font = Font, StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(380, 320),
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false })
            {
                var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
                list.Items.AddRange(recorded.Select(t => (object)(t.BlockName + "（" + FrameLibrary.PaperLabel(t.PaperWidth, t.PaperHeight) + "）  "
                    + (FrameLibrary.NamingRuleText(t.NamingTemplate) is var rule && rule.Length > 0 ? rule : "通用命名"))).ToArray());
                list.SelectedIndex = 0;
                var ok = FrameLibraryStyle.CreateButton("复  制", FrameLibraryStyle.OkIcon); ok.DialogResult = DialogResult.OK;
                var cancel = FrameLibraryStyle.CreateButton("取  消", FrameLibraryStyle.CancelIcon); cancel.DialogResult = DialogResult.Cancel;
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
                buttons.Controls.Add(cancel); buttons.Controls.Add(ok);
                list.DoubleClick += (s, e) => { if (list.SelectedIndex >= 0) dialog.DialogResult = DialogResult.OK; };
                dialog.Controls.Add(list); dialog.Controls.Add(buttons); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
                if (dialog.ShowDialog(this) == DialogResult.OK && list.SelectedIndex >= 0) CopyFrom(recorded[list.SelectedIndex]);
            }
        }

        private void CopyFrom(TitleBlockTemplate source)
        {
            FrameLibrary.CopyInformation(source, Template);
            RefreshState(); status.Text = "已从“" + source.BlockName + "”复制文件命名规则和信息框。";
        }

        private void Accept()
        {
            if (!int.TryParse(priority.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0 || value > 10000)
                throw new InvalidOperationException("排序优先级别须为 0～10000 的整数。");
            Template.Priority = value;
            TitleTemplateService.Validate(new TitleTemplateLibrary { Templates = recorded.Concat(new[] { Template }).ToList() });
            DialogResult = DialogResult.OK;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
    }
}

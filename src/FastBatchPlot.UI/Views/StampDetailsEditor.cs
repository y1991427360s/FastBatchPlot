using System;
using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Assets;

namespace FastBatchPlot.UI.Views
{
    /// <summary>编辑副本；独立印章期限与授权码期限分别检查。</summary>
    public sealed class StampDetailsEditor : UserControl
    {
        private readonly ComboBox kind = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox sizing = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown width = CreateDimension();
        private readonly NumericUpDown height = CreateDimension();
        private readonly CheckBox validEnabled = new CheckBox { Text = "限制印章本身的有效期（独立于授权期限）", AutoSize = true };
        private readonly DateTimePicker validDate = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", MaxDate = new DateTime(9998, 12, 30) };
        private readonly Label deadline = new Label { Dock = DockStyle.Fill };
        private readonly long initialTicks;
        private readonly DateTime initialDate;

        public StampDetailsEditor(StampDetails? details)
        {
            var value = details?.Copy() ?? new StampDetails(); value.Validate();
            Font = new Font("Microsoft YaHei UI", 9); MinimumSize = new Size(420, 268);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            kind.Items.AddRange(new object[] { "出图章", "注册章", "其他" }); kind.SelectedIndex = (int)value.Kind;
            sizing.Items.AddRange(new object[] { "适应模板区域（保持比例）", "固定纸面尺寸（毫米）" }); sizing.SelectedIndex = (int)value.Sizing;
            root.Controls.Add(new Label { Text = "印章类别", Dock = DockStyle.Fill }, 0, 0); root.Controls.Add(kind, 1, 0);
            root.Controls.Add(new Label { Text = "尺寸模式", Dock = DockStyle.Fill }, 0, 1); root.Controls.Add(sizing, 1, 1);
            root.Controls.Add(new Label { Text = "纸面宽 × 高（mm）", Dock = DockStyle.Fill }, 0, 2);
            var dimensions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            dimensions.Controls.Add(width); dimensions.Controls.Add(new Label { Text = "×", AutoSize = true, Margin = new Padding(1, 6, 1, 0) }); dimensions.Controls.Add(height);
            root.Controls.Add(dimensions, 1, 2);
            root.Controls.Add(validEnabled, 0, 3); root.SetColumnSpan(validEnabled, 2);
            root.Controls.Add(new Label { Text = "最后可用日期（本地）", Dock = DockStyle.Fill }, 0, 4); root.Controls.Add(validDate, 1, 4);
            root.Controls.Add(deadline, 0, 5); root.SetColumnSpan(deadline, 2);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "固定尺寸对应整张 PNG（含透明边缘）；指定宽高可能改变图片比例。\n超出模板区域会拒绝出图，不会自动缩小。" }, 0, 6); root.SetColumnSpan(root.GetControlFromPosition(0, 6)!, 2);
            Controls.Add(root);
            width.Value = value.Sizing == StampSizing.PhysicalSize ? (decimal)value.WidthMm : 40;
            height.Value = value.Sizing == StampSizing.PhysicalSize ? (decimal)value.HeightMm : 40;
            initialTicks = value.ValidUntilUtcTicks;
            DateTime date = initialTicks == 0 ? DateTime.Today.AddYears(1) : new DateTimeOffset(initialTicks, TimeSpan.Zero).ToLocalTime().AddTicks(-1).Date;
            initialDate = date < validDate.MinDate ? validDate.MinDate.Date : date > validDate.MaxDate ? validDate.MaxDate.Date : date;
            validDate.Value = initialDate; validEnabled.Checked = initialTicks != 0;
            sizing.SelectedIndexChanged += (s, e) => UpdateControls();
            validEnabled.CheckedChanged += (s, e) => UpdateControls(); validDate.ValueChanged += (s, e) => UpdateControls();
            UpdateControls();
        }
        private static NumericUpDown CreateDimension() => new NumericUpDown { Width = 102, Minimum = 0.1m, Maximum = 1000m, DecimalPlaces = 3, Increment = 0.1m };
        private void UpdateControls()
        {
            width.Enabled = height.Enabled = sizing.SelectedIndex == (int)StampSizing.PhysicalSize;
            validDate.Enabled = validEnabled.Checked;
            if (!validEnabled.Checked) { deadline.Text = "印章本身不设期限；若另设授权码，仍须满足授权期限。"; return; }
            try { deadline.Text = "印章截止（不含）：" + new DateTimeOffset(ReadExpiry(), TimeSpan.Zero).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"); }
            catch (Exception ex) { deadline.Text = ex.Message; }
        }
        private long ReadExpiry()
        {
            if (!validEnabled.Checked) return 0;
            // 对已有精确期限，未改日期时保留原时刻，避免悄悄延长有效期。
            if (initialTicks != 0 && validDate.Value.Date == initialDate) return initialTicks;
            var end = DateTime.SpecifyKind(validDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
            if (TimeZoneInfo.Local.IsInvalidTime(end) || TimeZoneInfo.Local.IsAmbiguousTime(end))
                throw new InvalidOperationException("所选日期的本地午夜存在时区歧义，请另选日期。");
            return new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end)).UtcDateTime.Ticks;
        }
        public StampDetails ReadDetails()
        {
            var details = new StampDetails
            {
                Kind = (StampKind)kind.SelectedIndex, Sizing = (StampSizing)sizing.SelectedIndex,
                WidthMm = sizing.SelectedIndex == (int)StampSizing.PhysicalSize ? (double)width.Value : 0,
                HeightMm = sizing.SelectedIndex == (int)StampSizing.PhysicalSize ? (double)height.Value : 0,
                ValidUntilUtcTicks = ReadExpiry()
            };
            details.Validate(); return details;
        }
    }
}

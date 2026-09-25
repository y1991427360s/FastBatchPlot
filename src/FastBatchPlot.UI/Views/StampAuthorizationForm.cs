using System;
using System.Drawing;
using System.Windows.Forms;
using FastBatchPlot.Core.Assets;

namespace FastBatchPlot.UI.Views
{
    /// <summary>授权码仅在本次对话框内使用，不写入配置、日志或印章库。</summary>
    public sealed class StampAuthorizationForm : Form
    {
        private enum AuthorizationMode { Use, Management, Edit }
        private readonly AuthorizationMode mode;
        private readonly StampAsset original;
        private readonly DateTime initialAuthorizationDate;
        private StampAsset? plain;
        private readonly TextBox code = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 128 };
        private readonly TextBox name = new TextBox { Dock = DockStyle.Fill, MaxLength = 128 };
        private readonly TextBox newCode = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 128 };
        private readonly TextBox repeatCode = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true, MaxLength = 128 };
        private readonly DateTimePicker expires = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", MaxDate = new DateTime(9998, 12, 30) };
        private readonly CheckBox protect = new CheckBox { Text = "需要授权码并限制有效期", AutoSize = true };
        private readonly Label status = new Label { Dock = DockStyle.Fill, ForeColor = SystemColors.ControlText };
        private readonly Label timeInfo = new Label { Dock = DockStyle.Fill };
        private readonly Label validity = new Label { Dock = DockStyle.Fill };
        private readonly TableLayoutPanel settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        private readonly Button unlock = new Button { Text = "验证原授权码", AutoSize = true };
        private readonly StampDetailsEditor detailsEditor;
        private StampUsePermit? permit;
        private StampAsset? editedAsset;

        private StampAuthorizationForm(StampAsset asset, AuthorizationMode mode)
        {
            original = asset.Copy(); this.mode = mode;
            detailsEditor = new StampDetailsEditor(asset.Details) { Dock = DockStyle.Fill };
            Text = mode == AuthorizationMode.Edit ? "印章授权设置" : mode == AuthorizationMode.Management ? "印章管理验证" : "印章使用授权";
            Size = mode == AuthorizationMode.Edit ? new Size(1000, 680) : new Size(630, 380);
            MinimumSize = Size; MaximumSize = new Size(1100, 900);
            StartPosition = FormStartPosition.CenterParent; Font = new Font("Microsoft YaHei UI", 9);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 7 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, mode == AuthorizationMode.Edit ? 60 : 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.Controls.Add(new Label { Text = "印章：" + asset.Name, Dock = DockStyle.Fill, AutoEllipsis = true }, 0, 0);
            root.Controls.Add(timeInfo, 0, 1);
            var authentication = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            authentication.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            authentication.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            authentication.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
            authentication.Controls.Add(new Label { Text = mode == AuthorizationMode.Edit ? "原授权码" : "授权码", Dock = DockStyle.Fill }, 0, 0);
            authentication.Controls.Add(code, 1, 0); authentication.Controls.Add(unlock, 2, 0);
            root.Controls.Add(authentication, 0, 2);
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 5; i++) settings.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            settings.Controls.Add(new Label { Text = "印章名称", Dock = DockStyle.Fill }, 0, 0); settings.Controls.Add(name, 1, 0);
            settings.Controls.Add(protect, 0, 1); settings.SetColumnSpan(protect, 2);
            settings.Controls.Add(new Label { Text = "授权码（8–128字符）", Dock = DockStyle.Fill }, 0, 2); settings.Controls.Add(newCode, 1, 2);
            settings.Controls.Add(new Label { Text = "再次输入授权码", Dock = DockStyle.Fill }, 0, 3); settings.Controls.Add(repeatCode, 1, 3);
            settings.Controls.Add(new Label { Text = "最后可用日期（本地）", Dock = DockStyle.Fill }, 0, 4); settings.Controls.Add(expires, 1, 4);
            settings.Controls.Add(validity, 0, 5); settings.SetColumnSpan(validity, 2);
            var configuration = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            configuration.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47)); configuration.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
            configuration.Controls.Add(settings, 0, 0);
            var detailsGroup = new GroupBox { Text = "印章属性（与授权保护分别设置）", Dock = DockStyle.Fill, Padding = new Padding(10, 23, 10, 8) };
            detailsGroup.Controls.Add(detailsEditor); configuration.Controls.Add(detailsGroup, 1, 0);
            root.Controls.Add(configuration, 0, 3);
            root.Controls.Add(new Label { Dock = DockStyle.Fill, Text = mode == AuthorizationMode.Edit
                ? "修改名称、印章属性或授权期限后须重新输入授权码，可以沿用原码。移除授权保护会保留印章独立有效期。\n期限基于本机时间；具备库文件写权限者仍可删除整个库，不能防止管理员修改系统时间。"
                : mode == AuthorizationMode.Management
                    ? "管理验证允许处理已过期印章；过期印章仍不能用于出图。授权码仅在本次窗口使用，不保存。"
                    : "依据本机时间检查有效期。授权码仅在本次窗口使用，不写入配置或日志。" }, 0, 4);
            root.Controls.Add(status, 0, 5);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
            var confirm = new Button { Text = mode == AuthorizationMode.Edit ? "确认修改" : "授权", AutoSize = true };
            footer.Controls.Add(cancel); footer.Controls.Add(confirm); root.Controls.Add(footer, 0, 6);
            Controls.Add(root); AcceptButton = confirm; CancelButton = cancel;
            confirm.Click += (s, e) => Guard(Confirm);
            unlock.Click += (s, e) => Guard(UnlockForEdit);
            protect.CheckedChanged += (s, e) => UpdateProtectionControls();
            expires.ValueChanged += (s, e) => UpdateValidity();
            name.Text = asset.Name; protect.Checked = true;
            DateTime initial = asset.Protection == null ? DateTime.Today.AddYears(1) :
                new DateTimeOffset(asset.Protection.ExpiresUtcTicks, TimeSpan.Zero).ToLocalTime().AddTicks(-1).Date;
            expires.Value = initial < expires.MinDate ? expires.MinDate : initial > expires.MaxDate ? expires.MaxDate : initial;
            initialAuthorizationDate=expires.Value.Date;
            if (mode != AuthorizationMode.Edit)
            {
                settings.Visible = false; configuration.Visible = false; authentication.ColumnStyles[2].Width = 0; unlock.Visible = false;
                root.RowStyles[3].Height = 0; root.RowStyles[3].SizeType = SizeType.Absolute;
                root.RowStyles[5].SizeType = SizeType.Percent; root.RowStyles[5].Height = 100;
            }
            else if (asset.Protection == null)
            {
                plain = asset.Copy(); code.Enabled = false; unlock.Enabled = false;
                status.Text = "未设置授权保护；确认后先进入库编辑草稿，保存库才生效。";
            }
            else { settings.Enabled = false; detailsEditor.Enabled = false; }
            RefreshTimeInfo(); UpdateProtectionControls(); UpdateValidity();
            FormClosed += (s, e) => ClearCodes();
        }

        public static StampUsePermit? RequestUnlock(IWin32Window owner, StampAsset asset)
        {
            using (var dialog = new StampAuthorizationForm(asset, AuthorizationMode.Use))
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.permit : null;
        }
        public static StampUsePermit? RequestManagementUnlock(IWin32Window owner, StampAsset asset)
        {
            using (var dialog = new StampAuthorizationForm(asset, AuthorizationMode.Management))
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.permit : null;
        }
        public static StampAsset? RequestProtection(IWin32Window owner, StampAsset asset)
        {
            using (var dialog = new StampAuthorizationForm(asset, AuthorizationMode.Edit))
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.editedAsset : null;
        }
        private void Guard(Action action)
        {
            try { status.ForeColor=SystemColors.ControlText; RefreshTimeInfo(); action(); }
            catch (Exception ex) { status.ForeColor=Color.Firebrick; status.Text = ex.Message; }
        }
        private void RefreshTimeInfo()
        {
            string expiry = original.Protection == null ? "未设置授权限制" :
                "有效截止（不含）：" + new DateTimeOffset(original.Protection.ExpiresUtcTicks, TimeSpan.Zero).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
            string stampExpiry = original.Details == null || original.Details.ValidUntilUtcTicks == 0 ? "印章独立期限：不限制" :
                "印章截止（不含）：" + new DateTimeOffset(original.Details.ValidUntilUtcTicks, TimeSpan.Zero).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
            timeInfo.Text = "本机时间：" + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") + "\n授权：" + expiry + "\n" + stampExpiry;
        }
        private void UnlockForEdit()
        {
            if (original.Protection == null) return;
            var authorized = StampAuthorization.UnlockForManagement(original, code.Text);
            plain = authorized.GetAssetForManagement(original);
            code.Clear(); code.Enabled = false; unlock.Enabled = false; settings.Enabled = true; detailsEditor.Enabled = true;
            status.Text = "管理验证通过；可编辑印章属性和授权保护，移除授权保护仍保留印章独立期限。";
        }
        private void UpdateProtectionControls()
        {
            newCode.Enabled = repeatCode.Enabled = expires.Enabled = protect.Checked;
            if (!protect.Checked) { newCode.Clear(); repeatCode.Clear(); }
            UpdateValidity();
        }
        private DateTimeOffset GetExpiryUtc()
        {
            if(original.Protection!=null && expires.Value.Date==initialAuthorizationDate)
                return new DateTimeOffset(original.Protection.ExpiresUtcTicks,TimeSpan.Zero);
            DateTime end = DateTime.SpecifyKind(expires.Value.Date.AddDays(1), DateTimeKind.Unspecified);
            if (TimeZoneInfo.Local.IsInvalidTime(end)) throw new InvalidOperationException("所选日期的本地午夜不存在，请另选有效日期。");
            if (TimeZoneInfo.Local.IsAmbiguousTime(end)) throw new InvalidOperationException("所选日期的本地午夜有两个时刻，请另选无歧义日期。");
            return new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end)).ToUniversalTime();
        }
        private void UpdateValidity()
        {
            if (!protect.Checked) { validity.Text = "移除授权保护后无需授权码，但仍受印章独立有效期限制。"; return; }
            try { validity.Text = "截止时刻（不含）：" + GetExpiryUtc().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"); }
            catch (Exception ex) { validity.Text = ex.Message; }
        }
        private void Confirm()
        {
            if (mode == AuthorizationMode.Edit)
            {
                if (plain == null) throw new InvalidOperationException("请先验证原授权码，已过期印章也可进行管理验证。");
                var candidate = plain.Copy(); candidate.Name = name.Text.Trim();
                candidate.Details = detailsEditor.ReadDetails();
                if (protect.Checked)
                {
                    if (string.IsNullOrWhiteSpace(newCode.Text) || newCode.Text.Length < 8 || newCode.Text.Length > 128)
                        throw new InvalidOperationException("授权码须为 8 至 128 个字符，且不能全为空白。");
                    if (!string.Equals(newCode.Text, repeatCode.Text, StringComparison.Ordinal)) throw new InvalidOperationException("两次输入的授权码不一致。");
                    editedAsset = StampAuthorization.Protect(candidate, newCode.Text, GetExpiryUtc(), DateTimeOffset.UtcNow);
                }
                else { candidate.Validate(); editedAsset = candidate; }
            }
            else permit = mode == AuthorizationMode.Management
                ? StampAuthorization.UnlockForManagement(original, code.Text)
                : StampAuthorization.Unlock(original, code.Text);
            ClearCodes(); DialogResult = DialogResult.OK; Close();
        }
        private void ClearCodes() { code.Clear(); newCode.Clear(); repeatCode.Clear(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ClearCodes(); plain = null; }
            base.Dispose(disposing);
        }
    }
}

using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckStampAuthorizationUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StampAuthorizationUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        const string oldCode = "offline-test-old-code";
        const string nextCode = "offline-test-new-code";
        try
        {
            byte[] png;
            using (var bitmap = new Bitmap(40, 40, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var memory = new MemoryStream())
            {
                graphics.Clear(Color.Transparent); graphics.FillEllipse(Brushes.Red, 5, 5, 30, 30);
                graphics.FillRectangle(Brushes.White, 15, 15, 10, 10); bitmap.Save(memory, ImageFormat.Png); png = memory.ToArray();
            }
            var now = DateTimeOffset.UtcNow;
            var plain = StampAsset.Import("授权测试几何图片", png);
            var protectedAsset = StampAuthorization.Protect(plain, oldCode, now.AddDays(2), now);
            string fingerprint = StampAuthorization.Fingerprint(protectedAsset);
            string path = Path.Combine(directory, "protected-stamps.json");
            var library = new StampLibrary { SchemaVersion = 2, Assets = new() { protectedAsset.Copy() } };
            StampLibraryStore.Save(path, library);
            File.SetLastWriteTimeUtc(path, new DateTime(2020, 2, 3, 0, 0, 0, DateTimeKind.Utc));
            DateTime beforeTime = File.GetLastWriteTimeUtc(path);
            byte[] before = File.ReadAllBytes(path);
            using (var locked = new StampLibraryForm(path, protectedAsset.Id))
            {
                Check(Field<PictureBox>(locked, "preview").Image == null && !Field<TextBox>(locked, "assetName").Enabled,
                    "受保护印章初始不显示图像且不能直接改名");
                Field<TextBox>(locked, "assetName").Text = "程序模拟尝试改名";
                Check(Field<StampLibrary>(locked, "library").Assets[0].Name == protectedAsset.Name,
                    "受保护名称即使被外部设入文本框，也不会写入库对象");
                InvokeStampEditor(locked, "DisplaySelection");
                Check(Field<Label>(locked, "dimensions").Text.Contains("受授权保护"), "库窗明确显示印章授权状态与截止信息");
                RenderStampWindow(locked, "ui-stamp-protected-library.png");
                InvokeStampEditor(locked, "ConfirmSelection");
                Check(locked.SelectedAssetId == protectedAsset.Id, "选择受保护印章不要求预先解锁，使用前再授权");
            }
            Check(File.ReadAllBytes(path).SequenceEqual(before) && File.GetLastWriteTimeUtc(path) == beforeTime,
                "只选择受保护印章不重写库文件及时间戳");
            string stored = File.ReadAllText(path);
            Check(!stored.Contains(oldCode) && !stored.Contains(plain.PngBase64) && StampLibraryStore.Load(path).Assets[0].PngBase64 == "",
                "持久库不含明文授权码或受保护印章 PNG");

            using (var use = CreateStampAuthorization(protectedAsset, "Use"))
            {
                ShowStampOffscreen(use); use.Size = use.MinimumSize; use.PerformLayout();
                Check(Field<TextBox>(use, "code").UseSystemPasswordChar, "授权对话框使用密码掩码");
                Check(Field<Label>(use, "timeInfo").Text.Contains("本机时间") && Field<Label>(use, "timeInfo").Text.Contains("不含"),
                    "授权窗告知本机时间与排他截止时刻");
                CheckStampAuthorizationBounds(use, new[] { "code", "timeInfo", "status" });
                Field<TextBox>(use, "code").Text = "wrong-offline-code";
                RejectStampEditor(() => InvokeStampEditor(use, "Confirm"), typeof(InvalidOperationException), "错误授权码不能完成使用授权");
                Check(use.DialogResult != DialogResult.OK && Field<StampUsePermit?>(use, "permit") == null && !use.IsDisposed,
                    "错误授权码保留窗口且不产生使用凭据");
                Field<TextBox>(use, "code").Text = oldCode; InvokeStampEditor(use, "Confirm");
                var permission = Field<StampUsePermit>(use, "permit");
                Check(use.DialogResult == DialogResult.OK && permission.GetAsset(protectedAsset, DateTimeOffset.UtcNow).PngBase64 == plain.PngBase64,
                    "正确授权码通过对话框确认后取得可用印章");
                Check(Field<TextBox>(use, "code").Text == "", "授权成功清空窗口授权码文本");
            }

            var expired = StampAuthorization.Protect(plain, oldCode, now.AddDays(-1), now.AddDays(-3));
            using (var use = CreateStampAuthorization(expired, "Use"))
            {
                Field<TextBox>(use, "code").Text = oldCode;
                RejectStampEditor(() => InvokeStampEditor(use, "Confirm"), typeof(InvalidOperationException), "过期印章即使授权码正确也不能通过使用授权");
            }
            using (var management = CreateStampAuthorization(expired, "Management"))
            {
                Field<TextBox>(management, "code").Text = oldCode; InvokeStampEditor(management, "Confirm");
                var permission = Field<StampUsePermit>(management, "permit");
                Check(permission.GetAssetForManagement(expired).PngBase64 == plain.PngBase64,
                    "已过期印章仍可通过管理验证读取以维护授权");
                bool denied = false;
                try { permission.GetAsset(expired, DateTimeOffset.UtcNow); } catch (InvalidOperationException) { denied = true; }
                Check(denied, "过期管理凭据不能用作出图授权");
            }

            StampAsset edited;
            using (var edit = CreateStampAuthorization(protectedAsset, "Edit"))
            {
                ShowStampOffscreen(edit); edit.Size = edit.MinimumSize; edit.PerformLayout();
                Check(!Field<TableLayoutPanel>(edit, "settings").Enabled, "受保护印章未验证旧码前，授权编辑区域不可用");
                Field<CheckBox>(edit, "protect").Checked = false;
                RejectStampEditor(() => InvokeStampEditor(edit, "Confirm"), typeof(InvalidOperationException),
                    "未验证旧授权码不能通过取消勾选移除保护");
                Field<TextBox>(edit, "code").Text = "wrong-offline-code";
                RejectStampEditor(() => InvokeStampEditor(edit, "UnlockForEdit"), typeof(InvalidOperationException), "错误旧码不能解锁保护设置");
                Field<TextBox>(edit, "code").Text = oldCode; InvokeStampEditor(edit, "UnlockForEdit");
                Check(Field<TableLayoutPanel>(edit, "settings").Enabled && Field<TextBox>(edit, "code").Text == "",
                    "正确旧码解锁授权编辑后立即清空原码");
                Field<CheckBox>(edit, "protect").Checked = true;
                Field<TextBox>(edit, "name").Text = "授权后重命名几何图";
                Field<TextBox>(edit, "newCode").Text = nextCode;
                Field<TextBox>(edit, "repeatCode").Text = "different-offline-code";
                RejectStampEditor(() => InvokeStampEditor(edit, "Confirm"), typeof(InvalidOperationException), "两次新授权码不同阻止修改提交");
                Check(Field<StampAsset?>(edit, "editedAsset") == null && StampAuthorization.Fingerprint(protectedAsset) == fingerprint && File.ReadAllBytes(path).SequenceEqual(before),
                    "授权码确认失败不改变原资产或磁盘库");
                Field<TextBox>(edit, "repeatCode").Text = nextCode;
                DateTime lastDate = DateTime.Today.AddDays(4);
                Field<DateTimePicker>(edit, "expires").Value = lastDate;
                var expiresUtc = (DateTimeOffset)InvokeStampEditor(edit, "GetExpiryUtc")!;
                Check(expiresUtc.ToLocalTime().Date == lastDate.AddDays(1) && expiresUtc.ToLocalTime().TimeOfDay == TimeSpan.Zero,
                    "授权最后日期换算为本地次日零点排他截止");
                RenderStampWindow(edit, "ui-stamp-authorization.png");
                CheckStampAuthorizationBounds(edit, new[] { "code", "name", "newCode", "repeatCode", "expires", "protect", "validity", "status", "timeInfo" });
                InvokeStampEditor(edit, "Confirm");
                edited = Field<StampAsset>(edit, "editedAsset");
                Check(edited.Protection != null && edited.PngBase64 == "" && edited.Name == "授权后重命名几何图" && edited.Protection.ExpiresUtcTicks == expiresUtc.UtcDateTime.Ticks,
                    "授权编辑确认返回已加密的新名称与截止时间草稿");
                Check(Field<TextBox>(edit, "newCode").Text == "" && Field<TextBox>(edit, "repeatCode").Text == "",
                    "保护修改确认后清空新授权码及确认输入");
            }
            Check(File.ReadAllBytes(path).SequenceEqual(before) && StampAuthorization.Fingerprint(protectedAsset) == fingerprint,
                "授权对话框只返回草稿，不自行写库或修改调用方资产");
            Check(StampAuthorization.Unlock(edited, nextCode, DateTimeOffset.UtcNow).GetAsset(edited, DateTimeOffset.UtcNow).Name == edited.Name,
                "重新保护的草稿可由新授权码解锁");

            using (var discard = new StampLibraryForm(path, protectedAsset.Id))
            {
                // 模拟授权设置回传后存入库编辑草稿，再直接 Dispose 丢弃。
                Field<StampLibrary>(discard, "library").Assets[0] = edited.Copy();
                discard.GetType().GetField("dirty", PrivateInstance)!.SetValue(discard, true);
            }
            Check(File.ReadAllBytes(path).SequenceEqual(before), "丢弃含授权修改的库草稿不保存新密文");
            using (var save = new StampLibraryForm(path, protectedAsset.Id))
            {
                Field<StampLibrary>(save, "library").Assets[0] = edited.Copy();
                save.GetType().GetField("dirty", PrivateInstance)!.SetValue(save, true);
                save.GetType().GetMethod("RefreshList", PrivateInstance)!.Invoke(save, new object[] { edited.Id });
                InvokeStampEditor(save, "ConfirmSelection");
            }
            var reloaded = StampLibraryStore.Load(path);
            stored = File.ReadAllText(path);
            Check(reloaded.SchemaVersion == 3 && reloaded.Assets[0].Name == edited.Name && reloaded.Assets[0].Protection != null && reloaded.Assets[0].Details != null,
                "库窗明确保存后持久化含显式属性的版本3授权草稿");
            Check(!stored.Contains(oldCode) && !stored.Contains(nextCode) && !stored.Contains(plain.PngBase64) && reloaded.Assets[0].PngBase64 == "",
                "保存授权修改仍不包含新旧授权码或 PNG 明文");

            using (var removal = CreateStampAuthorization(expired, "Edit"))
            {
                Field<TextBox>(removal, "code").Text = oldCode; InvokeStampEditor(removal, "UnlockForEdit");
                Field<CheckBox>(removal, "protect").Checked = false; InvokeStampEditor(removal, "Confirm");
                var unprotected = Field<StampAsset>(removal, "editedAsset");
                Check(unprotected.Protection == null && unprotected.PngBase64 == plain.PngBase64 && expired.Protection != null,
                    "旧码管理验证后可移除过期印章保护，原始资产仍保持加密");
            }

            using var main = new BatchPlotForm(Path.Combine(directory, "settings.json"));
            var config = Field<PlotConfig>(main, "_config");
            config.PrintStamps = true; config.StampLibraryPath = path; config.StampAssetId = edited.Id;
            config.StampPermit = StampAuthorization.Unlock(reloaded.Assets[0], nextCode, DateTimeOffset.UtcNow);
            var cachedPermit = config.StampPermit;
            Call(main, "CaptureStampAsset");
            Check(config.Stamp != null && config.Stamp.Protection != null && config.Stamp.PngBase64 == "" && ReferenceEquals(config.StampPermit, cachedPermit),
                "主窗复用有效内存凭据，任务配置保留加密资产，不弹重复授权窗口");
            Check(StampOutputGuard.Resolve(config, DateTimeOffset.UtcNow)!.PngBase64 == plain.PngBase64,
                "主窗捕获的授权配置通过每页检查后才取得印章明文");
            config.PrintStamps = false; Call(main, "CaptureStampAsset");
            Check(config.Stamp == null && config.StampPermit == null, "关闭印章输出后清除加密资产快照与授权凭据");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private static StampAuthorizationForm CreateStampAuthorization(StampAsset asset, string mode)
    {
        var enumType = typeof(StampAuthorizationForm).GetNestedType("AuthorizationMode", BindingFlags.NonPublic)!;
        var constructor = typeof(StampAuthorizationForm).GetConstructor(PrivateInstance, null, new[] { typeof(StampAsset), enumType }, null)!;
        return (StampAuthorizationForm)constructor.Invoke(new[] { (object)asset, Enum.Parse(enumType, mode) });
    }
    private static void CheckStampAuthorizationBounds(StampAuthorizationForm form, IEnumerable<string> fields)
    {
        foreach (string field in fields)
        {
            var control = Field<Control>(form, field);
            Check(control.Visible && control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds),
                "授权窗最小尺寸控件完整可见：" + field);
        }
    }
}

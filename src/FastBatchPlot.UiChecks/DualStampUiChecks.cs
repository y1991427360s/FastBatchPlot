using System.Drawing;
using System.Drawing.Imaging;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckDualStampSelectionUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DualStampSelection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            byte[] png;
            using (var image = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(image))
            using (var memory = new MemoryStream())
            {
                graphics.Clear(Color.Transparent); graphics.FillRectangle(Brushes.Red, 4, 4, 24, 24);
                image.Save(memory, ImageFormat.Png); png = memory.ToArray();
            }
            var primary = StampAsset.Import("主印章几何测试图", png);
            var registration = StampAsset.Import("注册章几何测试图", png); registration.Details = new StampDetails { Kind = StampKind.Registration };
            string path = Path.Combine(directory, "stamps.json");
            StampLibraryStore.Save(path, new StampLibrary { SchemaVersion = 3, Assets = new() { primary, registration } });
            var source = new PlotConfig { StampLibraryPath = path, StampAssetId = primary.Id, PrintPrimaryStamp = true,
                RegistrationStampLibraryPath = path, RegistrationStampAssetId = registration.Id, PrintRegistrationStamp = true,
                PrintStamps = false, Stamp = primary.Copy() };
            byte[] before = File.ReadAllBytes(path); DateTime timestamp = File.GetLastWriteTimeUtc(path);
            using (var form = new StampOutputForm(source))
            {
                Check(Field<TextBox>(form, "primaryName").Text == primary.Name && Field<TextBox>(form, "registrationName").Text == registration.Name,
                    "双章设置读取主章与注册章名称并保持两个独立选择");
                Check(Field<TextBox>(form, "primaryPath").ReadOnly && Field<TextBox>(form, "registrationPath").ReadOnly,
                    "双章库路径只读，由库选择入口统一管理");
                bool rejected = false; try { _ = form.Selection; } catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "双章设置未确认前不能取得有效选择结果");
                form.Size = form.MinimumSize; RenderStampWindow(form, "ui-dual-stamp-selection.png");
                foreach (string field in new[] { "primaryEnabled", "registrationEnabled", "primaryPath", "registrationPath", "primaryName", "registrationName", "confirm", "status" })
                    CheckDetailControlBounds(Field<Control>(form, field), "双章最小窗口控件完整可见：" + field);
                InvokeStampEditor(form, "Confirm");
                var selection = form.Selection;
                Check(selection.StampAssetId == primary.Id && selection.RegistrationStampAssetId == registration.Id && selection.PrintPrimaryStamp && selection.PrintRegistrationStamp,
                    "双章确认分别返回两个槽位选择和开关");
                Check(selection.Stamp == null && selection.StampPermit == null && selection.RegistrationStamp == null && selection.RegistrationStampPermit == null && !ReferenceEquals(selection, source),
                    "双章选择结果不携带任何印章图片或凭据，也不复用来源配置对象");
            }
            Check(!source.PrintStamps && source.Stamp!.Id == primary.Id && source.StampAssetId == primary.Id && File.ReadAllBytes(path).SequenceEqual(before) && File.GetLastWriteTimeUtc(path) == timestamp,
                "双章选择不改来源总开关、原配置或库文件");
            using (var cancel = new StampOutputForm(source))
            {
                Field<CheckBox>(cancel, "primaryEnabled").Checked = false;
                Field<TextBox>(cancel, "registrationPath").Text = "草稿路径.json";
            }
            Check(source.PrintPrimaryStamp && source.RegistrationStampLibraryPath == path, "丢弃双章设置草稿保留来源选择和开关");

            string broken = Path.Combine(directory, "broken.json"); File.WriteAllText(broken, "{broken");
            var disabled = new PlotConfig { StampLibraryPath = broken, StampAssetId = primary.Id, PrintPrimaryStamp = false,
                RegistrationStampLibraryPath = broken, RegistrationStampAssetId = registration.Id, PrintRegistrationStamp = false };
            using (var form = new StampOutputForm(disabled))
            {
                Check(Field<TextBox>(form, "primaryName").Text.StartsWith("已停用") && Field<TextBox>(form, "registrationName").Text.StartsWith("已停用"),
                    "关闭的双章槽位不读取损坏库，只显示保留的选择");
                InvokeStampEditor(form, "Confirm");
                Check(form.Selection.StampAssetId == primary.Id && form.Selection.RegistrationStampAssetId == registration.Id && !form.Selection.PrintPrimaryStamp && !form.Selection.PrintRegistrationStamp,
                    "关闭槽位确认保留失效库路径和ID，不强迫先修复库");
            }
            using (var empty = new StampOutputForm(new PlotConfig { StampLibraryPath = broken, RegistrationStampLibraryPath = broken }))
            {
                InvokeStampEditor(empty, "Confirm");
                Check(empty.Selection.StampAssetId == "" && empty.Selection.RegistrationStampAssetId == "", "无印章选择的启用槽位不要求库文件有效");
            }
            using (var wrongKind = new StampOutputForm(new PlotConfig { PrintPrimaryStamp = false, RegistrationStampLibraryPath = path, RegistrationStampAssetId = primary.Id }))
                RejectStampEditor(() => InvokeStampEditor(wrongKind, "Confirm"), typeof(InvalidOperationException), "注册章槽拒绝出图章和旧缺省类别资产");
            using (var duplicate = new StampOutputForm(new PlotConfig { StampLibraryPath = path, StampAssetId = registration.Id,
                RegistrationStampLibraryPath = Path.Combine(directory, ".", "stamps.json").ToUpperInvariant(), RegistrationStampAssetId = registration.Id }))
                RejectStampEditor(() => InvokeStampEditor(duplicate, "Confirm"), typeof(InvalidOperationException), "两个槽位规范化路径与大小写后拒绝重复库和ID");
            using (var legacy = new StampOutputForm(new PlotConfig { StampLibraryPath = path, StampAssetId = registration.Id, PrintRegistrationStamp = false }))
            {
                InvokeStampEditor(legacy, "Confirm");
                Check(legacy.Selection.StampAssetId == registration.Id, "主印章槽兼容原来以注册章作为唯一印章的配置");
            }
            using (var missing = new StampOutputForm(new PlotConfig { StampLibraryPath = path, StampAssetId = Guid.NewGuid().ToString("N") }))
                RejectStampEditor(() => InvokeStampEditor(missing, "Confirm"), typeof(InvalidOperationException), "已启用且选择不存在ID的槽位拒绝确认");
            Check(File.ReadAllText(broken) == "{broken" && File.ReadAllBytes(path).SequenceEqual(before), "双章校验失败不修改损坏库或有效库");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}

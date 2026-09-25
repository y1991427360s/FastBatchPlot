using System.Drawing;
using System.Drawing.Imaging;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Assets;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Planning;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckStampDetailsUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "StampDetailsUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousHost = CadHostProvider.Host;
        var previousPlotter = CadHostProvider.Plotter;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var original = new StampDetails { Kind = StampKind.Registration, Sizing = StampSizing.PhysicalSize,
                WidthMm = 20, HeightMm = 30, ValidUntilUtcTicks = now.AddDays(5).AddTicks(12345).UtcDateTime.Ticks };
            using (var form = new StampDetailsForm(original))
            {
                var editor = Field<StampDetailsEditor>(form, "detailsEditor");
                var value = editor.ReadDetails();
                Check(value.Kind == StampKind.Registration && value.Sizing == StampSizing.PhysicalSize && value.WidthMm == 20 && value.HeightMm == 30,
                    "印章属性编辑器恢复类别和固定纸面毫米尺寸");
                Check(value.ValidUntilUtcTicks == original.ValidUntilUtcTicks && !ReferenceEquals(value, original),
                    "未改日期时保持原精确印章期限，不延长为当天结束");
                Field<NumericUpDown>(editor, "width").Value = 25;
                Check(original.WidthMm == 20 && form.Details == null, "印章属性编辑只修改草稿，确认前不返回结果");
                Field<ComboBox>(editor, "sizing").SelectedIndex = 0;
                var fit = editor.ReadDetails();
                Check(fit.WidthMm == 0 && fit.HeightMm == 0 && !Field<NumericUpDown>(editor, "width").Enabled,
                    "适应区域模式清除输出固定尺寸且禁用尺寸编辑");
                Field<CheckBox>(editor, "validEnabled").Checked = false;
                Check(editor.ReadDetails().ValidUntilUtcTicks == 0 && !Field<DateTimePicker>(editor, "validDate").Enabled,
                    "不限制印章期限时保存零截止并禁用日期");
                Field<CheckBox>(editor, "validEnabled").Checked = true;
                var selectedDate = DateTime.Today.AddDays(9);
                Field<DateTimePicker>(editor, "validDate").Value = selectedDate;
                var end = new DateTimeOffset(editor.ReadDetails().ValidUntilUtcTicks, TimeSpan.Zero).ToLocalTime();
                Check(end.Date == selectedDate.AddDays(1) && end.TimeOfDay == TimeSpan.Zero,
                    "更改印章最后日期后保存本地次日零点排他期限");
                Field<ComboBox>(editor, "sizing").SelectedIndex = 1;
                var width = Field<NumericUpDown>(editor, "width");
                bool below = false, above = false;
                try { width.Value = 0.09m; } catch (ArgumentOutOfRangeException) { below = true; }
                try { width.Value = 1001m; } catch (ArgumentOutOfRangeException) { above = true; }
                Check(below && above, "纸面尺寸控件拒绝小于0.1毫米和超过1000毫米的值");
                form.Size = form.MinimumSize; RenderStampWindow(form, "ui-stamp-details.png");
                CheckDetailsBounds(editor);
                InvokeStampEditor(form, "Confirm");
                Check(form.Details!.WidthMm == 25 && form.Details.HeightMm == 30 && original.WidthMm == 20,
                    "属性确认返回验证副本，不改调用方原属性");
            }
            using (var cancelled = new StampDetailsForm(original))
            {
                Field<ComboBox>(Field<StampDetailsEditor>(cancelled, "detailsEditor"), "kind").SelectedIndex = 2;
                Check(cancelled.Details == null, "取消或丢弃属性窗不返回编辑结果");
            }
            Check(original.Kind == StampKind.Registration && original.WidthMm == 20, "取消属性编辑保留调用方原对象");
            bool invalid = false;
            try { using var ignored = new StampDetailsEditor(new StampDetails { Sizing = StampSizing.PhysicalSize, WidthMm = 0, HeightMm = 20 }); }
            catch (InvalidDataException) { invalid = true; }
            Check(invalid, "属性编辑器拒绝载入非法固定尺寸，避免悄悄纠正坏数据");
            using (var legacy = new StampDetailsEditor(null))
            {
                var defaults = legacy.ReadDetails();
                Check(defaults.Kind == StampKind.Issue && defaults.Sizing == StampSizing.FitRegion && defaults.ValidUntilUtcTicks == 0,
                    "旧无属性印章默认出图章、适应区域、无独立期限");
            }

            byte[] png;
            using (var image = new Bitmap(80, 40, PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(image))
            using (var memory = new MemoryStream())
            {
                graphics.Clear(Color.Transparent); graphics.FillRectangle(Brushes.Red, 5, 5, 70, 30);
                graphics.FillRectangle(Brushes.White, 12, 12, 56, 16); image.Save(memory, ImageFormat.Png); png = memory.ToArray();
            }
            var oldAsset = StampAsset.Import("旧格式出图章测试图", png);
            var registration = StampAsset.Import("注册章尺寸测试图", png); registration.Details = original.Copy();
            var other = StampAsset.Import("其他类别测试图", png); other.Details = new StampDetails { Kind = StampKind.Other };
            string path = Path.Combine(directory, "stamps.json");
            StampLibraryStore.Save(path, new StampLibrary { SchemaVersion = 3, Assets = new() { oldAsset, registration, other } });
            byte[] stored = File.ReadAllBytes(path); DateTime written = File.GetLastWriteTimeUtc(path);
            using (var libraryForm = new StampLibraryForm(path, oldAsset.Id))
            {
                var filter = Field<ComboBox>(libraryForm, "categoryFilter");
                var list = Field<ListBox>(libraryForm, "assets");
                Check(Field<Label>(libraryForm, "dimensions").Text.StartsWith("出图章") && list.Items.Count == 3,
                    "库窗把旧Details为空的资产显示为出图章，默认展示全部类别");
                filter.SelectedIndex = 2;
                Check(list.Items.Count == 1 && ((StampAsset)list.Items[0]).Id == registration.Id && list.SelectedItem == null,
                    "类别筛选只显示注册章，不将被隐藏的旧选择替换成其他资产");
                Check(!Field<bool>(libraryForm, "dirty") && File.ReadAllBytes(path).SequenceEqual(stored),
                    "类别过滤不标记库为修改状态，也不写文件");
                filter.SelectedIndex = 0;
                Check(((StampAsset)list.SelectedItem!).Id == oldAsset.Id, "恢复全部类别后找回原先被隐藏的选择");
                filter.SelectedIndex = 3;
                Check(list.Items.Count == 1 && ((StampAsset)list.Items[0]).Id == other.Id && list.SelectedItem == null,
                    "其他类别筛选保持未明确选择的资产不被自动选中");
                filter.SelectedIndex = 1;
                Check(list.Items.Count == 1 && ((StampAsset)list.SelectedItem!).Id == oldAsset.Id, "出图章分类兼容缺省旧属性");
                filter.SelectedIndex = 0;
                list.SelectedItem = list.Items.Cast<StampAsset>().Single(a => a.Id == registration.Id);
                Check(Field<Label>(libraryForm, "dimensions").Text.Contains("纸面 20 × 30 mm") && Field<Label>(libraryForm, "dimensions").Text.Contains("印章截止"),
                    "库窗分别展示注册章尺寸和独立期限");
                libraryForm.Size = libraryForm.MinimumSize; RenderStampWindow(libraryForm, "ui-stamp-details-library.png");
                foreach (string field in new[] { "categoryFilter", "assets", "assetName", "preview", "dimensions", "status", "confirm" })
                    CheckDetailControlBounds(Field<Control>(libraryForm, field), "库窗最小尺寸：" + field);
                InvokeStampEditor(libraryForm, "ConfirmSelection");
                Check(libraryForm.SelectedAssetId == registration.Id, "类别过滤后仅返回用户明确选择的资产");
            }
            Check(File.ReadAllBytes(path).SequenceEqual(stored) && File.GetLastWriteTimeUtc(path) == written,
                "仅筛选并选择印章属性不重写已有库");

            const string code = "offline-details-code";
            var encrypted = StampAuthorization.Protect(registration, code, now.AddDays(10), now);
            string fingerprint = StampAuthorization.Fingerprint(encrypted);
            using (var authorization = CreateStampAuthorization(encrypted, "Edit"))
            {
                var editor = Field<StampDetailsEditor>(authorization, "detailsEditor");
                Check(!editor.Enabled && !Field<ComboBox>(editor, "kind").Enabled, "受保护印章属性在旧码验证前不可编辑");
                RejectStampEditor(() => InvokeStampEditor(authorization, "Confirm"), typeof(InvalidOperationException), "不能绕过原授权码提交受保护属性");
                Field<TextBox>(authorization, "code").Text = code; InvokeStampEditor(authorization, "UnlockForEdit");
                Check(editor.Enabled, "旧授权码验证成功才解锁印章属性编辑器");
                Check(((DateTimeOffset)InvokeStampEditor(authorization,"GetExpiryUtc")!).UtcDateTime.Ticks==encrypted.Protection!.ExpiresUtcTicks,
                    "仅编辑印章属性不悄然延长原有精确授权截止时间");
                Field<ComboBox>(editor, "kind").SelectedIndex = 2;
                Field<NumericUpDown>(editor, "width").Value = 22;
                Field<TextBox>(authorization, "newCode").Text = code;
                Field<TextBox>(authorization, "repeatCode").Text = code;
                authorization.Size = authorization.MinimumSize; RenderStampWindow(authorization, "ui-stamp-authorization.png");
                CheckDetailsBounds(editor);
                CheckStampAuthorizationBounds(authorization, new[] { "code", "name", "newCode", "repeatCode", "expires", "protect", "validity", "status", "timeInfo" });
                Field<CheckBox>(authorization, "protect").Checked = false;
                InvokeStampEditor(authorization, "Confirm");
                var result = Field<StampAsset>(authorization, "editedAsset");
                Check(result.Protection == null && result.Details!.Kind == StampKind.Other && result.Details.WidthMm == 22 && result.Details.ValidUntilUtcTicks == original.ValidUntilUtcTicks,
                    "移除授权保护保留印章独立期限，同时返回属性更改草稿");
                Check(StampAuthorization.Fingerprint(encrypted) == fingerprint && registration.Details!.WidthMm == 20,
                    "授权属性更改不修改原加密资产或原属性");
            }

            var host = new OfflineStampHost(); CadHostProvider.Host = host;
            using var main = new BatchPlotForm(Path.Combine(directory, "settings.json"));
            var config = Field<PlotConfig>(main, "_config");
            config.PrintStamps = true; config.StampLibraryPath = path; config.StampAssetId = registration.Id;
            Call(main, "CaptureStampAsset");
            var region = new TemplateRegion { X1 = -1200, X2 = -200, Y1 = 100, Y2 = 900 };
            Field<TitleTemplateLibrary>(main, "_titleTemplates").Templates.Add(new TitleBlockTemplate { Name = "尺寸模板", BlockName = "STAMP_FRAME", StampRegion = region });
            var frame = new PlotFrame { Type = FrameType.BlockReference, SourceBlockName = "STAMP_FRAME", HandleOrId = "1", OrderIndex = 1,
                MinX = 0, MinY = 0, MaxX = 8410, MaxY = 5940, CalculatedScale = 10, SourceDocumentId = "stamp-document", SourceLayoutId = "stamp-space" };
            var placed = ((List<PlotFrame>)Call(main, "PrepareStampFrames", (object)new[] { frame })!)[0].StampPlacement!;
            Check(Math.Abs(placed.Bounds.Width - 200) < 1e-7 && Math.Abs(placed.Bounds.Height - 300) < 1e-7,
                "主窗按1:10把纸面20×30毫米换算为200×300图形单位，不铺满区域");
            Check(frame.StampPlacement == null && Math.Abs(placed.Bounds.MinX - 7610) < 1e-7 && Math.Abs(placed.Bounds.MinY - 350) < 1e-7,
                "固定尺寸在模板区域居中且不修改列表原框");
            config.MarginMm = -10;
            var plan = PlotPlanBuilder.Create(frame, config);
            var withMargins = ((List<PlotFrame>)Call(main, "PrepareStampFrames", (object)new[] { frame })!)[0].StampPlacement!;
            Check(Math.Abs(withMargins.Bounds.Width / plan.ScaleDenominator - 20) < 1e-7 && Math.Abs(withMargins.Bounds.Height / plan.ScaleDenominator - 30) < 1e-7,
                "缩小内容留白后按最终比例保持指定印章纸面毫米尺寸");
            config.MarginMm = 0; config.Stamp!.Details!.WidthMm = 101;
            Reject(() => Call(main, "PrepareStampFrames", (object)new[] { frame }), "固定尺寸超出模板区域时拒绝整批预检，不自动缩小");
            var expired = StampAsset.Import("已到期无授权测试图", png); expired.Details = new StampDetails { ValidUntilUtcTicks = now.AddDays(-1).UtcDateTime.Ticks };
            var revised = StampLibraryStore.Load(path); revised.Assets.Add(expired); StampLibraryStore.Save(path, revised);
            config.StampAssetId = expired.Id;
            Reject(() => Call(main, "CaptureStampAsset"), "没有授权码保护的印章独立到期后仍阻止主窗出图预检");
        }
        finally
        {
            CadHostProvider.Host = previousHost; CadHostProvider.Plotter = previousPlotter;
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
    private static void CheckDetailsBounds(StampDetailsEditor editor)
    {
        foreach (string field in new[] { "kind", "sizing", "width", "height", "validEnabled", "validDate", "deadline" })
            CheckDetailControlBounds(Field<Control>(editor, field), "印章属性最小尺寸：" + field);
    }
    private static void CheckDetailControlBounds(Control control, string message)
    {
        Check(control.Visible && control.Width > 0 && control.Height > 0 && control.Parent!.ClientRectangle.Contains(control.Bounds), message);
    }
}

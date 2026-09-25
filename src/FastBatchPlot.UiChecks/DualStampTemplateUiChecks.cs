using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckDualStampTemplateUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "DualStampTemplate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "templates.json");
            var first = new TitleBlockTemplate { Name = "双章示例图框", BlockName = "DUAL_STAMP_FRAME",
                StampRegion = new TemplateRegion { X1 = -150, Y1 = 20, X2 = -80, Y2 = 65 },
                RegistrationStampRegion = new TemplateRegion { X1 = -65, Y1 = 20, X2 = -10, Y2 = 70 } };
            var second = new TitleBlockTemplate { Name = "另一图框", BlockName = "OTHER_FRAME",
                RegistrationStampRegion = new TemplateRegion { X1 = -50, Y1 = 5, X2 = -5, Y2 = 55 } };
            var library = new TitleTemplateLibrary { Templates = new() { first, second } };
            TitleTemplateService.Validate(library);
            var clone = TitleTemplateService.Clone(first);
            clone.StampRegion!.X1 = -140; clone.RegistrationStampRegion!.X1 = -60;
            Check(clone.Id != first.Id && first.StampRegion!.X1 == -150 && first.RegistrationStampRegion!.X1 == -65,
                "复制双章模板生成新标识，并深复制两个独立区域");
            var copied = TitleTemplateService.CopyLibrary(library);
            copied.Templates[0].RegistrationStampRegion!.Y1 = 25;
            Check(copied.Templates[0].Id == first.Id && first.RegistrationStampRegion.Y1 == 20,
                "复制模板库保留标识且不共享注册章坐标");
            TitleTemplateStore.Save(path, library);
            var loaded = TitleTemplateStore.Load(path);
            Check(loaded.SchemaVersion == 1 && loaded.Templates[0].StampRegion!.X1 == -150 &&
                loaded.Templates[0].RegistrationStampRegion!.X1 == -65,
                "双章区域以可选字段在版本1模板库中独立持久化");
            var invalid = TitleTemplateService.CopyLibrary(library);
            invalid.Templates[0].RegistrationStampRegion!.X2 = -100;
            try { TitleTemplateService.Validate(invalid); throw new Exception("无效注册章区域未被拒绝"); }
            catch (InvalidDataException) { Check(true, "注册章区域与主章一样拒绝反向或零宽高坐标"); }

            byte[] beforeEditor = File.ReadAllBytes(path);
            using (var editor = new TitleTemplateForm(library, path, () => null, frame => null))
            {
                var mainDraft = Field<TemplateRegion>(editor, "stampRegion");
                var registrationDraft = Field<TemplateRegion>(editor, "registrationStampRegion");
                Check(!ReferenceEquals(mainDraft, first.StampRegion) && !ReferenceEquals(registrationDraft, first.RegistrationStampRegion) &&
                    !ReferenceEquals(mainDraft, registrationDraft), "双章编辑器的两个区域草稿互不共享，也不共享原始库");
                mainDraft.X1 = -145; registrationDraft.Y1 = 25;
                Check(first.StampRegion.X1 == -150 && first.RegistrationStampRegion.Y1 == 20 && File.ReadAllBytes(path).SequenceEqual(beforeEditor),
                    "修改双章编辑草稿不提前写磁盘或污染调用方模板");
                InvokeStampEditor(editor, "CommitEditor");
                Check(editor.Library.Templates[0].StampRegion!.X1 == -145 && editor.Library.Templates[0].RegistrationStampRegion!.Y1 == 25,
                    "提交模板同时保存主章与注册章区域");
                mainDraft.X1 = -135; registrationDraft.Y1 = 30;
                Check(editor.Library.Templates[0].StampRegion!.X1 == -145 && editor.Library.Templates[0].RegistrationStampRegion!.Y1 == 25,
                    "提交后草稿仍与双章模板对象隔离");
                Field<ListBox>(editor, "templates").SelectedIndex = 1;
                Check(Field<TemplateRegion>(editor, "registrationStampRegion").X1 == -50 &&
                    typeof(TitleTemplateForm).GetField("stampRegion", PrivateInstance)!.GetValue(editor) == null,
                    "切换模板载入注册章并清除缺省主章，不继承上一模板区域");
                Field<ListBox>(editor, "templates").SelectedIndex = 0;
                Check(Field<TemplateRegion>(editor, "stampRegion").X1 == -135 &&
                    Field<TemplateRegion>(editor, "registrationStampRegion").Y1 == 30,
                    "切回模板保留已提交的两个区域草稿");
                InvokeStampEditor(editor, "CopyTemplate");
                var duplicate = editor.Library.Templates.Last();
                Field<TemplateRegion>(editor, "registrationStampRegion").X1 = -55;
                InvokeStampEditor(editor, "CommitEditor");
                Check(editor.Library.Templates.Last().RegistrationStampRegion!.X1 == -55 &&
                    editor.Library.Templates[0].RegistrationStampRegion!.X1 == -65 && duplicate.Id != first.Id,
                    "界面复制模板后的注册章编辑不会改动原模板");
                editor.Size = editor.MinimumSize;
                RenderStampWindow(editor, "ui-dual-stamp-template.png");
                var panel = editor.Controls.OfType<FlowLayoutPanel>().Single();
                var buttons = panel.Controls.OfType<Button>().Where(b => b.Text.Contains("印章区域") || b.Text.Contains("注册章区域")).ToList();
                Check(buttons.Count == 2 && buttons.All(b => b.Visible && panel.ClientRectangle.Contains(b.Bounds)),
                    "最小模板窗口完整显示主印章区域与注册章区域两个独立按钮");
                InvokeStampEditor(editor, "SaveLibrary");
                Check(editor.DialogResult == DialogResult.OK && TitleTemplateStore.Load(path).Templates.Last().RegistrationStampRegion!.X1 == -55,
                    "保存并关闭实际持久化双章模板编辑结果");
            }
            Check(first.StampRegion.X1 == -150 && first.RegistrationStampRegion.Y1 == 20,
                "整个编辑保存流程不修改传入的原始模板对象");

            string legacyPath = Path.Combine(directory, "legacy.json");
            var legacy = new TitleTemplateLibrary { Templates = new() { new TitleBlockTemplate { Name = "旧单章模板", BlockName = "LEGACY",
                StampRegion = new TemplateRegion { X1 = -70, Y1 = 10, X2 = -10, Y2 = 50 } } } };
            TitleTemplateStore.Save(legacyPath, legacy);
            Check(!File.ReadAllText(legacyPath).Contains("RegistrationStampRegion") &&
                TitleTemplateStore.Load(legacyPath).Templates.Single().RegistrationStampRegion == null,
                "旧单章库不写入空的新字段，并按空注册章区域兼容加载");
        }
        finally { Directory.Delete(directory, true); }
    }
}

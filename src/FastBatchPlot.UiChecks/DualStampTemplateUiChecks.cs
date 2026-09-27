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
                SetPrivate(editor, "showEntry", new Func<FrameEntryForm, DialogResult>(entry =>
                {
                    InvokePrivate(entry, "ApplyNamingRule", "A-C");
                    InvokePrivate(entry, "Accept");
                    return entry.DialogResult;
                }));
                InvokePrivate(editor, "EditFrame", 0);
                Check(File.ReadAllBytes(path).SequenceEqual(beforeEditor) && first.NamingTemplate == null,
                    "修改双章图框草稿不提前写磁盘或污染调用方模板");
                var edited = editor.Library.Templates[0];
                Check(edited.NamingTemplate == "{图号}-{图名}" && edited.StampRegion!.X1 == -150 && edited.RegistrationStampRegion!.X1 == -65
                    && !ReferenceEquals(edited.StampRegion, first.StampRegion) && !ReferenceEquals(edited.RegistrationStampRegion, first.RegistrationStampRegion),
                    "图框信息库修改其他项目时保留双章区域的独立副本");
                InvokePrivate(editor, "SaveAndClose");
                var saved = TitleTemplateStore.Load(path);
                Check(editor.DialogResult == DialogResult.OK && saved.Templates[0].StampRegion!.X1 == -150 && saved.Templates[1].RegistrationStampRegion!.X1 == -50,
                    "确定保存后双章区域原样持久化");
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

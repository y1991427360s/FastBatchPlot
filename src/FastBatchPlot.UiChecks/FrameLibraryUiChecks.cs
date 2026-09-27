using System.Drawing;
using System.Reflection;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.UI.Views;

internal static partial class Program
{
    private static void CheckFrameLibraryUi()
    {
        string directory = Path.Combine(Path.GetTempPath(), "FrameLibraryUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var existing = new TitleBlockTemplate { Name = "电气", BlockName = "A1框", Priority = 3, NamingTemplate = "{图号}-{图名}",
                Fields = new() { new TitleFieldRule { Field = TitleField.DrawingName, AttributeTag = "TITLE" }, new TitleFieldRule { Field = TitleField.DrawingNo, AttributeTag = "NO" } },
                StampRegion = new TemplateRegion { X1 = -90, Y1 = 20, X2 = -10, Y2 = 60 }, Catalog = new CatalogOptions { Title = "专用目录" } };
            var library = new TitleTemplateLibrary { Templates = new() { existing } };
            string path = Path.Combine(directory, "title-templates.json");
            TitleTemplateStore.Save(path, library);
            byte[] before = File.ReadAllBytes(path);
            var found = new List<string>(); var picked = new List<PlotFrame>(); int frameReads = 0;
            PlotFrame Sample(string block, string handle) => new PlotFrame { Type = FrameType.BlockReference, SourceBlockName = block, HandleOrId = handle, MaxX = 59400, MaxY = 42000 };
            using var editor = new TitleTemplateForm(library, path,
                block => { found.Add(block); return Sample(block, "found"); },
                () => Sample("A2新框", "new"),
                frame => { picked.Add(frame); return new TemplateRegion { X1 = -180, Y1 = 8, X2 = -60, Y2 = 22 }; },
                frame => { frameReads++; return new TemplateRegion { X1 = -594, Y1 = 0, X2 = 0, Y2 = 420 }; });
            ShowFrameLibraryOffscreen(editor);
            SetPrivate(editor, "confirm", new Func<string, bool>(_ => true));
            var grid = Field<DataGridView>(editor, "frames");
            string[] headers = { "序号", "图框对应的图块", "对应纸张", "排序优先级别", "删除", "文件命名规则", "图号(A)", "版次(B)", "图名(C)", "日期(D)", "信息1(E)", "信息2(F)" };
            Check(grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText).SequenceEqual(headers),
                "图框信息库列与原版一致：序号、图块、纸张、优先级别、删除、命名规则及 A～F 信息框");
            Check(grid.Columns.Cast<DataGridViewColumn>().All(c => c.SortMode == DataGridViewColumnSortMode.NotSortable) && grid.ReadOnly,
                "图框信息库只读且禁止表头排序，序号与记录保持对应");
            Check(RowText(grid, 0).SequenceEqual(new[] { "1", "A1框", "自动识别", "3", "删除", "A-C", "√", "", "√", "", "", "" }),
                "已录图框一行显示块名、纸张、优先级别、字母命名规则和已录信息框");
            var buttons = FrameLibraryControls(editor).OfType<Button>().Where(b => b.Visible).Select(b => b.Text.Replace(" ", "")).ToList();
            Check(buttons.SequenceEqual(new[] { "录入新图框", "导出设置", "导入设置", "确定", "取消" }),
                "底部只保留录入新图框、导出设置、导入设置、确定、取消五个按钮");

            SetPrivate(editor, "showEntry", new Func<FrameEntryForm, DialogResult>(entry =>
            {
                var draft = entry.Template;
                Check(entry.Text == "录入新图框" && draft.BlockName == "A2新框" && draft.Priority == 2 && draft.PaperWidth == 594 && draft.PaperHeight == 420
                    && draft.PrintRegion!.X1 == -594 && draft.PrintRegion.Y2 == 420 && draft.NamingTemplate == null,
                    "录入新图框按所选图块识别 A2、默认优先级别 2，并登记图框自身为打印范围");
                Check(Field<Label>(entry, "paper").Text.StartsWith("A2") && Field<Label>(entry, "naming").Text.Contains("未定义")
                    && Field<CheckBox>(entry, "printCheck").Checked, "录入窗口显示识别纸张、未定义命名规则和已登记打印范围");
                InvokePrivate(entry, "PickField", TitleField.DrawingNo);
                InvokePrivate(entry, "ApplyNamingRule", "A-C");
                Check(draft.NamingTemplate == "{图号}-{图名}" && Field<Dictionary<TitleField, CheckBox>>(entry, "fieldChecks")[TitleField.DrawingNo].Checked
                    && picked.Last().HandleOrId == "new" && found.Count == 0, "点取图号范围直接使用刚选的图框，字母命名规则转换保存");
                Field<ComboBox>(entry, "priority").Text = "abc";
                Reject(() => InvokePrivate(entry, "Accept"), "排序优先级别不是整数时拒绝确定");
                Check(entry.DialogResult == DialogResult.None, "输入错误时录入窗口保持打开");
                Field<ComboBox>(entry, "priority").Text = "2";
                InvokePrivate(entry, "Accept");
                return entry.DialogResult;
            }));
            InvokePrivate(editor, "NewFrame");
            Check(grid.Rows.Count == 2 && RowText(grid, 1).SequenceEqual(new[] { "2", "A2新框", "A2", "2", "删除", "A-C", "√", "", "", "", "", "" }) && frameReads == 1,
                "确定后新图框加入列表：A2、优先级别 2、命名 A-C、图号已录入");
            var added = editor.Library.Templates[1];
            Check(added.Name == "A2新框" && added.Fields.Single().Region.X1 == -180 && added.Fields.Single().AttributeTag == "", "新图框保存点取的本地右下角区域");
            Check(File.ReadAllBytes(path).SequenceEqual(before) && library.Templates.Count == 1, "点确定前不写模板库，也不修改调用方原库");

            SetPrivate(editor, "showEntry", new Func<FrameEntryForm, DialogResult>(entry =>
            {
                Check(entry.Text.StartsWith("修改图框") && entry.Template.Id == added.Id && Field<Label>(entry, "status").Text.Contains("已录入"),
                    "再次选择同块同纸张图框时打开原记录修改，不重复录入");
                return DialogResult.Cancel;
            }));
            InvokePrivate(editor, "NewFrame");
            Check(grid.Rows.Count == 2, "取消修改时图框信息库不变");

            SetPrivate(editor, "showEntry", new Func<FrameEntryForm, DialogResult>(entry =>
            {
                InvokePrivate(entry, "PickField", TitleField.DrawingName);
                InvokePrivate(entry, "ApplyPaper", 841.0, 594.0);
                Field<ComboBox>(entry, "priority").Text = "1";
                InvokePrivate(entry, "Accept");
                return entry.DialogResult;
            }));
            InvokePrivate(editor, "EditFrame", 0);
            var edited = editor.Library.Templates[0];
            var title = edited.Fields.Single(f => f.Field == TitleField.DrawingName);
            Check(found.SequenceEqual(new[] { "A1框" }) && picked.Last().HandleOrId == "found" && title.Region.X1 == -180 && title.AttributeTag == "",
                "修改已录图框时按块名自动找样本，点取区域取代原属性标签规则");
            Check(edited.Fields.Single(f => f.Field == TitleField.DrawingNo).AttributeTag == "NO" && edited.Priority == 1 && edited.PaperWidth == 841
                && edited.StampRegion!.X1 == -90 && edited.Catalog!.Title == "专用目录" && edited.Id == existing.Id && edited.Name == "电气",
                "修改只更新改动项，保留其他字段、印章区域、专用目录、名称和标识");
            Check(RowText(grid, 0)[2] == "A1" && RowText(grid, 0)[3] == "1", "修改后列表刷新纸张和优先级别");

            SetPrivate(editor, "showEntry", new Func<FrameEntryForm, DialogResult>(entry =>
            {
                var source = Field<IReadOnlyList<TitleBlockTemplate>>(entry, "recorded").Single();
                InvokePrivate(entry, "CopyFrom", source);
                Check(entry.Template.NamingTemplate == source.NamingTemplate && entry.Template.Fields.Count == 2
                    && !ReferenceEquals(entry.Template.Fields[0].Region, source.Fields[0].Region) && entry.Template.PaperWidth == 594,
                    "从已录图框复制命名规则和信息框（独立副本），不改纸张");
                InvokePrivate(entry, "ClearField", TitleField.DrawingNo);
                InvokePrivate(entry, "ClearPrintRange");
                Check(!FrameLibrary.HasField(entry.Template, TitleField.DrawingNo) && entry.Template.PrintRegion == null
                    && !Field<CheckBox>(entry, "printCheck").Checked, "勾选框可清除信息框和打印范围");
                return DialogResult.Cancel;
            }));
            InvokePrivate(editor, "EditFrame", 1);
            Check(FrameLibrary.HasField(editor.Library.Templates[1], TitleField.DrawingNo) && editor.Library.Templates[1].PrintRegion != null
                && editor.Library.Templates[0].Fields.Count == 2, "取消录入窗口不改变原记录");

            string tk = Path.Combine(directory, "export.tk"), json = Path.Combine(directory, "export.json");
            InvokePrivate(editor, "ExportTo", tk); InvokePrivate(editor, "ExportTo", json);
            var fromTk = (TitleTemplateLibrary)InvokeStaticPrivate(typeof(TitleTemplateForm), "LoadSettings", tk)!;
            var tkFrame = fromTk.Templates.Single(t => t.BlockName == "A2新框");
            Check(fromTk.Templates.Count == 2 && tkFrame.PaperWidth == 594 && tkFrame.PaperHeight == 420 && tkFrame.NamingTemplate == "{图号}-{图名}" && tkFrame.Priority == 2
                && Math.Abs(tkFrame.Fields.Single().Region.X1 + 180) < 1e-6 && Math.Abs(tkFrame.Fields.Single().Region.Y2 - 22) < 1e-6
                && Math.Abs(tkFrame.PrintRegion!.X1 + 594) < 1e-6, "导出 .tk 再读取保留图块、纸张、命名规则、信息框和图框范围");
            var fromJson = (TitleTemplateLibrary)InvokeStaticPrivate(typeof(TitleTemplateForm), "LoadSettings", json)!;
            Check(fromJson.Templates.Select(t => t.Id).SequenceEqual(editor.Library.Templates.Select(t => t.Id))
                && fromJson.Templates[0].Fields.Single(f => f.Field == TitleField.DrawingNo).AttributeTag == "NO", "导出 JSON 完整保留标识与属性标签规则");
            RejectStampEditor(() => InvokeStaticPrivate(typeof(TitleTemplateForm), "LoadSettings", Path.Combine(directory, "missing.tk")),
                typeof(FileNotFoundException), "导入不存在的文件时报告而不是导入空库");
            InvokePrivate(editor, "ApplyImport", fromTk, false, "export.tk");
            Check(grid.Rows.Count == 2 && editor.Library.Templates.Select(t => t.Name).SequenceEqual(fromTk.Templates.Select(t => t.Name)), "导入设置选择替换时只保留导入的图框");
            InvokePrivate(editor, "ApplyImport", fromJson, true, "export.json");
            Check(grid.Rows.Count == 2 && editor.Library.Templates[0].Fields.Single(f => f.Field == TitleField.DrawingNo).AttributeTag == "NO",
                "合并导入时同一图块同一纸张的记录被替换，不重复增加");

            SetPrivate(editor, "confirm", new Func<string, bool>(_ => false));
            editor.Close();
            Check(editor.Visible && !editor.IsDisposed, "有未保存修改时关闭需确认，选否继续编辑");
            editor.Size = editor.MinimumSize; editor.PerformLayout();
            Check(grid.Width > 500 && grid.Height > 150, "最小窗口保留可用的图框列表区域");
            RenderFrameLibraryWindow(editor, "ui-template-window.png");
            InvokePrivate(editor, "SaveAndClose");
            var saved = TitleTemplateStore.Load(path);
            Check(editor.DialogResult == DialogResult.OK && saved.Templates.Count == 2 && saved.Templates.Any(t => t.BlockName == "A2新框" && t.PaperWidth == 594),
                "确定保存图框信息库并返回成功");

            using (var second = new TitleTemplateForm(saved, null, () => null, _ => null))
            {
                var rows = Field<DataGridView>(second, "frames");
                SetPrivate(second, "confirm", new Func<string, bool>(_ => false));
                InvokePrivate(second, "DeleteFrame", 1);
                Check(rows.Rows.Count == 2, "删除需确认，选否保留记录");
                SetPrivate(second, "confirm", new Func<string, bool>(_ => true));
                InvokePrivate(second, "DeleteFrame", 1);
                Check(rows.Rows.Count == 1 && second.Library.Templates.Single().BlockName == "A1框" && saved.Templates.Count == 2, "确认后删除该图框，只改编辑副本");
            }

            using (var entry = new FrameEntryForm(TitleTemplateService.Clone(existing), Array.Empty<TitleBlockTemplate>(), null, _ => null, () => null, _ => null))
            {
                ShowFrameLibraryOffscreen(entry);
                string[] forbidden = { "印章", "图章", "签章", "注册章" };
                var entryButtons = FrameLibraryControls(entry).OfType<Button>().Where(b => b.Visible).ToList();
                var texts = entryButtons.Select(b => b.Text).ToList();
                Check(texts.Count(t => t.StartsWith("点取")) == 6 && texts.Contains("调整图框打印范围") && texts.Contains("从已录图框复制信息")
                    && texts.Contains("选择对应纸张") && texts.Contains("定义文件命名规则") && !texts.Any(t => forbidden.Any(t.Contains)),
                    "录入窗口含纸张、命名、六个信息框、打印范围和复制信息，不含签章入口");
                Check(entryButtons.All(b => b.Width > 0 && b.Parent!.ClientRectangle.Contains(b.Bounds)), "录入窗口按钮完整可见");
                RenderFrameLibraryWindow(entry, "ui-frame-entry.png");
            }

            using (var naming = new NamingRuleForm("A(B版)(T)_C_RFF"))
            {
                Check(Field<Label>(naming, "preview").Text == "输出文件名示例：建施03(A版)(A1)_一层建筑平面图_RF", "命名规则对话框按原版示例实时预览");
                Field<TextBox>(naming, "rule").Text = " ";
                Check(naming.Rule == "" && Field<Label>(naming, "preview").Text.Contains("通用命名"), "清空命名规则表示使用通用命名");
            }

            using (var paper = new PaperChoiceForm(594, 420))
            {
                var combo = Field<ComboBox>(paper, "papers");
                Check(Equals(combo.SelectedItem, "A2") && paper.PaperWidth == 594 && paper.PaperHeight == 420, "纸张对话框按登记尺寸选中 A2");
                Field<CheckBox>(paper, "portrait").Checked = true;
                Check(paper.PaperWidth == 420 && paper.PaperHeight == 594 && Equals(combo.SelectedItem, "A2"), "切换竖向交换纸张宽高");
                combo.SelectedItem = "A3+1/2";
                Check(paper.PaperWidth == 297 && paper.PaperHeight == 630, "竖向选择加长图幅按竖向填写宽高");
                Field<NumericUpDown>(paper, "width").Value = 600;
                Check(Equals(combo.SelectedItem, "自定义") && paper.PaperWidth == 600, "手工修改尺寸后标记为自定义纸张");
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void SetPrivate(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance)!.SetValue(target, value);
    private static object? InvokePrivate(object target, string name, params object[] args) => target.GetType().GetMethod(name, PrivateInstance)!.Invoke(target, args);
    private static object? InvokeStaticPrivate(Type type, string name, params object[] args)
        => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    private static string[] RowText(DataGridView grid, int row) => grid.Rows[row].Cells.Cast<DataGridViewCell>().Select(c => Convert.ToString(c.Value) ?? "").ToArray();
    private static IEnumerable<Control> FrameLibraryControls(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in FrameLibraryControls(child)) yield return nested;
        }
    }
    private static void ShowFrameLibraryOffscreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000); form.ShowInTaskbar = false; form.Show(); form.PerformLayout();
    }
    private static void RenderFrameLibraryWindow(Form form, string name)
    {
        string evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "audit-evidence"));
        Directory.CreateDirectory(evidence);
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
        image.Save(Path.Combine(evidence, name), System.Drawing.Imaging.ImageFormat.Png);
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using FastBatchPlot.Core.Tasks;
using FastBatchPlot.Core.Templates;
using FastBatchPlot.CadBridge;
using FastBatchPlot.Core.Detection;
using FastBatchPlot.Core.Export;
using FastBatchPlot.Core.Models;
using FastBatchPlot.Core.Naming;
using FastBatchPlot.Core.Paper;
using FastBatchPlot.Core.Pdf;
using FastBatchPlot.Core.Configuration;
using FastBatchPlot.Core.Planning;

namespace FastBatchPlot.UI.Views
{
    public partial class BatchPlotForm : Form
    {
        private readonly List<PlotFrame> _frames = new List<PlotFrame>();
        private readonly PlotConfig _config = new PlotConfig();
        private readonly string? _settingsPath;
        private bool _isUpdatingGrid = false;
        private bool _isPlotting;
        private readonly HashSet<PlotFrame> _manualFileNames = new HashSet<PlotFrame>();
        private readonly List<Control> _taskControls = new List<Control>();
        private CatalogOptions _catalogOptions = new CatalogOptions();

        private string _specifiedBlockName = string.Empty;
        private string _specifiedLayerName = string.Empty;
        private double _minimumAreaPercent;
        private CheckBox chkRemoveDuplicates = null!;
        private CheckBox chkRemoveNestedFrames = null!;
        private Button btnLayerFilter = null!;
        private NumericUpDown numAreaFilter = null!;
        private NumericUpDown numDetectionScale = null!;
        private Button btnPickSample = null!;
        private ComboBox cboDetectMode = null!;

        // UI Controls
        private DataGridView dgvDrawings = null!;
        private ComboBox cboPlotters = null!;
        private ComboBox cboPlotStyles = null!;
        private ComboBox cboSortRule = null!;
        private ComboBox cboScanScope = null!;
        private ComboBox cboOutputMode = null!;
        private NumericUpDown numCopies = null!;
        private TextBox txtOutputFolder = null!;
        private TextBox txtMergedFileName = null!;
        private TextBox txtNamingTemplate = null!;
        private CheckBox chkMergePdf = null!;
        private CheckBox chkOverwrite = null!;
        private Button btnPdfParameters = null!;
        private CheckBox chkPrintSignatures = null!;
        private CheckBox chkPrintStamps = null!;
        private NumericUpDown numMargin = null!;
        private Button btnStartPlot = null!;
        private Button btnSelectFrames = null!;
        private Button btnAutoDetect = null!;
        private Button btnExportCatalog = null!;
        private Button btnBrowseOutput = null!;
        private Label lblStatus = null!;
        private Label lblStats = null!;
        private ContextMenuStrip ctxMenu = null!;

        public BatchPlotForm() : this(CadHostProvider.IsInitialized
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FastBatchPlot", "settings.json") : null)
        {
        }

        public BatchPlotForm(string? settingsPath)
        {
            FastBatchPlot.Core.Common.LegacyDependencyResolution.EnsureInitialized();
            _settingsPath = settingsPath;
            InitializeComponent();
            LoadPrintersAndStyles();
            RestorePreferences();
            LoadTitleTemplates();
            UpdateStats();
            FormClosing += (s, e) =>
            {
                if (_isPlotting) e.Cancel = true;
            };
            FormClosed += (s, e) =>
            {
                (CadHostProvider.Host as ICadHighlightHost)?.ClearFrameMarkers();
            };
        }

        private void InitializeComponent()
        {
            this.Text = _baseTitle;
            this.Size = new Size(1140, 740);
            this.MinimumSize = new Size(980, 740);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = Color.FromArgb(248, 249, 250);

            // 主面板
            var pnlMain = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            // 顶部工具栏
            var pnlTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 126,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true
            };

            cboDetectMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 145 };
            cboDetectMode.Items.AddRange(new object[] { "批打(通用型)", "批打(图框型)" });
            cboDetectMode.SelectedIndex = 0;
            cboDetectMode.SelectedIndexChanged += (s, e) =>
            {
                if (cboDetectMode.SelectedIndex == 0)
                {
                    lblStatus.Text = "已切换为【通用型】：无需预设图框，自动识别框选或全图范围内所有符合图纸规格的矩形与图块。";
                }
                else
                {
                    lblStatus.Text = $"已切换为【图框型】：仅识别已导入模板库中的图框（当前已导入 {_titleTemplates.Templates.Count} 个模板）。";
                }
            };
            btnSinglePdf = CreateButton("单张 PDF…", async (s,e) => await ShowSinglePdfOptions());
            btnDetectionReport = CreateButton("识别报告", (s,e) => ShowDetectionReport());
            btnAutoDetect = CreateButton("🔍 搜索图框", (s, e) => AutoDetectFrames());
            cboScanScope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            cboScanScope.Items.AddRange(new object[] { "当前空间", "仅模型", "所有布局", "模型和布局" });
            cboScanScope.SelectedIndex = 0;
            cboScanScope.SelectedIndexChanged += (s, e) =>
            {
                lblStatus.Text = "搜索范围将在下次搜索时生效；框选和手工添加仍使用当前空间。";
            };
            btnSelectFrames = CreateButton("📐 从图中框选", (s, e) => PickFramesFromCad());
            btnPickSample = CreateButton("🎯 指定图框图块", (s, e) => PickSampleBlockFromCad());
            var btnClearFilter = CreateButton("清除块/层筛选", (s, e) =>
            {
                _specifiedBlockName = string.Empty;
                _specifiedLayerName = string.Empty;
                btnLayerFilter.Text = "按图层筛选";
                btnPickSample.Text = "🎯 指定图框图块";
                btnPickSample.BackColor = Color.White;
                lblStatus.Text = "已清除图块及图层筛选，请重新搜索或框选。";
            });
            var btnWindow = CreateButton("两点添加图纸", (s, e) => AddManualFrame(ManualFrameSelectionMode.TwoCorners));
            var btnGroup = CreateButton("选择图集添加", (s, e) => AddManualFrame(ManualFrameSelectionMode.EntityGroup));
            btnLayerFilter = CreateButton("按图层筛选", (s,e) => PickLayerFilter());
            chkRemoveDuplicates = new CheckBox { Text="过滤重复框", Checked=true, AutoSize=true, Padding=new Padding(0,6,0,0) };
            chkRemoveNestedFrames = new CheckBox { Text="过滤内含框", Checked=true, AutoSize=true, Padding=new Padding(0,6,0,0) };
            chkRemoveDuplicates.CheckedChanged += (s,e) => lblStatus.Text="重复框过滤将在下次搜索或框选时生效。";
            chkRemoveNestedFrames.CheckedChanged += (s,e) => lblStatus.Text="内含框过滤将在下次搜索或框选时生效。";
            numAreaFilter = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 1, Width = 65 };
            numAreaFilter.ValueChanged += (s,e) => { _minimumAreaPercent = (double)numAreaFilter.Value; lblStatus.Text = "面积阈值将在下次搜索或框选时生效，0表示不按面积过滤。"; };
            numDetectionScale=new NumericUpDown{Minimum=0,Maximum=1000000,DecimalPlaces=3,Increment=25,Width=90};
            numDetectionScale.ValueChanged+=(s,e)=>{lblStatus.Text="识别比例用于下次搜索/框选：0 自动；指定值按范围生成纸张（含非标）。模板明确比例优先。";};
            var btnSort = CreateButton("重排", (s, e) => ReorderFrames());
            btnExportCatalog = CreateButton("导出目录", (s, e) => ExportCatalog());
            var btnSelectAll = CreateButton("全选/全消", (s, e) => ToggleSelectAll());
            var btnInvertSelect = CreateButton("反选", (s, e) => InvertSelection());

            var lblSort = new Label { Text = " 排序规则:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            cboSortRule = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            cboSortRule.Items.AddRange(new object[] { "从左到右，从上到下", "从上到下，从左到右", "按图号自然排序", "模板优先级，再按图号", "手动顺序" });
            cboSortRule.SelectedIndex = 0;
            cboSortRule.SelectedIndexChanged += (s, e) => ReorderFrames();

            pnlTop.Controls.AddRange(new Control[] { new Label { Text = "模式:", AutoSize = true, Padding = new Padding(0,8,0,0) }, cboDetectMode, cboScanScope, btnAutoDetect, btnSinglePdf, btnDetectionReport, btnSelectFrames, btnWindow, btnGroup, btnPickSample, btnLayerFilter, btnClearFilter,
                new Label { Text = "面积过滤(%)", AutoSize = true, Padding = new Padding(0,8,0,0) }, numAreaFilter, chkRemoveDuplicates, chkRemoveNestedFrames,
                new Label { Text = "识别比例 1:（0自动）", AutoSize=true,Padding=new Padding(0,8,0,0)},numDetectionScale,
                btnSelectAll, btnInvertSelect, lblSort, cboSortRule, btnSort, btnExportCatalog });
            pnlTop.Controls.Add(CreateButton("图框模板", (s,e) => ManageTitleTemplates()));
            pnlTop.Controls.Add(CreateButton("提取图框信息", (s,e) => ExtractTitleTemplates()));
            // 不常用的功能收进“更多”，保证最小窗口下工具栏三行即可完整显示。
            var moreMenu = new ContextMenuStrip();
            moreMenu.Items.Add("保存为默认设置", null, (s,e) => SavePreferences());
            moreMenu.Items.Add("PDF 任务历史（失败页重试 / 重新合并）…", null, (s,e) => ShowPdfTaskHistory());
            moreMenu.Items.Add("打开错误日志目录", null, (s,e) => OpenLogFolder());
            var btnMore = CreateButton("更多 ▾", (s,e) => { });
            btnMore.Click += (s,e) => moreMenu.Show(btnMore, new Point(0, btnMore.Height));
            pnlTop.Controls.Add(btnMore);
            cboOutputMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 96, Dock = DockStyle.Fill };
            cboOutputMode.Items.AddRange(new object[] { "PDF", "DWF", "PLT", "PNG", "JPG", "EPS", "SVG", "实体打印机" });
            numCopies = new NumericUpDown { Minimum = 1, Maximum = 999, Value = 1, Width = 55, Enabled = false, Dock = DockStyle.Fill };
            chkPrintSignatures = new CheckBox { Text = "签名层输出", Checked = true, AutoSize = true, Padding = new Padding(3, 7, 3, 0) };
            chkPrintStamps = new CheckBox { Text = "印章输出", Checked = true, AutoSize = true, Padding = new Padding(3, 7, 3, 0) };
            _taskControls.Add(pnlTop);

            // 右键菜单
            ctxMenu = new ContextMenuStrip();
            ctxMenu.Items.Add("单张 PDF 输出…", null, async (s,e) => await ShowSinglePdfOptions());
            ctxMenu.Items.Add("最近单张 PDF（打开/另存）", null, (s,e) => { if(File.Exists(_lastSinglePdfPath)) ShowSinglePdfResult(_lastSinglePdfPath, ""); else lblStatus.Text="本次会话尚无单张 PDF，或文件已被移走。"; });
            ctxMenu.Items.Add("打印预览（选中一行）", null, (s,e) => PreviewSelectedFrame());
            ctxMenu.Items.Add("🔍 在CAD中定位此图 (双击)", null, (s, e) => LocateSelectedFrameInCad());
            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("☑ 仅勾选所选行", null, (s, e) => SelectOnlyHighlightedRows());
            ctxMenu.Items.Add("🗑 从列表中移除选中图纸", null, (s, e) => RemoveSelectedRows());
            ctxMenu.Items.Add(new ToolStripSeparator());

            var mnuPaper = new ToolStripMenuItem("📐 批量修改图幅");
            mnuPaper.DropDownItems.Add("A0", null, (s, e) => BatchSetPaper("A0"));
            mnuPaper.DropDownItems.Add("A1", null, (s, e) => BatchSetPaper("A1"));
            mnuPaper.DropDownItems.Add("A2", null, (s, e) => BatchSetPaper("A2"));
            mnuPaper.DropDownItems.Add("A3", null, (s, e) => BatchSetPaper("A3"));
            mnuPaper.DropDownItems.Add("A4", null, (s, e) => BatchSetPaper("A4"));
            mnuPaper.DropDownItems.Add(new ToolStripSeparator());
            mnuPaper.DropDownItems.Add("A3+1", null, (s, e) => BatchSetPaper("A3+1"));
            mnuPaper.DropDownItems.Add("A2+1", null, (s, e) => BatchSetPaper("A2+1"));
            mnuPaper.DropDownItems.Add(new ToolStripSeparator());
            mnuPaper.DropDownItems.Add("自定义尺寸与比例…", null, (s, e) => EditCustomPaper());
            ctxMenu.Items.Add(mnuPaper);

            var mnuScale = new ToolStripMenuItem("📏 批量修改出图比例");
            mnuScale.DropDownItems.Add("1:50", null, (s, e) => BatchSetScale(50));
            mnuScale.DropDownItems.Add("1:100", null, (s, e) => BatchSetScale(100));
            mnuScale.DropDownItems.Add("1:150", null, (s, e) => BatchSetScale(150));
            mnuScale.DropDownItems.Add("1:200", null, (s, e) => BatchSetScale(200));
            ctxMenu.Items.Add(mnuScale);

            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("重编选中行图号（预览）", null, (s,e) => RenumberSelected(false));
            ctxMenu.Items.Add("重编全部图号（预览）", null, (s,e) => RenumberSelected(true));
            ctxMenu.Items.Add("撤销最近一次图号重编", null, (s,e) => UndoRenumber());
            ctxMenu.Items.Add("上移选中行", null, (s,e) => MoveSelectedRows(-1));
            ctxMenu.Items.Add("下移选中行", null, (s,e) => MoveSelectedRows(1));
            ctxMenu.Items.Add("拆分勾选图纸为 DWG（预检）", null, (s,e) => ExportSplitDwg());
            ctxMenu.Items.Add("标题栏写回 DWG（预览）", null, (s,e) => WriteTitlesToDwg());
            ctxMenu.Items.Add("撤销最近一次 DWG 标题写回", null, (s,e) => UndoTitleWrite());
            ctxMenu.Items.Add("应用模板打印范围（勾选图纸）",null,(s,e)=>ApplyTemplatePrintRegions());
            ctxMenu.Items.Add("恢复原图框打印范围（勾选图纸）",null,(s,e)=>ApplyTemplatePrintRegions(true));
            AddConvenienceMenuItems();

            // 绘制图纸列表 DataGridView
            dgvDrawings = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowTemplate = { Height = 28 },
                ContextMenuStrip = ctxMenu
            };

            dgvDrawings.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "打印", Width = 45, DataPropertyName = "IsSelected" });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "序号", Width = 55, ReadOnly = true });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "图号", Width = 140 });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "图面名称", Width = 230 });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "图幅", Width = 90 });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "出图比例", Width = 90 });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "方向", Width = 70, ReadOnly = true });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "输出文件名", Width = 240 });
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", Width = 240, ReadOnly = true });
            // 追加逻辑列后调整展示位置，保留原有编辑列索引和行对象绑定。
            dgvDrawings.Columns.Add(new DataGridViewTextBoxColumn { Name = "LayoutName", HeaderText = "布局", Width = 100, ReadOnly = true });
            dgvDrawings.Columns["LayoutName"]!.DisplayIndex = 2;

            InitializePaperColumns();
            dgvDrawings.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvDrawings.IsCurrentCellDirty)
                {
                    dgvDrawings.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            dgvDrawings.CellValueChanged += OnGridCellValueChanged;
            dgvDrawings.CellValueChanged += (s,e) => { if (!_isUpdatingGrid) RefreshDuplicateIndicators(); };
            dgvDrawings.SelectionChanged += (s, e) =>
            {
                if (_isUpdatingGrid) return;
                if (dgvDrawings.SelectedRows.Count > 0 && dgvDrawings.SelectedRows[0].Tag is PlotFrame f)
                {
                    (CadHostProvider.Host as ICadHighlightHost)?.HighlightCurrentFrame(f);
                }
                else
                {
                    (CadHostProvider.Host as ICadHighlightHost)?.HighlightCurrentFrame(null);
                }
            };
            _taskControls.Add(dgvDrawings);

            // 底部设置面板
            var pnlBottom = new GroupBox
            {
                Text = " 打印输出与格式设置 ",
                Dock = DockStyle.Bottom,
                Height = 260,
                Padding = new Padding(12)
            };

            var gridSettings = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 5
            };
            for (int r = 0; r < 5; r++) gridSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
            gridSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            gridSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            gridSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            gridSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

            // 行1: 打印设备与打印样式表
            gridSettings.Controls.Add(new Label { Text = "打印设备:", Anchor = AnchorStyles.Left }, 0, 0);
            cboPlotters = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            var devicePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, Margin = new Padding(0) };
            devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            devicePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
            devicePanel.Controls.Add(cboPlotters, 0, 0);
            devicePanel.Controls.Add(new Label { Text = "输出", AutoSize = true, Anchor = AnchorStyles.Left }, 1, 0);
            devicePanel.Controls.Add(cboOutputMode, 2, 0);
            devicePanel.Controls.Add(new Label { Text = "份数", AutoSize = true, Anchor = AnchorStyles.Left }, 3, 0);
            devicePanel.Controls.Add(numCopies, 4, 0);
            gridSettings.Controls.Add(devicePanel, 1, 0);

            gridSettings.Controls.Add(new Label { Text = "打印样式(CTB):", Anchor = AnchorStyles.Left }, 2, 0);
            cboPlotStyles = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            gridSettings.Controls.Add(cboPlotStyles, 3, 0);

            // 行2: 输出目录与周边留白
            gridSettings.Controls.Add(new Label { Text = "输出目录:", Anchor = AnchorStyles.Left }, 0, 1);
            var pnlFolder = new Panel { Dock = DockStyle.Fill };
            txtOutputFolder = new TextBox { Dock = DockStyle.Fill, Text = _config.OutputDirectory };
            btnBrowseOutput = new Button { Text = "浏览...", Dock = DockStyle.Right, Width = 70 };
            btnBrowseOutput.Click += (s, e) => BrowseOutputFolder();
            var btnOpenFolder = new Button { Text = "打开", Dock = DockStyle.Right, Width = 52 };
            btnOpenFolder.Click += (s, e) => OpenOutputFolder();
            pnlFolder.Controls.Add(txtOutputFolder);
            pnlFolder.Controls.Add(btnOpenFolder);
            pnlFolder.Controls.Add(btnBrowseOutput);
            gridSettings.Controls.Add(pnlFolder, 1, 1);

            gridSettings.Controls.Add(new Label { Text = "周边留白(mm):", Anchor = AnchorStyles.Left }, 2, 1);
            numMargin = new NumericUpDown { Dock = DockStyle.Fill, Minimum = -50, Maximum = 100, DecimalPlaces = 2, Increment = 0.5m, Value = 0 };
            numMargin.ValueChanged+=(s,e)=>RefreshPaperPlans();
            var marginPanel=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,Margin=new Padding(0) };
            marginPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));marginPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,70));
            btnPageMargins=new Button{Text="四边…",Dock=DockStyle.Fill};btnPageMargins.Click+=(s,e)=>EditPageMargins();
            marginPanel.Controls.Add(numMargin,0,0);marginPanel.Controls.Add(btnPageMargins,1,0);gridSettings.Controls.Add(marginPanel,3,1);
            _taskControls.Add(btnPageMargins);

            // 行3: 命名规则与PDF合并
            gridSettings.Controls.Add(new Label { Text = "命名模板:", Anchor = AnchorStyles.Left }, 0, 2);
            txtNamingTemplate = new TextBox { Dock = DockStyle.Fill, Text = _config.NamingTemplate };
            txtNamingTemplate.TextChanged += (s, e) => RefreshFileNames();
            gridSettings.Controls.Add(txtNamingTemplate, 1, 2);

            chkMergePdf = new CheckBox { Text = "合并为单份PDF(带书签)", AutoSize = true, Checked = true, Margin = new Padding(0, 4, 4, 0) };
            txtMergedFileName = new TextBox { Dock = DockStyle.Fill, Text = "施工图图纸合集.pdf" };
            gridSettings.Controls.Add(chkMergePdf, 2, 2);
            gridSettings.Controls.Remove(chkMergePdf);
            var mergeOptions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, Margin = new Padding(0) };
            mergeOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            mergeOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            mergeOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            mergeOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            var btnBookmarks = new Button { Text = "书签…", Dock = DockStyle.Fill };
            btnBookmarks.Click += (s, e) => EditBookmarks();
            chkOverwrite = new CheckBox { Text = "覆盖同名文件", AutoSize = true, Checked = true, Margin = new Padding(0, 4, 4, 0) };
            new ToolTip().SetToolTip(chkOverwrite, "勾选后重新出图会替换输出目录中的同名文件（先生成并校验新文件再替换）；不勾选则遇到同名文件停止。");
            mergeOptions.Controls.Add(chkMergePdf, 0, 0); mergeOptions.Controls.Add(chkOverwrite, 1, 0); mergeOptions.Controls.Add(btnBookmarks, 2, 0);
            btnPdfParameters = new Button { Text = "PDF 参数…", Dock = DockStyle.Fill };
            btnPdfParameters.Click += (s, e) => EditPdfParameters();
            mergeOptions.Controls.Add(btnPdfParameters, 3, 0);
            gridSettings.Controls.Add(mergeOptions, 2, 2); gridSettings.SetColumnSpan(mergeOptions, 2);
            _taskControls.Add(btnBookmarks);
            _taskControls.Add(btnPdfParameters);
            gridSettings.Controls.Add(new Label { Text = "合并文件名:", Anchor = AnchorStyles.Left }, 0, 3);
            chkOpenFolderWhenDone = new CheckBox { Text = "完成后打开", AutoSize = true, Checked = true, Dock = DockStyle.Right };
            var mergedPanel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            mergedPanel.Controls.Add(txtMergedFileName);
            mergedPanel.Controls.Add(chkOpenFolderWhenDone);
            gridSettings.Controls.Add(mergedPanel, 1, 3);

            // 行4: 状态条与开始打印按钮
            var pnlStatusBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            pnlStatusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            pnlStatusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            lblStatus = new Label { Text = "就绪。右键可修改图幅/比例，双击可定位。", Dock = DockStyle.Fill, AutoEllipsis = true };
            lblStats = new Label { Text = "", Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Color.DarkBlue, Font = new Font(this.Font, FontStyle.Bold) };
            pnlStatusBar.Controls.Add(lblStatus);
            pnlStatusBar.Controls.Add(lblStats);
            gridSettings.Controls.Add(pnlStatusBar, 0, 4);
            gridSettings.SetColumnSpan(pnlStatusBar, 4);

            btnStartPlot = new Button
            {
                Text = "🚀 开始批量打印",
                Dock = DockStyle.Fill,
                Height = 36,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnStartPlot.Click += async (s, e) => await ExecuteBatchPlot();
            btnCancelPlot=new Button{Text="取消批量任务",Dock=DockStyle.Fill,Enabled=false};
            btnCancelPlot.Click+=(s,e)=>RequestPlotCancellation();gridSettings.Controls.Add(btnCancelPlot,2,3);
            gridSettings.Controls.Add(btnStartPlot, 3, 3);
            _taskControls.AddRange(new Control[] { cboOutputMode, numCopies, cboPlotters, cboPlotStyles, txtOutputFolder, btnBrowseOutput, numMargin, txtNamingTemplate, chkMergePdf, chkOverwrite, txtMergedFileName, btnStartPlot });

            cboOutputMode.SelectedIndexChanged += (s,e) => UpdateOutputMode();
            cboOutputMode.SelectedIndex = 0;
            pnlBottom.Controls.Add(gridSettings);

            // 组装主界面
            pnlMain.Controls.Add(dgvDrawings);
            pnlMain.Controls.Add(pnlTop);
            pnlMain.Controls.Add(pnlBottom);
            this.Controls.Add(pnlMain);
        }

        private Button CreateButton(string text, EventHandler onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 32,
                Margin = new Padding(3, 3, 6, 3),
                BackColor = Color.White,
                FlatStyle = FlatStyle.System
            };
            btn.Click += onClick;
            return btn;
        }

        private void EditBookmarks()
        {
            using (var dialog = new BookmarkTemplateForm(_config.BookmarkTemplate, DisplayOrder().Where(f => f.IsSelected).ToList()))
                if (dialog.ShowDialog(this) == DialogResult.OK) _config.BookmarkTemplate = dialog.Value;
        }

        private void EditPdfParameters()
        {
            using (var dialog = new PdfParametersForm(_config.PdfOptions, CadHostProvider.Host?.PlatformName ?? ""))
                if (dialog.ShowDialog(this) == DialogResult.OK) _config.PdfOptions = dialog.Value;
        }

        private void RestorePreferences()
        {
            if (_settingsPath == null || !File.Exists(_settingsPath)) return;
            try
            {
                var preferences = UserSettingsStore.Load(_settingsPath);
                _catalogOptions=preferences.Catalog.Copy();
                txtOutputFolder.Text = preferences.OutputDirectory;
                txtNamingTemplate.Text = preferences.NamingTemplate;
                _config.BookmarkTemplate = preferences.BookmarkTemplate;
                _config.PdfOptions = preferences.PdfOptions.Copy();
                _config.PrintOuterBorderLine=preferences.PrintOuterBorderLine;_config.OuterBorderInsetMm=preferences.OuterBorderInsetMm;
                txtMergedFileName.Text = preferences.MergedFileName;
                chkMergePdf.Checked = preferences.MergeToSinglePdf;
                chkOverwrite.Checked = preferences.OverwriteExisting;
                chkOpenFolderWhenDone.Checked = preferences.OpenOutputWhenDone;
                numMargin.Value = (decimal)preferences.MarginMm;
                _pageMargins=preferences.Margins.Copy();UpdateMarginMode();
                numAreaFilter.Value = (decimal)preferences.MinimumAreaPercent;
                chkRemoveDuplicates.Checked=preferences.RemoveDuplicates;
                chkRemoveNestedFrames.Checked=preferences.RemoveNestedFrames;
                numDetectionScale.Value=(decimal)preferences.DetectionScale;
                cboDetectMode.SelectedIndex = (int)preferences.DetectMode;
                cboSortRule.SelectedIndex = preferences.SortRuleIndex;
                cboOutputMode.SelectedIndex = preferences.SendToPrinter ? 7 : (int)preferences.ExportFormat;
                numCopies.Value = preferences.Copies;
                CaptureStampAsset();
                // 不制造设备选项；已保存设备不在本机时必须重新选择。
                cboPlotters.SelectedIndex = FindItem(cboPlotters, preferences.PrinterDevice);
                cboPlotStyles.SelectedIndex = FindItem(cboPlotStyles, preferences.PlotStyleTable);
                lblStatus.Text = cboPlotters.SelectedIndex < 0 || cboPlotStyles.SelectedIndex < 0
                    ? "设置已加载，但原打印设备或样式不可用，请重新选择。" : "已加载上次保存的默认设置。";
            }
            catch (Exception ex) { lblStatus.Text = "默认设置读取失败，原文件已保留：" + ex.Message; }
        }

        /// <summary>批量任务或单张输出正在执行。</summary>
        public bool IsBusy => _isPlotting;

        /// <summary>命令行已完成框选后载入图框（原版流程：命令 → 选择打印范围 → 对话框）。</summary>
        public void LoadSelectedCandidates(List<RawPolylineCandidate> polylines, List<RawBlockCandidate> blocks, PlotDetectMode? requestedMode = null)
        {
            if (_isPlotting) return;
            if (requestedMode.HasValue) SetDetectMode(requestedMode.Value);
            (CadHostProvider.Host as ICadHighlightHost)?.ClearFrameMarkers();
            try { ApplySelection(polylines, blocks, "框选", requestedMode.HasValue ? requestedMode.Value == PlotDetectMode.Template : (bool?)null); }
            catch (Exception ex) { _lastDetectionReport = "框选失败，原列表已保留：" + ex.Message; lblStatus.Text = "框选失败：" + ex.Message; }
        }

        /// <summary>命令行未选择图框直接回车时，按所选范围遍历模型/布局。</summary>
        public void LoadScopeCandidates(CadScanScope scope)
        {
            if (_isPlotting) return;
            int index = (int)scope;
            if (index >= 0 && index < cboScanScope.Items.Count) cboScanScope.SelectedIndex = index;
            AutoDetectFrames();
        }

        private void ApplySelection(List<RawPolylineCandidate> polyCandidates, List<RawBlockCandidate> blockCandidates, string operation, bool? extractTemplateFields = null)
        {
            var selected = SelectCandidates(polyCandidates, blockCandidates);
            RecordDetectionReport(selected, polyCandidates.Count + blockCandidates.Count, operation, new string[0]);
            _manualFileNames.Clear();
            _frames.Clear();
            _frames.AddRange(selected.Frames);
            ReorderFrames();
            UpdateStats();
            if (extractTemplateFields ?? ((PlotDetectMode)cboDetectMode.SelectedIndex == PlotDetectMode.Template))
                ExtractTitleTemplates();
            else
                lblStatus.Text = $"{operation}识别完成，共识别出 {_frames.Count} 个有效图框。";
        }

        public void SetDetectMode(PlotDetectMode mode)
        {
            if (cboDetectMode != null && !cboDetectMode.IsDisposed)
            {
                int idx = (int)mode;
                if (idx >= 0 && idx < cboDetectMode.Items.Count)
                    cboDetectMode.SelectedIndex = idx;
            }
        }

        private static int FindItem(ComboBox combo, string value)
        {
            for (int i=0; i<combo.Items.Count; i++)
                if (string.Equals(combo.Items[i]?.ToString(),value,StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private void SavePreferences()
        {
            if (_settingsPath == null) { lblStatus.Text = "离线检查模式不写入用户默认设置。"; return; }
            try
            {
                CaptureStampAsset();
                UserSettingsStore.Save(_settingsPath, new BatchPlotPreferences {
                    DetectMode = (PlotDetectMode)cboDetectMode.SelectedIndex,
                    Catalog=_catalogOptions.Copy(),
                    PdfOptions=_config.PdfOptions.Copy(),
                    PrintOuterBorderLine=_config.PrintOuterBorderLine,OuterBorderInsetMm=_config.OuterBorderInsetMm,
                    StampLibraryPath=_config.StampLibraryPath, StampAssetId=_config.StampAssetId,
                    PrintPrimaryStamp=_config.PrintPrimaryStamp,PrintRegistrationStamp=_config.PrintRegistrationStamp,
                    RegistrationStampLibraryPath=_config.RegistrationStampLibraryPath,RegistrationStampAssetId=_config.RegistrationStampAssetId,
                    PrinterDevice = cboPlotters.SelectedItem?.ToString() ?? "",
                    PlotStyleTable = cboPlotStyles.SelectedItem?.ToString() ?? "",
                    OutputDirectory = txtOutputFolder.Text, NamingTemplate = txtNamingTemplate.Text, BookmarkTemplate = _config.BookmarkTemplate,
                    MergedFileName = txtMergedFileName.Text, MergeToSinglePdf = chkMergePdf.Checked, OverwriteExisting = chkOverwrite.Checked, OpenOutputWhenDone = chkOpenFolderWhenDone.Checked,
                    Margins = _pageMargins.Copy(), MarginMm = (double)numMargin.Value, MinimumAreaPercent = (double)numAreaFilter.Value,
                    DetectionScale=(double)numDetectionScale.Value,
                    RemoveDuplicates=chkRemoveDuplicates.Checked,RemoveNestedFrames=chkRemoveNestedFrames.Checked,
                    SortRuleIndex = cboSortRule.SelectedIndex,
                    ExportFormat = cboOutputMode.SelectedIndex == 7 ? PlotExportFormat.PDF : (PlotExportFormat)cboOutputMode.SelectedIndex,
                    SendToPrinter = cboOutputMode.SelectedIndex == 7, Copies = (int)numCopies.Value,
                    PrintSignatures = chkPrintSignatures.Checked, PrintStamps = chkPrintStamps.Checked,
                    SignatureLayerName = _config.SignatureLayerName, StampLayerName = _config.StampLayerName
                });
                lblStatus.Text = "默认设置已保存：" + _settingsPath;
            }
            catch (Exception ex) { lblStatus.Text = "设置未保存，原文件已保留：" + ex.Message; }
        }

        private void LoadPrintersAndStyles()
        {
            if (CadHostProvider.Plotter != null)
            {
                var plotters = CadHostProvider.Plotter.GetAvailablePlotters();
                cboPlotters.Items.AddRange(plotters.ToArray());
                if (cboPlotters.Items.Count > 0)
                {
                    int pdfIdx = -1;
                    // 优先级0：CAD 专属批打印 PDF 驱动（预置全部标准与加长工程图幅）
                    for (int i = 0; i < cboPlotters.Items.Count; i++)
                    {
                        string name = cboPlotters.Items[i]?.ToString() ?? "";
                        if (string.Equals(name, "M_PDF.pc5", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "M_PDF.pc3", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "FBP_PDF.pc5", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(name, "FBP_PDF.pc3", StringComparison.OrdinalIgnoreCase))
                        {
                            pdfIdx = i;
                            break;
                        }
                    }
                    // 优先级1：CAD 原生内置虚拟 PDF 打印机（支持任意自定义加长图幅，不依赖 Windows 系统驱动）
                    if (pdfIdx < 0)
                    {
                        for (int i = 0; i < cboPlotters.Items.Count; i++)
                        {
                            string name = cboPlotters.Items[i]?.ToString() ?? "";
                            if (string.Equals(name, "DWG to PDF.pc5", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(name, "DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(name, "ZWCAD to PDF.pc5", StringComparison.OrdinalIgnoreCase))
                            {
                                pdfIdx = i;
                                break;
                            }
                        }
                    }
                    // 优先级2：任何以 .pc5 或 .pc3 结尾且包含 PDF 的设备
                    if (pdfIdx < 0)
                    {
                        for (int i = 0; i < cboPlotters.Items.Count; i++)
                        {
                            string name = cboPlotters.Items[i]?.ToString() ?? "";
                            if ((name.EndsWith(".pc5", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase)) &&
                                name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                pdfIdx = i;
                                break;
                            }
                        }
                    }
                    // 优先级3：其他包含 PDF 的设备（如 Windows 系统打印机）
                    if (pdfIdx < 0)
                    {
                        for (int i = 0; i < cboPlotters.Items.Count; i++)
                        {
                            string name = cboPlotters.Items[i]?.ToString() ?? "";
                            if (name.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                pdfIdx = i;
                                break;
                            }
                        }
                    }
                    cboPlotters.SelectedIndex = pdfIdx >= 0 ? pdfIdx : 0;
                }

                var styles = CadHostProvider.Plotter.GetAvailablePlotStyles();
                cboPlotStyles.Items.AddRange(styles.ToArray());
                int monoIdx = -1;
                for (int i = 0; i < cboPlotStyles.Items.Count; i++)
                {
                    if (string.Equals(cboPlotStyles.Items[i]?.ToString(), "monochrome.ctb", StringComparison.OrdinalIgnoreCase))
                    {
                        monoIdx = i;
                        break;
                    }
                }
                cboPlotStyles.SelectedIndex = monoIdx >= 0 ? monoIdx : (cboPlotStyles.Items.Count > 0 ? 0 : -1);
            }
            else
            {
                cboPlotters.Items.AddRange(new object[] { "DWG To PDF.pc3", "ZWCAD to PDF.pc5", "Microsoft Print to PDF" });
                cboPlotters.SelectedIndex = 0;
                cboPlotStyles.Items.AddRange(new object[] { "monochrome.ctb", "acad.ctb", "None" });
                cboPlotStyles.SelectedIndex = 0;
            }
        }

        private void AutoDetectFrames()
        {
            if (CadHostProvider.Host == null)
            {
                MessageBox.Show("未连接到CAD宿主进程。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            lblStatus.Text = "正在搜索图纸中的图框...";
            lblStatus.Refresh();

            CadCandidateSnapshot snapshot;
            FrameSelectionResult selected;
            try
            {
                var scope = (CadScanScope)cboScanScope.SelectedIndex;
                if (CadHostProvider.Host is ICadLayoutHost layoutHost)
                    snapshot = layoutHost.CollectCandidates(scope);
                else if (scope != CadScanScope.CurrentSpace)
                    throw new InvalidOperationException("当前宿主不支持跨布局搜索，请选择“当前空间”。原列表已保留。");
                else if (CadHostProvider.Host is ICadFrameSelectionHost selectionHost)
                    snapshot = selectionHost.CollectCurrentSpaceCandidates();
                else
                {
                    snapshot = new CadCandidateSnapshot();
                    snapshot.Polylines.AddRange(CadHostProvider.Host.CollectPolylineCandidates());
                    snapshot.Blocks.AddRange(CadHostProvider.Host.CollectBlockCandidates());
                }
                selected = SelectCandidates(snapshot.Polylines, snapshot.Blocks);
            }
            catch (Exception ex) { _lastDetectionReport = "搜索失败，原列表已保留：" + ex.Message; lblStatus.Text = "搜索失败：" + ex.Message; return; }
            RecordDetectionReport(selected, snapshot.Polylines.Count + snapshot.Blocks.Count, "搜索（" + cboScanScope.SelectedItem + "）", snapshot.Warnings);
            selected.Report.Counts.TryGetValue("重复或嵌套内框", out int duplicateTotal);
            var allFrames = selected.Frames;

            _manualFileNames.Clear();
            _frames.Clear();
            _frames.AddRange(allFrames);

            ReorderFrames();
            UpdateStats();
            if ((PlotDetectMode)cboDetectMode.SelectedIndex == PlotDetectMode.Template)
                ExtractTitleTemplates();
            else
                lblStatus.Text = $"搜索完成（{cboScanScope.SelectedItem}）：{_frames.Count} 张，重复 {duplicateTotal} 个，面积过滤 {selected.AreaFilteredCount} 个，采集警告 {snapshot.Warnings.Count} 个。详情见识别报告。";
        }

        private void PickSampleBlockFromCad()
        {
            if (CadHostProvider.Host == null)
            {
                MessageBox.Show("未连接到CAD宿主进程。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            this.Hide();
            try
            {
                bool success = CadHostProvider.Host.PromptSelectSampleBlock(out var blkName, out var layerName);
                if (success && !string.IsNullOrEmpty(blkName))
                {
                    _specifiedBlockName = blkName;
                    btnPickSample.Text = $"🎯 指定图框: {blkName}";
                    btnPickSample.BackColor = Color.LightYellow;

                    // 如果当前列表有图纸，立即按照该图块过滤
                    if (_frames.Count > 0)
                    {
                        var filtered = _frames.Where(f => f.Type == FrameType.BlockReference && string.Equals(f.SourceBlockName, _specifiedBlockName, StringComparison.OrdinalIgnoreCase)).ToList();
                        _frames.Clear();
                        _frames.AddRange(filtered);
                        ReorderFrames();
                        UpdateStats();
                    }

                    lblStatus.Text = $"已指定目标图框块名：[{blkName}]，框选或全图搜索将仅提取该图框。";
                }
                else
                {
                    lblStatus.Text = "点选取消或所选实体不是图块。";
                }
            }
            catch (Exception ex) { lblStatus.Text = "指定图框图块失败：" + ex.Message; }
            finally
            {
                this.Show();
                this.BringToFront();
            }
        }

        private void PickFramesFromCad()
        {
            if (CadHostProvider.Host == null)
            {
                MessageBox.Show("未连接到CAD宿主进程。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            (CadHostProvider.Host as ICadHighlightHost)?.ClearFrameMarkers();
            this.Hide();
            try
            {
                bool success = CadHostProvider.Host.PromptSelectFrames(out var polyCandidates, out var blockCandidates);
                if (success)
                {
                    ApplySelection(polyCandidates, blockCandidates, "框选");
                }
                else
                {
                    _lastDetectionReport = "框选取消或未选择有效候选；原列表已保留。本次没有识别报告。";
                    lblStatus.Text = "未框选到有效的图框或取消了选择。";
                }
            }
            catch (Exception ex) { _lastDetectionReport = "框选失败，原列表已保留：" + ex.Message; lblStatus.Text = "框选失败：" + ex.Message; }
            finally
            {
                this.Show();
                this.BringToFront();
            }
        }

        // 显式块名筛选严格执行；默认联合候选，只消除同位置或近似大小的嵌套边框。
        private static List<PlotFrame> CombineAndFilterFrames(List<RawPolylineCandidate> polyCandidates, List<RawBlockCandidate> blockCandidates, string specifiedBlockName = "")
        {
            return FrameSelectionService.Select(BlockFrameDetector.ProcessBlockCandidates(blockCandidates)
                .Concat(PolylineFrameDetector.FilterAndCreateFrames(polyCandidates)),
                new FrameSelectionOptions { BlockName = specifiedBlockName }).Frames;
        }

        private FrameSelectionResult SelectCandidates(List<RawPolylineCandidate> polylines, List<RawBlockCandidate> blocks)
        {
            if(_templateLoadError!=null)throw new InvalidOperationException("模板库无效，搜索未执行："+_templateLoadError);
            // 显式筛选先于模板解析；无关图层/块中的坏模板不能阻止本次搜索。
            var report = new DetectionReport();
            var mode = cboDetectMode != null ? (PlotDetectMode)cboDetectMode.SelectedIndex : PlotDetectMode.Universal;

            var filteredPolys = new List<RawPolylineCandidate>();
            foreach (var p in polylines)
            {
                if (!string.IsNullOrEmpty(_specifiedLayerName) && !string.Equals(p.Layer,_specifiedLayerName,StringComparison.OrdinalIgnoreCase))
                { report.Add("图层筛选",p); continue; }
                if (!string.IsNullOrEmpty(_specifiedBlockName)) { report.Add("块名筛选",p); continue; }
                filteredPolys.Add(p);
            }
            var filteredBlocks = new List<RawBlockCandidate>();
            foreach (var b in blocks)
            {
                if (!string.IsNullOrEmpty(_specifiedLayerName) && !string.Equals(b.Layer,_specifiedLayerName,StringComparison.OrdinalIgnoreCase))
                { report.Add("图层筛选",b); continue; }
                if (!string.IsNullOrEmpty(_specifiedBlockName) && !string.Equals(b.BlockName,_specifiedBlockName,StringComparison.OrdinalIgnoreCase))
                { report.Add("块名筛选",b); continue; }
                filteredBlocks.Add(b);
            }

            if (mode == PlotDetectMode.Template)
            {
                if (_titleTemplates == null || _titleTemplates.Templates.Count == 0)
                {
                    throw new InvalidOperationException("当前图框模板库为空。图框型模式仅识别已导入的图框，请先在【图框模板】中导入图框配置文件（如 *.tk），或切换为【通用型】模式。");
                }
                var templateBlockNames = new HashSet<string>(_titleTemplates.Templates.Select(t => t.BlockName.Trim()), StringComparer.OrdinalIgnoreCase);
                var templateMatchingBlocks = new List<RawBlockCandidate>();
                foreach (var b in filteredBlocks)
                {
                    if (!templateBlockNames.Contains(b.BlockName.Trim()))
                    {
                        report.Add("非已导入图框模板（图框型过滤）", b);
                        continue;
                    }
                    templateMatchingBlocks.Add(b);
                }
                if (templateMatchingBlocks.Count == 0 && TitleTemplateService.DescribeLegacyTkImport(_titleTemplates) is string legacy)
                    throw new InvalidOperationException(legacy);
                var frames = TemplateFrameDetector.Detect(templateMatchingBlocks, _titleTemplates, (frame, region) =>
                    CadHostProvider.Host is ICadTemplateCropHost host ? host.ResolveTemplatePrintBounds(frame, region) : throw new NotSupportedException("当前宿主不支持模板打印范围。"), (double)numDetectionScale.Value, report);
                return FrameSelectionService.Select(frames,
                    new FrameSelectionOptions { BlockName = _specifiedBlockName, LayerName = _specifiedLayerName, MinimumAreaPercent = _minimumAreaPercent, RemoveDuplicates = chkRemoveDuplicates.Checked, RemoveNestedFrames = chkRemoveNestedFrames.Checked }, report);
            }
            else
            {
                var frames = TemplateFrameDetector.Detect(filteredBlocks, _titleTemplates, (frame, region) =>
                    CadHostProvider.Host is ICadTemplateCropHost host ? host.ResolveTemplatePrintBounds(frame, region) : throw new NotSupportedException("当前宿主不支持模板打印范围。"), (double)numDetectionScale.Value, report);
                return FrameSelectionService.Select(frames.Concat(PolylineFrameDetector.CreateCandidates(filteredPolys, (double)numDetectionScale.Value, report)),
                    new FrameSelectionOptions { BlockName = _specifiedBlockName, LayerName = _specifiedLayerName, MinimumAreaPercent = _minimumAreaPercent, RemoveDuplicates = chkRemoveDuplicates.Checked, RemoveNestedFrames = chkRemoveNestedFrames.Checked }, report);
            }
        }

        private void PickLayerFilter()
        {
            if (CadHostProvider.Host == null) return;
            Hide();
            try
            {
                if (CadHostProvider.Host.PromptSelectSampleBlock(out _, out var layer) && !string.IsNullOrEmpty(layer))
                {
                    _specifiedLayerName = layer;
                    btnLayerFilter.Text = "图层：" + layer;
                    lblStatus.Text = "图层筛选将在下次搜索或框选时生效：" + layer;
                }
            }
            catch (Exception ex) { lblStatus.Text = "选择图层失败：" + ex.Message; }
            finally { Show(); BringToFront(); }
        }

        private void AddManualFrame(ManualFrameSelectionMode mode)
        {
            if (!(CadHostProvider.Host is ICadFrameSelectionHost host)) { lblStatus.Text = "当前宿主未提供手工范围选择。"; return; }
            Hide();
            try
            {
                if (host.PromptManualFrame(mode, out var frame) && frame != null)
                {
                    frame.Id = _frames.Count == 0 ? 1 : _frames.Max(f => f.Id) + 1;
                    frame.OrderIndex = _frames.Count + 1;
                    frame.TitleInfo.DrawingNo = frame.OrderIndex.ToString("D2");
                    frame.TitleInfo.DrawingName = "手工图纸_" + frame.OrderIndex.ToString("D2");
                    _frames.Add(frame);
                    ReorderFrames();
                    lblStatus.Text = "已添加范围，可继续添加下一张图纸；自定义尺寸需打印设备提供对应纸张。";
                }
            }
            catch (Exception ex) { lblStatus.Text = "添加范围失败：" + ex.Message; }
            finally { Show(); BringToFront(); }
        }

        private void ReorderFrames()
        {
            var rule = (SortOrderRule)cboSortRule.SelectedIndex;
            var sorted = FrameSorter.Sort(_frames, rule).ToList();
            _frames.Clear();
            _frames.AddRange(sorted);

            RefreshGrid();
            (CadHostProvider.Host as ICadHighlightHost)?.ShowFrameMarkers(_frames);
        }

        private void OnGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdatingGrid || _isPlotting || e.RowIndex < 0 || e.RowIndex >= dgvDrawings.Rows.Count) return;

            var row = dgvDrawings.Rows[e.RowIndex];
            if (!(row.Tag is PlotFrame frame)) return;

            if(e.ColumnIndex==dgvDrawings.Columns["BasePaperSize"]!.Index){ApplyPaperDimensionCell(row,frame);return;}

            switch (e.ColumnIndex)
            {
                case 0: // 打印勾选
                    frame.IsSelected = Convert.ToBoolean(row.Cells[0].Value);
                    UpdateStats();
                    (CadHostProvider.Host as ICadHighlightHost)?.ShowFrameMarkers(_frames);
                    break;

                case 2: // 图号
                    frame.TitleInfo.DrawingNo = row.Cells[2].Value?.ToString() ?? "";
                    UpdateGeneratedFileName(frame, row);
                    break;

                case 3: // 图面名称
                    frame.TitleInfo.DrawingName = row.Cells[3].Value?.ToString() ?? "";
                    UpdateGeneratedFileName(frame, row);
                    break;

                case 4: // 图幅
                    string paperName = row.Cells[4].Value?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(paperName))
                    {
                        if (!TrySetPaper(frame, paperName))
                        {
                            SetCellValue(row, 4, frame.DetectedPaper.Name);
                            lblStatus.Text = "未知图幅，请使用内置标准或加长图幅名称。";
                            break;
                        }
                        UpdateGeneratedFileName(frame, row);
                        RefreshPaperPlan(row,frame);
                        UpdateStats();
                    }
                    break;

                case 5: // 比例
                    string scaleStr = row.Cells[5].Value?.ToString() ?? "";
                    if (scaleStr.Contains(":")) scaleStr = scaleStr.Split(':')[1];
                    if (double.TryParse(scaleStr, out double sc) && sc > 0 && !double.IsInfinity(sc) && !double.IsNaN(sc))
                    {
                        frame.CalculatedScale = sc;
                        UpdateGeneratedFileName(frame, row);
                        RefreshPaperPlan(row,frame);
                    }
                    else
                    {
                        SetCellValue(row, 5, $"1:{frame.CalculatedScale:0.########}");
                        lblStatus.Text = "比例必须是有限正数。";
                    }
                    break;

                case 7: // 输出文件名
                    frame.CustomOutputFileName = row.Cells[7].Value?.ToString() ?? "";
                    _manualFileNames.Add(frame);
                    break;
            }
        }

        private void BatchSetPaper(string paperName)
        {
            if (dgvDrawings.SelectedRows.Count == 0) return;
            foreach (DataGridViewRow row in dgvDrawings.SelectedRows)
            {
                if (row.Tag is PlotFrame frame)
                {
                    if (TrySetPaper(frame, paperName)) SetCellValue(row, 4, frame.DetectedPaper.Name);
                }
            }
            RefreshFileNames();
            RefreshPaperPlans();
            UpdateStats();
        }

        private void BatchSetScale(double scale)
        {
            if (dgvDrawings.SelectedRows.Count == 0) return;
            foreach (DataGridViewRow row in dgvDrawings.SelectedRows)
            {
                if (row.Tag is PlotFrame frame)
                {
                    frame.CalculatedScale = scale;
                    SetCellValue(row, 5, $"1:{scale:0.########}");
                }
            }
            RefreshFileNames();
            RefreshPaperPlans();
        }

        private void SelectOnlyHighlightedRows()
        {
            var selected = new HashSet<PlotFrame>(dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<PlotFrame>());
            foreach (DataGridViewRow row in dgvDrawings.Rows)
            {
                if (!(row.Tag is PlotFrame frame)) continue;
                frame.IsSelected = selected.Contains(frame);
                SetCellValue(row, 0, frame.IsSelected);
            }
            UpdateStats();
            (CadHostProvider.Host as ICadHighlightHost)?.ShowFrameMarkers(_frames);
        }

        private void RemoveSelectedRows()
        {
            var selected = dgvDrawings.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<PlotFrame>().ToList();
            foreach (var frame in selected)
            {
                _frames.Remove(frame);
                _manualFileNames.Remove(frame);
            }
            ReorderFrames();
        }

        private void RefreshFileNames()
        {
            foreach (DataGridViewRow row in dgvDrawings.Rows)
            {
                if (row.Tag is PlotFrame frame) UpdateGeneratedFileName(frame, row);
            }
            RefreshDuplicateIndicators();
        }

        private void SetCellValue(DataGridViewRow row, int column, object value)
        {
            bool wasUpdating = _isUpdatingGrid;
            _isUpdatingGrid = true;
            try { row.Cells[column].Value = value; }
            finally { _isUpdatingGrid = wasUpdating; }
        }

        private void UpdateGeneratedFileName(PlotFrame frame, DataGridViewRow row)
        {
            if (!_manualFileNames.Contains(frame))
                frame.CustomOutputFileName = DrawingNameFormatter.Format(TemplateOutputSettings.Naming(frame,_titleTemplates,txtNamingTemplate.Text), frame);
            SetCellValue(row, 7, frame.CustomOutputFileName);
        }

        private static bool TrySetPaper(PlotFrame frame, string paperName)
        {
            var paper = PaperSize.StandardSizes.FirstOrDefault(p => string.Equals(p.Name, paperName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (paper == null) return false;
            frame.DetectedPaper = new PaperSize(paper.Name, frame.IsLandscape ? paper.LongerEdgeMm : paper.ShorterEdgeMm,
                frame.IsLandscape ? paper.ShorterEdgeMm : paper.LongerEdgeMm, frame.IsLandscape) { StandardName = paper.StandardName };
            return true;
        }

        private void RefreshGrid()
        {
            _isUpdatingGrid = true;
            try
            {
                dgvDrawings.Rows.Clear();
                string template = txtNamingTemplate.Text;

                foreach (var f in _frames)
                {
                    if (!_manualFileNames.Contains(f)) f.CustomOutputFileName = DrawingNameFormatter.Format(TemplateOutputSettings.Naming(f,_titleTemplates,template), f);
                    int rowIndex = dgvDrawings.Rows.Add(
                        f.IsSelected,
                        f.OrderIndex,
                        f.TitleInfo.DrawingNo,
                        f.TitleInfo.DrawingName,
                        f.DetectedPaper.Name,
                        f.CalculatedScale > 0 ? $"1:{f.CalculatedScale:0.########}" : "1:100",
                        f.IsLandscape ? "横向" : "纵向",
                        f.CustomOutputFileName,
                        f.Status,
                        f.LayoutName
                    );
                    dgvDrawings.Rows[rowIndex].Tag = f;
                    dgvDrawings.Rows[rowIndex].Cells[8].ToolTipText = !string.IsNullOrWhiteSpace(f.ErrorMessage) ? f.ErrorMessage : f.Status;
                    dgvDrawings.Rows[rowIndex].Cells[8].Style.ForeColor = StatusColor(f.Status);
                    RefreshPaperPlan(dgvDrawings.Rows[rowIndex],f);
                }

                UpdateStats();
                RefreshDuplicateIndicators();
            }
            finally
            {
                _isUpdatingGrid = false;
            }
        }

        private void LocateSelectedFrameInCad()
        {
            if (dgvDrawings.SelectedRows.Count == 0 || CadHostProvider.Host == null)
            {
                (CadHostProvider.Host as ICadHighlightHost)?.HighlightCurrentFrame(null);
                return;
            }
            if (dgvDrawings.SelectedRows[0].Tag is PlotFrame f)
            {
                if (!(CadHostProvider.Host is ICadContextHost context) || !context.IsFrameContextCurrent(f))
                {
                    lblStatus.Text = "图框来源已变化，请切回原文档和布局，或重新搜索。";
                    (CadHostProvider.Host as ICadHighlightHost)?.HighlightCurrentFrame(null);
                    return;
                }
                CadHostProvider.Host.ZoomToFrame(f.MinX, f.MinY, f.MaxX, f.MaxY);
                (CadHostProvider.Host as ICadHighlightHost)?.HighlightCurrentFrame(f);
                lblStatus.Text = $"已定位至图纸：[{f.OrderIndex}] {f.TitleInfo.DrawingNo} {f.TitleInfo.DrawingName}";
            }
        }

        private void ToggleSelectAll()
        {
            bool anyUnchecked = _frames.Any(f => !f.IsSelected);
            foreach (var f in _frames)
            {
                f.IsSelected = anyUnchecked;
            }
            foreach (DataGridViewRow row in dgvDrawings.Rows)
            {
                SetCellValue(row, 0, anyUnchecked);
            }
            UpdateStats();
            (CadHostProvider.Host as ICadHighlightHost)?.ShowFrameMarkers(_frames);
        }

        private void InvertSelection()
        {
            foreach (var f in _frames)
            {
                f.IsSelected = !f.IsSelected;
            }
            foreach (DataGridViewRow row in dgvDrawings.Rows)
            {
                if (row.Tag is PlotFrame frame) SetCellValue(row, 0, frame.IsSelected);
            }
            UpdateStats();
            (CadHostProvider.Host as ICadHighlightHost)?.ShowFrameMarkers(_frames);
        }

        private void BrowseOutputFolder()
        {
            using var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = txtOutputFolder.Text;
            dlg.Description = "选择批量打印输出保存目录";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                txtOutputFolder.Text = dlg.SelectedPath;
            }
        }

        private void ExportCatalog()
        {
            if (_frames.Count == 0)
            {
                MessageBox.Show("当前列表无图纸。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if(_isPlotting)return;
            dgvDrawings.EndEdit();
            var choices=TemplateOutputSettings.CatalogChoices(DisplayOrder(),_titleTemplates,_catalogOptions);
            using (var options = new CatalogOptionsForm(_catalogOptions,profiles:choices))
            {
                if (options.ShowDialog(this) != DialogResult.OK) return;
                _catalogOptions=options.Options.Copy();
                using (var sfd = new SaveFileDialog { Filter = "Excel 工作簿 (*.xlsx)|*.xlsx|Excel CSV 文件 (*.csv)|*.csv", FileName = "图纸目录.xlsx", AddExtension = true, OverwritePrompt = true })
                {
                    if (sfd.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        if (sfd.FilterIndex == 1) CatalogXlsxExporter.Export(DisplayOrder(), sfd.FileName, options.Options, true);
                        else CatalogExporter.ExportConfiguredCsv(DisplayOrder(), sfd.FileName, options.Options, true);
                        lblStatus.Text = "目录已导出：" + sfd.FileName+"；本次设置已保留，点击保存默认设置可跨会话使用。";
                    }
                    catch (Exception ex) { lblStatus.Text = "目录导出失败，未替换原文件：" + ex.Message; }
                }
            }
        }

        private void UpdateStats()
        {
            int total = _frames.Count;
            int selected = _frames.Count(f => f.IsSelected);
            var stats = CatalogExporter.GetPaperStatistics(_frames.Where(f => f.IsSelected));
            string statDetails = string.Join(", ", stats.Select(kv => $"{kv.Key}: {kv.Value}张"));

            lblStats.Text = $"选中: {selected}/{total} 张" + (string.IsNullOrEmpty(statDetails) ? "" : $" ({statDetails})");
        }

        private static string PlanOutputPath(string directory, string name, HashSet<string> planned, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name == "." || name == ".." || name.EndsWith(".") || Path.IsPathRooted(name))
                throw new InvalidOperationException("输出文件名无效：" + name);
            string stem = name.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem))
                throw new InvalidOperationException("输出文件名属于系统保留名称：" + name);
            string path = Path.GetFullPath(Path.Combine(directory, name));
            if (!string.Equals(Path.GetDirectoryName(path)?.TrimEnd(Path.DirectorySeparatorChar), directory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("输出文件必须位于指定目录内。");
            if (!planned.Add(path)) throw new InvalidOperationException("输出文件名重复：" + name);
            if (Directory.Exists(path)) throw new InvalidOperationException("输出路径与已有文件夹同名：" + path);
            if (File.Exists(path))
            {
                if (!overwrite) throw new InvalidOperationException("输出已存在，请改名、选择其他目录，或勾选“覆盖同名文件”：" + path);
                EnsureWritable(path);
            }
            return path;
        }

        // 覆盖前预检：被 PDF 阅读器打开或只读的旧文件要在提交 CAD 之前报告，而不是打印完才失败。
        private static void EnsureWritable(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) throw new InvalidOperationException("同名文件为只读，无法覆盖：" + path);
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
            catch (IOException) { throw new InvalidOperationException("同名文件正被其他程序占用（可能在 PDF 阅读器中打开），请关闭后重试：" + path); }
            catch (UnauthorizedAccessException) { throw new InvalidOperationException("没有权限覆盖同名文件：" + path); }
        }

        private void SetPlottingState(bool plotting)
        {
            _isPlotting = plotting;
            foreach (var control in _taskControls) control.Enabled = !plotting;
            ctxMenu.Enabled = !plotting;
            if (!plotting) { RestoreOutputControlStates(); UpdateMarginMode(); }
        }

        private static void EnsureFrameContextsAccessible(IEnumerable<PlotFrame> frames)
        {
            var host = CadHostProvider.Host;
            foreach (var frame in frames)
            {
                bool accessible = host is ICadLayoutHost layouts
                    ? layouts.CanAccessFrameContext(frame)
                    : host is ICadContextHost current && current.IsFrameContextCurrent(frame);
                if (!accessible)
                    throw new InvalidOperationException("图框来源文档或布局已变化，请切回原文档并确认布局仍存在，或重新搜索。");
            }
        }

        private void CapturePlotSettings()
        {
            bool toPrinter = cboOutputMode.SelectedIndex == 7;
            _config.SendToPrinter = toPrinter;
            _config.ExportFormat = toPrinter ? PlotExportFormat.PDF : (PlotExportFormat)cboOutputMode.SelectedIndex;
            _config.Copies = toPrinter ? (int)numCopies.Value : 1;
            _config.PrinterDevice = cboPlotters.SelectedItem?.ToString() ?? "";
            _config.PlotStyleTable = cboPlotStyles.SelectedItem?.ToString() ?? "";
            _config.MarginMm = (double)numMargin.Value;
            _config.Margins = _pageMargins.Copy();
            _config.MergeToSinglePdf = !toPrinter && _config.ExportFormat == PlotExportFormat.PDF && chkMergePdf.Checked;
            _config.OverwriteExisting = chkOverwrite.Checked;
            CaptureStampAsset();
            if (string.IsNullOrWhiteSpace(_config.PrinterDevice)) throw new InvalidOperationException("请选择与输出类型匹配的设备。");
            if (string.IsNullOrWhiteSpace(_config.PlotStyleTable)) throw new InvalidOperationException("请选择有效的打印样式；原样式已不可用时必须重新选择，不能静默沿用布局样式。");
        }

        private async Task ExecuteBatchPlot()
        {
            if (_isPlotting) return;
            if (!CadHostProvider.IsInitialized)
            {
                MessageBox.Show("未连接到 CAD 宿主，无法打印。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            dgvDrawings.EndEdit();
            var selectedFrames = DisplayOrder().Where(f => f.IsSelected).ToList();
            if (selectedFrames.Count == 0)
            {
                MessageBox.Show("请先勾选需要打印的图框。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int successCount = 0, failedCount = 0, notSubmitted = 0;
            BatchPlotRun? executedRun=null;
            try
            {
                EnsureFrameContextsAccessible(selectedFrames);
                CapturePlotSettings();
                bool toPrinter = _config.SendToPrinter;
                var preparedFrames=PrepareStampFrames(selectedFrames);
                if (toPrinter && !(CadHostProvider.Plotter is ICadPrinter)) throw new NotSupportedException("当前宿主不支持实体打印机提交。");
                // 先验证所有图纸的数值计划，避免后面的非法页面导致前面已不可撤销地提交。
                foreach (var frame in selectedFrames) PlotPlanBuilder.Create(frame, _config);
                string outDir = toPrinter ? string.Empty : Path.GetFullPath(txtOutputFolder.Text);
                var planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var paths = selectedFrames.Select(f => toPrinter ? string.Empty : PlanOutputPath(outDir, f.CustomOutputFileName + PlotFileFormats.Extension(_config.ExportFormat), planned, _config.OverwriteExisting)).ToList();
                string mergedTarget = _config.MergeToSinglePdf
                    ? PlanOutputPath(outDir, txtMergedFileName.Text.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? txtMergedFileName.Text : txtMergedFileName.Text + ".pdf", planned, _config.OverwriteExisting)
                    : string.Empty;
                if (!toPrinter) Directory.CreateDirectory(outDir);
                _config.OutputDirectory = outDir;
                _config.MergedFileName = Path.GetFileName(mergedTarget);
                SetPlottingState(true);
                var plotter=CadHostProvider.Plotter!;
                var run=new BatchPlotRun(preparedFrames.Select((frame,index)=>new BatchPage(frame,paths[index])),_config);
                executedRun=run;
                if (run.Config.MergeToSinglePdf) PdfBookmarkItems.Create(run.Pages, run.Config); // 所有标题在提交 CAD 前验证。
                BeginPdfTaskRecord(run,mergedTarget);
                await RunPlotPages(run,selectedFrames,CadHostProvider.Host!,plotter);
                successCount=run.Pages.Count(p=>p.State==BatchPageState.Succeeded);
                failedCount=run.Pages.Count(p=>p.State==BatchPageState.Failed);
                notSubmitted=run.Pages.Count(p=>p.State==BatchPageState.Cancelled||p.State==BatchPageState.NotSubmitted);
                PdfMergeTaskResult? mergeResult = null;
                string sourceWarning = string.Empty;
                if (_config.MergeToSinglePdf && run.CanMerge)
                {
                    // 输出期间原图被编辑时仍合并本批成果，但明确提示用户复核，不静默吞掉。
                    if(_recordingTask!=null && _recordingTask.SourceRevisions.Count>0)
                    {
                        try { RequireRecordedSource(_recordingTask); }
                        catch (Exception ex) { sourceWarning = "\n注意：批量输出期间原图可能被修改（" + ex.Message + "），合并文件中的页面以各页输出时的图面为准，请复核。"; }
                    }
                    mergeResult = await MergeRecordedPdfsAsync(run, PdfBookmarkItems.Create(run.Pages, run.Config), mergedTarget);
                }
                bool mergeFailed = mergeResult != null && !mergeResult.Success && !mergeResult.Cancelled;
                bool mergeCancelled = mergeResult?.Cancelled == true;
                bool merged = mergeResult?.Success == true;
                string mergeError = mergeResult?.Error ?? string.Empty;
                string summary = toPrinter
                    ? $"设备提交结果：已提交 {successCount} 张，每张 {_config.Copies} 份，失败 {failedCount} 张，未提交 {notSubmitted} 张。\n提交成功不代表纸张已输出，请查看设备队列和实际纸张。"
                    : $"文件输出结果：成功 {successCount} 张，失败 {failedCount} 张，未执行 {notSubmitted} 张。";
                if (merged) summary += "\n完整批次已合并：" + mergedTarget + sourceWarning;
                else if (mergeCancelled) summary += "\nPDF 合并已取消；未提交合并文件，已有单页 PDF 保留。";
                else if (mergeFailed) summary += "\nPDF 合并失败：" + mergeError;
                else if (_config.MergeToSinglePdf) summary += "\n批次未全部成功或已取消，未生成合并文件；已有单页保留。";
                if (run.CancellationRequested) summary += merged
                    ? "\n取消请求到达时合并文件已经提交，已提交结果保留。"
                    : toPrinter ? "\n任务已取消；设备已接收的页面不能撤回。" : "\n任务已取消，已完成的单页 PDF 保留。";
                if (failedCount > 0)
                {
                    var firstFailed = run.Pages.FirstOrDefault(p => p.State == BatchPageState.Failed);
                    summary += $"\n请检查列表中的失败原因，成果尚不完整。\n\n【首个失败原因】：\n{firstFailed?.Error}";
                }
                string warnings=FormatSuccessWarnings(run.Pages.Where(p=>p.State==BatchPageState.Succeeded && !string.IsNullOrWhiteSpace(p.Error))
                    .Select(p=>$"第 {p.Frame.OrderIndex:D2} 页：{p.Error.Trim()}"));
                summary+=warnings;
                bool complete=failedCount==0 && notSubmitted==0 && !run.CancellationRequested && !mergeCancelled && !mergeFailed;
                string completionTitle=toPrinter?"任务已提交":"文件输出完成";
                if(warnings.Length>0)completionTitle+="（有警告）";
                lblStatus.Text = summary.Replace("\n", " ");
                MessageBox.Show(summary, complete?completionTitle:"任务未全部完成", MessageBoxButtons.OK,
                    complete && warnings.Length==0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                if (!toPrinter) OpenResultAfterRun(mergedTarget, merged, successCount);
            }
            catch (Exception ex)
            {
                if(executedRun!=null)
                {
                    successCount=executedRun.Pages.Count(p=>p.State==BatchPageState.Succeeded);
                    failedCount=executedRun.Pages.Count(p=>p.State==BatchPageState.Failed);
                }
                lblStatus.Text = "任务未完成：" + ex.Message;
                var firstFailed = executedRun?.Pages.FirstOrDefault(p => p.State == BatchPageState.Failed);
                var errDetail = firstFailed != null && !string.IsNullOrWhiteSpace(firstFailed.Error) ? $"\n\n【首个失败原因】：\n{firstFailed.Error}" : "";
                MessageBox.Show($"任务未完成：{ex.Message}\n页面接口返回成功 {successCount} 张，失败 {failedCount} 张。{errDetail}\n文件可能已生成而任务记录未保存，请核对输出目录；已有成果不会自动覆盖。", "打印错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndTaskHistoryRecording();
                FinishPlotTask();
                SetPlottingState(false);
                RefreshGrid();
            }
        }
    }
}



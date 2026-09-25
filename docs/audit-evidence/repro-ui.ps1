$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$uiDir = Join-Path $repo 'src\FastBatchPlot.UI\bin\Release\net8.0-windows'
Add-Type -AssemblyName System.Windows.Forms
foreach ($name in @('FastBatchPlot.Core', 'FastBatchPlot.CadBridge', 'FastBatchPlot.UI')) {
    Add-Type -Path (Join-Path $uiDir "$name.dll")
}
$flags = [System.Reflection.BindingFlags]'NonPublic,Instance'
$type = [FastBatchPlot.UI.Views.BatchPlotForm]
$form = [FastBatchPlot.UI.Views.BatchPlotForm]::new()
try {
    $frames = $type.GetField('_frames', $flags).GetValue($form)
    foreach ($number in @('B-02', 'A-01')) {
        $frame = [FastBatchPlot.Core.Models.PlotFrame]::new()
        $frame.OrderIndex = $frames.Count + 1
        $frame.TitleInfo.DrawingNo = $number
        $frames.Add($frame)
    }
    $type.GetMethod('RefreshGrid', $flags).Invoke($form, @()) | Out-Null
    $grid = $type.GetField('dgvDrawings', $flags).GetValue($form)
    $grid.Sort($grid.Columns[2], [System.ComponentModel.ListSortDirection]::Ascending)
    $visibleNumber = $grid.Rows[0].Cells[2].Value
    $grid.Rows[0].Cells[3].Value = '编辑第一行'
    [pscustomobject]@{
        Case = '按表头排序后修改第一行'
        VisibleDrawingNumber = $visibleNumber
        ActuallyEditedDrawingNumber = ($frames | Where-Object { $_.TitleInfo.DrawingName -eq '编辑第一行' }).TitleInfo.DrawingNo
    } | ConvertTo-Json -Compress
    $type.GetMethod('RefreshGrid', $flags).Invoke($form, @()) | Out-Null
    $grid.Rows[0].Cells[7].Value = '手工文件名'
    $before = $frames[0].CustomOutputFileName
    $type.GetMethod('RefreshGrid', $flags).Invoke($form, @()) | Out-Null
    [pscustomobject]@{ Case = '刷新列表丢失手工文件名'; Before = $before; After = $frames[0].CustomOutputFileName } | ConvertTo-Json -Compress
    $poly = [System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawPolylineCandidate]]::new()
    foreach ($offset in @(0, 2000)) {
        $candidate = [FastBatchPlot.Core.Detection.RawPolylineCandidate]::new()
        $width = if ($offset -eq 0) { 1189 } else { 297 }
        $height = if ($offset -eq 0) { 841 } else { 210 }
        foreach ($xy in @(@($offset,0),@(($offset+$width),0),@(($offset+$width),$height),@($offset,$height))) {
            $candidate.Vertices.Add([FastBatchPlot.Core.Common.Point2D]::new($xy[0],$xy[1]))
        }
        $poly.Add($candidate)
    }
    $blocks = [System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawBlockCandidate]]::new()
    $combine = $type.GetMethod('CombineAndFilterFrames', [System.Reflection.BindingFlags]'NonPublic,Static')
    $result = $combine.Invoke($null, @($poly,$blocks,''))
    [pscustomobject]@{ Case = '分开摆放的A0和A4'; Input = 2; Output = $result.Count; Papers = @($result | ForEach-Object { $_.DetectedPaper.Name }) } | ConvertTo-Json -Compress
    $first = [FastBatchPlot.Core.Paper.PaperSizeDetector]::Detect(841,594,1)
    $second = [FastBatchPlot.Core.Paper.PaperSizeDetector]::Detect(841,594,1)
    [pscustomobject]@{ Case = '两次检测共用可变图幅对象'; SameReference = [object]::ReferenceEquals($first.Paper,$second.Paper) } | ConvertTo-Json -Compress
    $savedName = $first.Paper.Name
    try {
        $first.Paper.Name = 'A3'
        [pscustomobject]@{ Case = '修改一张纸影响另一张及标准库'; OtherPaperName = $second.Paper.Name; StandardCatalogName = ([FastBatchPlot.Core.Models.PaperSize]::StandardSizes | Where-Object { $_.WidthMm -eq 841 }).Name } | ConvertTo-Json -Compress
    } finally { $first.Paper.Name = $savedName }
    $form.ShowInTaskbar = $false
    $form.Opacity = 0
    $form.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $bitmap = [System.Drawing.Bitmap]::new($form.Width,$form.Height)
    try { $form.DrawToBitmap($bitmap,[System.Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $PSScriptRoot 'ui-offline.png')) } finally { $bitmap.Dispose() }
    $export = $type.GetField('btnExportCatalog',$flags).GetValue($form)
    [pscustomobject]@{ Case = '默认窗口工具栏裁剪'; ExportButtonRight = $export.Right; ToolbarWidth = $export.Parent.ClientSize.Width; FullyVisible = ($export.Right -le $export.Parent.ClientSize.Width) } | ConvertTo-Json -Compress
    foreach ($field in @('btnStartPlot','txtNamingTemplate','txtMergedFileName')) {
        $control = $type.GetField($field,$flags).GetValue($form)
        $corner = $form.PointToClient($control.PointToScreen([System.Drawing.Point]::new(0,$control.Height)))
        [pscustomobject]@{ Case = '默认窗口底部控件'; Control = $field; BottomInForm = $corner.Y; FormClientHeight = $form.ClientSize.Height; InsideForm = ($corner.Y -le $form.ClientSize.Height) } | ConvertTo-Json -Compress
    }
    $form.Size = $form.MinimumSize
    $form.PerformLayout()
    [System.Windows.Forms.Application]::DoEvents()
    [pscustomobject]@{ Case = '最小允许窗口工具栏裁剪'; ExportButtonRight = $export.Right; ToolbarWidth = $export.Parent.ClientSize.Width; FullyVisible = ($export.Right -le $export.Parent.ClientSize.Width) } | ConvertTo-Json -Compress
} finally {
    $form.Dispose()
}

param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'PdfSharpCore.dll')
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$folder=Join-Path ([IO.Path]::GetTempPath()) ('PdfPackageCheck-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder) | Out-Null
try {
    $source=Join-Path $folder 'source.pdf'
    $single=Join-Path $folder 'single.pdf'
    $merged=Join-Path $folder 'merged.pdf'
    $pdf=New-Object PdfSharpCore.Pdf.PdfDocument
    try {
        $page=$pdf.AddPage()
        $page.Width=[PdfSharpCore.Drawing.XUnit]::FromMillimeter(420)
        $page.Height=[PdfSharpCore.Drawing.XUnit]::FromMillimeter(297)
        $pdf.Save($source)
    } finally {$pdf.Dispose()}
    $frame=New-Object FastBatchPlot.Core.Models.PlotFrame
    $frame.MaxX=420; $frame.MaxY=297; $frame.CalculatedScale=1
    $frame.DetectedPaper=New-Object FastBatchPlot.Core.Models.PaperSize('A3',420,297,$true)
    $config=New-Object FastBatchPlot.Core.Models.PlotConfig
    $plan=[FastBatchPlot.Core.Planning.PlotPlanBuilder]::Create($frame,$config)
    [FastBatchPlot.Core.Planning.PlotOutputCommitter]::ValidateAndCommit($source,$single,$plan)
    $items=New-Object 'System.Collections.Generic.List[FastBatchPlot.Core.Pdf.PdfMergeItem]'
    $frame.OrderIndex=7
    $frame.TitleInfo.DrawingName='总图/详图'
    $config.BookmarkTemplate='原任务/{Index:D3}:{DwgName} {Scale}'
    $title=[FastBatchPlot.Core.Naming.DrawingNameFormatter]::FormatBookmark($config.BookmarkTemplate,$frame)
    if($title -ne '原任务/007:总图/详图 1:1'){throw 'Bookmark formatting mismatch'}
    $items.Add((New-Object FastBatchPlot.Core.Pdf.PdfMergeItem($single,$title)))
    $mergeError=''
    if(-not [FastBatchPlot.Core.Pdf.PdfMerger]::MergePdfFiles($items,$merged,[ref]$mergeError,$false)){throw $mergeError}
    $read=[PdfSharpCore.Pdf.IO.PdfReader]::Open($merged,[PdfSharpCore.Pdf.IO.PdfDocumentOpenMode]::Import)
    try {if($read.Outlines[0].Title -ne $title){throw 'Merged bookmark mismatch'}} finally {$read.Dispose()}
    if(-not (Test-Path -LiteralPath $merged)){throw 'PDF output missing'}
    $frame.SourceDocumentId='offline-document'
    $frame.SourceLayoutId='offline-space'
    $config.OutputDirectory=$folder
    $pages=New-Object 'System.Collections.Generic.List[FastBatchPlot.Core.Tasks.BatchPage]'
    $pages.Add((New-Object FastBatchPlot.Core.Tasks.BatchPage($frame,$single)))
    $run=New-Object FastBatchPlot.Core.Tasks.BatchPlotRun($pages,$config)
    $record=[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Create($run,$merged)
    [FastBatchPlot.Core.Tasks.PdfTaskHistory]::RecordPage($record,0,[FastBatchPlot.Core.Tasks.BatchPageState]::Succeeded,'')
    $history=Join-Path $folder 'history.json'
    [FastBatchPlot.Core.Tasks.PdfTaskHistory]::Save($history,$record)
    $loaded=[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Load($history)
    if($loaded.Config.BookmarkTemplate -ne $config.BookmarkTemplate){throw 'History bookmark mismatch'}
    $restored=[FastBatchPlot.Core.Pdf.PdfBookmarkItems]::Create($run.Pages,$loaded.Config)
    if($restored[0].BookmarkTitle -ne $title){throw 'History bookmark items mismatch'}
    Write-Output ('PACKAGE_NET48_BOOKMARK_OK='+$Contents)
    [FastBatchPlot.Core.Tasks.PdfTaskHistory]::ValidateCompleted($loaded)
    if([FastBatchPlot.Core.Tasks.PdfTaskHistory]::PendingPages($loaded).Count -ne 0){throw 'Successful PDF scheduled again'}
    [IO.File]::AppendAllText($history,' {}')
    $rejected=$false
    try{[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Load($history) | Out-Null}catch{$rejected=$true}
    if(-not $rejected){throw 'Trailing history JSON was accepted'}
    Write-Output ('PACKAGE_NET48_HISTORY_OK='+$Contents)
    Write-Output ('PACKAGE_NET48_PDF_OK='+$Contents)
} finally {
    Get-ChildItem -LiteralPath $folder -File | ForEach-Object {Remove-Item -LiteralPath $_.FullName}
    Remove-Item -LiteralPath $folder
}

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
    $items.Add((New-Object FastBatchPlot.Core.Pdf.PdfMergeItem($single,'PDF')))
    $mergeError=''
    if(-not [FastBatchPlot.Core.Pdf.PdfMerger]::MergePdfFiles($items,$merged,[ref]$mergeError,$false)){throw $mergeError}
    if(-not (Test-Path -LiteralPath $merged)){throw 'PDF output missing'}
    Write-Output ('PACKAGE_NET48_PDF_OK='+$Contents)
} finally {
    Get-ChildItem -LiteralPath $folder -File | ForEach-Object {Remove-Item -LiteralPath $_.FullName}
    Remove-Item -LiteralPath $folder
}

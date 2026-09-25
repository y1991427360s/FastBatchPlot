param([Parameter(Mandatory=$true)][string]$Contents,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Contents 'FastBatchPlot.Core.dll')
$folder=Join-Path ([IO.Path]::GetTempPath()) ('FbpHistoryBench-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder) | Out-Null
$results=@()
try {
 foreach($count in @(10,100,500,1000)) {
  $pages=New-Object 'System.Collections.Generic.List[FastBatchPlot.Core.Tasks.BatchPage]'
  $config=New-Object FastBatchPlot.Core.Models.PlotConfig
  $config.OutputDirectory=$folder
  foreach($index in 1..$count) {
   $frame=New-Object FastBatchPlot.Core.Models.PlotFrame
   $frame.OrderIndex=$index;$frame.MaxX=420;$frame.MaxY=297;$frame.CalculatedScale=1
   $frame.DetectedPaper=New-Object FastBatchPlot.Core.Models.PaperSize('A3',420,297,$true)
   $frame.SourceDocumentId='offline-document';$frame.SourceLayoutId='offline-layout'
   $frame.TitleInfo.DrawingName='Test drawing';$frame.TitleInfo.DrawingNo='S-'+$index
   $pages.Add((New-Object FastBatchPlot.Core.Tasks.BatchPage($frame,(Join-Path $folder ('page'+$index+'.pdf')))))
  }
  $run=New-Object FastBatchPlot.Core.Tasks.BatchPlotRun($pages,$config)
  $watch=[Diagnostics.Stopwatch]::StartNew()
  $record=[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Create($run,(Join-Path $folder 'merged.pdf'))
  $create=$watch.Elapsed.TotalMilliseconds
  $path=Join-Path $folder ('task'+$count+'.json')
  $watch.Restart();[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Save($path,$record);$save=$watch.Elapsed.TotalMilliseconds
  $watch.Restart();$loaded=[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Load($path);$load=$watch.Elapsed.TotalMilliseconds
  $watch.Restart();[FastBatchPlot.Core.Tasks.PdfTaskHistory]::Save($path,$record);$update=$watch.Elapsed.TotalMilliseconds
  if($loaded.Pages.Count -ne $count -or $record.Revision -ne 2){throw 'History mismatch'}
  $results += [ordered]@{pages=$count;createMs=$create;firstSaveMs=$save;loadMs=$load;updateMs=$update;bytes=(Get-Item $path).Length}
  Write-Output ('HISTORY_BENCH pages='+$count+' createMs='+[int]$create+' updateMs='+[int]$update)
 }
 $results | ConvertTo-Json | Set-Content -LiteralPath $Output -Encoding UTF8
} finally {
 Get-ChildItem -LiteralPath $folder -File | ForEach-Object {Remove-Item -LiteralPath $_.FullName}
 [IO.Directory]::Delete($folder,$false)
}

param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$form=[FastBatchPlot.UI.Views.BatchPlotForm]::new()
try {
 $form.CreateControl();$flags=[Reflection.BindingFlags]'Instance,NonPublic'
 $candidate=[FastBatchPlot.Core.Detection.RawBlockCandidate]::new();$candidate.BlockName='long-frame';$candidate.Handle='BAD-FRAME';$candidate.SourceFileName='sample.dwg'
 $candidate.Bounds=[FastBatchPlot.Core.Common.Rect2D]::new(0,0,50000,10000)
 $blocks=[System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawBlockCandidate]]::new();$blocks.Add($candidate)
 $polys=[System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawPolylineCandidate]]::new()
 $result=$form.GetType().GetMethod('SelectCandidates',$flags).Invoke($form,[object[]]@($polys,$blocks))
 if($result.Frames.Count -ne 0 -or $result.Report.TotalCount -ne 1 -or -not $result.Report.ToText().Contains('BAD-FRAME')){throw 'Missing candidate diagnostics'}
 $warnings=[System.Collections.Generic.List[string]]::new();$warnings.Add('原始采集警告')
 $form.GetType().GetMethod('RecordDetectionReport',$flags).Invoke($form,[object[]]@($result,1,'离线搜索',$warnings))
 $dialog=$form.GetType().GetMethod('CreateDetectionReportDialog',$flags).Invoke($form,@())
 try {
  $dialog.CreateControl();$box=$dialog.Controls[0]
  if(-not $box.ReadOnly -or -not $box.Text.Contains('BAD-FRAME') -or -not $box.Text.Contains('原始采集警告')){throw 'Report dialog missing details'}
  Write-Output 'PACKAGE_NET48_DETECTION_REPORT_OK'
 } finally {$dialog.Dispose()}
} finally {$form.Dispose()}

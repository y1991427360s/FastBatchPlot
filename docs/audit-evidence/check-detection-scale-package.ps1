param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$form=[FastBatchPlot.UI.Views.BatchPlotForm]::new()
try {
 $form.CreateControl();$flags=[Reflection.BindingFlags]'Instance,NonPublic'
 $scale=$form.GetType().GetField('numDetectionScale',$flags).GetValue($form);$scale.Value=100
 $candidate=[FastBatchPlot.Core.Detection.RawBlockCandidate]::new();$candidate.BlockName='long-frame';$candidate.SourceDocumentId='doc';$candidate.SourceLayoutId='model'
 $candidate.Bounds=[FastBatchPlot.Core.Common.Rect2D]::new(0,0,50000,10000)
 $blocks=[System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawBlockCandidate]]::new();$blocks.Add($candidate)
 $polys=[System.Collections.Generic.List[FastBatchPlot.Core.Detection.RawPolylineCandidate]]::new()
 $result=$form.GetType().GetMethod('SelectCandidates',$flags).Invoke($form,[object[]]@($polys,$blocks))
 if($result.Frames.Count -ne 1 -or $result.Frames[0].CalculatedScale -ne 100 -or $result.Frames[0].DetectedPaper.WidthMm -ne 500 -or $result.Frames[0].DetectedPaper.HeightMm -ne 100){throw 'Detection scale did not reach frame pipeline'}
 Write-Output 'PACKAGE_NET48_DETECTION_SCALE_OK=50000x10000 at 1:100 becomes 500x100 mm'
} finally {$form.Dispose()}

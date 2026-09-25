param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$settingsPath=Join-Path ([IO.Path]::GetTempPath()) ('filter-options-'+[Guid]::NewGuid().ToString('N')+'.json')
$form=[FastBatchPlot.UI.Views.BatchPlotForm]::new($settingsPath)
try {
 $form.CreateControl()
 $flags=[Reflection.BindingFlags]'Instance,NonPublic'
 $duplicates=$form.GetType().GetField('chkRemoveDuplicates',$flags).GetValue($form)
 $nested=$form.GetType().GetField('chkRemoveNestedFrames',$flags).GetValue($form)
 if(-not $duplicates.Checked -or -not $nested.Checked){throw 'Default filters not enabled'}
 $duplicates.Checked=$false; $nested.Checked=$false
 $polys=[Collections.Generic.List[FastBatchPlot.Core.Detection.RawPolylineCandidate]]::new()
 foreach($id in @('one','two')) {
  $p=[FastBatchPlot.Core.Detection.RawPolylineCandidate]::new();$p.Handle=$id
  foreach($xy in @(@(0,0),@(420,0),@(420,297),@(0,297))){$p.Vertices.Add([FastBatchPlot.Core.Common.Point2D]::new($xy[0],$xy[1]))}
  $polys.Add($p)
 }
 $blocks=[Collections.Generic.List[FastBatchPlot.Core.Detection.RawBlockCandidate]]::new()
 $method=$form.GetType().GetMethod('SelectCandidates',$flags)
 $result=$method.Invoke($form,[object[]]@($polys,$blocks))
 if($result.Frames.Count -ne 2){throw 'Disabled duplicate filter discarded a candidate'}
 $duplicates.Checked=$true
 $result=$method.Invoke($form,[object[]]@($polys,$blocks))
 if($result.Frames.Count -ne 1){throw 'Enabled duplicate filter failed'}
 Write-Output 'PACKAGE_NET48_FILTER_OPTIONS_OK'
} finally {$form.Dispose()}

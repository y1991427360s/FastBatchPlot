param([Parameter(Mandatory=$true)][string]$Contents)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
foreach($name in @('FastBatchPlot.Core.dll','FastBatchPlot.CadBridge.dll','FastBatchPlot.UI.dll')){Add-Type -Path (Join-Path $Contents $name)}
$form=[FastBatchPlot.UI.Views.BatchPlotForm]::new()
$path=[IO.Path]::GetTempFileName()
try {
 $form.CreateControl()
 [IO.File]::WriteAllText($path,'AAAA')
 $stamp=[IO.File]::GetLastWriteTimeUtc($path)
 $before=[FastBatchPlot.Core.Tasks.PlotResourceRevision]::Compute([string[]]@($path))
 [IO.File]::WriteAllText($path,'BBBB');[IO.File]::SetLastWriteTimeUtc($path,$stamp)
 $after=[FastBatchPlot.Core.Tasks.PlotResourceRevision]::Compute([string[]]@($path))
 if($before -eq $after){throw 'Resource change not detected'}
 $property=[FastBatchPlot.Core.Tasks.PdfTaskRecord].GetProperty('PlotResourceRevision')
 if($null -eq $property){throw 'History field missing'}
 Write-Output 'PACKAGE_NET48_PLOT_RESOURCES_OK'
} finally {$form.Dispose();[IO.File]::Delete($path)}

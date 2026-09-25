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
 $before=[FastBatchPlot.Core.Tasks.ExternalFileRevision]::Read($path)
 [IO.File]::WriteAllText($path,'BBBB');[IO.File]::SetLastWriteTimeUtc($path,$stamp)
 $after=[FastBatchPlot.Core.Tasks.ExternalFileRevision]::Read($path)
 if($before -eq $after){throw 'Same-size replacement not detected'}
 $writer=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
 try {
  $rejected=$false
  try {$null=[FastBatchPlot.Core.Tasks.ExternalFileRevision]::Read($path)} catch {$rejected=$true}
  if(-not $rejected){throw 'Concurrent writer not rejected'}
 } finally {$writer.Dispose()}
 Write-Output 'PACKAGE_NET48_XREF_DIGEST_OK'
} finally {$form.Dispose();[IO.File]::Delete($path)}
